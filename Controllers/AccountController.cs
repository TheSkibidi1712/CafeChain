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
        public async Task<IActionResult> Login(string? email = null)
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }

            if (!string.IsNullOrEmpty(email))
            {
                var lockInfo = await _accountService.CheckLockAsync(email);

                if (lockInfo.IsLocked)
                {
                    ViewBag.IsLocked = true;
                    ViewBag.LockMinutes = lockInfo.RemainingMinutes;
                    ViewBag.LockMessage = $"Tài khoản bị khóa. Thử lại sau {lockInfo.RemainingMinutes} phút";
                }
                else
                {
                    ViewBag.IsLocked = false;
                }
            }

            return View(new LoginViewModel
            {
                Email = email
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home"); // hoặc dashboard
            }

            if (!ModelState.IsValid)
                return View(model);

            var dto = new LoginDto
            {
                Email = model.Email,
                Password = model.Password,
                RememberMe = model.RememberMe
            };

            var result = await _accountService.LoginAsync(dto);

            if (!result.IsSuccess)
            {
                await Task.Delay(800); // 🔥 chống brute force

                if (result.Data?.IsLocked == true)
                {
                    return RedirectToAction("Login", new { email = model.Email });
                }
                else
                {
                    ModelState.AddModelError(string.Empty, result.Message);
                }

                return View(model);
            }
            // ===== CLAIMS (Managed by Service) =====
            var claims = result.Data.Claims;

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = model.RememberMe
                    ? DateTimeOffset.UtcNow.AddDays(7)
                    : null,
                AllowRefresh = true
            });

            TempData["SuccessMessage"] = "Đăng nhập thành công!";

            // ===== REDIRECT ROLE =====
            var role = (result.Data.Role ?? "").Trim();

            // Ưu tiên returnUrl
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            // ===== ADMIN =====
            if (
                role == "Super Admin" ||
                role == "CEO / Ban Giám đốc" ||
                role == "Kế toán trưởng / Tài chính" ||
                role == "Giám đốc Marketing" ||
                role == "Giám đốc Vận hành" ||
                role == "Quản lý Nhân sự" ||
                role == "Quản lý Khu vực" ||
                role == "Cửa hàng trưởng"
            )
            {
                return RedirectToAction("Index", "AdminStaff", new { area = "Admin" });
            }

            // ===== KIOSK =====
            if (
                role == "Ca trưởng" ||
                role == "Thu ngân" ||
                role == "Thủ kho" ||
                role == "Nhân viên chung"
            )
            {
                return RedirectToAction("Index", "Kiosk");
            }

            // ===== CUSTOMER =====
            if (role == "Khách hàng")
            {
                return RedirectToAction("Index", "Home");
            }

            // fallback
            return RedirectToAction("Index", "Home");
        }

        // ========================= LOGOUT =========================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // Dọn dẹp Session/Cookie của Giỏ hàng
            HttpContext.Session.Clear(); 
            if (Request.Cookies["Cart"] != null) 
            {
                Response.Cookies.Delete("Cart");
            }

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


    }
}
