using System.Security.Claims;
using CafeChain.Application.Constants;
using CafeChain.Application.Authorization;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Interfaces.StaffHub;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Controllers;

[Authorize(Policy = AuthorizationPolicyConstants.StaffHubApp)]
public sealed class StaffHubController : Controller
{
    private readonly IStaffScheduleService _scheduleService;
    private readonly IConfiguration _configuration;
    private readonly IAuthorizationService _authorizationService;
    private readonly IPosSessionExchangeService _posSessionExchangeService;
    private readonly IWorkShiftService _workShiftService;

    public StaffHubController(
        IStaffScheduleService scheduleService,
        IConfiguration configuration,
        IAuthorizationService authorizationService,
        IPosSessionExchangeService posSessionExchangeService,
        IWorkShiftService workShiftService)
    {
        _scheduleService = scheduleService;
        _configuration = configuration;
        _authorizationService = authorizationService;
        _posSessionExchangeService = posSessionExchangeService;
        _workShiftService = workShiftService;
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

        var canAccessPosApp = (await _authorizationService.AuthorizeAsync(
            User, AuthorizationPolicyConstants.PosApp)).Succeeded;
        var canOpenWorkShift = (await _authorizationService.AuthorizeAsync(
            User,
            RequirePermissionAttribute.PolicyPrefix + PermissionConstants.PosWorkShiftOpen)).Succeeded;
        ViewBag.CanAccessPos = canAccessPosApp && canOpenWorkShift;
        ViewBag.CanAccessDashboard = (await _authorizationService.AuthorizeAsync(
            User,
            AuthorizationPolicyConstants.AdminDashboardApp)).Succeeded;
        return View(result.Data);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuthorizationPolicyConstants.PosApp)]
    [RequirePermission(PermissionConstants.PosWorkShiftOpen)]
    public async Task<IActionResult> PreviewOpenPos(CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue("StaffId"), out var staffId)
            || !int.TryParse(User.FindFirstValue("StoreId"), out var storeId))
            return Unauthorized(new { success = false, message = "Thiếu thông tin nhân viên hoặc cửa hàng trong phiên đăng nhập." });

        var result = await _workShiftService.AssessOpenContextAsync(
            staffId,
            storeId,
            cancellationToken);
        if (!result.IsSuccess || result.Data == null)
        {
            var statusCode = string.Equals(
                result.ErrorCode,
                WorkShiftErrorCodes.PosPermissionRequired,
                StringComparison.Ordinal)
                ? StatusCodes.Status403Forbidden
                : StatusCodes.Status400BadRequest;
            return StatusCode(statusCode, new
            {
                success = false,
                errorCode = result.ErrorCode,
                message = result.Message
            });
        }

        return Ok(new { success = true, data = result.Data });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = AuthorizationPolicyConstants.PosApp)]
    [RequirePermission(PermissionConstants.PosWorkShiftOpen)]
    public async Task<IActionResult> IssuePosToken(CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var accountId)
            || !int.TryParse(User.FindFirstValue("StaffId"), out var staffId)
            || !int.TryParse(User.FindFirstValue("StoreId"), out var storeId))
            return Unauthorized(new { success = false, message = "Thiếu thông tin StaffId/StoreId trong phiên đăng nhập." });

        var ticket = await _posSessionExchangeService.IssueAsync(
            accountId, staffId, storeId, cancellationToken);
        var posUrl = _configuration["AppLauncher:Pos:PosUrl"] ?? _configuration["PosFrontend:Url"] ?? "http://127.0.0.1:5173/order";
        return Ok(new
        {
            success = true,
            exchangeCode = ticket.ExchangeCode,
            expiresAtUtc = ticket.ExpiresAtUtc,
            exchangeUrl = Url.Content("~/api/v1/pos/session/exchange"),
            posUrl
        });
    }
}
