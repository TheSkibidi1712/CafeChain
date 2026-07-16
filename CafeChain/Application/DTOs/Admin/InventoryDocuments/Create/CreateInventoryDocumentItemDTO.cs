using System.ComponentModel.DataAnnotations;

namespace CafeChain.Application.DTOs.Admin.InventoryDocuments.Create
{
    public class CreateInventoryDocumentItemDTO
    {
        [Required]
        public int IngredientId { get; set; }

        public int? IngredientSupplierId { get; set; }

        [Required]
        public int UnitId { get; set; }

        [Required]
        public decimal Quantity { get; set; }

        // Giá nhập theo đơn vị đang chọn. Với phiếu xuất có thể để 0.
        public decimal UnitPrice { get; set; }

        // Giá vốn theo đơn vị cơ sở. Dùng khi nghiệp vụ tạo ra tồn kho nhưng không có giá nhập trực tiếp, ví dụ Production In.
        public decimal? CostPrice { get; set; }

        // Tổng giá vốn theo BaseQuantity. Nếu không truyền, service sẽ tính bằng BaseQuantity * CostPrice khi có CostPrice.
        public decimal? CostAmount { get; set; }

        // ======================
        // AUTO CALCULATE
        // ======================

        public decimal BaseQuantity { get; set; }

        public decimal TotalAmount { get; set; }

        public string? Note { get; set; }
    }
}
