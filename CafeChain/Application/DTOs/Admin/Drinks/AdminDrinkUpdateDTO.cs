using System.ComponentModel.DataAnnotations;

namespace CafeChain.Application.DTOs.Admin.Drinks
{
    public class AdminDrinkUpdateDTO
    {
        public int DrinkId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mã nước uống")]
        [StringLength(50, ErrorMessage = "Mã nước uống không được vượt quá 50 ký tự")]
        public string DrinkCode { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên nước uống")]
        [StringLength(200, ErrorMessage = "Tên không được vượt quá 200 ký tự")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn danh mục")]
        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn danh mục")]
        public int? CategoryId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn loại sản phẩm")]
        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn loại sản phẩm")]
        public int? ProductTypeId { get; set; }

        [StringLength(1000, ErrorMessage = "Mô tả không được vượt quá 1000 ký tự")]
        public string Description { get; set; }

        public bool Active { get; set; }
    }
}
