using System.ComponentModel.DataAnnotations;

namespace CafeChain.ViewModels.Accounts
{
    public class ForgotPasswordViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập email")]
        [EmailAddress]
        public string Email { get; set; }
    }
}
