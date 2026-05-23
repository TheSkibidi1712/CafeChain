using CafeChain.Models.Enums.Customer;
using System.ComponentModel.DataAnnotations;

namespace CafeChain.ViewModels.Accounts
{
    public class RegisterViewModel
    {
        // ================= FULL NAME =================
        [Required(ErrorMessage = "Vui lòng nhập họ và tên")]
        [Display(Name = "Họ tên")]
        [StringLength(100, ErrorMessage = "Họ tên không được vượt quá 100 ký tự")]
        [RegularExpression(
            @"^[a-zA-ZÀ-ỹ\s]+$",
            ErrorMessage = "Họ tên chỉ được chứa chữ cái và khoảng trắng"
        )]
        public string FullName { get; set; }

        // ================= PHONE =================
        [Required(ErrorMessage = "Số điện thoại không được để trống")]
        [Display(Name = "Số điện thoại")]
        [RegularExpression(
            @"^(0|\+84)\d{9}$",
            ErrorMessage = "SĐT phải bắt đầu bằng 0 hoặc +84 và có 10 số"
        )]
        public string PhoneNumber { get; set; }

        // ================= EMAIL =================
        [Required(ErrorMessage = "Email không được để trống")]
        [Display(Name = "Email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; }

        // ================= PASSWORD =================
        [Required(ErrorMessage = "Mật khẩu không được để trống")]
        [DataType(DataType.Password)]
        [RegularExpression(
            @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).{8,}$",
            ErrorMessage = "Mật khẩu phải ≥ 8 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt"
        )]
        [Display(Name = "Mật khẩu")]
        public string Password { get; set; }

        // ================= CONFIRM PASSWORD =================
        [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu")]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Mật khẩu xác nhận không khớp")]
        [Display(Name = "Xác nhận mật khẩu")]
        public string ConfirmPassword { get; set; }

        // ================= GENDER =================

        [Display(Name = "Giới tính")]
        public Gender Gender { get; set; }
            = Gender.Unknown;

        // ================= DATE OF BIRTH =================
        // Nhận dạng dd/MM/yyyy từ form, parse thủ công trong controller
        public string? DateOfBirthText { get; set; }

        // Helper: parse sang DateTime? (dùng trong controller)
        public DateTime? DateOfBirth =>
            DateTime.TryParseExact(DateOfBirthText, "dd/MM/yyyy",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var d)
            ? d : null;

        // ================= TERMS =================
        [Required(ErrorMessage = "Bạn phải đồng ý với điều khoản")]
        public bool AcceptTerms { get; set; }
    }
}