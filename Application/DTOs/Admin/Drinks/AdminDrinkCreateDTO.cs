using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace CafeChain.Application.DTOs.Admin.Drinks
{
    public class AdminDrinkCreateDTO
    {
        [Required(ErrorMessage = "Vui lòng nhập tên nước uống")]
        public string Name { get; set; }

        [Required]
        public int CategoryId { get; set; }

        [Required]
        public int ProductTypeId { get; set; }

        public string Description { get; set; }

        // 🔥 FIX: MULTIPLE FILE
        public List<IFormFile> ImageFiles { get; set; }

        // 🔥 CHỌN DEFAULT (index của ảnh)
        public int? DefaultImageIndex { get; set; }
    }
}
