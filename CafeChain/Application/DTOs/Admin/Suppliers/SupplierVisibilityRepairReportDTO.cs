namespace CafeChain.Application.DTOs.Admin.Suppliers;

public sealed class SupplierVisibilityRepairReportDTO
{
    public bool DryRun { get; init; }
    public int SupplierCount { get; init; }
    public int LegacyHiddenCount { get; init; }
    public int SafeChangesApplied { get; init; }
    public List<SupplierVisibilityFindingDTO> Findings { get; init; } = new();
}

public sealed class SupplierVisibilityFindingDTO
{
    public int SupplierId { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public bool Active { get; init; }
    public bool HasActiveStoreCoverage { get; init; }
    public bool HasDownstreamReferences { get; init; }
    public bool RequiresManualReview { get; init; }
    public IReadOnlyList<int> PossibleDuplicateSupplierIds { get; init; } = Array.Empty<int>();
    public string Resolution { get; init; } = "";
}
