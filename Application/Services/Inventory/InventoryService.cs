using CafeChain.Application.Interfaces;
using CafeChain.Data;
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

        public async Task ReserveInventoryForOrderAsync(int storeId, List<CartItemViewModel> items)
        {
            var requiredIngredients = await CalculateRequiredIngredientsAsync(items);
            if (!requiredIngredients.Any()) return;

            var ingredientIds = requiredIngredients.Keys.ToList();
            var inventories = await _context.StoreInventories
                .Where(si => si.StoreId == storeId && ingredientIds.Contains(si.IngredientId))
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
                .Where(si => si.StoreId == order.StoreId && ingredientIds.Contains(si.IngredientId))
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

            // (Có thể có nguyên liệu trong Child Recipes nữa, nên cần fetch đệ quy hoặc fetch rộng hơn)
            // Để đơn giản và an toàn, ta sẽ fetch nguyên liệu trong lúc process nếu chưa có.
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
    }
}
