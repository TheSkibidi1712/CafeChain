using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Transactions;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CafeChain.Application.Services.Inventories
{
    public class InventoryDeductionService : IInventoryDeductionService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<InventoryDeductionService> _logger;

        // Giới hạn tối đa 5 tầng BOM — đồng bộ với AdminRecipeService
        private const int MAX_BOM_DEPTH = 5;

        public InventoryDeductionService(AppDbContext context, ILogger<InventoryDeductionService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ============================================================
        // COGS: Tính giá vốn an toàn — Có Depth Limit + Cycle Guard
        // ============================================================
        public async Task<decimal> CalculateRecipeCogsAsync(int recipeId)
        {
            // Khởi tạo visited set để phát hiện vòng lặp
            return await CalculateRecipeCogsInternalAsync(recipeId, new HashSet<int>(), 0);
        }

        /// <summary>
        /// Internal recursive COGS calculation với Cycle Guard và Depth Limit.
        /// </summary>
        private async Task<decimal> CalculateRecipeCogsInternalAsync(
            int recipeId, HashSet<int> visited, int depth)
        {
            // GUARD 1: Depth Limit — tránh StackOverflow
            if (depth > MAX_BOM_DEPTH)
            {
                _logger.LogWarning(
                    "Tính COGS vượt quá {MaxDepth} tầng cho Recipe #{RecipeId}. Trả về 0.",
                    MAX_BOM_DEPTH, recipeId);
                return 0;
            }

            // GUARD 2: Cycle Detection — tránh đệ quy vô hạn
            if (!visited.Add(recipeId))
            {
                _logger.LogWarning(
                    "Phát hiện vòng lặp khi tính COGS tại Recipe #{RecipeId}. Trả về 0.",
                    recipeId);
                return 0;
            }

            // KHÔNG lọc theo Status/Active — phải tính được COGS cho cả Recipe đã Archived
            // để bảo vệ dữ liệu lịch sử (VD: đơn hàng cũ cần tra cứu giá vốn)
            var recipe = await _context.Recipes
                .Include(r => r.RecipeDetails)
                .FirstOrDefaultAsync(r => r.RecipeId == recipeId);

            if (recipe == null) return 0;

            decimal totalCost = 0;

            foreach (var detail in recipe.RecipeDetails)
            {
                decimal detailCost = 0;

                if (detail.IngredientId.HasValue)
                {
                    // Lấy giá vốn từ Supplier (Supplier chính hoặc đầu tiên)
                    var supplier = await _context.IngredientSuppliers
                        .Where(s => s.IngredientId == detail.IngredientId.Value)
                        .OrderByDescending(s => s.IsPrimary)
                        .FirstOrDefaultAsync();

                    decimal unitPrice = supplier?.CurrentPrice ?? 0;

                    // FIX #4: Apply Unit Conversion — quy đổi về BaseUnit trước khi nhân giá
                    decimal convertedQty = await ConvertQuantityToBaseUnitAsync(
                        detail.IngredientId.Value, detail.Quantity, detail.UnitId);

                    detailCost = unitPrice * convertedQty;
                }
                else if (detail.ChildRecipeId.HasValue)
                {
                    // Đệ quy tính giá vốn của Bán Thành Phẩm với depth + 1
                    decimal childCost = await CalculateRecipeCogsInternalAsync(
                        detail.ChildRecipeId.Value, visited, depth + 1);
                    detailCost = childCost * detail.Quantity;
                }

                totalCost += detailCost;
            }

            // YieldPercentage logic:
            //   100% = không hao hụt (mặc định, không điều chỉnh)
            //   < 100% (VD: 95%) = hao hụt 5% → chi phí tăng: totalCost / 0.95
            //   > 100% (VD: 110%) = nở ra 10% → chi phí giảm: totalCost / 1.10
            //   <= 0% = KHÔNG HỢP LỆ → bỏ qua để tránh Division by Zero
            if (recipe.YieldPercentage > 0 && recipe.YieldPercentage != 100)
            {
                totalCost = totalCost / (recipe.YieldPercentage / 100m);
            }
            else if (recipe.YieldPercentage <= 0)
            {
                _logger.LogError(
                    "YieldPercentage = {Yield}% cho Recipe #{RecipeId} — không hợp lệ, bỏ qua điều chỉnh hao hụt.",
                    recipe.YieldPercentage, recipeId);
            }
            // YieldPercentage == 100 → không điều chỉnh (default)

            return totalCost;
        }

        // ============================================================
        // FIX #4: Unit Conversion — Quy đổi Quantity về BaseUnit
        // ============================================================
        /// <summary>
        /// Chuyển đổi quantity từ fromUnitId về BaseUnit của Ingredient.
        /// Nếu đã ở BaseUnit hoặc không tìm thấy conversion → trả về nguyên bản.
        /// </summary>
        private async Task<decimal> ConvertQuantityToBaseUnitAsync(
            int ingredientId, decimal quantity, int fromUnitId)
        {
            var ingredient = await _context.Ingredients.FindAsync(ingredientId);
            if (ingredient == null || fromUnitId == ingredient.BaseUnitId)
                return quantity; // Đã ở BaseUnit, không cần convert

            // Tìm conversion trực tiếp: fromUnitId → BaseUnit
            var conversion = await _context.UnitConversions
                .FirstOrDefaultAsync(uc =>
                    uc.IngredientId == ingredientId &&
                    uc.FromUnitId == fromUnitId &&
                    uc.ToUnitId == ingredient.BaseUnitId);

            if (conversion != null && conversion.FromQuantity > 0)
            {
                return quantity * (conversion.ToQuantity / conversion.FromQuantity);
            }

            // Thử chiều ngược: BaseUnit → fromUnitId (đảo công thức)
            var reverseConversion = await _context.UnitConversions
                .FirstOrDefaultAsync(uc =>
                    uc.IngredientId == ingredientId &&
                    uc.FromUnitId == ingredient.BaseUnitId &&
                    uc.ToUnitId == fromUnitId);

            if (reverseConversion != null && reverseConversion.ToQuantity > 0)
            {
                return quantity * (reverseConversion.FromQuantity / reverseConversion.ToQuantity);
            }

            // Không tìm thấy conversion → log warning, trả về nguyên bản
            _logger.LogWarning(
                "Không tìm thấy tỷ lệ chuyển đổi UnitId {FromUnit} → BaseUnit cho Ingredient #{IngId}. Dùng quantity nguyên bản.",
                fromUnitId, ingredientId);
            return quantity;
        }

        // ============================================================
        // DEDUCT: Xuất kho bán hàng (giữ nguyên logic, bổ sung UnitConversion)
        // ============================================================
        public async Task<ServiceResult> DeductStockForOrderAsync(List<POSSoldItemDto> soldItems, int storeId)
        {
            if (soldItems == null || !soldItems.Any())
                return ServiceResult.Failure("Không có sản phẩm nào để xuất kho.");

            // Issue #65: Thu thập cảnh báo thiếu kho — không block đơn hàng
            var inventoryWarnings = new List<string>();

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var item in soldItems)
                {
                    var drinkRecipe = await GetActiveRecipeAsync(item.DrinkId, item.SizeId, null);
                    if (drinkRecipe == null)
                    {
                        _logger.LogWarning(
                            "Không tìm thấy công thức (BOM) hoạt động cho DrinkId={DrinkId}, SizeId={SizeId}",
                            item.DrinkId,
                            item.SizeId);
                    }
                    else
                    {
                        await DeductRecipeDetailsAsync(
                            drinkRecipe,
                            item.Quantity,
                            storeId,
                            inventoryWarnings);
                    }

                    foreach (var topping in item.Toppings ?? new List<POSOrderToppingDto>())
                    {
                        var toppingRecipe = await GetActiveRecipeAsync(null, null, topping.ToppingId);
                        if (toppingRecipe == null)
                        {
                            _logger.LogWarning(
                                "Không tìm thấy công thức (BOM) hoạt động cho ToppingId={ToppingId}",
                                topping.ToppingId);
                            continue;
                        }

                        await DeductRecipeDetailsAsync(
                            toppingRecipe,
                            item.Quantity,
                            storeId,
                            inventoryWarnings);
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Trả success kèm warnings (nếu có) — đơn hàng KHÔNG bị reject
                if (inventoryWarnings.Any())
                {
                    var result = ServiceResult.Success(
                        $"Trừ kho thành công. Cảnh báo: {inventoryWarnings.Count} nguyên liệu tồn kho âm.");
                    result.Errors = inventoryWarnings;  // Dùng Errors để chứa warnings — controller check IsSuccess
                    return result;
                }
                return ServiceResult.Success("Trừ kho bán hàng thành công.");
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi tranh chấp dữ liệu khi trừ kho.");
                return ServiceResult.Failure("Lỗi hệ thống: Có nhiều giao dịch đồng thời đang tranh chấp kho. Vui lòng thử lại.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi xuất kho bán hàng.");
                return ServiceResult.Failure($"Lỗi xuất kho: {ex.Message}");
            }
        }

        private async Task<Recipe?> GetActiveRecipeAsync(int? drinkId, int? sizeId, int? toppingId)
        {
            var query = _context.Recipes
                .Include(r => r.RecipeDetails)
                .Where(r => r.Active && r.Status == "Active");

            if (drinkId.HasValue)
            {
                var sizedRecipe = await query
                    .FirstOrDefaultAsync(r => r.DrinkId == drinkId.Value
                                           && r.SizeId == sizeId
                                           && r.ToppingId == null);

                if (sizedRecipe != null)
                    return sizedRecipe;

                return await query
                    .FirstOrDefaultAsync(r => r.DrinkId == drinkId.Value
                                           && r.SizeId == null
                                           && r.ToppingId == null);
            }

            if (toppingId.HasValue)
            {
                return await query
                    .FirstOrDefaultAsync(r => r.ToppingId == toppingId.Value
                                           && r.DrinkId == null);
            }

            return null;
        }

        private async Task DeductRecipeDetailsAsync(
            Recipe recipe,
            int soldQuantity,
            int storeId,
            List<string> inventoryWarnings)
        {
            // STRICT OPTION B: Xuất thẳng Bán Thành Phẩm / Nguyên Liệu, KHÔNG bóc tách đệ quy
            foreach (var detail in recipe.RecipeDetails)
            {
                decimal requiredQty = detail.Quantity * soldQuantity;

                // FIX #4: Apply Unit Conversion cho xuất kho
                decimal convertedQty = detail.IngredientId.HasValue
                    ? await ConvertQuantityToBaseUnitAsync(
                        detail.IngredientId.Value, requiredQty, detail.UnitId)
                    : requiredQty;

                var inventoryItem = await GetOrCreateInventoryItem(storeId, detail.IngredientId, detail.ChildRecipeId);

                decimal beforeQty = inventoryItem.AvailableQty;
                inventoryItem.AvailableQty -= convertedQty; // Cho phép âm kho (Soft-block)

                if (inventoryItem.AvailableQty < 0)
                {
                    var itemName = detail.IngredientId.HasValue
                        ? (await _context.Ingredients.FindAsync(detail.IngredientId.Value))?.Name ?? $"Ingredient #{detail.IngredientId}"
                        : $"Recipe #{detail.ChildRecipeId}";

                    inventoryWarnings.Add(
                        $"⚠️ {itemName}: tồn kho âm ({inventoryItem.AvailableQty:N2}), " +
                        $"trước xuất: {beforeQty:N2}, xuất: {convertedQty:N2}");

                    _logger.LogWarning(
                        "[InventoryDeduction] Kho âm — StoreId={StoreId}, Item={ItemName}, " +
                        "Before={Before:N2}, Deducted={Deducted:N2}, After={After:N2}",
                        storeId, itemName, beforeQty, convertedQty, inventoryItem.AvailableQty);
                }

                _context.InventoryTransactions.Add(new InventoryTransaction
                {
                    StoreInventoryId = inventoryItem.StoreInventoryId,
                    Type = InventoryDocumentType.SALES_DEDUCTION,
                    Quantity = -convertedQty,
                    BeforeQty = beforeQty,
                    AfterQty = inventoryItem.AvailableQty,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        private async Task<StoreInventory> GetOrCreateInventoryItem(int storeId, int? ingredientId, int? recipeId)
        {
            var item = await _context.StoreInventories
                .FirstOrDefaultAsync(i => i.StoreId == storeId && i.IngredientId == ingredientId && i.RecipeId == recipeId);
                
            if (item == null)
            {
                item = new StoreInventory 
                { 
                    StoreId = storeId, 
                    IngredientId = ingredientId, 
                    RecipeId = recipeId, 
                    AvailableQty = 0,
                    ReservedQty = 0,
                    LastUpdated = DateTime.UtcNow
                };
                _context.StoreInventories.Add(item);
                await _context.SaveChangesAsync(); // Cần Save ngay để có StoreInventoryId cho log Transaction
            }
            return item;
        }
    }
}
