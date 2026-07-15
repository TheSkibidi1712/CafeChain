namespace CafeChain.Models.Drinks
{
    public class DrinkSizeToppingPolicy
    {
        public int DrinkSizeToppingPolicyId { get; set; }
        public int DrinkSizeId { get; set; }
        public int ToppingId { get; set; }
        public bool IsDefaultSelected { get; set; }
        public string PriceTreatment { get; set; } = string.Empty;
        public string CostTreatment { get; set; } = string.Empty;
        public decimal QuantityPerDrink { get; set; }
        public bool IsActive { get; set; }
        public int CreatedByStaffId { get; set; }
        public int? UpdatedByStaffId { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public virtual DrinkSize DrinkSize { get; set; } = null!;
        public virtual Topping Topping { get; set; } = null!;
    }
}
