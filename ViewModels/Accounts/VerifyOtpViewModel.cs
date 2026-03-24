using System.ComponentModel.DataAnnotations;

namespace CafeChain.ViewModels.Accounts
{
    public class VerifyOtpViewModel
    {
        public string Email { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mã OTP")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP phải 6 ký tự")]
        public string OtpCode { get; set; }
    }
}
