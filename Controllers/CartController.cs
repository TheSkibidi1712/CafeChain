using CafeChain.Application.DTOs;
using CafeChain.Application.Interfaces;
using CafeChain.Data;
using CafeChain.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Controllers
{
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
                Discount = cart.Any() ? 10000 : 0
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
        public IActionResult UpdateQuantity(int id, int quantity)
        {
            _cartService.UpdateQuantity(id, quantity);
            return Json(new { success = true });
        }

        [HttpPost]
        public IActionResult RemoveItem(int id)
        {
            _cartService.RemoveFromCart(id);
            return Json(new { success = true });
        }
    }
}