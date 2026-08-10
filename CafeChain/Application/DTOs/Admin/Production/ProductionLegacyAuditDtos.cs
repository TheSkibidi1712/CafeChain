namespace CafeChain.Application.DTOs.Admin.Production;

public sealed class ProductionLegacyAuditReportDto
{
    public DateTime GeneratedAtUtc { get; set; }
    public bool DryRun { get; set; } = true;
    public int LegacyFractionalRunCount { get; set; }
    public int OrphanProductionAllocationCount { get; set; }
    public int DuplicateRunAllocationCount { get; set; }
    public int MissingCapabilityReviewCount { get; set; }
    public IReadOnlyList<ProductionLegacyAuditItemDto> Items { get; set; } = [];
}

public sealed class ProductionLegacyAuditItemDto
{
    public string ReviewStatus { get; set; } = "NEEDS_REVIEW";
    public string IssueCode { get; set; } = string.Empty;
    public int? ProductionRunId { get; set; }
    public int? RestockSourcingAllocationId { get; set; }
    public int? StoreId { get; set; }
    public int? PreparedItemId { get; set; }
    public string Message { get; set; } = string.Empty;
}
