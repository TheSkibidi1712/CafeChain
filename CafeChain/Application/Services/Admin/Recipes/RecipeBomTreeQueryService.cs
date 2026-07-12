using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CafeChain.Application.Interfaces.Admin.Recipes;
using CafeChain.Data;
using CafeChain.ViewModels.Admin.Recipes;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Admin.Recipes
{
    /// <summary>
    /// #129 Bounded BOM tree: batch load levels, pin ChildRecipeId, depth + cycle path guard.
    /// </summary>
    public sealed class RecipeBomTreeQueryService : IRecipeBomTreeQueryService
    {
        public const int DefaultMaxDepth = 5;

        private readonly AppDbContext _context;

        public RecipeBomTreeQueryService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<RecipeBomTreeResult> BuildTreeAsync(int rootRecipeId, int maxDepth = DefaultMaxDepth)
        {
            if (maxDepth <= 0) maxDepth = DefaultMaxDepth;

            var result = new RecipeBomTreeResult
            {
                RootRecipeId = rootRecipeId,
                MaxDepth = maxDepth
            };

            // Level-batch load: collect all reachable recipe IDs up to maxDepth, then materialize details once.
            var recipeCache = new Dictionary<int, LoadedRecipe>();
            var pending = new HashSet<int> { rootRecipeId };
            var loaded = new HashSet<int>();

            for (var level = 0; level <= maxDepth && pending.Count > 0; level++)
            {
                var toLoad = pending.Where(id => !loaded.Contains(id)).ToList();
                if (toLoad.Count == 0) break;

                var batch = await _context.Recipes
                    .AsNoTracking()
                    .Where(r => toLoad.Contains(r.RecipeId))
                    .Select(r => new
                    {
                        r.RecipeId,
                        r.Name,
                        Details = r.RecipeDetails.Select(d => new
                        {
                            d.IngredientId,
                            IngredientName = d.Ingredient != null ? d.Ingredient.Name : null,
                            IngredientBaseUnitName = d.Ingredient != null && d.Ingredient.BaseUnit != null
                                ? d.Ingredient.BaseUnit.Name
                                : null,
                            d.ChildRecipeId,
                            ChildRecipeName = d.ChildRecipe != null ? d.ChildRecipe.Name : null,
                            d.Quantity,
                            UnitName = d.Unit != null ? d.Unit.Name : null
                        }).ToList()
                    })
                    .ToListAsync();

                var nextPending = new HashSet<int>();
                foreach (var row in batch)
                {
                    loaded.Add(row.RecipeId);
                    var details = row.Details.Select(d => new LoadedDetail
                    {
                        IngredientId = d.IngredientId,
                        IngredientName = d.IngredientName,
                        IngredientBaseUnitName = d.IngredientBaseUnitName,
                        ChildRecipeId = d.ChildRecipeId,
                        ChildRecipeName = d.ChildRecipeName,
                        Quantity = d.Quantity,
                        UnitName = d.UnitName
                    }).ToList();

                    recipeCache[row.RecipeId] = new LoadedRecipe
                    {
                        RecipeId = row.RecipeId,
                        Name = row.Name ?? "",
                        Details = details
                    };

                    if (level < maxDepth)
                    {
                        foreach (var d in details)
                        {
                            if (d.ChildRecipeId.HasValue && !loaded.Contains(d.ChildRecipeId.Value))
                                nextPending.Add(d.ChildRecipeId.Value);
                        }
                    }
                }

                // Missing ids → empty placeholders so tree can show N/A without re-query.
                foreach (var id in toLoad)
                {
                    if (!recipeCache.ContainsKey(id))
                    {
                        loaded.Add(id);
                        recipeCache[id] = new LoadedRecipe { RecipeId = id, Name = "", Details = new List<LoadedDetail>() };
                    }
                }

                pending = nextPending;
            }

            if (!recipeCache.TryGetValue(rootRecipeId, out var root) || string.IsNullOrEmpty(root.Name) && root.Details.Count == 0)
            {
                // Distinguish not found vs empty recipe: check existence
                var exists = await _context.Recipes.AsNoTracking().AnyAsync(r => r.RecipeId == rootRecipeId);
                if (!exists)
                {
                    result.RootNotFound = true;
                    return result;
                }
            }

            if (recipeCache.TryGetValue(rootRecipeId, out var rootRecipe))
                result.RootName = rootRecipe.Name;

            var path = new HashSet<int> { rootRecipeId };
            result.Roots = BuildNodes(rootRecipeId, recipeCache, depth: 0, maxDepth, path);
            return result;
        }

        private static List<RecipeBomTreeNodeVM> BuildNodes(
            int recipeId,
            IReadOnlyDictionary<int, LoadedRecipe> cache,
            int depth,
            int maxDepth,
            HashSet<int> path)
        {
            if (!cache.TryGetValue(recipeId, out var recipe))
                return new List<RecipeBomTreeNodeVM>();

            var nodes = new List<RecipeBomTreeNodeVM>();
            foreach (var detail in recipe.Details)
            {
                if (detail.IngredientId.HasValue)
                {
                    nodes.Add(new RecipeBomTreeNodeVM
                    {
                        Kind = RecipeBomTreeNodeKind.Ingredient,
                        IngredientId = detail.IngredientId,
                        DisplayName = detail.IngredientName ?? "N/A",
                        Quantity = detail.Quantity,
                        UnitName = detail.UnitName ?? detail.IngredientBaseUnitName ?? "",
                        Depth = depth
                    });
                    continue;
                }

                if (!detail.ChildRecipeId.HasValue)
                    continue;

                var childId = detail.ChildRecipeId.Value;
                var childName = cache.TryGetValue(childId, out var child)
                    ? (string.IsNullOrEmpty(child.Name) ? (detail.ChildRecipeName ?? "N/A") : child.Name)
                    : (detail.ChildRecipeName ?? "N/A");

                var node = new RecipeBomTreeNodeVM
                {
                    Kind = RecipeBomTreeNodeKind.ChildRecipe,
                    ChildRecipeId = childId,
                    DisplayName = childName,
                    Quantity = detail.Quantity,
                    UnitName = detail.UnitName ?? "Phần",
                    Depth = depth
                };

                if (depth + 1 > maxDepth)
                {
                    node.Children.Add(new RecipeBomTreeNodeVM
                    {
                        Kind = RecipeBomTreeNodeKind.DepthLimit,
                        DisplayName = "",
                        Depth = depth + 1,
                        Message = $"Đã đạt giới hạn {maxDepth} tầng hiển thị."
                    });
                }
                else if (path.Contains(childId))
                {
                    node.Children.Add(new RecipeBomTreeNodeVM
                    {
                        Kind = RecipeBomTreeNodeKind.CycleDetected,
                        ChildRecipeId = childId,
                        DisplayName = childName,
                        Depth = depth + 1,
                        Message = $"Phát hiện chu trình BOM tại Recipe #{childId} — dừng mở rộng."
                    });
                }
                else
                {
                    path.Add(childId);
                    node.Children = BuildNodes(childId, cache, depth + 1, maxDepth, path);
                    path.Remove(childId);
                }

                nodes.Add(node);
            }

            return nodes;
        }

        private sealed class LoadedRecipe
        {
            public int RecipeId { get; set; }
            public string Name { get; set; } = "";
            public List<LoadedDetail> Details { get; set; } = new();
        }

        private sealed class LoadedDetail
        {
            public int? IngredientId { get; set; }
            public string? IngredientName { get; set; }
            public string? IngredientBaseUnitName { get; set; }
            public int? ChildRecipeId { get; set; }
            public string? ChildRecipeName { get; set; }
            public decimal Quantity { get; set; }
            public string? UnitName { get; set; }
        }
    }
}
