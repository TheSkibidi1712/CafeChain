namespace CafeChain.Application.Constants
{
    public static class BomRecipeErrorCodes
    {
        public const string InvalidPayload = "BOM_INVALID_PAYLOAD";
        public const string RecipeOverlap = "BOM_RECIPE_OVERLAP";
        public const string ComponentDuplicate = "BOM_COMPONENT_DUPLICATE";
        public const string ComponentInactive = "BOM_COMPONENT_INACTIVE";
        public const string ComponentUomIncompatible = "BOM_COMPONENT_UOM_INCOMPATIBLE";
        public const string ComponentConversionMissing = "BOM_COMPONENT_CONVERSION_MISSING";
        public const string CircularDependency = "BOM_CIRCULAR_DEPENDENCY";
        public const string CurrentRecipeInvalidTarget = "BOM_CURRENT_RECIPE_INVALID_TARGET";
        public const string CurrentRecipeMissing = "BOM_CURRENT_RECIPE_MISSING";
        public const string CurrentRecipeAmbiguous = "BOM_CURRENT_RECIPE_AMBIGUOUS";
        public const string FutureEffectiveDateNotSupported = "BOM_FUTURE_EFFECTIVE_DATE_NOT_SUPPORTED";
        public const string PublishConflict = "BOM_PUBLISH_CONFLICT";
        public const string TechnicalError = "BOM_TECHNICAL_ERROR";
    }
}
