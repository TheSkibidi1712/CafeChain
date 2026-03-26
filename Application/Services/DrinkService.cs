using CafeChain.Application.Interfaces;
using CafeChain.Data;
using CafeChain.Models.Customers;
using CafeChain.Models.Drinks;
using CafeChain.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services
{
    public class DrinkService : IDrinkService
    {
        private readonly AppDbContext _context;

        public DrinkService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<(List<DrinkCategory> Categories, List<Drink> Drinks, int TotalPages)> GetMenuDataAsync(
            int? categoryId, decimal minPrice, decimal maxPrice, string sortBy, int page, int pageSize)
        {
            // Lấy danh mục
            var categories = await _context.DrinkCategories.Where(c => c.Active).ToListAsync();

            // Khởi tạo Query
            var query = _context.Drinks
                .Include(d => d.DrinkImages)
                .Include(d => d.DrinkSizes)
                .Include(d => d.Ratings)
                .Where(d => d.Active)
                .AsQueryable();

            // Lọc theo danh mục
            if (categoryId.HasValue && categoryId > 0)
            {
                query = query.Where(d => d.CategoryId == categoryId);
            }

            // Lọc theo giá
            query = query.Where(d => d.DrinkSizes.Any(s => s.Price >= minPrice && s.Price <= maxPrice));

            // Sắp xếp
            switch (sortBy)
            {
                case "price_asc":
                    query = query.OrderBy(d => d.DrinkSizes.Min(s => s.Price));
                    break;
                case "price_desc":
                    query = query.OrderByDescending(d => d.DrinkSizes.Min(s => s.Price));
                    break;
                default:
                    query = query.OrderByDescending(d => d.DrinkId);
                    break;
            }

            // Phân trang
            int totalItems = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var drinks = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (categories, drinks, totalPages == 0 ? 1 : totalPages);
        }
        public async Task<DrinkDetailViewModel> GetDrinkDetailAsync(int drinkId)
        {
            // 1. Kéo sản phẩm lên, KÉO LUÔN CẢ RATINGS VÀ CUSTOMER
            var drink = await _context.Drinks
                .Include(d => d.DrinkImages)
                .Include(d => d.DrinkSizes).ThenInclude(ds => ds.Size)
                .Include(d => d.Category)
                // THÊM DÒNG NÀY MỚI CÓ DATA REVIEW
                .Include(d => d.Ratings).ThenInclude(r => r.Customer)
                .FirstOrDefaultAsync(d => d.DrinkId == drinkId && d.Active);

            if (drink == null) return null;

            var defaultToppings = await _context.DrinkDefaultToppings
                .Include(dt => dt.Topping).Where(dt => dt.DrinkId == drinkId).ToListAsync();

            var optionalToppings = await _context.DrinkToppings
                .Include(dt => dt.Topping).Where(dt => dt.DrinkId == drinkId).ToListAsync();

            var relatedDrinks = await _context.Drinks
                .Include(d => d.DrinkImages)
                .Include(d => d.DrinkSizes)
                .Where(d => d.DrinkId != drinkId && d.Active) // Lấy hết trừ món đang xem
                .ToListAsync();

            return new DrinkDetailViewModel
            {
                Drink = drink,
                RelatedDrinks = relatedDrinks,
                DefaultToppings = defaultToppings,
                OptionalToppings = optionalToppings,
                // THÊM DÒNG NÀY ĐỂ GÁN LIST REVIEW VÀO VIEWMODEL
                Ratings = drink.Ratings != null ? drink.Ratings.OrderByDescending(r => r.CreatedAt).ToList() : new List<Rating>()
            };
        }
    }
}