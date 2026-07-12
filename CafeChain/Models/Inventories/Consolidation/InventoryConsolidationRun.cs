using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;

namespace CafeChain.Models.Inventories.Consolidation
{
    /// <summary>
    /// Issue #123 — durable consolidation / zero-legacy no-op audit run.
    /// </summary>
    public class InventoryConsolidationRun
    {
        public int InventoryConsolidationRunId { get; set; }

        public int StoreId { get; set; }

        public Guid RequestKey { get; set; }

        public InventoryConsolidationRunType RunType { get; set; }

        public InventoryConsolidationRunStatus Status { get; set; }

        public string ManifestVersion { get; set; } = string.Empty;

        public string QueryContractVersion { get; set; } = string.Empty;

        public string ManifestHash { get; set; } = string.Empty;

        public string? DryRunHash { get; set; }

        public string EnvironmentFingerprint { get; set; } = string.Empty;

        public string? ManifestJson { get; set; }

        public string? ReportJson { get; set; }

        public int RequestedByStaffId { get; set; }

        public int? ApprovedByStaffId { get; set; }

        public int? ExecutedByStaffId { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? DryRunAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public string? FailureCode { get; set; }

        public string? FailureDetails { get; set; }

        public decimal BeforeAvailableTotal { get; set; }

        public decimal BeforeReservedTotal { get; set; }

        public decimal AfterAvailableTotal { get; set; }

        public decimal AfterReservedTotal { get; set; }

        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public virtual Store Store { get; set; } = null!;
        public virtual Staff RequestedByStaff { get; set; } = null!;
        public virtual Staff? ApprovedByStaff { get; set; }
        public virtual Staff? ExecutedByStaff { get; set; }
        public virtual ICollection<InventoryConsolidationLine> Lines { get; set; }
            = new List<InventoryConsolidationLine>();
    }
}
