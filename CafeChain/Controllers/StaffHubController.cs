using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces.StaffHub;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace CafeChain.Controllers;

[Authorize(Policy = AuthorizationPolicyConstants.StaffHubApp)]
public sealed class StaffHubController : Controller
{
    private readonly IStaffScheduleService _scheduleService;
    private readonly IConfiguration _configuration;
    private readonly IAuthorizationService _authorizationService;

    public StaffHubController(
        IStaffScheduleService scheduleService,
        IConfiguration configuration,
        IAuthorizationService authorizationService)
    {
        _scheduleService = scheduleService;
        _configuration = configuration;
        _authorizationService = authorizationService;
    }

    public async Task<IActionResult> Index(DateTime? date, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue("StaffId"), out var staffId) || staffId <= 0)
            return RedirectToAction("Login", "Account");

        var result = await _scheduleService.GetAsync(staffId, date ?? DateTime.Today, ct);
        if (!result.IsSuccess || result.Data == null)
        {
            TempData["ErrorMessage"] = result.Message;
            return RedirectToAction("Login", "Account");
        }

        ViewBag.CanAccessPos = (await _authorizationService.AuthorizeAsync(User, AuthorizationPolicyConstants.PosApp)).Succeeded;
        return View(result.Data);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = AuthorizationPolicyConstants.PosApp)]
    public IActionResult IssuePosToken()
    {
        var staffId = User.FindFirstValue("StaffId");
        var storeId = User.FindFirstValue("StoreId");
        if (string.IsNullOrWhiteSpace(staffId) || string.IsNullOrWhiteSpace(storeId))
            return Unauthorized(new { success = false, message = "Thiếu thông tin StaffId/StoreId trong phiên đăng nhập." });

        var jwtKey = _configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(jwtKey))
            throw new InvalidOperationException(
                "Jwt:Key is required. Configure it with User Secrets or a deployment secret.");
        var jwtIssuer = _configuration["Jwt:Issuer"] ?? "CafeChain";
        var jwtAudience = _configuration["Jwt:Audience"] ?? "CafeChain.POS";
        var expirationHours = double.TryParse(_configuration["Jwt:ExpirationHours"], out var hours) ? hours : 12;
        var expiresAt = DateTime.UtcNow.AddHours(expirationHours);
        var claims = User.Claims.Where(c => c.Type == ClaimTypes.NameIdentifier || c.Type == ClaimTypes.Name
            || c.Type == ClaimTypes.Email || c.Type == ClaimTypes.Role || c.Type == "StaffId"
            || c.Type == "StoreId" || c.Type == "AvatarUrl").ToList();
        var token = new JwtSecurityToken(jwtIssuer, jwtAudience, claims, expires: expiresAt,
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)), SecurityAlgorithms.HmacSha256));
        var posUrl = _configuration["AppLauncher:Pos:PosUrl"] ?? _configuration["PosFrontend:Url"] ?? "http://127.0.0.1:5173/order";
        return Ok(new { success = true, token = new JwtSecurityTokenHandler().WriteToken(token), expiresAt, posUrl });
    }
}
