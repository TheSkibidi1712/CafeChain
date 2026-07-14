using System.ComponentModel.DataAnnotations;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Orders;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;

namespace CafeChain.Models.Inventories.Refunds
{
    /// <summary>
    /// Full-order cash refund (#134). Order stays Completed; Payment becomes Refunded.
    /// </summary>
    public class OrderRefund
    {
        public int OrderRefundId { get; set; }

        public int OrderId { get; set; }
        public int StoreId { get; set; }

        /// <summary>Client idempotency key (GUID).</summary>
        public Guid RefundKey { get; set; }

        public OrderRefundStatus Status { get; set; }

        /// <summary>1 = Cash/manual only in #134.</summary>
        public int PaymentMethodId { get; set; }

        public string Reason { get; set; } = string.Empty;

        public decimal RefundAmount { get; set; }

        public SalesCostStatus CostStatus { get; set; } = SalesCostStatus.Pending;

        /// <summary>Sum of known reversed allocation costs; null if incomplete contract requires.</summary>
        public decimal? ReversedCogs { get; set; }

        public RefundInventoryReversalStatus InventoryReversalStatus { get; set; }
            = RefundInventoryReversalStatus.Pending;

        public DateTime RequestedAtUtc { get; set; }
        public int RequestedByStaffId { get; set; }

        public DateTime? ProcessingAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        public int? CompletedByStaffId { get; set; }

        public string? FailureCode { get; set; }
        public string? FailureMessage { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public virtual Order Order { get; set; } = null!;
        public virtual Store Store { get; set; } = null!;
        public virtual Staff RequestedByStaff { get; set; } = null!;
        public virtual Staff? CompletedByStaff { get; set; }
    }
}
