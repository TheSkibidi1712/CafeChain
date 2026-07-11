using System.ComponentModel.DataAnnotations;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;

namespace CafeChain.Models.Inventories.Production
{
    /// <summary>
    /// Production intent (#119) and stock completion (#120).
    /// StockApplied is derived: Status == Completed.
    /// </summary>
    public class ProductionRun
    {
        public int ProductionRunId { get; set; }

        public int StoreId { get; set; }

        /// <summary>Exact Recipe version PK (never re-resolved on execute).</summary>
        public int RecipeId { get; set; }

        public decimal RequestedRunCount { get; set; }

        public Guid RequestKey { get; set; }

        public string RequestFingerprint { get; set; } = string.Empty;

        public ProductionRunStatus Status { get; set; }

        public string? Notes { get; set; }

        public int CreatedByStaffId { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime ConfirmedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public int? CompletedByStaffId { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public virtual Store Store { get; set; } = null!;
        public virtual Recipe Recipe { get; set; } = null!;
        public virtual Staff CreatedByStaff { get; set; } = null!;
        public virtual Staff? CompletedByStaff { get; set; }
    }
}
