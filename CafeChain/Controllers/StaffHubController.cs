using System.Security.Claims;
using CafeChain.Application.Constants;
using CafeChain.Application.Authorization;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Interfaces.StaffHub;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.DTOs.StaffHub;
using CafeChain.Models.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;

namespace CafeChain.Controllers;

[Authorize(Policy = AuthorizationPolicyConstants.StaffHubApp)]
public sealed class StaffHubController : Controller
{
    private readonly IStaffScheduleService _scheduleService;
    private readonly IConfiguration _configuration;
    private readonly IAuthorizationService _authorizationService;
    private readonly IPosSessionExchangeService _posSessionExchangeService;
    private readonly IWorkShiftService _workShiftService;
    private readonly IOtpApprovalService _otpApprovalService;
    private readonly IWorkShiftOpenApprovalService? _lateOpenApprovals;

    public StaffHubController(
        IStaffScheduleService scheduleService,
        IConfiguration configuration,
        IAuthorizationService authorizationService,
        IPosSessionExchangeService posSessionExchangeService,
        IWorkShiftService workShiftService,
        IOtpApprovalService otpApprovalService,
        IWorkShiftOpenApprovalService? lateOpenApprovals = null)
    {
        _scheduleService = scheduleService;
        _configuration = configuration;
        _authorizationService = authorizationService;
        _posSessionExchangeService = posSessionExchangeService;
        _workShiftService = workShiftService;
        _otpApprovalService = otpApprovalService;
        _lateOpenApprovals = lateOpenApprovals;
    }

