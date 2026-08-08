namespace CafeChain.Application.DTOs.Admin.RestockRequests;

public sealed class PurchaseSourceEligibilityRequest
{
    public int StoreId { get; set; }
    public int? IngredientId { get; set; }
    public int? PreparedItemId { get; set; }
    public DateTime? AtUtc { get; set; }
}

public sealed class PurchaseSourceEligibilityDto
{
    public bool Eligible { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public static class PurchaseEligibilityReasonCodes
{
    public const string Eligible = "PURCHASE_ELIGIBLE";
    public const string InvalidRequest = "PURCHASE_REQUEST_INVALID";
    public const string ItemUnavailable = "PURCHASE_ITEM_UNAVAILABLE";
    public const string CapabilityMissing = "PURCHASE_ITEM_CAPABILITY_MISSING";
    public const string PackageMissing = "PURCHASE_SUPPLIER_PACKAGE_MISSING";
}
