using CafeChain.Models.Permissions;
using CafeChain.Models.Staffs;

namespace CafeChain.Infrastructure.Interfaces.Admin.Staffs;

public interface IShiftOptimizationRepository
{
    Task<List<int>> GetActiveStoreIdsAsync(CancellationToken cancellationToken);
    Task<string?> GetStoreNameAsync(int storeId, CancellationToken cancellationToken);
    Task<List<Staff>> GetStaffsAsync(int storeId, CancellationToken cancellationToken);
    Task<List<Role>> GetRolesAsync(CancellationToken cancellationToken);
    Task<Staff?> GetStaffAsync(int staffId, CancellationToken cancellationToken);
    Task<List<Shift>> GetShiftsAsync(int storeId, CancellationToken cancellationToken);
    Task<List<StaffShift>> GetSchedulesAsync(int storeId, DateTime from, DateTime to, CancellationToken cancellationToken);
    Task<List<StaffAvailabilityRule>> GetAvailabilityAsync(int storeId, DateTime from, DateTime to, CancellationToken cancellationToken);
    Task<List<StaffAvailabilityException>> GetExceptionsAsync(int storeId, DateTime from, DateTime to, CancellationToken cancellationToken);
    Task<List<StaffTimeOff>> GetTimeOffsAsync(int storeId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);
    Task<List<StaffWorkConstraint>> GetConstraintsAsync(int storeId, DateTime from, DateTime to, CancellationToken cancellationToken);
    Task<List<StoreStaffingRequirement>> GetRequirementsAsync(int storeId, DateTime from, DateTime to, CancellationToken cancellationToken);
    void Add(object entity);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
