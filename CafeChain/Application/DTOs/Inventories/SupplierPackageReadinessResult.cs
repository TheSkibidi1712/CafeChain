namespace CafeChain.Application.DTOs.Inventories;

public sealed class SupplierPackageReadinessResult
{
    public bool HasValidPackageDefinition { get; init; }
    public bool IsReady { get; init; }
    public bool IsProcurementEligible { get; init; }
    public decimal? PackageBaseQuantity { get; init; }
    public string ReasonCode { get; init; } = SupplierPackageReadinessCodes.Ready;
    public string Message { get; init; } = "Gói mua đã sẵn sàng để sử dụng.";

    public static SupplierPackageReadinessResult Ready(decimal packageBaseQuantity) => new()
    {
        HasValidPackageDefinition = true,
        IsReady = true,
        IsProcurementEligible = false,
        PackageBaseQuantity = packageBaseQuantity
    };

    public static SupplierPackageReadinessResult NotReady(
        string reasonCode,
        string message,
        bool hasValidPackageDefinition = false,
        decimal? packageBaseQuantity = null) => new()
    {
        HasValidPackageDefinition = hasValidPackageDefinition,
        IsReady = false,
        IsProcurementEligible = false,
        PackageBaseQuantity = packageBaseQuantity,
        ReasonCode = reasonCode,
        Message = message
    };

    public SupplierPackageReadinessResult NotEligible(string reasonCode, string message) => new()
    {
        HasValidPackageDefinition = HasValidPackageDefinition,
        IsReady = IsReady,
        IsProcurementEligible = false,
        PackageBaseQuantity = PackageBaseQuantity,
        ReasonCode = reasonCode,
        Message = message
    };

    public SupplierPackageReadinessResult Eligible() => new()
    {
        HasValidPackageDefinition = HasValidPackageDefinition,
        IsReady = IsReady,
        IsProcurementEligible = true,
        PackageBaseQuantity = PackageBaseQuantity,
        ReasonCode = SupplierPackageReadinessCodes.Ready,
        Message = Message
    };
}

public static class SupplierPackageReadinessCodes
{
    public const string Ready = "SUPPLIER_PACKAGE_READY";
    public const string Inactive = "SUPPLIER_PACKAGE_INACTIVE";
    public const string ParentInactive = "SUPPLIER_PACKAGE_PARENT_INACTIVE";
    public const string ContentMissing = "SUPPLIER_PACKAGE_CONTENT_MISSING";
    public const string ContentUomInvalid = "SUPPLIER_PACKAGE_CONTENT_UOM_INVALID";
    public const string PriceInvalid = "SUPPLIER_PACKAGE_PRICE_INVALID";
    public const string OperationalTermsInvalid = "SUPPLIER_PACKAGE_OPERATIONAL_TERMS_INVALID";
    public const string LooseContractInvalid = "SUPPLIER_PACKAGE_LOOSE_CONTRACT_INVALID";
    public const string StoreScopeInvalid = "SUPPLIER_PACKAGE_STORE_SCOPE_INVALID";
    public const string PurchaseModeInvalid = "SUPPLIER_PACKAGE_PURCHASE_MODE_INVALID";
    public const string NotProcurementReady = "SUPPLIER_PACKAGE_NOT_PROCUREMENT_READY";
}
