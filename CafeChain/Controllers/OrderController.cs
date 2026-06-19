using System.Security.Claims;
using System.Linq;
using System.Threading.Tasks;
using CafeChain.Application.Interfaces;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs;
using CafeChain.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Controllers
{
    [Authorize]
    public class OrderController : Controller
    {
        private readonly IOrderService _orderService;
        private readonly ICartService _cartService;
        private readonly IInventoryService _inventoryService;
        private readonly AppDbContext _context;

        public OrderController(
            IOrderService orderService,
            ICartService cartService,
            IInventoryService inventoryService,
            AppDbContext context)
        {
            _orderService = orderService;
            _cartService = cartService;
            _inventoryService = inventoryService;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> History(int page = 1, string statusGroup = null)
        {
            var customerIdStr = User.FindFirstValue("CustomerId");
            if (!int.TryParse(customerIdStr, out int customerId))
                return RedirectToAction("Login", "Account");

            var pagedOrders = await _orderService.GetCustomerOrdersAsync(customerId, page, 10, statusGroup);
            ViewBag.CurrentStatusGroup = statusGroup;
            ViewBag.CartItemCount = _cartService.GetTotalCount();
            return View(pagedOrders);
        }

        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            var customerIdStr = User.FindFirstValue("CustomerId");
            if (!int.TryParse(customerIdStr, out int customerId))
                return RedirectToAction("Login", "Account");

            var orderDetail = await _orderService.GetCustomerOrderDetailAsync(id, customerId);
            if (orderDetail == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy đơn hàng, hoặc bạn không có quyền xem đơn hàng này.";
                return RedirectToAction("History");
            }
            return View(orderDetail);
        }

        // [FIX BUG 5] Khách tự hủy đơn chưa thanh toán hoặc COD chưa được quán duyệt (Bọc thép & Bảo mật)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOrder(int orderId)
        {
            var customerIdStr = User.FindFirstValue("CustomerId");
            if (!int.TryParse(customerIdStr, out int customerId))
                return RedirectToAction("Login", "Account");

            try
            {
                // [REFACTOR] Gọi Service thay vì thao tác Raw SQL trực tiếp.
                // Service đã bọc Transaction để bảo toàn tồn kho (Skill.md §4).
                bool success = await _orderService.CancelOrderAsync(orderId, customerId);

                if (success)
                {
                    TempData["SuccessMessage"] = "Đã hủy đơn thành công. Kho nguyên liệu đã được hoàn trả.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Không thể hủy — đơn đã được hệ thống xử lý trước đó hoặc không thuộc quyền sở hữu của bạn.";
                }
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "Hệ thống đang gặp sự cố khi xử lý trạng thái. Vui lòng thử lại sau hoặc liên hệ Hotline.";
            }

            return RedirectToAction("History");
        }

        // [FIX BUG 5 + Re-order Price Fix] Mua lại đơn cũ với giá menu HIỆN TẠI
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReOrder(int orderId)
        {
            var customerIdStr = User.FindFirstValue("CustomerId");
            if (!int.TryParse(customerIdStr, out int customerId))
                return RedirectToAction("Login", "Account");

            // IDOR Guard: chỉ truy cập đơn của chính khách
            var order = await _context.Orders
                .Include(o => o.OrderDetails).ThenInclude(od => od.OrderToppings)
                .FirstOrDefaultAsync(o => o.OrderId == orderId && o.CustomerId == customerId);

            if (order == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy đơn hàng để mua lại.";
                return RedirectToAction("History");
            }

            // Xóa giỏ hàng cũ (User đã confirm qua JS trước khi POST)
            _cartService.ClearCart();

            foreach (var item in order.OrderDetails)
            {
                // Dùng AddToCartAdvanceAsync để lấy giá THỰC TẾ hiện tại từ DB
                // KHÔNG dùng giá snapshot cũ trong OrderDetail để tránh bán giá cũ
                var request = new AddToCartRequest
                {
                    DrinkId = item.DrinkId,
                    SizeId = item.SizeId ?? 0,
                    Quantity = item.Quantity,
                    Note = item.Note,
                    OptionalToppingIds = item.OrderToppings.Select(t => t.ToppingId).ToList()
                };
                await _cartService.AddToCartAdvanceAsync(request);
            }

            TempData["SuccessMessage"] = "Đã khôi phục đơn hàng vào giỏ với giá menu hiện tại. Xem lại trước khi đặt nhé!";
            return RedirectToAction("Index", "Cart");
        }
    }
}
