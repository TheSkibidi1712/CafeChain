using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Options;
using CafeChain.Application.Services.Inventories;
using CafeChain.Application.Services.POS;
using CafeChain.Models.Customers;
using CafeChain.Models.Inventories.Ice;
using CafeChain.Models.Permissions;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CafeChain.Tests;

public sealed class OperationalIceActualScheduleSourceTests : IntegrationTestBase
{
    private const int StoreId = 41801;
    private const int ManagerId = 41810;
    private const int SupervisorId = 41811;
    private const int TemplateId = 41820;
    private static readonly DateTime BusinessDate = new(2026, 8, 10);
    private static readonly TimeZoneInfo VietnamTimeZone = new WorkShiftOptions().ResolveTimeZone();

    [Fact]
    public async Task CreateOperationalShift_FromSchedule_UsesActualScheduledTimes()
    {
        using var context = CreateDbContext();
        var schedule = SeedSchedule(context, SupervisorId, new TimeSpan(11, 50, 0), new TimeSpan(17, 50, 0));
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetScheduleOptionsAsync(StoreId, BusinessDate, Actor());

        Assert.True(result.IsSuccess, result.Message);
        var option = Assert.Single(result.Data);
        Assert.Equal([schedule.StaffShiftId], option.StaffShiftIds);
        Assert.Equal(ToUtc(BusinessDate.AddHours(11).AddMinutes(50)), option.StartAtUtc);
        Assert.Equal(ToUtc(BusinessDate.AddHours(17).AddMinutes(50)), option.EndAtUtc);
    }

    [Fact]
    public async Task StaffShifts_WithDivergentOverrides_AreNotSilentlyGrouped()
    {
        using var context = CreateDbContext();
        var first = SeedSchedule(context, SupervisorId, new TimeSpan(11, 50, 0), new TimeSpan(17, 50, 0));
        var second = SeedSchedule(context, ManagerId, new TimeSpan(12, 0, 0), new TimeSpan(18, 0, 0));
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetScheduleOptionsAsync(StoreId, BusinessDate, Actor());

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(2, result.Data.Count);
        Assert.Contains(result.Data, x => x.StaffShiftIds.SequenceEqual([first.StaffShiftId]));
        Assert.Contains(result.Data, x => x.StaffShiftIds.SequenceEqual([second.StaffShiftId]));
    }

    [Fact]
    public async Task CreateScheduleShift_PersistsActualStaffShiftSourceAndIgnoresPostedTemplateTimes()
    {
        using var context = CreateDbContext();
        var schedule = SeedSchedule(context, SupervisorId, new TimeSpan(11, 50, 0), new TimeSpan(17, 50, 0));
        SeedValidIcePolicy(context);
        await context.SaveChangesAsync();

        var result = await CreateService(context).CreateShiftAsync(
            new CreateOperationalShiftRequest
            {
                StoreId = StoreId,
                BusinessDate = BusinessDate,
                Name = "Giá trị từ trình duyệt không phải authority",
                StartAtUtc = ToUtc(BusinessDate.AddHours(12)),
                EndAtUtc = ToUtc(BusinessDate.AddHours(18)),
                ShiftLeadId = SupervisorId,
                CreationSource = OperationalIceCreationSources.StaffSchedule,
                SourceScheduleShiftId = TemplateId,
                SourceStaffShiftIds = [schedule.StaffShiftId]
            },
            Actor());

        Assert.True(result.IsSuccess, result.Message);
        var saved = await context.OperationalShifts.AsNoTracking()
            .Include(x => x.ScheduleSources)
            .SingleAsync(x => x.OperationalShiftId == result.Data!.OperationalShiftId);
        Assert.Equal("Ca chiều", saved.Name);
        Assert.Equal(ToUtc(BusinessDate.AddHours(11).AddMinutes(50)), saved.StartAtUtc);
        Assert.Equal(ToUtc(BusinessDate.AddHours(17).AddMinutes(50)), saved.EndAtUtc);
        Assert.Equal([schedule.StaffShiftId], saved.ScheduleSources.Select(x => x.StaffShiftId));
    }

