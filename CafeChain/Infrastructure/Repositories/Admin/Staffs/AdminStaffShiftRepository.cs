using CafeChain.Data;
using CafeChain.Infrastructure.Interfaces.Admin.Staffs;
using CafeChain.Models.Inventories.Auditing;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CafeChain.Infrastructure.Repositories.Admin.Staffs;

public sealed class AdminStaffShiftRepository : IAdminStaffShiftRepository
{
    private readonly AppDbContext _context;
    private IDbContextTransaction? _transaction;
    public AdminStaffShiftRepository(AppDbContext context) => _context = context;

    public async Task BeginTransactionAsync(CancellationToken ct) =>
        _transaction = await _context.Database.BeginTransactionAsync(ct);

    public async Task CommitTransactionAsync(CancellationToken ct)
    {
        if (_transaction == null) return;
        await _transaction.CommitAsync(ct);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackTransactionAsync(CancellationToken ct)
    {
        if (_transaction == null) return;
        await _transaction.RollbackAsync(ct);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public Task<Store?> GetStoreAsync(int storeId, CancellationToken ct) =>
        _context.Stores.AsNoTracking().SingleOrDefaultAsync(x => x.StoreId == storeId, ct);

    public Task<List<Staff>> GetStaffsAsync(int storeId, DateTime start, DateTime end, CancellationToken ct) =>
        _context.Staffs.AsNoTracking()
            .Include(x => x.Account).ThenInclude(x => x.AccountRoles).ThenInclude(x => x.Role)
            .Where(x => x.StoreId == storeId && x.Account.AccountRoles.Any(r => r.Role.IsStoreLevel))
            .Where(x => x.Active || x.StaffShifts.Any(s => s.WorkDate >= start.Date && s.WorkDate <= end.Date))
            .OrderBy(x => x.FullName).ToListAsync(ct);

    public Task<List<StaffShift>> GetSchedulesAsync(int storeId, DateTime from, DateTime to, CancellationToken ct) =>
        _context.StaffShifts.AsNoTracking()
            .Include(x => x.Shift).Include(x => x.Status)
            .Where(x => x.Staff.StoreId == storeId && x.Shift.StoreId == storeId
                && x.WorkDate >= from.Date && x.WorkDate <= to.Date)
            .OrderBy(x => x.WorkDate).ThenBy(x => x.Shift.StartTime).ToListAsync(ct);

    public Task<List<Shift>> GetTemplatesAsync(int storeId, bool includeInactive, CancellationToken ct) =>
        _context.Shifts.AsNoTracking().Where(x => x.StoreId == storeId && (includeInactive || x.Active))
            .OrderBy(x => x.StartTime).ThenBy(x => x.Name).ToListAsync(ct);

    public Task<Staff?> GetStaffAsync(int id, CancellationToken ct) =>
        _context.Staffs.SingleOrDefaultAsync(x => x.StaffId == id, ct);

    public Task<Shift?> GetTemplateAsync(int id, CancellationToken ct) =>
        _context.Shifts.SingleOrDefaultAsync(x => x.ShiftId == id, ct);

    public Task<StaffShift?> GetScheduleAsync(int id, CancellationToken ct) =>
        _context.StaffShifts.Include(x => x.Shift).Include(x => x.Staff).Include(x => x.Status)
            .SingleOrDefaultAsync(x => x.StaffShiftId == id, ct);

    public Task<StaffShift?> GetScheduleAsync(int staffId, int shiftId, DateTime date, CancellationToken ct) =>
        _context.StaffShifts.Include(x => x.Shift).Include(x => x.Status)
            .SingleOrDefaultAsync(x => x.StaffId == staffId && x.ShiftId == shiftId && x.WorkDate == date.Date, ct);

    public Task<StaffShiftStatus?> GetStatusAsync(string code, CancellationToken ct) =>
        _context.StaffShiftStatuses.SingleOrDefaultAsync(x => x.Code == code, ct);

    public Task<List<StaffShift>> GetPotentialOverlapsAsync(int staffId, DateTime from, DateTime to, int? excludeId, CancellationToken ct) =>
        _context.StaffShifts.Include(x => x.Shift).Include(x => x.Status)
            .Where(x => x.StaffId == staffId && x.WorkDate >= from.Date && x.WorkDate <= to.Date
                && (!excludeId.HasValue || x.StaffShiftId != excludeId.Value))
            .ToListAsync(ct);

    public Task<List<StaffShift>> GetTemplateSchedulesAsync(int shiftId, CancellationToken ct) =>
        _context.StaffShifts.Include(x => x.Shift).Include(x => x.Status)
            .Where(x => x.ShiftId == shiftId).ToListAsync(ct);

    public void Add(Shift shift) => _context.Shifts.Add(shift);
    public void Add(StaffShift shift) => _context.StaffShifts.Add(shift);
    public void Add(AuditLog audit) => _context.AuditLogs.Add(audit);
    public Task<int> SaveChangesAsync(CancellationToken ct) => _context.SaveChangesAsync(ct);
}
