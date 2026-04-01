using System.ComponentModel.DataAnnotations;

namespace CafeChain.Application.DTOs.Admin.Drinks
{
    public class AdminDrinkUpdateDTO
    {
        public int DrinkId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên nước uống")]
        [StringLength(200, ErrorMessage = "Tên không được vượt quá 200 ký tự")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn danh mục")]
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn loại sản phẩm")]
        public int ProductTypeId { get; set; }

        [StringLength(1000, ErrorMessage = "Mô tả không được vượt quá 1000 ký tự")]
        public string Description { get; set; }

        public bool Active { get; set; }
    }
}
