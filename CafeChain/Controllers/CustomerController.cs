using CafeChain.Application.DTOs.Customer;
using CafeChain.Application.DTOs.Customers;
using CafeChain.Application.Interfaces.Customers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CafeChain.Controllers
{
    [Authorize]
    public class CustomerController : Controller
    {
        private readonly ICustomerService _customerService;

        public CustomerController(
            ICustomerService customerService)
        {
            _customerService = customerService;
        }

        // =====================================================
        // PROFILE
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var accountId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(accountId))
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            var viewModel =
                await _customerService
                    .GetCustomerProfileAsync(accountId);

            if (viewModel == null)
            {
                return NotFound(
                    "Không tìm thấy thông tin khách hàng.");
            }

            return View(viewModel);
        }

        // =====================================================
        // UPDATE AVATAR
        // =====================================================

        [HttpPost]
        public async Task<IActionResult> UpdateAvatar(
            IFormFile avatarFile)
        {
            if (avatarFile == null ||
                avatarFile.Length == 0)
            {
                return Json(new
                {
                    success = false,
                    message = "File không hợp lệ."
                });
            }

            var customerId =
                GetCustomerId();

            if (!customerId.HasValue)
            {
                return Unauthorized();
            }

            var avatarUrl =
                await _customerService
                    .UpdateAvatarAsync(
                        customerId.Value,
                        avatarFile);

            await RefreshAvatarClaimAsync(
                avatarUrl);

            return Json(new
            {
                success = true,
                imageUrl = avatarUrl,
                isReused = false
            });
        }

        // =====================================================
        // UPDATE PROFILE
        // =====================================================

        [HttpPost]
        public async Task<IActionResult> UpdateProfile(
            [FromBody] UpdateProfileRequest request)
        {
            if (!ModelState.IsValid)
            {
                return Json(new
                {
                    success = false,
                    message =
                        GetFirstModelError()
                        ?? "Dữ liệu không hợp lệ."
                });
            }

            var customerId =
                GetCustomerId();

            if (!customerId.HasValue)
            {
                return Unauthorized();
            }

            var success =
                await _customerService
                    .UpdateProfileAsync(
                        customerId.Value,
                        request);

            if (!success)
            {
                return Json(new
                {
                    success = false,
                    message =
                        "Không có thay đổi nào được lưu."
                });
            }

            await RefreshNameClaimAsync(
                request.FullName.Trim());

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
            var accountId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(accountId))
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            var viewModel =
                await _customerService
                    .GetCustomerProfileAsync(accountId);

            if (viewModel == null)
            {
                return NotFound(
                    "Không tìm thấy thông tin khách hàng.");
            }

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> UpdatePassword(
            [FromBody] ChangePasswordRequest request)
        {
            if (!ModelState.IsValid)
            {
                return Json(new
                {
                    success = false,
                    message =
                        GetFirstModelError()
                        ?? "Dữ liệu không hợp lệ."
                });
            }

            var accountId =
                GetAccountId();

            if (!accountId.HasValue)
            {
                return Unauthorized();
            }

            var result =
                await _customerService
                    .ChangePasswordAsync(
                        accountId.Value,
                        request);

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
            var provinces =
                await _customerService
                    .GetProvincesAsync();

            return Json(
                provinces.Select(x => new
                {
                    code = x.ProvinceId,
                    name = x.Name
                }));
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetWards(
            int provinceId)
        {
            var wards =
                await _customerService
                    .GetWardsByProvinceAsync(
                        provinceId);

            return Json(
                wards.Select(x => new
                {
                    code = x.WardId,
                    name = x.Name
                }));
        }

        // =====================================================
        // MY VOUCHERS — soft-removal (out of product scope)
        // =====================================================

        [HttpGet]
        public IActionResult MyVouchers()
        {
            return NotFound(new
            {
                errorCode = CafeChain.Application.Constants.ProductScopeErrorCodes.FeatureNotAvailable,
                message = CafeChain.Application.Constants.ProductScopeErrorCodes.VoucherNotAvailableMessage
            });
        }

        // =====================================================
        // PRIVATE HELPERS
        // =====================================================

        private int? GetCustomerId()
        {
            var customerIdStr =
                User.FindFirstValue("CustomerId");

            return int.TryParse(
                customerIdStr,
                out var customerId)
                ? customerId
                : null;
        }

        private int? GetAccountId()
        {
            var accountIdStr =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

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

        private async Task RefreshAvatarClaimAsync(
            string avatarUrl)
        {
            var authentication = await HttpContext.AuthenticateAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);
            var identity =
                (ClaimsIdentity)User.Identity!;

            var avatarClaim =
                identity.FindFirst("AvatarUrl");

            if (avatarClaim != null)
            {
                identity.RemoveClaim(
                    avatarClaim);
            }

            identity.AddClaim(
                new Claim(
                    "AvatarUrl",
                    avatarUrl));

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                authentication.Properties ?? new AuthenticationProperties { AllowRefresh = true });
        }

        private async Task RefreshNameClaimAsync(
            string fullName)
        {
            var authentication = await HttpContext.AuthenticateAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);
            var identity =
                (ClaimsIdentity)User.Identity!;

            var nameClaim =
                identity.FindFirst(
                    ClaimTypes.Name);

            if (nameClaim != null)
            {
                identity.RemoveClaim(
                    nameClaim);
            }

            identity.AddClaim(
                new Claim(
                    ClaimTypes.Name,
                    fullName));

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                authentication.Properties ?? new AuthenticationProperties { AllowRefresh = true });
        }
    }
}
