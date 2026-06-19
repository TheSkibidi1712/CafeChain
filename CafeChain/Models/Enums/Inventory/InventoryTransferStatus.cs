namespace CafeChain.Models.Enums.Inventory
{
    public enum InventoryTransferStatus
    {
        PENDING = 1,          // chưa nhận
        IN_PROGRESS = 2,      // đang nhận
        READY = 3,            // đã nhận đủ, chờ confirm
        COMPLETED = 4,        // đã confirm
        CANCELLED = 5
    }
}
