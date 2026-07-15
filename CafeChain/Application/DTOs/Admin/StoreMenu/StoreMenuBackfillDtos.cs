namespace CafeChain.Application.DTOs.Admin.StoreMenu
{
    public sealed class StoreMenuBackfillCandidateDto
    {
        public int StoreId { get; init; }
        public int LegacyStoreDrinkId { get; init; }
        public int DrinkSizeId { get; init; }
        public bool IsEnabled { get; init; }
        public int DisplayOrder { get; init; }
    }
}
