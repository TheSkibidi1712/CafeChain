using CafeChain.Models.Orders;

namespace CafeChain.Models.Inventories.Costing
{
    /// <summary>
    /// Durable incomplete-cost evidence for sales deduction (#133 Option B).
    /// Quantity may still be deducted; COGS remains non-zero-faked.
    /// </summary>
    public class SalesCostGap
    {
        public int SalesCostGapId { get; set; }

        public int OrderId { get; set; }
        public int OrderDetailId { get; set; }
        public int? OrderToppingId { get; set; }

        public int? IngredientId { get; set; }
        public int? PreparedItemId { get; set; }

        public decimal RequiredQuantity { get; set; }
        public decimal AllocatedCostQuantity { get; set; }
        public decimal MissingCostQuantity { get; set; }

        public int? BaseUnitId { get; set; }

        public string ReasonCode { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; }

        public virtual Order Order { get; set; } = null!;
        public virtual OrderDetail OrderDetail { get; set; } = null!;
        public virtual OrderTopping? OrderTopping { get; set; }
    }
}
