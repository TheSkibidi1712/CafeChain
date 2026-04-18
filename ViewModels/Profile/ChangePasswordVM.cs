using System.ComponentModel.DataAnnotations;

namespace CafeChain.ViewModels.Profile
{
    /// <summary>
    /// DTO đổi mật khẩu — yêu cầu xác minh mật khẩu cũ trước khi cho phép đặt mới.
    /// </summary>
    public class ChangePasswordVM
    {
        [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại.")]
        public string OldPassword { get; set; } = "";

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
        [MinLength(6, ErrorMessage = "Mật khẩu mới phải có ít nhất 6 ký tự.")]
        public string NewPassword { get; set; } = "";

        [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu mới.")]
        [Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không khớp.")]
        public string ConfirmNewPassword { get; set; } = "";
    }
}
