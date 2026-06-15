using CafeChain.Application.DTOs.Admin.Ingredients;
using CafeChain.Application.DTOs.Admin.Units;
using CafeChain.Models.Inventories;
namespace CafeChain.Application.Interfaces.Admin.Ingredients
{
    public interface IAdminIngredientService
    {
        Task<(List<AdminIngredientDTO> Items, int Total)> GetPagedAsync(string? search, bool? status, int page, int pageSize);
        Task<AdminIngredientUpdateDTO?> GetByIdAsync(int id);

        Task<int> CreateAsync(AdminIngredientCreateDTO dto);

        Task UpdateAsync(AdminIngredientUpdateDTO dto);

        Task ToggleStatusAsync(int id);

        // UNIT CONVERSION
        Task<List<UnitDTO>> GetUnitsAsync();
    }
}
