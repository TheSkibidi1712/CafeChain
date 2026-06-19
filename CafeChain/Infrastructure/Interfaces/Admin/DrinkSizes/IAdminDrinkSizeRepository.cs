using CafeChain.Models.Drinks;

namespace CafeChain.Infrastrusture.Interfaces.Admin.DrinkSizes
{
    public interface IAdminDrinkSizeRepository
    {
        Task<IEnumerable<DrinkSize>> GetBySizeIdAsync(int sizeId);

        Task<DrinkSize> GetByIdAsync(int id);

        Task AddAsync(DrinkSize entity);

        Task UpdateAsync(DrinkSize entity);

        Task SaveChangesAsync();
    }
}
