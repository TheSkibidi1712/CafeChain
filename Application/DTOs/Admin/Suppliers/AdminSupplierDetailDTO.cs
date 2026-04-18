namespace CafeChain.Application.DTOs.Admin.Suppliers
{
    // Dùng để hiển thị chi tiết 1 NCC (các tab con)
    public class AdminSupplierDetailDTO
    {
        public int SupplierId { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string? TaxCode { get; set; }
        public string? Website { get; set; }
        public string? Address { get; set; }  // Địa chỉ đầy đủ (gộp 3 cấp)
        public decimal DebtAmount { get; set; }
        public bool Active { get; set; }

        // Địa chỉ 3 cấp (dùng fill form Edit)
        public int? ProvinceId { get; set; }
        public string? ProvinceName { get; set; }
        public int? DistrictId { get; set; }
        public string? DistrictName { get; set; }
        public int? WardId { get; set; }
        public string? WardName { get; set; }
        public string? StreetAddress { get; set; }

        public List<AdminSupplierPhoneDTO> Phones { get; set; } = new();
        public List<AdminSupplierBankAccountDTO> BankAccounts { get; set; } = new();
        public List<AdminSupplierContactDTO> Contacts { get; set; } = new();
    }

    public class AdminSupplierPhoneDTO
    {
        public int SupplierPhoneId { get; set; }
        public string PhoneNumber { get; set; } = "";
        public bool IsPrimary { get; set; }
    }

    public class AdminSupplierBankAccountDTO
    {
        public int SupplierBankAccountId { get; set; }
        public string BankName { get; set; } = "";
        public string AccountNumber { get; set; } = "";
        public string AccountHolder { get; set; } = "";
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
