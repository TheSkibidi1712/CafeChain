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
using Microsoft.EntityFrameworkCore.Metadata;
using Moq;
using Xunit;

namespace CafeChain.Tests;

public sealed class OperationalIceScheduleSourceIssue256Tests : IntegrationTestBase
{
    private const int StoreId = 25601;
    private const int OtherStoreId = 25602;
    private const int ManagerStaffId = 25610;
    private const int SupervisorStaffId = 25611;
    private static readonly DateTime BusinessDate = new(2026, 7, 30);

    [Fact]
    public void ExistingOperationalShifts_AreBackfilledAsManual()
    {
        var migration = ReadMigration();

        Assert.Contains("defaultValue: \"Manual\"", migration);
        Assert.Contains("nullable: false", migration);
    }

    [Fact]
    public async Task ManualShift_RequiresNullScheduleSource()
    {
        using var context = CreateDbContext();
        SeedStore(context, StoreId);
        await context.SaveChangesAsync();

        var result = await CreateService(context).CreateShiftAsync(
            NewRequest(OperationalIceCreationSources.Manual, 25620),
            Actor());

        Assert.False(result.IsSuccess);
        Assert.Contains("không nhất quán", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StaffScheduleShift_RequiresScheduleSource()
    {
        using var context = CreateDbContext();
        SeedStore(context, StoreId);
        await context.SaveChangesAsync();

        var result = await CreateService(context).CreateShiftAsync(
            NewRequest(OperationalIceCreationSources.StaffSchedule, null),
            Actor());

        Assert.False(result.IsSuccess);
        Assert.Contains("không nhất quán", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateFromSchedule_RejectsMissingSource()
    {
        using var context = CreateDbContext();
        SeedStore(context, StoreId);
        await context.SaveChangesAsync();

        var result = await CreateService(context).CreateShiftAsync(
            NewRequest(OperationalIceCreationSources.StaffSchedule, null),
            Actor());

        Assert.False(result.IsSuccess);
        Assert.Contains("không nhất quán", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateFromSchedule_PreventsDuplicateActiveShift()
    {
        using var context = CreateDbContext();
        await SeedValidScheduleScenarioAsync(context);
        var service = CreateService(context);
        var request = NewRequest(OperationalIceCreationSources.StaffSchedule, 25620);

        var first = await service.CreateShiftAsync(request, Actor());
        var replay = await service.CreateShiftAsync(request, Actor());

        Assert.True(first.IsSuccess, first.Message);
        Assert.False(replay.IsSuccess);
        Assert.Equal(OperationalIceErrorCodes.ScheduleShiftAlreadyUsed, replay.ErrorCode);
        Assert.Contains("đã được tạo từ ca lịch này", replay.Message);
        Assert.Equal(1, await context.OperationalShifts.CountAsync(x =>
            x.StoreId == StoreId && x.SourceScheduleShiftId == 25620));
    }

    [Fact]
    public async Task CancelledScheduleShift_DoesNotBlockValidRecreation()
    {
        using var context = CreateDbContext();
        await SeedValidScheduleScenarioAsync(context);
        context.OperationalShifts.Add(new OperationalShift
        {
            StoreId = StoreId,
            BusinessDate = BusinessDate,
            Name = "Ca sáng",
            StartAtUtc = LocalUtc(BusinessDate.AddHours(6)),
            EndAtUtc = LocalUtc(BusinessDate.AddHours(14)),
            CreationSource = OperationalIceCreationSources.StaffSchedule,
            SourceScheduleShiftId = 25620,
            ShiftLeadId = SupervisorStaffId,
            Status = OperationalIceStatuses.Cancelled,
            CreatedByStaffId = ManagerStaffId,
            CreatedAtUtc = DateTime.UtcNow,
            RowVersion = [0]
        });
        await context.SaveChangesAsync();

        var result = await CreateService(context).CreateShiftAsync(
            NewRequest(OperationalIceCreationSources.StaffSchedule, 25620),
            Actor());

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(2, await context.OperationalShifts.CountAsync(x =>
            x.StoreId == StoreId && x.SourceScheduleShiftId == 25620));
    }

    [Fact]
    public async Task ConcurrentCreateFromSameSchedule_CreatesOnlyOneShift()
    {
        using var context = CreateDbContext();
        SeedStore(context, StoreId);
        context.OperationalShifts.AddRange(
            DirectScheduleShift("Ca sáng A"),
            DirectScheduleShift("Ca sáng B"));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        var index = context.Model.FindEntityType(typeof(OperationalShift))!.GetIndexes()
            .Single(x => x.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(OperationalShift.StoreId), nameof(OperationalShift.BusinessDate), nameof(OperationalShift.SourceScheduleShiftId)]));
        Assert.True(index.IsUnique);
        Assert.Contains("Status] <> 'Cancelled'", index.GetFilter());
    }

    [Fact]
    public void DuplicateSqlError_ReturnsBusinessConflict()
    {
        var service = ReadRepoFile(
            "CafeChain", "Application", "Services", "Inventories", "OperationalIceService.cs");

        Assert.Contains("sqlException.Number is 2601 or 2627", service);
        Assert.Contains("Ca vận hành đá đã được tạo từ ca lịch này trong ngày kinh doanh đã chọn.", service);
        Assert.Contains("ScheduleShiftAlreadyUsed", service);
    }

    [Fact]
    public async Task ScheduleSource_MustBelongToSameStore()
    {
        using var context = CreateDbContext();
        SeedStore(context, StoreId);
        SeedStore(context, OtherStoreId);
        SeedStaff(context, SupervisorStaffId, StoreId, RoleConstants.ShiftSupervisor);
        SeedPolicy(context);
        SeedSchedule(context, OtherStoreId, 25620, BusinessDate, SupervisorStaffId);
        await context.SaveChangesAsync();

        var result = await CreateService(context).CreateShiftAsync(
            NewRequest(OperationalIceCreationSources.StaffSchedule, 25620),
            Actor());

        Assert.False(result.IsSuccess);
        Assert.Contains("không thuộc chi nhánh", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateFromSchedule_RejectsOutOfScopeSource()
    {
        using var context = CreateDbContext();
        SeedStore(context, StoreId);
        SeedStore(context, OtherStoreId);
        SeedStaff(context, SupervisorStaffId, StoreId, RoleConstants.ShiftSupervisor);
        SeedPolicy(context);
        SeedSchedule(context, OtherStoreId, 25620, BusinessDate, SupervisorStaffId);
        await context.SaveChangesAsync();

        var result = await CreateService(context).CreateShiftAsync(
            NewRequest(OperationalIceCreationSources.StaffSchedule, 25620),
            Actor());

        Assert.False(result.IsSuccess);
        Assert.Contains("không thuộc chi nhánh", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ScheduleSource_MustMatchBusinessDateContract()
    {
        using var context = CreateDbContext();
        SeedStore(context, StoreId);
        SeedStaff(context, SupervisorStaffId, StoreId, RoleConstants.ShiftSupervisor);
        SeedPolicy(context);
        SeedSchedule(context, StoreId, 25620, BusinessDate.AddDays(1), SupervisorStaffId);
        await context.SaveChangesAsync();

        var result = await CreateService(context).CreateShiftAsync(
            NewRequest(OperationalIceCreationSources.StaffSchedule, 25620),
            Actor());

        Assert.False(result.IsSuccess);
        Assert.Contains("ngày kinh doanh", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MultipleEmployeeSchedules_GroupIntoOneOperationalShift()
    {
        using var context = CreateDbContext();
        SeedStore(context, StoreId);
        SeedStaff(context, SupervisorStaffId, StoreId, RoleConstants.ShiftSupervisor);
        SeedStaff(context, 25612, StoreId, RoleConstants.SalesStaff);
        SeedStaff(context, 25613, StoreId, RoleConstants.SalesStaff);
        SeedSchedule(context, StoreId, 25620, BusinessDate, SupervisorStaffId, 25612, 25613);
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetScheduleOptionsAsync(StoreId, BusinessDate, Actor());

        Assert.True(result.IsSuccess, result.Message);
        var option = Assert.Single(result.Data);
        Assert.Equal(25620, option.ScheduleShiftId);
        Assert.Equal(3, option.StaffCount);
        Assert.Equal(SupervisorStaffId, option.SuggestedShiftLeadId);
        Assert.Equal(BusinessDate, option.BusinessDate);
    }

    [Fact]
    public async Task ScheduleMode_GroupsEmployeeAssignmentsIntoOneShiftOption()
    {
        using var context = CreateDbContext();
        SeedStore(context, StoreId);
        SeedStaff(context, SupervisorStaffId, StoreId, RoleConstants.ShiftSupervisor);
        SeedStaff(context, 25612, StoreId, RoleConstants.SalesStaff);
        SeedStaff(context, 25613, StoreId, RoleConstants.SalesStaff);
        SeedSchedule(context, StoreId, 25620, BusinessDate, SupervisorStaffId, 25612, 25613);
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetScheduleOptionsAsync(StoreId, BusinessDate, Actor());

        Assert.True(result.IsSuccess, result.Message);
        var option = Assert.Single(result.Data);
        Assert.Equal(25620, option.ScheduleShiftId);
        Assert.Equal(3, option.StaffCount);
        Assert.Equal(SupervisorStaffId, option.SuggestedShiftLeadId);
    }

    [Fact]
    public async Task ManualShiftCreation_RemainsSupported()
    {
        using var context = CreateDbContext();
        SeedStore(context, StoreId);
        SeedStaff(context, SupervisorStaffId, StoreId, RoleConstants.ShiftSupervisor);
        SeedPolicy(context);
        await context.SaveChangesAsync();

        var result = await CreateService(context).CreateShiftAsync(
            NewRequest(OperationalIceCreationSources.Manual, null),
            Actor());

        Assert.True(result.IsSuccess, result.Message);
        var shift = await context.OperationalShifts.SingleAsync();
        Assert.Equal(OperationalIceCreationSources.Manual, shift.CreationSource);
        Assert.Null(shift.SourceScheduleShiftId);
    }

    [Fact]
    public async Task ManualMode_RemainsSupported()
    {
        using var context = CreateDbContext();
        SeedStore(context, StoreId);
        SeedStaff(context, SupervisorStaffId, StoreId, RoleConstants.ShiftSupervisor);
        SeedPolicy(context);
        await context.SaveChangesAsync();

        var result = await CreateService(context).CreateShiftAsync(
            NewRequest(OperationalIceCreationSources.Manual, null),
            Actor());

        Assert.True(result.IsSuccess, result.Message);
        var shift = await context.OperationalShifts.SingleAsync();
        Assert.Equal(OperationalIceCreationSources.Manual, shift.CreationSource);
        Assert.Null(shift.SourceScheduleShiftId);
    }

    [Fact]
    public async Task ExistingManualShift_RemainsManual()
    {
        using var context = CreateDbContext();
        SeedStore(context, StoreId);
        SeedStaff(context, SupervisorStaffId, StoreId, RoleConstants.ShiftSupervisor);
        SeedPolicy(context);
        context.OperationalShifts.Add(new OperationalShift
        {
            StoreId = StoreId,
            BusinessDate = BusinessDate,
            Name = "Ca thủ công hiện có",
            StartAtUtc = LocalUtc(BusinessDate.AddHours(6)),
            EndAtUtc = LocalUtc(BusinessDate.AddHours(14)),
            CreationSource = OperationalIceCreationSources.Manual,
            SourceScheduleShiftId = null,
            ShiftLeadId = SupervisorStaffId,
            Status = OperationalIceStatuses.Draft,
            CreatedByStaffId = ManagerStaffId,
            CreatedAtUtc = DateTime.UtcNow,
            RowVersion = [0]
        });
        await context.SaveChangesAsync();

        var shift = await context.OperationalShifts.AsNoTracking().SingleAsync();

        Assert.Equal(OperationalIceCreationSources.Manual, shift.CreationSource);
        Assert.Null(shift.SourceScheduleShiftId);
    }

    [Fact]
    public void Migration_PreservesHistoricalOperationalShifts()
    {
        var migration = ReadMigration();

        Assert.Contains("defaultValue: \"Manual\"", migration);
        Assert.DoesNotContain("DropTable(", migration);
        Assert.DoesNotContain("DeleteData(", migration);
        Assert.Contains("[CreationSource] = 'Manual' AND [Status] <> 'Cancelled'", migration);
        Assert.Contains("[SourceScheduleShiftId] IS NOT NULL AND [Status] <> 'Cancelled'", migration);
    }

    [Fact]
    public void DownMigration_RestoresPreviousSchema()
    {
        var migration = ReadMigration();

        Assert.Contains("DropForeignKey(", migration);
        Assert.Contains("DropCheckConstraint(", migration);
        Assert.Contains("name: \"CreationSource\"", migration);
        Assert.Contains("name: \"SourceScheduleShiftId\"", migration);
        Assert.Contains("IX_OperationalShifts_StoreId_BusinessDate_Name", migration);
    }

    [Fact]
    public async Task WorkShiftSuggestions_RequireSameStoreOverlapAndUnlinkedAuthority()
    {
        using var context = CreateDbContext();
        SeedStore(context, StoreId);
        SeedStore(context, OtherStoreId);
        SeedStaff(context, SupervisorStaffId, StoreId, RoleConstants.ShiftSupervisor);
        SeedStaff(context, 25612, OtherStoreId, RoleConstants.SalesStaff);
        var operational = DirectScheduleShift("Ca sáng");
        operational.Status = OperationalIceStatuses.Open;
        context.OperationalShifts.Add(operational);
        context.WorkShifts.AddRange(
            WorkShift(25630, StoreId, SupervisorStaffId, BusinessDate.AddHours(7), BusinessDate.AddHours(12)),
            WorkShift(25631, StoreId, SupervisorStaffId, BusinessDate.AddHours(15), BusinessDate.AddHours(16)),
            WorkShift(25632, OtherStoreId, 25612, BusinessDate.AddHours(7), BusinessDate.AddHours(12)));
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetWorkShiftSuggestionsAsync(
            operational.OperationalShiftId,
            Actor());

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(25630, Assert.Single(result.Data).WorkShiftId);
    }

    [Fact]
    public async Task WorkShiftSuggestions_ExcludeDifferentBusinessDateAndStaleOpenShift()
    {
        using var context = CreateDbContext();
        SeedStore(context, StoreId);
        SeedStaff(context, SupervisorStaffId, StoreId, RoleConstants.ShiftSupervisor);
        var operational = DirectScheduleShift("Ca sáng");
        operational.Status = OperationalIceStatuses.Open;
        var staleJanuary = WorkShift(
            25630,
            StoreId,
            SupervisorStaffId,
            new DateTime(2026, 1, 18, 6, 0, 0),
            new DateTime(2026, 1, 18, 14, 0, 0));
        staleJanuary.Status = "Open";
        staleJanuary.EndTime = null;
        context.OperationalShifts.Add(operational);
        context.WorkShifts.AddRange(
            staleJanuary,
            WorkShift(25631, StoreId, SupervisorStaffId, BusinessDate.AddDays(-1).AddHours(7), BusinessDate.AddDays(-1).AddHours(12)),
            WorkShift(25632, StoreId, SupervisorStaffId, BusinessDate.AddHours(7), BusinessDate.AddHours(12)));
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetWorkShiftSuggestionsAsync(
            operational.OperationalShiftId,
            Actor());

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(25632, Assert.Single(result.Data).WorkShiftId);
    }

    [Fact]
    public async Task WorkShiftSuggestions_ExcludeCancelledStatus()
    {
        using var context = CreateDbContext();
        SeedStore(context, StoreId);
        SeedStaff(context, SupervisorStaffId, StoreId, RoleConstants.ShiftSupervisor);
        var operational = DirectScheduleShift("Ca sáng");
        operational.Status = OperationalIceStatuses.Open;
        var cancelled = WorkShift(
            25630,
            StoreId,
            SupervisorStaffId,
            BusinessDate.AddHours(7),
            BusinessDate.AddHours(12));
        cancelled.Status = "Cancelled";
        context.OperationalShifts.Add(operational);
        context.WorkShifts.Add(cancelled);
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetWorkShiftSuggestionsAsync(
            operational.OperationalShiftId,
            Actor());

        Assert.True(result.IsSuccess, result.Message);
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task WorkShiftLink_BackendRevalidatesStoreDateAndOverlap()
    {
        using var context = CreateDbContext();
        SeedStore(context, StoreId);
        SeedStore(context, OtherStoreId);
        SeedStaff(context, SupervisorStaffId, StoreId, RoleConstants.ShiftSupervisor);
        SeedStaff(context, 25612, OtherStoreId, RoleConstants.SalesStaff);
        var operational = DirectScheduleShift("Ca sáng");
        operational.Status = OperationalIceStatuses.Open;
        context.OperationalShifts.Add(operational);
        context.WorkShifts.AddRange(
            WorkShift(25630, OtherStoreId, 25612, BusinessDate.AddHours(7), BusinessDate.AddHours(12)),
            WorkShift(25631, StoreId, SupervisorStaffId, BusinessDate.AddDays(-1).AddHours(7), BusinessDate.AddDays(-1).AddHours(12)),
            WorkShift(25632, StoreId, SupervisorStaffId, BusinessDate.AddHours(15), BusinessDate.AddHours(16)));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var wrongStore = await service.LinkWorkShiftsAsync(new LinkOperationalWorkShiftsRequest
        {
            OperationalShiftId = operational.OperationalShiftId,
            WorkShiftIds = [25630]
        }, Actor());
        var wrongDate = await service.LinkWorkShiftsAsync(new LinkOperationalWorkShiftsRequest
        {
            OperationalShiftId = operational.OperationalShiftId,
            WorkShiftIds = [25631]
        }, Actor());
        var noOverlap = await service.LinkWorkShiftsAsync(new LinkOperationalWorkShiftsRequest
        {
            OperationalShiftId = operational.OperationalShiftId,
            WorkShiftIds = [25632]
        }, Actor());

        Assert.False(wrongStore.IsSuccess);
        Assert.False(wrongDate.IsSuccess);
        Assert.False(noOverlap.IsSuccess);
        Assert.Empty(await context.OperationalShiftWorkShifts.ToListAsync());
    }

    [Fact]
    public async Task ChangingOperationalShift_ReturnsOnlyCurrentCandidates()
    {
        using var context = CreateDbContext();
        SeedStore(context, StoreId);
        SeedStaff(context, SupervisorStaffId, StoreId, RoleConstants.ShiftSupervisor);
        var morning = DirectScheduleShift("Ca sáng");
        morning.Status = OperationalIceStatuses.Open;
        var afternoon = DirectScheduleShift("Ca chiều");
        afternoon.SourceScheduleShiftId = 25621;
        afternoon.StartAtUtc = LocalUtc(BusinessDate.AddHours(14));
        afternoon.EndAtUtc = LocalUtc(BusinessDate.AddHours(22));
        afternoon.Status = OperationalIceStatuses.Open;
        context.OperationalShifts.AddRange(morning, afternoon);
        context.WorkShifts.AddRange(
            WorkShift(25630, StoreId, SupervisorStaffId, BusinessDate.AddHours(7), BusinessDate.AddHours(12)),
            WorkShift(25631, StoreId, SupervisorStaffId, BusinessDate.AddHours(15), BusinessDate.AddHours(20)));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var morningResult = await service.GetWorkShiftSuggestionsAsync(
            morning.OperationalShiftId,
            Actor());
        var afternoonResult = await service.GetWorkShiftSuggestionsAsync(
            afternoon.OperationalShiftId,
            Actor());

        Assert.Equal(25630, Assert.Single(morningResult.Data).WorkShiftId);
        Assert.Equal(25631, Assert.Single(afternoonResult.Data).WorkShiftId);
    }

    [Fact]
    public async Task ChangingStore_ReturnsOnlyCurrentStoreCandidates()
    {
        using var context = CreateDbContext();
        SeedStore(context, StoreId);
        SeedStore(context, OtherStoreId);
        SeedStaff(context, SupervisorStaffId, StoreId, RoleConstants.ShiftSupervisor);
        SeedStaff(context, 25612, OtherStoreId, RoleConstants.ShiftSupervisor);
        var firstStoreShift = DirectScheduleShift("Ca cửa hàng 1");
        firstStoreShift.Status = OperationalIceStatuses.Open;
        var secondStoreShift = DirectScheduleShift("Ca cửa hàng 2");
        secondStoreShift.StoreId = OtherStoreId;
        secondStoreShift.SourceScheduleShiftId = 25621;
        secondStoreShift.ShiftLeadId = 25612;
        secondStoreShift.CreatedByStaffId = 25612;
        secondStoreShift.Status = OperationalIceStatuses.Open;
        context.OperationalShifts.AddRange(firstStoreShift, secondStoreShift);
        context.WorkShifts.AddRange(
            WorkShift(25630, StoreId, SupervisorStaffId, BusinessDate.AddHours(7), BusinessDate.AddHours(12)),
            WorkShift(25631, OtherStoreId, 25612, BusinessDate.AddHours(7), BusinessDate.AddHours(12)));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var firstResult = await service.GetWorkShiftSuggestionsAsync(
            firstStoreShift.OperationalShiftId,
            Actor());
        var secondResult = await service.GetWorkShiftSuggestionsAsync(
            secondStoreShift.OperationalShiftId,
            Actor());

        Assert.Equal(25630, Assert.Single(firstResult.Data).WorkShiftId);
        Assert.Equal(25631, Assert.Single(secondResult.Data).WorkShiftId);
    }

    [Fact]
    public void WorkShiftSuggestionUi_IsServerRenderedPerAllocationAndNotClientCached()
    {
        var controller = ReadRepoFile(
            "CafeChain", "Areas", "Admin", "Controllers", "AdminOperationalIceController.cs");
        var details = ReadRepoFile(
            "CafeChain", "Areas", "Admin", "Views", "AdminOperationalIce", "Details.cshtml");

        Assert.Contains("[ResponseCache(NoStore = true", controller);
        Assert.Contains("GetWorkShiftSuggestionsAsync(", controller);
        Assert.Contains("allocation.OperationalShiftId", controller);
        Assert.Contains("name=\"OperationalShiftId\" value=\"@Model.OperationalShiftId\"", details);
        Assert.DoesNotContain("fetch(", details);
    }

    [Fact]
    public async Task BulkLink_IsIdempotentAndPreventsCrossShiftDoubleCount()
    {
        using var context = CreateDbContext();
        SeedStore(context, StoreId);
        SeedStaff(context, SupervisorStaffId, StoreId, RoleConstants.ShiftSupervisor);
        var firstShift = DirectScheduleShift("Ca sáng");
        firstShift.Status = OperationalIceStatuses.Open;
        var secondShift = DirectScheduleShift("Ca phụ");
        secondShift.SourceScheduleShiftId = 25621;
        secondShift.Status = OperationalIceStatuses.Open;
        context.OperationalShifts.AddRange(firstShift, secondShift);
        context.WorkShifts.AddRange(
            WorkShift(25630, StoreId, SupervisorStaffId, BusinessDate.AddHours(7), BusinessDate.AddHours(12)),
            WorkShift(25631, StoreId, SupervisorStaffId, BusinessDate.AddHours(8), BusinessDate.AddHours(13)));
        await context.SaveChangesAsync();
        var service = CreateService(context);
        var request = new LinkOperationalWorkShiftsRequest
        {
            OperationalShiftId = firstShift.OperationalShiftId,
            WorkShiftIds = [25630, 25631]
        };

        var first = await service.LinkWorkShiftsAsync(request, Actor());
        var replay = await service.LinkWorkShiftsAsync(request, Actor());
        var conflicting = await service.LinkWorkShiftsAsync(new LinkOperationalWorkShiftsRequest
        {
            OperationalShiftId = secondShift.OperationalShiftId,
            WorkShiftIds = [25630]
        }, Actor());

        Assert.True(first.IsSuccess, first.Message);
        Assert.True(replay.IsSuccess, replay.Message);
        Assert.Equal(2, await context.OperationalShiftWorkShifts.CountAsync());
        Assert.False(conflicting.IsSuccess);
        Assert.Equal(OperationalIceErrorCodes.WorkShiftAlreadyLinked, conflicting.ErrorCode);
    }

    [Fact]
    public void ConcurrentWorkShiftLink_IsProtectedByUniqueDatabaseIndex()
    {
        using var context = CreateDbContext();
        var entity = context.Model.FindEntityType(typeof(OperationalShiftWorkShift))!;
        var index = entity.GetIndexes()
            .Single(x => x.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(OperationalShiftWorkShift.WorkShiftId)]));

        Assert.True(index.IsUnique);
    }

    [Fact]
    public void OperationalIceUi_ExposesScheduleManualModesAndBulkWorkShiftLink()
    {
        var index = ReadRepoFile(
            "CafeChain", "Areas", "Admin", "Views", "AdminOperationalIce", "Index.cshtml");
        var details = ReadRepoFile(
            "CafeChain", "Areas", "Admin", "Views", "AdminOperationalIce", "Details.cshtml");

        Assert.Contains("Tạo từ lịch làm việc", index);
        Assert.Contains("Tạo thủ công", index);
        Assert.Contains("SourceScheduleShiftId", index);
        Assert.Contains("Không có lịch làm việc phù hợp.", index);
        Assert.Contains("LinkedWorkShiftCount", index);
        Assert.Contains("name=\"WorkShiftIds\"", details);
        Assert.Contains("Xác nhận liên kết", details);
    }

    [Fact]
    public async Task DraftShift_CanReviewAndExplicitlySyncScheduleChanges()
    {
        using var context = CreateDbContext();
        await SeedValidScheduleScenarioAsync(context);
        var operational = DirectScheduleShift("Ca sáng cũ");
        context.OperationalShifts.Add(operational);
        await context.SaveChangesAsync();

        var schedule = await context.Shifts.SingleAsync(x => x.ShiftId == 25620);
        schedule.Name = "Ca sáng cập nhật";
        schedule.StartTime = TimeSpan.FromHours(7);
        schedule.EndTime = TimeSpan.FromHours(15);
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var reviewResult = await service.GetScheduleReviewsAsync(
            StoreId,
            BusinessDate,
            Actor());

        Assert.True(reviewResult.IsSuccess, reviewResult.Message);
        var review = Assert.Single(reviewResult.Data);
        Assert.True(review.IsScheduleAvailable);
        Assert.True(review.HasChanges);
        Assert.True(review.CanSync);
        Assert.Equal("Ca sáng cũ", review.SavedName);
        Assert.Equal("Ca sáng cập nhật", review.CurrentName);
        Assert.Equal(LocalUtc(BusinessDate.AddHours(7)), review.CurrentStartAtUtc);
        Assert.Equal(LocalUtc(BusinessDate.AddHours(15)), review.CurrentEndAtUtc);

        var syncResult = await service.SyncDraftWithScheduleAsync(
            new SyncOperationalShiftScheduleRequest
            {
                OperationalShiftId = operational.OperationalShiftId
            },
            Actor());

        Assert.True(syncResult.IsSuccess, syncResult.Message);
        var synchronized = await context.OperationalShifts
            .AsNoTracking()
            .SingleAsync(x => x.OperationalShiftId == operational.OperationalShiftId);
        Assert.Equal("Ca sáng cập nhật", synchronized.Name);
        Assert.Equal(LocalUtc(BusinessDate.AddHours(7)), synchronized.StartAtUtc);
        Assert.Equal(LocalUtc(BusinessDate.AddHours(15)), synchronized.EndAtUtc);
        Assert.Equal(SupervisorStaffId, synchronized.ShiftLeadId);
    }

    [Fact]
    public async Task OpenShift_ReportsScheduleChangeButCannotBeSilentlySynchronized()
    {
        using var context = CreateDbContext();
        await SeedValidScheduleScenarioAsync(context);
        var operational = DirectScheduleShift("Ca sáng đang mở");
        operational.Status = OperationalIceStatuses.Open;
        context.OperationalShifts.Add(operational);
        await context.SaveChangesAsync();
        var savedStart = operational.StartAtUtc;
        var savedEnd = operational.EndAtUtc;

        var schedule = await context.Shifts.SingleAsync(x => x.ShiftId == 25620);
        schedule.Name = "Ca sáng thay đổi";
        schedule.StartTime = TimeSpan.FromHours(8);
        schedule.EndTime = TimeSpan.FromHours(16);
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var reviewResult = await service.GetScheduleReviewsAsync(
            StoreId,
            BusinessDate,
            Actor());

        Assert.True(reviewResult.IsSuccess, reviewResult.Message);
        var review = Assert.Single(reviewResult.Data);
        Assert.True(review.IsScheduleAvailable);
        Assert.True(review.HasChanges);
        Assert.False(review.CanSync);

        var syncResult = await service.SyncDraftWithScheduleAsync(
            new SyncOperationalShiftScheduleRequest
            {
                OperationalShiftId = operational.OperationalShiftId
            },
            Actor());

        Assert.False(syncResult.IsSuccess);
        Assert.Contains("Ca đã mở không được tự động thay đổi", syncResult.Message);
        var unchanged = await context.OperationalShifts
            .AsNoTracking()
            .SingleAsync(x => x.OperationalShiftId == operational.OperationalShiftId);
        Assert.Equal("Ca sáng đang mở", unchanged.Name);
        Assert.Equal(savedStart, unchanged.StartAtUtc);
        Assert.Equal(savedEnd, unchanged.EndAtUtc);
    }

    [Fact]
    public async Task MissingScheduleSource_IsReportedAndCannotBeSynchronized()
    {
        using var context = CreateDbContext();
        await SeedValidScheduleScenarioAsync(context);
        var operational = DirectScheduleShift("Ca sáng từ lịch");
        context.OperationalShifts.Add(operational);
        await context.SaveChangesAsync();

        var schedule = await context.Shifts.SingleAsync(x => x.ShiftId == 25620);
        schedule.Active = false;
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var reviewResult = await service.GetScheduleReviewsAsync(
            StoreId,
            BusinessDate,
            Actor());

        Assert.True(reviewResult.IsSuccess, reviewResult.Message);
        var review = Assert.Single(reviewResult.Data);
        Assert.False(review.IsScheduleAvailable);
        Assert.True(review.HasChanges);
        Assert.False(review.CanSync);

        var syncResult = await service.SyncDraftWithScheduleAsync(
            new SyncOperationalShiftScheduleRequest
            {
                OperationalShiftId = operational.OperationalShiftId
            },
            Actor());

        Assert.False(syncResult.IsSuccess);
        Assert.Contains("Lịch nguồn không còn hoạt động", syncResult.Message);
    }

    [Fact]
    public async Task ScheduleAssignmentCancellation_DoesNotCancelOperationalShift()
    {
        using var context = CreateDbContext();
        SeedStore(context, StoreId);
        SeedStaff(context, SupervisorStaffId, StoreId, RoleConstants.ShiftSupervisor);
        SeedStaff(context, 25612, StoreId, RoleConstants.SalesStaff);
        SeedPolicy(context);
        SeedSchedule(context, StoreId, 25620, BusinessDate, SupervisorStaffId, 25612);
        var operational = DirectScheduleShift("Ca sáng");
        context.OperationalShifts.Add(operational);
        await context.SaveChangesAsync();
        await CancelScheduleAssignmentAsync(context, 25612);

        var reviewResult = await CreateService(context).GetScheduleReviewsAsync(
            StoreId,
            BusinessDate,
            Actor());

        var review = Assert.Single(reviewResult.Data);
        Assert.True(review.HasCancelledAssignments);
        Assert.False(review.RequiresLeadReplacement);
        Assert.False(review.BlocksOpening);
        Assert.Equal(1, review.CancelledStaffCount);
        Assert.Equal(
            OperationalIceStatuses.Draft,
            (await context.OperationalShifts.AsNoTracking()
                .SingleAsync(x => x.OperationalShiftId == operational.OperationalShiftId)).Status);
    }

    [Fact]
    public async Task CancelledShiftLead_BlocksDraftOpeningUntilReplacement()
    {
        using var context = CreateDbContext();
        SeedStore(context, StoreId);
        SeedStaff(context, SupervisorStaffId, StoreId, RoleConstants.ShiftSupervisor);
        SeedStaff(context, 25612, StoreId, RoleConstants.SalesStaff);
        SeedStaff(context, 25613, StoreId, RoleConstants.ShiftSupervisor);
        SeedPolicy(context);
        SeedSchedule(context, StoreId, 25620, BusinessDate, SupervisorStaffId, 25612);
        var operational = DirectScheduleShift("Ca sáng");
        context.OperationalShifts.Add(operational);
        await context.SaveChangesAsync();
        await CancelScheduleAssignmentAsync(context, SupervisorStaffId);
        var service = CreateService(context);

        var reviewResult = await service.GetScheduleReviewsAsync(StoreId, BusinessDate, Actor());
        var blockedReview = Assert.Single(reviewResult.Data);
        var blockedOpen = await service.OpenAllocationAsync(new OpenIceAllocationRequest
        {
            OperationalShiftId = operational.OperationalShiftId,
            InitialIssuedQuantity = 15m
        }, Actor());

        Assert.True(blockedReview.RequiresLeadReplacement);
        Assert.True(blockedReview.BlocksOpening);
        Assert.False(blockedOpen.IsSuccess);
        Assert.Contains("hủy phân công", blockedOpen.Message, StringComparison.OrdinalIgnoreCase);

        var replacement = await service.UpdateDraftShiftLeadAsync(
            new UpdateOperationalShiftLeadRequest
            {
                OperationalShiftId = operational.OperationalShiftId,
                ShiftLeadId = 25613,
                Reason = "Thay ca trưởng đã hủy lịch"
            },
            Actor());

        Assert.True(replacement.IsSuccess, replacement.Message);
        Assert.Equal(
            25613,
            (await context.OperationalShifts.AsNoTracking()
                .SingleAsync(x => x.OperationalShiftId == operational.OperationalShiftId)).ShiftLeadId);
        Assert.Contains(await context.AuditLogs.AsNoTracking().ToListAsync(), x =>
            x.RecordId == operational.OperationalShiftId
            && x.Action == "UPDATE_SHIFT_LEAD");
    }

    [Fact]
    public async Task CancelledShiftLead_DoesNotAutoCloseOpenShift()
    {
        using var context = CreateDbContext();
        SeedStore(context, StoreId);
        SeedStaff(context, SupervisorStaffId, StoreId, RoleConstants.ShiftSupervisor);
        SeedStaff(context, 25612, StoreId, RoleConstants.SalesStaff);
        SeedPolicy(context);
        SeedSchedule(context, StoreId, 25620, BusinessDate, SupervisorStaffId, 25612);
        var operational = DirectScheduleShift("Ca sáng");
        operational.Status = OperationalIceStatuses.Open;
        context.OperationalShifts.Add(operational);
        await context.SaveChangesAsync();
        await CancelScheduleAssignmentAsync(context, SupervisorStaffId);

        var reviewResult = await CreateService(context).GetScheduleReviewsAsync(
            StoreId,
            BusinessDate,
            Actor());

        var review = Assert.Single(reviewResult.Data);
        Assert.True(review.RequiresLeadReplacement);
        Assert.False(review.CanSync);
        Assert.Equal(
            OperationalIceStatuses.Open,
            (await context.OperationalShifts.AsNoTracking()
                .SingleAsync(x => x.OperationalShiftId == operational.OperationalShiftId)).Status);
    }

    [Fact]
    public async Task CancelledScheduleSource_DoesNotMutateOpenShift()
    {
        using var context = CreateDbContext();
        await SeedValidScheduleScenarioAsync(context);
        var operational = DirectScheduleShift("Ca sáng đang mở");
        operational.Status = OperationalIceStatuses.Open;
        context.OperationalShifts.Add(operational);
        await context.SaveChangesAsync();
        var savedName = operational.Name;
        var savedStart = operational.StartAtUtc;
        var savedEnd = operational.EndAtUtc;
        await CancelScheduleAssignmentAsync(context, SupervisorStaffId);

        var reviewResult = await CreateService(context).GetScheduleReviewsAsync(
            StoreId,
            BusinessDate,
            Actor());

        var review = Assert.Single(reviewResult.Data);
        Assert.False(review.IsScheduleAvailable);
        Assert.True(review.BlocksOpening);
        Assert.False(review.CanSync);
        var persisted = await context.OperationalShifts.AsNoTracking()
            .SingleAsync(x => x.OperationalShiftId == operational.OperationalShiftId);
        Assert.Equal(OperationalIceStatuses.Open, persisted.Status);
        Assert.Equal(savedName, persisted.Name);
        Assert.Equal(savedStart, persisted.StartAtUtc);
        Assert.Equal(savedEnd, persisted.EndAtUtc);
        Assert.Equal(OperationalIceCreationSources.StaffSchedule, persisted.CreationSource);
        Assert.Equal(25620, persisted.SourceScheduleShiftId);
    }

    [Fact]
    public async Task CancelledScheduleSource_BlocksDraftOpeningAndCanConvertToManual()
    {
        using var context = CreateDbContext();
        await SeedValidScheduleScenarioAsync(context);
        var operational = DirectScheduleShift("Ca sáng");
        context.OperationalShifts.Add(operational);
        await context.SaveChangesAsync();
        await CancelScheduleAssignmentAsync(context, SupervisorStaffId);
        var service = CreateService(context);

        var blockedOpen = await service.OpenAllocationAsync(new OpenIceAllocationRequest
        {
            OperationalShiftId = operational.OperationalShiftId,
            InitialIssuedQuantity = 15m
        }, Actor());
        var converted = await service.ConvertDraftToManualAsync(
            new ConvertOperationalShiftToManualRequest
            {
                OperationalShiftId = operational.OperationalShiftId,
                Reason = "Toàn bộ lịch nguồn đã bị hủy"
            },
            Actor());

        Assert.False(blockedOpen.IsSuccess);
        Assert.Contains("lịch nguồn đã bị hủy", blockedOpen.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(converted.IsSuccess, converted.Message);
        var persisted = await context.OperationalShifts.AsNoTracking()
            .SingleAsync(x => x.OperationalShiftId == operational.OperationalShiftId);
        Assert.Equal(OperationalIceCreationSources.Manual, persisted.CreationSource);
        Assert.Null(persisted.SourceScheduleShiftId);
        Assert.Equal("Ca sáng", persisted.Name);
        Assert.Equal(LocalUtc(BusinessDate.AddHours(6)), persisted.StartAtUtc);
        Assert.Contains(await context.AuditLogs.AsNoTracking().ToListAsync(), x =>
            x.RecordId == operational.OperationalShiftId
            && x.Action == "CONVERT_TO_MANUAL"
            && x.OldData!.Contains("\"Reason\""));
    }

    [Fact]
    public async Task MissingScheduleSource_DraftCanBeCancelledWithAudit()
    {
        using var context = CreateDbContext();
        await SeedValidScheduleScenarioAsync(context);
        var operational = DirectScheduleShift("Ca sáng");
        context.OperationalShifts.Add(operational);
        await context.SaveChangesAsync();
        var schedule = await context.Shifts.SingleAsync(x => x.ShiftId == 25620);
        schedule.Active = false;
        await context.SaveChangesAsync();

        var result = await CreateService(context).CancelDraftShiftAsync(
            new CancelDraftOperationalShiftRequest
            {
                OperationalShiftId = operational.OperationalShiftId,
                Reason = "Không còn nhu cầu vận hành"
            },
            Actor());

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(
            OperationalIceStatuses.Cancelled,
            (await context.OperationalShifts.AsNoTracking()
                .SingleAsync(x => x.OperationalShiftId == operational.OperationalShiftId)).Status);
        Assert.Contains(await context.AuditLogs.AsNoTracking().ToListAsync(), x =>
            x.RecordId == operational.OperationalShiftId
            && x.Action == "CANCEL_DRAFT");
    }

    [Fact]
    public void OperationalIceUi_ShowsScheduleDiffAndExplicitDraftSyncAction()
    {
        var index = ReadRepoFile(
            "CafeChain", "Areas", "Admin", "Views", "AdminOperationalIce", "Index.cshtml");

        Assert.Contains("Lịch làm việc đã thay đổi", index);
        Assert.Contains("asp-action=\"SyncSchedule\"", index);
        Assert.Contains("Đồng bộ lịch", index);
        Assert.Contains("Ca lịch nguồn đã bị hủy", index);
        Assert.Contains("Ca trưởng trong lịch đã bị hủy phân công", index);
        Assert.Contains("asp-action=\"ConvertToManual\"", index);
        Assert.Contains("asp-action=\"UpdateShiftLead\"", index);
        Assert.Contains("asp-action=\"CancelDraftShift\"", index);
        Assert.Contains("không tự động thay đổi dữ liệu vận hành", index);
        Assert.Contains("BlocksOpening", index);
    }

    [Fact]
    public void StaffScheduleCancellation_PersistsStatusAndAudit()
    {
        var service = ReadRepoFile(
            "CafeChain", "Application", "Services", "Admin", "Staffs", "AdminStaffShiftService.cs");

        Assert.Contains("private const string Cancelled = \"CANCELLED\"", service);
        Assert.Contains("schedule.StatusId = (await RequireStatusAsync(Cancelled", service);
        Assert.Contains("AddAudit(\"StaffShifts\", schedule.StaffShiftId, \"CANCEL\"", service);
        Assert.Contains("Đã hủy lịch và giữ lại lịch sử.", service);
    }

    private static OperationalShift DirectScheduleShift(string name) => new()
    {
        StoreId = StoreId,
        BusinessDate = BusinessDate,
        Name = name,
        StartAtUtc = LocalUtc(BusinessDate.AddHours(6)),
        EndAtUtc = LocalUtc(BusinessDate.AddHours(14)),
        CreationSource = OperationalIceCreationSources.StaffSchedule,
        SourceScheduleShiftId = 25620,
        ShiftLeadId = SupervisorStaffId,
        Status = OperationalIceStatuses.Draft,
        CreatedByStaffId = ManagerStaffId,
        CreatedAtUtc = DateTime.UtcNow,
        RowVersion = [0]
    };

    private static CreateOperationalShiftRequest NewRequest(string source, int? scheduleShiftId) => new()
    {
        StoreId = StoreId,
        BusinessDate = BusinessDate,
        Name = "Ca sáng",
        StartAtUtc = LocalUtc(BusinessDate.AddHours(6)),
        EndAtUtc = LocalUtc(BusinessDate.AddHours(14)),
        ShiftLeadId = SupervisorStaffId,
        CreationSource = source,
        SourceScheduleShiftId = scheduleShiftId
    };

    private static WorkShift WorkShift(
        int id,
        int storeId,
        int staffId,
        DateTime start,
        DateTime end) => new()
    {
        ShiftId = id,
        StoreId = storeId,
        UserId = staffId,
        StartTime = start,
        EndTime = end,
        StartingCash = 0,
        ExpectedEndingCash = 0,
        Status = "Closed"
    };

    private static DateTime LocalUtc(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Local).ToUniversalTime();

    private async Task SeedValidScheduleScenarioAsync(CafeChain.Data.AppDbContext context)
    {
        SeedStore(context, StoreId);
        SeedStaff(context, SupervisorStaffId, StoreId, RoleConstants.ShiftSupervisor);
        SeedPolicy(context);
        SeedSchedule(context, StoreId, 25620, BusinessDate, SupervisorStaffId);
        await context.SaveChangesAsync();
    }

    private static void SeedStore(CafeChain.Data.AppDbContext context, int storeId)
    {
        context.Stores.Add(new Store
        {
            StoreId = storeId,
            Name = $"Cửa hàng {storeId}",
            Active = true,
            CreatedAt = DateTime.UtcNow
        });
    }

    private static void SeedPolicy(CafeChain.Data.AppDbContext context)
    {
        context.StoreInventories.Add(new StoreInventory
        {
            StoreId = StoreId,
            IngredientId = 7,
            AvailableQty = 100m,
            ReservedQty = 0,
            LastUpdated = DateTime.UtcNow,
            RowVersion = [0]
        });
        context.IcePolicies.Add(new IcePolicy
        {
            StoreId = StoreId,
            IngredientId = 7,
            DisplayUnitId = 2,
            SuggestedDailyQuantity = 30m,
            SuggestedShiftQuantity = 15m,
            AllowSupplementalIssue = true,
            AllowSameDayCarryOver = true,
            RequireVarianceApproval = true,
            VarianceApprovalQuantityThreshold = 5m,
            VarianceApprovalPercentThreshold = 10m,
            Active = true,
            UpdatedByStaffId = ManagerStaffId,
            UpdatedAtUtc = DateTime.UtcNow,
            RowVersion = [0]
        });
    }

    private static void SeedStaff(
        CafeChain.Data.AppDbContext context,
        int staffId,
        int storeId,
        string roleName)
    {
        var roleId = context.Roles.Where(x => x.Name == roleName).Select(x => x.RoleId).Single();
        context.Accounts.Add(new Account
        {
            AccountId = staffId,
            Email = $"issue256-{staffId}@test.local",
            PasswordHash = "test",
            Active = true,
            CreatedAt = DateTime.UtcNow
        });
        context.AccountRoles.Add(new AccountRole { AccountId = staffId, RoleId = roleId });
        context.Staffs.Add(new Staff
        {
            StaffId = staffId,
            AccountId = staffId,
            StoreId = storeId,
            FullName = $"Nhân viên {staffId}",
            Active = true,
            EmployeeStatus = 2,
            CreatedAt = DateTime.UtcNow
        });
    }

    private static void SeedSchedule(
        CafeChain.Data.AppDbContext context,
        int storeId,
        int shiftId,
        DateTime workDate,
        params int[] staffIds)
    {
        var status = context.StaffShiftStatuses.SingleOrDefault(x => x.Code == "SCHEDULED");
        if (status == null)
        {
            status = new StaffShiftStatus
            {
                StaffShiftStatusId = 25650,
                Code = "SCHEDULED",
                Name = "Đã lên lịch",
                IsSystem = true
            };
            context.StaffShiftStatuses.Add(status);
        }

        var shift = new Shift
        {
            ShiftId = shiftId,
            StoreId = storeId,
            Name = "Ca sáng",
            StartTime = TimeSpan.FromHours(6),
            EndTime = TimeSpan.FromHours(14),
            Active = true,
            RowVersion = [0]
        };
        context.Shifts.Add(shift);
        foreach (var staffId in staffIds)
        {
            context.StaffShifts.Add(new StaffShift
            {
                StaffId = staffId,
                ShiftId = shiftId,
                WorkDate = workDate.Date,
                StatusId = status.StaffShiftStatusId,
                Status = status,
                Shift = shift,
                RowVersion = [0]
            });
        }
    }

    private static async Task CancelScheduleAssignmentAsync(
        CafeChain.Data.AppDbContext context,
        int staffId)
    {
        var cancelled = await context.StaffShiftStatuses
            .SingleOrDefaultAsync(x => x.Code == "CANCELLED");
        if (cancelled == null)
        {
            cancelled = new StaffShiftStatus
            {
                StaffShiftStatusId = 25651,
                Code = "CANCELLED",
                Name = "Đã hủy",
                IsSystem = true
            };
            context.StaffShiftStatuses.Add(cancelled);
        }

        var assignment = await context.StaffShifts
            .SingleAsync(x => x.ShiftId == 25620
                              && x.WorkDate == BusinessDate
                              && x.StaffId == staffId);
        assignment.StatusId = cancelled.StaffShiftStatusId;
        assignment.Status = cancelled;
        await context.SaveChangesAsync();
    }

    private static OperationalIceService CreateService(CafeChain.Data.AppDbContext context)
    {
        var scope = new Mock<IScopeAuthorizationService>();
        scope.Setup(x => x.CanAccessStoreAsync(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(true);
        return new OperationalIceService(context, scope.Object);
    }

    private static AdminActorContext Actor() => new()
    {
        StaffId = ManagerStaffId,
        StoreId = StoreId,
        RoleNames = [RoleConstants.StoreManager]
    };

    private static string ReadMigration()
    {
        var migrations = Path.Combine(FindRepoRoot(), "CafeChain", "Migrations");
        return File.ReadAllText(Directory.GetFiles(
            migrations,
            "*_AddOperationalShiftScheduleSource.cs",
            SearchOption.TopDirectoryOnly).Single());
    }

    private static string ReadRepoFile(params string[] path) =>
        File.ReadAllText(Path.Combine([FindRepoRoot(), .. path]));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null
               && !File.Exists(Path.Combine(directory.FullName, "CafeChain", "CafeChain.slnx")))
            directory = directory.Parent;
        return directory?.FullName
               ?? throw new DirectoryNotFoundException("Không tìm thấy repository root.");
    }
}
