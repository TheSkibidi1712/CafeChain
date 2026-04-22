using System.ComponentModel.DataAnnotations;

namespace CafeChain.Application.DTOs.Admin.Suppliers
{
    // DTO tạo mới nhà cung cấp (kèm thông tin chính ban đầu)
    // Mã NCC (Code) được sinh TỰ ĐỘNG theo format NCC00001 — không nhập tay
    public class AdminSupplierCreateDTO
    {
        [Required(ErrorMessage = "Tên NCC không được để trống")]
        public string Name { get; set; } = "";

        public string? TaxCode { get; set; }
        public string? Website { get; set; }

        // ===== ĐỊA CHỈ 3 CẤP =====
        public int? ProvinceId { get; set; }
        public int? DistrictId { get; set; }
        public int? WardId { get; set; }
        /// <summary>Số nhà / tên đường — phần địa chỉ chi tiết do người dùng nhập tay</summary>
        public string? StreetAddress { get; set; }

        // ===== SỐ ĐIỆN THOẠI CHÍNH =====
        [Required(ErrorMessage = "Số điện thoại chính không được để trống")]
        public string PrimaryPhone { get; set; } = "";

        // ===== TÀI KHOẢN NGÂN HÀNG CHÍNH =====
        [Required(ErrorMessage = "Tên ngân hàng không được để trống")]
        public string PrimaryBankName { get; set; } = "";

        [Required(ErrorMessage = "Số tài khoản không được để trống")]
        public string PrimaryAccountNumber { get; set; } = "";

        [Required(ErrorMessage = "Chủ tài khoản không được để trống")]
        public string PrimaryAccountHolder { get; set; } = "";

        // ===== THÔNG TIN LIÊN HỆ CHÍNH =====
        [Required(ErrorMessage = "Tên người liên hệ chính không được để trống")]
        public string PrimaryContactName { get; set; } = "";

        public string? PrimaryContactPhone { get; set; }
        public string? PrimaryContactEmail { get; set; }
        public string? PrimaryContactPosition { get; set; }

        // ===== DANH SÁCH PHỤ (tuỳ chọn, thêm bao nhiêu cũng được) =====

        /// <summary>Danh sách số điện thoại phụ (ngoài số chính)</summary>
        public List<string> AdditionalPhones { get; set; } = new();

        /// <summary>Danh sách tài khoản ngân hàng phụ</summary>
        public List<AdminSupplierBankItemCreateDTO> AdditionalBankAccounts { get; set; } = new();

        /// <summary>Danh sách người liên hệ phụ</summary>
        public List<AdminSupplierContactItemCreateDTO> AdditionalContacts { get; set; } = new();
    }

    /// <summary>Thông tin 1 tài khoản ngân hàng phụ khi tạo NCC</summary>
    public class AdminSupplierBankItemCreateDTO
    {
        public string BankName { get; set; } = "";
        public string AccountNumber { get; set; } = "";
        public string AccountHolder { get; set; } = "";
    }

    /// <summary>Thông tin 1 người liên hệ phụ khi tạo NCC</summary>
    public class AdminSupplierContactItemCreateDTO
    {
        public string Name { get; set; } = "";
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Position { get; set; }
    }
}
