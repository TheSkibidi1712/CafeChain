namespace CafeChain.Application.DTOs.Admin.Production;

public sealed class ProductionSourceEligibilityRequest
{
    public int StoreId { get; set; }
    public int ActorAccountId { get; set; }
    public int? IngredientId { get; set; }
    public int? PreparedItemId { get; set; }
    public string RequiredPermissionCode { get; set; } = string.Empty;
    public DateTime? AtUtc { get; set; }
}

public sealed class ProductionSourceEligibilityDto
{
    public bool Eligible { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int StoreId { get; set; }
    public int? IngredientId { get; set; }
    public int? PreparedItemId { get; set; }
    public int? RecipeId { get; set; }
    public decimal? ExpectedOutputPerBatchBase { get; set; }
    public int? OutputBaseUnitId { get; set; }
    public string OutputBaseUnitCode { get; set; } = string.Empty;
}

public static class ProductionEligibilityReasonCodes
{
    public const string Eligible = "PRODUCTION_ELIGIBLE";
    public const string InvalidRequest = "PRODUCTION_REQUEST_INVALID";
    public const string StoreUnavailable = "PRODUCTION_STORE_UNAVAILABLE";
    public const string ItemUnavailable = "PRODUCTION_ITEM_UNAVAILABLE";
    public const string ItemCapabilityMissing = "PRODUCTION_ITEM_CAPABILITY_MISSING";
    public const string StoreCapabilityMissing = "PRODUCTION_STORE_CAPABILITY_MISSING";
    public const string PermissionDenied = "PRODUCTION_PERMISSION_DENIED";
    public const string RecipeMissing = "PRODUCTION_RECIPE_MISSING";
    public const string OutputContractInvalid = "PRODUCTION_OUTPUT_CONTRACT_INVALID";
}
