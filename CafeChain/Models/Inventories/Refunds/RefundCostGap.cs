using CafeChain.Models.Inventories.Costing;

namespace CafeChain.Models.Inventories.Refunds
{
    /// <summary>
    /// Durable unknown-COGS residual carried from original SalesCostGap on refund (#134).
    /// </summary>
    public class RefundCostGap
    {
        public int RefundCostGapId { get; set; }

        public int OrderRefundId { get; set; }
        public int SalesCostGapId { get; set; }

        public int? IngredientId { get; set; }
        public int? PreparedItemId { get; set; }

        public decimal Quantity { get; set; }
        public int? BaseUnitId { get; set; }

        public string ReasonCode { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; }

        public virtual OrderRefund OrderRefund { get; set; } = null!;
        public virtual SalesCostGap SalesCostGap { get; set; } = null!;
    }
}
