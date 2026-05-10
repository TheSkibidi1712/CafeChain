namespace CafeChain.Models.Inventories.Suppliers
{
    public class SupplierContactPhone
    {
        public int SupplierContactPhoneId { get; set; }

        public int SupplierContactId { get; set; }

        public string? PhoneNumber { get; set; }

        public bool IsPrimary { get; set; }

        public string? Description { get; set; }

        public virtual SupplierContact SupplierContact { get; set; }
    }
}
