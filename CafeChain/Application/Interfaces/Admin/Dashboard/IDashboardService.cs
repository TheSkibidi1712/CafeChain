using CafeChain.Application.DTOs.Admin.Dashboard;
using CafeChain.ViewModels.Admin.Dashboard;

namespace CafeChain.Application.Interfaces.Admin.Dashboard
{
    public interface IDashboardService
    {
        Task<DashboardVM> GetDashboardAsync(DashboardRequest request);
    }
}
