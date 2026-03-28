using CafeChain.Models.Stores;

namespace CafeChain.Models.Inventories
{
    public class StockTakeDetail
    {
        public int StockTakeDetailId { get; set; }

        public int StockTakeId { get; set; }
        public int StoreInventoryId { get; set; }

        public decimal SystemQty { get; set; }   // tồn lý thuyết
        public decimal ActualQty { get; set; }   // tồn thực tế

        // Navigation
        public virtual StockTake StockTake { get; set; }
        public virtual StoreInventory StoreInventory { get; set; }
    }
}
