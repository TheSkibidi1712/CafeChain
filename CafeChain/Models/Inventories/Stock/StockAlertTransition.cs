using System.ComponentModel.DataAnnotations;
using CafeChain.Models.Staffs;

namespace CafeChain.Models.Inventories.Stock
{
    public class StockAlertTransition
    {
        public int StockAlertTransitionId { get; set; }
        public int StockAlertId { get; set; }
        public string? PreviousStatus { get; set; }
        public string NewStatus { get; set; } = string.Empty;
        public string? PreviousAlertType { get; set; }
        public string NewAlertType { get; set; } = string.Empty;
        public string? PreviousSeverity { get; set; }
        public string NewSeverity { get; set; } = string.Empty;
        public decimal OnHandSnapshot { get; set; }
        public decimal ReservedSnapshot { get; set; }
        public decimal AvailableSnapshot { get; set; }
        public decimal? MinLevelSnapshot { get; set; }
        public string SourceType { get; set; } = string.Empty;
        public int? SourceId { get; set; }
        public string? Reason { get; set; }
        public int? ActorStaffId { get; set; }
        public DateTime CreatedAtUtc { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public virtual StockAlert StockAlert { get; set; } = null!;
        public virtual Staff? ActorStaff { get; set; }
    }
}
