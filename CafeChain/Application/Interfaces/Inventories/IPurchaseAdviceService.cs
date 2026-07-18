using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Inventories
{
    public interface IPurchaseAdviceService
    {
        Task<ServiceResult<PurchaseAdvicePageDto>> GetPageAsync(PurchaseAdviceFilterDto filter, AdminActorContext actor);
        Task<ServiceResult<IReadOnlyList<PurchaseAdviceSourceDto>>> GetAvailableSourcesAsync(int storeId, AdminActorContext actor);
        Task<ServiceResult<PurchaseAdviceDetailDto>> GetDetailAsync(int purchaseAdviceId, AdminActorContext actor);
        Task<ServiceResult<PurchaseAdviceDetailDto>> CreateAsync(CreatePurchaseAdviceRequest request, AdminActorContext actor);
        Task<ServiceResult<PurchaseAdviceDetailDto>> UpdateAsync(UpdatePurchaseAdviceRequest request, AdminActorContext actor);
        Task<ServiceResult<PurchaseAdviceDetailDto>> SubmitAsync(int purchaseAdviceId, PurchaseAdviceTransitionRequest request, AdminActorContext actor);
        Task<ServiceResult<PurchaseAdviceDetailDto>> StartReviewAsync(int purchaseAdviceId, PurchaseAdviceTransitionRequest request, AdminActorContext actor);
        Task<ServiceResult<PurchaseAdviceDetailDto>> RejectAsync(int purchaseAdviceId, PurchaseAdviceTransitionRequest request, AdminActorContext actor);
        Task<ServiceResult<PurchaseAdviceDetailDto>> CancelAsync(int purchaseAdviceId, PurchaseAdviceTransitionRequest request, AdminActorContext actor);
    }
}
