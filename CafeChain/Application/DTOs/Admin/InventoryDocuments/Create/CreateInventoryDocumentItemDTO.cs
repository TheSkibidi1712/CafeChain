using System.ComponentModel.DataAnnotations;

namespace CafeChain.Application.DTOs.Admin.InventoryDocuments.Create
{
    public class CreateInventoryDocumentItemDTO
    {
        [Required]
        public int IngredientId { get; set; }

        [Required]
        public int UnitId { get; set; }

        [Required]
        public decimal Quantity { get; set; }

        // Giá nhập hiện tại
        public decimal UnitPrice { get; set; }

        // ======================
        // AUTO CALCULATE
        // ======================

        public decimal BaseQuantity { get; set; }

        public decimal TotalAmount { get; set; }

        public string? Note { get; set; }
    }
}
