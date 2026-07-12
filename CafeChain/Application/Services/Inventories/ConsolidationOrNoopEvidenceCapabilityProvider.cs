using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.DTOs.Inventories.Consolidation;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Data;
using CafeChain.Models.Enums.Inventory;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CafeChain.Application.Services.Inventories
{
    /// <summary>
    /// Issue #123 — CONSOLIDATION_OR_NOOP_EVIDENCE readiness is per-Store and driven only by
    /// durable Completed consolidation / zero-legacy no-op runs. Never mutates mode or data.
    /// </summary>
    public sealed class ConsolidationOrNoopEvidenceCapabilityProvider
        : IInventoryWriterCapabilityProvider, IStoreScopedInventoryWriterCapabilityProvider
    {
        public const string ContractVersion = LegacyBtpConsolidationConstants.QueryContractVersion;

        private readonly AppDbContext _context;

        public ConsolidationOrNoopEvidenceCapabilityProvider(AppDbContext context)
        {
            _context = context;
        }

        public string CapabilityId => InventoryWriterCapabilityIds.ConsolidationOrNoopEvidence;

        /// <summary>
        /// Static GetStatus is intentionally NOT Ready — callers must use store-scoped evaluation.
        /// </summary>
        public InventoryWriterCapabilityStatus GetStatus()
            => new(
                CapabilityId,
                ContractVersion,
                Ready: false,
                BlockerCode: "CONSOLIDATION_EVIDENCE_REQUIRED",
                BlockerMessage: "Capability CONSOLIDATION_OR_NOOP_EVIDENCE yêu cầu durable evidence theo từng cửa hàng.");

        public async Task<InventoryWriterCapabilityStatus> GetStatusForStoreAsync(
            int storeId,
            CancellationToken cancellationToken = default)
        {
            var evidence = await _context.InventoryConsolidationRuns
                .AsNoTracking()
                .Where(x => x.StoreId == storeId
                    && x.Status == InventoryConsolidationRunStatus.Completed
                    && x.QueryContractVersion == ContractVersion
                    && (x.RunType == InventoryConsolidationRunType.AuditNoOp
                        || x.RunType == InventoryConsolidationRunType.Consolidation))
                .OrderByDescending(x => x.CompletedAt)
                .ThenByDescending(x => x.InventoryConsolidationRunId)
                .FirstOrDefaultAsync(cancellationToken);

            if (evidence == null)
            {
                return new InventoryWriterCapabilityStatus(
                    CapabilityId,
                    ContractVersion,
                    Ready: false,
                    BlockerCode: "CONSOLIDATION_EVIDENCE_MISSING",
                    BlockerMessage: $"Cửa hàng {storeId} chưa có evidence consolidation/no-op Completed (v{ContractVersion}).");
            }

            if (!EvidenceConfirmsClearCutover(evidence.ReportJson, evidence.RunType))
            {
                return new InventoryWriterCapabilityStatus(
                    CapabilityId,
                    ContractVersion,
                    Ready: false,
                    BlockerCode: "CONSOLIDATION_EVIDENCE_INCOMPLETE",
                    BlockerMessage: $"Evidence run {evidence.InventoryConsolidationRunId} không xác nhận cutover sạch.");
            }

            return new InventoryWriterCapabilityStatus(
                CapabilityId,
                ContractVersion,
                Ready: true);
        }

        private static bool EvidenceConfirmsClearCutover(string? reportJson, InventoryConsolidationRunType runType)
        {
            if (string.IsNullOrWhiteSpace(reportJson))
                return false;

            try
            {
                using var doc = JsonDocument.Parse(reportJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("noUnresolvedConsolidatableLegacy", out var flag) && flag.ValueKind == JsonValueKind.True)
                    return true;

                if (root.TryGetProperty("isNoOpEligible", out var noOp) && noOp.ValueKind == JsonValueKind.True
                    && runType == InventoryConsolidationRunType.AuditNoOp)
                    return true;

                if (root.TryGetProperty("conservationVerified", out var cons) && cons.ValueKind == JsonValueKind.True
                    && runType == InventoryConsolidationRunType.Consolidation)
                    return true;
            }
            catch (JsonException)
            {
                return false;
            }

            return false;
        }
    }
}
