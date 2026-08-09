using CafeChain.Application.Constants;
using CafeChain.Application.Services.Admin.Production;
using CafeChain.Data;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Production;
using CafeChain.Models.Inventories.Stock;
using Xunit;

namespace CafeChain.Tests;

public sealed class ProductionLegacyAuditTests : IntegrationTestBase
{
    [Fact]
    public async Task DryRun_FlagsAmbiguousLegacyRecordsWithoutModifyingData()
    {
        using var context = CreateDbContext();
        context.ProductionRuns.Add(new ProductionRun
        {
            StoreId = 1,
            RecipeId = 1,
            RequestedRunCount = 1.5m,
            ContractVersion = 1,
            RequestKey = Guid.NewGuid(),
            RequestFingerprint = new string('L', 64),
            Status = ProductionRunStatus.Completed,
            ValuationStatus = ProductionValuationStatus.Complete,
            CreatedByStaffId = 1,
            CreatedAt = DateTime.UtcNow,
            ConfirmedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        });
        context.RestockSourcingAllocations.Add(new RestockSourcingAllocation
        {
            RestockRequestId = 98765,
            DecisionType = RestockSourcingDecisionTypes.Production,
            ProcurementQuantity = 10,
            ProcurementUnitId = 1,
            Status = RestockSourcingAllocationStatuses.Active,
            SourceDocumentType = "PRODUCTION_RUN",
            SourceDocumentId = 99,
            ProductionRunId = null,
            CreatedByStaffId = 1,
            CreatedAtUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        var beforeRuns = context.ProductionRuns.Count();
        var beforeAllocations = context.RestockSourcingAllocations.Count();

        var report = await new ProductionLegacyAuditService(context).DryRunAsync();

        Assert.True(report.DryRun);
        Assert.Equal(1, report.LegacyFractionalRunCount);
        Assert.Equal(1, report.OrphanProductionAllocationCount);
        Assert.Contains(report.Items, x => x.IssueCode == "LEGACY_FRACTIONAL_RUN" && x.ReviewStatus == "NEEDS_REVIEW");
        Assert.Contains(report.Items, x => x.IssueCode == "ORPHAN_PRODUCTION_ALLOCATION" && x.ReviewStatus == "NEEDS_REVIEW");
        Assert.Equal(beforeRuns, context.ProductionRuns.Count());
        Assert.Equal(beforeAllocations, context.RestockSourcingAllocations.Count());
    }
}
