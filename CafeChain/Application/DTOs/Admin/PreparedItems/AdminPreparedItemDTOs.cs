using System.ComponentModel.DataAnnotations;

namespace CafeChain.Application.DTOs.Admin.PreparedItems
{
    public class AdminPreparedItemDTO
    {
        public int PreparedItemId { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public int BaseUnitId { get; set; }
        public string BaseUnitCode { get; set; } = "";
        public string BaseUnitName { get; set; } = "";
        public string? Description { get; set; }
        public bool Active { get; set; }

        // #126 additive list/combobox projection (read-only)
        public int? ActiveRecipeId { get; set; }
        public string? ActiveRecipeCode { get; set; }
        public string? ActiveRecipeName { get; set; }
        public int VersionCount { get; set; }

        /// <summary>Chưa có công thức | Có công thức hoạt động | Ngừng hoạt động</summary>
        public string ConfigStatus { get; set; } = "";

        /// <summary>Machine key: no_recipe | has_active | inactive</summary>
        public string ConfigStatusKey { get; set; } = "";
    }

    /// <summary>#126 BOM combobox option (Code+Name search, active recipe meta).</summary>
    public class AdminPreparedItemBomOptionDTO
    {
        public int PreparedItemId { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public int BaseUnitId { get; set; }
        public string BaseUnitCode { get; set; } = "";
        public string BaseUnitName { get; set; } = "";
        public bool Active { get; set; }
        public int? ActiveRecipeId { get; set; }
        public string? ActiveRecipeCode { get; set; }
        public string? ActiveRecipeName { get; set; }
        public int VersionCount { get; set; }
        public bool HasActiveRecipe => ActiveRecipeId.HasValue;
    }

    public class AdminPreparedItemSaveDTO
    {
        public int? PreparedItemId { get; set; }

        [Required(ErrorMessage = "Mã BTP là bắt buộc.")]
        [MaxLength(50)]
        public string Code { get; set; } = "";

        [Required(ErrorMessage = "Tên bán thành phẩm là bắt buộc.")]
        [MaxLength(200)]
        public string Name { get; set; } = "";

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Đơn vị tồn kho chuẩn là bắt buộc.")]
        public int BaseUnitId { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        public bool Active { get; set; } = true;
    }

    public class AdminPreparedItemToggleDTO
    {
        [Required]
        public int PreparedItemId { get; set; }

        public bool Active { get; set; }
    }
}
