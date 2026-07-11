using System.Collections.Generic;

namespace CafeChain.Application.DTOs.Costing
{
    public enum CostCompletenessStatus
    {
        Complete = 0,
        Incomplete = 1
    }

    public enum CostComponentKind
    {
        Ingredient = 0,
        ChildRecipe = 1
    }

    /// <summary>
    /// EstimatedBomCost result (ADR-0005). Never use TotalCost=0 as incomplete sentinel.
    /// Complete ⇒ TotalCost has value; Incomplete ⇒ TotalCost null (or partial line costs only).
    /// </summary>
    public sealed class CostCalculationResult
    {
        public CostCompletenessStatus Status { get; init; }
        public decimal? TotalCost { get; init; }
        public IReadOnlyList<CostLineResult> Lines { get; init; } = new List<CostLineResult>();
        public IReadOnlyList<CostIssue> Issues { get; init; } = new List<CostIssue>();

        public bool IsComplete => Status == CostCompletenessStatus.Complete;

        public static CostCalculationResult Complete(
            decimal totalCost,
            IReadOnlyList<CostLineResult> lines,
            IReadOnlyList<CostIssue>? issues = null)
            => new()
            {
                Status = CostCompletenessStatus.Complete,
                TotalCost = totalCost,
                Lines = lines,
                Issues = issues ?? new List<CostIssue>()
            };

        public static CostCalculationResult Incomplete(
            IReadOnlyList<CostLineResult> lines,
            IReadOnlyList<CostIssue> issues)
            => new()
            {
                Status = CostCompletenessStatus.Incomplete,
                TotalCost = null,
                Lines = lines,
                Issues = issues
            };
    }

    public sealed class CostLineResult
    {
        public int? RecipeDetailId { get; init; }
        public CostComponentKind ComponentKind { get; init; }
        public int? IngredientId { get; init; }
        public int? ChildRecipeId { get; init; }
        public int? PreparedItemId { get; init; }
        public decimal Quantity { get; init; }
        public int UnitId { get; init; }
        public string? UnitCode { get; init; }
        public decimal? QuantityInBase { get; init; }
        public string? BaseUnitCode { get; init; }
        public decimal? BaseUnitCost { get; init; }
        public decimal? LineCost { get; init; }
        public CostCompletenessStatus Status { get; init; }
        public decimal? PackagePrice { get; init; }
        public decimal? PackageQuantity { get; init; }
        public string? PackageUnitCode { get; init; }
        public int? IngredientSupplierId { get; init; }
        public string? DisplaySummary { get; init; }
    }

    public sealed class CostIssue
    {
        public string Code { get; init; } = "";
        public string Message { get; init; } = "";
        public int? IngredientId { get; init; }
        public int? PreparedItemId { get; init; }
        public int? RecipeId { get; init; }
        public int? RecipeDetailId { get; init; }
        public int? IngredientSupplierId { get; init; }
    }

    /// <summary>Resolved package-normalized cost for one Ingredient (EstimatedBomCost source).</summary>
    public sealed class IngredientBaseUnitCostResult
    {
        public CostCompletenessStatus Status { get; init; }
        public decimal? BaseUnitCost { get; init; }
        public decimal? BaseQuantityPerPackage { get; init; }
        public int? BaseUnitId { get; init; }
        public string? BaseUnitCode { get; init; }
        public string? BaseUnitName { get; init; }
        public decimal? PackagePrice { get; init; }
        public decimal? PackageQuantity { get; init; }
        public int? PackageUnitId { get; init; }
        public string? PackageUnitCode { get; init; }
        public int? IngredientSupplierId { get; init; }
        public int IngredientId { get; init; }
        public IReadOnlyList<CostIssue> Issues { get; init; } = new List<CostIssue>();

        public bool IsComplete => Status == CostCompletenessStatus.Complete && BaseUnitCost.HasValue;
    }
}
