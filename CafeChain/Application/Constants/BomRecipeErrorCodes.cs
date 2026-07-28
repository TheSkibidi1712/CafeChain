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
        public const string TechnicalError = "BOM_TECHNICAL_ERROR";
    }
}
