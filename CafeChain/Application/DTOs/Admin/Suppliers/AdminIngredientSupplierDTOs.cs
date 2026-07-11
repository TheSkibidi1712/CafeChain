using System.ComponentModel.DataAnnotations;

namespace CafeChain.Application.DTOs.Admin.Suppliers
{
    public class AdminIngredientSupplierDTO
    {
        public int IngredientSupplierId { get; set; }
        public int IngredientId { get; set; }
        public string IngredientCode { get; set; } = "";
        public string IngredientName { get; set; } = "";
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = "";
        public decimal CurrentPrice { get; set; }
        public decimal? PackageQuantity { get; set; }
        public int UnitId { get; set; }
        public string UnitCode { get; set; } = "";
        public string UnitName { get; set; } = "";
        public int BaseUnitId { get; set; }
        public string BaseUnitCode { get; set; } = "";
        public decimal? MinimumOrderQuantity { get; set; }
        public int? LeadTimeDays { get; set; }
        public bool IsPrimary { get; set; }
        public bool Active { get; set; }
        public string? Note { get; set; }

        /// <summary>Package definition complete — not cost completeness (#117).</summary>
        public bool HasCompletePackageDefinition { get; set; }

        public string PackageDisplay { get; set; } = "";
        public string PriceDisplay { get; set; } = "";
    }

    public class AdminIngredientSupplierSaveDTO
    {
        public int? IngredientSupplierId { get; set; }

        [Required]
        public int SupplierId { get; set; }

        [Required]
        public int IngredientId { get; set; }

        [Required]
        public int UnitId { get; set; }

        public decimal? PackageQuantity { get; set; }

        [Required]
        public decimal CurrentPrice { get; set; }

        public decimal? MinimumOrderQuantity { get; set; }

        public int? LeadTimeDays { get; set; }

        public bool IsPrimary { get; set; }

        public bool Active { get; set; } = true;

        public string? Note { get; set; }
    }

    public class AdminIngredientSupplierToggleDTO
    {
        [Required]
        public int IngredientSupplierId { get; set; }

        public bool Active { get; set; }
    }
}
