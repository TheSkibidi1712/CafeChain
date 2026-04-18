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
                return RedirectToAction("Index", "Home"); // hoặc dashboard
            }

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
        public IActionResult Login()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home"); // hoặc dashboard
            }

            return View(new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
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
                ModelState.AddModelError(string.Empty, result.Message);
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

            // ===== REDIRECT dựa trên Role ưu tiên cao nhất =====
            var role = result.Data.Role ?? "";

            if (role.Contains("Admin") || role.Contains("Manager"))
            {
                return RedirectToAction("Index", "AdminStaff", new { area = "Admin" });
            }
            else if (role.Contains("Cashier"))
            {
                return RedirectToAction("Index", "Pos", new { area = "Cashier" });
            }
            else
            {
                return RedirectToAction("Index", "Home");
            }
        }

        // ========================= LOGOUT =========================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        // ========================= ACCESS DENIED =========================
        public IActionResult AccessDenied()
        {
            return View();
        }


    }
}
