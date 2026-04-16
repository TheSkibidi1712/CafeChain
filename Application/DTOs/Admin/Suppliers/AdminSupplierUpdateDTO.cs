using System.ComponentModel.DataAnnotations;

namespace CafeChain.Application.DTOs.Admin.Suppliers
{
    // DTO cập nhật thông tin cơ bản của NCC
    public class AdminSupplierUpdateDTO
    {
        public int SupplierId { get; set; }

        [Required(ErrorMessage = "Mã NCC không được để trống")]
        public string Code { get; set; } = "";

        [Required(ErrorMessage = "Tên NCC không được để trống")]
        public string Name { get; set; } = "";

        public string? TaxCode { get; set; }
        public string? Website { get; set; }
        public string? Address { get; set; }
        public bool Active { get; set; }
    }

    // DTO thêm số điện thoại phụ
    public class AdminSupplierPhoneCreateDTO
    {
        public int SupplierId { get; set; }

        [Required(ErrorMessage = "Số điện thoại không được để trống")]
        public string PhoneNumber { get; set; } = "";
    }

    // DTO thêm tài khoản ngân hàng phụ
    public class AdminSupplierBankAccountCreateDTO
    {
        public int SupplierId { get; set; }

        [Required(ErrorMessage = "Tên ngân hàng không được để trống")]
        public string BankName { get; set; } = "";

        [Required(ErrorMessage = "Số tài khoản không được để trống")]
        public string AccountNumber { get; set; } = "";

        [Required(ErrorMessage = "Chủ tài khoản không được để trống")]
        public string AccountHolder { get; set; } = "";
    }

    // DTO thêm người liên hệ phụ
    public class AdminSupplierContactCreateDTO
    {
        public int SupplierId { get; set; }

        [Required(ErrorMessage = "Tên người liên hệ không được để trống")]
        public string Name { get; set; } = "";

        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Position { get; set; }
    }
}
