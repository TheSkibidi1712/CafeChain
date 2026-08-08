using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Accounts;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Services.POS;
using CafeChain.Application.Services.Operations;
using CafeChain.Infrastructure.Realtime;
using CafeChain.Infrastructure.Repositories.Admin.POS;
using CafeChain.Infrastructure.Repositories.Operations;
using CafeChain.Models.Customers;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace CafeChain.Tests.POS;

public sealed class OperationalOtpDualChannelTests : IntegrationTestBase
{
    [Fact]
    public async Task Request_sends_email_and_only_publishes_sanitized_utc_notification_events()
    {
        const string otp = "ABC234";
        await SeedApproversAsync();
        using var context = CreateDbContext();

        string? codeUsedByEmail = null;
        var realtimeEvents = new List<(int Recipient, OperationalOtpNotificationChangedDto Message)>();

        var email = new Mock<IEmailService>();
        email.Setup(x => x.BuildOperationalOtpEmail(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTime>(), It.IsAny<int>()))
            .Callback<string, string, string, string, string, string, DateTime, int>(
                (code, _, _, _, _, _, _, _) => codeUsedByEmail = code)
            .Returns("<html>safe email</html>");
        email.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var codeGenerator = new Mock<IOtpCodeGenerator>();
        codeGenerator.Setup(x => x.Generate()).Returns(otp);
        codeGenerator.Setup(x => x.NormalizeAndValidate(otp)).Returns(otp);

        var publisher = new Mock<IOperationalOtpNotificationPublisher>();
        publisher.Setup(x => x.PublishChangedAsync(
                It.IsAny<int>(), It.IsAny<OperationalOtpNotificationChangedDto>(), It.IsAny<CancellationToken>()))
            .Callback<int, OperationalOtpNotificationChangedDto, CancellationToken>((recipient, message, _) =>
                realtimeEvents.Add((recipient, message)))
            .Returns(Task.CompletedTask);

        var environment = new Mock<IWebHostEnvironment>();
        environment.Setup(x => x.EnvironmentName).Returns("Test");
        var protectedPayload = new OtpProtectedPayloadService(
            new EphemeralDataProtectionProvider(),
            codeGenerator.Object);
        var service = new OtpApprovalService(
            new OtpChallengeRepository(context),
            new WorkShiftRepository(context),
            email.Object,
            codeGenerator.Object,
            new OtpPayloadFingerprintService(),
            Mock.Of<ILogger<OtpApprovalService>>(),
            environment.Object,
            staffNotifications: new StaffNotificationRepository(context),
            otpNotificationPublisher: publisher.Object,
            otpProtectedPayload: protectedPayload);

        var result = await service.RequestOtpAsync(new OtpRequestDto
        {
            ActionType = OtpConstants.ActionTypes.OpenShiftOutsideSchedule,
            TargetType = OtpConstants.TargetTypes.Shifts,
            Reason = "Hỗ trợ cửa hàng theo điều phối của quản lý",
            StartingCash = 0,
            TerminalId = "TEST-TERMINAL",
            RequestKey = Guid.NewGuid().ToString("N")
        }, requestedByStaffId: 100, storeId: 1000);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(otp, codeUsedByEmail);
        Assert.Contains(realtimeEvents, x => x.Recipient == 300 && x.Message.ChangeKind == "Created");
        Assert.Contains(realtimeEvents, x => x.Recipient == 300 && x.Message.ChangeKind == "DeliveryUpdated");
        Assert.All(realtimeEvents, x => Assert.Equal(DateTimeKind.Utc, x.Message.OccurredAtUtc.Kind));

        var stored = await context.StaffNotifications.SingleAsync();
        Assert.Equal(StaffNotificationTypes.OperationalOtpRequest, stored.Type);
        Assert.Equal(300, stored.RecipientStaffId);
        Assert.DoesNotContain(otp, stored.Title, StringComparison.Ordinal);
        Assert.DoesNotContain(otp, stored.Body, StringComparison.Ordinal);
        Assert.NotNull(stored.OtpChallengeId);
        Assert.True(stored.EmailSent);
        var challenge = await context.OtpChallenges.SingleAsync();
        Assert.False(string.IsNullOrWhiteSpace(challenge.ProtectedOtpPayload));
        Assert.DoesNotContain(otp, challenge.ProtectedOtpPayload!, StringComparison.Ordinal);

        var notificationQuery = new StaffNotificationQueryService(
            new StaffNotificationRepository(context),
            protectedPayload);
        var approverList = await notificationQuery.GetListAsync(
            recipientStaffId: 300,
            page: 1,
            pageSize: 20,
            targetUrlChannel: StaffNotificationQueryService.ChannelAdmin);
        Assert.True(approverList.IsSuccess);
        var item = Assert.Single(approverList.Data!.Items);
        Assert.NotNull(item.OperationalOtp);
        Assert.True(item.OperationalOtp!.CanRevealOtp);
        Assert.Equal("Waiting", item.OperationalOtp.Status);
        Assert.InRange(item.OperationalOtp.RemainingSeconds, 1, 300);
        Assert.Equal(DateTimeKind.Utc, item.OperationalOtp.SentAtUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, item.OperationalOtp.ExpiresAtUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, item.OperationalOtp.ServerNowUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, item.CreatedAt.Kind);
        var json = System.Text.Json.JsonSerializer.Serialize(item);
        Assert.Contains("Z", json, StringComparison.Ordinal);
        Assert.DoesNotContain(otp, json, StringComparison.Ordinal);

        var otherApproverList = await notificationQuery.GetListAsync(
            recipientStaffId: 200,
            page: 1,
            pageSize: 20,
            targetUrlChannel: StaffNotificationQueryService.ChannelAdmin);
        Assert.True(otherApproverList.IsSuccess);
        Assert.Empty(otherApproverList.Data!.Items);

        await notificationQuery.MarkReadAsync(300, stored.StaffNotificationId);
        var readList = await notificationQuery.GetListAsync(
            300, 1, 20, StaffNotificationQueryService.ChannelAdmin);
        var readItem = Assert.Single(readList.Data!.Items);
        Assert.NotNull(readItem.OperationalOtp);

        var verified = await service.VerifyOtpAsync(new OtpVerifyDto
        {
            OtpChallengePublicId = result.Data!.OtpChallengePublicId!.Value,
            OtpCode = otp
        });
        Assert.True(verified.IsSuccess, verified.Message);
        Assert.NotNull(stored.ResolvedAt);
        Assert.Null(challenge.ProtectedOtpPayload);
    }

