namespace CafeChain.Application.DTOs.Admin.InventoryDocuments.Create
{
    public class ConfirmInventoryDocumentDTO
    {
        public int InventoryDocumentId { get; set; }

        public int ConfirmedByStaffId { get; set; }

        public string? RequestKey { get; set; }
    }
}
