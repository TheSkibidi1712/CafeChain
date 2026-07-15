namespace CafeChain.Models.Drinks
{
    public class DrinkSizePriceAudit
    {
        public int DrinkSizePriceAuditId { get; set; }
        public int DrinkSizeId { get; set; }
        public decimal OldPrice { get; set; }
        public decimal NewPrice { get; set; }
        public int ActorStaffId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string CostStatus { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }

        public virtual DrinkSize DrinkSize { get; set; } = null!;
    }
}