    [Fact]
    public async Task Smtp_failure_keeps_challenge_and_internal_notification_available()
    {
        const string otp = "ABC234";
        await SeedApproversAsync();
        using var context = CreateDbContext();
        var email = new Mock<IEmailService>();
        email.Setup(x => x.BuildOperationalOtpEmail(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTime>(), It.IsAny<int>()))
            .Returns("<html>internal fallback remains available</html>");
        email.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("SMTP unavailable"));
        var generator = new Mock<IOtpCodeGenerator>();
        generator.Setup(x => x.Generate()).Returns(otp);
        generator.Setup(x => x.NormalizeAndValidate(otp)).Returns(otp);
        var environment = new Mock<IWebHostEnvironment>();
        environment.Setup(x => x.EnvironmentName).Returns("Test");
        var protectedPayload = new OtpProtectedPayloadService(
            new EphemeralDataProtectionProvider(), generator.Object);
        var service = new OtpApprovalService(
            new OtpChallengeRepository(context),
            new WorkShiftRepository(context),
            email.Object,
            generator.Object,
            new OtpPayloadFingerprintService(),
            Mock.Of<ILogger<OtpApprovalService>>(),
            environment.Object,
            staffNotifications: new StaffNotificationRepository(context),
            otpProtectedPayload: protectedPayload);

        var result = await service.RequestOtpAsync(new OtpRequestDto
        {
            ActionType = OtpConstants.ActionTypes.OpenShiftOutsideSchedule,
            TargetType = OtpConstants.TargetTypes.Shifts,
            Reason = "Hỗ trợ cửa hàng theo điều phối của quản lý",
            TerminalId = "TEST-TERMINAL",
            RequestKey = Guid.NewGuid().ToString("N")
        }, requestedByStaffId: 100, storeId: 1000);

