using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Services.Inventories;
using CafeChain.Models.Customers;
using CafeChain.Models.Inventories.Ice;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CafeChain.Tests;

public sealed class OperationalIceWorkShiftLinkHardeningTests : IntegrationTestBase
{
    private const int StoreId = 29101;
    private const int OtherStoreId = 29102;
    private const int ManagerStaffId = 29110;
    private static readonly DateTime BusinessDate = new(2026, 8, 2);

    [Fact]
    public async Task WorkShiftCandidate_RequiresSameStoreAndTimeOverlap()
    {
        using var context = CreateDbContext();
        var shift = SeedOperationalShift(context, StoreId, Local(8), Local(16));
        var valid = SeedWorkShift(context, 29120, StoreId, Local(9), Local(15));
        SeedWorkShift(context, 29121, OtherStoreId, Local(9), Local(15));
        SeedWorkShift(context, 29122, StoreId, Local(16), Local(17));
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetWorkShiftSuggestionsAsync(shift.OperationalShiftId, ManagerActor());

        Assert.True(result.IsSuccess, result.Message);
        Assert.Single(result.Data);
        Assert.Equal(valid.ShiftId, result.Data[0].WorkShiftId);
    }

    [Fact]
    public async Task WorkShiftCandidate_AllowsOpenShiftWithNullClosedAt()
    {
        using var context = CreateDbContext();
        var shift = SeedOperationalShift(context, StoreId, Local(8), Local(16));
        var openWorkShift = SeedWorkShift(context, 29123, StoreId, Local(9), null, "Open");
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetWorkShiftSuggestionsAsync(shift.OperationalShiftId, ManagerActor());

        Assert.True(result.IsSuccess, result.Message);
        Assert.Contains(result.Data, item => item.WorkShiftId == openWorkShift.ShiftId);
    }

