using System.ComponentModel.DataAnnotations;

namespace CafeChain.Application.DTOs.Admin.Suppliers
{
    // DTO tạo mới nhà cung cấp (kèm thông tin chính ban đầu)
    public class AdminSupplierCreateDTO
    {
        [Required(ErrorMessage = "Mã NCC không được để trống")]
        public string Code { get; set; } = "";

        [Required(ErrorMessage = "Tên NCC không được để trống")]
        public string Name { get; set; } = "";

        public string? TaxCode { get; set; }
        public string? Website { get; set; }
        public string? Address { get; set; }

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
    }
}
