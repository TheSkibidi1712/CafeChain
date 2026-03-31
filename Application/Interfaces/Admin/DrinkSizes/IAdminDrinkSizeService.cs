using CafeChain.Application.DTOs.Admin.DrinkSizes;
using CafeChain.ViewModels.Admin.DrinkSizes;

namespace CafeChain.Application.Interfaces.Admin.DrinkSizes
{
    public interface IAdminDrinkSizeService
    {
        Task<IEnumerable<DrinkItemVM>> GetDrinksForSizeAsync(int sizeId);

        Task AssignDrinkAsync(DrinkSizeDto dto);

        Task ToggleDrinkSizeAsync(int drinkSizeId);

        Task UpdatePriceAsync(DrinkSizeDto dto);
    }
}
