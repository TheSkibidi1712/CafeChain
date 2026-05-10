using CafeChain.Models.Inventories.Ingredients;

namespace CafeChain.Infrastrusture.Interfaces.Admin.Ingredients
{
    public interface IAdminIngredientRepository
    {
        Task<(List<Ingredient> Items, int Total)> GetPagedAsync(string? search, bool? status, int page, int pageSize); 
        Task<Ingredient?> GetByIdAsync(int id);

        Task CreateAsync(Ingredient ingredient);
        Task UpdateAsync(Ingredient ingredient);

        Task<bool> IsCodeExists(string code, int? excludeId = null);
        Task<bool> IsNameExists(string name, int? excludeId = null);

        Task ToggleStatus(int id);
        Task SaveChangesAsync();

        // UNIT CONVERSION
        Task<List<Unit>> GetActiveUnitsAsync();
        Task AddConversionsAsync(List<UnitConversion> conversions);
        Task ReplaceConversionsAsync(int ingredientId, List<UnitConversion> conversions);
    }
}
