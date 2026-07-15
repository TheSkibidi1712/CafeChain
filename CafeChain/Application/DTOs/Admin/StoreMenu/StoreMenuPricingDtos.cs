using System.Text.Json;
using System.Text.Json.Serialization;

namespace CafeChain.Application.DTOs.Admin.StoreMenu
{
    public sealed class StoreMenuPriceDto
    {
        public int StoreMenuItemId { get; init; }
        public int StoreId { get; init; }
        public int DrinkSizeId { get; init; }
        public decimal GlobalPrice { get; init; }
        public decimal? StoreOverride { get; init; }
        public decimal EffectivePrice { get; init; }
        public string PriceSource { get; init; } = string.Empty;
        public string RowVersion { get; init; } = string.Empty;
        public long CatalogVersion { get; init; }
    }

    public sealed class UpdateStoreMenuPriceOverrideRequest
    {
        public int StoreMenuItemId { get; set; }
        public decimal? PriceOverride { get; set; }
        public string ExpectedRowVersion { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? UnexpectedFields { get; set; }
    }
}
