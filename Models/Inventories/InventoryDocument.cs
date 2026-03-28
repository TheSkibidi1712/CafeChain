using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;

namespace CafeChain.Models.Inventories
{
    public class InventoryDocument
    {
        public int InventoryDocumentId { get; set; }

        public string Code { get; set; } // NKMH/2603/0218

        public int StoreId { get; set; }
        public int StaffId { get; set; }

        public int? SupplierId { get; set; } // chỉ dùng cho nhập

        public DateTime DocumentDate { get; set; }

        public string Type { get; set; }
        // IMPORT, EXPORT, STOCK_TAKE, WASTE

        public string Note { get; set; }

        // Navigation
        public virtual Store Store { get; set; }
        public virtual Staff Staff { get; set; }
        public virtual Supplier Supplier { get; set; }

        public virtual ICollection<InventoryDocumentDetail> Details { get; set; }
    }
}
