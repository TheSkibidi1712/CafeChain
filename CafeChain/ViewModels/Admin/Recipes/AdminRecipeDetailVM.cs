using CafeChain.Application.DTOs.Admin.Production;

namespace CafeChain.ViewModels.Admin.Recipes
{
    public sealed class AdminRecipeVisualizePageVM
    {
        public int RecipeId { get; set; }
        public string RecipeCode { get; set; } = "";
        public string Name { get; set; } = "";
        public string BusinessName { get; set; } = "";
        public string? BusinessCode { get; set; }
        public string? SizeName { get; set; }
        public string TargetLabel { get; set; } = "";
        public string VersionLabel { get; set; } = "";
        public bool IsCurrentVersion { get; set; }
        public string AppliedStateLabel { get; set; } = "";
        public string AppliedStateCssClass { get; set; } = "rb-status-inactive";
        public string OutputHeading { get; set; } = "Đầu ra";
        public string OutputDisplay { get; set; } = "";
        public string OutputContext { get; set; } = "";
        public string Status { get; set; } = "";
        public bool Active { get; set; }
        public DateTime? EffectiveDate { get; set; }
        public int? ParentVersionId { get; set; }
        public string RecipeTypeKey { get; set; } = "";
        public string TypeLabel { get; set; } = "";
        public string IdentityDisplay { get; set; } = "";
        public int? PreparedItemId { get; set; }
        public string? PreparedItemCode { get; set; }
        public string? PreparedItemName { get; set; }
        public decimal? OutputQuantity { get; set; }
        public string? OutputUnitCode { get; set; }
        public string? OutputUnitName { get; set; }
        public decimal? NormalizedOutputQuantity { get; set; }
        public string? OutputBaseUnitCode { get; set; }
        public bool CanWrite { get; set; }
        public string BackUrl { get; set; } = "/Admin/AdminRecipe";
        public BomHealthStatusVM ConfigurationHealth { get; set; } = new();
        public BomHealthStatusVM CostingHealth { get; set; } = new();
        public decimal? EstimatedBatchCost { get; set; }
        public decimal? EstimatedPortionCost { get; set; }
        public decimal? EstimatedUnitCost { get; set; }
        public ToppingConsumptionSourceVM? ToppingConsumptionSource { get; set; }
        public List<BomComponentDetailVM> Components { get; set; } = new();
        public List<RecipeBomTreeNodeVM> FirstLevelNodes { get; set; } = new();
        public List<BomStoreOptionVM> Stores { get; set; } = new();
        public int? SelectedStoreId { get; set; }
        public string? SelectedStoreName { get; set; }
        public BomOperationalDetailVM? Operational { get; set; }
        public string? OperationalError { get; set; }

        public bool IsPreparedItemRecipe => RecipeTypeKey == "SUBRECIPE";
        public bool ShowBatchOutput => IsPreparedItemRecipe;
        public IReadOnlyList<BomComponentDetailVM> PreparedInputs =>
            Components.Where(x => x.IsPreparedInput).ToList();
        public IReadOnlyList<BomComponentDetailVM> DirectIngredients =>
            Components.Where(x => !x.IsPreparedInput).ToList();
    }

    public sealed class BomComponentDetailVM
    {
        public int RecipeDetailId { get; set; }
        public string ComponentType { get; set; } = "";
        public int? IngredientId { get; set; }
        public int? ChildRecipeId { get; set; }
        public string? ChildRecipeCode { get; set; }
        public int? PreparedItemId { get; set; }
        public string? PreparedItemCode { get; set; }
        public string ItemCode { get; set; } = "";
        public string ItemName { get; set; } = "";
        public decimal Quantity { get; set; }
        public string UnitCode { get; set; } = "";
        public decimal? NormalizedQuantity { get; set; }
        public string? BaseUnitCode { get; set; }
        public decimal? EstimatedLineCost { get; set; }
        public string CostStatus { get; set; } = "Chưa đủ dữ liệu";
        public List<BomHealthReasonVM> CostReasons { get; set; } = new();
        public bool IsPreparedInput => ChildRecipeId.HasValue;
        public string InputTypeLabel => IsPreparedInput
            ? "Bán thành phẩm đầu vào"
            : "Nguyên liệu trực tiếp";
        public string? SourceVersionLabel => ChildRecipeId.HasValue
            ? $"Phiên bản {ChildRecipeId.Value}"
            : null;
    }

    public sealed class BomStoreOptionVM
    {
        public int StoreId { get; set; }
        public string StoreName { get; set; } = "";
    }

    public sealed class BomOperationalDetailVM
    {
        public int StoreId { get; set; }
        public string StoreName { get; set; } = "";
        public ProductionReadinessPreviewDto? Readiness { get; set; }
        public BomPreparedItemStockVM? OutputStock { get; set; }
        public List<BomProductionRunVM> RecentRuns { get; set; } = new();
    }

    public sealed class BomPreparedItemStockVM
    {
        public int StoreInventoryId { get; set; }
        public int PreparedItemId { get; set; }
        public string PreparedItemCode { get; set; } = "";
        public string PreparedItemName { get; set; } = "";
        public string BaseUnitCode { get; set; } = "";
        public decimal CurrentQuantity { get; set; }
        public decimal ReservedQuantity { get; set; }
        public decimal UsableQuantity { get; set; }
        public int? LatestCostLayerId { get; set; }
        public int? SourceProductionRunId { get; set; }
        public decimal? ActualUnitCost { get; set; }
        public DateTime? ActualLayerCreatedAt { get; set; }
    }

    public sealed class BomProductionRunVM
    {
        public int ProductionRunId { get; set; }
        public decimal RequestedRunCount { get; set; }
        public string Status { get; set; } = "";
        public DateTime ConfirmedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? ActorName { get; set; }
        public decimal? NormalizedOutputQuantity { get; set; }
        public decimal? ActualTotalInputCost { get; set; }
        public decimal? ActualOutputUnitCost { get; set; }
    }
}
