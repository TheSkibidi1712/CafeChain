using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;

namespace CafeChain.Models.Inventories
{
    public class InventoryDocument
    {
        public int InventoryDocumentId { get; set; }

        public string Code { get; set; }

        public int StoreId { get; set; }
        public int StaffId { get; set; }

        public DateTime DocumentDate { get; set; }

        public InventoryDocumentType Type { get; set; }
        public InventoryDocumentStatus Status { get; set; }

        // ================= NGHIỆP VỤ =================

        public InventoryDocumentPurpose Purpose { get; set; } // 🔥 Mục đích

        // ================= ĐỐI TƯỢNG =================

        public InventoryPartnerType PartnerType { get; set; } // 🔥 loại đối tượng
        public int? PartnerId { get; set; }                  // 🔥 id liên kết
        public string? PartnerName { get; set; }             // 🔥 fallback

        // ================= NHẬP NCC =================

        public int? SupplierId { get; set; }

        // ================= REVERSAL =================

        public int? RefDocumentId { get; set; } // phiếu gốc
        public bool IsReversal { get; set; }    // có phải phiếu đảo

        public string? Note { get; set; }

        // ================= NAVIGATION =================

        public virtual Store Store { get; set; }
        public virtual Staff Staff { get; set; }
        public virtual Supplier Supplier { get; set; }

        public virtual ICollection<InventoryDocumentDetail> Details { get; set; } = new List<InventoryDocumentDetail>();
        public virtual ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();
    }
}
