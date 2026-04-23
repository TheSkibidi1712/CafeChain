using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces;
using CafeChain.Data;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories;
using CafeChain.Models.Stores;
using CafeChain.ViewModels.Cart;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CafeChain.Application.Services.Inventory
{
    public class InventoryService : IInventoryService
    {
        private readonly AppDbContext _context;

        public InventoryService(AppDbContext context)
        {
            _context = context;
        }

        // ============================================================
        // RESERVE: Giữ chỗ tồn kho khi đặt đơn Online
        // ============================================================
        public async Task ReserveInventoryForOrderAsync(int storeId, List<CartItemViewModel> items)
        {
            var requiredIngredients = await CalculateRequiredIngredientsAsync(items);
            if (!requiredIngredients.Any()) return;

            var ingredientIds = requiredIngredients.Keys.ToList();
            var inventories = await _context.StoreInventories
                .Where(si => si.StoreId == storeId && si.IngredientId.HasValue && ingredientIds.Contains(si.IngredientId.Value))
                .ToListAsync();

            foreach (var req in requiredIngredients)
            {
                var inv = inventories.FirstOrDefault(i => i.IngredientId == req.Key);
                if (inv == null || inv.AvailableQty < req.Value)
                {
                    var ingredientName = await _context.Ingredients
                        .Where(i => i.IngredientId == req.Key)
                        .Select(i => i.Name)
                        .FirstOrDefaultAsync() ?? "Nguyên liệu #" + req.Key;

                    throw new Exception($"Không đủ hàng trong kho cho nguyên liệu: {ingredientName}. Cần {req.Value:N2}, hiện có {inv?.AvailableQty ?? 0:N2}");
                }

                // Reservation logic
                inv.AvailableQty -= req.Value;
                inv.ReservedQty += req.Value;
                inv.LastUpdated = DateTime.Now;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new Exception("Kho hàng đang có thay đổi bởi giao dịch khác. Vui lòng thử lại sau giây lát.");
            }
        }

        // ============================================================
        // RELEASE: Hoàn trả tồn kho khi hủy đơn
        // ============================================================
        public async Task ReleaseInventoryForOrderAsync(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                .ThenInclude(od => od.OrderToppings)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null) return;

            var items = order.OrderDetails.Select(od => new CartItemViewModel
            {
                DrinkId = od.DrinkId,
                Quantity = od.Quantity,
                ToppingIds = od.OrderToppings.Select(ot => ot.ToppingId).ToList()
            }).ToList();

            var requiredIngredients = await CalculateRequiredIngredientsAsync(items);
            if (!requiredIngredients.Any()) return;

            var ingredientIds = requiredIngredients.Keys.ToList();
            var inventories = await _context.StoreInventories
                .Where(si => si.StoreId == order.StoreId && si.IngredientId.HasValue && ingredientIds.Contains(si.IngredientId.Value))
                .ToListAsync();

            foreach (var req in requiredIngredients)
            {
                var inv = inventories.FirstOrDefault(i => i.IngredientId == req.Key);
                if (inv != null)
                {
                    inv.ReservedQty = Math.Max(0, inv.ReservedQty - req.Value);
                    inv.AvailableQty += req.Value;
                    inv.LastUpdated = DateTime.Now;
                }
            }

            await _context.SaveChangesAsync();
        }

        // ============================================================
        // [MISSION 2] CONFIRM DEDUCTION: Trừ kho thực tế khi Hoàn thành
        // STRICT OPTION B: Trừ bán thành phẩm trực tiếp, không explode
        // ============================================================
        public async Task ConfirmInventoryDeductionAsync(int orderId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Load đơn hàng + chi tiết
                var order = await _context.Orders
                    .Include(o => o.OrderDetails)
                        .ThenInclude(od => od.OrderToppings)
                    .FirstOrDefaultAsync(o => o.OrderId == orderId);

                if (order == null)
                    throw new Exception($"Không tìm thấy đơn hàng #{orderId} để trừ kho.");

                // 2. Xác định loại đơn: POS (DineIn/TakeAway) vs Online (Delivery)
                bool isPOS = order.OrderTypeId == SystemConstants.OrderTypes.DineIn
                          || order.OrderTypeId == SystemConstants.OrderTypes.TakeAway;

                // 3. Lấy tất cả Recipe active liên quan đến Drink/Topping trong đơn
                var drinkIds = order.OrderDetails.Select(od => od.DrinkId).Distinct().ToList();
                var toppingIds = order.OrderDetails
                    .SelectMany(od => od.OrderToppings.Select(ot => ot.ToppingId))
                    .Distinct().ToList();

                var recipes = await _context.Recipes
                    .Include(r => r.RecipeDetails)
                    .Where(r => r.Status == "Active" &&
                        ((r.DrinkId.HasValue && drinkIds.Contains(r.DrinkId.Value)) ||
                         (r.ToppingId.HasValue && toppingIds.Contains(r.ToppingId.Value))))
                    .ToListAsync();

                // 4. Tính danh sách trừ kho — OPTION B: không explode sub-recipe
                var deductions = new List<InventoryDeductionItem>();

                foreach (var orderDetail in order.OrderDetails)
                {
                    // Tìm BOM cho món nước này
                    var drinkRecipe = recipes.FirstOrDefault(r => r.DrinkId == orderDetail.DrinkId);
                    if (drinkRecipe != null)
                    {
                        await BuildDeductionListOptionB(
                            drinkRecipe.RecipeDetails, orderDetail.Quantity,
                            order.StoreId, deductions);
                    }

                    // Tìm BOM cho từng topping
                    foreach (var ot in orderDetail.OrderToppings)
                    {
                        var toppingRecipe = recipes.FirstOrDefault(r => r.ToppingId == ot.ToppingId);
                        if (toppingRecipe != null)
                        {
                            await BuildDeductionListOptionB(
                                toppingRecipe.RecipeDetails, orderDetail.Quantity,
                                order.StoreId, deductions);
                        }
                    }
                }

                // 5. Thực thi trừ kho + ghi log InventoryTransaction
                foreach (var deduction in deductions)
                {
                    var inv = deduction.StoreInventory;
                    decimal beforeQty = inv.AvailableQty;

                    if (isPOS)
                    {
                        // POS: Trừ thẳng AvailableQty (không qua Reserve)
                        if (inv.AvailableQty < deduction.Quantity)
                        {
                            throw new Exception(
                                $"Không đủ tồn kho để trừ cho đơn POS #{orderId}. " +
                                $"Mục: {deduction.ItemName}, cần {deduction.Quantity:N3}, có {inv.AvailableQty:N3}");
                        }
                        inv.AvailableQty -= deduction.Quantity;
                    }
                    else
                    {
                        // Online (Delivery): Trừ ReservedQty (đã giữ chỗ lúc đặt đơn)
                        inv.ReservedQty = Math.Max(0, inv.ReservedQty - deduction.Quantity);
                    }

                    inv.LastUpdated = DateTime.Now;

                    // Ghi log lịch sử
                    _context.InventoryTransactions.Add(new InventoryTransaction
                    {
                        StoreInventoryId = inv.StoreInventoryId,
                        Type = InventoryDocumentType.SALES_DEDUCTION,
                        Quantity = -deduction.Quantity, // Âm = xuất kho
                        BeforeQty = beforeQty,
                        AfterQty = inv.AvailableQty,
                        ReferenceOrderId = orderId,
                        CreatedAt = DateTime.Now
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw; // Đẩy lỗi ra ngoài để caller bắt
            }
        }

        // ============================================================
        // PRIVATE: Tính danh sách trừ kho theo OPTION B
        // Option B = Trừ bán thành phẩm trực tiếp, KHÔNG explode ra NL thô
        // ============================================================
        private async Task BuildDeductionListOptionB(
            IEnumerable<CafeChain.Models.Drinks.RecipeDetail> details,
            int multiplier,
            int storeId,
            List<InventoryDeductionItem> deductions)
        {
            foreach (var detail in details)
            {
                if (detail.IngredientId.HasValue)
                {
                    // === NGUYÊN LIỆU THÔ: Trừ từ StoreInventory (IngredientId) ===
                    var ingredient = await _context.Ingredients
                        .Include(i => i.UnitConversions)
                        .FirstOrDefaultAsync(i => i.IngredientId == detail.IngredientId.Value);

                    if (ingredient == null) continue;

                    // Quy đổi đơn vị: BOM UnitId → Ingredient.BaseUnitId
                    decimal quantityInBaseUnit = detail.Quantity;
                    if (detail.UnitId != ingredient.BaseUnitId)
                    {
                        var conversion = ingredient.UnitConversions
                            .FirstOrDefault(c => c.FromUnitId == detail.UnitId && c.ToUnitId == ingredient.BaseUnitId);

                        if (conversion != null && conversion.FromQuantity != 0)
                        {
                            quantityInBaseUnit = (detail.Quantity / conversion.FromQuantity) * conversion.ToQuantity;
                        }
                    }

                    decimal totalQty = quantityInBaseUnit * multiplier;

                    // Tìm hoặc tạo entry trong deductions
                    var inv = await _context.StoreInventories
                        .FirstOrDefaultAsync(si => si.StoreId == storeId && si.IngredientId == detail.IngredientId.Value);

                    if (inv != null)
                    {
                        var existing = deductions.FirstOrDefault(d => d.StoreInventory.StoreInventoryId == inv.StoreInventoryId);
                        if (existing != null)
                        {
                            existing.Quantity += totalQty;
                        }
                        else
                        {
                            deductions.Add(new InventoryDeductionItem
                            {
                                StoreInventory = inv,
                                Quantity = totalQty,
                                ItemName = ingredient.Name
                            });
                        }
                    }
                }
                else if (detail.ChildRecipeId.HasValue)
                {
                    // === BÁN THÀNH PHẨM: Trừ trực tiếp từ StoreInventory (RecipeId) ===
                    // STRICT OPTION B: KHÔNG explode ra nguyên liệu thô
                    decimal totalQty = detail.Quantity * multiplier;

                    var inv = await _context.StoreInventories
                        .FirstOrDefaultAsync(si => si.StoreId == storeId && si.RecipeId == detail.ChildRecipeId.Value);

                    if (inv != null)
                    {
                        var childRecipeName = await _context.Recipes
                            .Where(r => r.RecipeId == detail.ChildRecipeId.Value)
                            .Select(r => r.Name)
                            .FirstOrDefaultAsync() ?? $"BTP #{detail.ChildRecipeId}";

                        var existing = deductions.FirstOrDefault(d => d.StoreInventory.StoreInventoryId == inv.StoreInventoryId);
                        if (existing != null)
                        {
                            existing.Quantity += totalQty;
                        }
                        else
                        {
                            deductions.Add(new InventoryDeductionItem
                            {
                                StoreInventory = inv,
                                Quantity = totalQty,
                                ItemName = childRecipeName
                            });
                        }
                    }
                    // Nếu không tìm thấy StoreInventory cho BTP → bỏ qua (Kho chưa có BTP này)
                }
            }
        }

        // ============================================================
        // PRIVATE: Tính nguyên liệu cho Reserve/Release (logic cũ)
        // ============================================================
        private async Task<Dictionary<int, decimal>> CalculateRequiredIngredientsAsync(List<CartItemViewModel> items)
        {
            var result = new Dictionary<int, decimal>();
            
            var drinkIds = items.Select(i => i.DrinkId).Distinct().ToList();
            var toppingIds = items.SelectMany(i => i.ToppingIds).Distinct().ToList();

            var recipes = await _context.Recipes
                .Include(r => r.RecipeDetails)
                .Where(r => (r.DrinkId.HasValue && drinkIds.Contains(r.DrinkId.Value)) 
                         || (r.ToppingId.HasValue && toppingIds.Contains(r.ToppingId.Value)))
                .ToListAsync();

            // Lấy tất cả nguyên liệu liên quan để xử lý chuyển đổi đơn vị
            var allIngredientIds = recipes.SelectMany(r => r.RecipeDetails)
                .Where(rd => rd.IngredientId.HasValue)
                .Select(rd => rd.IngredientId.Value)
                .Distinct().ToList();

            var ingredientCache = await _context.Ingredients
                .Include(i => i.UnitConversions)
                .Where(i => allIngredientIds.Contains(i.IngredientId))
                .ToDictionaryAsync(i => i.IngredientId);

            foreach (var item in items)
            {
                var drinkRecipe = recipes.FirstOrDefault(r => r.DrinkId == item.DrinkId);
                if (drinkRecipe != null)
                {
                    await ProcessRecipeDetailsAsync(drinkRecipe.RecipeDetails, item.Quantity, result, ingredientCache);
                }

                foreach (var toppingId in item.ToppingIds)
                {
                    var toppingRecipe = recipes.FirstOrDefault(r => r.ToppingId == toppingId);
                    if (toppingRecipe != null)
                    {
                        await ProcessRecipeDetailsAsync(toppingRecipe.RecipeDetails, item.Quantity, result, ingredientCache);
                    }
                }
            }

            return result;
        }

        private async Task ProcessRecipeDetailsAsync(
            IEnumerable<CafeChain.Models.Drinks.RecipeDetail> details, 
            int multiplier, 
            Dictionary<int, decimal> result,
            Dictionary<int, Ingredient> ingredientCache)
        {
            foreach (var detail in details)
            {
                if (detail.IngredientId.HasValue)
                {
                    if (!ingredientCache.TryGetValue(detail.IngredientId.Value, out var ingredient))
                    {
                        // Fetch bổ sung nếu chưa có (cho trường hợp Child Recipe)
                        ingredient = await _context.Ingredients
                            .Include(i => i.UnitConversions)
                            .FirstOrDefaultAsync(i => i.IngredientId == detail.IngredientId.Value);
                        
                        if (ingredient != null) ingredientCache[ingredient.IngredientId] = ingredient;
                    }

                    decimal quantityInBaseUnit = detail.Quantity;
                    if (ingredient != null && detail.UnitId != ingredient.BaseUnitId)
                    {
                        var conversion = ingredient.UnitConversions
                            .FirstOrDefault(c => c.FromUnitId == detail.UnitId && c.ToUnitId == ingredient.BaseUnitId);
                        
                        if (conversion != null && conversion.FromQuantity != 0)
                        {
                            quantityInBaseUnit = (detail.Quantity / conversion.FromQuantity) * conversion.ToQuantity;
                        }
                    }

                    if (result.ContainsKey(detail.IngredientId.Value))
                        result[detail.IngredientId.Value] += quantityInBaseUnit * multiplier;
                    else
                        result[detail.IngredientId.Value] = quantityInBaseUnit * multiplier;
                }
                else if (detail.ChildRecipeId.HasValue)
                {
                    var childRecipe = await _context.Recipes
                        .Include(r => r.RecipeDetails)
                        .FirstOrDefaultAsync(r => r.RecipeId == detail.ChildRecipeId.Value);

                    if (childRecipe != null)
                    {
                        await ProcessRecipeDetailsAsync(childRecipe.RecipeDetails, multiplier, result, ingredientCache);
                    }
                }
            }
        }

        // ============================================================
        // PRIVATE DTO: Đại diện cho 1 dòng trừ kho
        // ============================================================
        private class InventoryDeductionItem
        {
            public StoreInventory StoreInventory { get; set; }
            public decimal Quantity { get; set; }
            public string ItemName { get; set; }
        }
    }
}
