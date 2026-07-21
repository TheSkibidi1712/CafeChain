using CafeChain.Application.Results;
using CafeChain.ViewModels.StaffHub;

namespace CafeChain.Application.Interfaces.StaffHub;

public interface IStaffScheduleService
{
    Task<ServiceResult<StaffHubScheduleVM>> GetAsync(int staffId, DateTime selectedDate, CancellationToken cancellationToken = default);
}
