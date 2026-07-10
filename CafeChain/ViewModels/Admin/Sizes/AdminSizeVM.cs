using System.ComponentModel.DataAnnotations;

namespace CafeChain.ViewModels.Admin.Sizes
{
    public class AdminSizeVM
    {
        public int SizeId { get; set; }

        [Required(ErrorMessage = "Mã size là bắt buộc")]
        [StringLength(20, ErrorMessage = "Mã size tối đa 20 ký tự")]
        [Display(Name = "Mã Size")]
        public string? SizeCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên size là bắt buộc")]
        [StringLength(50, ErrorMessage = "Tên size tối đa 50 ký tự")]
        [Display(Name = "Tên Size")]
        public string Name { get; set; } = string.Empty;

        [StringLength(300, ErrorMessage = "Mô tả tối đa 300 ký tự")]
        [Display(Name = "Mô tả")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Loại size là bắt buộc")]
        public CafeChain.Models.Enums.Drink.SizeTypeEnum SizeType { get; set; } =
            CafeChain.Models.Enums.Drink.SizeTypeEnum.Cup;

        public bool Active { get; set; } = true;
    }
}
