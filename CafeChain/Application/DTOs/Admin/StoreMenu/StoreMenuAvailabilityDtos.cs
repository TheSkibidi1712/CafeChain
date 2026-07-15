namespace CafeChain.Application.DTOs.Admin.StoreMenu
{
    public sealed class StoreMenuAvailabilityDto
    {
        public int StoreId { get; init; }
        public int StoreMenuItemId { get; init; }
        public int DrinkSizeId { get; init; }
        public string ConfiguredStatus { get; init; } = string.Empty;
        public string OperationalStatus { get; init; } = string.Empty;
        public string Reason { get; init; } = string.Empty;
        public bool IsSellable { get; init; }
        public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    }
}