    [Fact]
    public async Task DraftScheduleSync_UsesCurrentEffectiveIntervalAndKeepsActualSourceIdentity()
    {
        using var context = CreateDbContext();
        var schedule = SeedSchedule(context, SupervisorId, new TimeSpan(12, 0, 0), new TimeSpan(18, 0, 0));
        var shift = SeedOperationalShift(context, schedule, OperationalIceStatuses.Draft);
        await context.SaveChangesAsync();
        schedule.CustomStartTime = new TimeSpan(11, 50, 0);
        schedule.CustomEndTime = new TimeSpan(17, 50, 0);
        await context.SaveChangesAsync();

        var result = await CreateService(context).SyncDraftWithScheduleAsync(
            new SyncOperationalShiftScheduleRequest { OperationalShiftId = shift.OperationalShiftId },
            Actor());

        Assert.True(result.IsSuccess, result.Message);
        var saved = await context.OperationalShifts.AsNoTracking()
            .Include(x => x.ScheduleSources)
            .SingleAsync(x => x.OperationalShiftId == shift.OperationalShiftId);
        Assert.Equal(ToUtc(BusinessDate.AddHours(11).AddMinutes(50)), saved.StartAtUtc);
        Assert.Equal(ToUtc(BusinessDate.AddHours(17).AddMinutes(50)), saved.EndAtUtc);
        Assert.Equal([schedule.StaffShiftId], saved.ScheduleSources.Select(x => x.StaffShiftId));
    }

    [Fact]
    public async Task OpenedShift_TimeSnapshotCannotBeSynchronizedAfterScheduleChange()
    {
        using var context = CreateDbContext();
        var schedule = SeedSchedule(context, SupervisorId, new TimeSpan(12, 0, 0), new TimeSpan(18, 0, 0));
        var shift = SeedOperationalShift(context, schedule, OperationalIceStatuses.Open);
        await context.SaveChangesAsync();
        var originalStart = shift.StartAtUtc;
        var originalEnd = shift.EndAtUtc;
        schedule.CustomStartTime = new TimeSpan(11, 50, 0);
        schedule.CustomEndTime = new TimeSpan(17, 50, 0);
        await context.SaveChangesAsync();

        var result = await CreateService(context).SyncDraftWithScheduleAsync(
            new SyncOperationalShiftScheduleRequest { OperationalShiftId = shift.OperationalShiftId },
            Actor());

        Assert.False(result.IsSuccess);
        await context.Entry(shift).ReloadAsync();
        Assert.Equal(originalStart, shift.StartAtUtc);
        Assert.Equal(originalEnd, shift.EndAtUtc);
    }

