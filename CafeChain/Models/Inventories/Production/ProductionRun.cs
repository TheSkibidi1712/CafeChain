using System.ComponentModel.DataAnnotations;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;

namespace CafeChain.Models.Inventories.Production
{
    /// <summary>
    /// Production intent (#119) and stock completion (#120) with actual valuation snapshot (#132).
    /// StockApplied is derived: Status == Completed.
    /// </summary>
    public class ProductionRun
    {
        public int ProductionRunId { get; set; }

        public int StoreId { get; set; }

        /// <summary>Exact Recipe version PK (never re-resolved on execute).</summary>
        public int RecipeId { get; set; }

        public decimal RequestedRunCount { get; set; }

        /// <summary>1 = legacy decimal-run contract; 2 = Restock-driven integer batch contract.</summary>
        public int ContractVersion { get; set; } = 1;

        public int? PlannedBatchCount { get; set; }
        public decimal? ExpectedOutputPerBatchBase { get; set; }
        public decimal? ExpectedOutputBase { get; set; }
        public int? OutputBaseUnitId { get; set; }
        public decimal? YieldVarianceTolerancePercent { get; set; }

        public Guid RequestKey { get; set; }

        public string RequestFingerprint { get; set; } = string.Empty;

        public ProductionRunStatus Status { get; set; }

        public string? Notes { get; set; }

        public int CreatedByStaffId { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime ConfirmedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public int? CompletedByStaffId { get; set; }

        public int? ReleasedByStaffId { get; set; }
        public DateTime? ReleasedAtUtc { get; set; }
        public int? StartedByStaffId { get; set; }
        public DateTime? StartedAtUtc { get; set; }
        public int? ActualRecordedByStaffId { get; set; }
        public DateTime? ActualRecordedAtUtc { get; set; }
        public int? VarianceApprovedByStaffId { get; set; }
        public DateTime? VarianceApprovedAtUtc { get; set; }
        public string? VarianceReason { get; set; }

        /// <summary>Issue #132 — Pending on confirm; Complete after successful valuation.</summary>
        public ProductionValuationStatus ValuationStatus { get; set; } = ProductionValuationStatus.Pending;

        /// <summary>Sum of actual FIFO input costs (base currency).</summary>
        public decimal? TotalInputCost { get; set; }

        /// <summary>TotalInputCost / normalized output base quantity.</summary>
        public decimal? OutputUnitCost { get; set; }

        public DateTime? ValuedAtUtc { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public virtual Store Store { get; set; } = null!;
        public virtual Recipe Recipe { get; set; } = null!;
        public virtual Staff CreatedByStaff { get; set; } = null!;
        public virtual Staff? CompletedByStaff { get; set; }
        public virtual ICollection<ProductionRunInputActual> ActualInputs { get; set; } = new List<ProductionRunInputActual>();
        public virtual ProductionRunOutput? ActualOutput { get; set; }
        public virtual ICollection<ProductionRunTransition> Transitions { get; set; } = new List<ProductionRunTransition>();
    }
}
