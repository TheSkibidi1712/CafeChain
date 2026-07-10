using CafeChain.Application.DTOs.Accounts;
using CafeChain.Application.Interfaces.Accounts;
using CafeChain.Application.Results;
using CafeChain.ViewModels.Accounts;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using System.Collections.Generic;
using CafeChain.Application.Constants;

namespace CafeChain.Controllers
{
    public class AccountController : Controller
    {
        private readonly IAccountService _accountService;

        public AccountController(IAccountService accountService)
        {
            _accountService = accountService;
        }

        // ========================= REGISTER =========================

        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home"); // hoặc dashboard
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }

            // Validate AcceptTerms thủ công (bool false không trigger [Required])
            if (!model.AcceptTerms)
                ModelState.AddModelError("AcceptTerms", "Bạn phải đồng ý với điều khoản");

            // Validate ngày sinh (nếu có nhập)
            if (!string.IsNullOrWhiteSpace(model.DateOfBirthText) && model.DateOfBirth == null)
                ModelState.AddModelError("DateOfBirthText", "Ngày sinh không hợp lệ (dd/MM/yyyy)");

            if (model.DateOfBirth.HasValue && model.DateOfBirth > DateTime.Today)
                ModelState.AddModelError("DateOfBirthText", "Ngày sinh không được lớn hơn hôm nay");

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var dto = new RegisterDto
            {
                FullName = model.FullName,
                Email = model.Email,
                PhoneNumber = model.PhoneNumber,
                Password = model.Password,
                Gender = model.Gender,
                DateOfBirth = model.DateOfBirth
            };

            var result = await _accountService.RegisterCustomerAsync(dto);

            if (result.IsSuccess)
            {
                TempData["SuccessMessage"] = result.Message;
                return RedirectToAction("Login");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return View(model);
        }

        // ========================= LOGIN =========================

        [HttpGet]
        public IActionResult Login(string? email = null, bool isLocked = false, int minutes = 0)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewBag.IsLocked = isLocked;

            if (isLocked)
            {
                ViewBag.LockMinutes = minutes;

                ViewBag.LockMessage =
                    $"Tài khoản bị khóa. Thử lại sau {minutes} phút";
            }

            return View(
                new LoginViewModel
                {
                    Email = email
                });
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction(
                    "Index",
                    "Home");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result =
                await _accountService.LoginAsync(
                    new LoginDto
                    {
                        Email = model.Email,
                        Password = model.Password,
                        RememberMe = model.RememberMe
                    });

            if (!result.IsSuccess)
            {
                await Task.Delay(800);

                if (result.Data?.IsLocked == true)
                {
                    return RedirectToAction(
                        nameof(Login),
                        new
                        {
                            email = model.Email,

                            isLocked = true,

                            minutes = result.Data.LockRemainingMinutes
                        });
                }

                ModelState.AddModelError(
                    string.Empty,
                    result.Message);

                return View(model);
            }

            await SignInAsync(result.Data, model.RememberMe);

            TempData["SuccessMessage"] = result.Message;

            return RedirectByRole(result.Data.Role, returnUrl);
        }

        // ========================= LOGOUT =========================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            HttpContext.Session.Clear();

            Response.Cookies.Delete("Cart");

            TempData["SuccessMessage"] = "Đăng xuất thành công";

            return RedirectToAction("Index", "Home");
        }

        // ========================= CHECK LOCK STATUS (AJAX) =========================
        [HttpGet]
        public async Task<IActionResult> CheckLockStatus(string email)
        {
            var lockInfo = await _accountService.CheckLockAsync(email);

            return Json(new
            {
                isLocked = lockInfo.IsLocked,
                remainingMinutes = lockInfo.RemainingMinutes
            });
        }

        // ========================= ACCESS DENIED =========================
        public IActionResult AccessDenied()
        {
            return View();
        }


        // ========================= PRIVATE HELPERS =========================
        private async Task SignInAsync(LoginResponseDto data, bool rememberMe)
        {
            var identity = new ClaimsIdentity(data.Claims, CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
                new AuthenticationProperties
                {
                    IsPersistent = rememberMe,
                    ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(7) : null,
                    AllowRefresh = true
                });
        }

        private IActionResult RedirectByRole(string role, string? returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            var adminRoles = new[]
            {
                RoleConstants.BusinessOwner,
                RoleConstants.AreaManager,
                RoleConstants.StoreManager,
                RoleConstants.AccountantWarehouse,
                RoleConstants.SystemAdmin
            };

            var staffHubRoles = new[]
            {
                RoleConstants.SalesStaff,
                RoleConstants.ShiftSupervisor
            };

            if (adminRoles.Contains(role))
            {
                return RedirectToAction(
                    "Index",
                    "AdminStaff",
                    new { area = "Admin" });
            }

            if (staffHubRoles.Contains(role))
            {
                return RedirectToAction(
                    "Index",
                    "StaffHub");
            }

            if (role == RoleConstants.Customer)
            {
                return RedirectToAction(
                    "Index",
                    "Home");
            }

            return RedirectToAction(
                "Index",
                "Home");
        }


    }
}