    [Fact]
    public async Task ZeroCandidateAssessment_ReturnsStableReasonAndVietnameseMessage()
    {
        using var context = CreateDbContext();
        SeedBase(context);
        var shift = new OperationalShift
        {
            StoreId = StoreId,
            BusinessDate = BusinessDate,
            Name = "Ca chiều",
            StartAtUtc = ToUtc(BusinessDate.AddHours(11).AddMinutes(50)),
            EndAtUtc = ToUtc(BusinessDate.AddHours(17).AddMinutes(50)),
            Status = OperationalIceStatuses.Open,
            CreatedByStaffId = ManagerId,
            CreatedAtUtc = DateTime.UtcNow,
            RowVersion = [0]
        };
        context.OperationalShifts.Add(shift);
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetWorkShiftCandidateAssessmentAsync(
            shift.OperationalShiftId,
            Actor());

        Assert.True(result.IsSuccess, result.Message);
        Assert.Empty(result.Data!.Candidates);
        var diagnostic = Assert.Single(result.Data.Diagnostics);
        Assert.Equal(OperationalIceErrorCodes.CandidateNoStoreDateMatch, diagnostic.ReasonCode);
        Assert.Contains("Chưa có ca bán hàng POS", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LinkedWorkShift_IsDiagnosedAsCurrentShiftInsteadOfConflictingShift()
    {
        using var context = CreateDbContext();
        SeedBase(context);
        var shift = new OperationalShift
        {
            StoreId = StoreId,
            BusinessDate = BusinessDate,
            Name = "Ca chiều",
            StartAtUtc = ToUtc(BusinessDate.AddHours(11).AddMinutes(50)),
            EndAtUtc = ToUtc(BusinessDate.AddHours(17).AddMinutes(50)),
            Status = OperationalIceStatuses.Open,
            CreatedByStaffId = ManagerId,
            CreatedAtUtc = DateTime.UtcNow,
            RowVersion = [0]
        };
        var workShift = NewWorkShift(
            41833,
            ToUtc(BusinessDate.AddHours(12)),
            ToUtc(BusinessDate.AddHours(18)));
        context.OperationalShifts.Add(shift);
        context.WorkShifts.Add(workShift);
        context.OperationalShiftWorkShifts.Add(new OperationalShiftWorkShift
        {
            OperationalShift = shift,
            WorkShift = workShift,
            LinkedByStaffId = ManagerId,
            LinkedAtUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetWorkShiftCandidateAssessmentAsync(
            shift.OperationalShiftId,
            Actor());

        Assert.True(result.IsSuccess, result.Message);
        Assert.Empty(result.Data!.Candidates);
        var diagnostic = Assert.Single(result.Data.Diagnostics);
        Assert.Equal(OperationalIceErrorCodes.CandidateLinkedToCurrent, diagnostic.ReasonCode);
        Assert.Contains("ca vận hành này", diagnostic.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("ca vận hành khác", diagnostic.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CandidateDiscovery_IsIndependentOfCreationOrder(bool operationalShiftFirst)
    {
        using var context = CreateDbContext();
        SeedBase(context);
        var shift = new OperationalShift
        {
            StoreId = StoreId,
            BusinessDate = BusinessDate,
            Name = "Ca chiều",
            StartAtUtc = ToUtc(BusinessDate.AddHours(11).AddMinutes(50)),
            EndAtUtc = ToUtc(BusinessDate.AddHours(17).AddMinutes(50)),
            Status = OperationalIceStatuses.Open,
            CreatedByStaffId = ManagerId,
            CreatedAtUtc = DateTime.UtcNow,
            RowVersion = [0]
        };
        var workShift = NewWorkShift(41830, ToUtc(BusinessDate.AddHours(12)), ToUtc(BusinessDate.AddHours(18)));
        if (operationalShiftFirst)
        {
            context.OperationalShifts.Add(shift);
            await context.SaveChangesAsync();
            context.WorkShifts.Add(workShift);
        }
        else
        {
            context.WorkShifts.Add(workShift);
            await context.SaveChangesAsync();
            context.OperationalShifts.Add(shift);
        }
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetWorkShiftCandidateAssessmentAsync(
            shift.OperationalShiftId,
            Actor());

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(workShift.ShiftId, Assert.Single(result.Data!.Candidates).WorkShiftId);
        Assert.Empty(await context.OperationalShiftWorkShifts.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task CandidateDiscovery_ReturnsMultipleOverlappingWorkShiftsWithoutAutoLinking()
    {
        using var context = CreateDbContext();
        SeedBase(context);
        var shift = new OperationalShift
        {
            StoreId = StoreId,
            BusinessDate = BusinessDate,
            Name = "Ca chiều",
            StartAtUtc = ToUtc(BusinessDate.AddHours(11).AddMinutes(50)),
            EndAtUtc = ToUtc(BusinessDate.AddHours(17).AddMinutes(50)),
            Status = OperationalIceStatuses.Open,
            CreatedByStaffId = ManagerId,
            CreatedAtUtc = DateTime.UtcNow,
            RowVersion = [0]
        };
        context.OperationalShifts.Add(shift);
        context.WorkShifts.AddRange(
            NewWorkShift(41831, ToUtc(BusinessDate.AddHours(12)), ToUtc(BusinessDate.AddHours(14)), ManagerId),
            NewWorkShift(41832, ToUtc(BusinessDate.AddHours(14)), ToUtc(BusinessDate.AddHours(18)), SupervisorId));
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetWorkShiftCandidateAssessmentAsync(
            shift.OperationalShiftId,
            Actor());

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(2, result.Data!.Candidates.Count);
        Assert.Empty(await context.OperationalShiftWorkShifts.AsNoTracking().ToListAsync());
    }

    [Fact]
    public void ScheduleSourceRelation_UsesActualStaffShiftCompositeIdentity()
    {
        using var context = CreateDbContext();
        var entity = context.Model.FindEntityType(typeof(OperationalShiftScheduleSource));

        Assert.NotNull(entity);
        Assert.Equal(
            [nameof(OperationalShiftScheduleSource.OperationalShiftId), nameof(OperationalShiftScheduleSource.StaffShiftId)],
            entity!.FindPrimaryKey()!.Properties.Select(x => x.Name));
        Assert.True(entity.GetIndexes().Single(x =>
            x.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(OperationalShiftScheduleSource.StaffShiftId)])).IsUnique);
    }

    [Fact]
    public void Baseline_CreatesScheduleSourceRelationWithoutLegacyHistoryBackfill()
    {
        var migrationPath = Path.Combine(
            FindRepositoryRoot(), "CafeChain", "Migrations", "20260815152712_InitialCreate.cs");
        var migration = File.ReadAllText(migrationPath);

        Assert.Contains("name: \"OperationalShiftScheduleSources\"", migration, StringComparison.Ordinal);
        Assert.Contains("x => new { x.OperationalShiftId, x.StaffShiftId }", migration, StringComparison.Ordinal);
        Assert.Contains("name: \"IX_OperationalShiftScheduleSources_StaffShiftId\"", migration, StringComparison.Ordinal);
        Assert.Contains("unique: true", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("UPDATE OperationalShifts", migration, StringComparison.OrdinalIgnoreCase);
    }

    private StaffShift SeedSchedule(
        CafeChain.Data.AppDbContext context,
        int staffId,
        TimeSpan? customStart,
        TimeSpan? customEnd)
    {
        SeedBase(context);
        var scheduledStatus = context.StaffShiftStatuses.Local
                                  .SingleOrDefault(x => x.Code == "SCHEDULED")
                              ?? context.StaffShiftStatuses.SingleOrDefault(x => x.Code == "SCHEDULED");
        if (scheduledStatus == null)
        {
            scheduledStatus = new StaffShiftStatus
            {
                Code = "SCHEDULED",
                Name = "Đã lên lịch",
                IsSystem = true
            };
            context.StaffShiftStatuses.Add(scheduledStatus);
        }
        var schedule = new StaffShift
        {
            StaffId = staffId,
            ShiftId = TemplateId,
            WorkDate = BusinessDate,
            CustomStartTime = customStart,
            CustomEndTime = customEnd,
            Status = scheduledStatus,
            RowVersion = [0]
        };
        context.StaffShifts.Add(schedule);
        return schedule;
    }

    private OperationalShift SeedOperationalShift(
        CafeChain.Data.AppDbContext context,
        StaffShift schedule,
        string status)
    {
        var shift = new OperationalShift
        {
            StoreId = StoreId,
            BusinessDate = BusinessDate,
            Name = "Ca chiều",
            StartAtUtc = ToUtc(BusinessDate.Add(schedule.CustomStartTime ?? TimeSpan.FromHours(12))),
            EndAtUtc = ToUtc(BusinessDate.Add(schedule.CustomEndTime ?? TimeSpan.FromHours(18))),
            CreationSource = OperationalIceCreationSources.StaffSchedule,
            SourceScheduleShiftId = TemplateId,
            ShiftLeadId = SupervisorId,
            Status = status,
            CreatedByStaffId = ManagerId,
            CreatedAtUtc = DateTime.UtcNow,
            RowVersion = [0],
            ScheduleSources =
            [
                new OperationalShiftScheduleSource { StaffShift = schedule }
            ]
        };
        context.OperationalShifts.Add(shift);
        return shift;
    }

    private static WorkShift NewWorkShift(
        int shiftId,
        DateTime startUtc,
        DateTime? endUtc,
        int userId = ManagerId) => new()
    {
        ShiftId = shiftId,
        StoreId = StoreId,
        UserId = userId,
        StartTimeUtc = startUtc,
        EndTimeUtc = endUtc,
        BusinessDate = BusinessDate,
        StartingCash = 0m,
        ExpectedEndingCash = 0m,
        Status = WorkShiftStatuses.Open,
        RowVersion = [0]
    };

    private void SeedBase(CafeChain.Data.AppDbContext context)
    {
        if (!context.Stores.Local.Any(x => x.StoreId == StoreId)
            && !context.Stores.Any(x => x.StoreId == StoreId))
        {
            context.Stores.Add(new Store
            {
                StoreId = StoreId,
                Name = "Cửa hàng kiểm thử",
                Active = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        SeedStaff(context, ManagerId, RoleConstants.StoreManager);
        SeedStaff(context, SupervisorId, RoleConstants.ShiftSupervisor);
        if (!context.Shifts.Local.Any(x => x.ShiftId == TemplateId)
            && !context.Shifts.Any(x => x.ShiftId == TemplateId))
        {
            context.Shifts.Add(new Shift
            {
                ShiftId = TemplateId,
                StoreId = StoreId,
                Name = "Ca chiều",
                StartTime = TimeSpan.FromHours(12),
                EndTime = TimeSpan.FromHours(18),
                Active = true,
                RowVersion = [0]
            });
        }
    }

    private void SeedValidIcePolicy(CafeChain.Data.AppDbContext context)
    {
        var ingredient = context.Ingredients.Include(x => x.BaseUnit)
            .Single(x => x.Code == "ING00007");
        var displayUnit = context.Units.Single(x => x.Active && x.UnitCode.ToLower() == "kg");
        if (!context.StoreInventories.Local.Any(x => x.StoreId == StoreId && x.IngredientId == ingredient.IngredientId)
            && !context.StoreInventories.Any(x => x.StoreId == StoreId && x.IngredientId == ingredient.IngredientId))
        {
            context.StoreInventories.Add(new StoreInventory
            {
                StoreId = StoreId,
                IngredientId = ingredient.IngredientId,
                AvailableQty = 100_000m,
                ReservedQty = 0m,
                LastUpdated = DateTime.UtcNow,
                RowVersion = [0]
            });
        }

        context.IcePolicies.Add(new IcePolicy
        {
            StoreId = StoreId,
            IngredientId = ingredient.IngredientId,
            DisplayUnitId = displayUnit.UnitId,
            SuggestedDailyQuantity = 30_000m,
            SuggestedShiftQuantity = 15_000m,
            AllowSupplementalIssue = true,
            AllowSameDayCarryOver = true,
            RequireVarianceApproval = true,
            VarianceApprovalQuantityThreshold = 5_000m,
            VarianceApprovalPercentThreshold = 10m,
            Active = true,
            UpdatedByStaffId = ManagerId,
            UpdatedAtUtc = DateTime.UtcNow,
            RowVersion = [0]
        });
    }

    private void SeedStaff(CafeChain.Data.AppDbContext context, int staffId, string roleName)
    {
        if (context.Staffs.Local.Any(x => x.StaffId == staffId)
            || context.Staffs.Any(x => x.StaffId == staffId))
            return;
        var roleId = context.Roles.Single(x => x.Name == roleName).RoleId;
        context.Accounts.Add(new Account
        {
            AccountId = staffId,
            Email = $"operational-ice-{staffId}@test.local",
            PasswordHash = "x",
            Active = true,
            CreatedAt = DateTime.UtcNow
        });
        context.AccountRoles.Add(new AccountRole { AccountId = staffId, RoleId = roleId });
        context.Staffs.Add(new Staff
        {
            StaffId = staffId,
            AccountId = staffId,
            StoreId = StoreId,
            FullName = $"Nhân viên {staffId}",
            Gender = 1,
            EmployeeStatus = 2,
            Active = true,
            CreatedAt = DateTime.UtcNow
        });
    }

    private static OperationalIceService CreateService(CafeChain.Data.AppDbContext context)
    {
        var scope = new Mock<IScopeAuthorizationService>();
        scope.Setup(x => x.CanAccessStoreAsync(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(true);
        return new OperationalIceService(
            context,
            scope.Object,
            workShiftOptions: Options.Create(new WorkShiftOptions { TimeZoneId = "Asia/Ho_Chi_Minh" }));
    }

    private static AdminActorContext Actor() => new()
    {
        StaffId = ManagerId,
        StoreId = StoreId,
        RoleNames = [RoleConstants.StoreManager]
    };

    private static DateTime ToUtc(DateTime local) =>
        ScheduleIntervalResolver.ToUtc(
            DateTime.SpecifyKind(local, DateTimeKind.Unspecified),
            VietnamTimeZone);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null
               && !File.Exists(Path.Combine(directory.FullName, "CafeChain", "CafeChain.csproj")))
            directory = directory.Parent;
        return directory?.FullName
               ?? throw new DirectoryNotFoundException("Không tìm thấy repository root.");
    }
}
