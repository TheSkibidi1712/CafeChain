namespace CafeChain.Models.Inventories.Stock
{
    public class StoreInventorySnapshot
    {
        public int StoreInventorySnapshotId { get; set; }

        public int StoreId { get; set; }
        public int IngredientId { get; set; }

        public decimal Quantity { get; set; }
        public decimal AvgCost { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
