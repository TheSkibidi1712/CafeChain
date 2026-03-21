using CafeChain.Application.Interfaces;
using CafeChain.Data;
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
            // 1. Lấy thông tin Drink (Chỉ include những gì có sẵn trong Model)
            var drink = await _context.Drinks
                .Include(d => d.DrinkImages)
                .Include(d => d.DrinkSizes).ThenInclude(ds => ds.Size)
                .Include(d => d.Category)
                .FirstOrDefaultAsync(d => d.DrinkId == drinkId && d.Active);

            if (drink == null) return null;

            // 2. TỰ ĐI LẤY TOPPING MẶC ĐỊNH (Truy vấn thẳng vào bảng DrinkDefaultTopping)
            var defaultToppings = await _context.DrinkDefaultToppings // Tên model đúng của ní nè
                .Include(dt => dt.Topping)
                .Where(dt => dt.DrinkId == drinkId)
                .ToListAsync();

            // 3. TỰ ĐI LẤY TOPPING MUA THÊM (Truy vấn thẳng vào bảng DrinkTopping)
            var optionalToppings = await _context.DrinkToppings
                .Include(dt => dt.Topping)
                .Where(dt => dt.DrinkId == drinkId)
                .ToListAsync();

            //// 4. Lấy món gợi ý (giữ nguyên)
            //var relatedDrinks = await _context.Drinks
            //    .Include(d => d.DrinkImages)
            //    .Include(d => d.DrinkSizes)
            //    .Where(d => d.CategoryId == drink.CategoryId && d.DrinkId != drinkId && d.Active)
            //    .Take(4)
            //    .ToListAsync();


            // LẤY MÓN GỢI Ý (Đã chỉnh sửa tạm để test Slider)
            var relatedDrinks = await _context.Drinks
                .Include(d => d.DrinkImages)
                .Include(d => d.DrinkSizes)
                // Tạm thời BỎ điều kiện CategoryId để nó lấy tất cả các món (trừ món đang xem)
                .Where(d => d.DrinkId != drinkId && d.Active)

                // NẾU SAU NÀY MUỐN LẤY THEO DANH MỤC LẠI, BÁC CHỈ CẦN MỞ COMMENT DÒNG DƯỚI VÀ XÓA DÒNG TRÊN:
                // .Where(d => d.CategoryId == drink.CategoryId && d.DrinkId != drinkId && d.Active)

                .Take(6) // Lấy ra tối đa 10 món để Slider có thể cuộn được
                .ToListAsync();

            return new DrinkDetailViewModel
            {
                Drink = drink,
                RelatedDrinks = relatedDrinks,
                DefaultToppings = defaultToppings, // Đổ dữ liệu vào đây
                OptionalToppings = optionalToppings // Đổ dữ liệu vào đây
            };
        }
    }
}