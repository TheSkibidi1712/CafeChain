using CafeChain.Application.Interfaces;
using CafeChain.Data;
using CafeChain.Models.Customers;
using CafeChain.Models.Drinks;
using CafeChain.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using CafeChain.Application.DTOs.Drinks; // (Sửa lại cho đúng namespace bác vừa tạo)

namespace CafeChain.Controllers
{
    [Authorize] // 🔥 ĐẶT CHỐT CHẶN Ở ĐÂY LÀ KHÓA TOÀN BỘ MENU VÀ DETAIL 🔥
    public class DrinkController : Controller
    {
        private readonly IDrinkService _drinkService;
        private readonly AppDbContext _context;

        // Tiêm IDrinkService vào Controller
        public DrinkController(IDrinkService drinkService, AppDbContext context )
        {
            _drinkService = drinkService;
            _context = context;
        }

        public async Task<IActionResult> Menu(int? categoryId, decimal minPrice = 0, decimal maxPrice = 150000, string sortBy = "popular", int page = 1)
        {
            int pageSize = 8;

            // Gọi Service để lấy cục dữ liệu
            var data = await _drinkService.GetMenuDataAsync(categoryId, minPrice, maxPrice, sortBy, page, pageSize);

            // Ráp vào ViewModel
            var viewModel = new MenuViewModel
            {
                Categories = data.Categories,
                Drinks = data.Drinks,
                SelectedCategoryId = categoryId,
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                SortBy = sortBy,
                CurrentPage = page,
                TotalPages = data.TotalPages
            };

            return View(viewModel);
        }
        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            // 1. QUERY LẤY SẢN PHẨM (KÈM TOÀN BỘ RỄ MÁ GIA PHẢ CỦA NÓ)
            var drink = await _context.Drinks
                .Include(d => d.DrinkImages)
                .Include(d => d.DrinkSizes)
                    .ThenInclude(ds => ds.Size) // Lấy tên Size (S, M, L)
                .Include(d => d.DrinkDefaultToppings)
                    .ThenInclude(dt => dt.Topping) // Lấy tên, giá Topping mặc định
                .Include(d => d.DrinkToppings)
                    .ThenInclude(dt => dt.Topping) // Lấy tên, giá Topping thêm

                // 🔥 ĐOẠN QUAN TRỌNG NHẤT CHO TÍNH NĂNG REVIEW Ở ĐÂY 🔥
                .Include(d => d.Ratings)
                    .ThenInclude(r => r.Customer) // Chọc sang bảng Customer để lấy Tên và Avatar của người đánh giá

                .FirstOrDefaultAsync(d => d.DrinkId == id);

            if (drink == null)
                return NotFound();

            // 2. QUERY LẤY DANH SÁCH GỢI Ý DÀNH CHO BẠN (Cùng danh mục, trừ món hiện tại)
            var relatedDrinks = await _context.Drinks
                .Include(d => d.DrinkImages)
                .Include(d => d.DrinkSizes)
                .Where(d => d.CategoryId == drink.CategoryId && d.DrinkId != id)
                .Take(4) // Chỉ lấy 4 món thôi cho đẹp UI
                .ToListAsync();

            // 3. ĐÓNG GÓI TẤT CẢ VÀO VIEWMODEL ĐỂ GỬI RA HTML
            var viewModel = new DrinkDetailViewModel
            {
                Drink = drink,
                RelatedDrinks = relatedDrinks,

                // Tách Topping ra 2 list riêng cho HTML dễ bề xử lý (Như file ViewModel bác gửi)
                DefaultToppings = drink.DrinkDefaultToppings?.ToList() ?? new List<DrinkDefaultTopping>(),
                OptionalToppings = drink.DrinkToppings?.ToList() ?? new List<DrinkTopping>(),

                // 🔥 LẤY LIST ĐÁNH GIÁ (SẮP XẾP MỚI NHẤT LÊN ĐẦU) 🔥
                Ratings = drink.Ratings != null
                    ? drink.Ratings.OrderByDescending(r => r.CreatedAt).ToList()
                    : new List<Rating>()
            };

            return View(viewModel);

        }
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> SubmitReview([FromBody] SubmitReviewRequest request)
        {
            if (request.Stars < 1 || request.Stars > 5)
            {
                return Json(new { success = false, message = "Số sao đánh giá phải từ 1 đến 5 sao nhé!" });
            }

            var customerIdStr = User.FindFirstValue("CustomerId");
            if (string.IsNullOrEmpty(customerIdStr)) return Unauthorized();
            var customerId = int.Parse(customerIdStr);

            try
            {
                // 1. DÒ TÌM XEM KHÁCH NÀY ĐÃ ĐÁNH GIÁ LY NƯỚC NÀY BAO GIỜ CHƯA?
                var existingReview = await _context.Ratings
                    .FirstOrDefaultAsync(r => r.CustomerId == customerId && r.DrinkId == request.DrinkId);

                if (existingReview != null)
                {
                    // 🔥 NẾU CÓ RỒI -> CẬP NHẬT LẠI ĐÁNH GIÁ CŨ (Update)
                    existingReview.Stars = request.Stars;
                    existingReview.Comment = request.Comment;
                    existingReview.CreatedAt = DateTime.Now; // Cập nhật lại ngày sửa

                    _context.Ratings.Update(existingReview);
                }
                else
                {
                    // 🔥 NẾU CHƯA CÓ -> TẠO MỚI (Insert)
                    var rating = new Rating
                    {
                        DrinkId = request.DrinkId,
                        CustomerId = customerId,
                        Stars = request.Stars,
                        Comment = request.Comment,
                        CreatedAt = DateTime.Now
                    };
                    _context.Ratings.Add(rating);
                }

                // Lưu vào DB (Lúc này SQL Server sẽ gật đầu cho qua vì không bị sinh thêm dòng trùng lặp)
                await _context.SaveChangesAsync();

                // Tính lại sao trung bình
                var newAvgRating = _context.Ratings.Where(r => r.DrinkId == request.DrinkId).Average(r => r.Stars);

                return Json(new
                {
                    success = true,
                    message = existingReview != null ? "Đã cập nhật lại đánh giá của bạn!" : "Tuyệt vời! Cảm ơn bạn đã đánh giá!",
                    newAverageRating = newAvgRating.ToString("0.0")
                });
            }
            catch (Exception ex)
            {
                var innerMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = "Lỗi Database: " + innerMessage });
            }
        }
    }
}
       
    
