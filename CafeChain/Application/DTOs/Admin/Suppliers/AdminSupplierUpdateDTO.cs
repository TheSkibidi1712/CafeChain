using System.ComponentModel.DataAnnotations;

namespace CafeChain.Application.DTOs.Admin.Suppliers
{
    // DTO cập nhật thông tin cơ bản của NCC
    // Code là readonly (không đổi sau khi tạo)
    public class AdminSupplierUpdateDTO
    {
        public int SupplierId { get; set; }

        [Required(ErrorMessage = "Tên NCC không được để trống")]
        public string Name { get; set; } = "";

        [MaxLength(500)]
        public string? Address { get; set; }

        [MaxLength(1000)]
        public string? Note { get; set; }

        public bool Active { get; set; }

        public string? RowVersion { get; set; }
    }

    // DTO thêm số điện thoại phụ
    public class AdminSupplierPhoneCreateDTO
    {
        public int SupplierId { get; set; }

        [Required(ErrorMessage = "Số điện thoại không được để trống")]
        public string PhoneNumber { get; set; } = "";
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

    public class AdminSupplierContactUpdateDTO : AdminSupplierContactCreateDTO
    {
        public int SupplierContactId { get; set; }
        public bool Active { get; set; } = true;
    }
}
