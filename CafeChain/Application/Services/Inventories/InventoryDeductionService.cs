using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Constants;
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
        private readonly IUnitConversionService _unitConversion;
        private readonly IStockAlertService? _stockAlertService;

        // Giới hạn tối đa 5 tầng BOM — đồng bộ với AdminRecipeService
        private const int MAX_BOM_DEPTH = 5;

        public InventoryDeductionService(
            AppDbContext context,
            ILogger<InventoryDeductionService> logger,
            IUnitConversionService unitConversion,
            IStockAlertService? stockAlertService = null)
        {
            _context = context;
            _logger = logger;
            _unitConversion = unitConversion;
            _stockAlertService = stockAlertService;
        }

        // ============================================================
        // COGS: Tính giá vốn an toàn — Có Depth Limit + Cycle Guard
        // ============================================================
        public async Task<ServiceResult<decimal>> CalculateRecipeCogsAsync(int recipeId)
        {
            return await CalculateRecipeCogsInternalAsync(recipeId, new HashSet<int>(), 0);
        }

        /// <summary>
        /// Internal recursive COGS calculation với Cycle Guard và Depth Limit.
        /// Missing/invalid unit conversion → Failure (never understated success total).
        /// </summary>
        private async Task<ServiceResult<decimal>> CalculateRecipeCogsInternalAsync(
            int recipeId, HashSet<int> visited, int depth)
        {
            // GUARD 1: Depth Limit — tránh StackOverflow
            if (depth > MAX_BOM_DEPTH)
            {
                _logger.LogWarning(
                    "Tính COGS vượt quá {MaxDepth} tầng cho Recipe #{RecipeId}.",
                    MAX_BOM_DEPTH, recipeId);
                return ServiceResult<decimal>.Failure(
                    $"Tính COGS vượt quá {MAX_BOM_DEPTH} tầng cho Recipe #{recipeId}.");
            }

            // GUARD 2: Cycle Detection — tránh đệ quy vô hạn
            if (!visited.Add(recipeId))
            {
                _logger.LogWarning(
                    "Phát hiện vòng lặp khi tính COGS tại Recipe #{RecipeId}.",
                    recipeId);
                return ServiceResult<decimal>.Failure(
                    $"Phát hiện vòng lặp khi tính COGS tại Recipe #{recipeId}.");
            }

            // KHÔNG lọc theo Status/Active — phải tính được COGS cho cả Recipe đã Archived
            // để bảo vệ dữ liệu lịch sử (VD: đơn hàng cũ cần tra cứu giá vốn)
            var recipe = await _context.Recipes
                .Include(r => r.RecipeDetails)
                .FirstOrDefaultAsync(r => r.RecipeId == recipeId);

            if (recipe == null)
                return ServiceResult<decimal>.Success(0m);

            decimal totalCost = 0;

            foreach (var detail in recipe.RecipeDetails)
            {
                decimal detailCost = 0;

                if (detail.IngredientId.HasValue)
                {
                    var supplier = await _context.IngredientSuppliers
                        .Where(s => s.IngredientId == detail.IngredientId.Value)
                        .OrderByDescending(s => s.IsPrimary)
                        .FirstOrDefaultAsync();

                    decimal unitPrice = supplier?.CurrentPrice ?? 0;

                    var converted = await ConvertQuantityToBaseUnitAsync(
                        detail.IngredientId.Value, detail.Quantity, detail.UnitId);
                    if (!converted.ok)
                    {
                        _logger.LogError(
                            "COGS conversion failure IngredientId={IngredientId}: {Error}",
                            detail.IngredientId, converted.error);
                        return ServiceResult<decimal>.Failure(
                            converted.error
                            ?? $"Thiếu quy đổi đơn vị khi tính COGS cho nguyên liệu #{detail.IngredientId}.");
                    }

                    detailCost = unitPrice * converted.qty;
                }
                else if (detail.ChildRecipeId.HasValue)
                {
                    var childResult = await CalculateRecipeCogsInternalAsync(
                        detail.ChildRecipeId.Value, visited, depth + 1);
                    if (!childResult.IsSuccess)
                        return childResult;

                    detailCost = childResult.Data * detail.Quantity;
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

            return ServiceResult<decimal>.Success(totalCost);
        }

        // ============================================================
        // Unit Conversion via shared service (fail-closed)
        // ============================================================
        private async Task<(bool ok, decimal qty, string? error)> ConvertQuantityToBaseUnitAsync(
            int ingredientId, decimal quantity, int fromUnitId)
        {
            var result = await _unitConversion.ConvertAsync(ingredientId, quantity, fromUnitId);
            if (!result.IsSuccess)
                return (false, 0m, result.Message);
            return (true, result.Data, null);
        }

        // ============================================================
        // DEDUCT: Xuất kho bán hàng (giữ nguyên logic, bổ sung UnitConversion)
        // ============================================================
        public async Task<ServiceResult> DeductStockForOrderAsync(List<POSSoldItemDto> soldItems, int storeId)
        {
            return await DeductStockForOrderInternalAsync(soldItems, storeId, null);
        }

        /// <summary>
        /// Trừ kho cho một Order đã commit/paid. Idempotent theo ReferenceOrderId.
        /// </summary>
        public async Task<ServiceResult> DeductStockForCommittedOrderAsync(
            List<POSSoldItemDto> soldItems,
            int storeId,
            int referenceOrderId)
        {
            if (referenceOrderId <= 0)
                return ServiceResult.Failure("Thiếu mã đơn hàng đã commit để trừ kho.");

            return await DeductStockForOrderInternalAsync(soldItems, storeId, referenceOrderId);
        }

        private async Task<ServiceResult> DeductStockForOrderInternalAsync(
            List<POSSoldItemDto> soldItems,
            int storeId,
            int? referenceOrderId)
        {
            if (soldItems == null || !soldItems.Any())
                return ServiceResult.Failure("Không có sản phẩm nào để xuất kho.");

            // Issue #65: Thu thập cảnh báo thiếu kho — không block đơn hàng
            var inventoryWarnings = new List<string>();

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (referenceOrderId.HasValue)
                {
                    var orderCanDeduct = await _context.Orders
                        .AsNoTracking()
                        .AnyAsync(order =>
                            order.OrderId == referenceOrderId.Value &&
                            order.StoreId == storeId &&
                            order.OrderStatusId == SystemConstants.OrderStatuses.Completed &&
                            order.PaymentStatusId == SystemConstants.PaymentStatuses.Paid);

                    if (!orderCanDeduct)
                    {
                        await transaction.RollbackAsync();
                        return ServiceResult.Failure("Chỉ trừ kho cho đơn POS đã thanh toán và đã commit.");
                    }

                    var alreadyDeducted = await _context.InventoryTransactions
                        .AsNoTracking()
                        .AnyAsync(inventoryTransaction =>
                            inventoryTransaction.ReferenceOrderId == referenceOrderId.Value &&
                            inventoryTransaction.Type == InventoryTransactionTypeEnum.SALES_DEDUCTION);

                    if (alreadyDeducted)
                    {
                        await transaction.CommitAsync();
                        return ServiceResult.Success("Đơn hàng đã được trừ kho trước đó.");
                    }
                }

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
                            inventoryWarnings,
                            referenceOrderId);
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
                            inventoryWarnings,
                            referenceOrderId);
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Issue #97: evaluate stock alerts after qty is committed (never fail deduction).
                await EvaluateStockAlertsSafeAsync(storeId, referenceOrderId);

                // Trả success kèm warnings (nếu có) — đơn hàng KHÔNG bị reject
                if (inventoryWarnings.Any())
                {
                    var result = ServiceResult.Success(
                        $"Trừ kho thành công. Cảnh báo: {inventoryWarnings.Count} nguyên liệu tồn kho âm.");
                    result.Errors = inventoryWarnings;  // Dùng Errors để chở warnings — controller check IsSuccess
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

        /// <summary>
        /// Issue #97 — post-commit stock alert evaluation. Failures are logged only.
        /// Idempotent deduction is unchanged: alerts run only after successful commit.
        /// Offline sync and online POS share this path (same DeductStock* entrypoints).
        /// </summary>
        private async Task EvaluateStockAlertsSafeAsync(int storeId, int? referenceOrderId)
        {
            if (_stockAlertService == null)
                return;

            try
            {
                var source = referenceOrderId.HasValue
                    ? StockAlertSources.PosSale
                    : StockAlertSources.PosSale;

                var alertResult = await _stockAlertService.EvaluateStoreAsync(storeId, source);
                if (!alertResult.IsSuccess)
                {
                    _logger.LogWarning(
                        "[InventoryDeduction] Stock alert evaluation failed for StoreId={StoreId}: {Message}",
                        storeId,
                        alertResult.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "[InventoryDeduction] Stock alert evaluation threw for StoreId={StoreId}",
                    storeId);
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
            List<string> inventoryWarnings,
            int? referenceOrderId)
        {
            // STRICT OPTION B: Xuất thẳng Bán Thành Phẩm / Nguyên Liệu, KHÔNG bóc tách đệ quy
            foreach (var detail in recipe.RecipeDetails)
            {
                decimal requiredQty = detail.Quantity * soldQuantity;

                decimal convertedQty;
                if (detail.IngredientId.HasValue)
                {
                    var converted = await ConvertQuantityToBaseUnitAsync(
                        detail.IngredientId.Value, requiredQty, detail.UnitId);
                    if (!converted.ok)
                    {
                        // Do not silently deduct raw quantity — surface as failure via exception
                        // so outer transaction rolls back and order path can retry after data fix.
                        throw new InvalidOperationException(
                            converted.error ??
                            $"Thiếu quy đổi đơn vị cho nguyên liệu #{detail.IngredientId}.");
                    }

                    convertedQty = converted.qty;
                }
                else
                {
                    convertedQty = requiredQty;
                }

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
                    Type = InventoryTransactionTypeEnum.SALES_DEDUCTION,
                    StockStatus = inventoryItem.AvailableQty < 0
                        ? InventoryStockStatus.NEGATIVE_CONFIRMED
                        : InventoryStockStatus.NORMAL,
                    Quantity = convertedQty,
                    BeforeQty = beforeQty,
                    AfterQty = inventoryItem.AvailableQty,
                    ReferenceOrderId = referenceOrderId,
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
