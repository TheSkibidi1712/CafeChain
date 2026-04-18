using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using CafeChain.Application.DTOs;
using CafeChain.Application.Interfaces;
using CafeChain.ViewModels.Cart;
using CafeChain.ViewModels.Customers;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Controllers
{
    public class CheckoutController : Controller
    {
        private readonly IOrderService _orderService;
        private readonly ICartService _cartService;

        // [REFACTOR] Đã xóa AppDbContext — Mọi truy vấn DB giờ đều qua Service (Skill.md §1)
        public CheckoutController(IOrderService orderService, ICartService cartService)
        {
            _orderService = orderService;
            _cartService = cartService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var cart = _cartService.GetCart();
            if (cart == null || !cart.Any())
                return RedirectToAction("Index", "Cart");

            var model = new CheckoutViewModel
            {
                Items = cart.Select(c => new CartItemViewModel
                {
                    CartItemId = c.CartItemId,
                    DrinkId = c.DrinkId,
                    SizeId = c.SizeId,
                    ToppingIds = c.ToppingIds,
                    Name = c.Name,
                    ImageUrl = c.ImageUrl,
                    SizeName = c.SizeName,
                    ToppingNames = c.AddedToppings,
                    Price = c.Price,
                    Quantity = c.Quantity,
                    Note = c.Note
                }).ToList(),
                SubTotal = cart.Sum(c => c.Total),
                CheckoutToken = Guid.NewGuid()
            };

            // Lấy danh sách PTM để hiển thị
            ViewBag.PaymentMethods = new List<dynamic> { 
                new { Id = 1, Name = "Tiền mặt (COD)", Code = "CASH", Icon = "bi-cash-coin" },
                new { Id = 2, Name = "Chuyển khoản", Code = "BANK", Icon = "bi-bank" },
                new { Id = 3, Name = "Ví MoMo", Code = "MOMO", Icon = "bi-wallet2" }
            };

            // 🛡️ Lấy CustomerId từ Claim để truy vấn địa chỉ và SĐT thật — QUA SERVICE
            var customerIdStr = User.FindFirstValue("CustomerId");
            List<CustomerAddressViewModel> savedAddresses = new();
            if (int.TryParse(customerIdStr, out int customerId))
            {
                savedAddresses = await _orderService.GetSavedAddressesAsync(customerId);

                // [REFACTOR] Gọi Service thay vì _context trực tiếp
                ViewBag.SavedPhones = await _orderService.GetCustomerPhonesAsync(customerId);

                // Tự động chọn mặc định
                if (model.SelectedAddressId == 0 && savedAddresses.Any())
                    model.SelectedAddressId = savedAddresses.First().CustomerAddressId;
                
                var phones = (List<CustomerPhoneViewModel>)ViewBag.SavedPhones;
                if (model.SelectedPhoneId == 0 && phones.Any())
                    model.SelectedPhoneId = phones.First().CustomerPhoneId;

                // [REFACTOR] Lấy tên KH qua Service
                ViewBag.CustomerName = await _orderService.GetCustomerNameAsync(customerId);
            }
            ViewBag.SavedAddresses = savedAddresses;

            // Lấy danh sách Voucher khả dụng
            ViewBag.AvailableVouchers = await _orderService.GetAvailableVouchersAsync();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(CheckoutViewModel model)
        {
            var cart = _cartService.GetCart();
            
            if (!ModelState.IsValid)
            {
                await ReloadCheckoutViewData(model, cart);
                return View(model);
            }

           try
            {
                // Lấy CustomerId từ Claims nếu đã đăng nhập
                int? customerId = null;
                var customerIdStr = User.FindFirstValue("CustomerId");
                if (int.TryParse(customerIdStr, out var id)) customerId = id;

                // Chuyển đổi Cart DTO sang CartItemViewModel
                var sessionCart = cart.Select(c => new CartItemViewModel
                {
                    DrinkId = c.DrinkId,
                    SizeId = c.SizeId,
                    ToppingIds = c.ToppingIds,
                    Quantity = c.Quantity,
                    Note = c.Note,
                    Name = c.Name 
                }).ToList();

                // 1. Tạo đơn hàng vào Database (Lúc này OrderStatus = Draft, PaymentStatus = Pending)
                int orderId = await _orderService.PlaceOrderAsync(model, customerId, sessionCart);

                // 2. Thành công: Xóa giỏ hàng (Session-based — nằm ngoài DB Transaction theo Skill.md §4.1)
                _cartService.ClearCart();

                // 3. 🛡️ RẼ NHÁNH DỰA VÀO PHƯƠNG THỨC THANH TOÁN
                if (model.PaymentMethodId == 2 || model.PaymentMethodId == 3) 
                {
                    // Chuyển khoản (2) hoặc MoMo (3) -> Đẩy sang PaymentController để sinh mã QR
                    return RedirectToAction("GenerateQR", "Payment", new { orderId = orderId });
                }
                
                // Nếu là Tiền mặt COD (PaymentMethodId == 1) -> Cho qua trang Thành công luôn
                return RedirectToAction("Success", new { id = orderId });
            }
            catch (Exception ex)
            {
                // 🔴 Catch lỗi nghiệp vụ từ Service (hết hàng, sai giá, token trùng...)
                ModelState.AddModelError(string.Empty, ex.Message);
                
                await ReloadCheckoutViewData(model, cart);
                return View(model);
            }
        }

        [HttpGet]
        public IActionResult Success(int id)
        {
            ViewBag.OrderId = id;
            return View();
        }

        /// <summary>
        /// Helper: Nạp lại toàn bộ dữ liệu cần thiết cho View khi validation fail hoặc exception.
        /// Tách riêng để giảm duplicate code (DRY Principle).
        /// </summary>
        private async Task ReloadCheckoutViewData(CheckoutViewModel model, List<CartItem> cart)
        {
            model.Items = cart.Select(c => new CartItemViewModel
            {
                CartItemId = c.CartItemId,
                DrinkId = c.DrinkId,
                SizeId = c.SizeId,
                ToppingIds = c.ToppingIds,
                Name = c.Name,
                ImageUrl = c.ImageUrl,
                SizeName = c.SizeName,
                ToppingNames = c.AddedToppings,
                Price = c.Price,
                Quantity = c.Quantity,
                Note = c.Note
            }).ToList();
            model.SubTotal = cart.Sum(c => c.Total);

            // 🛡️ Nạp lại địa chỉ, SĐT, tên KH qua Service
            var customerIdStr = User.FindFirstValue("CustomerId");
            if (int.TryParse(customerIdStr, out int customerId))
            {
                ViewBag.SavedAddresses = await _orderService.GetSavedAddressesAsync(customerId);
                ViewBag.SavedPhones = await _orderService.GetCustomerPhonesAsync(customerId);
                ViewBag.CustomerName = await _orderService.GetCustomerNameAsync(customerId);
            }
            else
            {
                ViewBag.SavedAddresses = new List<CustomerAddressViewModel>();
                ViewBag.SavedPhones = new List<CustomerPhoneViewModel>();
                ViewBag.CustomerName = null;
            }

            ViewBag.PaymentMethods = new List<dynamic> { 
                new { Id = 1, Name = "Tiền mặt (COD)", Code = "CASH", Icon = "bi-cash-coin" },
                new { Id = 2, Name = "Chuyển khoản", Code = "BANK", Icon = "bi-bank" },
                new { Id = 3, Name = "Ví MoMo", Code = "MOMO", Icon = "bi-wallet2" }
            };

            // Lấy danh sách Voucher khả dụng
            ViewBag.AvailableVouchers = await _orderService.GetAvailableVouchersAsync();
        }
    }
}
