using System.ComponentModel.DataAnnotations;

namespace CafeChain.Application.DTOs.Admin.Ingredients
{
    public class AdminIngredientUpdateDTO
    {
        public int IngredientId { get; set; }

        [Required, StringLength(50)]
        public string Code { get; set; }

        [Required, StringLength(200)]
        public string Name { get; set; }

        [Required]
        public int BaseUnitId { get; set; }

        public string? BaseUnitName { get; set; } // 🔥 để render select2

    }
}
