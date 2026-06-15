using CafeChain.Application.DTOs.Admin.Drinks;
using CafeChain.Models.Drinks;

namespace CafeChain.Application.Interfaces.Admin.Drinks
{
    public interface IAdminDrinkService
    {
        Task<IEnumerable<AdminDrinkDTO>> GetAllDrinksAsync();
        Task<AdminDrinkDTO> GetDrinkByIdAsync(int id);
        Task<int> CreateDrinkAsync(AdminDrinkCreateDTO drinkCreateDTO);
        Task<AdminDrinkUpdateDTO> GetDrinkForUpdateAsync(int id);
        Task UpdateDrinkAsync(AdminDrinkUpdateDTO updateDTO);
        Task ToggleDrinkStatusAsync(int id);
        Task<IEnumerable<DrinkCategory>> GetDrinkCategoriesAsync();
        Task<IEnumerable<ProductType>> GetProductTypesAsync();

        // Image Management
        Task<IEnumerable<AdminDrinkImageDTO>> GetDrinkImagesAsync(int drinkId);
        Task AddDrinkImageAsync(int drinkId, Microsoft.AspNetCore.Http.IFormFile imageFile, bool isDefault);
        Task SetDefaultDrinkImageAsync(int drinkId, int drinkImageId);
        Task DeleteDrinkImageAsync(int drinkImageId);
        Task UpdateDrinkImageAsync(int drinkImageId, Microsoft.AspNetCore.Http.IFormFile newImageFile);
    }
}
