namespace CafeChain.Models.Inventories.Suppliers
{
    public class SupplierPhone
    {
        public int SupplierPhoneId { get; set; }

        public int SupplierId { get; set; }

        public string? PhoneNumber { get; set; }

        public bool IsPrimary { get; set; }

        public string? Description { get; set; }

        public virtual Supplier Supplier { get; set; }
    }
}
