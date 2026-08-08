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

        var trace = await LoadOrderTraceForUpdateAsync(purchaseOrderLineId);
        if (trace == null)
            return await HandleMissingAllocationAsync(purchaseOrderLineId);

        var receiptLine = await _context.BranchReceiptLines.AsNoTracking()
            .Where(x => x.BranchReceiptLineId == branchReceiptLineId)
            .Select(x => new { x.BranchReceiptLineId, x.BranchReceiptId, x.PurchaseOrderLineId })
            .SingleOrDefaultAsync();
        if (receiptLine == null)
            return Failure(PurchaseAdviceErrorCodes.BackPostTraceMissing, "Không tìm thấy dòng phiếu nhận để truy vết số lượng chấp nhận.");
        if (receiptLine.PurchaseOrderLineId != purchaseOrderLineId)
            return Failure(PurchaseAdviceErrorCodes.BackPostTraceMissing, "Dòng phiếu nhận không thuộc dòng đơn mua cần ghi nhận.");

        var existing = await _context.PurchaseAdviceFulfillmentPostings
            .SingleOrDefaultAsync(x => x.BranchReceiptLineId == branchReceiptLineId
                && x.PurchaseOrderLineAllocationId == trace.AllocationId
                && x.PurchaseOrderLineId == purchaseOrderLineId
                && x.PostingType == PurchaseAdviceFulfillmentPostingTypes.Accepted);
        if (existing != null)
        {
            if (existing.Quantity != acceptedQuantity
                || existing.PurchaseAdviceLineId != trace.PurchaseAdviceLineId)
            {
                return Failure(PurchaseAdviceErrorCodes.BackPostConflict, "Dòng phiếu nhận đã được ghi nhận với số lượng hoặc phân bổ khác.");
            }

            var replayLine = await LoadAdviceLineAsync(trace.PurchaseAdviceLineId);
            if (replayLine != null)
            {
                await RefreshCachedQuantitiesAsync(replayLine);
                await _context.SaveChangesAsync();
            }
            if (replayLine != null)
            {
                await RecomputeHeaderStatusAsync(replayLine.PurchaseAdviceId, actorStaffId, "Đồng bộ lại tổng số lượng chấp nhận từ sổ theo dõi.");
                await _context.SaveChangesAsync();
            }
            return ServiceResult.Success("Số lượng chấp nhận đã được ghi nhận trước đó.");
        }

        var line = await LoadAdviceLineAsync(trace.PurchaseAdviceLineId);
        if (line == null)
            return Failure(PurchaseAdviceErrorCodes.BackPostTraceMissing, "Không tìm thấy dòng đề nghị mua để ghi số lượng chấp nhận.");

        var accepted = await SumPostingQuantityAsync(line.PurchaseAdviceLineId, PurchaseAdviceFulfillmentPostingTypes.Accepted);
        var closed = await SumPostingQuantityAsync(line.PurchaseAdviceLineId, PurchaseAdviceFulfillmentPostingTypes.Closed);
        var allocationAccepted = await SumPostingQuantityAsync(
            line.PurchaseAdviceLineId,
            PurchaseAdviceFulfillmentPostingTypes.Accepted,
            trace.AllocationId,
            purchaseOrderLineId);
        var allocationClosed = await SumPostingQuantityAsync(
            line.PurchaseAdviceLineId,
            PurchaseAdviceFulfillmentPostingTypes.Closed,
            trace.AllocationId,
            purchaseOrderLineId);
        var totalAllocated = await SumActiveAllocatedBaseQuantityAsync(line.PurchaseAdviceLineId);
        if (allocationAccepted + allocationClosed + acceptedQuantity > trace.AllocatedBaseQuantity)
        {
            return Failure(
                PurchaseAdviceErrorCodes.AcceptedExceedsAllocation,
                "Số lượng chấp nhận vượt số lượng đã phân bổ cho đề nghị mua.");
        }
        if (accepted + closed + acceptedQuantity > totalAllocated)
        {
            return Failure(
                PurchaseAdviceErrorCodes.AcceptedExceedsAllocation,
                "Tổng số lượng chấp nhận vượt tổng số lượng đã phân bổ cho đề nghị mua.");
        }

        var sourceHash = ComputeHash($"{branchReceiptLineId}|{trace.AllocationId?.ToString() ?? $"PO-LINE-{purchaseOrderLineId}"}|{acceptedQuantity:0.000}");
        _context.PurchaseAdviceFulfillmentPostings.Add(new PurchaseAdviceFulfillmentPosting
        {
            PurchaseAdviceLineId = trace.PurchaseAdviceLineId,
            PurchaseOrderLineAllocationId = trace.AllocationId,
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
        await RecomputeHeaderStatusAsync(line.PurchaseAdviceId, actorStaffId, "Ghi nhận số lượng chấp nhận từ phiếu nhận hàng.");
        await _context.SaveChangesAsync();
        return ServiceResult.Success("Đã ghi số lượng chấp nhận về đề nghị mua.");
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

        var trace = await LoadOrderTraceForUpdateAsync(purchaseOrderLineId);
        if (trace == null)
            return await HandleMissingAllocationAsync(purchaseOrderLineId);

        var existing = await _context.PurchaseAdviceFulfillmentPostings
            .SingleOrDefaultAsync(x => x.CloseOperationKey == closeOperationKey
                && x.PurchaseOrderLineAllocationId == trace.AllocationId
                && x.PurchaseOrderLineId == purchaseOrderLineId
                && x.PostingType == PurchaseAdviceFulfillmentPostingTypes.Closed);
        if (existing != null)
        {
            if (existing.PayloadHash != payloadHash
                || existing.Quantity != closedQuantity
                || existing.PurchaseOrderLineId != purchaseOrderLineId)
            {
                return Failure(PurchaseAdviceErrorCodes.BackPostConflict, "Mã chống gửi trùng đã được dùng cho dữ liệu đóng phần còn lại khác.");
            }

            var replayLine = await LoadAdviceLineAsync(trace.PurchaseAdviceLineId);
            if (replayLine != null)
            {
                await RefreshCachedQuantitiesAsync(replayLine);
                await _context.SaveChangesAsync();
                await RecomputeHeaderStatusAsync(replayLine.PurchaseAdviceId, actorStaffId, "Đồng bộ lại tổng số lượng đóng từ sổ theo dõi.");
                await _context.SaveChangesAsync();
            }
            return ServiceResult.Success("Phần còn lại đã được đóng và ghi nhận trước đó.");
        }

        var line = await LoadAdviceLineAsync(trace.PurchaseAdviceLineId);
        if (line == null)
            return Failure(PurchaseAdviceErrorCodes.BackPostTraceMissing, "Không tìm thấy dòng đề nghị mua để ghi số lượng đóng.");

        var accepted = await SumPostingQuantityAsync(line.PurchaseAdviceLineId, PurchaseAdviceFulfillmentPostingTypes.Accepted);
        var closed = await SumPostingQuantityAsync(line.PurchaseAdviceLineId, PurchaseAdviceFulfillmentPostingTypes.Closed);
        var allocationAccepted = await SumPostingQuantityAsync(
            line.PurchaseAdviceLineId,
            PurchaseAdviceFulfillmentPostingTypes.Accepted,
            trace.AllocationId,
            purchaseOrderLineId);
        var allocationClosed = await SumPostingQuantityAsync(
            line.PurchaseAdviceLineId,
            PurchaseAdviceFulfillmentPostingTypes.Closed,
            trace.AllocationId,
            purchaseOrderLineId);
        var totalAllocated = await SumActiveAllocatedBaseQuantityAsync(line.PurchaseAdviceLineId);
        if (allocationAccepted + allocationClosed + closedQuantity > trace.AllocatedBaseQuantity)
        {
            return Failure(
                PurchaseAdviceErrorCodes.ClosedExceedsAllocation,
                "Số lượng đóng vượt số lượng đã phân bổ cho đề nghị mua.");
        }
        if (accepted + closed + closedQuantity > totalAllocated)
        {
            return Failure(
                PurchaseAdviceErrorCodes.ClosedExceedsAllocation,
                "Tổng số lượng đóng vượt tổng số lượng đã phân bổ cho đề nghị mua.");
        }

        _context.PurchaseAdviceFulfillmentPostings.Add(new PurchaseAdviceFulfillmentPosting
        {
            PurchaseAdviceLineId = trace.PurchaseAdviceLineId,
            PurchaseOrderLineAllocationId = trace.AllocationId,
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
        await RecomputeHeaderStatusAsync(line.PurchaseAdviceId, actorStaffId, "Ghi nhận phần không giao bù từ đơn đặt hàng.");
        await _context.SaveChangesAsync();
        return ServiceResult.Success("Đã ghi số lượng đóng về đề nghị mua.");
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
                    "Không có phân bổ dòng đơn đặt hàng để truy vết số lượng chấp nhận về đề nghị mua."));
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
                Message = exists
                    ? "Bản ghi số lượng chấp nhận đã tồn tại."
                    : "Có thể bổ sung số lượng chấp nhận bằng phân bổ truy vết chính xác."
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
                    "Không có phân bổ dòng đơn đặt hàng để truy vết số lượng đóng về đề nghị mua."));
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
                Message = exists
                    ? "Bản ghi số lượng đóng đã tồn tại."
                    : "Có thể bổ sung số lượng đóng bằng phân bổ truy vết chính xác."
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
                Message = $"Số lượng chấp nhận/đóng đang lưu={adviceLine.AcceptedBaseQuantity:0.###}/{adviceLine.ClosedBaseQuantity:0.###}; tổng đúng theo nhu cầu={expected.Accepted:0.###}/{expected.Closed:0.###}; sổ nghĩa vụ={ledgerAccepted:0.###}/{ledgerClosed:0.###}."
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

    public static string ComputeClosePayloadHash(
        int purchaseOrderLineId,
        decimal closedBaseQuantity,
        string rowVersion,
        string reason)
    {
        return ComputeHash($"{purchaseOrderLineId}|{closedBaseQuantity:0.###}|{rowVersion.Trim()}|{reason.Trim()}");
    }

    public static string ComputeLegacyClosePayloadHash(int purchaseOrderLineId, string rowVersion, string reason)
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
        int? allocationId = null,
        int? purchaseOrderLineId = null)
    {
        var query = _context.PurchaseAdviceFulfillmentPostings
            .Where(x => x.PurchaseAdviceLineId == purchaseAdviceLineId && x.PostingType == postingType);
        if (allocationId.HasValue)
            query = query.Where(x => x.PurchaseOrderLineAllocationId == allocationId.Value);
        else if (purchaseOrderLineId.HasValue)
            query = query.Where(x => x.PurchaseOrderLineAllocationId == null
                && x.PurchaseOrderLineId == purchaseOrderLineId.Value);
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

        if (!line.RequestedProcurementQuantity.HasValue
            || line.RequestedProcurementQuantity.Value <= 0
            || !line.ProcurementUnitId.HasValue)
        {
            return;
        }

        var procurementEvidence = await _context.PurchaseOrderLineAllocations
            .AsNoTracking()
            .Where(x => x.PurchaseAdviceLineId == line.PurchaseAdviceLineId)
            .Select(x => new
            {
                x.PurchaseOrderLineId,
                x.DemandCoveredProcurementQuantity,
                x.PurchaseOrder.Status,
                x.PurchaseOrderLine.ClosedProcurementQuantity
            })
            .ToListAsync();
        var normalEvidence = await _context.PurchaseOrderLines
            .AsNoTracking()
            .Where(x => x.PurchaseAdviceLineId == line.PurchaseAdviceLineId
                && x.PurchaseOrder.PurchaseOrderBatchId == null)
            .Select(x => new
            {
                x.PurchaseOrderLineId,
                DemandCoveredProcurementQuantity = x.OrderedProcurementQuantity,
                x.PurchaseOrder.Status,
                x.ClosedProcurementQuantity
            })
            .ToListAsync();
        var purchaseOrderLineIds = procurementEvidence
            .Select(x => x.PurchaseOrderLineId)
            .Concat(normalEvidence.Select(x => x.PurchaseOrderLineId))
            .Distinct()
            .ToArray();
        var acceptedProcurementRows = await _context.PurchaseOrderReceiptPostings
            .AsNoTracking()
            .Where(x => purchaseOrderLineIds.Contains(x.PurchaseOrderLineId))
            .Select(x => new
            {
                x.PurchaseOrderLineId,
                x.AcceptedProcurementQuantity
            })
            .ToListAsync();

        line.AllocatedToPoProcurementQuantity = Math.Min(
            line.RequestedProcurementQuantity.Value,
            procurementEvidence
                .Where(x => x.Status != PurchaseOrderStatuses.Cancelled)
                .Sum(x => x.DemandCoveredProcurementQuantity ?? 0m)
            + normalEvidence
                .Where(x => x.Status != PurchaseOrderStatuses.Cancelled)
                .Sum(x => x.DemandCoveredProcurementQuantity ?? 0m));

        var procurementAggregate = ClampFulfillmentToDemand(
            line.RequestedProcurementQuantity.Value,
            acceptedProcurementRows.Sum(x => x.AcceptedProcurementQuantity ?? 0m),
            procurementEvidence.Sum(x => x.ClosedProcurementQuantity)
                + normalEvidence.Sum(x => x.ClosedProcurementQuantity));
        line.AcceptedProcurementQuantity = procurementAggregate.Accepted;
        line.ClosedProcurementQuantity = procurementAggregate.Closed;
    }

    private static (decimal Accepted, decimal Closed) ClampFulfillmentToDemand(
        decimal requestedQuantity,
        decimal ledgerAccepted,
        decimal ledgerClosed)
    {
        var demand = Math.Max(0m, requestedQuantity);
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

    private async Task<PurchaseOrderTrace?> LoadOrderTraceForUpdateAsync(int purchaseOrderLineId)
    {
        var allocation = await LoadAllocationForUpdateAsync(purchaseOrderLineId);
        if (allocation != null)
        {
            return new PurchaseOrderTrace(
                allocation.PurchaseAdviceLineId,
                allocation.PurchaseOrderLineAllocationId,
                allocation.AllocatedBaseQuantity);
        }

        PurchaseOrderLine? line;
        if (_context.Database.IsSqlServer())
        {
            line = await _context.PurchaseOrderLines.FromSqlInterpolated(
                    $"SELECT * FROM PurchaseOrderLines WITH (UPDLOCK, HOLDLOCK, ROWLOCK) WHERE PurchaseOrderLineId = {purchaseOrderLineId}")
                .SingleOrDefaultAsync();
        }
        else
        {
            line = await _context.PurchaseOrderLines.SingleOrDefaultAsync(
                x => x.PurchaseOrderLineId == purchaseOrderLineId);
        }

        if (line?.PurchaseAdviceLineId == null)
            return null;

        var belongsToBatch = await _context.PurchaseOrders
            .Where(x => x.PurchaseOrderId == line.PurchaseOrderId)
            .Select(x => x.PurchaseOrderBatchId.HasValue)
            .SingleAsync();
        if (belongsToBatch)
            return null;

        var requested = await _context.PurchaseAdviceLines
            .Where(x => x.PurchaseAdviceLineId == line.PurchaseAdviceLineId.Value)
            .Select(x => x.RequestedPurchaseBaseQuantity)
            .SingleAsync();
        return new PurchaseOrderTrace(
            line.PurchaseAdviceLineId.Value,
            null,
            Math.Min(requested, line.OrderedBaseQuantity));
    }

    private async Task<decimal> SumActiveAllocatedBaseQuantityAsync(int purchaseAdviceLineId)
    {
        var batchQuantities = await _context.PurchaseOrderLineAllocations
            .Where(x => x.PurchaseAdviceLineId == purchaseAdviceLineId
                && x.PurchaseOrder.Status != PurchaseOrderStatuses.Cancelled)
            .Select(x => x.AllocatedBaseQuantity)
            .ToListAsync();
        var normalQuantities = await _context.PurchaseOrderLines
            .Where(x => x.PurchaseAdviceLineId == purchaseAdviceLineId
                && x.PurchaseOrder.PurchaseOrderBatchId == null
                && x.PurchaseOrder.Status != PurchaseOrderStatuses.Cancelled)
            .Select(x => x.OrderedBaseQuantity)
            .ToListAsync();
        return batchQuantities.Sum() + normalQuantities.Sum();
    }

    private sealed record PurchaseOrderTrace(
        int PurchaseAdviceLineId,
        int? AllocationId,
        decimal AllocatedBaseQuantity);

    private async Task<ServiceResult> HandleMissingAllocationAsync(int purchaseOrderLineId)
    {
        var orderLine = await _context.PurchaseOrderLines.AsNoTracking()
            .Where(x => x.PurchaseOrderLineId == purchaseOrderLineId)
            .Select(x => new { x.PurchaseOrder.PurchaseOrderBatchId })
            .SingleOrDefaultAsync();
        if (orderLine == null)
            return Failure(PurchaseAdviceErrorCodes.AllocationNotFound, "Không tìm thấy dòng đơn mua để ghi nhận về đề nghị mua.");
        if (!orderLine.PurchaseOrderBatchId.HasValue)
            return ServiceResult.Success("Đơn mua thủ công không có đề nghị mua để ghi nhận.");
        return Failure(PurchaseAdviceErrorCodes.AllocationNotFound, "Đơn mua từ đề nghị mua bị thiếu phân bổ truy vết.");
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
