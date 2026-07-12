using CafeChain.Application.DTOs.Admin.Production;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Admin.Production
{
    /// <summary>Issue #119 — durable production intent + idempotency (no stock writer).</summary>
    public interface IProductionRunService
    {
        Task<ServiceResult<ProductionRunResultDto>> CreateAndConfirmAsync(
            CreateAndConfirmProductionRunRequest request,
            int staffId,
            int staffHomeStoreId);

        Task<IReadOnlyList<ProductionRunHistoryItemDto>> GetRecentAsync(int storeId, int take = 5);
    }
}
