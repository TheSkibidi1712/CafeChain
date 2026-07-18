using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Inventories;

public interface IPurchaseOrderBatchService
{
    Task<ServiceResult<PurchaseOrderBatchDetailDto>> CreateAsync(CreatePurchaseOrderBatchRequest request, AdminActorContext actor);
    Task<ServiceResult<PurchaseOrderBatchDetailDto>> GetDetailAsync(int id, AdminActorContext actor);
    Task<ServiceResult<IReadOnlyList<PurchaseOrderBatchListItemDto>>> ListAsync(string? status, int? supplierId, AdminActorContext actor);
    Task<ServiceResult<PurchaseOrderBatchDetailDto>> ApproveAsync(int id, PurchaseOrderBatchTransitionRequest request, AdminActorContext actor);
    Task<ServiceResult<PurchaseOrderBatchDetailDto>> CancelAsync(int id, PurchaseOrderBatchTransitionRequest request, AdminActorContext actor);
    Task RefreshStatusAsync(int id);
}
