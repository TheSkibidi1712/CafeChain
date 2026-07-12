using System.ComponentModel.DataAnnotations;

namespace CafeChain.Application.DTOs.Admin.Categories
{
    public class AdminCreateCategoryDto
    {
        [Required(ErrorMessage = "Tên danh mục không được để trống")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Tên danh mục phải từ 2 đến 100 ký tự")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Mã danh mục không được để trống")]
        [StringLength(30, MinimumLength = 2, ErrorMessage = "Mã danh mục phải từ 2 đến 30 ký tự")]
        public string? CategoryCode { get; set; }

        public bool Active { get; set; } = true;

        [StringLength(10, ErrorMessage = "Icon tối đa 10 ký tự")]
        public string? Icon { get; set; }


    }
}
