using System.ComponentModel.DataAnnotations;

namespace CafeChain.ViewModels.Admin.Staffs
{
    public class StaffEditVM
    {
        public int StaffId { get; set; }
        public int AccountId { get; set; }

        [Required(ErrorMessage = "Họ và tên không được để trống")]
        [MaxLength(200, ErrorMessage = "Họ và tên tối đa 200 ký tự")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Email không được để trống")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [MaxLength(255)]
        public string Email { get; set; }

        [MaxLength(500, ErrorMessage = "Mật khẩu tối đa 500 ký tự")]
        public string? NewPassword { get; set; }

        [RegularExpression(@"^(\d{12})?$", ErrorMessage = "CCCD phải đúng 12 chữ số")]
        public string? CCCD { get; set; }
        public string? TaxCode { get; set; }

        public int Gender { get; set; }
        public DateTime? StartDate { get; set; }
        public int EmployeeStatus { get; set; }
        
        public int SalaryType { get; set; }
        public decimal BaseSalary { get; set; }
        public decimal Allowance { get; set; }
        public decimal ProbationRate { get; set; }
        public decimal OvertimeRate { get; set; }

        public string? SocialInsuranceNumber { get; set; }
        [StringLength(15, MinimumLength = 10, ErrorMessage = "Mã số BHYT phải từ 10 đến 15 ký tự")]
        public string? HealthInsuranceNumber { get; set; }

        public List<StaffDependentVM> Dependents { get; set; } = new();

        public List<StaffBankVM> Banks { get; set; } = new();
        public int PrimaryBankIndex { get; set; }

        public DateTime? DateOfBirth { get; set; }

        public int? StoreId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn vai trò")]
        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn vai trò hợp lệ")]
        public int SelectedRoleId { get; set; }

        // Scope
        [Required(ErrorMessage = "Vui lòng chọn phạm vi quản lý")]
        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn phạm vi quản lý hợp lệ")]
        public int ScopeTypeId { get; set; }
        public int ScopeRefId { get; set; }

        // Phones & Addresses (max 3 mỗi loại)
        public List<string> Phones { get; set; } = new();
        public List<string> Addresses { get; set; } = new();

        // Avatar
        public IFormFile? AvatarFile { get; set; }
        public string CurrentAvatarUrl { get; set; }

        public bool Active { get; set; }
    }
}
