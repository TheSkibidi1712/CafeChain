using CafeChain.Application.DTOs.Customer;
using CafeChain.Application.DTOs.Customers;
using CafeChain.Application.Interfaces.Customers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace CafeChain.Controllers
{
    [Authorize] // Bắt buộc phải đăng nhập mới được vào đây
    public class CustomerController : Controller
    {
        private readonly ICustomerService _customerService;
        // Bác nhớ Inject IWebHostEnvironment vào Controller để lấy đường dẫn thư mục wwwroot nhé
        private readonly IWebHostEnvironment _env;

        public CustomerController(ICustomerService customerService, IWebHostEnvironment env)
        {
            _customerService = customerService;
            _env = env;
        }
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            // Lấy AccountId từ cái Claim mà bác đã set lúc Login
            // (new Claim(ClaimTypes.NameIdentifier, result.Data.AccountId.ToString()))
            var accountId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(accountId))
            {
                return RedirectToAction("Login", "Account");
            }

            // Gọi Service để lấy toàn bộ dữ liệu Profile
            var viewModel = await _customerService.GetCustomerProfileAsync(accountId);

            if (viewModel == null)
            {
                return NotFound("Không tìm thấy thông tin khách hàng.");
            }

            return View(viewModel); // Trả về file Views/Customer/Profile.cshtml
        }
        [HttpPost]
        public async Task<IActionResult> UpdateAvatar(IFormFile avatarFile)
        {
            if (avatarFile == null || avatarFile.Length == 0)
                return Json(new { success = false, message = "File không hợp lệ." });

            var customerIdStr = User.FindFirstValue("CustomerId");
            var customerId = int.Parse(customerIdStr);

            var result = await _customerService.UpdateAvatarAsync(customerId, avatarFile);

            // 🔥 BÍ KÍP CẬP NHẬT ẢNH TRÊN HEADER (CẤP LẠI COOKIE) 🔥
            if (result.Url != null)
            {
                var identity = (ClaimsIdentity)User.Identity;
                var avatarClaim = identity.FindFirst("AvatarUrl"); // Tìm link ảnh cũ

                if (avatarClaim != null)
                {
                    identity.RemoveClaim(avatarClaim); // Xé bỏ link ảnh cũ
                }
                // Dán link ảnh mới vào Cookie
                identity.AddClaim(new Claim("AvatarUrl", result.Url));

                // Bắt trình duyệt lưu lại ngay lập tức
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(identity)
                );
            }

            return Json(new
            {
                success = true,
                imageUrl = result.Url,
                isReused = result.IsReused
            });
        }
        [HttpPost]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            // 1. Lấy ID
            var customerIdStr = User.FindFirstValue("CustomerId");
            if (!int.TryParse(customerIdStr, out int customerId)) return Unauthorized();

            // 2. Lưu vào Database qua Service
            var result = await _customerService.UpdateProfileAsync(customerId, request);

            if (result)
            {
                // 3. 🔥 BÍ KÍP CẬP NHẬT TÊN TRÊN HEADER (CẤP LẠI COOKIE) 🔥
                var identity = (ClaimsIdentity)User.Identity;
                var nameClaim = identity.FindFirst(ClaimTypes.Name); // Tìm cái tên cũ

                if (nameClaim != null)
                {
                    identity.RemoveClaim(nameClaim); // Xé bỏ tên cũ "Phuc Gia"
                    identity.AddClaim(new Claim(ClaimTypes.Name, request.FullName)); // Dán tên mới "To ka du" vào

                    // Bắt trình duyệt lưu lại Cookie mới ngay lập tức
                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(identity)
                    );
                }

                return Json(new { success = true, message = "Cập nhật thành công!" });
            }

            return Json(new { success = false, message = "Không có thay đổi nào được lưu." });
        }
        [HttpGet]
        public async Task<IActionResult> ChangePassword()
        {
            var accountIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(accountIdStr)) return RedirectToAction("Login", "Account");

            // Vẫn phải gọi cái này để cái Sidebar bên trái nó không bị lỗi Null
            var viewModel = await _customerService.GetCustomerProfileAsync(accountIdStr);

            return View(viewModel);
        }
    }
}