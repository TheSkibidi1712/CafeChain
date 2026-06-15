namespace CafeChain.ViewModels.Admin.InventoryDocuments
{
    public class InventoryTransferReceiveItemVM
    {
        public int IngredientId { get; set; }

        public decimal BaseQuantity { get; set; } // 🔥 nhận theo base luôn

        public string? Note { get; set; }
    }
}
