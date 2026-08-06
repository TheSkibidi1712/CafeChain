using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Constants;
using CafeChain.Application.Authorization;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Controllers.Api.v1;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Controllers.Api.v1
{
    /// <summary>
    /// POS WorkShift APIs — Open, Close, Current shift.
    /// Reuses existing IWorkShiftService business logic.
    /// StoreId/StaffId từ JWT Claims (PosApiController base).
    /// </summary>
    [Route("api/v1/pos/shifts")]
    public class POSShiftController : PosApiController
    {
        private readonly IWorkShiftService _shiftService;

        public POSShiftController(IWorkShiftService shiftService)
        {
            _shiftService = shiftService;
        }

        /// <summary>
        /// POST /api/v1/pos/shifts/open
        /// Mở ca két tiền mới. StoreId/StaffId lấy từ JWT.
        /// </summary>
        [HttpPost("open")]
        [RequirePermission(PermissionConstants.PosWorkShiftOpen)]
        public async Task<IActionResult> OpenShift([FromBody] OpenPosSessionRequestDto request)
        {
            if (CurrentExchangeContextId <= 0
                || !string.Equals(User.FindFirst("PosPurpose")?.Value,
                    PosSessionPurposes.OpenWorkShift, StringComparison.Ordinal))
                return WorkShiftError(
                    WorkShiftErrorCodes.PosOpenContextRequired,
                    "Vui lòng mở POS từ StaffHub để nhận ngữ cảnh mở phiên hợp lệ.");
            var command = new OpenShiftRequestDto
            {
                StartingCash = request.StartingCash,
                ExchangeContextId = CurrentExchangeContextId,
                AccountId = CurrentAccountId
            };
            var result = await _shiftService.OpenShiftAsync(CurrentStaffId, CurrentStoreId, command);

            if (!result.IsSuccess)
                return WorkShiftError(result.ErrorCode, result.Message);

            // Fetch the newly created shift to return summary
            var summary = await _shiftService.GetSummaryAsync(
                CurrentStaffId, CurrentStoreId, result.EntityId);

            return summary == null
                ? WorkShiftError(WorkShiftErrorCodes.WorkShiftNotOpen, "Không đọc được phiên POS vừa tạo.")
                : CreatedSummary(summary);
        }

        /// <summary>
        /// POST /api/v1/pos/shifts/{id}/close
        /// Đóng ca két tiền + đối soát tiền mặt.
        /// </summary>
        [HttpPost("{id}/close")]
        [RequirePermission(PermissionConstants.PosWorkShiftClose)]
        public async Task<IActionResult> CloseShift(int id, [FromBody] CloseShiftRequestDto request)
        {
            var result = await _shiftService.CloseShiftAsync(CurrentStaffId, CurrentStoreId, id, request);

            if (!result.IsSuccess)
            {
                // Include errorCode so POS UI can open OTP panel on OTP_REQUIRED (#90/#91).
                return WorkShiftError(result.ErrorCode, result.Message);
            }

            var summary = await _shiftService.GetSummaryAsync(CurrentStaffId, CurrentStoreId, id);
            if (summary == null)
                return Ok(new { success = true, message = result.Message });
            summary.ResultCode = WorkShiftOpenResultCodes.WorkShiftClosed;
            summary.RecommendedAction = null;
            return Ok(summary);
        }

        /// <summary>
        /// POST /api/v1/pos/shifts/{id}/close-exception
        /// Đóng ca ngoại lệ bằng OTP phê duyệt (online) khi còn Offline Order local chưa Sync.
        /// </summary>
        [HttpPost("{id}/start-closing")]
        [RequirePermission(PermissionConstants.PosWorkShiftClose)]
        public async Task<IActionResult> StartClosing(int id, [FromBody] StartClosingRequestDto request)
        {
            var result = await _shiftService.StartClosingAsync(CurrentStaffId, CurrentStoreId, id, request);
            return result.IsSuccess
                ? Ok(new
                {
                    success = true,
                    resultCode = WorkShiftOpenResultCodes.WorkShiftClosingStarted,
                    recommendedAction = WorkShiftRecommendedActions.CompleteClosing,
                    message = result.Message
                })
                : WorkShiftError(result.ErrorCode, result.Message);
        }

        [HttpPost("{id}/close-exception")]
        [RequirePermission(PermissionConstants.PosWorkShiftCloseException)]
        public async Task<IActionResult> CloseShiftByException(int id, [FromBody] CloseShiftExceptionRequestDto request)
        {
            var result = await _shiftService.CloseShiftByExceptionAsync(
                CurrentStaffId, CurrentStoreId, id, request);

            if (!result.IsSuccess)
            {
                return WorkShiftError(result.ErrorCode, result.Message);
            }

            var summary = await _shiftService.GetSummaryAsync(CurrentStaffId, CurrentStoreId, id);
            if (summary == null)
                return Ok(new { success = true, message = result.Message });
            summary.ResultCode = WorkShiftOpenResultCodes.WorkShiftReconciliationRequired;
            summary.RecommendedAction = null;
            return Ok(summary);
        }

        /// <summary>
        /// GET /api/v1/pos/shifts/current
        /// Trả ca đang mở. Nếu không có → { status: "NoActiveShift" }
        /// </summary>
        [HttpPost("{id}/reconcile")]
        [RequirePermission(PermissionConstants.PosWorkShiftReconcile)]
        public async Task<IActionResult> Reconcile(int id, [FromBody] ReconcileWorkShiftRequestDto request)
        {
            var result = await _shiftService.ReconcileAsync(CurrentStaffId, CurrentStoreId, id, request);
            return result.IsSuccess
                ? Ok(new
                {
                    success = true,
                    resultCode = WorkShiftOpenResultCodes.WorkShiftReconciled,
                    message = result.Message
                })
                : WorkShiftError(result.ErrorCode, result.Message);
        }

        [HttpGet("current")]
        [RequirePermission(PermissionConstants.PosWorkShiftView)]
        public async Task<IActionResult> GetCurrentShift()
        {
            var summary = await _shiftService.GetSummaryAsync(CurrentStaffId, CurrentStoreId);
            if (summary == null)
            {
                return Ok(new ShiftSummaryDto { Status = "NoActiveShift" });
            }
            return Ok(summary);
        }

        [HttpPost("operator/pin")]
        [RequirePermission(PermissionConstants.PosOperatorSwitch)]
        public async Task<IActionResult> SetOperatorPin([FromBody] SetOperatorPinRequestDto request)
        {
            var result = await _shiftService.SetOperatorPinAsync(
                CurrentAccountId, CurrentStaffId, CurrentStoreId, request);
            return result.IsSuccess
                ? Ok(new { success = true, message = result.Message })
                : WorkShiftError(result.ErrorCode, result.Message);
        }

        [HttpGet("operator/candidates")]
        [RequirePermission(PermissionConstants.PosOperatorSwitch)]
        public async Task<IActionResult> GetOperatorCandidates()
        {
            var result = await _shiftService.GetOperatorCandidatesAsync(CurrentStoreId);
            return result.IsSuccess
                ? Ok(result.Data)
                : WorkShiftError(result.ErrorCode, result.Message);
        }

        [HttpPost("{id}/operator/switch")]
        [RequirePermission(PermissionConstants.PosOperatorSwitch)]
        public async Task<IActionResult> SwitchOperator(int id, [FromBody] SwitchOperatorRequestDto request)
        {
            var result = await _shiftService.SwitchOperatorAsync(
                CurrentStaffId, CurrentStoreId, id, request);
            if (!result.IsSuccess)
                return WorkShiftError(result.ErrorCode, result.Message);

            var summary = await _shiftService.GetSummaryAsync(CurrentStaffId, CurrentStoreId, id);
            return summary == null
                ? WorkShiftError(WorkShiftErrorCodes.WorkShiftNotOpen, "Không đọc được phiên POS sau khi đổi người thao tác.")
                : Ok(summary);
        }

        private IActionResult WorkShiftError(string? errorCode, string? message)
        {
            var payload = new
            {
                success = false,
                errorCode,
                recommendedAction = errorCode switch
                {
                    WorkShiftErrorCodes.StaffHubOpenRequired
                        or WorkShiftErrorCodes.PosOpenContextRequired
                        or WorkShiftErrorCodes.PosOpenContextInvalid => WorkShiftRecommendedActions.OpenStaffHub,
                    WorkShiftErrorCodes.OpeningCashRequired => WorkShiftRecommendedActions.EnterOpeningCash,
                    WorkShiftErrorCodes.WorkShiftPendingClose => WorkShiftRecommendedActions.CountAndClose,
                    _ => null
                },
                staffHubUrl = errorCode is WorkShiftErrorCodes.StaffHubOpenRequired
                    or WorkShiftErrorCodes.PosOpenContextRequired
                    or WorkShiftErrorCodes.PosOpenContextInvalid
                    ? Url.Action("Index", "StaffHub", new { openPos = 1 })
                    : null,
                message = message ?? "Không thể xử lý yêu cầu.",
                correlationId = HttpContext.TraceIdentifier
            };
            return errorCode switch
            {
                WorkShiftErrorCodes.PosPermissionRequired
                    or WorkShiftErrorCodes.StoreScopeDenied
                    or WorkShiftErrorCodes.OutsideScheduleApprovalRequired
                    or WorkShiftErrorCodes.InvalidApproverScope
                    or OtpConstants.ErrorCodes.LateOpeningRequiresOtp
                    or OtpConstants.ErrorCodes.Required => StatusCode(403, payload),
                WorkShiftErrorCodes.OperatorNotAuthorized => StatusCode(403, payload),
                WorkShiftErrorCodes.OperatorPinLocked => StatusCode(423, payload),
                WorkShiftErrorCodes.TerminalNotFound => NotFound(payload),
                WorkShiftErrorCodes.PosOpenContextRequired
                    or WorkShiftErrorCodes.PosOpenContextInvalid => Unauthorized(payload),
                WorkShiftErrorCodes.DuplicateRequest
                    or WorkShiftErrorCodes.ConcurrencyConflict
                    or WorkShiftErrorCodes.StaffHubOpenRequired
                    or WorkShiftErrorCodes.OpeningCashRequired
                    or WorkShiftErrorCodes.TerminalAlreadyHasOpenShift
                    or WorkShiftErrorCodes.StaffAlreadyHasOpenShift
                    or WorkShiftErrorCodes.WorkShiftExpired
                    or WorkShiftErrorCodes.WorkShiftPendingClose => Conflict(payload),
                _ => BadRequest(payload)
            };
        }

        private IActionResult CreatedSummary(ShiftSummaryDto summary)
        {
            summary.ResultCode = WorkShiftOpenResultCodes.OpenedNewWorkShift;
            summary.RequiresOpeningCash = false;
            summary.RecommendedAction = WorkShiftRecommendedActions.ContinuePos;
            return StatusCode(StatusCodes.Status201Created, summary);
        }
    }
}
