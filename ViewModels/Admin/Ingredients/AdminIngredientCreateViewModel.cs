using CafeChain.Application.DTOs.Admin.Ingredients;
using CafeChain.Models.Inventories.Ingredients;

namespace CafeChain.ViewModels.Admin.Ingredients
{
    public class AdminIngredientCreateViewModel
    {
        public AdminIngredientCreateDTO Data { get; set; }
        public List<Unit> Units { get; set; }
    }
}
