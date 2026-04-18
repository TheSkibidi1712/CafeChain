namespace CafeChain.Models.Enums.Inventory
{
    public enum InventoryTransferStatus
    {
        PENDING = 1,          // đã tạo phiếu xuất, chưa nhận
        IN_PROGRESS = 2,      // đang nhận (có thể nhận 1 phần)
        COMPLETED = 3,        // đã nhận đủ
        PARTIAL = 4,          // nhận thiếu
        CANCELLED = 5         // hủy
    }
}
