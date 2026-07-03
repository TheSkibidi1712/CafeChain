using CafeChain.Models.Enums.Inventory;
using System.ComponentModel.DataAnnotations;

namespace CafeChain.Application.DTOs.Admin.InventoryDocuments.Create
{
    public class CreateInventoryDocumentDTO
    {
        // =====================================================
        // HEADER
        // =====================================================

        [Required]
        public InventoryDocumentType Type { get; set; }

        [Required]
        public InventoryDocumentPurpose Purpose { get; set; }

        [Required]
        public int StoreId { get; set; }

        [Required]
        public DateTime DocumentDate { get; set; }

        public string? Note { get; set; }

        // =====================================================
        // PARTNER
        // =====================================================

        public InventoryPartnerType PartnerType { get; set; }

        public int? SupplierId { get; set; }

        public int? PartnerId { get; set; }

        public string? PartnerName { get; set; }

        public int? TargetStoreId { get; set; }

        public int? SourceTransferId { get; set; }

        // =====================================================
        // DETAIL
        // =====================================================

        public List<CreateInventoryDocumentItemDTO> Details
        { get; set; } = [];

        // =====================================================
        // SAVE OPTION
        // =====================================================

        public bool SaveAsDraft { get; set; }
    }
}
