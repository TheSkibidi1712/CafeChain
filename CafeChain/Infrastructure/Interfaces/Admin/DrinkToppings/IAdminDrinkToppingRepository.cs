using CafeChain.Models.Drinks;

namespace CafeChain.Infrastrusture.Interfaces.Admin.DrinkToppings
{
    public interface IAdminDrinkToppingRepository
    {
        // =============================
        // DRINK (PHỤC VỤ UI)
        // =============================
        Task<IEnumerable<Drink>> GetActiveDrinksAsync();

        // =============================
        // DRINK - TOPPING
        // =============================
        Task<IEnumerable<DrinkTopping>> GetByToppingIdAsync(int toppingId);

        Task<DrinkTopping?> GetByIdAsync(int id);

        Task AddAsync(DrinkTopping entity);

        Task UpdateAsync(DrinkTopping entity);

        Task SaveChangesAsync();
    }
}
