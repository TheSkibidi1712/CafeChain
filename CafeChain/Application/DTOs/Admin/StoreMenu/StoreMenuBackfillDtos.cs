namespace CafeChain.Application.DTOs.Admin.StoreMenu
{
    public sealed class StoreMenuBackfillCandidateDto
    {
        public int StoreId { get; init; }
        public int DrinkId { get; init; }
        public int? LegacyStoreDrinkId { get; init; }
        public int DrinkSizeId { get; init; }
        public bool IsEnabled { get; init; }
        public int DisplayOrder { get; init; }
    }

    public sealed class StoreMenuProvisioningResultDto
    {
        public int StoreId { get; init; }
        public int CreatedStoreDrinkCount { get; init; }
        public int CreatedCount { get; init; }
    }
}
