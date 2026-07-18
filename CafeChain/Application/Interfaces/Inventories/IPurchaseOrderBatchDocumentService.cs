using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Inventories;

public interface IPurchaseOrderBatchDocumentService
{
    Task<ServiceResult<PurchaseOrderBatchDocumentRevisionDto>> GenerateAsync(int batchId, AdminActorContext actor);
    Task<ServiceResult<IReadOnlyList<PurchaseOrderBatchDocumentRevisionDto>>> ListAsync(int batchId, AdminActorContext actor);
    Task<ServiceResult<PurchaseOrderBatchDocumentDownloadDto>> DownloadAsync(int revisionId, AdminActorContext actor);
    Task<ServiceResult<PurchaseOrderBatchDocumentRevisionDto>> MarkSentAsync(
        int batchId,
        int revisionId,
        MarkPurchaseOrderBatchDocumentSentRequest request,
        AdminActorContext actor);
}

public interface IPurchaseOrderBatchPdfRenderer
{
    byte[] Render(PurchaseOrderBatchDocumentSnapshot snapshot, int revisionNumber, DateTime generatedAtUtc, string contentHash);
}

public interface IPurchaseOrderBatchDocumentStorage
{
    Task SaveAsync(string storageReference, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default);
    Task<byte[]?> ReadAsync(string storageReference, CancellationToken cancellationToken = default);
    Task DeleteAsync(string storageReference, CancellationToken cancellationToken = default);
}
