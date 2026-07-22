using CafeChain.Models.Inventories.Auditing;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;

namespace CafeChain.Infrastructure.Interfaces.Admin.Staffs;

public interface IAdminStaffShiftRepository
{
    Task BeginTransactionAsync(CancellationToken cancellationToken);
    Task CommitTransactionAsync(CancellationToken cancellationToken);
    Task RollbackTransactionAsync(CancellationToken cancellationToken);
    Task<Store?> GetStoreAsync(int storeId, CancellationToken cancellationToken);
    Task<List<Staff>> GetStaffsAsync(int storeId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken);
    Task<List<StaffShift>> GetSchedulesAsync(int storeId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken);
    Task<List<Shift>> GetTemplatesAsync(int storeId, bool includeInactive, CancellationToken cancellationToken);
    Task<Staff?> GetStaffAsync(int staffId, CancellationToken cancellationToken);
    Task<Shift?> GetTemplateAsync(int shiftId, CancellationToken cancellationToken);
    Task<StaffShift?> GetScheduleAsync(int staffShiftId, CancellationToken cancellationToken);
    Task<StaffShift?> GetScheduleAsync(int staffId, int shiftId, DateTime workDate, CancellationToken cancellationToken);
    Task<StaffShiftStatus?> GetStatusAsync(string code, CancellationToken cancellationToken);
    Task<List<StaffShift>> GetPotentialOverlapsAsync(int staffId, DateTime fromDate, DateTime toDate, int? excludeId, CancellationToken cancellationToken);
    Task<List<StaffShift>> GetTemplateSchedulesAsync(int shiftId, CancellationToken cancellationToken);
    void Add(Shift shift);
    void Add(StaffShift staffShift);
    void Add(AuditLog auditLog);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
