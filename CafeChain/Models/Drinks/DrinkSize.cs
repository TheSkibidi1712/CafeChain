namespace CafeChain.Models.Drinks
{
    public class DrinkSize
    {
        public int DrinkSizeId { get; set; }
        public int DrinkId { get; set; }
        public int SizeId { get; set; }
        public decimal Price { get; set; }
        public bool Active { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public virtual Drink Drink { get; set; }
        public virtual Size Size { get; set; }
        public virtual ICollection<DrinkSizeToppingPolicy> ToppingPolicies { get; set; } = new List<DrinkSizeToppingPolicy>();
        public virtual ICollection<DrinkSizePriceAudit> PriceAudits { get; set; } = new List<DrinkSizePriceAudit>();
    }
}
