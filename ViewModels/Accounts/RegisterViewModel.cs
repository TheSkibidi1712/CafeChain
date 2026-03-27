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
            @"^0\d{9,10}$",
            ErrorMessage = "Số điện thoại phải bắt đầu bằng 0 và có 10-11 chữ số"
        )]
        public string PhoneNumber { get; set; }

        // ================= EMAIL =================
        [Required(ErrorMessage = "Email không được để trống")]
        [Display(Name = "Email")]
        [RegularExpression(
            @"^[^@\s]+@[^@\s]+\.com$",
            ErrorMessage = "Email phải đúng định dạng và kết thúc bằng .com"
        )]
        public string Email { get; set; }

        // ================= PASSWORD =================
        [Required(ErrorMessage = "Mật khẩu không được để trống")]
        [DataType(DataType.Password)]
        // Bỏ [MinLength(6)] cũ đi và thay bằng cụm này:
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$",
            ErrorMessage = "Mật khẩu phải có ít nhất 8 ký tự, bao gồm chữ hoa, chữ thường và chữ số")]
        [Display(Name = "Mật khẩu")]
        public string Password { get; set; }

        // ================= CONFIRM PASSWORD =================
        [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu")]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Mật khẩu xác nhận không khớp")]
        [Display(Name = "Xác nhận mật khẩu")]
        public string ConfirmPassword { get; set; }

        // ================= DATE OF BIRTH =================
        [DataType(DataType.Date)]
        [Display(Name = "Ngày sinh")]
        public DateTime? DateOfBirth { get; set; }

        // ================= TERMS =================
        [Required(ErrorMessage = "Bạn phải đồng ý với điều khoản")]
        public bool AcceptTerms { get; set; }
    }
}
