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
        CancellationToken cancellationToken = default,
        Guid? contextId = null);

    Task<DashboardAnalysisContextDto> CreateContextAsync(
        AdminActorContext actor,
        DashboardContextRequestDto request,
        CancellationToken cancellationToken = default);

    Task<DashboardAnalysisContextDto> GetContextAsync(
        AdminActorContext actor,
        Guid contextId,
        CancellationToken cancellationToken = default);

    Task<DashboardVM> GetDashboardAsync(DashboardRequest request);

    Task<DashboardAnalyticsResponse> GetAnalyticsAsync(
        DashboardAnalyticsWidget widget,
        DashboardAnalyticsFilter filter,
        CancellationToken cancellationToken = default);

    Task<DashboardAnalyticsBatchResponse> GetAnalyticsBatchAsync(
        AdminActorContext actor,
        IReadOnlyCollection<DashboardAnalyticsWidget> widgets,
        DashboardAnalyticsFilter filter,
        string period = "Current",
        CancellationToken cancellationToken = default);

    Task WriteAnalysisAuditAsync(
        int staffId,
        DashboardAnalysisAuditDto audit,
        CancellationToken cancellationToken = default);
}
