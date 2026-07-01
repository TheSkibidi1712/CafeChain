using CafeChain.Application.DTOs.POS;
using CafeChain.Controllers.Api.v1;
using CafeChain.Infrastructure.Interfaces.Admin.POS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CafeChain.Data;

namespace CafeChain.Controllers.Api.v1
{
    /// <summary>
    /// POS Catalog APIs — GET categories, menu-items, toppings
    /// Tất cả queries filter theo StoreId từ JWT Claims.
    /// N+1 prevention: sử dụng Projection (.Select) thay vì Eager Loading.
    /// </summary>
    [Route("api/v1/pos")]
    public class POSCatalogController : PosApiController
    {
        private readonly AppDbContext _context;
        private readonly ILogger<POSCatalogController> _logger;

        public POSCatalogController(AppDbContext context, ILogger<POSCatalogController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// GET /api/v1/pos/categories
        /// Trả danh sách danh mục có ít nhất 1 món active tại store.
        /// </summary>
        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
        {
            var storeId = CurrentStoreId;

            var categories = await _context.DrinkCategories
                .Where(c => c.Active)
                .Select(c => new POSCategoryDto
                {
                    Id = c.CategoryId,
                    Name = c.Name,
                    Icon = c.Icon,
                    Count = c.Drinks.Count(d => d.Active
                        && d.StoreDrinks.Any(sd => sd.StoreId == storeId && sd.Active))
                })
                .Where(c => c.Count > 0)
                .ToListAsync();

            return Ok(categories);
        }

        /// <summary>
        /// GET /api/v1/pos/menu-items?categoryId=1
        /// Trả menu items tại store — nested sizes + toppings.
        /// Query optimization: 1 query duy nhất dùng Projection.
        /// </summary>
        [HttpGet("menu-items")]
        public async Task<IActionResult> GetMenuItems([FromQuery] int? categoryId)
        {
            var storeId = CurrentStoreId;
            var storeToppingIds = await _context.StoreToppings
                .Where(st => st.StoreId == storeId && st.Active && st.Topping.Active)
                .Select(st => st.ToppingId)
                .ToListAsync();

            var query = _context.Drinks
                .Where(d => d.Active
                    && d.StoreDrinks.Any(sd => sd.StoreId == storeId && sd.Active));

            if (categoryId.HasValue)
            {
                query = query.Where(d => d.CategoryId == categoryId.Value);
            }

            var menuItems = await query
                .Select(d => new POSMenuItemDto
                {
                    Id = d.DrinkId,
                    Name = d.Name,
                    CategoryId = d.CategoryId ?? 0,
                    Image = d.DrinkImages
                        .Where(di => di.IsDefault)
                        .Select(di => di.ImageUrl)
                        .FirstOrDefault()
                        ?? d.DrinkImages
                            .Select(di => di.ImageUrl)
                            .FirstOrDefault(),
                    IsAvailable = false,
                    Price = d.DrinkSizes
                        .Where(ds => ds.Active)
                        .OrderBy(ds => ds.Price)
                        .ThenBy(ds => ds.SizeId)
                        .Select(ds => ds.Price)
                        .FirstOrDefault(),
                    Sizes = d.DrinkSizes
                        .Where(ds => ds.Active)
                        .OrderBy(ds => ds.Price)
                        .ThenBy(ds => ds.SizeId)
                        .Select(ds => new POSMenuItemSizeDto
                        {
                            SizeId = ds.SizeId,
                            SizeName = ds.Size.Name,
                            Price = ds.Price
                        }).ToList(),
                    AvailableToppings = d.DrinkToppings
                        .Where(dt => dt.Topping.Active && storeToppingIds.Contains(dt.ToppingId))
                        .Select(dt => new POSToppingDto
                        {
                            Id = dt.ToppingId,
                            Name = dt.Topping.Name,
                            Price = dt.Topping.Price,
                            ImageUrl = dt.Topping.ImageUrl
                        }).ToList()
                })
                .ToListAsync();

            foreach (var item in menuItems)
            {
                var availableToppings = new List<POSToppingDto>();
                foreach (var topping in item.AvailableToppings)
                {
                    if (await HasSufficientRecipeInventoryAsync(storeId, null, null, topping.Id))
                    {
                        availableToppings.Add(topping);
                    }
                }

                item.AvailableToppings = availableToppings;
                item.IsAvailable = item.Sizes.Any()
                    && await AnySizeHasSufficientRecipeInventoryAsync(storeId, item.Id, item.Sizes);
            }

            return Ok(menuItems);
        }

        /// <summary>
        /// GET /api/v1/pos/toppings
        /// Trả toàn bộ toppings active tại store (từ StoreTopping).
        /// </summary>
        [HttpGet("toppings")]
        public async Task<IActionResult> GetToppings()
        {
            var storeId = CurrentStoreId;

            var toppings = await _context.StoreToppings
                .Where(st => st.StoreId == storeId && st.Active)
                .Select(st => new POSToppingDto
                {
                    Id = st.ToppingId,
                    Name = st.Topping.Name,
                    Price = st.Topping.Price,
                    ImageUrl = st.Topping.ImageUrl
                })
                .ToListAsync();

            return Ok(toppings);
        }

        private async Task<bool> AnySizeHasSufficientRecipeInventoryAsync(
            int storeId,
            int drinkId,
            List<POSMenuItemSizeDto> sizes)
        {
            foreach (var size in sizes)
            {
                if (await HasSufficientRecipeInventoryAsync(storeId, drinkId, size.SizeId, null))
                    return true;
            }

            return false;
        }

        private async Task<bool> HasSufficientRecipeInventoryAsync(
            int storeId,
            int? drinkId,
            int? sizeId,
            int? toppingId)
        {
            var recipe = await _context.Recipes
                .Include(r => r.RecipeDetails)
                .FirstOrDefaultAsync(r =>
                    r.Active &&
                    r.Status == "Active" &&
                    r.DrinkId == drinkId &&
                    r.SizeId == sizeId &&
                    r.ToppingId == toppingId);

            if (recipe == null)
                return false;

            foreach (var detail in recipe.RecipeDetails)
            {
                var requiredQty = detail.IngredientId.HasValue
                    ? await ConvertQuantityToBaseUnitAsync(detail.IngredientId.Value, detail.Quantity, detail.UnitId)
                    : detail.Quantity;

                var inventory = await _context.StoreInventories
                    .FirstOrDefaultAsync(i =>
                        i.StoreId == storeId &&
                        i.IngredientId == detail.IngredientId &&
                        i.RecipeId == detail.ChildRecipeId);

                if (inventory == null || inventory.AvailableQty < requiredQty)
                    return false;
            }

            return true;
        }

        private async Task<decimal> ConvertQuantityToBaseUnitAsync(
            int ingredientId,
            decimal quantity,
            int fromUnitId)
        {
            var ingredient = await _context.Ingredients.FindAsync(ingredientId);
            if (ingredient == null || fromUnitId == ingredient.BaseUnitId)
                return quantity;

            var conversion = await _context.UnitConversions
                .FirstOrDefaultAsync(uc =>
                    uc.IngredientId == ingredientId &&
                    uc.FromUnitId == fromUnitId &&
                    uc.ToUnitId == ingredient.BaseUnitId);

            if (conversion != null && conversion.FromQuantity > 0)
                return quantity * (conversion.ToQuantity / conversion.FromQuantity);

            var reverseConversion = await _context.UnitConversions
                .FirstOrDefaultAsync(uc =>
                    uc.IngredientId == ingredientId &&
                    uc.FromUnitId == ingredient.BaseUnitId &&
                    uc.ToUnitId == fromUnitId);

            if (reverseConversion != null && reverseConversion.ToQuantity > 0)
                return quantity * (reverseConversion.FromQuantity / reverseConversion.ToQuantity);

            _logger.LogWarning(
                "[POSCatalog] Missing unit conversion for IngredientId={IngredientId}, UnitId={UnitId}. Using raw quantity.",
                ingredientId,
                fromUnitId);
            return quantity;
        }
    }
}