        Assert.True(result.IsSuccess, result.Message);
        var challenge = await context.OtpChallenges.SingleAsync();
        var notification = await context.StaffNotifications.SingleAsync();
        Assert.Equal(OtpConstants.Statuses.Pending, challenge.Status);
        Assert.False(string.IsNullOrWhiteSpace(challenge.ProtectedOtpPayload));
        Assert.True(notification.EmailAttempted);
        Assert.False(notification.EmailSent);
        Assert.Equal(challenge.OtpChallengeId, notification.OtpChallengeId);
    }

    [Fact]
    public async Task Requester_can_cancel_pending_terminal_registration_and_realtime_is_sanitized()
    {
        const string otp = "ABC234";
        await SeedApproversAsync();
        using var context = CreateDbContext();
        var email = new Mock<IEmailService>();
        email.Setup(x => x.BuildOperationalOtpEmail(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTime>(), It.IsAny<int>()))
            .Returns("<html>safe</html>");
        email.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        var generator = new Mock<IOtpCodeGenerator>();
        generator.Setup(x => x.Generate()).Returns(otp);
        generator.Setup(x => x.NormalizeAndValidate(otp)).Returns(otp);
        var publisher = new Mock<IOperationalOtpNotificationPublisher>();
        publisher.Setup(x => x.PublishChangedAsync(
                It.IsAny<int>(), It.IsAny<OperationalOtpNotificationChangedDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        TerminalRegistrationChangedDto? requesterEvent = null;
        publisher.Setup(x => x.PublishTerminalRegistrationChangedAsync(
                100, It.IsAny<TerminalRegistrationChangedDto>(), It.IsAny<CancellationToken>()))
            .Callback<int, TerminalRegistrationChangedDto, CancellationToken>((_, value, _) => requesterEvent = value)
            .Returns(Task.CompletedTask);
        var environment = new Mock<IWebHostEnvironment>();
        environment.Setup(x => x.EnvironmentName).Returns("Test");
        var protectedPayload = new OtpProtectedPayloadService(
            new EphemeralDataProtectionProvider(), generator.Object);
        var service = new OtpApprovalService(
            new OtpChallengeRepository(context),
            new WorkShiftRepository(context),
            email.Object,
            generator.Object,
            new OtpPayloadFingerprintService(),
            Mock.Of<ILogger<OtpApprovalService>>(),
            environment.Object,
            staffNotifications: new StaffNotificationRepository(context),
            otpNotificationPublisher: publisher.Object,
            otpProtectedPayload: protectedPayload);
        var terminalId = Guid.NewGuid().ToString();
        var requested = await service.RequestOtpAsync(new OtpRequestDto
        {
            ActionType = OtpConstants.ActionTypes.RegisterTerminal,
            TargetType = OtpConstants.TargetTypes.Shifts,
            Reason = "Quầy bán hàng tầng một",
            TerminalId = terminalId,
            TerminalName = "Quầy tầng một",
            RequestKey = Guid.NewGuid().ToString("N")
        }, 100, 1000);
        Assert.True(requested.IsSuccess, requested.Message);

        var cancelled = await service.CancelTerminalRegistrationOtpAsync(
            new OtpCancelDto { OtpChallengePublicId = requested.Data!.OtpChallengePublicId!.Value },
            100,
            1000);

        Assert.True(cancelled.IsSuccess, cancelled.Message);
        var challenge = await context.OtpChallenges.SingleAsync();
        var notification = await context.StaffNotifications.SingleAsync();
        Assert.Equal(OtpConstants.Statuses.Cancelled, challenge.Status);
        Assert.NotNull(challenge.CancelledAt);
        Assert.Null(challenge.ProtectedOtpPayload);
        Assert.NotNull(notification.ResolvedAt);
        Assert.Equal(OtpConstants.Statuses.Cancelled, requesterEvent?.Status);
        Assert.Equal(terminalId, requesterEvent?.TerminalId);
    }

    [Fact]
    public void Private_group_is_identity_scoped_and_not_store_scoped()
    {
        Assert.Equal(
            "staff:15:operational-notifications",
            CafeChain.Hubs.InventoryNotificationGroups.ForStaff(15));
        Assert.NotEqual(
            CafeChain.Hubs.InventoryNotificationGroups.ForStore(1),
            CafeChain.Hubs.InventoryNotificationGroups.ForStaff(15));
    }

    private async Task SeedApproversAsync()
    {
        using var context = CreateDbContext();
        context.Stores.Add(new Store
        {
            StoreId = 1000,
            Name = "Chi nhánh OTP",
            Active = true,
            CreatedAt = DateTime.UtcNow
        });

        AddStaff(context, 100, "sales@test.local", "Nhân viên bán hàng", roleId: 4);
        AddStaff(context, 200, "manager@test.local", "Quản lý chi nhánh", roleId: 3);
        AddStaff(context, 300, "supervisor@test.local", "Ca trưởng", roleId: 8);
        await context.SaveChangesAsync();
    }

    private static void AddStaff(
        CafeChain.Data.AppDbContext context,
        int id,
        string email,
        string fullName,
        int roleId)
    {
        context.Accounts.Add(new Account
        {
            AccountId = id,
            Email = email,
            PasswordHash = "test",
            Active = true,
            CreatedAt = DateTime.UtcNow
        });
        context.Staffs.Add(new Staff
        {
            StaffId = id,
            AccountId = id,
            StoreId = 1000,
            FullName = fullName,
            Active = true,
            CreatedAt = DateTime.UtcNow
        });
        context.AccountRoles.Add(new AccountRole { AccountId = id, RoleId = roleId });
    }
}
