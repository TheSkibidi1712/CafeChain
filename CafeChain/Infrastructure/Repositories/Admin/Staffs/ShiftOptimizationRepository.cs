using CafeChain.Data;
using CafeChain.Infrastructure.Interfaces.Admin.Staffs;
using CafeChain.Models.Staffs;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Infrastructure.Repositories.Admin.Staffs;

public sealed class ShiftOptimizationRepository : IShiftOptimizationRepository
{
    private readonly AppDbContext _context;
    public ShiftOptimizationRepository(AppDbContext context) => _context = context;
    public Task<List<Staff>> GetStaffsAsync(int storeId, CancellationToken ct) => _context.Staffs.AsNoTracking().Include(x => x.Account).ThenInclude(x => x.AccountRoles).Where(x => x.StoreId == storeId && x.Active && x.EmployeeStatus != 3 && x.Account.Active).ToListAsync(ct);
    public Task<Staff?> GetStaffAsync(int staffId, CancellationToken ct) => _context.Staffs.AsNoTracking().FirstOrDefaultAsync(x => x.StaffId == staffId, ct);
    public Task<List<Shift>> GetShiftsAsync(int storeId, CancellationToken ct) => _context.Shifts.AsNoTracking().Where(x => x.StoreId == storeId && x.Active).ToListAsync(ct);
    public Task<List<StaffShift>> GetSchedulesAsync(int storeId, DateTime from, DateTime to, CancellationToken ct) => _context.StaffShifts.AsNoTracking().Include(x => x.Shift).Include(x => x.Status).Where(x => x.Staff.StoreId == storeId && x.WorkDate >= from.Date.AddDays(-1) && x.WorkDate <= to.Date.AddDays(1) && x.Status.Code == "SCHEDULED").ToListAsync(ct);
    public Task<List<StaffAvailabilityRule>> GetAvailabilityAsync(int storeId, DateTime from, DateTime to, CancellationToken ct) => _context.StaffAvailabilityRules.AsNoTracking().Where(x => x.Staff.StoreId == storeId && x.Active && x.EffectiveFrom <= to && (!x.EffectiveTo.HasValue || x.EffectiveTo >= from)).ToListAsync(ct);
    public Task<List<StaffAvailabilityException>> GetExceptionsAsync(int storeId, DateTime from, DateTime to, CancellationToken ct) => _context.StaffAvailabilityExceptions.AsNoTracking().Where(x => x.Staff.StoreId == storeId && x.Date >= from.Date && x.Date <= to.Date).ToListAsync(ct);
    public Task<List<StaffTimeOff>> GetTimeOffsAsync(int storeId, DateTime from, DateTime to, CancellationToken ct) => _context.StaffTimeOffs.AsNoTracking().Where(x => x.Staff.StoreId == storeId && x.Status == "APPROVED" && x.FromUtc < to && x.ToUtc > from).ToListAsync(ct);
    public Task<List<StaffWorkConstraint>> GetConstraintsAsync(int storeId, DateTime from, DateTime to, CancellationToken ct) => _context.StaffWorkConstraints.AsNoTracking().Where(x => x.Staff.StoreId == storeId && x.EffectiveFrom <= to && (!x.EffectiveTo.HasValue || x.EffectiveTo >= from)).ToListAsync(ct);
    public Task<List<StoreStaffingRequirement>> GetRequirementsAsync(int storeId, DateTime from, DateTime to, CancellationToken ct) => _context.StoreStaffingRequirements.AsNoTracking().Include(x => x.Shift).Where(x => x.StoreId == storeId && x.Active && x.EffectiveFrom <= to && (!x.EffectiveTo.HasValue || x.EffectiveTo >= from)).ToListAsync(ct);
    public Task<ScheduleOptimizationProposal?> GetProposalAsync(Guid id, bool tracking, CancellationToken ct)
    { var q = _context.ScheduleOptimizationProposals.Include(x => x.Assignments).ThenInclude(x => x.Staff).Include(x => x.Assignments).ThenInclude(x => x.Shift).AsQueryable(); if (!tracking) q = q.AsNoTracking(); return q.FirstOrDefaultAsync(x => x.ScheduleOptimizationProposalId == id, ct); }
    public Task<StaffShiftStatus?> GetScheduledStatusAsync(CancellationToken ct) => _context.StaffShiftStatuses.FirstOrDefaultAsync(x => x.Code == "SCHEDULED", ct);
    public void Add(object entity) => _context.Add(entity);
    public Task BeginTransactionAsync(CancellationToken ct) => _context.Database.BeginTransactionAsync(ct);
    public Task CommitAsync(CancellationToken ct) => _context.Database.CommitTransactionAsync(ct);
    public Task RollbackAsync(CancellationToken ct) => _context.Database.RollbackTransactionAsync(ct);
    public Task<int> SaveChangesAsync(CancellationToken ct) => _context.SaveChangesAsync(ct);
}