    [Fact]
    public async Task WorkShiftCandidate_ExcludesCancelledAndConflictingLink()
    {
        using var context = CreateDbContext();
        var current = SeedOperationalShift(context, StoreId, Local(8), Local(16));
        var other = SeedOperationalShift(context, StoreId, Local(8), Local(16), "Ca khác");
        SeedWorkShift(context, 29124, StoreId, Local(9), Local(10), "Cancelled");
        var linked = SeedWorkShift(context, 29125, StoreId, Local(10), Local(11));
        await context.SaveChangesAsync();
        context.OperationalShiftWorkShifts.Add(NewLink(other.OperationalShiftId, linked.ShiftId));
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetWorkShiftSuggestionsAsync(current.OperationalShiftId, ManagerActor());

        Assert.True(result.IsSuccess, result.Message);
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task WorkShiftCandidate_HandlesOvernightOverlap()
    {
        using var context = CreateDbContext();
        var shift = SeedOperationalShift(context, StoreId, Local(22), LocalNextDay(6));
        var overnight = SeedWorkShift(context, 29126, StoreId, Local(23), LocalNextDay(2));
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetWorkShiftSuggestionsAsync(shift.OperationalShiftId, ManagerActor());

        Assert.True(result.IsSuccess, result.Message);
        Assert.Contains(result.Data, item => item.WorkShiftId == overnight.ShiftId);
    }

    [Fact]
    public async Task LinkWorkShift_SucceedsPersistsAuditAndIsIdempotent()
    {
        using var context = CreateDbContext();
        var shift = SeedOperationalShift(context, StoreId, Local(8), Local(16));
        var workShift = SeedWorkShift(context, 29127, StoreId, Local(9), null, "Open");
        await context.SaveChangesAsync();
        var service = CreateService(context);
        var request = new LinkOperationalWorkShiftsRequest
        {
            OperationalShiftId = shift.OperationalShiftId,
            WorkShiftIds = [workShift.ShiftId]
        };

        var first = await service.LinkWorkShiftsAsync(request, ManagerActor());
        var replay = await service.LinkWorkShiftsAsync(request, ManagerActor());

        Assert.True(first.IsSuccess, first.Message);
        Assert.True(replay.IsSuccess, replay.Message);
        Assert.Contains("đã được liên kết", replay.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(await context.OperationalShiftWorkShifts.AsNoTracking().ToListAsync());
        var audit = Assert.Single(await context.AuditLogs.AsNoTracking()
            .Where(x => x.Action == "LINK_WORKSHIFT")
            .ToListAsync());
        Assert.Contains(workShift.ShiftId.ToString(), audit.NewData, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public async Task LinkWorkShift_BackendRevalidatesStoreOverlapAndOperationalState(
        bool otherStore,
        bool noOverlap,
        bool closedOperationalShift)
    {
        using var context = CreateDbContext();
        var shift = SeedOperationalShift(
            context,
            StoreId,
            Local(8),
            Local(16),
            status: closedOperationalShift ? OperationalIceStatuses.Closed : OperationalIceStatuses.Open);
        var workShift = SeedWorkShift(
            context,
            29128,
            otherStore ? OtherStoreId : StoreId,
            noOverlap ? Local(17) : Local(9),
            noOverlap ? Local(18) : Local(10));
        await context.SaveChangesAsync();

        var result = await CreateService(context).LinkWorkShiftsAsync(
            new LinkOperationalWorkShiftsRequest
            {
                OperationalShiftId = shift.OperationalShiftId,
                WorkShiftIds = [workShift.ShiftId]
            },
            ManagerActor());

        Assert.False(result.IsSuccess);
        Assert.Empty(await context.OperationalShiftWorkShifts.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task LinkWorkShift_RejectsConflictingOperationalShiftInVietnamese()
    {
        using var context = CreateDbContext();
        var first = SeedOperationalShift(context, StoreId, Local(8), Local(16));
        var second = SeedOperationalShift(context, StoreId, Local(8), Local(16), "Ca thứ hai");
        var workShift = SeedWorkShift(context, 29129, StoreId, Local(9), Local(10));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var linked = await service.LinkWorkShiftAsync(
            new LinkOperationalWorkShiftRequest { OperationalShiftId = first.OperationalShiftId, WorkShiftId = workShift.ShiftId },
            ManagerActor());
        var conflict = await service.LinkWorkShiftAsync(
            new LinkOperationalWorkShiftRequest { OperationalShiftId = second.OperationalShiftId, WorkShiftId = workShift.ShiftId },
            ManagerActor());

        Assert.True(linked.IsSuccess, linked.Message);
        Assert.False(conflict.IsSuccess);
        Assert.Equal(OperationalIceErrorCodes.WorkShiftAlreadyLinked, conflict.ErrorCode);
        Assert.Contains("ca đá khác", conflict.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(await context.OperationalShiftWorkShifts.AsNoTracking().ToListAsync());
    }

    [Fact]
    public void LinkWorkShiftRelation_EnforcesUniqueWorkShiftAtDatabaseLevel()
    {
        using var context = CreateDbContext();
        var entity = context.Model.FindEntityType(typeof(OperationalShiftWorkShift));

        Assert.NotNull(entity);
        Assert.Contains(entity!.GetIndexes(), index =>
            index.IsUnique
            && index.Properties.Select(property => property.Name).SequenceEqual([nameof(OperationalShiftWorkShift.WorkShiftId)]));
    }

    [Fact]
    public void LinkRequest_UsesWritableConcreteCollectionForMvcBinding()
    {
        var property = typeof(LinkOperationalWorkShiftsRequest).GetProperty(nameof(LinkOperationalWorkShiftsRequest.WorkShiftIds));

        Assert.NotNull(property);
        Assert.True(property!.CanWrite);
        Assert.Equal(typeof(List<int>), property.PropertyType);
    }

    private static OperationalIceService CreateService(CafeChain.Data.AppDbContext context)
    {
        var scope = new Mock<IScopeAuthorizationService>();
        scope.Setup(x => x.CanAccessStoreAsync(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(true);
        return new OperationalIceService(context, scope.Object);
    }

    private static AdminActorContext ManagerActor() => new()
    {
        StaffId = ManagerStaffId,
        StoreId = StoreId,
        RoleNames = [RoleConstants.StoreManager]
    };

    private static OperationalShift SeedOperationalShift(
        CafeChain.Data.AppDbContext context,
        int storeId,
        DateTime startLocal,
        DateTime endLocal,
        string? name = null,
        string status = OperationalIceStatuses.Open)
    {
        EnsureStoreAndManager(context, storeId);
        var shift = new OperationalShift
        {
            StoreId = storeId,
            BusinessDate = BusinessDate,
            Name = name ?? $"Ca {Guid.NewGuid():N}",
            StartAtUtc = startLocal.ToUniversalTime(),
            EndAtUtc = endLocal.ToUniversalTime(),
            Status = status,
            CreatedByStaffId = ManagerStaffId,
            OpenedByStaffId = status == OperationalIceStatuses.Open ? ManagerStaffId : null,
            CreatedAtUtc = DateTime.UtcNow,
            OpenedAtUtc = status == OperationalIceStatuses.Open ? DateTime.UtcNow : null,
            RowVersion = [0]
        };
        context.OperationalShifts.Add(shift);
        return shift;
    }

    private static WorkShift SeedWorkShift(
        CafeChain.Data.AppDbContext context,
        int shiftId,
        int storeId,
        DateTime start,
        DateTime? end,
        string status = "Closed")
    {
        EnsureStoreAndManager(context, storeId);
        var workShift = new WorkShift
        {
            ShiftId = shiftId,
            StoreId = storeId,
            UserId = ManagerStaffId,
            StartTime = start,
            EndTime = end,
            StartingCash = 0,
            ExpectedEndingCash = 0,
            Status = status
        };
        context.WorkShifts.Add(workShift);
        return workShift;
    }

    private static OperationalShiftWorkShift NewLink(int operationalShiftId, int workShiftId) => new()
    {
        OperationalShiftId = operationalShiftId,
        WorkShiftId = workShiftId,
        LinkedByStaffId = ManagerStaffId,
        LinkedAtUtc = DateTime.UtcNow
    };

    private static void EnsureStoreAndManager(CafeChain.Data.AppDbContext context, int storeId)
    {
        if (!context.Stores.Local.Any(x => x.StoreId == storeId))
        {
            context.Stores.Add(new Store
            {
                StoreId = storeId,
                Name = $"Store {storeId}",
                Active = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (context.Staffs.Local.Any(x => x.StaffId == ManagerStaffId))
            return;

        context.Accounts.Add(new Account
        {
            AccountId = ManagerStaffId,
            Email = "ice-link-manager@test.local",
            PasswordHash = "x",
            Active = true,
            CreatedAt = DateTime.UtcNow
        });
        context.Staffs.Add(new Staff
        {
            StaffId = ManagerStaffId,
            AccountId = ManagerStaffId,
            StoreId = StoreId,
            FullName = "Quản lý kiểm thử",
            Gender = 1,
            EmployeeStatus = 2,
            Active = true,
            CreatedAt = DateTime.UtcNow
        });
    }

    private static DateTime Local(int hour) =>
        DateTime.SpecifyKind(BusinessDate.AddHours(hour), DateTimeKind.Local);

    private static DateTime LocalNextDay(int hour) =>
        DateTime.SpecifyKind(BusinessDate.AddDays(1).AddHours(hour), DateTimeKind.Local);
}
