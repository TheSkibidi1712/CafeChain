using System.ComponentModel.DataAnnotations;

using Microsoft.AspNetCore.Http;

namespace CafeChain.Application.DTOs.Admin.Categories
{
    public class AdminCreateCategoryDto
    {
        [Required(ErrorMessage = "Tên danh mục không được để trống")]
        public string Name { get; set; }

        public string? Description { get; set; }

        public IFormFile? ImageFile { get; set; }

        public bool Active { get; set; } = true;
    }
}
