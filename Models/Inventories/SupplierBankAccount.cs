namespace CafeChain.Models.Inventories
{
    public class SupplierBankAccount
    {
        public int SupplierBankAccountId { get; set; }

        public int SupplierId { get; set; }
        public virtual Supplier Supplier { get; set; }

        public string BankName { get; set; }        // Tên ngân hàng
        public string AccountNumber { get; set; }   // Số tài khoản
        public string AccountHolder { get; set; }   // Chủ tài khoản

        public bool IsPrimary { get; set; }
    }
}
