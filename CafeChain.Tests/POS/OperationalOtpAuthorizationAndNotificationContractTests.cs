using CafeChain.Application.Authorization;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Permissions;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Operations;
using CafeChain.Application.Services.POS;
using CafeChain.Infrastructure.Interfaces.Operations;
using CafeChain.Models.Customers;
using CafeChain.Models.Operations;
using CafeChain.Models.Staffs;
using Microsoft.AspNetCore.DataProtection;
using Moq;

namespace CafeChain.Tests.POS;

public sealed class OperationalOtpAuthorizationAndNotificationContractTests
{
    [Theory]
    [InlineData(OtpConstants.ActionTypes.OpenShiftOutsideSchedule, PermissionConstants.PosWorkShiftApproveOutsideSchedule)]
    [InlineData(OtpConstants.ActionTypes.OpenShiftLate, PermissionConstants.PosWorkShiftApproveOutsideSchedule)]
    [InlineData(OtpConstants.ActionTypes.RegisterTerminal, PermissionConstants.PosWorkShiftOverrideTerminal)]
    [InlineData(OtpConstants.ActionTypes.CashDifference, PermissionConstants.PosWorkShiftClose)]
    [InlineData(OtpConstants.ActionTypes.CloseShiftException, PermissionConstants.PosWorkShiftCloseException)]
    [InlineData(OtpConstants.ActionTypes.ReconcileWorkShift, PermissionConstants.PosWorkShiftReconcile)]
    public void Action_permission_mapping_is_explicit(string actionType, string expectedPermission)
    {
        Assert.True(OperationalOtpAuthorization.TryGetApproverPermission(actionType, out var permission));
        Assert.Equal(expectedPermission, permission);
    }

    [Fact]
    public void Unknown_action_is_denied_without_generic_fallback()
    {
        Assert.False(OperationalOtpAuthorization.TryGetApproverPermission("UNKNOWN_ACTION", out var permission));
        Assert.Equal(string.Empty, permission);
    }

