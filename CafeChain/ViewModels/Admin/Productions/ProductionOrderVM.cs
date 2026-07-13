using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using CafeChain.Application.DTOs.Admin.Production;
using CafeChain.Application.DTOs.Admin.StoreInventories;

namespace CafeChain.ViewModels.Admin.Productions
{
    public class ProductionOrderVM
    {
        [Required(ErrorMessage = "Vui lòng chọn cửa hàng")]
        public int StoreId { get; set; }

        public List<InventoryStoreDTO> Stores { get; set; } = new();

        public List<ProductionRecipeOptionDto> RecipeOptions { get; set; } = new();

        // Chọn công thức Bán thành phẩm cần nấu
        [Required(ErrorMessage = "Vui lòng chọn công thức sơ chế")]
        public int TargetRecipeId { get; set; }
        public string? RecipeName { get; set; } // Display

        // Số mẻ nấu (VD: nấu 3 mẻ Cốt Trà = Quantity × 3)
        [Required]
        [Range(0.01, 9999, ErrorMessage = "Số lượng mẻ nấu phải lớn hơn 0")]
        public decimal PlannedBatches { get; set; } = 1;

        // Sản lượng dự kiến đầu ra (PlannedBatches × ExpectedYield của Recipe)
        public decimal EstimatedOutput { get; set; }
        public string? OutputUnitName { get; set; }

        // Ghi chú của Bếp trưởng
        public string? Notes { get; set; }

        // Ngày giờ thực hiện
        public DateTime ProductionDate { get; set; } = DateTime.Now;

        // Trạng thái (Planned / InProgress / Completed)
        public string Status { get; set; } = "Planned";

        // Danh sách nguyên liệu tiêu hao (tính toán từ BOM × PlannedBatches)
        public List<ProductionOrderDetailVM> ExpectedIngredients { get; set; } = new();
    }

    public class ProductionOrderDetailVM
    {
        public string ItemName { get; set; } = "";
        public string ItemType { get; set; } = ""; // "Nguyên liệu" or "Bán thành phẩm"
        public decimal BaseQuantity { get; set; }    // Qty trong BOM (1 mẻ)
        public decimal TotalQuantity { get; set; }   // BaseQty × PlannedBatches
        public string UnitName { get; set; } = "";
        public decimal YieldPercentage { get; set; } = 100;
        public decimal ActualQuantity { get; set; }  // TotalQty / (Yield/100) → thực tế cần dùng
    }
}
