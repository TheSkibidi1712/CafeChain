using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.POS
{
    /// <summary>
    /// Issue #96 — read-only branch inventory list for POS “Kho chi nhánh”.
    /// </summary>
    public interface IPosBranchInventoryService
    {
        Task<ServiceResult<POSBranchInventoryListDto>> GetBranchInventoryAsync(
            int storeId,
            string? search,
            string? itemType,
            int page,
            int pageSize);
    }
}
