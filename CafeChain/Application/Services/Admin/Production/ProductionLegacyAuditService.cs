using CafeChain.Application.DTOs.Admin.Production;
using CafeChain.Application.Interfaces.Admin.Production;
using CafeChain.Data;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Admin.Production;

public sealed class ProductionLegacyAuditService : IProductionLegacyAuditService
{
    private readonly AppDbContext _context;

    public ProductionLegacyAuditService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ProductionLegacyAuditReportDto> DryRunAsync()
    {
        var items = new List<ProductionLegacyAuditItemDto>();
        var legacyRuns = await _context.ProductionRuns.AsNoTracking()
            .Where(x => x.ContractVersion == 1)
            .Select(x => new { x.ProductionRunId, x.StoreId, x.RequestedRunCount })
            .ToListAsync();
        foreach (var run in legacyRuns.Where(x => x.RequestedRunCount != decimal.Truncate(x.RequestedRunCount)))
        {
            items.Add(new ProductionLegacyAuditItemDto
            {
                IssueCode = "LEGACY_FRACTIONAL_RUN",
                ProductionRunId = run.ProductionRunId,
                StoreId = run.StoreId,
                Message = $"Lệnh cũ có số mẻ thập phân {run.RequestedRunCount:0.#####}; giữ nguyên lịch sử, không chuyển sang contract v2."
            });
        }

        var productionAllocations = await _context.RestockSourcingAllocations.AsNoTracking()
            .Where(x => x.DecisionType == Constants.RestockSourcingDecisionTypes.Production)
            .Select(x => new
            {
                x.RestockSourcingAllocationId,
                x.RestockRequestId,
                x.ProductionRunId,
                x.SourceDocumentId
            }).ToListAsync();
        foreach (var allocation in productionAllocations.Where(x => !x.ProductionRunId.HasValue))
        {
            items.Add(new ProductionLegacyAuditItemDto
            {
                IssueCode = "ORPHAN_PRODUCTION_ALLOCATION",
                RestockSourcingAllocationId = allocation.RestockSourcingAllocationId,
                Message = "Phân bổ nguồn sản xuất chưa có liên kết ProductionRun xác định; không tự ghép theo thời gian."
            });
        }
        foreach (var duplicate in productionAllocations
                     .Where(x => x.ProductionRunId.HasValue)
                     .GroupBy(x => x.ProductionRunId!.Value)
                     .Where(x => x.Count() > 1))
        {
            foreach (var allocation in duplicate)
            {
                items.Add(new ProductionLegacyAuditItemDto
                {
                    IssueCode = "DUPLICATE_RUN_ALLOCATION",
                    ProductionRunId = duplicate.Key,
                    RestockSourcingAllocationId = allocation.RestockSourcingAllocationId,
                    Message = "Một lệnh sản xuất đang liên kết nhiều phân bổ Restock; cần quyết định thủ công trước migration v2."
                });
            }
        }

        var activeRecipeItems = await _context.Recipes.AsNoTracking()
            .Where(x => x.Active && x.PreparedItemId.HasValue)
            .Select(x => x.PreparedItemId!.Value)
            .Distinct()
            .ToListAsync();
        var explicitCapabilities = await _context.InventoryItemSourceCapabilities.AsNoTracking()
            .Where(x => x.PreparedItemId.HasValue)
            .Select(x => x.PreparedItemId!.Value)
            .ToListAsync();
        foreach (var preparedItemId in activeRecipeItems.Except(explicitCapabilities))
        {
            items.Add(new ProductionLegacyAuditItemDto
            {
                IssueCode = "PRODUCTION_CAPABILITY_UNCONFIRMED",
                PreparedItemId = preparedItemId,
                Message = "Bán thành phẩm có công thức nhưng chưa có capability sản xuất toàn cục; không tự bật khi thiếu bằng chứng."
            });
        }

        return new ProductionLegacyAuditReportDto
        {
            GeneratedAtUtc = DateTime.UtcNow,
            DryRun = true,
            LegacyFractionalRunCount = items.Count(x => x.IssueCode == "LEGACY_FRACTIONAL_RUN"),
            OrphanProductionAllocationCount = items.Count(x => x.IssueCode == "ORPHAN_PRODUCTION_ALLOCATION"),
            DuplicateRunAllocationCount = items.Count(x => x.IssueCode == "DUPLICATE_RUN_ALLOCATION"),
            MissingCapabilityReviewCount = items.Count(x => x.IssueCode == "PRODUCTION_CAPABILITY_UNCONFIRMED"),
            Items = items
        };
    }
}