    [Fact]
    public async Task Outside_schedule_otp_reveal_uses_outside_permission_not_terminal_override()
    {
        const int notificationId = 41;
        const int challengeId = 51;
        const int approverStaffId = 61;
        const int accountId = 71;
        const int storeId = 81;
        const string otp = "ABC234";
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(5);
        var publicId = Guid.NewGuid();

        var generator = new Mock<IOtpCodeGenerator>();
        generator.Setup(x => x.NormalizeAndValidate(otp)).Returns(otp);
        var protectedPayload = new OtpProtectedPayloadService(
            new EphemeralDataProtectionProvider(), generator.Object);
        var challenge = new OtpChallenge
        {
            OtpChallengeId = challengeId,
            PublicId = publicId,
            StoreId = storeId,
            RequestedByStaffId = 90,
            ApproverStaffId = approverStaffId,
            ActionType = OtpConstants.ActionTypes.OpenShiftOutsideSchedule,
            TargetType = OtpConstants.TargetTypes.Shifts,
            Status = OtpConstants.Statuses.Pending,
            ExpiresAt = expiresAtUtc,
            CreatedAt = DateTime.UtcNow,
            ProtectedOtpPayload = protectedPayload.Protect(publicId, approverStaffId, otp, expiresAtUtc),
            ApproverStaff = new Staff
            {
                StaffId = approverStaffId,
                AccountId = accountId,
                Active = true,
                Account = new Account { AccountId = accountId, Active = true, Email = "lead@test.local" }
            }
        };
        var notification = new StaffNotification
        {
            StaffNotificationId = notificationId,
            StoreId = storeId,
            RecipientStaffId = approverStaffId,
            Type = StaffNotificationTypes.OperationalOtpRequest,
            EntityType = StaffNotificationEntityTypes.OtpChallenge,
            EntityId = challengeId,
            OtpChallengeId = challengeId
        };
        var repository = new Mock<IStaffNotificationRepository>();
        repository.Setup(x => x.GetAsync(
                approverStaffId, notificationId, It.IsAny<IReadOnlyCollection<int>?>(), false))
            .ReturnsAsync(notification);
        repository.Setup(x => x.GetOtpChallengesAsync(
                approverStaffId, It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([challenge]);
        var permissions = new Mock<IAdminPermissionService>();
        permissions.Setup(x => x.HasPermissionAsync(
                accountId, PermissionConstants.PosWorkShiftApproveOutsideSchedule, storeId))
            .ReturnsAsync(ServiceResult<PermissionDecisionDto>.Success(new PermissionDecisionDto
            {
                AccountId = accountId,
                PermissionCode = PermissionConstants.PosWorkShiftApproveOutsideSchedule,
                TargetStoreId = storeId,
                Allowed = true,
                ScopeAllowed = true
            }));
        var service = new TerminalRegistrationNotificationService(
            repository.Object,
            protectedPayload,
            Mock.Of<IWorkShiftService>(),
            permissions.Object);

        var result = await service.RevealOperationalOtpAsync(
            approverStaffId, notificationId, [storeId]);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(otp, result.Data!.Code);
        permissions.Verify(x => x.HasPermissionAsync(
            accountId, PermissionConstants.PosWorkShiftApproveOutsideSchedule, storeId), Times.Once);
        permissions.Verify(x => x.HasPermissionAsync(
            It.IsAny<int>(), PermissionConstants.PosWorkShiftOverrideTerminal, It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public async Task Reveal_denies_wrong_recipient_before_permission_or_unprotect()
    {
        var repository = new Mock<IStaffNotificationRepository>();
        repository.Setup(x => x.GetAsync(
                501, 601, It.IsAny<IReadOnlyCollection<int>?>(), false))
            .ReturnsAsync((StaffNotification?)null);
        var permissions = new Mock<IAdminPermissionService>();
        var protectedPayload = new Mock<IOtpProtectedPayloadService>();
        var service = new TerminalRegistrationNotificationService(
            repository.Object,
            protectedPayload.Object,
            Mock.Of<IWorkShiftService>(),
            permissions.Object);

        var result = await service.RevealOperationalOtpAsync(501, 601, [701]);

        Assert.False(result.IsSuccess);
        Assert.Equal(OtpConstants.ErrorCodes.ContextMismatch, result.ErrorCode);
        permissions.Verify(x => x.HasPermissionAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int?>()), Times.Never);
        protectedPayload.Verify(x => x.TryUnprotect(
            It.IsAny<string?>(), It.IsAny<Guid>(), It.IsAny<int>(),
            It.IsAny<DateTime>(), It.IsAny<DateTime>(), out It.Ref<string>.IsAny), Times.Never);
    }

    [Fact]
    public void Generic_reveal_routes_and_typed_authorization_are_present()
    {
        var service = Read("CafeChain", "Application", "Services", "Operations", "TerminalRegistrationNotificationService.cs");
        var admin = Read("CafeChain", "Areas", "Admin", "Controllers", "AdminNotificationsController.cs");
        var api = Read("CafeChain", "Controllers", "Api", "v1", "POSNotificationsController.cs");

        Assert.Contains("RevealOperationalOtpAsync", service, StringComparison.Ordinal);
        Assert.Contains("StaffNotificationEntityTypes.OtpChallenge", service, StringComparison.Ordinal);
        Assert.Contains("OperationalOtpAuthorization.TryGetApproverPermission", service, StringComparison.Ordinal);
        Assert.Contains("HasPermissionAsync", service, StringComparison.Ordinal);
        Assert.Contains("RevealOperationalOtp", admin, StringComparison.Ordinal);
        Assert.Contains("RevealTerminalOtp", admin, StringComparison.Ordinal);
        Assert.Contains("notifications/{id:int}/operational-otp", api, StringComparison.Ordinal);
        Assert.Contains("notifications/{id:int}/terminal-otp", api, StringComparison.Ordinal);
    }

    [Fact]
    public void Notification_ui_uses_backend_capability_flags_and_rebuilds_terminal_form()
    {
        var view = Read("CafeChain", "Areas", "Admin", "Views", "AdminNotifications", "Index.cshtml");
        var script = Read("CafeChain", "wwwroot", "js", "Admin", "Notifications", "admin-notification-list.js");

        Assert.Contains("otp.CanRevealOtp", view, StringComparison.Ordinal);
        Assert.Contains("otp.CanContinueTerminalConfirmation", view, StringComparison.Ordinal);
        Assert.Contains("otp.CanRejectTerminalRegistration", view, StringComparison.Ordinal);
        Assert.Contains("data-reveal-operational-otp", view, StringComparison.Ordinal);
        Assert.Contains("createTerminalConfirmationForm", script, StringComparison.Ordinal);
        Assert.Contains("otp.canRevealOtp === true", script, StringComparison.Ordinal);
        Assert.Contains("otp.canContinueTerminalConfirmation === true", script, StringComparison.Ordinal);
        Assert.Contains("otp.canRejectTerminalRegistration === true", script, StringComparison.Ordinal);
        Assert.Contains("createTerminalRejectionForm", script, StringComparison.Ordinal);
        Assert.Contains("/Admin/AdminNotifications/RejectTerminal", script, StringComparison.Ordinal);
        Assert.Contains(".notification-terminal-reject-form [data-reject-submit-once]", script, StringComparison.Ordinal);
        Assert.Contains("form.dataset.rejectPending", script, StringComparison.Ordinal);
        Assert.Contains("type=\"button\" class=\"cc-button notification-terminal-reject-button\"", view, StringComparison.Ordinal);
        Assert.Contains("__RequestVerificationToken", script, StringComparison.Ordinal);
        Assert.Contains("/Admin/AdminNotifications/RevealOperationalOtp", script, StringComparison.Ordinal);
        Assert.Contains("VerificationCodeInput.setValue(otpInput, payload.data.code)", script, StringComparison.Ordinal);
        Assert.Contains("form.noValidate = true", script, StringComparison.Ordinal);
        Assert.Contains("form.dataset.validationFeedback = \"inline\"", script, StringComparison.Ordinal);
        Assert.Contains("data-terminal-confirm-feedback", view, StringComparison.Ordinal);
        Assert.Contains("data-validation-feedback=\"inline\"", view, StringComparison.Ordinal);
        Assert.Contains("novalidate", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-reveal-terminal-otp", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Staffhub_and_guides_use_final_terminal_confirmation_copy()
    {
        var view = Read("CafeChain", "Views", "StaffHub", "Index.cshtml");
        var script = Read("CafeChain", "wwwroot", "js", "StaffHub", "staffhub-schedule.js");
        var guide = Read("CafeChain", "Doc", "POS_TERMINAL_USER_GUIDE.md");
        var flows = Read("CafeChain", "Doc", "STAFFHUB_USER_BUSINESS_FLOWS.md");

        Assert.Contains("Gửi yêu cầu xác nhận Terminal", view, StringComparison.Ordinal);
        Assert.Contains("Gửi yêu cầu xác nhận Terminal", script, StringComparison.Ordinal);
        Assert.Contains("notifyTerminalResolutionAndReload", script, StringComparison.Ordinal);
        Assert.Contains("window.location.reload()", script, StringComparison.Ordinal);
        Assert.Contains("Xem OTP", guide, StringComparison.Ordinal);
        Assert.Contains("Xác nhận Terminal", guide, StringComparison.Ordinal);
        Assert.Contains("shiftsupervisor@cafechain.vn", flows, StringComparison.Ordinal);
        Assert.Contains("NO_ELIGIBLE_APPROVER", flows, StringComparison.Ordinal);
        Assert.Contains("trên 30 phút", flows, StringComparison.Ordinal);
        Assert.Contains("không dùng OTP", flows, StringComparison.Ordinal);
    }

    private static string Read(params string[] path) =>
        File.ReadAllText(Path.Combine([RepositoryRoot(), .. path]));

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "CafeChain"))
                && Directory.Exists(Path.Combine(current.FullName, "CafeChain.Tests")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Không tìm thấy repository root.");
    }
}
