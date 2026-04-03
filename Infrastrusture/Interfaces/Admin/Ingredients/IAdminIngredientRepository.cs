using CafeChain.Models.Inventories;

namespace CafeChain.Infrastrusture.Interfaces.Admin.Ingredients
{
    public interface IAdminIngredientRepository
    {
        Task<IEnumerable<Ingredient>> GetAllIngredientsAsync();
        Task<Ingredient> GetIngredientByIdAsync(int id);
        Task CreateIngredientAsync(Ingredient ingredient);
        Task UpdateIngredientAsync(Ingredient ingredient);
        Task ToggleIngredientStatusAsync(int id);
        Task<bool> IsIngredientCodeExistsAsync(string code, int? excludeId = null);
        Task<bool> IsIngredientNameExistsAsync(string name, int? excludeId = null);
    }
}
