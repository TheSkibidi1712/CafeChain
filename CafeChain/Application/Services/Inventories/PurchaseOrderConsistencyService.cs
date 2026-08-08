using System.Text.Json;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Auditing;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Inventories;

public sealed class PurchaseOrderConsistencyService : IPurchaseOrderConsistencyService
{
    private const string SeedFixtureMarker = "DEMO_AI_DASHBOARD_ROLLING_V1_LINE_";
    private readonly AppDbContext _context;
    private readonly IUnitConversionService _conversion;

    public PurchaseOrderConsistencyService(AppDbContext context, IUnitConversionService conversion)
    {
        _context = context;
        _conversion = conversion;
    }

    public async Task<PurchaseOrderConsistencyReportDto> DryRunAsync()
    {
        var rows = await _context.PurchaseOrderLines.AsNoTracking().AsSplitQuery()
            .Include(x => x.PurchaseOrder)
            .Include(x => x.Ingredient).ThenInclude(x => x.BaseUnit)
            .Include(x => x.PackageUnitSnapshot)
            .Include(x => x.ReceiptPostings)
            .Include(x => x.Closures)
            .OrderBy(x => x.PurchaseOrderLineId)
            .ToListAsync();
        var findings = new List<PurchaseOrderConsistencyItemDto>();

        foreach (var line in rows)
        {
            decimal? expectedBase = null;
            if (line.PurchaseMode == PurchaseMode.Packaged
                && line.OrderedPackageCount.GetValueOrDefault() > 0m
                && line.PackageQuantitySnapshot.GetValueOrDefault() > 0m
                && line.PackageUnitIdSnapshot.HasValue)
            {
                var converted = await _conversion.ConvertAsync(
                    line.IngredientId,
                    line.OrderedPackageCount!.Value * line.PackageQuantitySnapshot!.Value,
                    line.PackageUnitIdSnapshot.Value,
                    line.Ingredient.BaseUnitId);
                if (converted.IsSuccess)
                    expectedBase = converted.Data;
            }

            var accepted = line.ReceiptPostings.Sum(x => x.AcceptedBaseQuantity);
            var rejected = line.ReceiptPostings.Sum(x => x.RejectedBaseQuantity);
            var eventClosed = line.Closures.Sum(x => x.ClosedBaseQuantity);
            var outstanding = Math.Max(0m, line.OrderedBaseQuantity - accepted - line.ClosedRemainingQuantity);
            var hasDownstream = line.ReceiptPostings.Count > 0 || line.Closures.Count > 0;

            if (line.PurchaseMode == PurchaseMode.Packaged && !expectedBase.HasValue)
                findings.Add(Item(line, PurchaseOrderConsistencyStatuses.InvalidBlocking,
                    "PACKAGE_SNAPSHOT_CONVERSION_MISSING",
                    "Không thể quy đổi snapshot gói mua về đơn vị tồn cơ sở.", expectedBase, accepted, rejected, outstanding));
            else if (expectedBase.HasValue && expectedBase.Value != line.OrderedBaseQuantity)
                findings.Add(Item(line,
                    hasDownstream ? PurchaseOrderConsistencyStatuses.NeedsReview : PurchaseOrderConsistencyStatuses.SafeAutoRepair,
                    "PACKAGE_ORDERED_BASE_MISMATCH",
                    $"Snapshot gói tương đương {expectedBase.Value:0.###} {line.Ingredient.BaseUnit.Name}, nhưng dòng đang lưu {line.OrderedBaseQuantity:0.###}.",
                    expectedBase, accepted, rejected, outstanding));

            if (line.ClosedRemainingQuantity != eventClosed)
            {
                var deterministicSeedGhost = line.Note?.StartsWith(SeedFixtureMarker, StringComparison.Ordinal) == true
                    && !hasDownstream
                    && string.IsNullOrWhiteSpace(line.CloseRemainingReason)
                    && !line.ClosedRemainingByStaffId.HasValue
                    && !line.ClosedRemainingAtUtc.HasValue;
                findings.Add(Item(line,
                    deterministicSeedGhost ? PurchaseOrderConsistencyStatuses.SafeAutoRepair : PurchaseOrderConsistencyStatuses.NeedsReview,
                    "CLOSURE_EVENT_AGGREGATE_MISMATCH",
                    "Số lượng đã đóng không khớp tổng sự kiện đóng nghĩa vụ có actor, thời gian và lý do.",
                    expectedBase, accepted, rejected, outstanding));
            }

            if (accepted > line.OrderedBaseQuantity
                || line.ClosedRemainingQuantity > Math.Max(0m, line.OrderedBaseQuantity - accepted))
                findings.Add(Item(line, PurchaseOrderConsistencyStatuses.InvalidBlocking,
                    "OBLIGATION_NEGATIVE",
                    "Tổng đã nhận hoặc đã đóng vượt số lượng đặt.", expectedBase, accepted, rejected, outstanding));

            if (line.PurchaseOrder.Status == PurchaseOrderStatuses.Completed && outstanding > 0m)
                findings.Add(Item(line, PurchaseOrderConsistencyStatuses.InvalidBlocking,
                    "COMPLETED_WITH_OUTSTANDING",
                    "Đơn được đánh dấu hoàn tất nhưng vẫn còn nghĩa vụ giao hàng.", expectedBase, accepted, rejected, outstanding));
        }

        return BuildReport(findings, dryRun: true, repairedCount: 0);
    }

