namespace CafeChain.Models.Inventories
{
    public class Supplier
    {
        public int SupplierId { get; set; }

        public string? Code { get; set; } // SUP00001
        public string? Name { get; set; }

        public string? TaxCode { get; set; }   // Mã số thuế
        public string? Website { get; set; }   // Website

        public string? Address { get; set; }   // 🔥 Giữ 1 địa chỉ chính

        public decimal DebtAmount { get; set; } // Công nợ hiện tại

        public bool Active { get; set; }

        // ================= RELATIONS =================

        // 🔥 Nhiều số điện thoại
        public virtual ICollection<SupplierPhone> Phones { get; set; } = new List<SupplierPhone>();

        // 🔥 Nhiều tài khoản ngân hàng
        public virtual ICollection<SupplierBankAccount> BankAccounts { get; set; } = new List<SupplierBankAccount>();

        // 🔥 Nhiều người liên hệ
        public virtual ICollection<SupplierContact> Contacts { get; set; } = new List<SupplierContact>();

        // 🔥 Giá nguyên liệu theo NCC
        public virtual ICollection<IngredientSupplier> IngredientSuppliers { get; set; } = new List<IngredientSupplier>();

        // 🔥 Phiếu nhập hàng từ NCC
        public virtual ICollection<InventoryDocument> InventoryDocuments { get; set; } = new List<InventoryDocument>();

    }
}
