using System.Security.Cryptography;
using System.Text;
using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Inventories.Procurement;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Inventories;

public sealed class PurchaseAdviceFulfillmentService : IPurchaseAdviceFulfillmentService
{
    private readonly AppDbContext _context;

    public PurchaseAdviceFulfillmentService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ServiceResult> BackPostAcceptedAsync(
        int purchaseOrderLineId,
        int branchReceiptLineId,
        decimal acceptedQuantity,
        int actorStaffId)
    {
        if (acceptedQuantity <= 0)
            return Failure(PurchaseAdviceErrorCodes.AcceptedExceedsAllocation, "Số lượng chấp nhận phải lớn hơn 0.");

        var allocation = await LoadAllocationForUpdateAsync(purchaseOrderLineId);
        if (allocation == null)
            return await HandleMissingAllocationAsync(purchaseOrderLineId);

        var receiptLine = await _context.BranchReceiptLines.AsNoTracking()
            .Where(x => x.BranchReceiptLineId == branchReceiptLineId)
            .Select(x => new { x.BranchReceiptLineId, x.BranchReceiptId, x.PurchaseOrderLineId })
            .SingleOrDefaultAsync();
        if (receiptLine == null)
            return Failure(PurchaseAdviceErrorCodes.BackPostTraceMissing, "Không tìm thấy dòng phiếu nhận để truy vết Accepted.");
        if (receiptLine.PurchaseOrderLineId != purchaseOrderLineId)
            return Failure(PurchaseAdviceErrorCodes.BackPostTraceMissing, "Dòng phiếu nhận không thuộc dòng đơn mua được yêu cầu back-post.");

        var existing = await _context.PurchaseAdviceFulfillmentPostings
            .SingleOrDefaultAsync(x => x.BranchReceiptLineId == branchReceiptLineId
                && x.PurchaseOrderLineAllocationId == allocation.PurchaseOrderLineAllocationId
                && x.PostingType == PurchaseAdviceFulfillmentPostingTypes.Accepted);
        if (existing != null)
        {
            if (existing.Quantity != acceptedQuantity
                || existing.PurchaseAdviceLineId != allocation.PurchaseAdviceLineId)
            {
                return Failure(PurchaseAdviceErrorCodes.BackPostConflict, "Dòng phiếu nhận đã được back-post với số lượng hoặc phân bổ khác.");
            }

            var replayLine = await LoadAdviceLineAsync(allocation.PurchaseAdviceLineId);
            if (replayLine != null)
            {
                await RefreshCachedQuantitiesAsync(replayLine);
                await _context.SaveChangesAsync();
            }
            if (replayLine != null)
            {
                await RecomputeHeaderStatusAsync(replayLine.PurchaseAdviceId, actorStaffId, "Đồng bộ lại aggregate Accepted từ ledger.");
                await _context.SaveChangesAsync();
            }
            return ServiceResult.Success("Accepted đã được ghi nhận trước đó.");
        }

        var line = await LoadAdviceLineAsync(allocation.PurchaseAdviceLineId);
        if (line == null)
            return Failure(PurchaseAdviceErrorCodes.BackPostTraceMissing, "Không tìm thấy Purchase Advice line để ghi Accepted.");

        var accepted = await SumPostingQuantityAsync(line.PurchaseAdviceLineId, PurchaseAdviceFulfillmentPostingTypes.Accepted);
        var closed = await SumPostingQuantityAsync(line.PurchaseAdviceLineId, PurchaseAdviceFulfillmentPostingTypes.Closed);
        var allocationAccepted = await SumPostingQuantityAsync(
            line.PurchaseAdviceLineId,
            PurchaseAdviceFulfillmentPostingTypes.Accepted,
            allocation.PurchaseOrderLineAllocationId);
        var allocationClosed = await SumPostingQuantityAsync(
            line.PurchaseAdviceLineId,
            PurchaseAdviceFulfillmentPostingTypes.Closed,
            allocation.PurchaseOrderLineAllocationId);
        var totalAllocated = await _context.PurchaseOrderLineAllocations
            .Where(x => x.PurchaseAdviceLineId == line.PurchaseAdviceLineId)
            .Select(x => x.AllocatedBaseQuantity)
            .ToListAsync();
        if (allocationAccepted + allocationClosed + acceptedQuantity > allocation.AllocatedBaseQuantity)
        {
            return Failure(
                PurchaseAdviceErrorCodes.AcceptedExceedsAllocation,
                "Số lượng Accepted vượt số lượng đã phân bổ cho Purchase Advice.");
        }
        if (accepted + closed + acceptedQuantity > totalAllocated.Sum())
        {
            return Failure(
                PurchaseAdviceErrorCodes.AcceptedExceedsAllocation,
                "Tổng số lượng Accepted vượt tổng số lượng đã phân bổ cho Purchase Advice.");
        }

        var sourceHash = ComputeHash($"{branchReceiptLineId}|{allocation.PurchaseOrderLineAllocationId}|{acceptedQuantity:0.000}");
        _context.PurchaseAdviceFulfillmentPostings.Add(new PurchaseAdviceFulfillmentPosting
        {
            PurchaseAdviceLineId = allocation.PurchaseAdviceLineId,
            PurchaseOrderLineAllocationId = allocation.PurchaseOrderLineAllocationId,
            PurchaseOrderLineId = purchaseOrderLineId,
            BranchReceiptLineId = branchReceiptLineId,
            PostingType = PurchaseAdviceFulfillmentPostingTypes.Accepted,
            Quantity = acceptedQuantity,
            BaseUnitId = line.BaseUnitId,
            SourceDocumentType = PurchaseAdviceFulfillmentSourceTypes.BranchReceiptLine,
            SourceDocumentId = receiptLine.BranchReceiptId,
            SourceDocumentLineId = branchReceiptLineId,
            PayloadHash = sourceHash,
            ActorStaffId = actorStaffId,
            CreatedAtUtc = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        await RefreshCachedQuantitiesAsync(line);
        await RecomputeHeaderStatusAsync(line.PurchaseAdviceId, actorStaffId, "Back-post Accepted từ phiếu nhận hàng.");
        await _context.SaveChangesAsync();
        return ServiceResult.Success("Đã back-post Accepted về Purchase Advice.");
    }

    public async Task<ServiceResult> BackPostClosedAsync(
        int purchaseOrderLineId,
        decimal closedQuantity,
        string closeOperationKey,
        string payloadHash,
        int actorStaffId)
    {
        if (closedQuantity <= 0)
            return Failure(PurchaseAdviceErrorCodes.ClosedExceedsAllocation, "Số lượng đóng không giao bù phải lớn hơn 0.");

        var allocation = await LoadAllocationForUpdateAsync(purchaseOrderLineId);
        if (allocation == null)
            return await HandleMissingAllocationAsync(purchaseOrderLineId);

        var existing = await _context.PurchaseAdviceFulfillmentPostings
            .SingleOrDefaultAsync(x => x.CloseOperationKey == closeOperationKey
                && x.PurchaseOrderLineAllocationId == allocation.PurchaseOrderLineAllocationId
                && x.PostingType == PurchaseAdviceFulfillmentPostingTypes.Closed);
        if (existing != null)
        {
            if (existing.PayloadHash != payloadHash
                || existing.Quantity != closedQuantity
                || existing.PurchaseOrderLineId != purchaseOrderLineId)
            {
                return Failure(PurchaseAdviceErrorCodes.BackPostConflict, "RequestKey đã được dùng cho payload Close Remaining khác.");
            }

            var replayLine = await LoadAdviceLineAsync(allocation.PurchaseAdviceLineId);
            if (replayLine != null)
            {
                await RefreshCachedQuantitiesAsync(replayLine);
                await _context.SaveChangesAsync();
                await RecomputeHeaderStatusAsync(replayLine.PurchaseAdviceId, actorStaffId, "Đồng bộ lại aggregate Closed từ ledger.");
                await _context.SaveChangesAsync();
            }
            return ServiceResult.Success("Close Remaining đã được back-post trước đó.");
        }

        var line = await LoadAdviceLineAsync(allocation.PurchaseAdviceLineId);
        if (line == null)
            return Failure(PurchaseAdviceErrorCodes.BackPostTraceMissing, "Không tìm thấy Purchase Advice line để ghi Closed.");

        var accepted = await SumPostingQuantityAsync(line.PurchaseAdviceLineId, PurchaseAdviceFulfillmentPostingTypes.Accepted);
        var closed = await SumPostingQuantityAsync(line.PurchaseAdviceLineId, PurchaseAdviceFulfillmentPostingTypes.Closed);
        var allocationAccepted = await SumPostingQuantityAsync(
            line.PurchaseAdviceLineId,
            PurchaseAdviceFulfillmentPostingTypes.Accepted,
            allocation.PurchaseOrderLineAllocationId);
        var allocationClosed = await SumPostingQuantityAsync(
            line.PurchaseAdviceLineId,
            PurchaseAdviceFulfillmentPostingTypes.Closed,
            allocation.PurchaseOrderLineAllocationId);
        var totalAllocated = await _context.PurchaseOrderLineAllocations
            .Where(x => x.PurchaseAdviceLineId == line.PurchaseAdviceLineId)
            .Select(x => x.AllocatedBaseQuantity)
            .ToListAsync();
        if (allocationAccepted + allocationClosed + closedQuantity > allocation.AllocatedBaseQuantity)
        {
            return Failure(
                PurchaseAdviceErrorCodes.ClosedExceedsAllocation,
                "Số lượng Closed vượt số lượng đã phân bổ cho Purchase Advice.");
        }
        if (accepted + closed + closedQuantity > totalAllocated.Sum())
        {
            return Failure(
                PurchaseAdviceErrorCodes.ClosedExceedsAllocation,
                "Tổng số lượng Closed vượt tổng số lượng đã phân bổ cho Purchase Advice.");
        }

        _context.PurchaseAdviceFulfillmentPostings.Add(new PurchaseAdviceFulfillmentPosting
        {
            PurchaseAdviceLineId = allocation.PurchaseAdviceLineId,
            PurchaseOrderLineAllocationId = allocation.PurchaseOrderLineAllocationId,
            PurchaseOrderLineId = purchaseOrderLineId,
            CloseOperationKey = closeOperationKey,
            PostingType = PurchaseAdviceFulfillmentPostingTypes.Closed,
            Quantity = closedQuantity,
            BaseUnitId = line.BaseUnitId,
            SourceDocumentType = PurchaseAdviceFulfillmentSourceTypes.PurchaseOrderCloseRemaining,
            SourceDocumentId = purchaseOrderLineId,
            SourceDocumentLineId = purchaseOrderLineId,
            PayloadHash = payloadHash,
            ActorStaffId = actorStaffId,
            CreatedAtUtc = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        await RefreshCachedQuantitiesAsync(line);
        await RecomputeHeaderStatusAsync(line.PurchaseAdviceId, actorStaffId, "Back-post Closed từ đóng phần còn lại PO.");
        await _context.SaveChangesAsync();
        return ServiceResult.Success("Đã back-post Closed về Purchase Advice.");
    }

    public async Task<PurchaseAdviceCloseReplay?> FindClosedReplayAsync(string closeOperationKey)
    {
        return await _context.PurchaseAdviceFulfillmentPostings.AsNoTracking()
            .Where(x => x.CloseOperationKey == closeOperationKey
                && x.PostingType == PurchaseAdviceFulfillmentPostingTypes.Closed)
            .Select(x => new PurchaseAdviceCloseReplay
            {
                PurchaseOrderLineId = x.PurchaseOrderLineId,
                Quantity = x.Quantity,
                PayloadHash = x.PayloadHash
            })
            .SingleOrDefaultAsync();
    }

    public async Task RecomputeHeaderStatusAsync(int purchaseAdviceId, int actorStaffId, string reason)
    {
        var advice = await _context.PurchaseAdvices
            .Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.PurchaseAdviceId == purchaseAdviceId);
        if (advice == null)
            return;

        var next = PurchaseAdviceStatusPolicy.DeriveHeaderStatus(advice);
        if (next == advice.Status)
            return;

        var previous = advice.Status;
        advice.Status = next;
        advice.UpdatedAtUtc = DateTime.UtcNow;
        advice.Transitions.Add(new PurchaseAdviceTransition
        {
            PreviousStatus = previous,
            NewStatus = next,
            ActorStaffId = actorStaffId,
            OccurredAtUtc = DateTime.UtcNow,
            Reason = reason
        });
    }

    public async Task<PurchaseAdviceBackfillDryRunReport> BuildBackfillDryRunReportAsync()
    {
        var items = new List<PurchaseAdviceBackfillDryRunItem>();
        var allocations = (await _context.PurchaseOrderLineAllocations.AsNoTracking().ToListAsync())
            .ToDictionary(x => x.PurchaseOrderLineId);
        var existingPostings = await _context.PurchaseAdviceFulfillmentPostings.AsNoTracking().ToListAsync();
        var receiptPostings = await _context.PurchaseOrderReceiptPostings.AsNoTracking()
            .Include(x => x.BranchReceiptLine)
            .ThenInclude(x => x.BranchReceipt)
            .Where(x => x.AcceptedBaseQuantity > 0
                && x.BranchReceiptLine.BranchReceipt.Status == BranchReceiptStatuses.Confirmed)
            .ToListAsync();

        foreach (var receiptPosting in receiptPostings)
        {
            if (!allocations.TryGetValue(receiptPosting.PurchaseOrderLineId, out var allocation))
            {
                items.Add(ManualReview(
                    PurchaseAdviceFulfillmentSourceTypes.BranchReceiptLine,
                    receiptPosting.BranchReceiptLine.BranchReceiptId,
                    receiptPosting.BranchReceiptLineId,
                    receiptPosting.PurchaseOrderLineId,
                    receiptPosting.AcceptedBaseQuantity,
                    "Không có PurchaseOrderLineAllocation để truy vết Accepted về Purchase Advice."));
                continue;
            }

            var exists = existingPostings.Any(x =>
                x.BranchReceiptLineId == receiptPosting.BranchReceiptLineId
                && x.PurchaseOrderLineAllocationId == allocation.PurchaseOrderLineAllocationId
                && x.PostingType == PurchaseAdviceFulfillmentPostingTypes.Accepted);
            items.Add(new PurchaseAdviceBackfillDryRunItem
            {
                Status = exists ? PurchaseAdviceBackfillStatuses.AlreadyPosted : PurchaseAdviceBackfillStatuses.Ready,
                SourceType = PurchaseAdviceFulfillmentSourceTypes.BranchReceiptLine,
                SourceDocumentId = receiptPosting.BranchReceiptLine.BranchReceiptId,
                SourceDocumentLineId = receiptPosting.BranchReceiptLineId,
                PurchaseOrderLineId = receiptPosting.PurchaseOrderLineId,
                PurchaseAdviceLineId = allocation.PurchaseAdviceLineId,
                Quantity = receiptPosting.AcceptedBaseQuantity,
                Message = exists ? "Accepted posting đã tồn tại." : "Có thể backfill Accepted bằng exact allocation."
            });
        }

        var closedLines = await _context.PurchaseOrderLines.AsNoTracking()
            .Where(x => x.ClosedRemainingQuantity > 0)
            .Select(x => new
            {
                x.PurchaseOrderLineId,
                x.PurchaseOrderId,
                x.ClosedRemainingQuantity
            })
            .ToListAsync();
        foreach (var closedLine in closedLines)
        {
            if (!allocations.TryGetValue(closedLine.PurchaseOrderLineId, out var allocation))
            {
                items.Add(ManualReview(
                    PurchaseAdviceFulfillmentSourceTypes.PurchaseOrderCloseRemaining,
                    closedLine.PurchaseOrderId,
                    closedLine.PurchaseOrderLineId,
                    closedLine.PurchaseOrderLineId,
                    closedLine.ClosedRemainingQuantity,
                    "Không có PurchaseOrderLineAllocation để truy vết Closed về Purchase Advice."));
                continue;
            }

            var exists = existingPostings.Any(x =>
                x.PurchaseOrderLineAllocationId == allocation.PurchaseOrderLineAllocationId
                && x.PurchaseOrderLineId == closedLine.PurchaseOrderLineId
                && x.PostingType == PurchaseAdviceFulfillmentPostingTypes.Closed);
            items.Add(new PurchaseAdviceBackfillDryRunItem
            {
                Status = exists ? PurchaseAdviceBackfillStatuses.AlreadyPosted : PurchaseAdviceBackfillStatuses.Ready,
                SourceType = PurchaseAdviceFulfillmentSourceTypes.PurchaseOrderCloseRemaining,
                SourceDocumentId = closedLine.PurchaseOrderId,
                SourceDocumentLineId = closedLine.PurchaseOrderLineId,
                PurchaseOrderLineId = closedLine.PurchaseOrderLineId,
                PurchaseAdviceLineId = allocation.PurchaseAdviceLineId,
                Quantity = closedLine.ClosedRemainingQuantity,
                Message = exists ? "Closed posting đã tồn tại." : "Có thể backfill Closed bằng exact allocation."
            });
        }

        var adviceLines = await _context.PurchaseAdviceLines.AsNoTracking().ToListAsync();
        foreach (var adviceLine in adviceLines)
        {
            var ledgerAccepted = existingPostings
                .Where(x => x.PurchaseAdviceLineId == adviceLine.PurchaseAdviceLineId
                    && x.PostingType == PurchaseAdviceFulfillmentPostingTypes.Accepted)
                .Sum(x => x.Quantity);
            var ledgerClosed = existingPostings
                .Where(x => x.PurchaseAdviceLineId == adviceLine.PurchaseAdviceLineId
                    && x.PostingType == PurchaseAdviceFulfillmentPostingTypes.Closed)
                .Sum(x => x.Quantity);
            var expected = ClampFulfillmentToDemand(
                adviceLine.RequestedPurchaseBaseQuantity,
                ledgerAccepted,
                ledgerClosed);
            if (expected.Accepted == adviceLine.AcceptedBaseQuantity
                && expected.Closed == adviceLine.ClosedBaseQuantity)
            {
                continue;
            }

            items.Add(new PurchaseAdviceBackfillDryRunItem
            {
                Status = PurchaseAdviceBackfillStatuses.AggregateDrift,
                SourceType = "PURCHASE_ADVICE_LINE",
                SourceDocumentId = adviceLine.PurchaseAdviceId,
                SourceDocumentLineId = adviceLine.PurchaseAdviceLineId,
                PurchaseAdviceLineId = adviceLine.PurchaseAdviceLineId,
                Quantity = ledgerAccepted + ledgerClosed,
                Message = $"Cache Accepted/Closed={adviceLine.AcceptedBaseQuantity:0.###}/{adviceLine.ClosedBaseQuantity:0.###}; expected demand aggregate={expected.Accepted:0.###}/{expected.Closed:0.###}; obligation ledger={ledgerAccepted:0.###}/{ledgerClosed:0.###}."
            });
        }

        return new PurchaseAdviceBackfillDryRunReport
        {
            AcceptedCandidateCount = items.Count(x => x.Status == PurchaseAdviceBackfillStatuses.Ready
                && x.SourceType == PurchaseAdviceFulfillmentSourceTypes.BranchReceiptLine),
            AcceptedCandidateQuantity = items.Where(x => x.Status == PurchaseAdviceBackfillStatuses.Ready
                && x.SourceType == PurchaseAdviceFulfillmentSourceTypes.BranchReceiptLine).Sum(x => x.Quantity),
            ClosedCandidateCount = items.Count(x => x.Status == PurchaseAdviceBackfillStatuses.Ready
                && x.SourceType == PurchaseAdviceFulfillmentSourceTypes.PurchaseOrderCloseRemaining),
            ClosedCandidateQuantity = items.Where(x => x.Status == PurchaseAdviceBackfillStatuses.Ready
                && x.SourceType == PurchaseAdviceFulfillmentSourceTypes.PurchaseOrderCloseRemaining).Sum(x => x.Quantity),
            ExistingPostingCount = items.Count(x => x.Status == PurchaseAdviceBackfillStatuses.AlreadyPosted),
            ManualReviewCount = items.Count(x => x.Status is PurchaseAdviceBackfillStatuses.ManualReviewRequired
                or PurchaseAdviceBackfillStatuses.AggregateDrift),
            Items = items
        };
    }

    public static string ComputeClosePayloadHash(int purchaseOrderLineId, string rowVersion, string reason)
    {
        return ComputeHash($"{purchaseOrderLineId}|{rowVersion.Trim()}|{reason.Trim()}");
    }

    private async Task<PurchaseAdviceLine?> LoadAdviceLineAsync(int id)
    {
        return await _context.PurchaseAdviceLines
            .Include(x => x.PurchaseAdvice)
            .SingleOrDefaultAsync(x => x.PurchaseAdviceLineId == id);
    }

    private async Task<decimal> SumPostingQuantityAsync(
        int purchaseAdviceLineId,
        string postingType,
        int? allocationId = null)
    {
        var query = _context.PurchaseAdviceFulfillmentPostings
            .Where(x => x.PurchaseAdviceLineId == purchaseAdviceLineId && x.PostingType == postingType);
        if (allocationId.HasValue)
            query = query.Where(x => x.PurchaseOrderLineAllocationId == allocationId.Value);
        var quantities = await query.Select(x => x.Quantity).ToListAsync();
        return quantities.Sum();
    }

    private async Task RefreshCachedQuantitiesAsync(PurchaseAdviceLine line)
    {
        var ledgerAccepted = await SumPostingQuantityAsync(
            line.PurchaseAdviceLineId,
            PurchaseAdviceFulfillmentPostingTypes.Accepted);
        var ledgerClosed = await SumPostingQuantityAsync(
            line.PurchaseAdviceLineId,
            PurchaseAdviceFulfillmentPostingTypes.Closed);
        var aggregate = ClampFulfillmentToDemand(
            line.RequestedPurchaseBaseQuantity,
            ledgerAccepted,
            ledgerClosed);
        line.AcceptedBaseQuantity = aggregate.Accepted;
        line.ClosedBaseQuantity = aggregate.Closed;
    }

    private static (decimal Accepted, decimal Closed) ClampFulfillmentToDemand(
        decimal requestedBaseQuantity,
        decimal ledgerAccepted,
        decimal ledgerClosed)
    {
        var demand = Math.Max(0m, requestedBaseQuantity);
        var accepted = Math.Min(demand, Math.Max(0m, ledgerAccepted));
        var closed = Math.Min(
            Math.Max(0m, demand - accepted),
            Math.Max(0m, ledgerClosed));
        return (accepted, closed);
    }

    private async Task<PurchaseOrderLineAllocation?> LoadAllocationForUpdateAsync(int purchaseOrderLineId)
    {
        if (_context.Database.IsSqlServer())
        {
            return await _context.PurchaseOrderLineAllocations.FromSqlInterpolated(
                    $"SELECT * FROM PurchaseOrderLineAllocations WITH (UPDLOCK, HOLDLOCK, ROWLOCK) WHERE PurchaseOrderLineId = {purchaseOrderLineId}")
                .SingleOrDefaultAsync();
        }

        return await _context.PurchaseOrderLineAllocations
            .SingleOrDefaultAsync(x => x.PurchaseOrderLineId == purchaseOrderLineId);
    }

    private async Task<ServiceResult> HandleMissingAllocationAsync(int purchaseOrderLineId)
    {
        var orderLine = await _context.PurchaseOrderLines.AsNoTracking()
            .Where(x => x.PurchaseOrderLineId == purchaseOrderLineId)
            .Select(x => new { x.PurchaseOrder.PurchaseOrderBatchId })
            .SingleOrDefaultAsync();
        if (orderLine == null)
            return Failure(PurchaseAdviceErrorCodes.AllocationNotFound, "Không tìm thấy dòng đơn mua để back-post Purchase Advice.");
        if (!orderLine.PurchaseOrderBatchId.HasValue)
            return ServiceResult.Success("Đơn mua thủ công không có Purchase Advice để back-post.");
        return Failure(PurchaseAdviceErrorCodes.AllocationNotFound, "Đơn mua từ Purchase Advice bị thiếu phân bổ truy vết.");
    }

    private static ServiceResult Failure(string code, string message) => ServiceResult.Failure(message, errorCode: code);

    private static PurchaseAdviceBackfillDryRunItem ManualReview(
        string sourceType,
        int sourceDocumentId,
        int sourceDocumentLineId,
        int purchaseOrderLineId,
        decimal quantity,
        string message) => new()
        {
            Status = PurchaseAdviceBackfillStatuses.ManualReviewRequired,
            SourceType = sourceType,
            SourceDocumentId = sourceDocumentId,
            SourceDocumentLineId = sourceDocumentLineId,
            PurchaseOrderLineId = purchaseOrderLineId,
            Quantity = quantity,
            Message = message
        };

    private static string ComputeHash(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }
}
