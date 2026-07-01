using CafeChain.Application.DTOs.Admin.Categories;
using CafeChain.ViewModels.Shared;

namespace CafeChain.ViewModels.Admin.Categories
{
    public class AdminCategoryIndexViewModel
    {
        public CategoryFilterDto Filter { get; set; } = new();

        public PaginatedListViewModel<AdminCategoryViewModel> Categories { get; set; }
            = new(new List<AdminCategoryViewModel>(), 0, 1, 10);
    }
}