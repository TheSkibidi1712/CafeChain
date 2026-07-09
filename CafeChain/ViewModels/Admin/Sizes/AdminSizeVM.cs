using System.ComponentModel.DataAnnotations;

namespace CafeChain.ViewModels.Admin.Sizes
{
    public class AdminSizeVM
    {
        public int SizeId { get; set; }

        public string? SizeCode { get; set; }

        [Required(ErrorMessage = "Mã size (ví dụ: S, M, L) là bắt buộc")]
        [Display(Name = "Mã Size")]
        public string Name { get; set; } // Tương ứng field Mã Size trong ảnh

        [Required(ErrorMessage = "Tên kích thước là bắt buộc")]
        [Display(Name = "Tên kích thước")]
        public string Description { get; set; } // Tương ứng Tên Size trong ảnh

        [Required(ErrorMessage = "Loại size là bắt buộc")]
        public CafeChain.Models.Enums.Drink.SizeTypeEnum SizeType { get; set; } =
            CafeChain.Models.Enums.Drink.SizeTypeEnum.Cup;

        public bool Active { get; set; } = true;
    }
}
