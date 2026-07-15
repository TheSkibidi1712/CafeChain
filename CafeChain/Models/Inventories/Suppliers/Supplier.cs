using CafeChain.Models.Inventories.Documents;
using System.ComponentModel.DataAnnotations;

namespace CafeChain.Models.Inventories.Suppliers
{
    public class Supplier
    {
        public int SupplierId { get; set; }

        public string? Code { get; set; }
        public string? Name { get; set; }

        public string? Address { get; set; }
        public bool Active { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? Note { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        // ================= RELATIONS =================

        // hotline công ty
        public virtual ICollection<SupplierPhone> Phones { get; set; }
            = new List<SupplierPhone>();

        // người liên hệ
        public virtual ICollection<SupplierContact> Contacts { get; set; }
            = new List<SupplierContact>();

        // nguyên liệu NCC cung cấp
        public virtual ICollection<IngredientSupplier> IngredientSuppliers { get; set; }
            = new List<IngredientSupplier>();

        // phiếu nhập
        public virtual ICollection<InventoryDocument> InventoryDocuments { get; set; }
            = new List<InventoryDocument>();


    }
}
