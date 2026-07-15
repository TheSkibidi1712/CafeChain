using System.Text.Json;
using System.Text.Json.Serialization;

namespace CafeChain.Application.DTOs.Admin.StoreMenu
{
    public sealed class StoreMenuWorkspaceRowDto
    {
        public int StoreMenuItemId { get; init; }
        public int StoreId { get; init; }
        public int DrinkId { get; init; }
        public int DrinkSizeId { get; init; }
        public string DrinkCode { get; init; } = string.Empty;
        public string DrinkName { get; init; } = string.Empty;
        public string SizeName { get; init; } = string.Empty;
        public string CategoryName { get; init; } = string.Empty;
        public string ConfiguredStatus { get; init; } = string.Empty;
        public string OperationalStatus { get; init; } = string.Empty;
        public string AvailabilityReason { get; init; } = string.Empty;
        public bool IsSellable { get; init; }
        public decimal GlobalPrice { get; init; }
        public decimal? StoreOverride { get; init; }
        public decimal EffectivePrice { get; init; }
        public string PriceSource { get; init; } = string.Empty;
        public decimal? FifoCost { get; init; }
        public string CostStatus { get; init; } = string.Empty;
        public decimal? EstimatedGrossMarginPercent { get; init; }
        public DateTime? EffectiveFromUtc { get; init; }
        public DateTime? EffectiveToUtc { get; init; }
        public int DisplayOrder { get; init; }
        public string? PauseReason { get; init; }
        public int? RecipeId { get; init; }
        public string RowVersion { get; init; } = string.Empty;
    }

    public sealed class UpdateStoreMenuLifecycleRequest
    {
        public int StoreMenuItemId { get; set; }
        public string Action { get; set; } = string.Empty;
        public int? DisplayOrder { get; set; }
        public string ExpectedRowVersion { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? UnexpectedFields { get; set; }
    }

    public static class StoreMenuLifecycleActions
    {
        public const string Publish = "PUBLISH";
        public const string Pause = "PAUSE";
        public const string Resume = "RESUME";
        public const string ChangeDisplayOrder = "CHANGE_DISPLAY_ORDER";

        public static bool IsSupported(string action) =>
            action is Publish or Pause or Resume or ChangeDisplayOrder;
    }
}
