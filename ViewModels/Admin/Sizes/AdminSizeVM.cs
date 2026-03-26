using System.ComponentModel.DataAnnotations;

namespace CafeChain.ViewModels.Admin.Sizes
{
    public class AdminSizeVM
    {
        public int SizeId { get; set; }

        [Required(ErrorMessage = "Mã size (ví dụ: S, M, L) là bắt buộc")]
        [Display(Name = "Mã Size")]
        public string Name { get; set; } // Tương ứng field Mã Size trong ảnh

        [Required(ErrorMessage = "Tên kích thước là bắt buộc")]
        [Display(Name = "Tên kích thước")]
        public string Description { get; set; } // Tương ứng Tên Size trong ảnh

        public bool Active { get; set; } = true;
    }
}
