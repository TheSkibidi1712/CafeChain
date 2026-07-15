namespace CafeChain.Application.DTOs.POS
{
    public sealed class POSAcceptedSaleLineDto
    {
        public int StoreMenuItemId { get; init; }
        public int DrinkSizeId { get; init; }
        public int DrinkId { get; init; }
        public int SizeId { get; init; }
        public string DrinkName { get; init; } = string.Empty;
        public string? SizeName { get; init; }
        public decimal AcceptedBasePrice { get; init; }
        public decimal AcceptedUnitPrice { get; init; }
        public string PriceSource { get; init; } = string.Empty;
        public long CatalogVersion { get; init; }
        public IReadOnlyList<POSAcceptedSaleToppingDto> Toppings { get; init; } = Array.Empty<POSAcceptedSaleToppingDto>();
    }

    public sealed class POSAcceptedSaleToppingDto
    {
        public int ToppingId { get; init; }
        public string Name { get; init; } = string.Empty;
        public decimal AcceptedPrice { get; init; }
    }
}
