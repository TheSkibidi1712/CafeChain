using CafeChain.Application.DTOs.Admin.StoreMenu;

namespace CafeChain.Application.Interfaces.Admin.StoreMenu
{
    public interface IStoreMenuBackfillPlanner
    {
        Task<IReadOnlyList<StoreMenuBackfillCandidateDto>> BuildPlanAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyList<StoreMenuBackfillCandidateDto>> BuildStoreProvisioningPlanAsync(
            int storeId,
            CancellationToken cancellationToken = default);
    }
}
