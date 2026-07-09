using CafeChain.Models.Drinks;
namespace CafeChain.Infrastrusture.Interfaces.Admin.Sizes
{
    public interface IAdminSizeRepository
    {
        // ===== SIZE =====
        Task<IEnumerable<Size>> GetAllAsync();
        Task<Size?> GetByIdAsync(int id);
        Task AddAsync(Size size);
        Task UpdateAsync(Size size);
        Task SaveChangesAsync();
        Task<bool> ExistsByNameAsync(string name);
        Task<bool> ExistsByNameAsync(string name, int excludeId);
        Task<bool> ExistsBySizeCodeAsync(string sizeCode);
        Task<bool> ExistsBySizeCodeAsync(string sizeCode, int excludeId);

        // ===== DRINK =====
        Task<IEnumerable<Drink>> GetActiveDrinksAsync();
        Task<Drink?> GetActiveDrinkByIdAsync(int drinkId);
    }
}
