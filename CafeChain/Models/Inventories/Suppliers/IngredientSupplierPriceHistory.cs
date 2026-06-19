namespace CafeChain.Models.Inventories.Suppliers
{
    public class IngredientSupplierPriceHistory
    {
        public int IngredientSupplierPriceHistoryId { get; set; }

        public int IngredientSupplierId { get; set; }

        public decimal Price { get; set; }

        public DateTime EffectiveDate { get; set; }

        public bool IsCurrent { get; set; }

        public string? Note { get; set; }

        public virtual IngredientSupplier IngredientSupplier { get; set; }
    }
}
