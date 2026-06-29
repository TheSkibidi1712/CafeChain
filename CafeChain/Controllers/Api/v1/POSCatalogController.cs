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
                    IsAvailable = true,
                    Price = d.DrinkSizes
                        .Where(ds => ds.Active)
                        .OrderBy(ds => ds.Price)
                        .Select(ds => ds.Price)
                        .FirstOrDefault(),
                    Sizes = d.DrinkSizes
                        .Where(ds => ds.Active)
                        .Select(ds => new POSMenuItemSizeDto
                        {
                            SizeId = ds.SizeId,
                            SizeName = ds.Size.Name,
                            Price = ds.Price
                        }).ToList(),
                    AvailableToppings = d.DrinkToppings
                        .Where(dt => dt.Topping.Active)
                        .Select(dt => new POSToppingDto
                        {
                            Id = dt.ToppingId,
                            Name = dt.Topping.Name,
                            Price = dt.Topping.Price,
                            ImageUrl = dt.Topping.ImageUrl
                        }).ToList()
                })
                .ToListAsync();

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
    }
}
