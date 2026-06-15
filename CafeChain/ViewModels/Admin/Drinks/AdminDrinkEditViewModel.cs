using CafeChain.Application.DTOs.Admin.Drinks;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CafeChain.ViewModels.Admin.Drinks
{
    public class AdminDrinkEditViewModel
    {
        public AdminDrinkUpdateDTO DrinkUpdateDTO { get; set; }
        public IEnumerable<SelectListItem> Categories { get; set; }
        public IEnumerable<SelectListItem> ProductTypes { get; set; }
    }
}
