using System.ComponentModel.DataAnnotations;
using CafeChain.Models.Stores;

namespace CafeChain.Models.Inventories.Suppliers
{
    public class SupplierStore
    {
        public int SupplierStoreId { get; set; }
        public int SupplierId { get; set; }
        public int StoreId { get; set; }
        public bool Active { get; set; }
        public int? LeadTimeOverrideDays { get; set; }
        public string? DeliverySchedule { get; set; }
        public string? Note { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public virtual Supplier Supplier { get; set; } = null!;
        public virtual Store Store { get; set; } = null!;
    }
}
