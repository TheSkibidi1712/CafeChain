using CafeChain.Models.Stores;

namespace CafeChain.Models.Inventories
{
    public class WasteDetail
    {
        public int WasteDetailId { get; set; }

        public int WasteId { get; set; }
        public int StoreInventoryId { get; set; }

        public decimal Quantity { get; set; } // số lượng hủy

        public int WasteReasonId { get; set; } // bắt buộc (AC1)

        // Navigation
        public virtual Waste Waste { get; set; }
        public virtual StoreInventory StoreInventory { get; set; }
        public virtual WasteReason WasteReason { get; set; }
    }
}
