using System.ComponentModel.DataAnnotations;

namespace CafeChain.ViewModels.Accounts
{
    public class OtpInputViewModel
    {
        public string Email { get; set; }

        [Required]
        public string OtpCode { get; set; }
    }
}
