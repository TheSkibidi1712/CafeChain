using System.ComponentModel.DataAnnotations;

namespace CafeChain.Application.DTOs.Admin.Suppliers
{
    // DTO tạo mới nhà cung cấp (kèm thông tin chính ban đầu)
    // Mã NCC (Code) được sinh TỰ ĐỘNG theo format NCC00001 — không nhập tay
    public class AdminSupplierCreateDTO
    {
        [Required(ErrorMessage = "Tên NCC không được để trống")]
        public string Name { get; set; } = "";

        [MaxLength(32)]
        public string? TaxCode { get; set; }

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

        public Guid? DuplicateWarningId { get; set; }

        [MaxLength(500)]
        public string? DuplicateOverrideReason { get; set; }
    }

    /// <summary>Thông tin 1 người liên hệ phụ khi tạo NCC</summary>
    public class AdminSupplierContactItemCreateDTO
    {
        public string Name { get; set; } = "";
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Position { get; set; }
    }

    public class AdminSupplierDuplicateWarningDTO
    {
        public Guid WarningId { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public List<AdminSupplierDuplicateMatchDTO> Matches { get; set; } = new();
    }

    public class AdminSupplierDuplicateMatchDTO
    {
        public int SupplierId { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public bool Active { get; set; }
        public List<string> MatchedSignals { get; set; } = new();
    }
}
