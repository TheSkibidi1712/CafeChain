using CafeChain.Application.DTOs.Inventories.Cutover;
using CafeChain.Application.Results;
using CafeChain.Models.Enums.Inventory;

namespace CafeChain.Application.Interfaces.Inventories
{
    /// <summary>Issue #124 — read-only cutover reconciliation + activation orchestration.</summary>
    public interface ICutoverReconciliationService
    {
        Task<ServiceResult<CutoverReconciliationReport>> ReconcileStoreAsync(
            int storeId,
            CancellationToken cancellationToken = default);

        Task<ServiceResult<CutoverActivationResult>> ActivatePreparedItemAsync(
            CutoverActivationRequest request,
            CancellationToken cancellationToken = default);

        Task<ServiceResult<CutoverActivationResult>> RollbackToBlockedAsync(
            int storeId,
            byte[] expectedRowVersion,
            InventoryWriterMode expectedMode,
            string reason,
            int actorAccountId,
            CancellationToken cancellationToken = default);

        Task<ServiceResult<CutoverGraduationSummary>> BuildGraduationSummaryAsync(
            CancellationToken cancellationToken = default);
    }
}
