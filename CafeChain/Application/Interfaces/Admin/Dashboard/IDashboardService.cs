using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Dashboard;
using CafeChain.ViewModels.Admin.Dashboard;

namespace CafeChain.Application.Interfaces.Admin.Dashboard;

public interface IDashboardService
{
    Task<DashboardPageDto> GetPageAsync(
        AdminActorContext actor,
        DashboardFilterDto filter,
        CancellationToken cancellationToken = default);

    Task<object> GetSectionAsync(
        AdminActorContext actor,
        DashboardSection section,
        DashboardFilterDto filter,
        CancellationToken cancellationToken = default);

    Task<DashboardVM> GetDashboardAsync(DashboardRequest request);

    Task<DashboardAnalyticsResponse> GetAnalyticsAsync(
        DashboardAnalyticsWidget widget,
        DashboardAnalyticsFilter filter,
        CancellationToken cancellationToken = default);
}
