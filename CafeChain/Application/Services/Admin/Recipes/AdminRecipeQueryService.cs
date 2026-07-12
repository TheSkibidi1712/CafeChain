using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces.Admin.PreparedItems;
using CafeChain.Application.Interfaces.Admin.Recipes;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Data;
using CafeChain.Models.Enums.Unit;
using CafeChain.ViewModels.Admin.Recipes;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Admin.Recipes
{
    /// <summary>
    /// #129 Read/orchestration for Recipe Admin pages. Does not own write/create domain rules.
    /// Cost/normalize use existing services (no algorithm rewrite).
    /// </summary>
    public sealed class AdminRecipeQueryService : IAdminRecipeQueryService
    {
        private readonly AppDbContext _context;
        private readonly IRecipeOutputNormalizer _outputNormalizer;
        private readonly IEstimatedBomCostService _estimatedBomCost;
        private readonly IAdminPreparedItemService _preparedItemService;
        private readonly IRecipeBomTreeQueryService _bomTree;

        public AdminRecipeQueryService(
            AppDbContext context,
            IRecipeOutputNormalizer outputNormalizer,
            IEstimatedBomCostService estimatedBomCost,
            IAdminPreparedItemService preparedItemService,
            IRecipeBomTreeQueryService bomTree)
        {
            _context = context;
            _outputNormalizer = outputNormalizer;
            _estimatedBomCost = estimatedBomCost;
            _preparedItemService = preparedItemService;
            _bomTree = bomTree;
        }

        public async Task<AdminRecipeListPageVM> GetIndexPageAsync(string? typeFilter = null)
        {
            var recipes = await _context.Recipes
                .AsNoTracking()
                .Include(r => r.PreparedItem)
                    .ThenInclude(p => p!.BaseUnit)
                .Include(r => r.OutputUnit)
                .Include(r => r.Size)
                .Where(r => r.Status == "Active")
                .OrderByDescending(r => r.RecipeId)
                .ToListAsync();

            var items = new List<AdminRecipeListItemVM>();
            foreach (var r in recipes)
            {
                var typeKey = ResolveRecipeTypeKey(r);
                if (!string.IsNullOrWhiteSpace(typeFilter)
                    && !string.Equals(typeFilter, "ALL", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(typeFilter, typeKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var vm = new AdminRecipeListItemVM
                {
                    RecipeId = r.RecipeId,
                    RecipeCode = r.RecipeCode ?? "",
                    Name = r.Name ?? "",
                    RecipeType = typeKey,
                    TypeLabel = typeKey switch
                    {
                        "POS" => "Món bán",
                        "TOPPING" => "Topping",
                        "SUBRECIPE" => "Bán thành phẩm",
                        _ => "Khác"
                    },
                    IdentityDisplay = BuildIdentityDisplay(r, typeKey),
                    PreparedItemId = r.PreparedItemId,
                    PreparedItemCode = r.PreparedItem?.Code,
                    PreparedItemName = r.PreparedItem?.Name,
                    DrinkId = r.DrinkId,
                    SizeId = r.SizeId,
                    ToppingId = r.ToppingId,
                    OutputQuantity = r.OutputQuantity,
                    OutputUnitCode = r.OutputUnit?.UnitCode,
                    OutputUnitName = r.OutputUnit?.Name,
                    Active = r.Active,
                    Status = r.Status ?? "",
                    EffectiveDate = r.EffectiveDate,
                    ParentVersionId = r.ParentVersionId,
                    BaseUnitCode = r.PreparedItem?.BaseUnit?.UnitCode
                };

                if (r.OutputQuantity.HasValue && r.OutputUnitId.HasValue)
                {
                    vm.OutputPerBatchDisplay =
                        $"{r.OutputQuantity.Value:0.####} {r.OutputUnit?.UnitCode ?? r.OutputUnit?.Name ?? ""}".Trim();
                }

                if (r.PreparedItemId.HasValue
                    && r.OutputQuantity.HasValue
                    && r.OutputUnitId.HasValue)
                {
                    var norm = await _outputNormalizer.NormalizeAsync(
                        r.PreparedItemId.Value,
                        r.OutputQuantity.Value,
                        r.OutputUnitId.Value);
                    if (norm.IsSuccess && norm.Data != null)
                    {
                        vm.NormalizedQuantityInBase = norm.Data.NormalizedQuantityInBase;
                        vm.BaseUnitCode = norm.Data.BaseUnitCode;
                        vm.NormalizedOutputDisplay =
                            $"{norm.Data.NormalizedQuantityInBase:0.####} {norm.Data.BaseUnitCode}";
                    }
                }

                try
                {
                    var cost = await _estimatedBomCost.CalculateRecipeEstimatedCostAsync(r.RecipeId);
                    vm.CostComplete = cost.IsComplete;
                    vm.EstimatedCost = cost.IsComplete ? cost.TotalCost : null;
                    vm.CostStatus = cost.IsComplete ? "Đủ dữ liệu" : "Thiếu dữ liệu";
                }
                catch
                {
                    vm.CostStatus = "—";
                }

                items.Add(vm);
            }

            return new AdminRecipeListPageVM
            {
                TypeFilter = string.IsNullOrWhiteSpace(typeFilter) ? "ALL" : typeFilter.ToUpperInvariant(),
                Items = items
            };
        }

        public async Task<AdminRecipeFormPageVM> GetCreatePageAsync()
        {
            return new AdminRecipeFormPageVM
            {
                Form = new RecipeCreateVM(),
                Options = await GetFormOptionsAsync(),
                IsEdit = false
            };
        }

        public async Task<AdminRecipeFormPageVM?> GetEditPageAsync(int recipeId)
        {
            var recipe = await _context.Recipes
                .AsNoTracking()
                .Include(r => r.RecipeDetails)
                    .ThenInclude(rd => rd.Ingredient)
                        .ThenInclude(i => i!.BaseUnit)
                .Include(r => r.RecipeDetails)
                    .ThenInclude(rd => rd.ChildRecipe)
                .Include(r => r.RecipeDetails)
                    .ThenInclude(rd => rd.Unit)
                .Include(r => r.PreparedItem)
                .FirstOrDefaultAsync(r => r.RecipeId == recipeId && r.Status == "Active");

            if (recipe == null)
                return null;

            bool isLegacyUnmapped =
                !recipe.DrinkId.HasValue
                && !recipe.ToppingId.HasValue
                && !recipe.PreparedItemId.HasValue;

            string recipeType = recipe.ToppingId.HasValue
                ? "TOPPING"
                : recipe.DrinkId.HasValue
                    ? "POS"
                    : "SUBRECIPE";

            var form = new RecipeCreateVM
            {
                RecipeType = recipeType,
                DrinkId = recipe.DrinkId,
                SizeId = recipe.SizeId,
                ToppingId = recipe.ToppingId,
                PreparedItemId = recipe.PreparedItemId,
                ExpectedYield = recipe.OutputQuantity,
                OutputUnitId = recipe.OutputUnitId,
                SubRecipeName = recipe.DrinkId.HasValue || recipe.ToppingId.HasValue
                    ? null
                    : recipe.Name,
                IsLegacyUnmappedSubRecipe = isLegacyUnmapped,
                PreparedItemLocked = recipe.PreparedItemId.HasValue,
                Active = recipe.Active,
                EffectiveDate = recipe.EffectiveDate ?? DateTime.Today,
                Description = null,
                Details = recipe.RecipeDetails.Select(rd => new RecipeDetailVM
                {
                    ItemCode = rd.IngredientId.HasValue
                        ? $"ING_{rd.IngredientId}"
                        : $"REC_{rd.ChildRecipeId}",
                    Quantity = rd.Quantity,
                    UnitId = rd.UnitId,
                    UnitName = rd.Unit?.Name ?? ""
                }).ToList()
            };

            return new AdminRecipeFormPageVM
            {
                Form = form,
                Options = await GetFormOptionsAsync(),
                SourceRecipeId = recipeId,
                RecipeName = recipe.Name,
                IsEdit = true
            };
        }

        public async Task<AdminRecipeVisualizePageVM?> GetVisualizePageAsync(int recipeId)
        {
            var recipe = await _context.Recipes
                .AsNoTracking()
                .Include(r => r.PreparedItem)
                .Include(r => r.OutputUnit)
                .FirstOrDefaultAsync(r => r.RecipeId == recipeId);

            if (recipe == null)
                return null;

            var tree = await _bomTree.BuildTreeAsync(recipeId);
            string typeLabel = recipe.ToppingId.HasValue ? "Topping"
                : recipe.DrinkId.HasValue ? "Món bán (POS)"
                : recipe.PreparedItemId.HasValue ? "Bán thành phẩm"
                : "Công thức";

            return new AdminRecipeVisualizePageVM
            {
                RecipeId = recipe.RecipeId,
                Name = recipe.Name ?? "",
                Status = recipe.Status ?? "",
                TypeLabel = typeLabel,
                PreparedItemId = recipe.PreparedItemId,
                PreparedItemCode = recipe.PreparedItem?.Code,
                PreparedItemName = recipe.PreparedItem?.Name,
                OutputQuantity = recipe.OutputQuantity,
                OutputUnitCode = recipe.OutputUnit?.UnitCode,
                OutputUnitName = recipe.OutputUnit?.Name,
                FirstLevelNodes = tree.Roots
            };
        }

        public async Task<AdminRecipeFormOptionsVM> GetFormOptionsAsync()
        {
            var options = new AdminRecipeFormOptionsVM();

            // Preserve previous behavior: resolve ingredient base-unit cost via existing service (sequential).
            var ingredients = await _context.Ingredients
                .AsNoTracking()
                .Include(i => i.BaseUnit)
                .Where(x => x.Active)
                .OrderBy(x => x.Name)
                .ToListAsync();

            foreach (var x in ingredients)
            {
                var cost = await _estimatedBomCost.ResolveIngredientBaseUnitCostAsync(x.IngredientId);
                options.Ingredients.Add(new RecipeBomIngredientOptionVM
                {
                    Id = x.IngredientId,
                    Name = x.Name ?? "",
                    BaseCost = cost.IsComplete ? cost.BaseUnitCost!.Value : 0m,
                    CostComplete = cost.IsComplete,
                    PackagePrice = cost.PackagePrice,
                    PackageQuantity = cost.PackageQuantity,
                    PackageUnitCode = cost.PackageUnitCode,
                    BaseUnitCode = cost.BaseUnitCode ?? x.BaseUnit?.UnitCode,
                    CostMessage = cost.IsComplete
                        ? null
                        : (cost.Issues.FirstOrDefault()?.Message ?? "Chưa đủ dữ liệu giá vốn"),
                    UnitId = x.BaseUnitId,
                    UnitName = x.BaseUnit?.Name ?? ""
                });
            }

            options.SubRecipes = await _context.Recipes
                .AsNoTracking()
                .Include(x => x.PreparedItem)
                .Include(x => x.OutputUnit)
                .Where(x => x.Active && x.Status == "Active")
                .OrderBy(x => x.Name)
                .Select(x => new RecipeBomChildRecipeOptionVM
                {
                    Id = x.RecipeId,
                    Name = x.Name ?? "",
                    RecipeCode = x.RecipeCode,
                    PreparedItemId = x.PreparedItemId,
                    PreparedItemCode = x.PreparedItem != null ? x.PreparedItem.Code : null,
                    PreparedItemName = x.PreparedItem != null ? x.PreparedItem.Name : null,
                    OutputQuantity = x.OutputQuantity,
                    OutputUnitCode = x.OutputUnit != null ? x.OutputUnit.UnitCode : null,
                    BaseCost = 0m,
                    CostComplete = false,
                    UnitId = x.OutputUnitId ?? 0,
                    UnitName = x.OutputUnit != null ? x.OutputUnit.Name : "Phần",
                    CostMessage = "BTP con: pin phiên bản Recipe — EstimateBomCost trên server"
                })
                .ToListAsync();

            options.Drinks = await _context.Drinks
                .AsNoTracking()
                .Where(x => x.Active)
                .OrderBy(x => x.Name)
                .Select(x => new RecipeFormIdNameOption { Id = x.DrinkId, Name = x.Name ?? "" })
                .ToListAsync();

            options.Toppings = await _context.Toppings
                .AsNoTracking()
                .Where(x => x.Active)
                .OrderBy(x => x.Name)
                .Select(x => new RecipeFormIdNameOption { Id = x.ToppingId, Name = x.Name ?? "" })
                .ToListAsync();

            options.PreparedItems = await _preparedItemService.GetBomOptionsAsync(null);

            var units = await _context.Units
                .AsNoTracking()
                .Where(x => x.Active
                    && (x.Type == UnitType.KhoiLuong
                        || x.Type == UnitType.TheTich
                        || x.Type == UnitType.Dem))
                .OrderBy(x => x.UnitCode)
                .Select(x => new { x.UnitId, x.Name, x.UnitCode })
                .ToListAsync();

            options.Units = units
                .Where(u => !PackageUnitCodes.IsRejectedCommercialPackaging(u.UnitCode))
                .Select(u => new RecipeFormUnitOption
                {
                    UnitId = u.UnitId,
                    Name = u.Name ?? "",
                    UnitCode = u.UnitCode ?? ""
                })
                .ToList();

            return options;
        }

        public async Task<List<RecipeSizeOptionVM>> GetSizesByDrinkAsync(int drinkId)
        {
            return await _context.DrinkSizes
                .AsNoTracking()
                .Include(ds => ds.Size)
                .Where(ds => ds.DrinkId == drinkId && ds.Active)
                .Select(ds => new RecipeSizeOptionVM
                {
                    SizeId = ds.SizeId,
                    SizeName = ds.Size.Name ?? "",
                    Price = ds.Price
                })
                .ToListAsync();
        }

        private static string ResolveRecipeTypeKey(Models.Drinks.Recipe r)
        {
            if (r.ToppingId.HasValue) return "TOPPING";
            if (r.DrinkId.HasValue) return "POS";
            if (r.PreparedItemId.HasValue) return "SUBRECIPE";
            if (!r.DrinkId.HasValue && !r.ToppingId.HasValue) return "SUBRECIPE";
            return "OTHER";
        }

        private static string BuildIdentityDisplay(Models.Drinks.Recipe r, string typeKey)
        {
            if (typeKey == "SUBRECIPE" && r.PreparedItem != null)
                return $"[{r.PreparedItem.Code}] {r.PreparedItem.Name}";
            if (typeKey == "SUBRECIPE")
                return r.Name ?? $"Recipe #{r.RecipeId}";
            if (typeKey == "TOPPING")
                return r.Name ?? $"Topping #{r.ToppingId}";
            if (typeKey == "POS")
            {
                var size = r.Size?.Name;
                return string.IsNullOrWhiteSpace(size) ? (r.Name ?? "") : $"{r.Name} · {size}";
            }
            return r.Name ?? $"#{r.RecipeId}";
        }
    }
}
