using CafeChain.Models.Enums.Inventory;

namespace CafeChain.Application.DTOs.Admin.Production;

public sealed class ProductionRunListQuery
{
    public int StoreId { get; set; }
    public string? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class ProductionRunListDto
{
    public IReadOnlyList<ProductionRunListItemDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)Math.Max(1, PageSize)));
}

public sealed class ProductionRunListItemDto
{
    public int ProductionRunId { get; set; }
    public int StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public string RecipeName { get; set; } = string.Empty;
    public string OutputName { get; set; } = string.Empty;
    public int ContractVersion { get; set; }
    public decimal RequestedRunCount { get; set; }
    public int? PlannedBatchCount { get; set; }
    public decimal? ExpectedOutputBase { get; set; }
    public string OutputUnitCode { get; set; } = string.Empty;
    public ProductionRunStatus Status { get; set; }
    public string StatusLabel => ProductionRunDisplay.Status(Status);
    public DateTime CreatedAt { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public int? RestockRequestId { get; set; }
}

public sealed class ProductionRunDetailDto
{
    public int ProductionRunId { get; set; }
    public int ContractVersion { get; set; }
    public int StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public int RecipeId { get; set; }
    public string RecipeName { get; set; } = string.Empty;
    public string OutputName { get; set; } = string.Empty;
    public int? PlannedBatchCount { get; set; }
    public decimal RequestedRunCount { get; set; }
    public decimal? ExpectedOutputPerBatchBase { get; set; }
    public decimal? ExpectedOutputBase { get; set; }
    public string OutputUnitCode { get; set; } = string.Empty;
    public decimal YieldVarianceTolerancePercent { get; set; }
    public ProductionRunStatus Status { get; set; }
    public string StatusLabel => ProductionRunDisplay.Status(Status);
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime? CompletedAt { get; set; }
    public decimal? TotalInputCost { get; set; }
    public decimal? OutputUnitCost { get; set; }
    public int? RestockRequestId { get; set; }
    public string? RestockReferenceCode { get; set; }
    public decimal? RestockRequestedQuantity { get; set; }
    public decimal RestockFulfilledQuantity { get; set; }
    public decimal RestockRemainingQuantity => Math.Max(0, RestockRequestedQuantity.GetValueOrDefault() - RestockFulfilledQuantity);
    public ProductionRunOutputDetailDto? Output { get; set; }
    public IReadOnlyList<ProductionRunInputDetailDto> Inputs { get; set; } = [];
    public IReadOnlyList<ProductionRunTransitionDetailDto> Transitions { get; set; } = [];
    public bool CanRelease { get; set; }
    public bool CanStart { get; set; }
    public bool CanRecordActual { get; set; }
    public bool CanApproveVariance { get; set; }
    public bool CanAcceptOutput { get; set; }
    public bool CanCancel { get; set; }
}

public sealed class ProductionRunInputDetailDto
{
    public int? IngredientId { get; set; }
    public int? PreparedItemId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int BaseUnitId { get; set; }
    public string BaseUnitCode { get; set; } = string.Empty;
    public decimal PlannedBaseQuantity { get; set; }
    public decimal? ActualBaseQuantity { get; set; }
}

public sealed class ProductionRunOutputDetailDto
{
    public decimal ExpectedOutputBase { get; set; }
    public decimal ActualProducedBase { get; set; }
    public decimal AcceptedOutputBase { get; set; }
    public decimal RejectedOutputBase { get; set; }
    public decimal VariancePercent { get; set; }
    public string? Reason { get; set; }
    public string RecordedByName { get; set; } = string.Empty;
    public DateTime RecordedAtUtc { get; set; }
}

public sealed class ProductionRunTransitionDetailDto
{
    public string FromStatusLabel { get; set; } = string.Empty;
    public string ToStatusLabel { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public string? Reason { get; set; }
}

public static class ProductionRunDisplay
{
    public static string Status(ProductionRunStatus status) => status switch
    {
        ProductionRunStatus.Confirmed => "Đã xác nhận (cũ)",
        ProductionRunStatus.Completed => "Hoàn tất",
        ProductionRunStatus.Planned => "Đã lập kế hoạch",
        ProductionRunStatus.Released => "Đã phát hành",
        ProductionRunStatus.InProgress => "Đang sản xuất",
        ProductionRunStatus.AwaitingAcceptance => "Chờ nhận đầu ra",
        ProductionRunStatus.AwaitingVarianceApproval => "Chờ duyệt chênh lệch",
        ProductionRunStatus.Cancelled => "Đã hủy",
        _ => "Không xác định"
    };

    public static string Status(string? status)
        => Enum.TryParse<ProductionRunStatus>(status, true, out var parsed)
            ? Status(parsed)
            : "Không xác định";
}
