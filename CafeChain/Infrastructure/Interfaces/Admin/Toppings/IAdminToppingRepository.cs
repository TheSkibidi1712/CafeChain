using CafeChain.Models.Drinks;

namespace CafeChain.Infrastrusture.Interfaces.Admin.Toppings
{
    public interface IAdminToppingRepository
    {
        Task<IEnumerable<Topping>> GetAllAsync();
        Task<IEnumerable<Topping>> GetActiveAsync();

        Task<Topping?> GetByIdAsync(int id);

        Task AddAsync(Topping topping);
        void Update(Topping topping);

        Task<bool> ExistsByNameAsync(string name);
        Task<bool> ExistsByNameAsync(string name, int excludeId);

        Task SaveChangesAsync();
    }
}
