using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Dashboard;

namespace CafeChain.Application.Interfaces.Admin.Dashboard;

public interface IDashboardAuthorizationService
{
    Task<DashboardAuthorizationDto> GetAccessAsync(
        AdminActorContext actor, CancellationToken cancellationToken = default);
    Task<DashboardAuthorizationDto> AuthorizeSectionAsync(
        AdminActorContext actor, DashboardSection section,
        CancellationToken cancellationToken = default);
    Task<DashboardAuthorizationDto> AuthorizeWidgetsAsync(
        AdminActorContext actor, IReadOnlyCollection<DashboardAnalyticsWidget> widgets,
        CancellationToken cancellationToken = default);
}
