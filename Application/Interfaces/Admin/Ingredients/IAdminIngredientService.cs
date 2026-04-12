using CafeChain.Application.DTOs.Admin.Ingredients;
using CafeChain.Application.DTOs.Admin.Units;
namespace CafeChain.Application.Interfaces.Admin.Ingredients
{
    public interface IAdminIngredientService
    {
        Task<List<AdminIngredientDTO>> GetAllAsync(string? search, bool? status);
        Task<AdminIngredientUpdateDTO?> GetByIdAsync(int id);

        Task<int> CreateAsync(AdminIngredientCreateDTO dto);

        Task UpdateAsync(AdminIngredientUpdateDTO dto);

        Task ToggleStatusAsync(int id);

        // UNIT CONVERSION
        Task<List<UnitDTO>> GetUnitsAsync();
    }
}
