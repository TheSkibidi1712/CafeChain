namespace CafeChain.Application.DTOs.Admin.Production
{
    public static class ProductionReadinessCodes
    {
        public const string Ready = "READY";
        public const string InvalidRecipe = "INVALID_RECIPE";
        public const string InvalidOutput = "INVALID_OUTPUT_CONTRACT";
        public const string WriterMode = "WRITER_MODE_NOT_READY";
        public const string WriterCapability = "WRITER_CAPABILITY_NOT_READY";
        public const string MissingInventory = "MISSING_INPUT_INVENTORY";
        public const string IngredientShortage = "INGREDIENT_SHORTAGE";
        public const string PreparedItemShortage = "PREPARED_ITEM_SHORTAGE";
        public const string MissingCostEvidence = "MISSING_COST_EVIDENCE";
        public const string EstimatedCostIncomplete = "ESTIMATED_BOM_COST_INCOMPLETE";
        public const string ConversionFailed = "UNIT_CONVERSION_FAILED";
    }

    public static class ProductionReadinessDisplay
    {
        public static string MessageFor(string? code, string? fallback = null) => code switch
        {
            ProductionReadinessCodes.WriterMode =>
                "Công thức hiện tại chưa sẵn sàng cho quy trình sản xuất. Quy trình mới yêu cầu đầu ra được xác định bằng Bán thành phẩm; công thức đang chọn vẫn dùng cách ghi nhận tồn kho cũ.",
            ProductionReadinessCodes.WriterCapability =>
                "Chức năng ghi nhận tồn kho bán thành phẩm hiện chưa sẵn sàng. Vui lòng liên hệ quản trị hệ thống trước khi thực hiện.",
            ProductionReadinessCodes.InvalidRecipe =>
                "Công thức chưa đáp ứng đầy đủ điều kiện sản xuất. Vui lòng kiểm tra định mức và đầu ra của công thức.",
            ProductionReadinessCodes.InvalidOutput =>
                "Sản lượng đầu ra của công thức chưa được cấu hình hợp lệ.",
            _ when !string.IsNullOrWhiteSpace(fallback) => fallback.Trim(),
            _ => "Chưa thể xác định mức độ sẵn sàng của công thức."
        };

        public static string WriterModeLabel(string? writerMode) => writerMode switch
        {
            "PreparedItem" => "Theo bán thành phẩm",
            "LegacyRecipe" => "Theo cách ghi nhận tồn kho cũ",
            _ => "Chưa xác định"
        };
    }

    public sealed class ProductionRecipeOptionDto
    {
        public int RecipeId { get; set; }
        public string RecipeCode { get; set; } = "";
        public string RecipeName { get; set; } = "";
        public int? PreparedItemId { get; set; }
        public string? PreparedItemCode { get; set; }
        public string? PreparedItemName { get; set; }
        public string OutputPerRunDisplay { get; set; } = "—";
        public bool Selectable { get; set; }
        public string? DisabledReason { get; set; }
    }

    public sealed class ProductionReadinessPreviewDto
    {
        public int StoreId { get; set; }
        public string StoreName { get; set; } = "";
        public int RecipeId { get; set; }
        public string RecipeCode { get; set; } = "";
        public string RecipeName { get; set; } = "";
        public int PreparedItemId { get; set; }
        public string PreparedItemCode { get; set; } = "";
        public string PreparedItemName { get; set; } = "";
        public decimal RunCount { get; set; }
        public decimal OutputQuantityPerRun { get; set; }
        public string OutputUnitCode { get; set; } = "";
        public decimal RawTotalOutput { get; set; }
        public decimal NormalizedOutputPerRun { get; set; }
        public decimal NormalizedTotalOutput { get; set; }
        public string OutputBaseUnitCode { get; set; } = "";
        public string WriterMode { get; set; } = "Chưa cấu hình";
        public string WriterModeLabel { get; set; } = "Chưa xác định";
        public bool WriterCapabilityReady { get; set; }
        public bool EstimatedBomCostComplete { get; set; }
        public decimal? EstimatedBomCostPerRun { get; set; }
        public decimal? EstimatedBomCostTotal { get; set; }
        public bool CostEvidenceComplete { get; set; }
        public decimal? ProjectedFifoInputCost { get; set; }
        public decimal MaxSupportedRunCount { get; set; }
        public bool IsReady { get; set; }
        public string OverallStatus { get; set; } = "Chưa sẵn sàng";
        public List<ProductionReadinessReasonDto> Reasons { get; set; } = new();
        public List<ProductionReadinessInputDto> Inputs { get; set; } = new();
    }

    public sealed class ProductionReadinessReasonDto
    {
        public string Code { get; set; } = "";
        public string Message { get; set; } = "";
        public bool Blocking { get; set; } = true;
    }

    public sealed class ProductionReadinessInputDto
    {
        public string SourceType { get; set; } = "";
        public int? IngredientId { get; set; }
        public int? PreparedItemId { get; set; }
        public int? ChildRecipeId { get; set; }
        public string ItemCode { get; set; } = "";
        public string ItemName { get; set; } = "";
        public decimal RequiredPerRun { get; set; }
        public decimal RequiredTotal { get; set; }
        public string BaseUnitCode { get; set; } = "";
        public int? StoreInventoryId { get; set; }
        public decimal CurrentQuantity { get; set; }
        public decimal ReservedQuantity { get; set; }
        public decimal UsableQuantity { get; set; }
        public decimal ShortageQuantity { get; set; }
        public decimal MaxSupportedRunCount { get; set; }
        public decimal CostLayerAvailableQuantity { get; set; }
        public decimal CostEvidenceShortage { get; set; }
        public decimal? ProjectedFifoCost { get; set; }
        public bool InventoryResolved { get; set; }
        public bool CostEvidenceComplete { get; set; }
        public string Status { get; set; } = "";
    }
}
