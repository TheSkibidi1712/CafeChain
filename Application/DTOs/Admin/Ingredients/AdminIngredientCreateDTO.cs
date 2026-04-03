using System.ComponentModel.DataAnnotations;

namespace CafeChain.Application.DTOs.Admin.Ingredients
{
    public class AdminIngredientCreateDTO
    {
        [Required(ErrorMessage = "Mã nguyên liệu không được để trống.")]
        [StringLength(20, MinimumLength = 3, ErrorMessage = "Mã nguyên liệu từ 3-20 ký tự.")]
        public string Code { get; set; }

        [Required(ErrorMessage = "Tên nguyên liệu không được để trống.")]
        [StringLength(100, ErrorMessage = "Tên nguyên liệu tối đa 100 ký tự.")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Đơn vị tính không được để trống.")]
        [StringLength(20, ErrorMessage = "Đơn vị tính tối đa 20 ký tự.")]
        public string BaseUnit { get; set; }
    }
}
