using System.ComponentModel.DataAnnotations;

namespace CafeChain.Application.DTOs.Admin.Ingredients
{
    public class AdminIngredientCreateDTO
    {
        [Required, StringLength(50)]
        public string Code { get; set; }

        [Required, StringLength(200)]
        public string Name { get; set; }

        [Required]
        public int BaseUnitId { get; set; }

    }
}
