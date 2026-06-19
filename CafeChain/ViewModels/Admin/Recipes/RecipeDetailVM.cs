using System.ComponentModel.DataAnnotations;

namespace CafeChain.ViewModels.Admin.Recipes
{
    public class RecipeDetailVM
    {
        [Required(ErrorMessage = "Danh tính cấu thành (ItemCode) không hợp lệ")]
        public string ItemCode { get; set; } // "ING_15", "REC_2"

        [Range(0.01, double.MaxValue, ErrorMessage = "Định lượng (Quantity) phải lớn hơn 0")]
        public decimal Quantity { get; set; }

        [Required(ErrorMessage = "Đơn vị tính không được để trống")]
        public int UnitId { get; set; }

        public string? UnitName { get; set; } // READ-ONLY, auto-populated từ BaseUnit

        // V2: Tỷ lệ thu hồi di chuyển xuống cấp nguyên liệu
        // 100% = không hao hụt, 95% = mất 5%
        [Range(0, 100, ErrorMessage = "Tỷ lệ thu hồi phải từ 0 đến 100")]
        public decimal YieldPercentage { get; set; } = 100;
    }
}
