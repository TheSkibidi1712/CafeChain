using System.ComponentModel.DataAnnotations;

namespace CafeChain.ViewModels.Admin.Staffs
{
    public class StaffCreateVM
    {
        [Required(ErrorMessage = "Họ và tên không được để trống")]
        [MaxLength(200)]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Email không được để trống")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [MaxLength(255)]
        public string Email { get; set; }

        [Required(ErrorMessage = "Mật khẩu không được để trống")]
        [MaxLength(500)]
        public string Password { get; set; }

        public string? CCCD { get; set; }

        public string? TaxCode { get; set; }

        public decimal? Salary { get; set; }

        public DateTime? DateOfBirth { get; set; }

        public int? StoreId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn vai trò")]
        public int SelectedRoleId { get; set; }

        // Scope
        [Required(ErrorMessage = "Vui lòng chọn phạm vi quản lý")]
        public int ScopeTypeId { get; set; }
        public int ScopeRefId { get; set; }

        // Phones & Addresses (max 3 mỗi loại)
        public List<string> Phones { get; set; } = new();
        public List<string> Addresses { get; set; } = new();

        // Avatar
        public IFormFile AvatarFile { get; set; }
    }
}
