using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System;

namespace CafeChain.ViewModels.Admin.Recipes
{
    public class RecipeCreateVM
    {
        // ===== LOẠI CÔNG THỨC =====
        // "POS" = Món bán (liên kết Drink+Size), "SUBRECIPE" = Bán thành phẩm (chọn từ Master Data)
        [Required(ErrorMessage = "Vui lòng chọn loại công thức")]
        public string RecipeType { get; set; } = "POS";

        // ===== CHO LOẠI POS (Món bán) =====
        public int? DrinkId { get; set; }       // Dropdown 1: Chọn sản phẩm
        public int? SizeId { get; set; }        // Dropdown 2: Chọn Size (cascaded từ DrinkId)

        // ===== CHO LOẠI SUBRECIPE (Bán thành phẩm) =====
        // Cho người dùng gõ tên Bán thành phẩm mới (VD: Cốt trà sâm bí đao)
        public string SubRecipeName { get; set; }

        // Sản lượng đầu ra dự kiến (VD: 5 Lít Cốt Trà)
        public decimal? ExpectedYield { get; set; }

        // Đơn vị đầu ra (VD: Lít, Kg, Gram)
        public int? OutputUnitId { get; set; }

        // ===== CHUNG =====
        public string Description { get; set; }

        public decimal TotalCost { get; set; }

        public bool Active { get; set; } = true;

        [Required(ErrorMessage = "Ngày hiệu lực không được để trống")]
        public DateTime EffectiveDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Công thức phải có ít nhất một nguyên liệu")]
        public List<RecipeDetailVM> Details { get; set; } = new List<RecipeDetailVM>();
    }
}
