using CafeChain.Application.DTOs.Admin.PreparedItems;
using CafeChain.Application.DTOs.Admin.Units;

namespace CafeChain.Application.Interfaces.Admin.PreparedItems
{
    public interface IAdminPreparedItemService
    {
        Task<(List<AdminPreparedItemDTO> Items, int Total)> GetPagedAsync(
            string? search,
            bool? status,
            int page,
            int pageSize);

        Task<AdminPreparedItemDTO?> GetByIdAsync(int id);

        Task<int> CreateAsync(AdminPreparedItemSaveDTO dto);

        Task UpdateAsync(AdminPreparedItemSaveDTO dto);

        Task SetActiveAsync(int preparedItemId, bool active);

        Task<List<UnitDTO>> GetInventoryUnitsAsync();
    }
}
