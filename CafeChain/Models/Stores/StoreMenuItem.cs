using CafeChain.Models.Drinks;
using CafeChain.Models.Staffs;

namespace CafeChain.Models.Stores
{
    public class StoreMenuItem
    {
        public int StoreMenuItemId { get; set; }
        public int StoreId { get; set; }
        public int DrinkSizeId { get; set; }
        public bool IsEnabled { get; set; }
        public decimal? PriceOverride { get; set; }
        public DateTime? EffectiveFromUtc { get; set; }
        public DateTime? EffectiveToUtc { get; set; }
        public int DisplayOrder { get; set; }
        public string? PauseReason { get; set; }
        public string? Note { get; set; }
        public DateTime? PublishedAtUtc { get; set; }
        public int? PublishedByStaffId { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public virtual Store Store { get; set; } = null!;
        public virtual DrinkSize DrinkSize { get; set; } = null!;
        public virtual Staff? PublishedByStaff { get; set; }

        public string GetConfiguredStatus(DateTime asOfUtc)
        {
            if (!PublishedAtUtc.HasValue)
                return StoreMenuConfiguredStatuses.Draft;
            if (EffectiveToUtc.HasValue && EffectiveToUtc.Value <= asOfUtc)
                return StoreMenuConfiguredStatuses.Ended;
            if (!IsEnabled)
                return StoreMenuConfiguredStatuses.Paused;
            if (EffectiveFromUtc.HasValue && EffectiveFromUtc.Value > asOfUtc)
                return StoreMenuConfiguredStatuses.Scheduled;
            return StoreMenuConfiguredStatuses.Active;
        }

        public decimal GetEffectivePrice() => PriceOverride ?? DrinkSize.Price;

        public string GetPriceSource() => PriceOverride.HasValue
            ? StoreMenuPriceSources.StoreOverride
            : StoreMenuPriceSources.Global;
    }

    public static class StoreMenuConfiguredStatuses
    {
        public const string Draft = "DRAFT";
        public const string Scheduled = "SCHEDULED";
        public const string Active = "ACTIVE";
        public const string Paused = "PAUSED";
        public const string Ended = "ENDED";
    }

    public static class StoreMenuPriceSources
    {
        public const string Global = "GLOBAL";
        public const string StoreOverride = "STORE_OVERRIDE";
    }
}
