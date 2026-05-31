using CafeChain.Application.DTOs.Customer;
using CafeChain.Application.DTOs.Customers;
using CafeChain.Application.Interfaces.Customers;
using CafeChain.ViewModels.Customers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using CafeChain.Data;

namespace CafeChain.Controllers
{
    [Authorize]
    public class CustomerController : Controller
    {
        private readonly ICustomerService _customerService;
        private readonly IWebHostEnvironment _env;
        private readonly AppDbContext _context;

        public CustomerController(ICustomerService customerService, IWebHostEnvironment env, AppDbContext context)
        {
            _customerService = customerService;
            _env = env;
            _context = context;
        }

        // =====================================================
        // PROFILE
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var accountId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(accountId))
            {
                return RedirectToAction("Login", "Account");
            }

            var viewModel = await _customerService.GetCustomerProfileAsync(accountId);

            if (viewModel == null)
            {
                return NotFound("Không tìm thấy thông tin khách hàng.");
            }

            return View(viewModel);
        }

        // =====================================================
        // AVATAR
        // =====================================================

        [HttpPost]
        public async Task<IActionResult> UpdateAvatar(IFormFile avatarFile)
        {
            if (avatarFile == null || avatarFile.Length == 0)
            {
                return Json(new
                {
                    success = false,
                    message = "File không hợp lệ."
                });
            }

            var customerId = GetCustomerId();

            if (!customerId.HasValue)
            {
                return Unauthorized();
            }

            var avatarUrl = await _customerService.UpdateAvatarAsync(customerId.Value, avatarFile);

            await RefreshAvatarClaimAsync(avatarUrl);

            return Json(new
            {
                success = true,
                imageUrl = avatarUrl
            });
        }

        // =====================================================
        // UPDATE PROFILE
        // =====================================================

        [HttpPost]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            if (!ModelState.IsValid)
            {
                return Json(new
                {
                    success = false,
                    message = GetFirstModelError()
                              ?? "Dữ liệu không hợp lệ."
                });
            }

            var customerId = GetCustomerId();

            if (!customerId.HasValue)
            {
                return Unauthorized();
            }

            var result = await _customerService.UpdateProfileAsync(customerId.Value, request);

            if (!result)
            {
                return Json(new
                {
                    success = false,
                    message = "Không có thay đổi nào được lưu."
                });
            }

            await RefreshNameClaimAsync(request.FullName.Trim());

            return Json(new
            {
                success = true,
                message = "Cập nhật thành công!"
            });
        }

        // =====================================================
        // CHANGE PASSWORD
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> ChangePassword()
        {
            var accountId =User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(accountId))
            {
                return RedirectToAction("Login", "Account");
            }

            var viewModel = await _customerService.GetCustomerProfileAsync(accountId);

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> UpdatePassword([FromBody] ChangePasswordRequest request)
        {
            if (!ModelState.IsValid)
            {
                return Json(new
                {
                    success = false,
                    message = GetFirstModelError()
                              ?? "Dữ liệu không hợp lệ."
                });
            }

            var accountId = GetAccountId();

            if (!accountId.HasValue)
            {
                return Unauthorized();
            }

            var result = await _customerService.ChangePasswordAsync(accountId.Value, request);

            return Json(new
            {
                success = result.Success,
                message = result.Message
            });
        }

        // =====================================================
        // LOCATION
        // =====================================================

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetProvinces()
        {
            var provinces = await _customerService.GetProvincesAsync();

            return Json(
                provinces.Select(x => new
                {
                    code = x.ProvinceId,
                    name = x.Name
                }));
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetDistricts(int provinceId)
        {
            var districts = await _customerService.GetDistrictsByProvinceAsync(provinceId);

            return Json(
                districts.Select(x => new
                {
                    code = x.DistrictId,
                    name = x.Name
                }));
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetWards(int districtId)
        {
            var wards = await _customerService.GetWardsByDistrictAsync(districtId);

            return Json(
                wards.Select(x => new
                {
                    code = x.WardId,
                    name = x.Name
                }));
        }

        // =====================================================
        // MY VOUCHERS
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> MyVouchers()
        {
            var customerId = GetCustomerId();

            if (!customerId.HasValue)
            {
                return Unauthorized();
            }

            var accountId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var profile = await _customerService.GetCustomerProfileAsync(accountId!);

            var customerVouchers = await _context.CustomerVouchers
                    .Include(x => x.Voucher)
                    .Where(x =>
                        x.CustomerId ==
                        customerId.Value)
                    .ToListAsync();

            var now = DateTime.Now;

            var viewModel =
                new MyVouchersViewModel
                {
                    Profile = profile,

                    ValidVouchers =
                        customerVouchers
                            .Where(x =>
                                !x.IsUsed &&
                                (!x.Voucher.EndDate.HasValue ||
                                 x.Voucher.EndDate >= now))
                            .ToList(),

                    UsedVouchers =
                        customerVouchers
                            .Where(x => x.IsUsed)
                            .ToList(),

                    ExpiredVouchers =
                        customerVouchers
                            .Where(x =>
                                !x.IsUsed &&
                                x.Voucher.EndDate.HasValue &&
                                x.Voucher.EndDate < now)
                            .ToList()
                };

            return View(viewModel);
        }

        // =====================================================
        // PRIVATE HELPERS
        // =====================================================

        private int? GetCustomerId()
        {
            var customerIdStr = User.FindFirstValue("CustomerId");

            return int.TryParse(
                customerIdStr,
                out var customerId)
                ? customerId
                : null;
        }

        private int? GetAccountId()
        {
            var accountIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

            return int.TryParse(
                accountIdStr,
                out var accountId)
                ? accountId
                : null;
        }

        private string? GetFirstModelError()
        {
            return ModelState.Values
                .SelectMany(x => x.Errors)
                .FirstOrDefault()
                ?.ErrorMessage;
        }

        private async Task RefreshAvatarClaimAsync(string avatarUrl)
        {
            var identity = (ClaimsIdentity)User.Identity!;

            var avatarClaim = identity.FindFirst("AvatarUrl");

            if (avatarClaim != null)
            {
                identity.RemoveClaim(avatarClaim);
            }

            identity.AddClaim(new Claim("AvatarUrl", avatarUrl));

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity));
        }

        private async Task RefreshNameClaimAsync(string fullName)
        {
            var identity = (ClaimsIdentity)User.Identity!;

            var nameClaim = identity.FindFirst(ClaimTypes.Name);

            if (nameClaim != null)
            {
                identity.RemoveClaim(nameClaim);
            }

            identity.AddClaim(
                new Claim(
                    ClaimTypes.Name,
                    fullName));

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity));
        }
    }
}