using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.DTOs.AI;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Inventories
{
    public interface IReorderSuggestionService
    {
        Task<ServiceResult<ReorderSuggestionListDto>> GetForStoreAsync(
            int storeId,
            int actorStaffId,
            IReadOnlyCollection<string> actorRoles,
            int analysisWindowDays = 30);

        Task<ServiceResult<InventoryReorderExplanationResultDto>> ExplainAsync(
            int storeId,
            int ingredientId,
            int actorStaffId,
            IReadOnlyCollection<string> actorRoles,
            int analysisWindowDays = 30,
            CancellationToken cancellationToken = default);
    }
}
