using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Inventories;

public sealed class DuplicatePurchaseOrderRepairService : IDuplicatePurchaseOrderRepairService
{
    private readonly AppDbContext _context;
    private readonly IPurchaseOrderBatchService _batchService;

    public DuplicatePurchaseOrderRepairService(
        AppDbContext context,
        IPurchaseOrderBatchService batchService)
    {
        _context = context;
        _batchService = batchService;
    }

    public async Task<DuplicatePurchaseOrderRepairReport> DryRunAsync()
    {
        var rows = await _context.PurchaseOrderLineAllocations.AsNoTracking()
            .Where(x => x.PurchaseOrder.Status != PurchaseOrderStatuses.Cancelled
                && x.PurchaseOrderBatchLine.PurchaseOrderBatch.Status != PurchaseOrderBatchStatuses.Cancelled)
            .Select(x => new
            {
                Allocation = x,
                AdviceNumber = x.PurchaseAdviceLine.PurchaseAdvice.AdviceNumber,
                RequestedQuantity = x.PurchaseAdviceLine.RequestedPurchaseBaseQuantity,
                BatchId = x.PurchaseOrderBatchLine.PurchaseOrderBatchId,
                BatchNumber = x.PurchaseOrderBatchLine.PurchaseOrderBatch.BatchNumber,
                BatchStatus = x.PurchaseOrderBatchLine.PurchaseOrderBatch.Status,
                BatchCreatedAt = x.PurchaseOrderBatchLine.PurchaseOrderBatch.CreatedAtUtc,
                BatchApprovedAt = x.PurchaseOrderBatchLine.PurchaseOrderBatch.ApprovedAtUtc,
                OrderCode = x.PurchaseOrder.Code,
                HasReceipt = x.PurchaseOrderLine.ReceiptPostings.Any(),
                HasSentDocument = x.PurchaseOrderBatchLine.PurchaseOrderBatch.DocumentRevisions.Any(r => r.Status == "SENT")
            })
            .OrderBy(x => x.BatchCreatedAt)
            .ThenBy(x => x.Allocation.PurchaseOrderLineAllocationId)
            .ToListAsync();

        var overflowAllocationIds = new HashSet<int>();
        foreach (var group in rows.GroupBy(x => x.Allocation.PurchaseAdviceLineId))
        {
            var remaining = group.First().RequestedQuantity;
            foreach (var row in group)
            {
                if (remaining <= 0m || row.Allocation.AllocatedBaseQuantity > remaining)
                    overflowAllocationIds.Add(row.Allocation.PurchaseOrderLineAllocationId);
                remaining = Math.Max(0m, remaining - row.Allocation.AllocatedBaseQuantity);
            }
        }

        var items = new List<DuplicatePurchaseOrderRepairItem>();
        foreach (var batch in rows.GroupBy(x => x.BatchId))
        {
            var overflowRows = batch.Where(x => overflowAllocationIds.Contains(x.Allocation.PurchaseOrderLineAllocationId)).ToArray();
            if (overflowRows.Length == 0) continue;

            var wholeBatchIsDuplicate = overflowRows.Length == batch.Count();
            var safe = wholeBatchIsDuplicate
                && batch.All(x => x.BatchStatus is PurchaseOrderBatchStatuses.Draft or PurchaseOrderBatchStatuses.PendingApproval)
                && batch.All(x => !x.BatchApprovedAt.HasValue && !x.HasReceipt && !x.HasSentDocument);
            foreach (var row in overflowRows)
            {
                items.Add(new DuplicatePurchaseOrderRepairItem
                {
                    Status = safe
                        ? DuplicatePurchaseOrderRepairStatuses.SafeToCancel
                        : DuplicatePurchaseOrderRepairStatuses.ManualReviewRequired,
                    PurchaseAdviceLineId = row.Allocation.PurchaseAdviceLineId,
                    PurchaseAdviceNumber = row.AdviceNumber,
                    PurchaseOrderBatchId = row.BatchId,
                    PurchaseOrderBatchNumber = row.BatchNumber,
                    PurchaseOrderCode = row.OrderCode,
                    AllocatedBaseQuantity = row.Allocation.AllocatedBaseQuantity,
                    Message = safe
                        ? "Chứng từ tạo sau bao phủ lại phần đề nghị mua đã được đặt và chưa có nghiệp vụ hạ nguồn."
                        : "Chứng từ có phân bổ hợp lệ đi kèm hoặc đã duyệt/gửi/nhận; cần người có thẩm quyền xem xét."
                });
            }
        }

        return new DuplicatePurchaseOrderRepairReport
        {
            SafeToCancelCount = items.Count(x => x.Status == DuplicatePurchaseOrderRepairStatuses.SafeToCancel),
            ManualReviewCount = items.Count(x => x.Status == DuplicatePurchaseOrderRepairStatuses.ManualReviewRequired),
            Items = items
        };
    }

    public async Task<ServiceResult<DuplicatePurchaseOrderRepairReport>> ExecuteAsync(AdminActorContext actor)
    {
        if (!actor.RoleNames.Any(x => x is RoleConstants.BusinessOwner or RoleConstants.SystemAdmin))
            return ServiceResult<DuplicatePurchaseOrderRepairReport>.Failure(
                "Bạn không có quyền sửa chứng từ mua hàng trùng.",
                errorCode: "FORBIDDEN");

        var report = await DryRunAsync();
        var safeBatchIds = report.Items
            .Where(x => x.Status == DuplicatePurchaseOrderRepairStatuses.SafeToCancel)
            .Select(x => x.PurchaseOrderBatchId)
            .Distinct()
            .ToArray();
        var cancelled = 0;
        foreach (var batchId in safeBatchIds)
        {
            var rowVersion = await _context.PurchaseOrderBatches.AsNoTracking()
                .Where(x => x.PurchaseOrderBatchId == batchId && x.Status != PurchaseOrderBatchStatuses.Cancelled)
                .Select(x => x.RowVersion)
                .SingleOrDefaultAsync();
            if (rowVersion == null) continue;

            var result = await _batchService.CancelAsync(batchId, new PurchaseOrderBatchTransitionRequest
            {
                RowVersion = Convert.ToBase64String(rowVersion),
                Reason = "Tự động hủy chứng từ trùng sau dry-run đối soát phân bổ đề nghị mua."
            }, actor);
            if (result.IsSuccess) cancelled++;
        }

        var after = await DryRunAsync();
        after.CancelledCount = cancelled;
        return ServiceResult<DuplicatePurchaseOrderRepairReport>.Success(after);
    }
}
