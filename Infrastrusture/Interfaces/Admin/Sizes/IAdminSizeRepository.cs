using CafeChain.Models.Drinks;
namespace CafeChain.Infrastrusture.Interfaces.Admin.Sizes
{
    public interface IAdminSizeRepository
    {
        Task<IEnumerable<Size>> GetAllAsync();
        Task<Size> GetByIdAsync(int id);
        Task AddAsync(Size size);
        Task UpdateAsync(Size size);
        Task SaveChangesAsync();
        Task<bool> ExistsByNameAsync(string name);
        Task<bool> ExistsByNameAsync(string name, int excludeId);
    }
}
