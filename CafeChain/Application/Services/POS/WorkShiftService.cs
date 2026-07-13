using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Attendance;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Results;
using CafeChain.Infrastructure.Interfaces.Admin.POS;
using CafeChain.Models.Operations;
using CafeChain.Models.Orders;
using CafeChain.Models.Stores;
using Microsoft.Extensions.Logging;
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace CafeChain.Application.Services.POS
{
    public class WorkShiftService : IWorkShiftService
    {
        private readonly IWorkShiftRepository _shiftRepo;
        private readonly IHrAttendanceService _hrAttendanceService;
        private readonly IPOSOrderRepository _posRepo;
        private readonly ISupervisorAuthService _supervisorAuthService;
        private readonly IOtpChallengeRepository _otpChallengeRepo;
        private readonly IOtpPayloadFingerprintService _otpFingerprint;
        private readonly ILogger<WorkShiftService> _logger;

        public WorkShiftService(
            IWorkShiftRepository shiftRepo,
            IHrAttendanceService hrAttendanceService,
            IPOSOrderRepository posRepo,
            ISupervisorAuthService supervisorAuthService,
            IOtpChallengeRepository otpChallengeRepo,
            IOtpPayloadFingerprintService otpFingerprint,
            ILogger<WorkShiftService> logger)
        {
            _shiftRepo = shiftRepo;
            _hrAttendanceService = hrAttendanceService;
            _posRepo = posRepo;
            _supervisorAuthService = supervisorAuthService;
            _otpChallengeRepo = otpChallengeRepo;
            _otpFingerprint = otpFingerprint;
            _logger = logger;
        }

        public async Task<ServiceResult> OpenShiftAsync(int userId, int storeId, OpenShiftRequestDto request)
        {
            try
            {
                request ??= new OpenShiftRequestDto();
                var startingCash = request.StartingCash;
                var posTerminalId = request.PosTerminalId;

                if (!await _hrAttendanceService.VerifyRecentCheckInAsync(userId, storeId))
                {
                    return ServiceResult.Failure(
                        "Từ chối truy cập: Vui lòng sử dụng điện thoại cá nhân kết nối Wifi quán và quét khuôn mặt để Chấm công trước khi Nhận ca POS!");
                }

                var activeShift = await _shiftRepo.GetActiveShiftAsync(userId, storeId);
                if (activeShift != null)
                {
                    return ServiceResult.Failure("Bạn đang có một ca làm việc chưa được đóng. Vui lòng đóng ca trước khi nhận ca mới.");
                }

                var staffShiftToday = await _shiftRepo.GetTodayStaffShiftAsync(userId);
                var isLate = false;
                string scheduledCanonical = "none";
                string? lateScheduleMessage = null;

                if (staffShiftToday?.Shift != null)
                {
                    var today = DateTime.Today;
                    var shiftStartTime = today.Add(staffShiftToday.Shift.StartTime);
                    scheduledCanonical = shiftStartTime.ToString(
                        "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
                    var minutesLate = (DateTime.Now - shiftStartTime).TotalMinutes;

                    if (minutesLate > OtpConstants.LateOpenThresholdMinutes)
                    {
                        isLate = true;
                        lateScheduleMessage =
                            $"Ca của bạn bắt đầu lúc {staffShiftToday.Shift.StartTime:hh\\:mm}. Bạn đã trễ hơn {OtpConstants.LateOpenThresholdMinutes} phút.";
                    }
                }

                OtpChallenge? otpChallenge = null;
                var ownsTransaction = false;

                try
                {
                    if (isLate)
                    {
                        if (request.OtpChallengePublicId == null)
                        {
                            return ServiceResult.Failure(
                                $"LATE_OPENING_REQUIRES_OTP|{lateScheduleMessage} Cần OTP từ Ca trưởng/QL chi nhánh để mở ca trễ (online).",
                                errorCode: OtpConstants.ErrorCodes.LateOpeningRequiresOtp);
                        }

                        var lateReason = request.LateOpeningReason?.Trim();
                        if (string.IsNullOrWhiteSpace(lateReason))
                        {
                            return ServiceResult.Failure("Vui lòng nhập lý do mở ca trễ (khớp yêu cầu OTP).");
                        }

                        await _otpChallengeRepo.BeginTransactionAsync();
                        ownsTransaction = true;

                        otpChallenge = await _otpChallengeRepo.GetByPublicIdForUpdateAsync(request.OtpChallengePublicId.Value);
                        var expectedFingerprint = _otpFingerprint.BuildOpenShiftLateFingerprint(
                            storeId,
                            userId,
                            startingCash,
                            lateReason,
                            scheduledCanonical);

                        var otpError = await ValidateAndPrepareOtpConsumeAsync(
                            otpChallenge,
                            expectedActionType: OtpConstants.ActionTypes.OpenShiftLate,
                            workShiftId: null,
                            storeId: storeId,
                            actorStaffId: userId,
                            expectedFingerprint: expectedFingerprint,
                            expectedTargetId: userId);

                        if (otpError != null)
                        {
                            await _otpChallengeRepo.RollbackTransactionAsync();
                            return ServiceResult.Failure(otpError.Value.message, errorCode: otpError.Value.code);
                        }
                    }

                    var normalizedTerminalId = string.IsNullOrWhiteSpace(posTerminalId)
                        ? null
                        : posTerminalId.Trim();

                    if (normalizedTerminalId != null)
                    {
                        var terminalName = $"POS-Store{storeId}-{DateTime.Now:MMdd-HHmm}";
                        await _shiftRepo.EnsurePosTerminalAsync(normalizedTerminalId, storeId, terminalName);
                    }

                    var newShift = new WorkShift
                    {
                        UserId = userId,
                        StoreId = storeId,
                        StartTime = DateTime.Now,
                        StartingCash = startingCash,
                        ExpectedEndingCash = startingCash,
                        Status = "Open",
                        PosTerminalId = normalizedTerminalId
                    };

                    await _shiftRepo.CreateShiftAsync(newShift);

                    if (otpChallenge != null)
                    {
                        otpChallenge.Status = OtpConstants.Statuses.Used;
                        otpChallenge.UsedAt = DateTime.UtcNow;
                        otpChallenge.WorkShiftId = newShift.ShiftId;
                        await _otpChallengeRepo.SaveChangesAsync();
                    }

                    if (ownsTransaction)
                        await _otpChallengeRepo.CommitTransactionAsync();

                    if (otpChallenge != null)
                    {
                        // Audit after successful mutation+consume — never authorization authority.
                        try
                        {
                            await _posRepo.CreateAuditLogAsync(new InvoiceAuditLog
                            {
                                OrderId = newShift.ShiftId,
                                CashierId = userId,
                                SupervisorId = otpChallenge.ApproverStaffId,
                                ActionName = OtpConstants.ActionTypes.OpenShiftLate,
                                Reason = Truncate(
                                    $"OTP {otpChallenge.PublicId:N}; {request.LateOpeningReason}", 500),
                                CreatedAt = DateTime.Now
                            });
                        }
                        catch (Exception auditEx)
                        {
                            _logger.LogWarning(auditEx,
                                "OPEN_SHIFT_LATE audit write failed after successful open | ShiftId={ShiftId}",
                                newShift.ShiftId);
                        }
                    }

                    return ServiceResult.Success("Mở ca thành công! Chào mừng bạn.");
                }
                catch
                {
                    if (ownsTransaction)
                        await _otpChallengeRepo.RollbackTransactionAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                return ServiceResult.Failure("Lỗi hệ thống khi mở ca: " + ex.Message);
            }
        }

        public async Task<WorkShift?> GetActiveShiftAsync(int userId, int storeId)
        {
            return await _shiftRepo.GetActiveShiftAsync(userId, storeId);
        }

        public async Task<WorkShift?> GetShiftByIdAsync(int shiftId, int userId, int storeId)
        {
            return await _shiftRepo.GetShiftByIdAsync(shiftId, userId, storeId);
        }

        public async Task<ServiceResult> CloseShiftAsync(int userId, int storeId, CloseShiftRequestDto request)
        {
            try
            {
                var activeShift = await _shiftRepo.GetActiveShiftAsync(userId, storeId);
                if (activeShift == null)
                    return ServiceResult.Failure("Không tìm thấy ca két tiền đang mở.");

                if (await _shiftRepo.HasOpenPosPaymentAsync(activeShift.ShiftId, storeId))
                {
                    return ServiceResult.Failure(
                        "Không thể đóng ca thường. Đang có giao dịch thanh toán chưa hoàn tất. " +
                        "Vui lòng hoàn tất hoặc hủy giao dịch trước khi đóng ca.");
                }

                var totalCashSales = await _shiftRepo.GetTotalCashSalesAsync(activeShift.ShiftId);
                var expectedEndingCash = activeShift.StartingCash + totalCashSales;
                var discrepancy = request.ActualEndingCash - expectedEndingCash;

                if (discrepancy != 0 && string.IsNullOrWhiteSpace(request.DiscrepancyReason))
                {
                    return ServiceResult.Failure($"Phát hiện chênh lệch {discrepancy:N0}đ. Vui lòng nhập lý do chênh lệch.");
                }

                OtpChallenge? otpChallenge = null;
                var absDiscrepancy = Math.Abs(discrepancy);
                var otpRequired = absDiscrepancy > OtpConstants.Thresholds.AbsoluteAmountVnd
                    || (expectedEndingCash > 0 && absDiscrepancy / expectedEndingCash > OtpConstants.Thresholds.PercentageOfExpected);

                var ownsTransaction = false;
                try
                {
                    if (otpRequired)
                    {
                        if (request.OtpChallengePublicId == null)
                        {
                            return ServiceResult.Failure(
                                "Chênh lệch két tiền vượt ngưỡng cho phép. Cần xác nhận OTP từ Ca trưởng.",
                                errorCode: OtpConstants.ErrorCodes.Required);
                        }

                        await _otpChallengeRepo.BeginTransactionAsync();
                        ownsTransaction = true;

                        otpChallenge = await _otpChallengeRepo.GetByPublicIdForUpdateAsync(request.OtpChallengePublicId.Value);
                        var expectedFingerprint = _otpFingerprint.BuildCashDifferenceFingerprint(
                            storeId,
                            userId,
                            activeShift.ShiftId,
                            request.ActualEndingCash,
                            request.DiscrepancyReason ?? string.Empty);

                        var otpError = await ValidateAndPrepareOtpConsumeAsync(
                            otpChallenge,
                            OtpConstants.ActionTypes.CashDifference,
                            activeShift.ShiftId,
                            storeId,
                            userId,
                            expectedFingerprint,
                            expectedTargetId: activeShift.ShiftId);

                        if (otpError != null)
                        {
                            await _otpChallengeRepo.RollbackTransactionAsync();
                            return ServiceResult.Failure(otpError.Value.message, errorCode: otpError.Value.code);
                        }
                    }

                    activeShift.ExpectedEndingCash = expectedEndingCash;
                    activeShift.ActualEndingCash = request.ActualEndingCash;
                    activeShift.CashDiscrepancy = discrepancy;
                    activeShift.DiscrepancyReason = request.DiscrepancyReason;
                    activeShift.EndTime = DateTime.Now;
                    activeShift.Status = "Closed";

                    if (otpChallenge != null)
                    {
                        otpChallenge.Status = OtpConstants.Statuses.Used;
                        otpChallenge.UsedAt = DateTime.UtcNow;
                        await _otpChallengeRepo.SaveChangesAsync();
                    }

                    await _shiftRepo.UpdateShiftAsync(activeShift);

                    if (ownsTransaction)
                        await _otpChallengeRepo.CommitTransactionAsync();

                    if (discrepancy != 0)
                    {
                        _logger.LogWarning(
                            "SHIFT_RECONCILIATION_DISCREPANCY | ShiftId={ShiftId} | StoreId={StoreId} | " +
                            "UserId={UserId} | Expected={Expected:N0}đ | Actual={Actual:N0}đ | " +
                            "Discrepancy={Discrepancy:N0}đ | Reason=\"{Reason}\"",
                            activeShift.ShiftId, storeId, userId,
                            expectedEndingCash, request.ActualEndingCash,
                            discrepancy, request.DiscrepancyReason ?? "N/A");
                    }

                    return ServiceResult.Success(
                        $"Đóng ca thành công! Doanh thu tiền mặt: {totalCashSales:N0}đ. " +
                        $"Kỳ vọng: {expectedEndingCash:N0}đ. Thực tế: {request.ActualEndingCash:N0}đ. " +
                        $"Chênh lệch: {discrepancy:N0}đ.");
                }
                catch (Exception)
                {
                    if (ownsTransaction)
                        await _otpChallengeRepo.RollbackTransactionAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                return ServiceResult.Failure("Lỗi hệ thống khi đóng ca: " + ex.Message);
            }
        }

        public async Task<ServiceResult> CloseShiftByExceptionAsync(
            int userId,
            int storeId,
            int shiftId,
            CloseShiftExceptionRequestDto request)
        {
            try
            {
                if (request == null)
                    return ServiceResult.Failure("Thiếu dữ liệu đóng ca ngoại lệ.");

                // Phase 2: PIN no longer authorizes this flow.
                if (!string.IsNullOrWhiteSpace(request.SupervisorPin))
                {
                    return ServiceResult.Failure(
                        "Đóng ca ngoại lệ không còn dùng PIN supervisor. Vui lòng gửi và xác nhận OTP online.",
                        errorCode: OtpConstants.ErrorCodes.FeatureNotAvailable);
                }

                var exceptionReason = request.ExceptionReason?.Trim();
                if (string.IsNullOrWhiteSpace(exceptionReason))
                    return ServiceResult.Failure("Vui lòng nhập lý do đóng ca ngoại lệ.");

                if (request.OtpChallengePublicId == null || request.OtpChallengePublicId == Guid.Empty)
                {
                    return ServiceResult.Failure(
                        "Cần OTP phê duyệt (online) để đóng ca ngoại lệ.",
                        errorCode: OtpConstants.ErrorCodes.Required);
                }

                var activeShift = await _shiftRepo.GetActiveShiftAsync(userId, storeId);
                if (activeShift == null || activeShift.ShiftId != shiftId)
                    return ServiceResult.Failure("Không tìm thấy ca két tiền đang mở với ID này.");

                if (await _shiftRepo.HasOpenPosPaymentAsync(activeShift.ShiftId, storeId))
                {
                    return ServiceResult.Failure(
                        "Không thể đóng ca ngoại lệ. Đang có giao dịch thanh toán chưa hoàn tất. " +
                        "Vui lòng hoàn tất hoặc hủy giao dịch trước khi đóng ca.");
                }

                var offlineSummary = request.OfflineQueueSummary ?? new OfflineQueueSummaryDto();
                if (offlineSummary.OfflineOrderCount < 0 ||
                    offlineSummary.EstimatedTotal < 0 ||
                    offlineSummary.LocalCashTotal < 0)
                {
                    return ServiceResult.Failure("Tóm tắt đơn offline không hợp lệ.");
                }

                var totalCashSales = await _shiftRepo.GetTotalCashSalesAsync(activeShift.ShiftId);
                var expectedEndingCash = activeShift.StartingCash + totalCashSales;
                var discrepancy = request.ActualEndingCash - expectedEndingCash;

                if (discrepancy != 0 && string.IsNullOrWhiteSpace(request.DiscrepancyReason))
                {
                    return ServiceResult.Failure($"Phát hiện chênh lệch {discrepancy:N0}đ. Vui lòng nhập lý do chênh lệch.");
                }

                await _otpChallengeRepo.BeginTransactionAsync();
                try
                {
                    var otpChallenge = await _otpChallengeRepo.GetByPublicIdForUpdateAsync(request.OtpChallengePublicId.Value);
                    var expectedFingerprint = _otpFingerprint.BuildCloseShiftExceptionFingerprint(
                        storeId,
                        userId,
                        shiftId,
                        request.ActualEndingCash,
                        exceptionReason,
                        request.DiscrepancyReason,
                        offlineSummary);

                    var otpError = await ValidateAndPrepareOtpConsumeAsync(
                        otpChallenge,
                        OtpConstants.ActionTypes.CloseShiftException,
                        shiftId,
                        storeId,
                        userId,
                        expectedFingerprint,
                        expectedTargetId: shiftId);

                    if (otpError != null)
                    {
                        await _otpChallengeRepo.RollbackTransactionAsync();
                        return ServiceResult.Failure(otpError.Value.message, errorCode: otpError.Value.code);
                    }

                    var closedAt = DateTime.Now;
                    activeShift.ExpectedEndingCash = expectedEndingCash;
                    activeShift.ActualEndingCash = request.ActualEndingCash;
                    activeShift.CashDiscrepancy = discrepancy;
                    activeShift.DiscrepancyReason = request.DiscrepancyReason;
                    activeShift.EndTime = closedAt;
                    activeShift.Status = "Closed";
                    activeShift.IsExceptionClosed = true;
                    activeShift.ExceptionCloseReason = exceptionReason;
                    activeShift.ExceptionClosedByStaffId = otpChallenge!.ApproverStaffId;
                    activeShift.ExceptionClosedAt = closedAt;
                    activeShift.OfflineOrderCountAtClose = offlineSummary.OfflineOrderCount;
                    activeShift.OfflineEstimatedTotalAtClose = offlineSummary.EstimatedTotal;
                    activeShift.OfflineCashTotalAtClose = offlineSummary.LocalCashTotal;
                    activeShift.RequiresReconciliation = true;

                    otpChallenge.Status = OtpConstants.Statuses.Used;
                    otpChallenge.UsedAt = DateTime.UtcNow;
                    await _otpChallengeRepo.SaveChangesAsync();
                    await _shiftRepo.UpdateShiftAsync(activeShift);
                    await _otpChallengeRepo.CommitTransactionAsync();

                    try
                    {
                        await _posRepo.CreateAuditLogAsync(new InvoiceAuditLog
                        {
                            OrderId = shiftId,
                            CashierId = userId,
                            SupervisorId = otpChallenge.ApproverStaffId,
                            ActionName = OtpConstants.ActionTypes.CloseShiftException,
                            Reason = Truncate(
                                $"OTP {otpChallenge.PublicId:N}; offline={offlineSummary.OfflineOrderCount}; {exceptionReason}",
                                500),
                            CreatedAt = DateTime.Now
                        });
                    }
                    catch (Exception auditEx)
                    {
                        _logger.LogWarning(auditEx,
                            "CLOSE_SHIFT_EXCEPTION audit write failed after successful close | ShiftId={ShiftId}",
                            shiftId);
                    }

                    return ServiceResult.Success(
                        $"Đóng ca ngoại lệ thành công. Ca cần đối soát lại sau khi các đơn offline đồng bộ. " +
                        $"Offline chưa sync: {offlineSummary.OfflineOrderCount} đơn, " +
                        $"ước tính {offlineSummary.EstimatedTotal:N0}đ.");
                }
                catch
                {
                    await _otpChallengeRepo.RollbackTransactionAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                return ServiceResult.Failure("Lỗi hệ thống khi đóng ca ngoại lệ: " + ex.Message);
            }
        }

        /// <summary>
        /// Validates an approved OTP challenge for consume. Returns null if valid.
        /// </summary>
        private async Task<(string message, string? code)?> ValidateAndPrepareOtpConsumeAsync(
            OtpChallenge? challenge,
            string expectedActionType,
            int? workShiftId,
            int storeId,
            int actorStaffId,
            string expectedFingerprint,
            int? expectedTargetId)
        {
            if (challenge == null)
                return ("Mã OTP không tồn tại hoặc đã hết hạn.", null);

            if (challenge.Status != OtpConstants.Statuses.Approved)
            {
                var msg = challenge.Status switch
                {
                    OtpConstants.Statuses.Used => "Mã OTP này đã được sử dụng.",
                    OtpConstants.Statuses.Expired => "Mã OTP đã hết hạn.",
                    OtpConstants.Statuses.Locked => "Mã OTP đã bị khóa do nhập sai quá nhiều lần.",
                    OtpConstants.Statuses.Cancelled => "Mã OTP đã bị hủy.",
                    OtpConstants.Statuses.Pending => "Mã OTP chưa được Ca trưởng duyệt.",
                    _ => $"Mã OTP ở trạng thái '{challenge.Status}', chưa được Ca trưởng duyệt."
                };
                return (msg, null);
            }

            if (challenge.ExpiresAt < DateTime.UtcNow)
                return ("Mã OTP đã hết hạn.", null);

            if (!string.Equals(challenge.ActionType, expectedActionType, StringComparison.Ordinal))
                return ($"Mã OTP không dùng cho thao tác {expectedActionType}.", null);

            if (challenge.TargetType != OtpConstants.TargetTypes.Shifts)
                return ("Mã OTP không dùng cho ca két tiền.", null);

            if (workShiftId.HasValue)
            {
                if (challenge.WorkShiftId != workShiftId)
                    return ("Mã OTP không thuộc ca két tiền hiện tại.", null);

                if (challenge.TargetId != null && challenge.TargetId != workShiftId)
                    return ("Mã OTP chỉ định ca két tiền khác, không khớp ca hiện tại.", null);
            }
            else if (expectedTargetId.HasValue)
            {
                if (challenge.TargetId != expectedTargetId)
                    return ("Mã OTP không khớp đối tượng mở ca.", null);
            }

            if (challenge.StoreId != storeId)
                return ("Mã OTP không thuộc chi nhánh hiện tại.", null);

            if (challenge.RequestedByStaffId != actorStaffId)
                return ("Mã OTP không thuộc nhân viên đang thao tác.", null);

            if (challenge.ApproverStaffId == actorStaffId
                || challenge.ApproverStaffId == challenge.RequestedByStaffId)
            {
                return ("Không được tự duyệt OTP cho chính mình.", OtpConstants.ErrorCodes.NoEligibleApprover);
            }

            if (!_otpFingerprint.FixedTimeEquals(challenge.PayloadFingerprint, expectedFingerprint))
            {
                return (
                    "Dữ liệu thao tác không khớp yêu cầu OTP. Vui lòng gửi OTP mới.",
                    OtpConstants.ErrorCodes.PayloadMismatch);
            }

            var approverOk = await _otpChallengeRepo.IsApproverStillEligibleAsync(
                challenge.ApproverStaffId, storeId, actorStaffId);
            if (!approverOk)
            {
                return (
                    "Người duyệt OTP không còn hợp lệ (role/store/active). Vui lòng gửi OTP mới.",
                    OtpConstants.ErrorCodes.ApproverNoLongerEligible);
            }

            return null;
        }

        private static string Truncate(string value, int max)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= max)
                return value;
            return value.Substring(0, max);
        }
    }
}
