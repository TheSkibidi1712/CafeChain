using System.ComponentModel.DataAnnotations;

namespace CafeChain.ViewModels.Admin.Categories
{
    public class AdminCategoryViewModel
    {
        public int CategoryId { get; set; }

        public string CategoryCode { get; set; }

        [Required(ErrorMessage = "Tên danh mục không được để trống")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Tên danh mục phải từ 2 đến 100 ký tự")]
        public string Name { get; set; }

        public bool Active { get; set; }

        public string? Icon { get; set; }
    }
}