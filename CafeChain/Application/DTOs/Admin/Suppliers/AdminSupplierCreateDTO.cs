using System.ComponentModel.DataAnnotations;

namespace CafeChain.Application.DTOs.Admin.Suppliers
{
    // DTO tạo mới nhà cung cấp (kèm thông tin chính ban đầu)
    // Mã NCC (Code) được sinh TỰ ĐỘNG theo format NCC00001 — không nhập tay
    public class AdminSupplierCreateDTO
    {
        [Required(ErrorMessage = "Tên NCC không được để trống")]
        public string Name { get; set; } = "";

        [MaxLength(500)]
        public string? Address { get; set; }

        [MaxLength(1000)]
        public string? Note { get; set; }

        // ===== SỐ ĐIỆN THOẠI CHÍNH =====
        [Required(ErrorMessage = "Số điện thoại chính không được để trống")]
        public string PrimaryPhone { get; set; } = "";

        // ===== THÔNG TIN LIÊN HỆ CHÍNH =====
        [Required(ErrorMessage = "Tên người liên hệ chính không được để trống")]
        public string PrimaryContactName { get; set; } = "";

        public string? PrimaryContactPhone { get; set; }
        public string? PrimaryContactEmail { get; set; }
        public string? PrimaryContactPosition { get; set; }

        // ===== DANH SÁCH PHỤ (tuỳ chọn, thêm bao nhiêu cũng được) =====

        /// <summary>Danh sách số điện thoại phụ (ngoài số chính)</summary>
        public List<string> AdditionalPhones { get; set; } = new();

        /// <summary>Danh sách người liên hệ phụ</summary>
        public List<AdminSupplierContactItemCreateDTO> AdditionalContacts { get; set; } = new();
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
