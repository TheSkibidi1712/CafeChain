using CafeChain.Models.Stores;
using CafeChain.Models.Enums.Inventory;
namespace CafeChain.Models.Inventories
{
    public class InventoryTransfer
    {
        public int InventoryTransferId { get; set; }

        // ================= DOCUMENT LINK =================

        public int ExportDocumentId { get; set; }     // phiếu xuất
        public int? ImportDocumentId { get; set; }    // phiếu nhập (có thể null nếu chưa nhận)

        // ================= STORE =================

        public int FromStoreId { get; set; }
        public int ToStoreId { get; set; }

        // ================= TRACKING =================

        public decimal TotalExportQty { get; set; }
        public decimal TotalReceivedQty { get; set; }

        public InventoryTransferStatus Status { get; set; } // PENDING / PARTIAL / COMPLETED

        public DateTime CreatedAt { get; set; }

        // ================= NAVIGATION =================

        public virtual InventoryDocument ExportDocument { get; set; }
        public virtual InventoryDocument ImportDocument { get; set; }

        public virtual Store FromStore { get; set; }
        public virtual Store ToStore { get; set; }
        public virtual ICollection<InventoryTransferDetail> Details { get; set; } = new List<InventoryTransferDetail>();
    }
}
