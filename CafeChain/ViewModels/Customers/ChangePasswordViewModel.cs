using System.ComponentModel.DataAnnotations;

namespace CafeChain.ViewModels.Customers
{
    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại.")]
        public string CurrentPassword { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
        [DataType(DataType.Password)]
        // Đồng bộ chính sách mật khẩu
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$",
            ErrorMessage = "Mật khẩu mới phải có ít nhất 8 ký tự, bao gồm chữ hoa, chữ thường và chữ số")]
        public string NewPassword { get; set; }

        [Required(ErrorMessage = "Vui lòng xác nhận lại mật khẩu mới.")]
        [Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không khớp.")]
        public string ConfirmPassword { get; set; }
    }
}