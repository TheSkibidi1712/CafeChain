using CafeChain.Models.Drinks;

namespace CafeChain.Infrastrusture.Interfaces.Admin.DrinkToppings
{
    public interface IAdminDrinkToppingRepository
    {
        Task<IEnumerable<Drink>> GetActiveDrinksAsync();

        Task<IEnumerable<DrinkTopping>> GetByToppingIdAsync(int toppingId);

        Task<DrinkTopping?> GetByIdAsync(int id);

        // NEW
        Task<Drink?> GetDrinkByIdAsync(int drinkId);

        Task<Topping?> GetToppingByIdAsync(int toppingId);

        Task AddAsync(DrinkTopping entity);

        Task UpdateAsync(DrinkTopping entity);

        Task SaveChangesAsync();
    }
}
