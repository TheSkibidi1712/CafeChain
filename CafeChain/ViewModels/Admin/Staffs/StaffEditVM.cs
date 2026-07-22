using System.ComponentModel.DataAnnotations;

namespace CafeChain.ViewModels.Admin.Staffs;

public class StaffEditVM
{
    public int StaffId { get; set; }
    public int AccountId { get; set; }
    [Required(ErrorMessage = "Họ và tên không được để trống"), MaxLength(200)]
    public string FullName { get; set; } = string.Empty;
    [Required(ErrorMessage = "Email không được để trống"), EmailAddress(ErrorMessage = "Email không hợp lệ"), MaxLength(255)]
    public string Email { get; set; } = string.Empty;
    [MaxLength(500)]
    public string? NewPassword { get; set; }
    [RegularExpression(@"^(\d{12})?$", ErrorMessage = "CCCD phải đúng 12 chữ số")]
    public string? CCCD { get; set; }
    public int Gender { get; set; }
    public DateTime? StartDate { get; set; }
    public int EmployeeStatus { get; set; }
    public DateTime? DateOfBirth { get; set; }
    [Required(ErrorMessage = "Vui lòng chọn cửa hàng chính")]
    public int? StoreId { get; set; }
    [Required(ErrorMessage = "Vui lòng chọn vai trò"), Range(1, int.MaxValue)]
    public int SelectedRoleId { get; set; }
    [Required(ErrorMessage = "Vui lòng chọn phạm vi quản lý"), Range(1, int.MaxValue)]
    public int ScopeTypeId { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn phạm vi cụ thể")]
    public int ScopeRefId { get; set; }
    public List<string> Phones { get; set; } = new();
    [Required(ErrorMessage = "Vui lòng chọn Tỉnh/Thành phố")]
    public int? ProvinceId { get; set; }
    [Required(ErrorMessage = "Vui lòng chọn Quận/Huyện")]
    public int? DistrictId { get; set; }
    [Required(ErrorMessage = "Vui lòng chọn Phường/Xã")]
    public int? WardId { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập địa chỉ chi tiết"), MaxLength(500)]
    public string Address { get; set; } = string.Empty;
    public IFormFile? AvatarFile { get; set; }
    public string CurrentAvatarUrl { get; set; } = string.Empty;
    public bool Active { get; set; }
}
