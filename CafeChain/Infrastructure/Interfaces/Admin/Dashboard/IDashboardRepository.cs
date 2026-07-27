using CafeChain.Application.DTOs.Admin.Dashboard;

namespace CafeChain.Infrastrusture.Interfaces.Admin.Dashboard;

public interface IDashboardRepository
{
    Task<IReadOnlyList<DashboardStoreOptionDto>> GetStoreOptionsAsync(
        IReadOnlyCollection<int> allowedStoreIds,
        CancellationToken cancellationToken = default);

    Task<ExecutiveDashboardData> GetExecutiveAsync(DashboardFilterDto filter, IReadOnlyCollection<int> storeIds, CancellationToken cancellationToken = default);
    Task<OperationsDashboardData> GetOperationsAsync(DashboardFilterDto filter, IReadOnlyCollection<int> storeIds, CancellationToken cancellationToken = default);
    Task<InventoryDashboardData> GetInventoryAsync(DashboardFilterDto filter, IReadOnlyCollection<int> storeIds, CancellationToken cancellationToken = default);
    Task<ProcurementDashboardData> GetProcurementAsync(DashboardFilterDto filter, IReadOnlyCollection<int> storeIds, CancellationToken cancellationToken = default);
    Task<ProductDashboardData> GetProductAsync(DashboardFilterDto filter, IReadOnlyCollection<int> storeIds, CancellationToken cancellationToken = default);
    Task<WorkforceDashboardData> GetWorkforceAsync(DashboardFilterDto filter, IReadOnlyCollection<int> storeIds, CancellationToken cancellationToken = default);
    Task WriteAnalysisAuditAsync(
        int staffId,
        DashboardAnalysisAuditDto audit,
        CancellationToken cancellationToken = default);
}
