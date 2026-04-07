using CafeChain.Application.DTOs.Admin.Ingredients;
using CafeChain.Application.Interfaces.Admin.Ingredients;
using CafeChain.Infrastrusture.Interfaces.Admin.Ingredients;
using CafeChain.Models.Inventories;

namespace CafeChain.Application.Services.Admin.Ingredients
{
    public class AdminIngredientService : IAdminIngredientService
    {
        //private readonly IAdminIngredientRepository _ingredientRepository;

        //public AdminIngredientService(IAdminIngredientRepository ingredientRepository)
        //{
        //    _ingredientRepository = ingredientRepository;
        //}

        //public async Task<IEnumerable<AdminIngredientDTO>> GetAllIngredientsAsync()
        //{
        //    var ingredients = await _ingredientRepository.GetAllIngredientsAsync();
        //    return ingredients.Select(i => new AdminIngredientDTO
        //    {
        //        IngredientId = i.IngredientId,
        //        Code = i.Code,
        //        Name = i.Name,
        //        BaseUnit = i.BaseUnit,
        //        Active = i.Active
        //    });
        //}

        //public async Task<AdminIngredientUpdateDTO> GetIngredientForUpdateAsync(int id)
        //{
        //    var ingredient = await _ingredientRepository.GetIngredientByIdAsync(id);
        //    if (ingredient == null) return null;

        //    return new AdminIngredientUpdateDTO
        //    {
        //        IngredientId = ingredient.IngredientId,
        //        Code = ingredient.Code,
        //        Name = ingredient.Name,
        //        BaseUnit = ingredient.BaseUnit,
        //        Active = ingredient.Active
        //    };
        //}

        //public async Task CreateIngredientAsync(AdminIngredientCreateDTO dto)
        //{
        //    if (await _ingredientRepository.IsIngredientCodeExistsAsync(dto.Code))
        //        throw new ArgumentException("Mã nguyên liệu đã tồn tại.");

        //    if (await _ingredientRepository.IsIngredientNameExistsAsync(dto.Name))
        //        throw new ArgumentException("Tên nguyên liệu đã tồn tại.");

        //    var ingredient = new Ingredient
        //    {
        //        Code = dto.Code.Trim().ToUpper(),
        //        Name = dto.Name.Trim(),
        //        BaseUnit = dto.BaseUnit.Trim(),
        //        Active = true
        //    };

        //    await _ingredientRepository.CreateIngredientAsync(ingredient);
        //}

        //public async Task UpdateIngredientAsync(AdminIngredientUpdateDTO dto)
        //{
        //    if (await _ingredientRepository.IsIngredientCodeExistsAsync(dto.Code, dto.IngredientId))
        //        throw new ArgumentException("Mã nguyên liệu đã tồn tại.");

        //    if (await _ingredientRepository.IsIngredientNameExistsAsync(dto.Name, dto.IngredientId))
        //        throw new ArgumentException("Tên nguyên liệu đã tồn tại.");

        //    var ingredient = await _ingredientRepository.GetIngredientByIdAsync(dto.IngredientId);
        //    if (ingredient == null) throw new KeyNotFoundException("Không tìm thấy nguyên liệu.");

        //    ingredient.Code = dto.Code.Trim().ToUpper();
        //    ingredient.Name = dto.Name.Trim();
        //    ingredient.BaseUnit = dto.BaseUnit.Trim();
        //    ingredient.Active = dto.Active;

        //    await _ingredientRepository.UpdateIngredientAsync(ingredient);
        //}

        //public async Task ToggleIngredientStatusAsync(int id)
        //{
        //    await _ingredientRepository.ToggleIngredientStatusAsync(id);
        //}
    }
}
