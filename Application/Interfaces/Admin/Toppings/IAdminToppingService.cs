using CafeChain.Application.DTOs.Admin.Toppings;

namespace CafeChain.Application.Interfaces.Admin.Toppings
{
    public interface IAdminToppingService
    {
        Task<IEnumerable<ToppingDto>> GetAllAsync();
        Task<IEnumerable<ToppingDto>> GetActiveAsync();

        Task<ToppingDto?> GetByIdAsync(int id);

        Task CreateAsync(ToppingDto dto);
        Task UpdateAsync(ToppingDto dto);

        Task ToggleStatusAsync(int id);
    }
}
