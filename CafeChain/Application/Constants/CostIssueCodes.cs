namespace CafeChain.Application.Constants
{
    /// <summary>Issue codes for EstimatedBomCost completeness (Issue #117 / ADR-0005).</summary>
    public static class CostIssueCodes
    {
        public const string MissingPackageQuantity = "MISSING_PACKAGE_QUANTITY";
        public const string InvalidPackageQuantity = "INVALID_PACKAGE_QUANTITY";
        public const string MissingPackagePrice = "MISSING_PACKAGE_PRICE";
        public const string ZeroPackagePrice = "ZERO_PACKAGE_PRICE";
        public const string MissingPackageUnit = "MISSING_PACKAGE_UNIT";
        public const string InactivePackageUnit = "INACTIVE_PACKAGE_UNIT";
        public const string MissingUnitConversion = "MISSING_UNIT_CONVERSION";
        public const string ConflictingUnitConversion = "CONFLICTING_UNIT_CONVERSION";
        public const string MissingSupplierOffer = "MISSING_SUPPLIER_OFFER";
        public const string MultiplePrimarySuppliers = "MULTIPLE_PRIMARY_SUPPLIERS";
        public const string InactiveSupplierOffer = "INACTIVE_SUPPLIER_OFFER";
        public const string MissingRecipe = "MISSING_RECIPE";
        public const string MissingRecipeDetails = "MISSING_RECIPE_DETAILS";
        public const string MissingRecipeOutput = "MISSING_RECIPE_OUTPUT";
        public const string InvalidRecipeOutput = "INVALID_RECIPE_OUTPUT";
        public const string MissingChildRecipe = "MISSING_CHILD_RECIPE";
        public const string LegacyChildRecipeWithoutOutput = "LEGACY_CHILD_RECIPE_WITHOUT_OUTPUT";
        public const string RecipeCycle = "RECIPE_CYCLE";
        public const string MaxDepthExceeded = "MAX_DEPTH_EXCEEDED";
        public const string RejectedPackagingUnit = "REJECTED_PACKAGING_UNIT";
    }
}
