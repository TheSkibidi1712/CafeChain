using CafeChain.Application.Constants;
using CafeChain.Application.Results;
using CafeChain.Infrastructure.Repositories.Admin.POS;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Tests.POS;

public sealed class WorkShiftTerminalBindingTests : IntegrationTestBase
{
    private const int StoreId = 91001;

    [Fact]
    public async Task Legacy_active_shift_is_bound_to_selected_terminal()
    {
        await using var db = CreateDbContext();
        var shift = await SeedAsync(db);
        var repository = new WorkShiftRepository(db);

        var bound = await repository.BindTerminalForResumeAsync(
            shift.ShiftId, shift.UserId, shift.StoreId, "POS-1");

        Assert.Equal("POS-1", bound.PosTerminalId);
        db.ChangeTracker.Clear();
        Assert.Equal("POS-1", (await db.WorkShifts.SingleAsync(x => x.ShiftId == shift.ShiftId)).PosTerminalId);
    }

    [Fact]
    public async Task Already_bound_shift_rejects_a_different_terminal()
    {
        await using var db = CreateDbContext();
        var shift = await SeedAsync(db, terminalId: "POS-1");
        var repository = new WorkShiftRepository(db);

        var error = await Assert.ThrowsAsync<WorkShiftBusinessException>(() =>
            repository.BindTerminalForResumeAsync(
                shift.ShiftId, shift.UserId, shift.StoreId, "POS-2"));

        Assert.Equal(WorkShiftErrorCodes.WorkShiftTerminalMismatch, error.ErrorCode);
    }

    [Fact]
    public async Task Terminal_occupied_by_another_active_shift_is_rejected()
    {
        await using var db = CreateDbContext();
        var shift = await SeedAsync(db);
        db.WorkShifts.Add(NewShift(userId: 202, terminalId: "POS-1"));
        await db.SaveChangesAsync();
        var repository = new WorkShiftRepository(db);

        var error = await Assert.ThrowsAsync<WorkShiftBusinessException>(() =>
            repository.BindTerminalForResumeAsync(
                shift.ShiftId, shift.UserId, shift.StoreId, "POS-1"));

        Assert.Equal(WorkShiftErrorCodes.TerminalAlreadyHasOpenShift, error.ErrorCode);
    }

    private static async Task<WorkShift> SeedAsync(
        CafeChain.Data.AppDbContext db,
        string? terminalId = null)
    {
        var store = new Store
        {
            StoreId = StoreId,
            Name = "Terminal Binding Store",
            Active = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Stores.Add(store);
        db.PosTerminals.AddRange(
            new PosTerminal
            {
                TerminalId = "POS-1",
                StoreId = StoreId,
                Name = "POS 1",
                Active = true,
                Store = store
            },
            new PosTerminal
            {
                TerminalId = "POS-2",
                StoreId = StoreId,
                Name = "POS 2",
                Active = true,
                Store = store
            });
        var shift = NewShift(userId: 101, terminalId);
        db.WorkShifts.Add(shift);
        await db.SaveChangesAsync();
        return shift;
    }

    private static WorkShift NewShift(int userId, string? terminalId) => new()
    {
        StoreId = StoreId,
        UserId = userId,
        StartTimeUtc = DateTime.UtcNow.AddHours(-1),
        BusinessDate = DateTime.UtcNow.Date,
        Status = WorkShiftStatuses.Open,
        PosTerminalId = terminalId
    };
}
