using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.DTOs.Inventories.Consolidation;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Inventories
{
    /// <summary>Issue #123 — auditable legacy BTP inventory consolidation tooling.</summary>
    public interface ILegacyBtpConsolidationService
    {
        /// <summary>Read-only audit. Never mutates inventory, alerts, requests, or mode.</summary>
        Task<ServiceResult<ConsolidationAuditReportDto>> AuditStoreAsync(
            int storeId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Persist durable zero-legacy no-op evidence when audit is eligible and approval is explicit.
        /// Does not change Store WriterMode.
        /// </summary>
        Task<ServiceResult<ConsolidationRunResultDto>> CreateNoOpEvidenceAsync(
            ConsolidationNoOpRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>Validate manifest, re-audit, compute DryRunHash, persist DryRunReady or Blocked.</summary>
        Task<ServiceResult<ConsolidationRunResultDto>> DryRunAsync(
            ConsolidationDryRunRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Atomic execute when Store is Blocked, run is DryRunReady, actor is SystemAdmin/BusinessOwner.
        /// </summary>
        Task<ServiceResult<ConsolidationRunResultDto>> ExecuteAsync(
            ConsolidationExecuteRequest request,
            CancellationToken cancellationToken = default);

        Task<ServiceResult<ConsolidationRunResultDto>> GetRunAsync(
            int storeId,
            Guid requestKey,
            CancellationToken cancellationToken = default);

        Task<ServiceResult<ConsolidationRunResultDto>> GetRunByIdAsync(
            int consolidationRunId,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Capability evaluated per store from durable Completed consolidation / no-op evidence.
    /// </summary>
    public interface IStoreScopedInventoryWriterCapabilityProvider : IInventoryWriterCapabilityProvider
    {
        string CapabilityId { get; }

        Task<InventoryWriterCapabilityStatus> GetStatusForStoreAsync(
            int storeId,
            CancellationToken cancellationToken = default);
    }
}
