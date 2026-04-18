using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace CafeChain.ViewModels.Profile
{
    /// <summary>
    /// DTO Anti-Overposting — CHỈ cho phép bind 2 trường: PhoneNumber + AvatarFile.
    /// Mọi trường khác (BaseSalary, Role, FullName...) sẽ bị Backend bỏ qua hoàn toàn.
    /// </summary>
    public class UpdateProfileVM
    {
        [RegularExpression(@"^(0[3-9])\d{8}$", ErrorMessage = "Số điện thoại không hợp lệ (VD: 0901234567).")]
        [StringLength(10, MinimumLength = 10, ErrorMessage = "Số điện thoại phải đúng 10 chữ số.")]
        public string? PhoneNumber { get; set; }

        public IFormFile? AvatarFile { get; set; }
    }
}
