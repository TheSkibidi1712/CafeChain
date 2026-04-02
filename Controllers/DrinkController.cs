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
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Http;

namespace CafeChain.Controllers
{
    [Authorize] // 🔥 ĐẶT CHỐT CHẶN Ở ĐÂY LÀ KHÓA TOÀN BỘ MENU VÀ DETAIL 🔥
    public class DrinkController : Controller
    {
        private readonly IDrinkService _drinkService;
        private readonly AppDbContext _context;
        private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _webHostEnvironment;
        private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _cache;

        // Tiêm IDrinkService vào Controller
        public DrinkController(IDrinkService drinkService, AppDbContext context, Microsoft.AspNetCore.Hosting.IWebHostEnvironment webHostEnvironment, Microsoft.Extensions.Caching.Memory.IMemoryCache cache)
        {
            _drinkService = drinkService;
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _cache = cache;
        }

        [AllowAnonymous]
        public async Task<IActionResult> Menu(string? keyword, int? categoryId, decimal minPrice = 0, decimal maxPrice = 150000, string sortBy = "popular", int page = 1)
        {
            int pageSize = 8;

            // Gọi Service để lấy cục dữ liệu
            var data = await _drinkService.GetMenuDataAsync(keyword, categoryId, minPrice, maxPrice, sortBy, page, pageSize);

            // Ráp vào ViewModel
            var viewModel = new MenuViewModel
            {
                Categories = data.Categories,
                Drinks = data.Drinks,
                SelectedCategoryId = categoryId,
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                SortBy = sortBy,
                Keyword = keyword,
                CurrentPage = page,
                TotalPages = data.TotalPages
            };

            return View(viewModel);
        }
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Detail(int id)
        {
            var viewModel = await _drinkService.GetDrinkDetailAsync(id);
            if (viewModel == null) return NotFound();

            return View(viewModel);
        }

        private int? GetCurrentCustomerId()
        {
            var customerIdStr = User.FindFirstValue("CustomerId");
            if (string.IsNullOrEmpty(customerIdStr)) return null;
            if (int.TryParse(customerIdStr, out int id)) return id;
            return null;
        }
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> SubmitReview([FromForm] SubmitReviewRequest request)
        {
            if (request.Stars < 1 || request.Stars > 5)
            {
                return Json(new { success = false, message = "Số sao đánh giá phải từ 1 đến 5 sao nhé!" });
            }

            var customerId = GetCurrentCustomerId();
            if (customerId == null) return Unauthorized();

            var result = await _drinkService.SubmitReviewAsync(customerId.Value, request.DrinkId, request.Stars, request.Comment, request.Images, _webHostEnvironment.WebRootPath);
            return Json(new
            {
                success = result.Success,
                message = result.Message,
                newAverageRating = result.NewAverageRating
            });
        }
        [HttpPost]
        public async Task<IActionResult> ToggleReactionAjax(int ratingId, CafeChain.Models.Enums.Customer.ReactionType type)
        {
            if (!User.Identity.IsAuthenticated)
                return Json(new { success = false, message = "Vui lòng đăng nhập", status = 401 });

            var customerId = GetCurrentCustomerId();
            if (customerId == null) return Json(new { success = false, message = "Unauthorized", status = 401 });

            // Chống spam siêu ngắn: Bắt double-click do chuột lỗi (500ms). Còn lại thả ga cho phép Undo (Bỏ cảm xúc) như FB.
            var cacheKey = $"reaction_spam_{customerId}_{ratingId}";
            if (_cache.TryGetValue(cacheKey, out _))
            {
                return Json(new { success = false, message = "Bạn thao tác quá nhanh, vui lòng chờ chút nhẹ!", status = 429 });
            }
            _cache.Set(cacheKey, true, TimeSpan.FromMilliseconds(500)); 
            
            var result = await _drinkService.ToggleReactionAsync(ratingId, customerId.Value, type);
            return Json(new { success = result.Success, message = result.Message, action = result.Action, type = result.Type, totalCount = result.TotalCount });
        }

        [HttpPost]
        public async Task<IActionResult> SubmitReplyAjax([FromForm] int parentRatingId, [FromForm] int drinkId, [FromForm] string comment, [FromForm] IFormFile imageFile)
        {
            if (!User.Identity.IsAuthenticated)
                return Json(new { success = false, message = "Vui lòng đăng nhập để bình luận", status = 401 });

            var customerId = GetCurrentCustomerId();
            if (customerId == null) return Json(new { success = false, message = "Unauthorized", status = 401 });

            if (string.IsNullOrWhiteSpace(comment) && imageFile == null)
                return Json(new { success = false, message = "Bạn chưa nhập nội dung bình luận!" });

            // Chống spam thao tác nhanh
            var cacheKey = $"reply_spam_{customerId}";
            if (_cache.TryGetValue(cacheKey, out _))
            {
                return Json(new { success = false, message = "Bạn bình luận quá nhanh, vui lòng chờ chút nhé!", status = 429 });
            }
            _cache.Set(cacheKey, true, TimeSpan.FromSeconds(5)); // Block gửi quá nhanh trong 5 giây

            using var stream = imageFile?.OpenReadStream();
            var result = await _drinkService.SubmitReplyAsync(customerId.Value, parentRatingId, drinkId, comment, stream, imageFile?.FileName, imageFile?.Length, _webHostEnvironment.WebRootPath);
            return Json(new { success = result.Success, message = result.Message, ratingId = result.RatingId, imageUrl = result.ImageUrl });
        }
    }
}
    
