using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Admin.Profitability;
using CafeChain.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Controllers.Api.v1
{
    /// <summary>
    /// POS Catalog APIs — GET categories, menu-items, toppings.
    /// Store-scoped via JWT. Availability uses shared IUnitConversionService (fail-closed).
    /// </summary>
    [Route("api/v1/pos")]
    public class POSCatalogController : PosApiController
    {
        private readonly AppDbContext _context;
        private readonly IUnitConversionService _unitConversion;
        private readonly ILogger<POSCatalogController> _logger;
        private readonly IDrinkSizePricingService? _pricingService;

        public POSCatalogController(
            AppDbContext context,
            IUnitConversionService unitConversion,
            IDrinkSizePricingService pricingService,
            ILogger<POSCatalogController> logger)
        {
            _context = context;
            _unitConversion = unitConversion;
            _pricingService = pricingService;
            _logger = logger;
        }

        // Compatibility constructor for focused controller tests that do not exercise catalog versioning.
        public POSCatalogController(
            AppDbContext context,
            IUnitConversionService unitConversion,
            ILogger<POSCatalogController> logger)
        {
            _context = context;
            _unitConversion = unitConversion;
            _logger = logger;
        }

        [HttpGet("catalog/version")]
        public async Task<IActionResult> GetCatalogVersion(CancellationToken cancellationToken)
        {
            if (_pricingService == null)
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Catalog version service is unavailable." });
            return Ok(await _pricingService.GetCatalogVersionAsync(cancellationToken));
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
                    AvailabilityStatus = "TemporarilyUnavailable",
                    AvailabilityReason = "Tạm hết hàng",
                    Price = d.DrinkSizes
                        .Where(ds => ds.Active)
                        .Select(ds => ds.Price)
                        .FirstOrDefault(),
                    Sizes = d.DrinkSizes
                        .Where(ds => ds.Active)
                        .OrderBy(ds => ds.SizeId)
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
                item.Sizes = item.Sizes
                    .OrderBy(size => size.Price)
                    .ThenBy(size => size.SizeId)
                    .ToList();
                item.Price = item.Sizes.FirstOrDefault()?.Price ?? item.Price;

                // Existing product behavior: only inventory-available toppings are returned.
                // Unavailable toppings (incl. MissingUnitConversion) are omitted, not listed with reason.
                var availableToppings = new List<POSToppingDto>();
                foreach (var topping in item.AvailableToppings)
                {
                    var toppingAvailability = await HasSufficientRecipeInventoryAsync(storeId, null, null, topping.Id);
                    if (toppingAvailability.IsAvailable)
                    {
                        availableToppings.Add(topping);
                    }
                }

                item.AvailableToppings = availableToppings;

                var availability = await EvaluateAnySizeAvailabilityAsync(storeId, item.Id, item.Sizes);
                item.IsAvailable = availability.IsAvailable;
                item.AvailabilityStatus = availability.Status;
                item.AvailabilityReason = availability.Reason;
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

        private async Task<RecipeAvailabilityResult> EvaluateAnySizeAvailabilityAsync(
            int storeId,
            int drinkId,
            List<POSMenuItemSizeDto> sizes)
        {
            if (sizes.Count == 0)
                return RecipeAvailabilityResult.Unavailable("TemporarilyUnavailable", "Tạm hết hàng", 0);

            RecipeAvailabilityResult? strongestUnavailable = null;
            foreach (var size in sizes)
            {
                var availability = await HasSufficientRecipeInventoryAsync(storeId, drinkId, size.SizeId, null);
                if (availability.IsAvailable)
                    return RecipeAvailabilityResult.Available();

                if (strongestUnavailable == null || availability.Priority > strongestUnavailable.Priority)
                    strongestUnavailable = availability;
            }

            return strongestUnavailable
                ?? RecipeAvailabilityResult.Unavailable("TemporarilyUnavailable", "Tạm hết hàng", 0);
        }

        private async Task<RecipeAvailabilityResult> HasSufficientRecipeInventoryAsync(
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

            if (recipe == null || recipe.RecipeDetails.Count == 0)
                return RecipeAvailabilityResult.Unavailable("MissingRecipe", "Chưa cấu hình công thức", 10);

            foreach (var detail in recipe.RecipeDetails)
            {
                decimal requiredQty;
                if (detail.IngredientId.HasValue)
                {
                    var converted = await _unitConversion.ConvertAsync(
                        detail.IngredientId.Value,
                        detail.Quantity,
                        detail.UnitId);

                    if (!converted.IsSuccess)
                    {
                        _logger.LogWarning(
                            "[POSCatalog] MissingUnitConversion IngredientId={IngredientId} FromUnitId={UnitId}: {Message}",
                            detail.IngredientId, detail.UnitId, converted.Message);
                        return RecipeAvailabilityResult.Unavailable(
                            "MissingUnitConversion",
                            "Thiếu quy đổi đơn vị nguyên liệu",
                            25);
                    }

                    requiredQty = converted.Data;
                }
                else
                {
                    requiredQty = detail.Quantity;
                }

                var inventory = await _context.StoreInventories
                    .FirstOrDefaultAsync(i =>
                        i.StoreId == storeId &&
                        i.IngredientId == detail.IngredientId &&
                        i.RecipeId == detail.ChildRecipeId);

                if (inventory == null)
                    return RecipeAvailabilityResult.Unavailable("MissingInventory", "Chưa có tồn kho tại cửa hàng", 20);

                if (inventory.AvailableQty < requiredQty)
                    return RecipeAvailabilityResult.Unavailable("InsufficientStock", "Hết nguyên liệu", 30);
            }

            return RecipeAvailabilityResult.Available();
        }

        private sealed record RecipeAvailabilityResult(
            bool IsAvailable,
            string Status,
            string? Reason,
            int Priority)
        {
            public static RecipeAvailabilityResult Available() =>
                new(true, "Available", null, int.MaxValue);

            public static RecipeAvailabilityResult Unavailable(string status, string reason, int priority) =>
                new(false, status, reason, priority);
        }
    }
}
