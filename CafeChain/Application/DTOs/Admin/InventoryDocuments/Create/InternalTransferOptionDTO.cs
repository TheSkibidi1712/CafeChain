namespace CafeChain.Application.DTOs.Admin.InventoryDocuments.Create
{
    public class InternalTransferOptionDTO
    {
        public int InventoryTransferId { get; set; }

        public string TransferCode { get; set; } = string.Empty;

        public int FromStoreId { get; set; }

        public string FromStoreName { get; set; } = string.Empty;

        public int ToStoreId { get; set; }

        public string ToStoreName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public decimal TotalBaseQuantity { get; set; }
    }
}
