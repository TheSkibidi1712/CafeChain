using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CafeChain.Application.Interfaces.Attendance;
using CafeChain.Application.Constants;

namespace CafeChain.Controllers
{
    [Authorize(Policy = AuthorizationPolicyConstants.StaffHubApp)]
    public class StaffHubController : Controller
    {
        private readonly IAttendanceActionService _actionService;
        private readonly IAttendanceSecurityService _securityService;
        private readonly IConfiguration _configuration;
        private readonly IAuthorizationService _authorizationService;

        public StaffHubController(
            IAttendanceActionService actionService,
            IAttendanceSecurityService securityService,
            IConfiguration configuration,
            IAuthorizationService authorizationService)
        {
            _actionService = actionService;
            _securityService = securityService;
            _configuration = configuration;
            _authorizationService = authorizationService;
        }

        // GET: /StaffHub/Index
        public async Task<IActionResult> Index()
        {
            // Anti-IDOR: Extract accountId from authenticated Claims only
            var accountIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(accountIdStr) || !int.TryParse(accountIdStr, out int accountId))
            {
                return RedirectToAction("Login", "Account");
            }

            // Get user role for POS access check (SalesStaff + Ca trưởng)
            var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            bool canAccessPos = (await _authorizationService.AuthorizeAsync(
                User,
                AuthorizationPolicyConstants.PosApp)).Succeeded;

            // Load dashboard data via service layer (N-Tier compliant)
            var result = await _actionService.GetKioskDataAsync(accountId);
            if (!result.IsSuccess)
            {
                TempData["ErrorMessage"] = result.Message;
                return RedirectToAction("Login", "Account");
            }

            ViewBag.StaffHubData = result.Data;
            ViewBag.CanAccessPos = canAccessPos;
            ViewBag.UserRole = role;
            ViewBag.AccountId = accountId;

            return View();
        }

        // POST: /StaffHub/IssuePosToken
        [HttpPost]
        [Authorize(Policy = AuthorizationPolicyConstants.PosApp)]
        public IActionResult IssuePosToken()
        {
            var staffId = User.FindFirst("StaffId")?.Value;
            var storeId = User.FindFirst("StoreId")?.Value;
            if (string.IsNullOrWhiteSpace(staffId) || string.IsNullOrWhiteSpace(storeId))
            {
                return Unauthorized(new { success = false, message = "Thiếu thông tin StaffId/StoreId trong phiên đăng nhập." });
            }

            var jwtKey = _configuration["Jwt:Key"]
                ?? "CafeChain-POS-JWT-Secret-Key-Change-In-Production-2026-Min32Chars!";
            var jwtIssuer = _configuration["Jwt:Issuer"] ?? "CafeChain";
            var jwtAudience = _configuration["Jwt:Audience"] ?? "CafeChain.POS";
            var expirationHours = double.TryParse(_configuration["Jwt:ExpirationHours"], out var hours) ? hours : 12;
            var expiresAt = DateTime.UtcNow.AddHours(expirationHours);

            var claims = User.Claims
                .Where(c =>
                    c.Type == ClaimTypes.NameIdentifier ||
                    c.Type == ClaimTypes.Name ||
                    c.Type == ClaimTypes.Email ||
                    c.Type == ClaimTypes.Role ||
                    c.Type == "StaffId" ||
                    c.Type == "StoreId" ||
                    c.Type == "AvatarUrl")
                .ToList();

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var token = new JwtSecurityToken(
                issuer: jwtIssuer,
                audience: jwtAudience,
                claims: claims,
                expires: expiresAt,
                signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

            var posUrl = _configuration["AppLauncher:Pos:PosUrl"]
                ?? _configuration["PosFrontend:Url"]
                ?? "http://127.0.0.1:5173/order";

            return Ok(new
            {
                success = true,
                token = new JwtSecurityTokenHandler().WriteToken(token),
                expiresAt,
                posUrl
            });
        }
    }
}