    public async Task<IActionResult> Index(
        DateTime? date,
        bool openPos = false,
        string? terminalId = null,
        string? posErrorCode = null,
        CancellationToken ct = default)
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
        var identityStoreId = int.TryParse(User.FindFirstValue("StoreId"), out var parsedStoreId)
            ? parsedStoreId : 0;
        ViewBag.PosTerminals = await _workShiftService.GetAvailableTerminalsAsync(identityStoreId, ct);
        ViewBag.AutoOpenPos = openPos;
        ViewBag.RequestedTerminalId = terminalId?.Trim();
        ViewBag.PosLaunchError = posErrorCode switch
        {
            PosSessionExchangeErrorCodes.Expired =>
                "Mã mở POS đã hết hạn. Vui lòng thử mở POS lại.",
            PosSessionExchangeErrorCodes.AlreadyUsed =>
                "Mã mở POS đã được sử dụng. Vui lòng thử mở POS lại.",
            PosSessionExchangeErrorCodes.ContextInvalid =>
                "Ngữ cảnh mở POS không hợp lệ. Vui lòng chọn lại terminal và thử lại.",
            PosSessionExchangeErrorCodes.Invalid =>
                "Mã mở POS không hợp lệ. Vui lòng thử mở POS lại.",
            "POS_EXCHANGE_UNAVAILABLE" =>
                "Không thể kết nối máy chủ để mở POS. Vui lòng kiểm tra kết nối và thử lại.",
            _ => string.Empty
        };
        return View(result.Data);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = AuthorizationPolicyConstants.PosApp)]
    [RequirePermission(PermissionConstants.PosOperatorSwitch)]
    public async Task<IActionResult> SetOperatorPin([FromForm] SetOperatorPinRequestDto request)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var accountId)
            || !int.TryParse(User.FindFirstValue("StaffId"), out var staffId)
            || !int.TryParse(User.FindFirstValue("StoreId"), out var storeId))
            return Unauthorized(new { success = false, message = "Phiên đăng nhập không hợp lệ." });

        var result = await _workShiftService.SetOperatorPinAsync(accountId, staffId, storeId, request);
        return result.IsSuccess
            ? Ok(new { success = true, message = result.Message, pinConfigured = true })
            : StatusCode(result.ErrorCode == WorkShiftErrorCodes.OperatorNotAuthorized
                ? StatusCodes.Status403Forbidden : StatusCodes.Status400BadRequest,
                new { success = false, errorCode = result.ErrorCode, message = result.Message });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuthorizationPolicyConstants.PosApp)]
    [RequirePermission(PermissionConstants.PosWorkShiftOpen)]
    public async Task<IActionResult> PreviewOpenPos(
        [FromForm] StaffHubPosPreviewRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue("StaffId"), out var staffId)
            || !int.TryParse(User.FindFirstValue("StoreId"), out var storeId))
            return Unauthorized(new { success = false, message = "Thiếu thông tin nhân viên hoặc cửa hàng trong phiên đăng nhập." });

        var result = await _workShiftService.AssessOpenContextAsync(
            staffId,
            storeId,
            request.TerminalId,
            cancellationToken);
        if (!result.IsSuccess || result.Data == null)
        {
            var statusCode = string.Equals(
                result.ErrorCode,
                WorkShiftErrorCodes.PosPermissionRequired,
                StringComparison.Ordinal)
                ? StatusCodes.Status403Forbidden
                : StatusCodes.Status400BadRequest;
            return StatusCode(result.ErrorCode is WorkShiftErrorCodes.StaffAlreadyHasOpenShift
                or WorkShiftErrorCodes.TerminalAlreadyHasOpenShift
                or WorkShiftErrorCodes.WorkShiftPendingClose
                    ? StatusCodes.Status409Conflict : statusCode, new
            {
                success = false,
                errorCode = result.ErrorCode,
                message = result.Message,
                data = result.Data
            });
        }

        return Ok(new { success = true, data = result.Data });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = AuthorizationPolicyConstants.PosApp)]
    [RequirePermission(PermissionConstants.PosWorkShiftOpen)]
    public async Task<IActionResult> IssuePosToken(
        [FromForm] StaffHubIssuePosRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var accountId)
            || !int.TryParse(User.FindFirstValue("StaffId"), out var staffId)
            || !int.TryParse(User.FindFirstValue("StoreId"), out var storeId))
            return Unauthorized(new { success = false, message = "Thiếu thông tin StaffId/StoreId trong phiên đăng nhập." });

        var prepared = await _workShiftService.PrepareOpenExchangeContextAsync(
            accountId, staffId, storeId, request.TerminalId, request.RequestKey,
            request.Reason, request.OtpChallengePublicId, cancellationToken,
            request.LateOpenApprovalPublicId);
        if (!prepared.IsSuccess || prepared.Data == null)
            return BadRequest(new { success = false, errorCode = prepared.ErrorCode, message = prepared.Message });

        // The exchange is only an expiring authorization context. WorkShift is
        // committed later by /api/v1/pos/shifts/open with the confirmed cash.
        var ticket = await _posSessionExchangeService.IssueAsync(prepared.Data, cancellationToken);
        var baseUrl = _configuration["AppLauncher:Pos:PosUrl"] ?? _configuration["PosFrontend:Url"] ?? "http://127.0.0.1:5173/shift";
        var posUrl = new UriBuilder(new Uri(baseUrl)) { Path = "/shift" }.Uri.ToString();
        return Ok(new
        {
            success = true,
            resultCode = WorkShiftOpenResultCodes.OpenedNewWorkShift,
            recommendedAction = WorkShiftRecommendedActions.EnterOpeningCash,
            workShiftId = (int?)null,
            requiresOpeningCash = true,
            exchangeCode = ticket.ExchangeCode,
            expiresAtUtc = ticket.ExpiresAtUtc,
            exchangeUrl = Url.Content("~/api/v1/pos/session/exchange"),
            posUrl
        });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = AuthorizationPolicyConstants.PosApp)]
    [RequirePermission(PermissionConstants.PosWorkShiftOpen)]
    public async Task<IActionResult> IssueResumePosToken(
        [FromForm] StaffHubResumePosRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var accountId, out var staffId, out var storeId)) return Unauthorized();
        var prepared = await _workShiftService.PrepareResumeExchangeContextAsync(
            accountId, staffId, storeId, request.TerminalId, cancellationToken);
        if (!prepared.IsSuccess || prepared.Data == null)
            return BadRequest(new { success = false, errorCode = prepared.ErrorCode, message = prepared.Message });
        var ticket = await _posSessionExchangeService.IssueAsync(prepared.Data, cancellationToken);
        var baseUrl = _configuration["AppLauncher:Pos:PosUrl"]
            ?? _configuration["PosFrontend:Url"] ?? "http://127.0.0.1:5173/order";
        var uri = new Uri(baseUrl);
        var route = prepared.Data.OpenContext == WorkShiftStatuses.Open ? "/order" : "/shift";
        var posUrl = new UriBuilder(uri) { Path = route }.Uri.ToString();
        return Ok(new
        {
            success = true,
            resultCode = WorkShiftOpenResultCodes.ResumeExistingWorkShift,
            recommendedAction = prepared.Data.OpenContext == WorkShiftStatuses.Open
                ? WorkShiftRecommendedActions.ContinuePos
                : prepared.Data.OpenContext == WorkShiftStatuses.Closing
                    ? WorkShiftRecommendedActions.CompleteClosing
                    : WorkShiftRecommendedActions.CountAndClose,
            workShiftId = prepared.Data.WorkShiftId,
            requiresOpeningCash = false,
            exchangeCode = ticket.ExchangeCode,
            expiresAtUtc = ticket.ExpiresAtUtc,
            exchangeUrl = Url.Content("~/api/v1/pos/session/exchange"),
            posUrl
        });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = AuthorizationPolicyConstants.PosApp)]
    [RequirePermission(PermissionConstants.PosWorkShiftOpen)]
    public async Task<IActionResult> GetOpenPosOtpState()
    {
        if (!TryGetIdentity(out _, out var staffId, out var storeId)) return Unauthorized();
        return OtpResult(await _otpApprovalService.GetCurrentOpenPosOtpStateAsync(staffId, storeId));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = AuthorizationPolicyConstants.PosApp)]
    [RequirePermission(PermissionConstants.PosWorkShiftOpen)]
    public async Task<IActionResult> RequestOpenPosOtp(
        [FromForm] StaffHubOpenOtpRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out _, out var staffId, out var storeId)) return Unauthorized();
        var preview = await _workShiftService.AssessOpenContextAsync(
            staffId, storeId, request.TerminalId, cancellationToken);
        if (!preview.IsSuccess || preview.Data == null)
            return BadRequest(new { success = false, errorCode = preview.ErrorCode, message = preview.Message });
        if (!preview.Data.ApprovalRequired)
            return BadRequest(new { success = false, message = "Ngữ cảnh hiện tại không yêu cầu OTP." });
        var action = preview.Data.OpenContext == WorkShiftOpenContexts.OutsideSchedule
            ? OtpConstants.ActionTypes.OpenShiftOutsideSchedule
            : OtpConstants.ActionTypes.OpenShiftLate;
        var otpRequest = new OtpRequestDto
        {
            ActionType = action,
            TargetType = OtpConstants.TargetTypes.Shifts,
            Reason = request.Reason,
            StartingCash = 0,
            TerminalId = request.TerminalId,
            RequestKey = request.RequestKey
        };
        ApplySecurityMetadata(otpRequest, request.TerminalId, staffId);
        var result = await _otpApprovalService.RequestOtpAsync(otpRequest, staffId, storeId);
        return OtpResult(result);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = AuthorizationPolicyConstants.PosApp)]
    [RequirePermission(PermissionConstants.PosWorkShiftOpen)]
    public async Task<IActionResult> RequestTerminalRegistrationOtp(
        [FromForm] StaffHubTerminalOtpRequestDto request)
    {
        if (!TryGetIdentity(out _, out var staffId, out var storeId)) return Unauthorized();
        var otpRequest = new OtpRequestDto
        {
            ActionType = OtpConstants.ActionTypes.RegisterTerminal,
            TargetType = OtpConstants.TargetTypes.Shifts,
            Reason = request.TerminalName,
            TerminalId = request.TerminalId,
            TerminalName = request.TerminalName,
            RequestKey = request.RequestKey
        };
        ApplySecurityMetadata(otpRequest, request.TerminalId, staffId);
        var result = await _otpApprovalService.RequestOtpAsync(otpRequest, staffId, storeId);
        return OtpResult(result);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = AuthorizationPolicyConstants.PosApp)]
    [RequirePermission(PermissionConstants.PosWorkShiftOpen)]
    public async Task<IActionResult> GetTerminalRegistrationOtpState()
    {
        if (!TryGetIdentity(out _, out var staffId, out var storeId)) return Unauthorized();
        return OtpResult(await _otpApprovalService.GetCurrentTerminalRegistrationOtpStateAsync(staffId, storeId));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = AuthorizationPolicyConstants.PosApp)]
    [RequirePermission(PermissionConstants.PosWorkShiftOpen)]
    public async Task<IActionResult> CancelOpenPosIntent(
        [FromForm] StaffHubCancelOpenPosIntentRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out _, out var staffId, out var storeId)) return Unauthorized();
        var terminalId = request.TerminalId?.Trim() ?? string.Empty;
        var requestKey = request.RequestKey?.Trim() ?? string.Empty;
        if (terminalId.Length == 0 || requestKey.Length == 0)
            return BadRequest(new { success = false, message = "Thiếu Terminal hoặc mã yêu cầu mở ca." });

        var active = await _workShiftService.GetActiveShiftAsync(staffId, storeId);
        if (active != null && string.Equals(active.PosTerminalId, terminalId, StringComparison.Ordinal))
            return Conflict(new
            {
                success = false,
                errorCode = WorkShiftErrorCodes.ConcurrencyConflict,
                message = "WorkShift đã được tạo; không thể hủy intent mở ca."
            });

        if (request.OtpChallengePublicId.HasValue)
        {
            var otp = await _otpApprovalService.CancelOpenPosOtpAsync(new OtpCancelDto
            {
                OtpChallengePublicId = request.OtpChallengePublicId.Value
            }, staffId, storeId);
            if (!otp.IsSuccess) return OtpResult(otp);
        }

        if (request.LateOpenApprovalPublicId.HasValue)
        {
            if (_lateOpenApprovals == null) return StatusCode(StatusCodes.Status503ServiceUnavailable);
            var approval = await _lateOpenApprovals.CancelAsync(
                staffId, storeId, request.LateOpenApprovalPublicId.Value,
                terminalId, requestKey, cancellationToken);
            if (!approval.IsSuccess)
                return Conflict(new
                {
                    success = false,
                    errorCode = approval.ErrorCode,
                    message = approval.Message
                });
        }

        return Ok(new
        {
            success = true,
            status = "CANCELLED",
            message = "Đã hủy yêu cầu mở ca. Không có WorkShift nào được tạo."
        });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = AuthorizationPolicyConstants.PosApp)]
    [RequirePermission(PermissionConstants.PosWorkShiftOpen)]
    public async Task<IActionResult> CancelTerminalRegistrationOtp([FromForm] OtpCancelDto request)
    {
        if (!TryGetIdentity(out _, out var staffId, out var storeId)) return Unauthorized();
        return OtpResult(await _otpApprovalService.CancelTerminalRegistrationOtpAsync(
            request, staffId, storeId));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = AuthorizationPolicyConstants.PosApp)]
    [RequirePermission(PermissionConstants.PosWorkShiftOpen)]
    public async Task<IActionResult> RequestLateOpenApproval(
        [FromForm] CreateWorkShiftOpenApprovalRequestDto request,
        CancellationToken cancellationToken)
    {
        if (_lateOpenApprovals == null) return StatusCode(503);
        if (!TryGetIdentity(out _, out var staffId, out var storeId)) return Unauthorized();
        var result = await _lateOpenApprovals.CreateAsync(staffId, storeId, request, cancellationToken);
        return result.IsSuccess
            ? Ok(new { success = true, message = result.Message, data = result.Data })
            : BadRequest(new { success = false, errorCode = result.ErrorCode, message = result.Message, data = result.Data });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = AuthorizationPolicyConstants.PosApp)]
    [RequirePermission(PermissionConstants.PosWorkShiftOpen)]
    public async Task<IActionResult> GetLateOpenApproval(
        [FromForm] Guid id,
        CancellationToken cancellationToken)
    {
        if (_lateOpenApprovals == null) return StatusCode(503);
        if (!TryGetIdentity(out _, out var staffId, out _)) return Unauthorized();
        var result = await _lateOpenApprovals.GetAsync(staffId, id, cancellationToken);
        return result.IsSuccess
            ? Ok(new { success = true, message = result.Message, data = result.Data })
            : BadRequest(new { success = false, errorCode = result.ErrorCode, message = result.Message });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = AuthorizationPolicyConstants.PosApp)]
    [RequirePermission(PermissionConstants.PosWorkShiftOpen)]
    public async Task<IActionResult> VerifyOperationalOtp([FromForm] OtpVerifyDto request)
    {
        if (!TryGetIdentity(out _, out var staffId, out var storeId)) return Unauthorized();
        ApplySecurityMetadata(request, staffId);
        return OtpResult(await _otpApprovalService.VerifyOtpAsync(request, staffId, storeId));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = AuthorizationPolicyConstants.PosApp)]
    [RequirePermission(PermissionConstants.PosWorkShiftOpen)]
    public async Task<IActionResult> ResendOperationalOtp([FromForm] OtpResendDto request)
    {
        if (!TryGetIdentity(out _, out var staffId, out var storeId)) return Unauthorized();
        ApplySecurityMetadata(request, staffId);
        return OtpResult(await _otpApprovalService.ResendOtpAsync(request, staffId, storeId));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = AuthorizationPolicyConstants.PosApp)]
    [RequirePermission(PermissionConstants.PosWorkShiftOpen)]
    public async Task<IActionResult> RegisterTerminal(
        [FromForm] StaffHubTerminalRegistrationRequestDto request)
    {
        if (!TryGetIdentity(out _, out _, out _)) return Unauthorized();
        return Conflict(new
        {
            success = false,
            errorCode = "TERMINAL_CONFIRMATION_MANAGER_REQUIRED",
            message = "Terminal chỉ được hoàn tất bởi Manager từ Notification đang chờ."
        });
    }

    private IActionResult OtpResult<T>(CafeChain.Application.Results.ServiceResult<T> result)
    {
        var payload = new
        {
            success = result.IsSuccess,
            message = result.Message,
            errorCode = result.ErrorCode,
            data = result.Data
        };
        if (result.IsSuccess) return Ok(payload);
        return result.ErrorCode switch
        {
            OtpConstants.ErrorCodes.Expired => StatusCode(StatusCodes.Status410Gone, payload),
            OtpConstants.ErrorCodes.AlreadyUsed or OtpConstants.ErrorCodes.ContextMismatch => Conflict(payload),
            OtpConstants.ErrorCodes.VerificationLocked => StatusCode(StatusCodes.Status423Locked, payload),
            OtpConstants.ErrorCodes.RateLimited => StatusCode(StatusCodes.Status429TooManyRequests, payload),
            _ => BadRequest(payload)
        };
    }

    private void ApplySecurityMetadata(OtpRequestDto request, string? terminalId, int staffId)
    {
        var metadata = BuildSecurityMetadata(terminalId, staffId);
        request.ClientIpHash = metadata.clientIpHash;
        request.DeviceFingerprintHash = metadata.deviceFingerprintHash;
    }

    private void ApplySecurityMetadata(OtpVerifyDto request, int staffId)
    {
        var metadata = BuildSecurityMetadata(null, staffId);
        request.ClientIpHash = metadata.clientIpHash;
        request.DeviceFingerprintHash = metadata.deviceFingerprintHash;
    }

    private void ApplySecurityMetadata(OtpResendDto request, int staffId)
    {
        var metadata = BuildSecurityMetadata(null, staffId);
        request.ClientIpHash = metadata.clientIpHash;
        request.DeviceFingerprintHash = metadata.deviceFingerprintHash;
    }

    private (string? clientIpHash, string? deviceFingerprintHash) BuildSecurityMetadata(
        string? terminalId,
        int staffId)
    {
        var address = HttpContext.Connection.RemoteIpAddress;
        if (address?.IsIPv4MappedToIPv6 == true) address = address.MapToIPv4();
        var deviceSource = Request.Headers["X-Device-Id"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(deviceSource)) deviceSource = terminalId;
        if (string.IsNullOrWhiteSpace(deviceSource))
            deviceSource = $"staff:{staffId}:{Request.Headers.UserAgent}";
        return (HashMetadata(address?.ToString()), HashMetadata(deviceSource));
    }

    private static string? HashMetadata(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > 512) normalized = normalized[..512];
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }

    private bool TryGetIdentity(out int accountId, out int staffId, out int storeId)
    {
        accountId = 0;
        staffId = 0;
        storeId = 0;
        return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out accountId)
            && int.TryParse(User.FindFirstValue("StaffId"), out staffId)
            && int.TryParse(User.FindFirstValue("StoreId"), out storeId);
    }
}
