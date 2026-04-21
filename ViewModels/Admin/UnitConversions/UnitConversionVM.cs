using System.ComponentModel.DataAnnotations;

namespace CafeChain.ViewModels.Admin.UnitConversions
{
    public class UnitConversionVM
    {
        public int UnitConversionId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn nguyên liệu áp dụng")]
        public int IngredientId { get; set; }
        public string? IngredientName { get; set; } // Display-only

        [Required(ErrorMessage = "Vui lòng chọn đơn vị nguồn")]
        public int FromUnitId { get; set; }
        public string? FromUnitName { get; set; } // Display-only

        [Required(ErrorMessage = "Số lượng nguồn phải lớn hơn 0")]
        [Range(0.0001, double.MaxValue, ErrorMessage = "Số lượng nguồn phải lớn hơn 0")]
        public decimal FromQuantity { get; set; } = 1;

        [Required(ErrorMessage = "Vui lòng chọn đơn vị đích")]
        public int ToUnitId { get; set; }
        public string? ToUnitName { get; set; } // Display-only

        [Required(ErrorMessage = "Số lượng đích phải lớn hơn 0")]
        [Range(0.0001, double.MaxValue, ErrorMessage = "Số lượng đích phải lớn hơn 0")]
        public decimal ToQuantity { get; set; } = 1000;
    }
}
