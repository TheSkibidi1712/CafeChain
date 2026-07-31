using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.AI;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Inventories
{
    public interface IReorderSuggestionService
    {
        /// <summary>
        /// Trusted deterministic calculation used by internal consumers such
        /// as Dashboard, notification workers and confirmation revalidation.
        /// Authorization must be enforced by the calling boundary.
        /// </summary>
        Task<ServiceResult<ReorderSuggestionListDto>> CalculateForStoreAsync(
            int storeId,
            int analysisWindowDays = 30,
            DateTime? analysisToUtc = null,
            IReadOnlyCollection<int>? ingredientIds = null,
            CancellationToken cancellationToken = default);

        Task<ServiceResult<IReadOnlyList<ReorderSuggestionListDto>>> CalculateForStoresAsync(
            IReadOnlyCollection<int> storeIds,
            int analysisWindowDays = 30,
            DateTime? analysisToUtc = null,
            CancellationToken cancellationToken = default);

        Task<ServiceResult<ReorderSuggestionListDto>> GetForStoreAsync(
            int storeId,
            int actorStaffId,
            IReadOnlyCollection<string> actorRoles,
            int analysisWindowDays = 30);

        Task<ServiceResult<ReorderSuggestionListDto>> GetForStoreAsync(
            int storeId,
            AdminActorContext actor,
            int analysisWindowDays = 30,
            CancellationToken cancellationToken = default);

        Task<ServiceResult<InventoryReorderExplanationResultDto>> ExplainAsync(
            int storeId,
            int ingredientId,
            int actorStaffId,
            IReadOnlyCollection<string> actorRoles,
            int analysisWindowDays = 30,
            CancellationToken cancellationToken = default);

        Task<ServiceResult<InventoryReorderExplanationResultDto>> ExplainAsync(
            int storeId,
            int ingredientId,
            AdminActorContext actor,
            int analysisWindowDays = 30,
            CancellationToken cancellationToken = default);

        Task<ServiceResult<InventoryReorderExplanationResultDto>> ExplainCalculatedAsync(
            ReorderSuggestionItemDto item,
            CancellationToken cancellationToken = default);
    }
}
