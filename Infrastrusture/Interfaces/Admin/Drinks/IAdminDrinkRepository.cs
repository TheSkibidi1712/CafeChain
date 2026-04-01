using CafeChain.Models.Drinks;

namespace CafeChain.Infrastrusture.Interfaces.Admin.Drinks
{
    public interface IAdminDrinkRepository
    {
        Task<IEnumerable<Drink>> GetAllDrinksAsync();
        Task<Drink> GetDrinkByIdAsync(int id);
        Task<int> CreateDrinkAsync(Drink drink);
        Task UpdateDrinkAsync(Drink drink);
        Task ToggleDrinkStatusAsync(int id);
        Task<bool> IsDrinkNameExistsAsync(string name, int? excludeId = null);
        Task<IEnumerable<DrinkCategory>> GetDrinkCategoriesAsync();
        Task<IEnumerable<ProductType>> GetProductTypesAsync();
        
        // Image Management
        Task<IEnumerable<DrinkImage>> GetDrinkImagesAsync(int drinkId);
        Task<DrinkImage> GetDrinkImageByIdAsync(int drinkImageId);
        Task AddDrinkImageAsync(DrinkImage drinkImage);
        Task SetDefaultDrinkImageAsync(int drinkId, int newDefaultImageId);
        Task DeleteDrinkImageAsync(int drinkImageId);
        Task UpdateDrinkImageAsync(DrinkImage drinkImage);
    }
}
