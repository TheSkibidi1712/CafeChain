namespace CafeChain.Application.DTOs.Admin.Replenishment;

public sealed class PreparedItemReplenishmentDto
{
    public int StoreInventoryId { get; set; }
    public int StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public int PreparedItemId { get; set; }
    public string PreparedItemName { get; set; } = string.Empty;
    public string PreparedItemCode { get; set; } = string.Empty;
    public int BaseUnitId { get; set; }
    public string BaseUnitCode { get; set; } = string.Empty;
    public string BaseUnitName { get; set; } = string.Empty;
    public decimal OnHandBase { get; set; }
    public decimal ReservedBase { get; set; }
    public decimal UsableBase { get; set; }
    public decimal? LowThresholdBase { get; set; }
    public decimal? TargetStockBase { get; set; }
    public bool IsLow { get; set; }
    public decimal? GrossNeedBase { get; set; }
    public decimal? OpenProductionCoverageBase { get; set; }
    public decimal? NetNeedBase { get; set; }
    public PreparedItemAlertSummaryDto? ActiveAlert { get; set; }
    public PreparedItemRequestSummaryDto? ActiveRestockRequest { get; set; }
    public IReadOnlyList<PreparedItemOpenProductionRunDto> OpenProductionRuns { get; set; } = [];
    public int OpenProductionRunTotal { get; set; }
    public bool HasMoreOpenProductionRuns { get; set; }
    public string DataStatus { get; set; } = PreparedItemReplenishmentDataStatuses.Ready;
    public string BusinessMessageVi { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class PreparedItemAlertSummaryDto
{
    public int StockAlertId { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class PreparedItemRequestSummaryDto
{
    public int RestockRequestId { get; set; }
    public string ReferenceCode { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class PreparedItemOpenProductionRunDto
{
    public int ProductionRunId { get; set; }
    public int RecipeId { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public decimal CoverageBase { get; set; }
    public string BaseUnitCode { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

public static class PreparedItemReplenishmentDataStatuses
{
    public const string Ready = "READY";
    public const string TargetNotConfigured = "TARGET_NOT_CONFIGURED";
    public const string OpenCoverageUnitIncompatible = "OPEN_COVERAGE_UNIT_INCOMPATIBLE";
}
