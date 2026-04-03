using CafeChain.Application.DTOs.Admin.Ingredients;

namespace CafeChain.ViewModels.Admin.Ingredients
{
    public class AdminIngredientViewModel
    {
        public IEnumerable<AdminIngredientDTO> Ingredients { get; set; }
    }
}
