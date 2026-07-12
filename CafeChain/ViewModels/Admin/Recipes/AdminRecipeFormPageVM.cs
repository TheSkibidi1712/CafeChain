using System.Collections.Generic;
using CafeChain.Application.DTOs.Admin.PreparedItems;

namespace CafeChain.ViewModels.Admin.Recipes
{
    public class RecipeFormIdNameOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public class RecipeFormUnitOption
    {
        public int UnitId { get; set; }
        public string Name { get; set; } = "";
        public string UnitCode { get; set; } = "";
    }

    /// <summary>Ingredient option for BOM row template (cost fields server-filled).</summary>
    public class RecipeBomIngredientOptionVM
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public decimal BaseCost { get; set; }
        public bool CostComplete { get; set; }
        public decimal? PackagePrice { get; set; }
        public decimal? PackageQuantity { get; set; }
        public string? PackageUnitCode { get; set; }
        public string? BaseUnitCode { get; set; }
        public string? CostMessage { get; set; }
        public int UnitId { get; set; }
        public string UnitName { get; set; } = "";
    }

    /// <summary>Child recipe option — pin RecipeId (REC_{id}).</summary>
    public class RecipeBomChildRecipeOptionVM
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? RecipeCode { get; set; }
        public int? PreparedItemId { get; set; }
        public string? PreparedItemCode { get; set; }
        public string? PreparedItemName { get; set; }
        public decimal? OutputQuantity { get; set; }
        public string? OutputUnitCode { get; set; }
        public decimal BaseCost { get; set; }
        public bool CostComplete { get; set; }
        public int UnitId { get; set; }
        public string UnitName { get; set; } = "Phần";
        public string? CostMessage { get; set; }
    }

    public class AdminRecipeFormOptionsVM
    {
        public List<RecipeBomIngredientOptionVM> Ingredients { get; set; } = new();
        public List<RecipeBomChildRecipeOptionVM> SubRecipes { get; set; } = new();
        public List<RecipeFormIdNameOption> Drinks { get; set; } = new();
        public List<RecipeFormIdNameOption> Toppings { get; set; } = new();
        public List<AdminPreparedItemBomOptionDTO> PreparedItems { get; set; } = new();
        public List<RecipeFormUnitOption> Units { get; set; } = new();
    }

    public class AdminRecipeFormPageVM
    {
        public RecipeCreateVM Form { get; set; } = new();
        public AdminRecipeFormOptionsVM Options { get; set; } = new();
        public int? SourceRecipeId { get; set; }
        public string? RecipeName { get; set; }
        public bool IsEdit { get; set; }
    }

    public class AdminRecipeVisualizePageVM
    {
        public int RecipeId { get; set; }
        public string Name { get; set; } = "";
        public string Status { get; set; } = "";
        public string TypeLabel { get; set; } = "";
        public int? PreparedItemId { get; set; }
        public string? PreparedItemCode { get; set; }
        public string? PreparedItemName { get; set; }
        public decimal? OutputQuantity { get; set; }
        public string? OutputUnitCode { get; set; }
        public string? OutputUnitName { get; set; }
        public List<RecipeBomTreeNodeVM> FirstLevelNodes { get; set; } = new();
    }

    public class RecipeSizeOptionVM
    {
        public int SizeId { get; set; }
        public string SizeName { get; set; } = "";
        public decimal Price { get; set; }
    }
}
