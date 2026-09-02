namespace CafeChain.ViewModels.Admin.Recipes;

public sealed class RecipeVersionHistoryVM
{
    public const int ResultLimit = 20;

    public List<RecipeVersionHistoryItemVM> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public bool IsTruncated => TotalCount > Items.Count;
}

public sealed class RecipeVersionHistoryItemVM
{
    public int RecipeId { get; set; }
    public int? ParentVersionId { get; set; }
    public string VersionLabel { get; set; } = "";
    public string StateLabel { get; set; } = "";
    public bool IsCurrent { get; set; }
    public DateTime? AppliedAt { get; set; }
    public string RelationshipLabel { get; set; } = "";
}

public sealed class RecipeVersionCompareResult
{
    public bool IsSuccess { get; init; }
    public string? ReasonCode { get; init; }
    public string? Message { get; init; }
    public RecipeVersionCompareVM? Comparison { get; init; }

    public static RecipeVersionCompareResult Success(RecipeVersionCompareVM comparison) => new()
    {
        IsSuccess = true,
        Comparison = comparison
    };

    public static RecipeVersionCompareResult Failure(string reasonCode, string message) => new()
    {
        IsSuccess = false,
        ReasonCode = reasonCode,
        Message = message
    };
}

public sealed class RecipeVersionCompareVM
{
    public string BusinessName { get; set; } = "";
    public string TargetLabel { get; set; } = "";
    public RecipeVersionCompareSideVM From { get; set; } = new();
    public RecipeVersionCompareSideVM To { get; set; } = new();
    public string OutputChangeLabel { get; set; } = "Không thay đổi";
    public bool OutputChanged { get; set; }
    public decimal? DesignCostDelta { get; set; }
    public string CostCompletenessChangeLabel { get; set; } = "";
    public List<RecipeVersionLineChangeVM> AddedLines { get; set; } = new();
    public List<RecipeVersionLineChangeVM> RemovedLines { get; set; } = new();
    public List<RecipeVersionLineChangeVM> ChangedLines { get; set; } = new();
    public bool HasLineChanges => AddedLines.Count + RemovedLines.Count + ChangedLines.Count > 0;
}

public sealed class RecipeVersionCompareSideVM
{
    public int RecipeId { get; set; }
    public string VersionLabel { get; set; } = "";
    public string StateLabel { get; set; } = "";
    public string OutputDisplay { get; set; } = "";
    public decimal? DesignCost { get; set; }
    public string CostCompletenessLabel { get; set; } = "";
    public bool IsCurrent { get; set; }
    public bool CostComplete { get; set; }
}

public sealed class RecipeVersionLineChangeVM
{
    public string BusinessName { get; set; } = "";
    public string InputTypeLabel { get; set; } = "";
    public string InputTypeCode { get; set; } = "";
    public string? TechnicalCode { get; set; }
    public string? BeforeQuantity { get; set; }
    public string? AfterQuantity { get; set; }
    public string? BeforeNormalizedQuantity { get; set; }
    public string? AfterNormalizedQuantity { get; set; }
    public string ChangeSummary { get; set; } = "";
    public List<string> ChangeCodes { get; set; } = new();
}
