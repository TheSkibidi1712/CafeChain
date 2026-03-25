using System.ComponentModel.DataAnnotations;

namespace CafeChain.Application.DTOs.Admin.Categories
{
    public class AdminUpdateCategoryDto
    {
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "Tên danh mục không được để trống")]
        public string Name { get; set; }
        public bool Active { get; set; }
    }
}
