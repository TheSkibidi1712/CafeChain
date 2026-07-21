using CafeChain.Models.Staffs;

namespace CafeChain.Infrastructure.Interfaces.StaffHub;

public interface IStaffScheduleRepository
{
    Task<Staff?> GetStaffScheduleAsync(int staffId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken);
}
