namespace CafeChain.ViewModels.Admin.Recipes;

public static class RecipeWhereUsedRelationTypes
{
    public const string MenuItemSize = "MENU_ITEM_SIZE";
    public const string Topping = "TOPPING";
    public const string PreparedItem = "PREPARED_ITEM";
    public const string PointOfSale = "POINT_OF_SALE";
}

public static class RecipeWhereUsedLimits
{
    public const int MaxParentResults = 12;
    public const int MaxPointOfSaleResults = 12;
}

public sealed class RecipeWhereUsedVM
{
    public List<RecipeWhereUsedItemVM> CurrentParents { get; set; } = [];
    public List<RecipeWhereUsedItemVM> PointOfSaleLocations { get; set; } = [];
    public bool ParentResultsTruncated { get; set; }
    public bool PointOfSaleResultsTruncated { get; set; }
    public string EmptyMessage { get; set; } = "Chưa ghi nhận nơi sử dụng hiện hành.";
    public bool IsEmpty => CurrentParents.Count == 0 && PointOfSaleLocations.Count == 0;
}

public sealed class RecipeWhereUsedItemVM
{
    public string RelationType { get; set; } = "";
    public string TypeLabel { get; set; } = "";
    public string BusinessName { get; set; } = "";
    public string? ContextLabel { get; set; }
    public string? TechnicalCode { get; set; }
    public int? ParentRecipeId { get; set; }
    public int? PinnedChildRecipeId { get; set; }
    public string? PinnedVersionLabel => PinnedChildRecipeId.HasValue
        ? $"Phiên bản đầu vào được ghim: {PinnedChildRecipeId.Value}"
        : null;
}
