using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Permissions;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.Operations;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Options;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Admin.Staffs;
using CafeChain.Infrastructure.Interfaces.Admin.Staffs;
using CafeChain.Infrastructure.Interfaces.Operations;
using CafeChain.Models.Customers;
using CafeChain.Models.Operations;
using CafeChain.Models.Staffs;
using Microsoft.Extensions.Options;
using Moq;

namespace CafeChain.Tests;

public sealed class StaffScheduleGapNotificationServiceTests
{
    [Fact]
    public async Task ScanStoreAsync_DetectsGapAndListsOnlyEligibleCandidate()
    {
        var date = new DateTime(2026, 7, 30);
        var repository = BuildRepository(date, includeTimeOff: false);
        var (service, delivery) = BuildService(repository.Object, canAccessStore: true);
        InventoryNotificationDeliveryRequest? request = null;
        delivery
            .Setup(x => x.DeliverAsync(
                It.IsAny<InventoryNotificationDeliveryRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<InventoryNotificationDeliveryRequest, CancellationToken>(
                (value, _) => request = value)
            .ReturnsAsync(new InventoryNotificationDeliveryResult(1, 0, 0, true, []));

        var result = await service.ScanStoreAsync(1, date, date);

        Assert.Equal(1, result.MissingRequirementCount);
        Assert.Equal(1, result.AlertsCreated);
        Assert.NotNull(request);
        Assert.Equal(StaffScheduleNotificationTypes.Gap, request.Type);
        Assert.Contains("thiếu 2 người", request.Body, StringComparison.Ordinal);
        Assert.Contains("Nhân viên A", request.Body, StringComparison.Ordinal);
        Assert.Equal(24 * 60, request.CooldownMinutes);
        Assert.Equal([90], request.RecipientStaffIds);
        Assert.Contains(":20260730", request.DeduplicationKey, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScanStoreAsync_ExcludesApprovedTimeOffCandidate()
    {
        var date = new DateTime(2026, 7, 30);
        var repository = BuildRepository(date, includeTimeOff: true);
        var (service, delivery) = BuildService(repository.Object, canAccessStore: true);
        InventoryNotificationDeliveryRequest? request = null;
        delivery
            .Setup(x => x.DeliverAsync(
                It.IsAny<InventoryNotificationDeliveryRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<InventoryNotificationDeliveryRequest, CancellationToken>(
                (value, _) => request = value)
            .ReturnsAsync(new InventoryNotificationDeliveryResult(1, 0, 0, true, []));

        await service.ScanStoreAsync(1, date, date);

        Assert.NotNull(request);
        Assert.Contains(
            "Chưa có nhân viên đủ điều kiện để đề xuất.",
            request.Body,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Nhân viên A", request.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScanStoreAsync_DoesNotNotifyRecipientOutsideStoreScope()
    {
        var date = new DateTime(2026, 7, 30);
        var repository = BuildRepository(date, includeTimeOff: false);
        var (service, delivery) = BuildService(repository.Object, canAccessStore: false);

        var result = await service.ScanStoreAsync(1, date, date);

        Assert.Equal(0, result.MissingRequirementCount);
        delivery.Verify(
            x => x.DeliverAsync(
                It.IsAny<InventoryNotificationDeliveryRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ScanStoreAsync_ResolvesExistingNotificationWhenRequirementIsFilled()
    {
        var date = new DateTime(2026, 7, 30);
        var repository = BuildRepository(date, includeTimeOff: false, targetStaff: 0);
        var (service, delivery) = BuildService(repository.Object, canAccessStore: true);
        const string key = "90:STAFF_SCHEDULE_GAP:1:51:20260730";

        var notificationRepository = Mock.Get(
            GetPrivateField<IInventoryReorderNotificationRepository>(
                service,
                "_notificationRepository"));
        notificationRepository
            .Setup(x => x.GetActiveForStoreAsync(1, StaffScheduleNotificationTypes.Gap))
            .ReturnsAsync([
                new StaffNotification
                {
                    StoreId = 1,
                    Type = StaffScheduleNotificationTypes.Gap,
                    DeduplicationKey = key
                }
            ]);
        delivery
            .Setup(x => x.ResolveByDeduplicationKeyAsync(
                key,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InventoryNotificationDeliveryResult(0, 0, 1, true, []));

        var result = await service.ScanStoreAsync(1, date, date);

        Assert.Equal(1, result.AlertsResolved);
        delivery.Verify(
            x => x.ResolveByDeduplicationKeyAsync(
                key,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static Mock<IShiftOptimizationRepository> BuildRepository(
        DateTime date,
        bool includeTimeOff,
        int targetStaff = 2)
    {
        var account = new Account
        {
            AccountId = 10,
            Email = "employee@cafechain.vn",
            PasswordHash = "test",
            Active = true,
            AccountRoles = []
        };
        var staff = new Staff
        {
            StaffId = 10,
            AccountId = 10,
            StoreId = 1,
            FullName = "Nhân viên A",
            Active = true,
            Account = account
        };
        var shift = new Shift
        {
            ShiftId = 7,
            StoreId = 1,
            Name = "Ca sáng",
            StartTime = TimeSpan.FromHours(8),
            EndTime = TimeSpan.FromHours(16),
            Active = true
        };

        var repository = new Mock<IShiftOptimizationRepository>();
        repository.Setup(x => x.GetStaffsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([staff]);
        repository.Setup(x => x.GetShiftsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([shift]);
        repository.Setup(x => x.GetSchedulesAsync(
                1, date, date, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        repository.Setup(x => x.GetAvailabilityAsync(
                1, date, date, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new StaffAvailabilityRule
                {
                    StaffId = staff.StaffId,
                    DayOfWeek = date.DayOfWeek,
                    StartTime = TimeSpan.FromHours(7),
                    EndTime = TimeSpan.FromHours(17),
                    EffectiveFrom = date.AddDays(-1)
                }
            ]);
        repository.Setup(x => x.GetExceptionsAsync(
                1, date, date, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        repository.Setup(x => x.GetTimeOffsAsync(
                1, date, date.AddDays(1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(includeTimeOff
                ? [
                    new StaffTimeOff
                    {
                        StaffId = staff.StaffId,
                        FromUtc = date.AddHours(7),
                        ToUtc = date.AddHours(17),
                        Status = "APPROVED"
                    }
                ]
                : []);
        repository.Setup(x => x.GetConstraintsAsync(
                1, date, date, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new StaffWorkConstraint
                {
                    StaffId = staff.StaffId,
                    EffectiveFrom = date.AddDays(-1),
                    MaxDailyHours = 8,
                    MaxWeeklyHours = 40,
                    MinimumRestMinutes = 480
                }
            ]);
        repository.Setup(x => x.GetRequirementsAsync(
                1, date, date, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new StoreStaffingRequirement
                {
                    StoreStaffingRequirementId = 51,
                    StoreId = 1,
                    ShiftId = shift.ShiftId,
                    DayOfWeek = date.DayOfWeek,
                    MinimumStaff = 1,
                    TargetStaff = targetStaff,
                    MaximumStaff = 3,
                    EffectiveFrom = date.AddDays(-1),
                    Active = true
                }
            ]);
        repository.Setup(x => x.GetStoreNameAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync("CafeChain Thủ Dầu Một");
        return repository;
    }

    private static (
        StaffScheduleGapNotificationService Service,
        Mock<IInventoryNotificationDeliveryService> Delivery)
        BuildService(IShiftOptimizationRepository repository, bool canAccessStore)
    {
        var delivery = new Mock<IInventoryNotificationDeliveryService>();
        var notificationRepository = new Mock<IInventoryReorderNotificationRepository>();
        notificationRepository.Setup(x => x.GetRecipientCandidatesAsync())
            .ReturnsAsync([
                new ReorderNotificationRecipientRow(
                    90,
                    900,
                    [RoleConstants.StoreManager])
            ]);
        notificationRepository
            .Setup(x => x.GetActiveForStoreAsync(1, StaffScheduleNotificationTypes.Gap))
            .ReturnsAsync([]);

        var scope = new Mock<IScopeAuthorizationService>();
        scope.Setup(x => x.CanAccessStoreAsync(90, 1)).ReturnsAsync(canAccessStore);

        var permission = new Mock<IAdminPermissionService>();
        permission.Setup(x => x.HasPermissionAsync(
                900,
                It.IsAny<string>(),
                1))
            .ReturnsAsync(ServiceResult<PermissionDecisionDto>.Success(
                new PermissionDecisionDto
                {
                    AccountId = 900,
                    TargetStoreId = 1,
                    Allowed = true
                }));

        var service = new StaffScheduleGapNotificationService(
            repository,
            delivery.Object,
            notificationRepository.Object,
            scope.Object,
            permission.Object,
            Options.Create(new StaffScheduleGapNotificationOptions
            {
                ReminderCooldownHours = 24,
                MaximumCandidatesPerAlert = 10
            }));
        return (service, delivery);
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.NonPublic);
        return (T)(field?.GetValue(instance)
            ?? throw new InvalidOperationException($"Không tìm thấy field {fieldName}."));
    }
}
