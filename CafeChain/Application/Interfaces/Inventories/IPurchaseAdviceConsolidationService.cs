using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Inventories;

public interface IPurchaseAdviceConsolidationService
{
    Task<ServiceResult<PurchaseAdviceConsolidationPageDto>> GetQueueAsync(
        PurchaseAdviceConsolidationFilterDto filter,
        AdminActorContext actor);

    Task<ServiceResult<PurchaseAdviceConsolidationPreviewDto>> PreviewAsync(
        PurchaseAdviceConsolidationPreviewRequest request,
        AdminActorContext actor);
}
