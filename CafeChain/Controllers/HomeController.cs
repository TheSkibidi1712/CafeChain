using CafeChain.Data;
using CafeChain.Models.Drinks;
using CafeChain.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CafeChain.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;
        private readonly CafeChain.Application.Services.Admin.Vouchers.IAdminWheelService _wheelService;
        private readonly CafeChain.Application.Interfaces.IDrinkService _drinkService;
        private readonly ILogger<HomeController> _logger;

        public HomeController(
            AppDbContext context, 
            CafeChain.Application.Services.Admin.Vouchers.IAdminWheelService wheelService, 
            CafeChain.Application.Interfaces.IDrinkService drinkService,
            ILogger<HomeController> logger)
        {
            _context = context;
            _wheelService = wheelService;
            _drinkService = drinkService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                // === 1. Lấy danh sách Categories ===
                var categories = await _context.DrinkCategories
                    .Where(c => c.Active)
                    .ToListAsync();

                // === 2. Lấy TẤT CẢ drinks (Include DrinkImages, DrinkSizes, Ratings — tránh N+1) ===
                var allDrinks = await _context.Drinks
                    .Include(d => d.DrinkImages)
                    .Include(d => d.DrinkSizes)
                    .Include(d => d.Ratings)
                    .Include(d => d.Category)
                    .Where(d => d.Active)
                    .ToListAsync();

                // === 3. Helper: Chuyển Drink entity -> DrinkItemViewModel ===
                DrinkItemViewModel ToDrinkItemVM(Drink d)
                {
                    var defaultImage = d.DrinkImages?.FirstOrDefault(i => i.IsDefault)
                                    ?? d.DrinkImages?.FirstOrDefault();
                    return new DrinkItemViewModel
                    {
                        DrinkId = d.DrinkId,
                        Name = d.Name,
                        Price = d.DrinkSizes?.OrderBy(s => s.DrinkSizeId).FirstOrDefault()?.Price ?? 0,
                        ImageUrl = defaultImage?.ImageUrl ?? "/images/default.jpg",
                        AverageRating = d.Ratings != null && d.Ratings.Any()
                            ? Math.Round(d.Ratings.Average(r => r.Stars), 1)
                            : 0,
                        RatingCount = d.Ratings?.Count ?? 0
                    };
                }

                // === 4. BestSellers: Top 8 (ưu tiên nhiều rating + điểm cao, fallback theo mới nhất) ===
                var bestSellers = allDrinks
                    .OrderByDescending(d => d.Ratings?.Count ?? 0)
                    .ThenByDescending(d => d.Ratings != null && d.Ratings.Any() ? d.Ratings.Average(r => r.Stars) : 0)
                    .ThenByDescending(d => d.CreatedAt)
                    .Take(8)
                    .Select(ToDrinkItemVM)
                    .ToList();

                // Check Availability for all drinks
                var allDrinkIds = allDrinks.Select(d => d.DrinkId).ToList();
                var availabilities = await _drinkService.CheckDrinksAvailabilityAsync(allDrinkIds, 1);

                foreach (var bs in bestSellers)
                {
                    bs.IsAvailable = availabilities.ContainsKey(bs.DrinkId) ? availabilities[bs.DrinkId] : true;
                }

                // === 5. Build ViewModel ===
                var viewModel = new HomeViewModel
                {
                    Categories = categories,
                    Drinks = allDrinks,
                    ActiveWheel = await _wheelService.GetActiveConfigAsync(),
                    BestSellers = bestSellers,
                    Availability = availabilities
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Index page in HomeController.");
                var emptyModel = new HomeViewModel
                {
                    Categories = new List<DrinkCategory>(),
                    Drinks = new List<Drink>(),
                    ActiveWheel = null,
                    Availability = new Dictionary<int, bool>()
                };
                return View(emptyModel);
            }
        }
    }
}