using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Interfaces.Systems;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Options;
using CafeChain.Application.Results;
using CafeChain.Infrastructure.Interfaces.Admin.POS;
using CafeChain.Models.Operations;
using CafeChain.Models.Orders;
using CafeChain.Models.Stores;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace CafeChain.Application.Services.POS
{
    public class WorkShiftService : IWorkShiftService
    {
        private readonly IWorkShiftRepository _shiftRepo;
        private readonly IPOSOrderRepository _posRepo;
        private readonly IOtpChallengeRepository _otpChallengeRepo;
        private readonly IOtpPayloadFingerprintService _otpFingerprint;
        private readonly ILogger<WorkShiftService> _logger;
        private readonly decimal _cashDenominationStep;
        private readonly WorkShiftOptions _workShiftOptions;
        private readonly TimeProvider _timeProvider;
        private readonly IRequestDeduplicationService? _deduplication;
        private readonly IWorkShiftAuditService? _audit;
        private readonly IAdminPermissionService? _permissions;
        private readonly IWorkShiftNotificationPublisher? _notifications;

        public WorkShiftService(
            IWorkShiftRepository shiftRepo,
            IPOSOrderRepository posRepo,
            IOtpChallengeRepository otpChallengeRepo,
            IOtpPayloadFingerprintService otpFingerprint,
            ILogger<WorkShiftService> logger,
            IOptions<POSPaymentOptions>? paymentOptions = null,
            IOptions<WorkShiftOptions>? workShiftOptions = null,
            TimeProvider? timeProvider = null,
            IRequestDeduplicationService? deduplication = null,
            IWorkShiftAuditService? audit = null,
            IAdminPermissionService? permissions = null,
            IWorkShiftNotificationPublisher? notifications = null)
        {
            _shiftRepo = shiftRepo;
            _posRepo = posRepo;
            _otpChallengeRepo = otpChallengeRepo;
            _otpFingerprint = otpFingerprint;
            _logger = logger;
            _cashDenominationStep = paymentOptions?.Value.GetEffectiveCashDenominationStep()
                ?? POSPaymentOptions.DefaultCashDenominationStep;
            _workShiftOptions = workShiftOptions?.Value ?? new WorkShiftOptions();
            _timeProvider = timeProvider ?? TimeProvider.System;
            _deduplication = deduplication;
            _audit = audit;
            _permissions = permissions;
            _notifications = notifications;
        }

        public async Task<ServiceResult> OpenShiftAsync(int userId, int storeId, OpenShiftRequestDto request)
        {
            Models.Systems.RequestDeduplication? dedupEntry = null;
            var ownsTransaction = false;
            try
            {
                request ??= new OpenShiftRequestDto();
                if (_deduplication != null
                    && (string.IsNullOrWhiteSpace(request.RequestKey) || request.RequestKey.Trim().Length > 200))
                    return ServiceResult.Failure("RequestKey không hợp lệ.", errorCode: WorkShiftErrorCodes.InvalidRequestKey);

                var cashError = POSCashAmountValidator.Validate(
                    request.StartingCash,
                    _cashDenominationStep,
                    allowZero: true);
                if (cashError != null)
                    return ServiceResult.Failure(
                        $"Tiền đầu phiên không hợp lệ. {cashError}",
                        errorCode: WorkShiftErrorCodes.InvalidCashAmount);

                var terminalId = request.PosTerminalId?.Trim();
                if (string.IsNullOrWhiteSpace(terminalId) && _deduplication != null)
                    return ServiceResult.Failure("Terminal POS là bắt buộc.", errorCode: WorkShiftErrorCodes.TerminalNotFound);
                terminalId ??= string.Empty;

                if (_permissions != null)
                {
                    var actor = await _otpChallengeRepo.GetRequestingStaffAsync(userId, storeId);
                    if (actor?.Account == null || !actor.Active || !actor.Account.Active)
                        return ServiceResult.Failure("Tài khoản hoặc hồ sơ nhân viên không hoạt động.", errorCode: WorkShiftErrorCodes.PosPermissionRequired);
                    var openPermission = await _permissions.HasPermissionAsync(actor.AccountId, PermissionConstants.PosWorkShiftOpen, storeId);
                    if (!openPermission.IsSuccess || openPermission.Data?.Allowed != true)
                        return ServiceResult.Failure("Bạn không có quyền mở phiên POS tại cửa hàng này.", errorCode: WorkShiftErrorCodes.PosPermissionRequired);
                }

                var current = await _shiftRepo.GetActiveShiftAsync(userId, storeId);
                if (current != null)
                    return ServiceResult.Failure(
                        "Nhân viên đang chịu trách nhiệm một phiên POS chưa kết thúc.",
                        errorCode: WorkShiftErrorCodes.StaffAlreadyHasOpenShift);

                var assessment = await AssessOpenShiftCoreAsync(userId, storeId);
                if (_permissions != null && assessment.OpenContext == WorkShiftOpenContexts.OutsideSchedule)
                {
                    var actor = await _otpChallengeRepo.GetRequestingStaffAsync(userId, storeId);
                    var outsidePermission = actor?.Account == null
                        ? null
                        : await _permissions.HasPermissionAsync(actor.AccountId, PermissionConstants.PosWorkShiftOpenOutsideSchedule, storeId);
                    if (outsidePermission == null || !outsidePermission.IsSuccess || outsidePermission.Data?.Allowed != true)
                        return ServiceResult.Failure("Bạn không có quyền mở POS ngoài lịch.", errorCode: WorkShiftErrorCodes.PosPermissionRequired);
                }
                var reason = (request.Reason ?? request.LateOpeningReason)?.Trim();
                if (assessment.ReasonRequired && !IsValidReason(reason))
                    return ServiceResult.Failure(
                        assessment.OpenContext == WorkShiftOpenContexts.OutsideSchedule
                            ? "Lý do mở POS ngoài lịch phải có từ 10 đến 500 ký tự và có nội dung cụ thể."
                            : "Vui lòng nhập lý do mở POS trễ.",
                        errorCode: assessment.OpenContext == WorkShiftOpenContexts.OutsideSchedule
                            ? WorkShiftErrorCodes.OutsideScheduleReasonRequired
                            : OtpConstants.ErrorCodes.LateOpeningRequiresOtp);

                if (assessment.ApprovalRequired && request.OtpChallengePublicId == null)
                    return ServiceResult.Failure(
                        assessment.OpenContext == WorkShiftOpenContexts.OutsideSchedule
                            ? "Mở POS ngoài lịch cần OTP phê duyệt hợp lệ."
                            : "Mở POS trễ quá ngưỡng cần OTP phê duyệt.",
                        errorCode: assessment.OpenContext == WorkShiftOpenContexts.OutsideSchedule
                            ? WorkShiftErrorCodes.OutsideScheduleApprovalRequired
                            : OtpConstants.ErrorCodes.LateOpeningRequiresOtp);

                if (_deduplication != null || assessment.ApprovalRequired)
                {
                    await _otpChallengeRepo.BeginTransactionAsync();
                    ownsTransaction = true;
                }

                if (_deduplication != null)
                {
                    var begin = await _deduplication.BeginScopedAsync(
                        request.RequestKey,
                        "POS.WORKSHIFT.OPEN",
                        userId,
                        request,
                        referenceId: null,
                        storeId: storeId,
                        accountId: null);
                    if (!begin.CanProcess)
                    {
                        if (ownsTransaction) await _otpChallengeRepo.RollbackTransactionAsync();
                        ownsTransaction = false;
                        if (string.Equals(begin.Status, "SUCCESS", StringComparison.Ordinal))
                            return ServiceResult.Success("Yêu cầu mở POS đã được xử lý trước đó.");
                        return ServiceResult.Failure(
                            begin.ErrorMessage ?? "RequestKey đã được sử dụng.",
                            errorCode: begin.ErrorCode == "IDEMPOTENCY_KEY_REUSED"
                                ? WorkShiftErrorCodes.DuplicateRequest
                                : begin.ErrorCode);
                    }
                    dedupEntry = begin.Entry;
                }

                OtpChallenge? otpChallenge = null;
                if (assessment.ApprovalRequired)
                {
                    otpChallenge = await _otpChallengeRepo.GetByPublicIdForUpdateAsync(request.OtpChallengePublicId!.Value);
                    var scheduledCanonical = assessment.PlannedStartUtc.HasValue
                        ? TimeZoneInfo.ConvertTimeFromUtc(
                            DateTime.SpecifyKind(assessment.PlannedStartUtc.Value, DateTimeKind.Utc),
                            _workShiftOptions.ResolveTimeZone())
                            .ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture)
                        : "none";
                    var expectedAction = assessment.OpenContext == WorkShiftOpenContexts.OutsideSchedule
                        ? OtpConstants.ActionTypes.OpenShiftOutsideSchedule
                        : OtpConstants.ActionTypes.OpenShiftLate;
                    var expectedFingerprint = _otpFingerprint.BuildOpenShiftBoundFingerprint(
                        storeId,
                        userId,
                        request.StartingCash,
                        reason ?? string.Empty,
                        scheduledCanonical,
                        expectedAction,
                        _deduplication != null ? terminalId : null,
                        _deduplication != null ? request.RequestKey : null);
                    var otpError = await ValidateAndPrepareOtpConsumeAsync(
                        otpChallenge,
                        expectedAction,
                        null,
                        storeId,
                        userId,
                        expectedFingerprint,
                        userId);
                    if (otpError != null)
                    {
                        if (ownsTransaction) await _otpChallengeRepo.RollbackTransactionAsync();
                        ownsTransaction = false;
                        return ServiceResult.Failure(otpError.Value.message, errorCode: otpError.Value.code);
                    }
                }

                await _shiftRepo.EnsurePosTerminalAsync(terminalId, storeId, terminalId);
                var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
                var businessDate = assessment.SourceStaffShift?.WorkDate.Date
                    ?? TimeZoneInfo.ConvertTimeFromUtc(nowUtc, _workShiftOptions.ResolveTimeZone()).Date;
                var newShift = new WorkShift
                {
                    UserId = userId,
                    StoreId = storeId,
                    StartTimeUtc = nowUtc,
                    BusinessDate = businessDate,
                    SourceStaffShiftId = assessment.SourceStaffShift?.StaffShiftId,
                    OpenContext = assessment.OpenContext,
                    OutsideScheduleReason = assessment.OpenContext == WorkShiftOpenContexts.OutsideSchedule ? reason : null,
                    ApprovedByStaffId = otpChallenge?.ApproverStaffId,
                    ApprovedAtUtc = otpChallenge?.ApprovedAt,
                    AutoCloseAtUtc = assessment.OpenContext == WorkShiftOpenContexts.OutsideSchedule
                        ? nowUtc.AddHours(_workShiftOptions.OutsideScheduleDurationHours)
                        : null,
                    StartingCash = request.StartingCash,
                    ExpectedEndingCash = request.StartingCash,
                    Status = WorkShiftStatuses.Open,
                    PosTerminalId = terminalId
                };

                await _shiftRepo.CreateShiftAsync(newShift);
                if (_audit != null)
                {
                    await _audit.WriteAsync(
                        "WORKSHIFT_OPENED",
                        newShift.ShiftId,
                        userId,
                        null,
                        new
                        {
                            newShift.StoreId,
                            newShift.UserId,
                            newShift.PosTerminalId,
                            newShift.SourceStaffShiftId,
                            newShift.OpenContext,
                            newShift.BusinessDate,
                            newShift.StartTimeUtc,
                            newShift.AutoCloseAtUtc,
                            RequestKey = request.RequestKey,
                            Reason = reason,
                            ApproverStaffId = otpChallenge?.ApproverStaffId
                        });
                }
                if (otpChallenge != null)
                {
                    otpChallenge.Status = OtpConstants.Statuses.Used;
                    otpChallenge.UsedAt = nowUtc;
                    otpChallenge.ProtectedOtpPayload = null;
                    otpChallenge.WorkShiftId = newShift.ShiftId;
                    otpChallenge.TerminalId = terminalId;
                    otpChallenge.RequestKey = request.RequestKey.Trim();
                    await _otpChallengeRepo.SaveChangesAsync();
                }

                if (dedupEntry != null && _deduplication != null)
                    await _deduplication.MarkSuccessAsync(dedupEntry, newShift.ShiftId, new { newShift.ShiftId });

                if (ownsTransaction)
                {
                    await _otpChallengeRepo.CommitTransactionAsync();
                    ownsTransaction = false;
                }

                await PublishNotificationSafeAsync(newShift, "OPENED");

                _logger.LogInformation(
                    "WORKSHIFT_OPENED | ShiftId={ShiftId} StoreId={StoreId} StaffId={StaffId} Context={Context} RequestKey={RequestKey}",
                    newShift.ShiftId, storeId, userId, newShift.OpenContext, request.RequestKey);
                return ServiceResult.Success("Mở phiên POS thành công.");
            }
            catch (WorkShiftBusinessException ex)
            {
                if (ownsTransaction) await _otpChallengeRepo.RollbackTransactionAsync();
                return ServiceResult.Failure(ex.Message, errorCode: ex.ErrorCode);
            }
            catch (Exception ex)
            {
                if (ownsTransaction) await _otpChallengeRepo.RollbackTransactionAsync();
                _logger.LogError(ex, "WORKSHIFT_OPEN_FAILED | StoreId={StoreId} StaffId={StaffId}", storeId, userId);
                return ServiceResult.Failure("Không thể mở phiên POS. Vui lòng thử lại.");
            }
        }

        public async Task<ServiceResult<OpenShiftAssessmentDto>> AssessOpenShiftAsync(
            int userId,
            int storeId,
            OpenShiftAssessmentRequestDto request,
            CancellationToken cancellationToken = default)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.PosTerminalId))
                return ServiceResult<OpenShiftAssessmentDto>.Failure(
                    "Terminal không hợp lệ.",
                    errorCode: WorkShiftErrorCodes.TerminalNotFound);
            try
            {
                await _shiftRepo.EnsurePosTerminalAsync(request.PosTerminalId.Trim(), storeId, request.PosTerminalId.Trim());
                var assessment = await AssessOpenShiftCoreAsync(userId, storeId);
                return ServiceResult<OpenShiftAssessmentDto>.Success(assessment.ToDto(_workShiftOptions));
            }
            catch (WorkShiftBusinessException ex)
            {
                return ServiceResult<OpenShiftAssessmentDto>.Failure(ex.Message, errorCode: ex.ErrorCode);
            }
        }

        public async Task<ServiceResult<OpenShiftAssessmentDto>> AssessOpenContextAsync(
            int staffId,
            int storeId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var actor = await _otpChallengeRepo.GetRequestingStaffAsync(staffId, storeId);
                if (actor?.Account == null || !actor.Active || !actor.Account.Active)
                    return ServiceResult<OpenShiftAssessmentDto>.Failure(
                        "Tài khoản hoặc hồ sơ nhân viên không hoạt động.",
                        errorCode: WorkShiftErrorCodes.PosPermissionRequired);

                if (_permissions != null)
                {
                    var openPermission = await _permissions.HasPermissionAsync(
                        actor.AccountId,
                        PermissionConstants.PosWorkShiftOpen,
                        storeId);
                    if (!openPermission.IsSuccess || openPermission.Data?.Allowed != true)
                        return ServiceResult<OpenShiftAssessmentDto>.Failure(
                            "Bạn không có quyền mở phiên POS tại cửa hàng này.",
                            errorCode: WorkShiftErrorCodes.PosPermissionRequired);
                }

                var assessment = await AssessOpenShiftCoreAsync(staffId, storeId);
                if (_permissions != null
                    && assessment.OpenContext == WorkShiftOpenContexts.OutsideSchedule)
                {
                    var outsidePermission = await _permissions.HasPermissionAsync(
                        actor.AccountId,
                        PermissionConstants.PosWorkShiftOpenOutsideSchedule,
                        storeId);
                    if (!outsidePermission.IsSuccess || outsidePermission.Data?.Allowed != true)
                        return ServiceResult<OpenShiftAssessmentDto>.Failure(
                            "Bạn không có quyền mở POS ngoài lịch.",
                            errorCode: WorkShiftErrorCodes.PosPermissionRequired);
                }

                return ServiceResult<OpenShiftAssessmentDto>.Success(
                    assessment.ToDto(_workShiftOptions));
            }
            catch (WorkShiftBusinessException ex)
            {
                return ServiceResult<OpenShiftAssessmentDto>.Failure(
                    ex.Message,
                    errorCode: ex.ErrorCode);
            }
        }

        private async Task<OpenAssessment> AssessOpenShiftCoreAsync(int userId, int storeId)
        {
            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            var timeZone = _workShiftOptions.ResolveTimeZone();
            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, timeZone);
            var schedule = await _shiftRepo.GetEffectiveStaffShiftAsync(userId, storeId, nowLocal);
            if (schedule?.Shift == null)
                return new OpenAssessment(WorkShiftOpenContexts.OutsideSchedule, null, null, null, 0, true, true, nowUtc);

            var interval = ScheduleIntervalResolver.Resolve(schedule);
            var minutesLate = Math.Max(0, (int)Math.Floor((nowLocal - interval.StartLocal).TotalMinutes));
            var within = nowLocal >= interval.StartLocal.AddMinutes(-_workShiftOptions.EarlyOpenMinutes)
                && nowLocal <= interval.StartLocal.AddMinutes(_workShiftOptions.LateReasonAfterMinutes);
            var late = !within && nowLocal <= interval.EndLocal.AddMinutes(_workShiftOptions.PostEndGraceMinutes);
            if (!within && !late)
                return new OpenAssessment(WorkShiftOpenContexts.OutsideSchedule, null, null, null, 0, true, true, nowUtc);

            return new OpenAssessment(
                within ? WorkShiftOpenContexts.WithinSchedule : WorkShiftOpenContexts.LateForSchedule,
                schedule,
                ScheduleIntervalResolver.ToUtc(interval.StartLocal, timeZone),
                ScheduleIntervalResolver.ToUtc(interval.EndLocal, timeZone),
                minutesLate,
                !within,
                late && minutesLate > _workShiftOptions.LateApprovalAfterMinutes,
                nowUtc);
        }

        private bool IsValidReason(string? reason)
        {
            if (string.IsNullOrWhiteSpace(reason)) return false;
            var value = reason.Trim();
            return value.Length >= _workShiftOptions.MinimumReasonLength
                && value.Length <= _workShiftOptions.MaximumReasonLength
                && value.Any(char.IsLetterOrDigit);
        }

        private sealed record OpenAssessment(
            string OpenContext,
            Models.Staffs.StaffShift? SourceStaffShift,
            DateTime? PlannedStartUtc,
            DateTime? PlannedEndUtc,
            int MinutesLate,
            bool ReasonRequired,
            bool ApprovalRequired,
            DateTime ServerNowUtc)
        {
            public OpenShiftAssessmentDto ToDto(WorkShiftOptions options) => new()
            {
                OpenContext = OpenContext,
                SourceStaffShiftId = SourceStaffShift?.StaffShiftId,
                PlannedStartUtc = PlannedStartUtc,
                PlannedEndUtc = PlannedEndUtc,
                MinutesLate = MinutesLate,
                ReasonRequired = ReasonRequired,
                ApprovalRequired = ApprovalRequired,
                ServerNowUtc = ServerNowUtc,
                AutoCloseAtUtc = OpenContext == WorkShiftOpenContexts.OutsideSchedule
                    ? ServerNowUtc.AddHours(options.OutsideScheduleDurationHours)
                    : null
            };
        }

        public async Task<WorkShift?> GetActiveShiftAsync(int userId, int storeId)
        {
            return await _shiftRepo.GetActiveShiftAsync(userId, storeId);
        }

        public async Task<WorkShift?> GetShiftByIdAsync(int shiftId, int userId, int storeId)
        {
            return await _shiftRepo.GetShiftByIdAsync(shiftId, userId, storeId);
        }

        public async Task<WorkShift?> GetShiftByIdAsync(int shiftId)
        {
            return await _shiftRepo.GetShiftByIdAsync(shiftId);
        }

        public async Task<ShiftSummaryDto?> GetSummaryAsync(
            int userId,
            int storeId,
            int? shiftId = null)
        {
            var shift = shiftId.HasValue
                ? await _shiftRepo.GetShiftByIdAsync(shiftId.Value, userId, storeId)
                : await _shiftRepo.GetActiveShiftAsync(userId, storeId);
            if (shift == null) return null;

            var totalCash = await _shiftRepo.GetTotalCashSalesAsync(shift.ShiftId);
            var totalBanking = await _posRepo.GetTotalSalesByPaymentMethodAsync(shift.ShiftId, 2);
            var totalOrders = await _posRepo.GetCompletedOrderCountAsync(shift.ShiftId);
            return new ShiftSummaryDto
            {
                ShiftId = shift.ShiftId,
                StoreId = shift.StoreId,
                StaffName = shift.User?.FullName,
                StartTime = shift.StartTimeUtc,
                EndTime = shift.EndTimeUtc,
                StartTimeUtc = shift.StartTimeUtc,
                EndTimeUtc = shift.EndTimeUtc,
                BusinessDate = shift.BusinessDate,
                SourceStaffShiftId = shift.SourceStaffShiftId,
                OpenContext = shift.OpenContext,
                AutoCloseAtUtc = shift.AutoCloseAtUtc,
                ExpiredAtUtc = shift.ExpiredAtUtc,
                ClosingStartedAtUtc = shift.ClosingStartedAtUtc,
                ServerNowUtc = _timeProvider.GetUtcNow().UtcDateTime,
                CloseType = shift.CloseType,
                ClosedByStaffId = shift.ClosedByStaffId,
                CloseReason = shift.CloseReason,
                RowVersion = shift.RowVersion == null || shift.RowVersion.Length == 0
                    ? null
                    : Convert.ToBase64String(shift.RowVersion),
                StartingCash = shift.StartingCash,
                ExpectedEndingCash = shift.ExpectedEndingCash,
                ActualEndingCash = shift.ActualEndingCash,
                CashDiscrepancy = shift.CashDiscrepancy,
                IsExceptionClosed = shift.IsExceptionClosed,
                ExceptionCloseReason = shift.ExceptionCloseReason,
                ExceptionClosedByStaffId = shift.ExceptionClosedByStaffId,
                ExceptionClosedAt = shift.ExceptionClosedAt,
                OfflineOrderCountAtClose = shift.OfflineOrderCountAtClose,
                OfflineEstimatedTotalAtClose = shift.OfflineEstimatedTotalAtClose,
                OfflineCashTotalAtClose = shift.OfflineCashTotalAtClose,
                RequiresReconciliation = shift.RequiresReconciliation,
                HasLateOfflineSync = shift.HasLateOfflineSync,
                LateOfflineSyncCount = shift.LateOfflineSyncCount,
                LastLateOfflineSyncedAt = shift.LastLateOfflineSyncedAtUtc,
                TotalCashSales = totalCash,
                TotalBankingSales = totalBanking,
                TotalOrders = totalOrders,
                Status = shift.Status
            };
        }

        public async Task<ServiceResult> CloseShiftAsync(
            int userId,
            int storeId,
            int shiftId,
            CloseShiftRequestDto request)
        {
            var active = await _shiftRepo.GetActiveShiftAsync(userId, storeId);
            if (active == null || active.ShiftId != shiftId)
                return ServiceResult.Failure("Không tìm thấy phiên POS cần đóng.", errorCode: WorkShiftErrorCodes.WorkShiftNotOpen);
            return await CloseShiftAsync(userId, storeId, request);
        }

        public async Task<ServiceResult> CloseShiftAsync(int userId, int storeId, CloseShiftRequestDto request)
        {
            Models.Systems.RequestDeduplication? dedupEntry = null;
            try
            {
                if (request == null)
                    return ServiceResult.Failure("Thiếu dữ liệu đóng ca.");

                var offlineSummary = request.OfflineQueueSummary ?? new OfflineQueueSummaryDto();
                if (offlineSummary.OfflineOrderCount < 0 || offlineSummary.EstimatedTotal < 0 || offlineSummary.LocalCashTotal < 0)
                    return ServiceResult.Failure("Manifest đơn offline không hợp lệ.", errorCode: WorkShiftErrorCodes.OfflineOrdersPending);
                if (offlineSummary.OfflineOrderCount > 0)
                    return ServiceResult.Failure("Đóng thường bị chặn vì còn đơn offline chưa đồng bộ.", errorCode: WorkShiftErrorCodes.OfflineOrdersPending);

                var endingCashError = ValidateActualEndingCash(request.ActualEndingCash);
                if (endingCashError != null)
                    return ServiceResult.Failure(endingCashError);
                if (_deduplication != null
                    && (string.IsNullOrWhiteSpace(request.RequestKey) || request.RequestKey.Trim().Length > 200))
                    return ServiceResult.Failure("RequestKey không hợp lệ.", errorCode: WorkShiftErrorCodes.InvalidRequestKey);

                var activeShift = await _shiftRepo.GetActiveShiftAsync(userId, storeId);
                if (activeShift == null)
                    return ServiceResult.Failure("Không tìm thấy ca két tiền đang mở.");
                if (!MatchesRequestRowVersion(activeShift, request.RowVersion))
                    return ServiceResult.Failure("Dữ liệu phiên POS đã thay đổi.", errorCode: WorkShiftErrorCodes.ConcurrencyConflict);

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

                if (otpRequired && request.OtpChallengePublicId == null)
                    return ServiceResult.Failure(
                        "Chênh lệch két vượt ngưỡng cho phép và cần OTP phê duyệt.",
                        errorCode: WorkShiftErrorCodes.CashDiscrepancyApprovalRequired);

                var ownsTransaction = false;
                try
                {
                    await _otpChallengeRepo.BeginTransactionAsync();
                    ownsTransaction = true;

                    if (_deduplication != null)
                    {
                        var begin = await _deduplication.BeginScopedAsync(
                            request.RequestKey,
                            "POS.WORKSHIFT.CLOSE",
                            userId,
                            request,
                            activeShift.ShiftId,
                            storeId,
                            null);
                        if (!begin.CanProcess)
                        {
                            await _otpChallengeRepo.RollbackTransactionAsync();
                            ownsTransaction = false;
                            return string.Equals(begin.Status, "SUCCESS", StringComparison.Ordinal)
                                ? ServiceResult.Success("Yêu cầu đóng phiên đã được xử lý trước đó.")
                                : ServiceResult.Failure(
                                    begin.ErrorMessage ?? "RequestKey đã được sử dụng.",
                                    errorCode: begin.ErrorCode == "IDEMPOTENCY_KEY_REUSED"
                                        ? WorkShiftErrorCodes.DuplicateRequest
                                        : begin.ErrorCode);
                        }
                        dedupEntry = begin.Entry;
                    }

                    var lockedShift = await _shiftRepo.GetActiveShiftAsync(userId, storeId);
                    if (lockedShift == null
                        || lockedShift.ShiftId != activeShift.ShiftId
                        || !MatchesRequestRowVersion(lockedShift, request.RowVersion))
                        throw new WorkShiftBusinessException(
                            WorkShiftErrorCodes.ConcurrencyConflict,
                            "Dữ liệu phiên POS đã thay đổi.");
                    activeShift = lockedShift;
                    activeShift.Status = WorkShiftStatuses.Closing;
                    activeShift.ClosingStartedAtUtc ??= _timeProvider.GetUtcNow().UtcDateTime;
                    await _shiftRepo.UpdateShiftAsync(activeShift);

                    if (await _shiftRepo.HasOpenPosPaymentAsync(activeShift.ShiftId, storeId))
                        throw new WorkShiftBusinessException(
                            WorkShiftErrorCodes.PaymentInProgress,
                            "Vẫn còn thanh toán đang xử lý.");

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
                    activeShift.EndTimeUtc = _timeProvider.GetUtcNow().UtcDateTime;
                    activeShift.Status = WorkShiftStatuses.Closed;
                    activeShift.CloseType = activeShift.ExpiredAtUtc.HasValue
                        ? WorkShiftCloseTypes.Expired
                        : WorkShiftCloseTypes.Normal;
                    activeShift.ClosedByStaffId = userId;
                    activeShift.CloseReason = request.DiscrepancyReason?.Trim();

                    if (otpChallenge != null)
                    {
                        otpChallenge.Status = OtpConstants.Statuses.Used;
                        otpChallenge.UsedAt = _timeProvider.GetUtcNow().UtcDateTime;
                        otpChallenge.ProtectedOtpPayload = null;
                        await _otpChallengeRepo.SaveChangesAsync();
                    }

                    await _shiftRepo.UpdateShiftAsync(activeShift);

                    if (_audit != null)
                        await _audit.WriteAsync(
                            "WORKSHIFT_CLOSED",
                            activeShift.ShiftId,
                            userId,
                            new { Status = WorkShiftStatuses.Closing },
                            new { activeShift.Status, activeShift.CloseType, activeShift.ExpectedEndingCash, activeShift.ActualEndingCash, activeShift.CashDiscrepancy });

                    if (dedupEntry != null && _deduplication != null)
                        await _deduplication.MarkSuccessAsync(
                            dedupEntry,
                            activeShift.ShiftId,
                            new { activeShift.ShiftId, activeShift.Status });

                    if (ownsTransaction)
                        await _otpChallengeRepo.CommitTransactionAsync();

                    await PublishNotificationSafeAsync(activeShift, "CLOSED");

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
            catch (WorkShiftBusinessException ex)
            {
                return ServiceResult.Failure(ex.Message, errorCode: ex.ErrorCode);
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "WORKSHIFT_CLOSE_CONCURRENCY | StoreId={StoreId} StaffId={StaffId}", storeId, userId);
                return ServiceResult.Failure("Dữ liệu phiên POS đã thay đổi.", errorCode: WorkShiftErrorCodes.ConcurrencyConflict);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WORKSHIFT_CLOSE_FAILED | StoreId={StoreId} StaffId={StaffId}", storeId, userId);
                return ServiceResult.Failure("Không thể đóng phiên POS. Vui lòng thử lại.");
            }
        }

        public async Task<ServiceResult> CloseShiftByExceptionAsync(
            int userId,
            int storeId,
            int shiftId,
            CloseShiftExceptionRequestDto request)
        {
            Models.Systems.RequestDeduplication? dedupEntry = null;
            try
            {
                if (request == null)
                    return ServiceResult.Failure("Thiếu dữ liệu đóng ca ngoại lệ.");
                var endingCashError = ValidateActualEndingCash(request.ActualEndingCash);
                if (endingCashError != null)
                    return ServiceResult.Failure(endingCashError);

                var exceptionReason = request.ExceptionReason?.Trim();
                if (string.IsNullOrWhiteSpace(exceptionReason))
                    return ServiceResult.Failure("Vui lòng nhập lý do đóng ca ngoại lệ.");
                if (_deduplication != null
                    && (string.IsNullOrWhiteSpace(request.RequestKey) || request.RequestKey.Trim().Length > 200))
                    return ServiceResult.Failure("RequestKey không hợp lệ.", errorCode: WorkShiftErrorCodes.InvalidRequestKey);

                if (request.OtpChallengePublicId == null || request.OtpChallengePublicId == Guid.Empty)
                {
                    return ServiceResult.Failure(
                        "Cần OTP phê duyệt (online) để đóng ca ngoại lệ.",
                        errorCode: OtpConstants.ErrorCodes.Required);
                }

                var activeShift = await _shiftRepo.GetActiveShiftAsync(userId, storeId);
                if (activeShift == null || activeShift.ShiftId != shiftId)
                    return ServiceResult.Failure("Không tìm thấy ca két tiền đang mở với ID này.");
                if (!MatchesRequestRowVersion(activeShift, request.RowVersion))
                    return ServiceResult.Failure("Dữ liệu phiên POS đã thay đổi.", errorCode: WorkShiftErrorCodes.ConcurrencyConflict);

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
                    if (_deduplication != null)
                    {
                        var begin = await _deduplication.BeginScopedAsync(
                            request.RequestKey,
                            "POS.WORKSHIFT.CLOSE_EXCEPTION",
                            userId,
                            request,
                            shiftId,
                            storeId,
                            null);
                        if (!begin.CanProcess)
                        {
                            await _otpChallengeRepo.RollbackTransactionAsync();
                            return string.Equals(begin.Status, "SUCCESS", StringComparison.Ordinal)
                                ? ServiceResult.Success("Yêu cầu đóng ngoại lệ đã được xử lý trước đó.")
                                : ServiceResult.Failure(
                                    begin.ErrorMessage ?? "RequestKey đã được sử dụng.",
                                    errorCode: begin.ErrorCode == "IDEMPOTENCY_KEY_REUSED"
                                        ? WorkShiftErrorCodes.DuplicateRequest
                                        : begin.ErrorCode);
                        }
                        dedupEntry = begin.Entry;
                    }

                    var lockedShift = await _shiftRepo.GetActiveShiftAsync(userId, storeId);
                    if (lockedShift == null || lockedShift.ShiftId != shiftId || !MatchesRequestRowVersion(lockedShift, request.RowVersion))
                        throw new WorkShiftBusinessException(
                            WorkShiftErrorCodes.ConcurrencyConflict,
                            "Dữ liệu phiên POS đã thay đổi.");
                    activeShift = lockedShift;

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

                    var closedAt = _timeProvider.GetUtcNow().UtcDateTime;
                    activeShift.ExpectedEndingCash = expectedEndingCash;
                    activeShift.ActualEndingCash = request.ActualEndingCash;
                    activeShift.CashDiscrepancy = discrepancy;
                    activeShift.DiscrepancyReason = request.DiscrepancyReason;
                    activeShift.EndTimeUtc = _timeProvider.GetUtcNow().UtcDateTime;
                    activeShift.Status = WorkShiftStatuses.ReconciliationRequired;
                    activeShift.CloseType = WorkShiftCloseTypes.Exception;
                    activeShift.ClosedByStaffId = userId;
                    activeShift.CloseReason = exceptionReason;
                    activeShift.IsExceptionClosed = true;
                    activeShift.ExceptionCloseReason = exceptionReason;
                    activeShift.ExceptionClosedByStaffId = otpChallenge!.ApproverStaffId;
                    activeShift.ExceptionClosedAt = closedAt;
                    activeShift.OfflineOrderCountAtClose = offlineSummary.OfflineOrderCount;
                    activeShift.OfflineEstimatedTotalAtClose = offlineSummary.EstimatedTotal;
                    activeShift.OfflineCashTotalAtClose = offlineSummary.LocalCashTotal;
                    activeShift.RequiresReconciliation = true;

                    otpChallenge.Status = OtpConstants.Statuses.Used;
                    otpChallenge.UsedAt = _timeProvider.GetUtcNow().UtcDateTime;
                    otpChallenge.ProtectedOtpPayload = null;
                    await _otpChallengeRepo.SaveChangesAsync();
                    await _shiftRepo.UpdateShiftAsync(activeShift);
                    if (_audit != null)
                        await _audit.WriteAsync(
                            "WORKSHIFT_EXCEPTION_CLOSED",
                            activeShift.ShiftId,
                            userId,
                            new { Status = WorkShiftStatuses.Closing },
                            new { activeShift.Status, activeShift.CloseType, activeShift.RequiresReconciliation, Reason = exceptionReason, ApproverStaffId = otpChallenge.ApproverStaffId });
                    if (dedupEntry != null && _deduplication != null)
                        await _deduplication.MarkSuccessAsync(
                            dedupEntry,
                            activeShift.ShiftId,
                            new { activeShift.ShiftId, activeShift.Status });
                    await _otpChallengeRepo.CommitTransactionAsync();
                    await PublishNotificationSafeAsync(activeShift, "RECONCILIATION_REQUIRED");

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
                _logger.LogError(ex, "WORKSHIFT_EXCEPTION_CLOSE_FAILED | ShiftId={ShiftId}", shiftId);
                return ex is WorkShiftBusinessException business
                    ? ServiceResult.Failure(business.Message, errorCode: business.ErrorCode)
                    : ServiceResult.Failure("Không thể đóng phiên POS ngoại lệ. Vui lòng thử lại.");
            }
        }

        public async Task<ServiceResult> StartClosingAsync(
            int userId,
            int storeId,
            int shiftId,
            StartClosingRequestDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.RequestKey))
                return ServiceResult.Failure("RequestKey là bắt buộc.", errorCode: WorkShiftErrorCodes.InvalidRequestKey);

            await _otpChallengeRepo.BeginTransactionAsync();
            try
            {
                Models.Systems.RequestDeduplication? dedupEntry = null;
                if (_deduplication != null)
                {
                    var begin = await _deduplication.BeginScopedAsync(request.RequestKey, "POS.WORKSHIFT.START_CLOSING",
                        userId, request, shiftId, storeId, null);
                    if (!begin.CanProcess)
                    {
                        await _otpChallengeRepo.RollbackTransactionAsync();
                        return string.Equals(begin.Status, "SUCCESS", StringComparison.Ordinal)
                            ? ServiceResult.Success("Phiên POS đã bắt đầu chốt trước đó.")
                            : ServiceResult.Failure(begin.ErrorMessage ?? "RequestKey đã được sử dụng.",
                                errorCode: begin.ErrorCode == "IDEMPOTENCY_KEY_REUSED"
                                    ? WorkShiftErrorCodes.DuplicateRequest : begin.ErrorCode);
                    }
                    dedupEntry = begin.Entry;
                }

                var shift = await _shiftRepo.GetActiveShiftAsync(userId, storeId);
                if (shift == null || shift.ShiftId != shiftId)
                    throw new WorkShiftBusinessException(WorkShiftErrorCodes.WorkShiftNotOpen, "Không tìm thấy phiên POS cần chốt.");
                if (shift.Status != WorkShiftStatuses.Closing)
                {
                    if (shift.Status != WorkShiftStatuses.Open && shift.Status != WorkShiftStatuses.ExpiredPendingClose && shift.Status != "Open")
                        throw new WorkShiftBusinessException(WorkShiftErrorCodes.WorkShiftPendingClose, "Phiên POS không thể bắt đầu chốt.");
                    if (!MatchesRequestRowVersion(shift, request.RowVersion))
                        throw new WorkShiftBusinessException(WorkShiftErrorCodes.ConcurrencyConflict, "Dữ liệu phiên POS đã thay đổi.");
                    shift.Status = WorkShiftStatuses.Closing;
                    shift.ClosingStartedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
                    await _shiftRepo.UpdateShiftAsync(shift);
                    if (_audit != null)
                        await _audit.WriteAsync("WORKSHIFT_CLOSING", shiftId, userId,
                            new { Status = WorkShiftStatuses.Open }, new { shift.Status, request.RequestKey });
                }
                if (dedupEntry != null && _deduplication != null)
                    await _deduplication.MarkSuccessAsync(dedupEntry, shiftId, new { shiftId, shift.Status });
                await _otpChallengeRepo.CommitTransactionAsync();
                await PublishNotificationSafeAsync(shift, "CLOSING");
                _logger.LogInformation("WORKSHIFT_CLOSING | ShiftId={ShiftId} StaffId={StaffId} RequestKey={RequestKey}", shiftId, userId, request.RequestKey);
                return ServiceResult.Success("Phiên POS đã khóa giao dịch mới và sẵn sàng chốt két.");
            }
            catch (WorkShiftBusinessException ex)
            {
                await _otpChallengeRepo.RollbackTransactionAsync();
                return ServiceResult.Failure(ex.Message, errorCode: ex.ErrorCode);
            }
            catch (Exception ex)
            {
                await _otpChallengeRepo.RollbackTransactionAsync();
                _logger.LogError(ex, "WORKSHIFT_START_CLOSING_FAILED | ShiftId={ShiftId}", shiftId);
                return ServiceResult.Failure("Không thể bắt đầu chốt phiên POS.");
            }
        }

        public async Task<ServiceResult> ReconcileAsync(
            int userId,
            int storeId,
            int shiftId,
            ReconcileWorkShiftRequestDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.RequestKey))
                return ServiceResult.Failure("RequestKey là bắt buộc.", errorCode: WorkShiftErrorCodes.InvalidRequestKey);
            if (!IsValidReason(request.Reason))
                return ServiceResult.Failure("Lý do đối soát phải có từ 10 đến 500 ký tự.");
            var offlineSummary = request.OfflineQueueSummary ?? new OfflineQueueSummaryDto();
            if (offlineSummary.OfflineOrderCount < 0
                || offlineSummary.EstimatedTotal < 0
                || offlineSummary.LocalCashTotal < 0)
            {
                return ServiceResult.Failure(
                    "Manifest đơn offline không hợp lệ.",
                    errorCode: WorkShiftErrorCodes.OfflineOrdersPending);
            }
            if (offlineSummary.OfflineOrderCount > 0)
            {
                return ServiceResult.Failure(
                    "Vẫn còn đơn offline chưa đồng bộ, chưa thể hoàn tất đối soát.",
                    errorCode: WorkShiftErrorCodes.OfflineOrdersPending);
            }

            await _otpChallengeRepo.BeginTransactionAsync();
            try
            {
                Models.Systems.RequestDeduplication? dedupEntry = null;
                if (_deduplication != null)
                {
                    var begin = await _deduplication.BeginScopedAsync(request.RequestKey, "POS.WORKSHIFT.RECONCILE",
                        userId, request, shiftId, storeId, null);
                    if (!begin.CanProcess)
                    {
                        await _otpChallengeRepo.RollbackTransactionAsync();
                        return string.Equals(begin.Status, "SUCCESS", StringComparison.Ordinal)
                            ? ServiceResult.Success("Phiên POS đã được đối soát trước đó.")
                            : ServiceResult.Failure(begin.ErrorMessage ?? "RequestKey đã được sử dụng.",
                                errorCode: begin.ErrorCode == "IDEMPOTENCY_KEY_REUSED"
                                    ? WorkShiftErrorCodes.DuplicateRequest : begin.ErrorCode);
                    }
                    dedupEntry = begin.Entry;
                }

                var shift = await _shiftRepo.GetShiftByIdAsync(shiftId, userId, storeId);
                if (shift == null || shift.Status != WorkShiftStatuses.ReconciliationRequired)
                    throw new WorkShiftBusinessException(WorkShiftErrorCodes.WorkShiftAlreadyClosed, "Phiên POS không ở trạng thái cần đối soát.");
                if (!MatchesRequestRowVersion(shift, request.RowVersion))
                    throw new WorkShiftBusinessException(WorkShiftErrorCodes.ConcurrencyConflict, "Dữ liệu phiên POS đã thay đổi.");
                if (await _shiftRepo.HasOpenPosPaymentAsync(shiftId, storeId))
                    throw new WorkShiftBusinessException(WorkShiftErrorCodes.PaymentInProgress, "Vẫn còn thanh toán đang xử lý.");

                var offlineOrdersAtExceptionClose = shift.OfflineOrderCountAtClose.GetValueOrDefault();
                if (offlineOrdersAtExceptionClose > shift.LateOfflineSyncCount)
                {
                    throw new WorkShiftBusinessException(
                        WorkShiftErrorCodes.OfflineOrdersPending,
                        $"Còn {offlineOrdersAtExceptionClose - shift.LateOfflineSyncCount} đơn offline chưa được server xác nhận đồng bộ.");
                }

                var totalCash = await _shiftRepo.GetTotalCashSalesAsync(shiftId);
                shift.ExpectedEndingCash = shift.StartingCash + totalCash;
                shift.CashDiscrepancy = shift.ActualEndingCash.HasValue ? shift.ActualEndingCash.Value - shift.ExpectedEndingCash : null;
                shift.RequiresReconciliation = false;
                shift.Status = WorkShiftStatuses.Closed;
                shift.CloseReason = request.Reason.Trim();
                await _shiftRepo.UpdateShiftAsync(shift);
                if (_audit != null)
                    await _audit.WriteAsync("WORKSHIFT_RECONCILED", shiftId, userId,
                        new { Status = WorkShiftStatuses.ReconciliationRequired }, new { shift.Status, request.RequestKey, request.Reason });
                if (dedupEntry != null && _deduplication != null)
                    await _deduplication.MarkSuccessAsync(dedupEntry, shiftId, new { shiftId, shift.Status });
                await _otpChallengeRepo.CommitTransactionAsync();
                await PublishNotificationSafeAsync(shift, "RECONCILED");
                _logger.LogInformation("WORKSHIFT_RECONCILED | ShiftId={ShiftId} StaffId={StaffId} RequestKey={RequestKey}", shiftId, userId, request.RequestKey);
                return ServiceResult.Success("Đối soát phiên POS thành công.");
            }
            catch (WorkShiftBusinessException ex)
            {
                await _otpChallengeRepo.RollbackTransactionAsync();
                return ServiceResult.Failure(ex.Message, errorCode: ex.ErrorCode);
            }
            catch (Exception ex)
            {
                await _otpChallengeRepo.RollbackTransactionAsync();
                _logger.LogError(ex, "WORKSHIFT_RECONCILE_FAILED | ShiftId={ShiftId}", shiftId);
                return ServiceResult.Failure("Không thể đối soát phiên POS.");
            }
        }

        public async Task<ServiceResult> RegisterTerminalAsync(
            int userId,
            int storeId,
            PosTerminalRegisterDto request)
        {
            if (request == null
                || string.IsNullOrWhiteSpace(request.TerminalId)
                || string.IsNullOrWhiteSpace(request.Name)
                || string.IsNullOrWhiteSpace(request.RequestKey))
                return ServiceResult.Failure("Thiếu thông tin đăng ký terminal.");
            if (request.Name.Trim().Length > 100 || request.TerminalId.Trim().Length > 100)
                return ServiceResult.Failure("Tên hoặc mã terminal quá dài.");
            if (request.OtpChallengePublicId == Guid.Empty)
                return ServiceResult.Failure("Cần OTP phê duyệt terminal.", errorCode: WorkShiftErrorCodes.InvalidApproverScope);

            await _otpChallengeRepo.BeginTransactionAsync();
            try
            {
                Models.Systems.RequestDeduplication? dedupEntry = null;
                if (_deduplication != null)
                {
                    var begin = await _deduplication.BeginScopedAsync(
                        request.RequestKey,
                        "POS.TERMINAL.REGISTER",
                        userId,
                        request,
                        null,
                        storeId,
                        null);
                    if (!begin.CanProcess)
                    {
                        await _otpChallengeRepo.RollbackTransactionAsync();
                        return string.Equals(begin.Status, "SUCCESS", StringComparison.Ordinal)
                            ? ServiceResult.Success("Terminal POS đã được đăng ký trước đó.")
                            : ServiceResult.Failure(
                                begin.ErrorMessage ?? "RequestKey đã được sử dụng.",
                                errorCode: begin.ErrorCode == "IDEMPOTENCY_KEY_REUSED"
                                    ? WorkShiftErrorCodes.DuplicateRequest
                                    : begin.ErrorCode);
                    }
                    dedupEntry = begin.Entry;
                }

                var challenge = await _otpChallengeRepo.GetByPublicIdForUpdateAsync(request.OtpChallengePublicId);
                var fingerprint = _otpFingerprint.BuildOpenShiftBoundFingerprint(
                    storeId,
                    userId,
                    0,
                    request.Name.Trim(),
                    $"terminal:{request.TerminalId.Trim()}|request:{request.RequestKey.Trim()}",
                    OtpConstants.ActionTypes.RegisterTerminal,
                    request.TerminalId,
                    request.RequestKey);
                var error = await ValidateAndPrepareOtpConsumeAsync(
                    challenge,
                    OtpConstants.ActionTypes.RegisterTerminal,
                    null,
                    storeId,
                    userId,
                    fingerprint,
                    userId);
                if (error != null)
                {
                    await _otpChallengeRepo.RollbackTransactionAsync();
                    return ServiceResult.Failure(error.Value.message, errorCode: error.Value.code);
                }

                var terminal = await _shiftRepo.RegisterPosTerminalAsync(
                    request.TerminalId.Trim(),
                    storeId,
                    request.Name.Trim());
                challenge!.Status = OtpConstants.Statuses.Used;
                challenge.UsedAt = _timeProvider.GetUtcNow().UtcDateTime;
                challenge.ProtectedOtpPayload = null;
                challenge.TerminalId = terminal.TerminalId;
                challenge.RequestKey = request.RequestKey.Trim();
                await _otpChallengeRepo.SaveChangesAsync();
                if (_audit != null)
                    await _audit.WriteAsync("POS_TERMINAL_REGISTERED", 0, userId, null, new { terminal.TerminalId, terminal.StoreId, terminal.Name, request.RequestKey });
                if (dedupEntry != null && _deduplication != null)
                    await _deduplication.MarkSuccessAsync(
                        dedupEntry,
                        0,
                        new { terminal.TerminalId, terminal.StoreId });
                await _otpChallengeRepo.CommitTransactionAsync();
                return ServiceResult.Success("Terminal POS đã được đăng ký và kích hoạt.");
            }
            catch (Exception ex)
            {
                await _otpChallengeRepo.RollbackTransactionAsync();
                _logger.LogError(ex, "POS_TERMINAL_REGISTER_FAILED | StoreId={StoreId} StaffId={StaffId}", storeId, userId);
                return ex is WorkShiftBusinessException business
                    ? ServiceResult.Failure(business.Message, errorCode: business.ErrorCode)
                    : ServiceResult.Failure("Không thể đăng ký terminal POS.");
            }
        }

        private async Task PublishNotificationSafeAsync(WorkShift shift, string eventType)
        {
            if (_notifications == null) return;

            try
            {
                await _notifications.PublishAsync(new WorkShiftNotificationDto
                {
                    WorkShiftId = shift.ShiftId,
                    StoreId = shift.StoreId,
                    StaffId = shift.UserId,
                    TerminalId = shift.PosTerminalId,
                    EventType = eventType,
                    Status = shift.Status,
                    ServerNowUtc = _timeProvider.GetUtcNow().UtcDateTime,
                    AutoCloseAtUtc = shift.AutoCloseAtUtc,
                    RemainingMinutes = shift.AutoCloseAtUtc.HasValue
                        ? Math.Max(0, (int)Math.Ceiling((shift.AutoCloseAtUtc.Value - _timeProvider.GetUtcNow().UtcDateTime).TotalMinutes))
                        : null
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "WORKSHIFT_NOTIFICATION_FAILED | ShiftId={ShiftId} EventType={EventType}",
                    shift.ShiftId,
                    eventType);
            }
        }

        private static bool MatchesRowVersion(WorkShift shift, string? encoded)
        {
            if (shift.RowVersion == null || shift.RowVersion.Length == 0) return true;
            if (string.IsNullOrWhiteSpace(encoded)) return false;
            try
            {
                return shift.RowVersion.SequenceEqual(Convert.FromBase64String(encoded));
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private bool MatchesRequestRowVersion(WorkShift shift, string? encoded) =>
            _deduplication == null && string.IsNullOrWhiteSpace(encoded)
                ? true
                : MatchesRowVersion(shift, encoded);

        /// <summary>
        /// Validates an approved OTP challenge for consume. Returns null if valid.
        /// </summary>
        private string? ValidateActualEndingCash(decimal actualEndingCash)
        {
            var error = POSCashAmountValidator.Validate(
                actualEndingCash,
                _cashDenominationStep,
                allowZero: true);

            return error == null
                ? null
                : $"Tiền mặt thực tế trong két không hợp lệ. {error}";
        }

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

            if (challenge.ExpiresAt < _timeProvider.GetUtcNow().UtcDateTime)
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

            var approverPermission = expectedActionType switch
            {
                OtpConstants.ActionTypes.OpenShiftLate => PermissionConstants.PosWorkShiftApproveOutsideSchedule,
                OtpConstants.ActionTypes.OpenShiftOutsideSchedule => PermissionConstants.PosWorkShiftApproveOutsideSchedule,
                OtpConstants.ActionTypes.CloseShiftException => PermissionConstants.PosWorkShiftCloseException,
                OtpConstants.ActionTypes.ReconcileWorkShift => PermissionConstants.PosWorkShiftReconcile,
                OtpConstants.ActionTypes.RegisterTerminal => PermissionConstants.PosWorkShiftOverrideTerminal,
                _ => PermissionConstants.PosWorkShiftClose
            };
            var permissionDecision = _permissions == null || challenge.ApproverStaff?.AccountId is not > 0
                ? null
                : await _permissions.HasPermissionAsync(challenge.ApproverStaff.AccountId, approverPermission, storeId);
            var approverOk = permissionDecision != null
                ? permissionDecision.IsSuccess && permissionDecision.Data?.Allowed == true
                : await _otpChallengeRepo.IsApproverStillEligibleAsync(
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
