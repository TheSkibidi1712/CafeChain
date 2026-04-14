namespace CafeChain.Models.Inventories
{
    public class SupplierPhone
    {
        public int SupplierPhoneId { get; set; }

        public int SupplierId { get; set; }
        public virtual Supplier Supplier { get; set; }

        public string PhoneNumber { get; set; }

        public bool IsPrimary { get; set; } // số chính
    }
}
