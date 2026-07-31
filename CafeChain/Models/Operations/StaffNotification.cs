using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;

namespace CafeChain.Models.Operations
{
    /// <summary>
    /// Issue #98 — minimal in-system notification foundation.
    /// Badge/list/mark-read UI deferred to #101.
    /// </summary>
    public class StaffNotification
    {
        public int StaffNotificationId { get; set; }

        public int StoreId { get; set; }

        public int RecipientStaffId { get; set; }

        /// <summary>e.g. STOCK_SHORTAGE_REPORT</summary>
        public string Type { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Body { get; set; } = string.Empty;

        public string Severity { get; set; } = "INFO";

        public string? DeduplicationKey { get; set; }

        /// <summary>
        /// Stable business fingerprint used to distinguish a meaningful state change
        /// from repeated delivery of the same notification.
        /// </summary>
        public string? MeaningfulVersion { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public DateTime? ResolvedAt { get; set; }

        /// <summary>e.g. StockAlert</summary>
        public string EntityType { get; set; } = string.Empty;

        public int EntityId { get; set; }

        public bool IsRead { get; set; }

        public DateTime? ReadAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public bool EmailAttempted { get; set; }

        public bool EmailSent { get; set; }

        /// <summary>Safe truncated error summary — never secrets/stack traces.</summary>
        public string? EmailErrorSummary { get; set; }

        public virtual Store Store { get; set; } = null!;
        public virtual Staff RecipientStaff { get; set; } = null!;
    }
}
