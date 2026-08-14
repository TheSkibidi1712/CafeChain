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
        public bool CanViewProduction { get; set; }
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
        public RecipeWorkspaceCostEvidenceVM DesignCost { get; set; } =
            RecipeWorkspaceCostEvidenceVM.DesignUnavailable();
        public RecipeWorkspaceCostEvidenceVM StoreFifoCost { get; set; } =
            RecipeWorkspaceCostEvidenceVM.StoreNotSelected();
        public RecipeWorkspaceReadinessSummaryVM GlobalReadiness { get; set; } = new();
        public RecipeWorkspaceReadinessSummaryVM StoreReadiness { get; set; } =
            RecipeWorkspaceReadinessSummaryVM.StoreNotSelected();
        public RecipeWhereUsedVM WhereUsed { get; set; } = new();
        public RecipeVersionHistoryVM VersionHistory { get; set; } = new();

        public bool IsPreparedItemRecipe => RecipeTypeKey == "SUBRECIPE";
        public bool ShowBatchOutput => IsPreparedItemRecipe;
        public IReadOnlyList<BomComponentDetailVM> PreparedInputs =>
            Components.Where(x => x.IsPreparedInput).ToList();
        public IReadOnlyList<BomComponentDetailVM> DirectIngredients =>
            Components.Where(x => !x.IsPreparedInput).ToList();

        public void ApplyStoreEvidence(RecipeWorkspaceStoreEvidenceVM evidence)
        {
            ArgumentNullException.ThrowIfNull(evidence);
            StoreFifoCost = evidence.Cost;
            StoreReadiness = evidence.Readiness;
        }

        public void ApplyProductionReadiness(ProductionReadinessPreviewDto? readiness)
        {
            var facet = StoreReadiness.Facets.FirstOrDefault(x =>
                x.Code == RecipeWorkspaceReadinessCodes.StoreOperations);
            if (facet == null)
                return;

            if (readiness == null)
            {
                facet.State = RecipeWorkspaceEvidenceState.Unavailable;
                facet.Message = "Chưa thể kiểm tra điều kiện sản xuất tại chi nhánh.";
                return;
            }

            facet.State = readiness.IsReady
                ? RecipeWorkspaceEvidenceState.Available
                : RecipeWorkspaceEvidenceState.Incomplete;
            facet.Message = readiness.IsReady
                ? "Đủ điều kiện sản xuất một mẻ tại chi nhánh đã chọn."
                : "Còn điều kiện vận hành cần xử lý trước khi sản xuất tại chi nhánh.";
        }
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
        public decimal? CostContributionPercent { get; set; }
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

    public static class RecipeWorkspaceCostAuthorityCodes
    {
        public const string DesignEstimate = "DESIGN_ESTIMATE";
        public const string StoreFifo = "STORE_FIFO";
    }

    public static class RecipeWorkspaceEvidenceState
    {
        public const string Available = "AVAILABLE";
        public const string Incomplete = "INCOMPLETE";
        public const string Unavailable = "UNAVAILABLE";
        public const string NotApplicable = "NOT_APPLICABLE";
    }

    public static class RecipeWorkspaceReadinessCodes
    {
        public const string Configuration = "CONFIGURATION";
        public const string Pricing = "PRICING";
        public const string PointOfSale = "POINT_OF_SALE";
        public const string PreparedInputs = "PREPARED_INPUTS";
        public const string StoreFifo = "STORE_FIFO_EVIDENCE";
        public const string StoreOperations = "STORE_OPERATIONS";
    }

    public sealed class RecipeWorkspaceCostEvidenceVM
    {
        public string AuthorityCode { get; set; } = "";
        public string Label { get; set; } = "";
        public string State { get; set; } = RecipeWorkspaceEvidenceState.Unavailable;
        public decimal? Amount { get; set; }
        public string? UnitLabel { get; set; }
        public string? ContextLabel { get; set; }
        public DateTime? EvidenceAtUtc { get; set; }
        public string Message { get; set; } = "";
        public bool IsAvailable => State == RecipeWorkspaceEvidenceState.Available && Amount.HasValue;

        public static RecipeWorkspaceCostEvidenceVM DesignUnavailable() => new()
        {
            AuthorityCode = RecipeWorkspaceCostAuthorityCodes.DesignEstimate,
            Label = "Giá vốn ước tính theo thiết kế",
            State = RecipeWorkspaceEvidenceState.Unavailable,
            Message = "Chưa có dữ liệu giá vốn thiết kế."
        };

        public static RecipeWorkspaceCostEvidenceVM StoreNotSelected() => new()
        {
            AuthorityCode = RecipeWorkspaceCostAuthorityCodes.StoreFifo,
            Label = "Giá vốn theo nhập trước - xuất trước (FIFO) tại chi nhánh",
            State = RecipeWorkspaceEvidenceState.Unavailable,
            Message = "Chọn chi nhánh để xem bằng chứng giá vốn FIFO thực tế."
        };
    }

    public sealed class RecipeWorkspaceReadinessFacetVM
    {
        public string Code { get; set; } = "";
        public string Label { get; set; } = "";
        public string State { get; set; } = RecipeWorkspaceEvidenceState.Unavailable;
        public string Message { get; set; } = "";
        public bool IsPassed => State == RecipeWorkspaceEvidenceState.Available;
        public bool IsApplicable => State != RecipeWorkspaceEvidenceState.NotApplicable;
        public string StateLabel => State switch
        {
            RecipeWorkspaceEvidenceState.Available => "Đạt",
            RecipeWorkspaceEvidenceState.Incomplete => "Chưa đạt",
            RecipeWorkspaceEvidenceState.NotApplicable => "Không áp dụng",
            _ => "Chưa có dữ liệu"
        };
    }

    public sealed class RecipeWorkspaceReadinessSummaryVM
    {
        public string ScopeLabel { get; set; } = "Toàn hệ thống";
        public List<RecipeWorkspaceReadinessFacetVM> Facets { get; set; } = new();
        public int ApplicableCount => Facets.Count(x => x.IsApplicable);
        public int PassedCount => Facets.Count(x => x.IsApplicable && x.IsPassed);
        public bool IsReady => ApplicableCount > 0 && PassedCount == ApplicableCount;
        public string SummaryLabel => ApplicableCount == 0
            ? "Chưa có tiêu chí áp dụng"
            : $"{PassedCount}/{ApplicableCount} tiêu chí đạt";

        public static RecipeWorkspaceReadinessSummaryVM StoreNotSelected() => new()
        {
            ScopeLabel = "Theo chi nhánh",
            Facets =
            [
                new RecipeWorkspaceReadinessFacetVM
                {
                    Code = RecipeWorkspaceReadinessCodes.StoreFifo,
                    Label = "Bằng chứng giá FIFO",
                    State = RecipeWorkspaceEvidenceState.Unavailable,
                    Message = "Chọn chi nhánh để đánh giá dữ liệu giá thực tế."
                }
            ]
        };
    }

    public sealed class RecipeWorkspaceStoreEvidenceVM
    {
        public int StoreId { get; set; }
        public string StoreName { get; set; } = "";
        public RecipeWorkspaceCostEvidenceVM Cost { get; set; } =
            RecipeWorkspaceCostEvidenceVM.StoreNotSelected();
        public RecipeWorkspaceReadinessSummaryVM Readiness { get; set; } = new();
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
        public string BusinessCode => $"PR-{ProductionRunId}";
        public decimal RequestedRunCount { get; set; }
        public string Status { get; set; } = "";
        public DateTime ConfirmedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? ActorName { get; set; }
        public decimal? NormalizedOutputQuantity { get; set; }
        public decimal? AcceptedOutputQuantity { get; set; }
        public string? OutputUnitCode { get; set; }
        public decimal? ActualTotalInputCost { get; set; }
        public decimal? ActualOutputUnitCost { get; set; }
    }
}
