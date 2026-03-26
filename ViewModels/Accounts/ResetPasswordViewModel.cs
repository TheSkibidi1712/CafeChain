using System.ComponentModel.DataAnnotations;

namespace CafeChain.ViewModels.Accounts
{
    public class ResetPasswordViewModel
    {
        public string Email { get; set; }

        public string OtpCode { get; set; }

        [Required(ErrorMessage = "Mật khẩu là bắt buộc")]
        [MinLength(6)]
        public string NewPassword { get; set; }

        [Compare("NewPassword", ErrorMessage = "Mật khẩu không khớp")]
        public string ConfirmPassword { get; set; }
    }
}
