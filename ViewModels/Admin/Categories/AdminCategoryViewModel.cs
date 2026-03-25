using System.ComponentModel.DataAnnotations;

namespace CafeChain.ViewModels.Admin.Categories
{
    public class AdminCategoryViewModel
    {
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "Tên danh mục không được để trống")]
        public string Name { get; set; }

        public bool Active { get; set; }

    }
}
