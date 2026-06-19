using CafeChain.Application.DTOs.Admin.DrinkToppings;
using CafeChain.ViewModels.Admin.DrinkToppings;
namespace CafeChain.Application.Interfaces.Admin.DrinkToppings
{
    public interface IAdminDrinkToppingService
    {
        Task<IEnumerable<DrinkToppingItemVM>> GetDrinksForToppingAsync(int toppingId);

        Task AssignAsync(DrinkToppingDto dto);

        Task ToggleAsync(int drinkToppingId);
    }
}
