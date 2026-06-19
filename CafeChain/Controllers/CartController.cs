using CafeChain.Application.DTOs;
using CafeChain.Application.Interfaces;
using CafeChain.Data;
using CafeChain.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Controllers
{
    // ✅ [FIX 1] Bỏ [Authorize] khỏi class. Khách vãng lai được phép xem giỏ hàng và thêm sản phẩm (Session-based).
    // Chỉ CheckoutController mới cần [Authorize] để bắt buộc đăng nhập trước khi đặt hàng.
    public class CartController : Controller
    {
        private readonly ICartService _cartService;
      

        public CartController(ICartService cartService)
        {
            _cartService = cartService;
        }
        public IActionResult Index()
        {
            var cart = _cartService.GetCart();
            var viewModel = new CartViewModel
            {
                Items = cart,
                VoucherDiscount = 0
            };
            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> AddToCart(int id)
        {
            // Controller không còn đụng vào _context nữa
            // Nó chỉ việc "nhờ" Service làm hộ
            var result = await _cartService.AddDrinkToCartAsync(id);

            if (!result) return NotFound();

            return Json(new
            {
                success = true,
                totalCount = _cartService.GetTotalCount()
            });
        }
        [HttpPost]
        public async Task<IActionResult> AddToCartAdvance([FromBody] AddToCartRequest request)
        {
            var result = await _cartService.AddToCartAdvanceAsync(request);

            if (!result) return BadRequest(new { success = false, message = "Có lỗi xảy ra khi thêm món!" });

            // Trả về success và tổng số lượng mới để JS cập nhật cái cục đỏ trên Header
            return Json(new { success = true, totalCount = _cartService.GetTotalCount() });
        }
        [HttpPost]
        public IActionResult UpdateQuantity(string id, int quantity) // Đổi int thành string
        {
            _cartService.UpdateQuantity(id, quantity);
            return Json(new { success = true });
        }

        [HttpPost]
        public IActionResult RemoveItem(string id) // Đổi int thành string
        {
            _cartService.RemoveFromCart(id);
            return Json(new { success = true });
        }
    }
}