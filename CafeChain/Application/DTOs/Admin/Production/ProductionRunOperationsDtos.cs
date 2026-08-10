namespace CafeChain.Application.DTOs.Admin.Production;

public sealed class ProductionActualInputRequest
{
    public int? IngredientId { get; set; }
    public int? PreparedItemId { get; set; }
    public decimal ActualBaseQuantity { get; set; }
}

public sealed class RecordProductionActualRequest
{
    public int ProductionRunId { get; set; }
    public decimal ActualProducedBase { get; set; }
    public decimal AcceptedOutputBase { get; set; }
    public decimal RejectedOutputBase { get; set; }
    public string? Reason { get; set; }
    public List<ProductionActualInputRequest> Inputs { get; set; } = new();
}

public sealed class ProductionRunOperationResultDto
{
    public int ProductionRunId { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal? ExpectedOutputBase { get; set; }
    public decimal? AcceptedOutputBase { get; set; }
    public decimal? VariancePercent { get; set; }
    public bool RequiresVarianceApproval { get; set; }
    public bool WasReplay { get; set; }
}

public static class ProductionRunOperationErrorCodes
{
    public const string NotFound = "PRODUCTION_RUN_NOT_FOUND";
    public const string InvalidState = "PRODUCTION_RUN_INVALID_STATE";
    public const string Unauthorized = "PRODUCTION_RUN_UNAUTHORIZED";
    public const string InvalidActual = "PRODUCTION_ACTUAL_INVALID";
    public const string ActualInputsIncomplete = "PRODUCTION_ACTUAL_INPUTS_INCOMPLETE";
    public const string NotReady = "PRODUCTION_RUN_NOT_READY";
    public const string MakerChecker = "PRODUCTION_VARIANCE_MAKER_CHECKER_REQUIRED";
    public const string Concurrency = "PRODUCTION_RUN_CONCURRENCY_CONFLICT";
}
