namespace CafeChain.Application.DTOs.Admin.Suppliers;

public sealed class SupplierProcurementDataQualityReportDTO
{
    public DateTime GeneratedAtUtc { get; set; }
    public bool DryRun { get; set; } = true;
    public int ScannedOfferCount { get; set; }
    public int ScannedStoreAssignmentCount { get; set; }
    public int ScannedRestockCount { get; set; }
    public List<SupplierProcurementDataQualityFindingDTO> Findings { get; set; } = new();
}

public sealed class SupplierProcurementDataQualityFindingDTO
{
    public string Code { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Resolution { get; set; } = "NEEDS_REVIEW";
}
