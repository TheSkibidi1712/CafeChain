using System.ComponentModel.DataAnnotations;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;

namespace CafeChain.Models.Inventories.Production
{
    /// <summary>
    /// Issue #119 / 114B — durable production intent only (no stock mutation until 114C/#120).
    /// </summary>
    public class ProductionRun
    {
        public int ProductionRunId { get; set; }

        public int StoreId { get; set; }

        /// <summary>Exact Recipe version PK selected at confirm time.</summary>
        public int RecipeId { get; set; }

        public decimal RequestedRunCount { get; set; }

        /// <summary>Client-generated UUID; unique with StoreId.</summary>
        public Guid RequestKey { get; set; }

        /// <summary>SHA-256 hex of versioned immutable inputs.</summary>
        public string RequestFingerprint { get; set; } = string.Empty;

        public ProductionRunStatus Status { get; set; }

        public string? Notes { get; set; }

        public int CreatedByStaffId { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime ConfirmedAt { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public virtual Store Store { get; set; } = null!;
        public virtual Recipe Recipe { get; set; } = null!;
        public virtual Staff CreatedByStaff { get; set; } = null!;
    }
}
