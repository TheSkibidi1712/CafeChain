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
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
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
            return View(new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
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

            // ===== CLAIMS =====
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, result.Data.AccountId.ToString()),
                new Claim(ClaimTypes.Name, result.Data.FullName),
                new Claim(ClaimTypes.Email, result.Data.Email),
            };

            // 🔥 Thêm TẤT CẢ roles vào Claims (không chỉ 1 role)
            foreach (var roleName in result.Data.AllRoles)
            {
                claims.Add(new Claim(ClaimTypes.Role, roleName));
            }
            // Nếu không có role nào, gán mặc định Customer
            if (!result.Data.AllRoles.Any())
            {
                claims.Add(new Claim(ClaimTypes.Role, "Customer"));
            }

            if (result.Data.CustomerId.HasValue)
                claims.Add(new Claim("CustomerId", result.Data.CustomerId.ToString()));

            if (result.Data.StaffId.HasValue)
                claims.Add(new Claim("StaffId", result.Data.StaffId.ToString()));

            // 🔥 BẮT BUỘC cho Decentralized RBAC: StoreId Claim
            if (result.Data.StoreId.HasValue)
                claims.Add(new Claim("StoreId", result.Data.StoreId.Value.ToString()));

            // Avatar Claim (hỗ trợ cả Customer & Staff)
            var avatarUrl = result.Data.AvatarUrl ?? "/Images/Upload/avtdf.jpg";
            claims.Add(new Claim("AvatarUrl", avatarUrl));

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