    public async Task<ServiceResult<PurchaseOrderConsistencyReportDto>> RepairSafeAsync(int actorStaffId)
    {
        if (actorStaffId <= 0)
            return ServiceResult<PurchaseOrderConsistencyReportDto>.Failure("Người thực hiện repair không hợp lệ.");
        var before = await DryRunAsync();
        var candidates = before.Items
            .Where(x => x.Classification == PurchaseOrderConsistencyStatuses.SafeAutoRepair)
            .GroupBy(x => x.PurchaseOrderLineId)
            .ToArray();
        var repaired = 0;

        await using var transaction = await _context.Database.BeginTransactionAsync();
        foreach (var candidate in candidates)
        {
            var line = await _context.PurchaseOrderLines
                .AsSplitQuery()
                .Include(x => x.Ingredient)
                .Include(x => x.ReceiptPostings)
                .Include(x => x.Closures)
                .SingleAsync(x => x.PurchaseOrderLineId == candidate.Key);
            if (line.ReceiptPostings.Count > 0 || line.Closures.Count > 0)
                continue;

            var oldData = new
            {
                line.OrderedBaseQuantity,
                line.ClosedRemainingQuantity,
                line.InventoryBaseUnitId,
                line.ProcurementToInventoryFactor
            };
            var quantityFinding = candidate.FirstOrDefault(x => x.IssueCode == "PACKAGE_ORDERED_BASE_MISMATCH");
            if (quantityFinding?.ExpectedOrderedBaseQuantity is decimal expected)
            {
                line.OrderedBaseQuantity = expected;
                line.InventoryBaseUnitId = line.Ingredient?.BaseUnitId ?? line.InventoryBaseUnitId;
            }
            if (candidate.Any(x => x.IssueCode == "CLOSURE_EVENT_AGGREGATE_MISMATCH")
                && line.Note?.StartsWith(SeedFixtureMarker, StringComparison.Ordinal) == true)
            {
                line.ClosedRemainingQuantity = 0m;
                line.ClosedProcurementQuantity = 0m;
                line.CloseRemainingReason = null;
                line.ClosedRemainingByStaffId = null;
                line.ClosedRemainingAtUtc = null;
            }

            _context.AuditLogs.Add(new AuditLog
            {
                TableName = "PurchaseOrderLines",
                RecordId = line.PurchaseOrderLineId,
                Action = "PO_CONSISTENCY_SAFE_REPAIR",
                UserId = actorStaffId,
                CreatedAt = DateTime.UtcNow,
                OldData = JsonSerializer.Serialize(oldData),
                NewData = JsonSerializer.Serialize(new
                {
                    line.OrderedBaseQuantity,
                    line.ClosedRemainingQuantity,
                    line.InventoryBaseUnitId,
                    line.ProcurementToInventoryFactor,
                    Issues = candidate.Select(x => x.IssueCode).ToArray()
                })
            });
            repaired++;
        }

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        var after = await DryRunAsync();
        after.DryRun = false;
        after.RepairedCount = repaired;
        return ServiceResult<PurchaseOrderConsistencyReportDto>.Success(after, $"Đã sửa an toàn {repaired} dòng đơn đặt hàng.");
    }

    private static PurchaseOrderConsistencyItemDto Item(
        CafeChain.Models.Inventories.Procurement.PurchaseOrderLine line,
        string classification,
        string issueCode,
        string message,
        decimal? expected,
        decimal accepted,
        decimal rejected,
        decimal outstanding) => new()
        {
            PurchaseOrderId = line.PurchaseOrderId,
            PurchaseOrderLineId = line.PurchaseOrderLineId,
            PurchaseOrderCode = line.PurchaseOrder.Code,
            IngredientName = line.Ingredient.Name,
            Status = line.PurchaseOrder.Status,
            Classification = classification,
            IssueCode = issueCode,
            Message = message,
            OrderedBaseQuantity = line.OrderedBaseQuantity,
            ExpectedOrderedBaseQuantity = expected,
            AcceptedBaseQuantity = accepted,
            RejectedBaseQuantity = rejected,
            ClosedBaseQuantity = line.ClosedRemainingQuantity,
            OutstandingBaseQuantity = outstanding
        };

    private static PurchaseOrderConsistencyReportDto BuildReport(
        List<PurchaseOrderConsistencyItemDto> items,
        bool dryRun,
        int repairedCount) => new()
        {
            DryRun = dryRun,
            SafeAutoRepairCount = items.Count(x => x.Classification == PurchaseOrderConsistencyStatuses.SafeAutoRepair),
            NeedsReviewCount = items.Count(x => x.Classification == PurchaseOrderConsistencyStatuses.NeedsReview),
            InvalidBlockingCount = items.Count(x => x.Classification == PurchaseOrderConsistencyStatuses.InvalidBlocking),
            RepairedCount = repairedCount,
            Items = items
        };
}
