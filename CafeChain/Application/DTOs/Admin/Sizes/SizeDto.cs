namespace CafeChain.Application.DTOs.Admin.Sizes
{
    public class SizeDto
    {
        public int SizeId { get; set; }
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Mã size là bắt buộc")]
        [System.ComponentModel.DataAnnotations.StringLength(20, ErrorMessage = "Mã size tối đa 20 ký tự")]
        public string SizeCode { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Tên size là bắt buộc")]
        [System.ComponentModel.DataAnnotations.StringLength(50, ErrorMessage = "Tên size tối đa 50 ký tự")]
        public string Name { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.StringLength(300, ErrorMessage = "Mô tả tối đa 300 ký tự")]
        public string Description { get; set; } = string.Empty;
        public CafeChain.Models.Enums.Drink.SizeTypeEnum SizeType { get; set; } =
            CafeChain.Models.Enums.Drink.SizeTypeEnum.Cup;
        public bool Active { get; set; }
    }
}
