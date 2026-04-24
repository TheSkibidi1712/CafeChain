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
using CafeChain.ViewModels.Customers;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Controllers
{
    [Authorize] // Bắt buộc phải đăng nhập mới được vào đây
    public class CustomerController : Controller
    {
        private readonly ICustomerService _customerService;
        // Bác nhớ Inject IWebHostEnvironment vào Controller để lấy đường dẫn thư mục wwwroot nhé
        private readonly IWebHostEnvironment _env;
        private readonly CafeChain.Data.AppDbContext _context;

        public CustomerController(ICustomerService customerService, IWebHostEnvironment env, CafeChain.Data.AppDbContext context)
        {
            _customerService = customerService;
            _env = env;
            _context = context;
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
            // =========================
            // VALIDATE FULL NAME
            // =========================

            if (string.IsNullOrWhiteSpace(request.FullName))
            {
                return Json(new
                {
                    success = false,
                    message = "Họ và tên không được để trống."
                });
            }

            request.FullName = request.FullName.Trim();

            if (request.FullName.Length < 2)
            {
                return Json(new
                {
                    success = false,
                    message = "Họ và tên phải có ít nhất 2 ký tự."
                });
            }

            if (request.FullName.Length > 100)
            {
                return Json(new
                {
                    success = false,
                    message = "Họ và tên không được vượt quá 100 ký tự."
                });
            }

            // chặn chỉ nhập số hoặc ký tự rác
            if (!System.Text.RegularExpressions.Regex.IsMatch(
                request.FullName,
                @"^[\p{L}\s]+$"))
            {
                return Json(new
                {
                    success = false,
                    message = "Họ và tên chỉ được chứa chữ cái và khoảng trắng."
                });
            }

            // =========================
            // LẤY CUSTOMER ID
            // =========================

            var customerIdStr = User.FindFirstValue("CustomerId");

            if (!int.TryParse(customerIdStr, out int customerId))
                return Unauthorized();

            // =========================
            // SAVE DB
            // =========================

            var result = await _customerService.UpdateProfileAsync(customerId, request);

            if (result)
            {
                // =========================
                // UPDATE CLAIM NAME
                // =========================

                var identity = (ClaimsIdentity)User.Identity;
                var nameClaim = identity.FindFirst(ClaimTypes.Name);

                if (nameClaim != null)
                {
                    identity.RemoveClaim(nameClaim);
                }

                identity.AddClaim(new Claim(ClaimTypes.Name, request.FullName));

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(identity)
                );

                return Json(new
                {
                    success = true,
                    message = "Cập nhật thành công!"
                });
            }

            return Json(new
            {
                success = false,
                message = "Không có thay đổi nào được lưu."
            });
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
        [HttpPost]
        public async Task<IActionResult> UpdatePassword([FromBody] ChangePasswordViewModel model)
        {
            // Kiểm tra Validation từ ViewModel
            if (!ModelState.IsValid)
            {
                var errorMsg = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage;
                return Json(new { success = false, message = errorMsg ?? "Dữ liệu không hợp lệ." });
            }

            // Lấy AccountId từ Cookie Auth
            var accountIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(accountIdStr, out int accountId))
            {
                return Unauthorized();
            }

            // Gọi Service thực thi
            var result = await _customerService.ChangePasswordAsync(accountId, model);

            return Json(new { success = result.Success, message = result.Message });
        }

        // ======================= LOCATION ENDPOINTS =========================
        [HttpGet]
        [AllowAnonymous] // Phường/Xã thì không nhất thiết phải login mới load được
        public async Task<IActionResult> GetProvinces()
        {
            var provinces = await _customerService.GetProvincesAsync();
            // Map sang cùng cấu trúc "code" và "name" để JS khỏi phải sửa nhiều
            var data = provinces.Select(p => new { code = p.ProvinceId, name = p.Name }).ToList();
            return Json(data);
        }

        [HttpGet]
        [AllowAnonymous] // Cấp Quận/Huyện
        public async Task<IActionResult> GetDistricts(int provinceId)
        {
            var districts = await _customerService.GetDistrictsByProvinceAsync(provinceId);
            var data = districts.Select(d => new { code = d.DistrictId, name = d.Name }).ToList();
            return Json(data);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetWards(int districtId)
        {
            var wards = await _customerService.GetWardsByDistrictAsync(districtId);
            var data = wards.Select(w => new { code = w.WardId, name = w.Name }).ToList();
            return Json(data);
        }

        // ======================= WALLET VOUCHERS =========================
        [HttpGet]
        public async Task<IActionResult> MyVouchers()
        {
            var customerIdStr = User.FindFirstValue("CustomerId");
            if (!int.TryParse(customerIdStr, out int customerId)) return Unauthorized();

            var accountIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var customerProfile = await _customerService.GetCustomerProfileAsync(accountIdStr);

            var myVouchers = await _context.CustomerVouchers
                .Include(cv => cv.Voucher)
                .Where(cv => cv.CustomerId == customerId)
                .ToListAsync();

            var now = DateTime.Now;

            var viewModel = new CafeChain.ViewModels.Customers.MyVouchersViewModel
            {
                Profile = customerProfile,
                ValidVouchers = myVouchers.Where(cv => !cv.IsUsed && (!cv.Voucher.EndDate.HasValue || cv.Voucher.EndDate >= now)).ToList(),
                UsedVouchers = myVouchers.Where(cv => cv.IsUsed).ToList(),
                ExpiredVouchers = myVouchers.Where(cv => !cv.IsUsed && cv.Voucher.EndDate.HasValue && cv.Voucher.EndDate < now).ToList()
            };

            return View(viewModel);
        }
    }
}