using CafeChain.Models.Enums.Inventory.Suppliers;
using CafeChain.Models.Inventories.Documents;

namespace CafeChain.Models.Inventories.Suppliers
{
    public class Supplier
    {
        public int SupplierId { get; set; }

        public string? Code { get; set; }
        public string? Name { get; set; }

        public string? TaxCode { get; set; }
        public string? Website { get; set; }

        public string? Address { get; set; }

        public decimal DebtAmount { get; set; }

        public SupplierStatus Status { get; set; }

        public bool Active { get; set; }

        public DateTime CreatedAt { get; set; }

        public string? Note { get; set; }

        // ================= RELATIONS =================

        // hotline công ty
        public virtual ICollection<SupplierPhone> Phones { get; set; }
            = new List<SupplierPhone>();

        // tài khoản ngân hàng
        public virtual ICollection<SupplierBankAccount> BankAccounts { get; set; }
            = new List<SupplierBankAccount>();

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
