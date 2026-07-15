namespace CafeChain.Models.Inventories.Suppliers
{
    public class SupplierContact
    {
        public int SupplierContactId { get; set; }

        public int SupplierId { get; set; }

        public string? Name { get; set; }

        public string? Email { get; set; }

        public string? PhoneNumber { get; set; }

        public string? Position { get; set; }

        public bool IsPrimary { get; set; }

        public bool Active { get; set; }

        public string? Note { get; set; }

        public virtual Supplier Supplier { get; set; }

    }
}
