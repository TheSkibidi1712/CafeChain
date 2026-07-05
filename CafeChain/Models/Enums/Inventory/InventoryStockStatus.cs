namespace CafeChain.Models.Enums.Inventory
{
    public enum InventoryStockStatus
    {
        NORMAL = 1,
        LOW_STOCK = 2,
        NEGATIVE_PENDING = 3,
        NEGATIVE_CONFIRMED = 4,
        ADJUSTED = 5
    }
}
