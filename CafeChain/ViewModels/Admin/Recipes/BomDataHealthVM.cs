using System.Collections.Generic;

namespace CafeChain.ViewModels.Admin.Recipes
{
    public static class BomConfigurationHealthCodes
    {
        public const string Complete = "COMPLETE";
        public const string Inactive = "INACTIVE";
        public const string MissingComponents = "MISSING_COMPONENTS";
        public const string MissingOutputIdentity = "MISSING_OUTPUT_IDENTITY";
        public const string MissingOutputQuantity = "MISSING_OUTPUT_QUANTITY";
        public const string MissingOutputUnit = "MISSING_OUTPUT_UNIT";
        public const string InvalidPreparedItemMapping = "INVALID_PREPARED_ITEM_MAPPING";
        public const string MissingComponentUnit = "MISSING_COMPONENT_UNIT";
    }

    public static class BomCostingHealthCodes
    {
        public const string Complete = "COMPLETE";
        public const string MissingQuote = "MISSING_QUOTE";
        public const string MissingConversion = "MISSING_CONVERSION";
        public const string MissingChildCost = "MISSING_CHILD_COST";
        public const string Indeterminate = "INDETERMINATE";
    }

    public static class BomCurrentRecipeHealthCodes
    {
        public const string Current = "CURRENT";
        public const string Historical = "HISTORICAL";
        public const string Missing = "MISSING";
        public const string Ambiguous = "AMBIGUOUS";
        public const string InvalidTarget = "INVALID_TARGET";
    }

    public sealed class BomCurrentRecipeHealthVM
    {
        public string Code { get; set; } = "";
        public string ReasonCode { get; set; } = "";
        public string Label { get; set; } = "";
        public string Message { get; set; } = "";
        public bool IsBlocking { get; set; }
    }

    public sealed class BomHealthReasonVM
    {
        public string Code { get; set; } = "";
        public string GroupCode { get; set; } = "";
        public string Message { get; set; } = "";
        public string CtaLabel { get; set; } = "";
        public string CtaController { get; set; } = "AdminRecipe";
        public string CtaAction { get; set; } = "Index";
        public int? CtaId { get; set; }
    }

    public sealed class BomHealthStatusVM
    {
        public string Code { get; set; } = "";
        public string Label { get; set; } = "";
        public bool IsComplete { get; set; }
        public List<BomHealthReasonVM> Reasons { get; set; } = new();
    }

    public sealed class BomDataHealthRowVM
    {
        public int RecipeId { get; set; }
        public string RecipeCode { get; set; } = "";
        public string Name { get; set; } = "";
        public string TypeCode { get; set; } = "";
        public string TypeLabel { get; set; } = "";
        public string IdentityDisplay { get; set; } = "";
        public BomCurrentRecipeHealthVM CurrentRecipe { get; set; } = new();
        public BomHealthStatusVM Configuration { get; set; } = new();
        public BomHealthStatusVM Costing { get; set; } = new();
        public decimal? EstimatedCost { get; set; }
    }

    public sealed class BomDataHealthPageVM
    {
        public List<BomDataHealthRowVM> Items { get; set; } = new();
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string Search { get; set; } = "";
        public string TypeFilter { get; set; } = "ALL";
        public int TotalCount { get; set; }
        public int CurrentPageCount => Items.Count;
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)Math.Max(1, PageSize)));
        public int CompleteCount { get; set; }
        public int MissingQuoteCount { get; set; }
        public int MissingConversionCount { get; set; }
        public int MissingOutputCount { get; set; }
        public int MappingErrorCount { get; set; }
        public int CurrentRecipeIssueCount { get; set; }
    }
}
