namespace CafeChain.Models.Inventories
{
    public class SupplierContact
    {
        public int SupplierContactId { get; set; }

        public int SupplierId { get; set; }
        public virtual Supplier Supplier { get; set; }

        public string? Name { get; set; }        // Tên
        public string? Phone { get; set; }       // SĐT
        public string? Email { get; set; }
        public string? Position { get; set; }    // Chức vụ

        public bool IsPrimary { get; set; }     // người chính
    }
}
