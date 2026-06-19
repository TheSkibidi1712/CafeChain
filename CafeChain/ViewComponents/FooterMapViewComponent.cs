using CafeChain.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CafeChain.ViewComponents
{
    public class FooterMapViewComponent : ViewComponent
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;

        public FooterMapViewComponent(AppDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            // 1. Get Active Stores with coordinates
            var storesList = await _context.Stores
                .Where(s => s.Active && s.Latitude.HasValue && s.Longitude.HasValue)
                .Select(s => new {
                    s.StoreId,
                    s.Name,
                    s.Address,
                    Latitude = s.Latitude ?? 0,
                    Longitude = s.Longitude ?? 0
                })
                .ToListAsync();

            ViewBag.StoresJson = System.Text.Json.JsonSerializer.Serialize(storesList);

            // 2. TỐI ƯU CẤU HÌNH BẰNG CACHE (Theo yêu cầu)
            const string cacheKey = "Settings_Map_Default_Center";
            if (!_cache.TryGetValue(cacheKey, out string? defaultCenter))
            {
                var setting = await _context.SystemSettings
                    .FirstOrDefaultAsync(s => s.SettingKey == "Map_Default_Center");
                
                defaultCenter = setting?.SettingValue ?? "10.8231, 106.6297"; // Fallback TPHCM
                
                // Lưu lại trong MemoryCache 24 giờ để tránh query DB nhiều lần
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromHours(24));
                _cache.Set(cacheKey, defaultCenter, cacheOptions);
            }

            ViewBag.MapDefaultCenter = defaultCenter;

            return View("~/Views/Shared/Components/FooterMap/Default.cshtml");
        }
    }
}
