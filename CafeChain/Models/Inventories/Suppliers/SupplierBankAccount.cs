namespace CafeChain.Models.Inventories.Suppliers
{
    public class SupplierBankAccount
    {
        public int SupplierBankAccountId { get; set; }

        public int SupplierId { get; set; }

        public string? BankName { get; set; }

        public string? AccountNumber { get; set; }

        public string? AccountHolder { get; set; }

        public string? Branch { get; set; }

        public bool IsPrimary { get; set; }

        public bool Active { get; set; }

        public virtual Supplier Supplier { get; set; }
    }
}
