using CafeChain.Application.DTOs.Admin.Units;
using System.ComponentModel.DataAnnotations;

namespace CafeChain.Application.DTOs.Admin.Ingredients
{
    public class AdminIngredientUpdateDTO
    {
        public int IngredientId { get; set; }

        [Required]
        public string Code { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        public int BaseUnitId { get; set; }

        public string? BaseUnitName { get; set; } // 🔥 để render select2

        public bool Active { get; set; }

        public List<UnitConversionDTO> Conversions { get; set; } = new();
    }
}
