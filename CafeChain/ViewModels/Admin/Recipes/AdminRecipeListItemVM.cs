using System;
using System.Collections.Generic;

namespace CafeChain.ViewModels.Admin.Recipes
{
    /// <summary>#126 Recipe list row — type from identity fields, not name heuristic.</summary>
    public class AdminRecipeListItemVM
    {
        public int RecipeId { get; set; }
        public string RecipeCode { get; set; } = "";
        public string Name { get; set; } = "";

        /// <summary>POS | TOPPING | SUBRECIPE</summary>
        public string RecipeType { get; set; } = "";

        public string TypeLabel { get; set; } = "";

        public string IdentityDisplay { get; set; } = "";

        public int? PreparedItemId { get; set; }
        public string? PreparedItemCode { get; set; }
        public string? PreparedItemName { get; set; }

        public int? DrinkId { get; set; }
        public int? SizeId { get; set; }
        public int? ToppingId { get; set; }

        public decimal? OutputQuantity { get; set; }
        public string? OutputUnitCode { get; set; }
        public string? OutputUnitName { get; set; }

        public decimal? NormalizedQuantityInBase { get; set; }
        public string? BaseUnitCode { get; set; }

        public bool Active { get; set; }
        public string Status { get; set; } = "";
        public DateTime? EffectiveDate { get; set; }
        public int? ParentVersionId { get; set; }

        public string CostStatus { get; set; } = "";
        public bool? CostComplete { get; set; }
        public decimal? EstimatedCost { get; set; }

        public decimal? EstimatedUnitCost { get; set; }

        public int ComponentCount { get; set; }

        public string PortionDefinitionDisplay { get; set; } = "Chưa có thành phần";

        public string ConsumptionSourceDisplay { get; set; } = "Chưa cấu hình";

        public BomHealthStatusVM ConfigurationHealth { get; set; } = new();

        public BomHealthStatusVM CostingHealth { get; set; } = new();

        public string OutputPerBatchDisplay { get; set; } = "—";
        public string NormalizedOutputDisplay { get; set; } = "—";
    }

    public class AdminRecipeListPageVM
    {
        public string? TypeFilter { get; set; }
        public string? Search { get; set; }
        public string StatusFilter { get; set; } = "ALL";
        public int Page { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int TotalCount { get; set; }
        public bool CanWrite { get; set; }
        public List<AdminRecipeListItemVM> Items { get; set; } = new();
    }
}
