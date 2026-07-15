namespace CafeChain.Application.DTOs.Admin.Suppliers
{
    // Dùng để hiển thị chi tiết 1 NCC (các tab con)
    public class AdminSupplierDetailDTO
    {
        public int SupplierId { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Address { get; set; }
        public string? Note { get; set; }
        public bool Active { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string RowVersion { get; set; } = "";

        public List<AdminSupplierPhoneDTO> Phones { get; set; } = new();
        public List<AdminSupplierContactDTO> Contacts { get; set; } = new();
    }

    public class AdminSupplierPhoneDTO
    {
        public int SupplierPhoneId { get; set; }
        public string PhoneNumber { get; set; } = "";
        public bool IsPrimary { get; set; }
    }

    public class AdminSupplierContactDTO
    {
        public int SupplierContactId { get; set; }
        public string Name { get; set; } = "";
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Position { get; set; }
        public bool IsPrimary { get; set; }
    }
}
