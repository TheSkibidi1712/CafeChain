using System.ComponentModel.DataAnnotations;

namespace CafeChain.Application.DTOs.Admin.Categories
{
    public class AdminCreateCategoryDto
    {
        [Required(ErrorMessage = "Tên danh mục không được để trống")]
        [StringLength(
            100,
            MinimumLength = 2,
            ErrorMessage = "Tên danh mục phải từ 2 đến 100 ký tự")]
        public string Name { get; set; }

        public bool Active { get; set; } = true;
    }
}