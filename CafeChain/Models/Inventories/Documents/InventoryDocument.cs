using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Debts;
using CafeChain.Models.Inventories.Suppliers;
using CafeChain.Models.Inventories.Transactions;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using System.ComponentModel.DataAnnotations.Schema;
using CafeChain.Models.Inventories.Approvals;

namespace CafeChain.Models.Inventories.Documents
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
        public string? RequestKey { get; set; }

        public bool IsProcessing { get; set; }

        public byte[] RowVersion { get; set; } = [];

        public DateTime? ConfirmedAt { get; set; }

        public int? ConfirmedBy { get; set; }

        // ================= NGHIỆP VỤ =================

        public InventoryDocumentPurpose Purpose { get; set; } // 🔥 Mục đích

        // ================= ĐỐI TƯỢNG =================

        public InventoryPartnerType PartnerType { get; set; } // 🔥 loại đối tượng
        public int? PartnerId { get; set; }                  // 🔥 id liên kết
        public string? PartnerName { get; set; }             // 🔥 fallback

        // ================= NHẬP NCC =================

        public int? SupplierId { get; set; }
        public string? Note { get; set; }
        public string? NegativeReason { get; set; }

        // ===== MONEY =====
        public decimal? TotalAmount { get; set; }
        public decimal? VatAmount { get; set; }
        public decimal? FinalAmount { get; set; }

        // ================= NAVIGATION =================

        public virtual Store Store { get; set; }
        public virtual Staff Staff { get; set; }
        public virtual Supplier Supplier { get; set; }

        public virtual ICollection<InventoryDocumentDetail> Details { get; set; } = new List<InventoryDocumentDetail>();
        public virtual ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();
        public virtual ICollection<InventoryDebt> Debts { get; set; } = new List<InventoryDebt>();
        public virtual InventoryDocumentSnapshot? Snapshot { get; set; }
        public virtual InventoryNegativeApproval? NegativeApproval { get; set; }
    }
}
