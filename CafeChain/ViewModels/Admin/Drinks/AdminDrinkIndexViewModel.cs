using CafeChain.Application.DTOs.Admin.Drinks;
using CafeChain.ViewModels.Shared;

namespace CafeChain.ViewModels.Admin.Drinks
{
    public class AdminDrinkIndexViewModel
    {
        public AdminDrinkFilterDTO Filter { get; set; } = new();

        public PaginatedListViewModel<AdminDrinkDTO> Drinks { get; set; }
            = new(new List<AdminDrinkDTO>(), 0, 1, 10);

        public int TotalCount { get; set; }

        public int ActiveCount { get; set; }

        public int InactiveCount { get; set; }
    }
}
