namespace CafeChain.Models.Inventories.Costing
{
    public class InventoryCostLayer
    {
        public int InventoryCostLayerId { get; set; }

        public int IngredientId { get; set; }
        public int StoreId { get; set; }

        public decimal Quantity { get; set; }
        public decimal RemainingQuantity { get; set; }

        public decimal UnitCost { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
