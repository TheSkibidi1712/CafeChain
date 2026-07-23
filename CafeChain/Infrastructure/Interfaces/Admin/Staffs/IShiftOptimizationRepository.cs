using CafeChain.Models.Inventories.Auditing;
using CafeChain.Models.Staffs;

namespace CafeChain.Infrastructure.Interfaces.Admin.Staffs;

public interface IShiftOptimizationRepository
{
    Task<List<Staff>> GetStaffsAsync(int storeId, CancellationToken cancellationToken);
    Task<Staff?> GetStaffAsync(int staffId, CancellationToken cancellationToken);
    Task<List<Shift>> GetShiftsAsync(int storeId, CancellationToken cancellationToken);
    Task<List<StaffShift>> GetSchedulesAsync(int storeId, DateTime from, DateTime to, CancellationToken cancellationToken);
    Task<List<StaffAvailabilityRule>> GetAvailabilityAsync(int storeId, DateTime from, DateTime to, CancellationToken cancellationToken);
    Task<List<StaffAvailabilityException>> GetExceptionsAsync(int storeId, DateTime from, DateTime to, CancellationToken cancellationToken);
    Task<List<StaffTimeOff>> GetTimeOffsAsync(int storeId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);
    Task<List<StaffWorkConstraint>> GetConstraintsAsync(int storeId, DateTime from, DateTime to, CancellationToken cancellationToken);
    Task<List<StoreStaffingRequirement>> GetRequirementsAsync(int storeId, DateTime from, DateTime to, CancellationToken cancellationToken);
    Task<ScheduleOptimizationProposal?> GetProposalAsync(Guid id, bool tracking, CancellationToken cancellationToken);
    Task<StaffShiftStatus?> GetScheduledStatusAsync(CancellationToken cancellationToken);
    void Add(object entity);
    Task BeginTransactionAsync(CancellationToken cancellationToken);
    Task CommitAsync(CancellationToken cancellationToken);
    Task RollbackAsync(CancellationToken cancellationToken);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
