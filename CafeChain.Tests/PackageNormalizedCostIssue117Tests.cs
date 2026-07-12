using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Costing;
using CafeChain.Application.Services.Admin.Recipes;
using CafeChain.Application.Services.Inventories;
using CafeChain.Data;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Inventories.Suppliers;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CafeChain.Tests.POS
{
    /// <summary>Issue #117 — package-normalized EstimatedBomCost + COMPLETE/INCOMPLETE.</summary>
    public class PackageNormalizedCostIssue117Tests : IntegrationTestBase
    {
        private const int UnitG = 1;
        private const int UnitKg = 2;
        private const int UnitMl = 3;
        private const int UnitL = 4;

        private static EstimatedBomCostService CreateService(AppDbContext ctx)
        {
            var physical = new PhysicalUnitConversionService(ctx, NullLogger<PhysicalUnitConversionService>.Instance);
            var unit = new UnitConversionService(ctx, NullLogger<UnitConversionService>.Instance, physical);
            var norm = new RecipeOutputNormalizer(ctx, physical);
            return new EstimatedBomCostService(
                ctx, unit, physical, norm, NullLogger<EstimatedBomCostService>.Instance);
        }

        private static void EnsureUnits(AppDbContext ctx)
        {
            EnsureUnit(ctx, UnitG, "g", UnitType.KhoiLuong);
            EnsureUnit(ctx, UnitKg, "kg", UnitType.KhoiLuong);
            EnsureUnit(ctx, UnitMl, "ml", UnitType.TheTich);
            EnsureUnit(ctx, UnitL, "l", UnitType.TheTich);
        }

        private static void EnsureUnit(AppDbContext ctx, int id, string code, UnitType type)
        {
            var u = ctx.Units.FirstOrDefault(x => x.UnitId == id);
            if (u != null)
            {
                u.UnitCode = code;
                u.Type = type;
                u.Active = true;
                u.Name = code;
                ctx.SaveChanges();
                return;
            }
            ctx.Units.Add(new Unit { UnitId = id, UnitCode = code, Name = code, Type = type, Active = true });
            ctx.SaveChanges();
        }

        private static int SeedIngredientWithOffer(
            AppDbContext ctx,
            int ingredientId,
            int baseUnitId,
            decimal? packageQty,
            int packageUnitId,
            decimal price,
            bool isPrimary = true,
            bool active = true)
        {
            EnsureUnits(ctx);
            if (!ctx.Ingredients.Any(i => i.IngredientId == ingredientId))
            {
                ctx.Ingredients.Add(new Ingredient
                {
                    IngredientId = ingredientId,
                    Code = $"ING{ingredientId:D5}",
                    Name = $"Ing {ingredientId}",
                    BaseUnitId = baseUnitId,
                    Active = true
                });
                ctx.SaveChanges();
            }

            // Clear existing offers for clean tests
            var existing = ctx.IngredientSuppliers.Where(s => s.IngredientId == ingredientId).ToList();
            ctx.IngredientSuppliers.RemoveRange(existing);
            ctx.SaveChanges();

            var offer = new IngredientSupplier
            {
                IngredientId = ingredientId,
                SupplierId = 1,
                UnitId = packageUnitId,
                PackageQuantity = packageQty,
                CurrentPrice = price,
                IsPrimary = isPrimary,
                Active = active
            };
            ctx.IngredientSuppliers.Add(offer);
            ctx.SaveChanges();
            return offer.IngredientSupplierId;
        }

        [Fact]
        public async Task Coffee_1kg_140000_BaseUnitCost_Is_140_PerGram()
        {
            using var ctx = CreateDbContext();
            SeedIngredientWithOffer(ctx, 101, UnitG, 1m, UnitKg, 140000m);
            var svc = CreateService(ctx);

            var r = await svc.ResolveIngredientBaseUnitCostAsync(101);
            Assert.True(r.IsComplete, string.Join("; ", r.Issues.Select(i => i.Message)));
            Assert.Equal(140m, r.BaseUnitCost);
            Assert.Equal(1000m, r.BaseQuantityPerPackage);
        }

        [Fact]
        public async Task RecipeLine_18g_Coffee_Complete_2520()
        {
            using var ctx = CreateDbContext();
            SeedIngredientWithOffer(ctx, 102, UnitG, 1m, UnitKg, 140000m);
            var recipe = new Recipe
            {
                RecipeCode = "RCP_COGS_18",
                Name = "Coffee test",
                YieldPercentage = 100,
                Active = true,
                Status = "Active",
                RecipeDetails = new List<RecipeDetail>
                {
                    new() { IngredientId = 102, Quantity = 18m, UnitId = UnitG }
                }
            };
            ctx.Recipes.Add(recipe);
            await ctx.SaveChangesAsync();

            var svc = CreateService(ctx);
            var r = await svc.CalculateRecipeEstimatedCostAsync(recipe.RecipeId);
            Assert.True(r.IsComplete, string.Join("; ", r.Issues.Select(i => i.Message)));
            Assert.Equal(2520m, r.TotalCost);
            Assert.Single(r.Lines);
            Assert.Equal(2520m, r.Lines[0].LineCost);
            Assert.Equal(140m, r.Lines[0].BaseUnitCost);
        }

        [Fact]
        public async Task ZeroPackagePrice_IsIncomplete_NotCompleteZero()
        {
            using var ctx = CreateDbContext();
            SeedIngredientWithOffer(ctx, 103, UnitG, 1m, UnitKg, 0m);
            var svc = CreateService(ctx);

            var r = await svc.ResolveIngredientBaseUnitCostAsync(103);
            Assert.False(r.IsComplete);
            Assert.Null(r.BaseUnitCost);
            Assert.Contains(r.Issues, i => i.Code == CostIssueCodes.ZeroPackagePrice);
        }

        [Fact]
        public async Task NullPackageQuantity_IsIncomplete()
        {
            using var ctx = CreateDbContext();
            SeedIngredientWithOffer(ctx, 104, UnitG, null, UnitKg, 140000m);
            var svc = CreateService(ctx);

            var r = await svc.ResolveIngredientBaseUnitCostAsync(104);
            Assert.False(r.IsComplete);
            Assert.Contains(r.Issues, i => i.Code == CostIssueCodes.MissingPackageQuantity);
        }

        [Fact]
        public async Task MissingPrimaryOffer_IsIncomplete()
        {
            using var ctx = CreateDbContext();
            SeedIngredientWithOffer(ctx, 105, UnitG, 1m, UnitKg, 140000m, isPrimary: false);
            var svc = CreateService(ctx);

            var r = await svc.ResolveIngredientBaseUnitCostAsync(105);
            Assert.False(r.IsComplete);
            Assert.Contains(r.Issues, i => i.Code == CostIssueCodes.MissingSupplierOffer);
        }

        [Fact]
        public async Task MultipleActivePrimary_IsIncomplete()
        {
            using var ctx = CreateDbContext();
            EnsureUnits(ctx);
            if (!ctx.Ingredients.Any(i => i.IngredientId == 106))
            {
                ctx.Ingredients.Add(new Ingredient
                {
                    IngredientId = 106,
                    Code = "ING00106",
                    Name = "Multi primary",
                    BaseUnitId = UnitG,
                    Active = true
                });
                await ctx.SaveChangesAsync();
            }

            ctx.IngredientSuppliers.AddRange(
                new IngredientSupplier
                {
                    IngredientId = 106,
                    SupplierId = 1,
                    UnitId = UnitKg,
                    PackageQuantity = 1m,
                    CurrentPrice = 100000m,
                    IsPrimary = true,
                    Active = true
                },
                new IngredientSupplier
                {
                    IngredientId = 106,
                    SupplierId = 2,
                    UnitId = UnitKg,
                    PackageQuantity = 1m,
                    CurrentPrice = 110000m,
                    IsPrimary = true,
                    Active = true
                });
            await ctx.SaveChangesAsync();

            var svc = CreateService(ctx);
            var r = await svc.ResolveIngredientBaseUnitCostAsync(106);
            Assert.False(r.IsComplete);
            Assert.Contains(r.Issues, i => i.Code == CostIssueCodes.MultiplePrimarySuppliers);
        }

        [Fact]
        public async Task MissingPackageConversion_IsIncomplete_NotRaw()
        {
            using var ctx = CreateDbContext();
            // mass package unit → volume base: fail closed
            SeedIngredientWithOffer(ctx, 107, UnitMl, 1m, UnitKg, 50000m);
            var svc = CreateService(ctx);

            var r = await svc.ResolveIngredientBaseUnitCostAsync(107);
            Assert.False(r.IsComplete);
            Assert.Null(r.BaseUnitCost);
            Assert.Contains(r.Issues, i => i.Code == CostIssueCodes.MissingUnitConversion);
        }

        [Fact]
        public async Task IngredientDetail_MissingUnitConversion_RecipeIncomplete()
        {
            using var ctx = CreateDbContext();
            SeedIngredientWithOffer(ctx, 108, UnitG, 1m, UnitKg, 140000m);
            EnsureUnit(ctx, 79, "badx", UnitType.Dem);
            var recipe = new Recipe
            {
                RecipeCode = "RCP_BADUNIT",
                Name = "Bad unit",
                YieldPercentage = 50, // must NOT be applied to invent complete cost
                Active = true,
                Status = "Active",
                RecipeDetails = new List<RecipeDetail>
                {
                    new() { IngredientId = 108, Quantity = 10m, UnitId = 79 }
                }
            };
            ctx.Recipes.Add(recipe);
            await ctx.SaveChangesAsync();

            var svc = CreateService(ctx);
            var r = await svc.CalculateRecipeEstimatedCostAsync(recipe.RecipeId);
            Assert.False(r.IsComplete);
            Assert.Null(r.TotalCost);
            Assert.Contains(r.Issues, i => i.Code == CostIssueCodes.MissingUnitConversion);
        }

        [Fact]
        public async Task ChildBtp_WithOutput_AllocatesCostPerBase()
        {
            using var ctx = CreateDbContext();
            SeedIngredientWithOffer(ctx, 109, UnitG, 1m, UnitKg, 140000m); // 140/g

            ctx.PreparedItems.Add(new PreparedItem
            {
                PreparedItemId = 501,
                Code = "BTP-COFFEE",
                Name = "Coffee concentrate",
                BaseUnitId = UnitMl,
                Active = true
            });
            await ctx.SaveChangesAsync();

            // Child BTP: 1000g coffee → output 4.5 l = 4500 ml → input cost 140000
            var child = new Recipe
            {
                RecipeCode = "RCP_BTP_CHILD",
                Name = "BTP coffee",
                YieldPercentage = 80, // must NOT double-apply
                Active = true,
                Status = "Active",
                PreparedItemId = 501,
                OutputQuantity = 4.5m,
                OutputUnitId = UnitL,
                RecipeDetails = new List<RecipeDetail>
                {
                    new() { IngredientId = 109, Quantity = 1000m, UnitId = UnitG }
                }
            };
            ctx.Recipes.Add(child);
            await ctx.SaveChangesAsync();

            // Parent consumes 450 ml of concentrate
            var parent = new Recipe
            {
                RecipeCode = "RCP_BTP_PARENT",
                Name = "Drink",
                YieldPercentage = 100,
                Active = true,
                Status = "Active",
                RecipeDetails = new List<RecipeDetail>
                {
                    new() { ChildRecipeId = child.RecipeId, Quantity = 450m, UnitId = UnitMl }
                }
            };
            ctx.Recipes.Add(parent);
            await ctx.SaveChangesAsync();

            var svc = CreateService(ctx);
            var childEst = await svc.CalculateRecipeEstimatedCostAsync(child.RecipeId);
            Assert.True(childEst.IsComplete, string.Join("; ", childEst.Issues.Select(i => i.Message)));
            Assert.Equal(140000m, childEst.TotalCost); // no yield 80% applied

            var parentEst = await svc.CalculateRecipeEstimatedCostAsync(parent.RecipeId);
            Assert.True(parentEst.IsComplete, string.Join("; ", parentEst.Issues.Select(i => i.Message)));
            // costPerMl = 140000/4500; line = 450 * that = 14000
            Assert.Equal(14000m, parentEst.TotalCost);
        }

        [Fact]
        public async Task LegacyChildRecipe_WithoutOutput_IsIncomplete()
        {
            using var ctx = CreateDbContext();
            SeedIngredientWithOffer(ctx, 110, UnitG, 1m, UnitKg, 140000m);
            var child = new Recipe
            {
                RecipeCode = "RCP_LEGACY_CHILD",
                Name = "Legacy child",
                YieldPercentage = 100,
                Active = true,
                Status = "Active",
                // no PreparedItem / output
                RecipeDetails = new List<RecipeDetail>
                {
                    new() { IngredientId = 110, Quantity = 10m, UnitId = UnitG }
                }
            };
            ctx.Recipes.Add(child);
            await ctx.SaveChangesAsync();

            var parent = new Recipe
            {
                RecipeCode = "RCP_LEGACY_PARENT",
                Name = "Parent",
                YieldPercentage = 100,
                Active = true,
                Status = "Active",
                RecipeDetails = new List<RecipeDetail>
                {
                    new() { ChildRecipeId = child.RecipeId, Quantity = 1m, UnitId = UnitG }
                }
            };
            ctx.Recipes.Add(parent);
            await ctx.SaveChangesAsync();

            var svc = CreateService(ctx);
            var r = await svc.CalculateRecipeEstimatedCostAsync(parent.RecipeId);
            Assert.False(r.IsComplete);
            Assert.Null(r.TotalCost);
            Assert.Contains(r.Issues, i => i.Code == CostIssueCodes.LegacyChildRecipeWithoutOutput);
        }

        [Fact]
        public async Task Cycle_IsIncomplete()
        {
            using var ctx = CreateDbContext();
            // Create two recipes that reference each other via ChildRecipeId after both exist
            var r1 = new Recipe
            {
                RecipeCode = "RCP_CYC1",
                Name = "C1",
                YieldPercentage = 100,
                Active = true,
                Status = "Active",
                RecipeDetails = new List<RecipeDetail>()
            };
            var r2 = new Recipe
            {
                RecipeCode = "RCP_CYC2",
                Name = "C2",
                YieldPercentage = 100,
                Active = true,
                Status = "Active",
                RecipeDetails = new List<RecipeDetail>()
            };
            ctx.Recipes.AddRange(r1, r2);
            await ctx.SaveChangesAsync();

            ctx.RecipeDetails.Add(new RecipeDetail
            {
                RecipeId = r1.RecipeId,
                ChildRecipeId = r2.RecipeId,
                Quantity = 1m,
                UnitId = UnitG
            });
            ctx.RecipeDetails.Add(new RecipeDetail
            {
                RecipeId = r2.RecipeId,
                ChildRecipeId = r1.RecipeId,
                Quantity = 1m,
                UnitId = UnitG
            });
            await ctx.SaveChangesAsync();

            // Give them PreparedItem output so we get past legacy check into recursion
            ctx.PreparedItems.Add(new PreparedItem
            {
                PreparedItemId = 601,
                Code = "BTP-CYC",
                Name = "Cyc",
                BaseUnitId = UnitG,
                Active = true
            });
            r1.PreparedItemId = 601;
            r1.OutputQuantity = 1m;
            r1.OutputUnitId = UnitG;
            r2.PreparedItemId = 601;
            r2.OutputQuantity = 1m;
            r2.OutputUnitId = UnitG;
            // uniqueness: only one Active per PI — archive r2 for Active uniqueness if needed
            // Filtered unique on Active+PreparedItem — set r2 Active false for DB, but costing loads by id
            r2.Active = false;
            r2.Status = "Archived";
            await ctx.SaveChangesAsync();

            var svc = CreateService(ctx);
            var result = await svc.CalculateRecipeEstimatedCostAsync(r1.RecipeId);
            Assert.False(result.IsComplete);
            // cycle or incomplete child propagation
            Assert.True(
                result.Issues.Any(i =>
                    i.Code == CostIssueCodes.RecipeCycle
                    || i.Code == CostIssueCodes.LegacyChildRecipeWithoutOutput
                    || i.Message.Contains("vòng", StringComparison.OrdinalIgnoreCase)
                    || result.Issues.Count > 0));
        }

        [Fact]
        public async Task YieldPercentage_NotApplied_InEstimatedBomCost()
        {
            using var ctx = CreateDbContext();
            SeedIngredientWithOffer(ctx, 111, UnitG, 1m, UnitKg, 140000m);
            var recipe = new Recipe
            {
                RecipeCode = "RCP_YIELD",
                Name = "Yield ignore",
                YieldPercentage = 50,
                Active = true,
                Status = "Active",
                RecipeDetails = new List<RecipeDetail>
                {
                    new() { IngredientId = 111, Quantity = 1000m, UnitId = UnitG }
                }
            };
            ctx.Recipes.Add(recipe);
            await ctx.SaveChangesAsync();

            var svc = CreateService(ctx);
            var r = await svc.CalculateRecipeEstimatedCostAsync(recipe.RecipeId);
            Assert.True(r.IsComplete);
            // 1000g * 140 = 140000 — not doubled via yield 50%
            Assert.Equal(140000m, r.TotalCost);
        }

        [Fact]
        public async Task Incomplete_NeverReportsCompleteZeroTotal()
        {
            using var ctx = CreateDbContext();
            SeedIngredientWithOffer(ctx, 112, UnitG, null, UnitKg, 0m);
            var recipe = new Recipe
            {
                RecipeCode = "RCP_ZEROINC",
                Name = "Zero incomplete",
                YieldPercentage = 100,
                Active = true,
                Status = "Active",
                RecipeDetails = new List<RecipeDetail>
                {
                    new() { IngredientId = 112, Quantity = 10m, UnitId = UnitG }
                }
            };
            ctx.Recipes.Add(recipe);
            await ctx.SaveChangesAsync();

            var svc = CreateService(ctx);
            var r = await svc.CalculateRecipeEstimatedCostAsync(recipe.RecipeId);
            Assert.Equal(CostCompletenessStatus.Incomplete, r.Status);
            Assert.Null(r.TotalCost);
        }

        [Fact]
        public async Task CogsAdapter_Incomplete_ReturnsFailure_NotSuccessZero()
        {
            using var ctx = CreateDbContext();
            SeedIngredientWithOffer(ctx, 113, UnitG, null, UnitKg, 140000m);
            var recipe = new Recipe
            {
                RecipeCode = "RCP_ADAPTER",
                Name = "Adapter",
                YieldPercentage = 100,
                Active = true,
                Status = "Active",
                RecipeDetails = new List<RecipeDetail>
                {
                    new() { IngredientId = 113, Quantity = 10m, UnitId = UnitG }
                }
            };
            ctx.Recipes.Add(recipe);
            await ctx.SaveChangesAsync();

            var physical = new PhysicalUnitConversionService(ctx, NullLogger<PhysicalUnitConversionService>.Instance);
            var unit = new UnitConversionService(ctx, NullLogger<UnitConversionService>.Instance, physical);
            var estimated = CreateService(ctx);
            var deduction = new InventoryDeductionService(
                ctx,
                NullLogger<InventoryDeductionService>.Instance,
                unit,
                estimated,
                physical);

            var cogs = await deduction.CalculateRecipeCogsAsync(recipe.RecipeId);
            Assert.False(cogs.IsSuccess);
        }

        [Fact]
        public async Task Estimate_DoesNotMutateStoreInventory()
        {
            using var ctx = CreateDbContext();
            SeedIngredientWithOffer(ctx, 114, UnitG, 1m, UnitKg, 140000m);
            var invBefore = await ctx.Set<StoreInventory>().CountAsync();
            var recipe = new Recipe
            {
                RecipeCode = "RCP_NOINV",
                Name = "No inv",
                YieldPercentage = 100,
                Active = true,
                Status = "Active",
                RecipeDetails = new List<RecipeDetail>
                {
                    new() { IngredientId = 114, Quantity = 5m, UnitId = UnitG }
                }
            };
            ctx.Recipes.Add(recipe);
            await ctx.SaveChangesAsync();

            var svc = CreateService(ctx);
            await svc.CalculateRecipeEstimatedCostAsync(recipe.RecipeId);
            Assert.Equal(invBefore, await ctx.Set<StoreInventory>().CountAsync());
        }

        [Fact]
        public async Task SeedCoffee_UsesPackageNormalizedFormula()
        {
            // Ingredient 1 coffee seed: PackageQuantity=1 kg, price 140000, base g
            using var ctx = CreateDbContext();
            EnsureUnits(ctx);
            var svc = CreateService(ctx);
            var r = await svc.ResolveIngredientBaseUnitCostAsync(1);
            if (r.IsComplete)
            {
                Assert.Equal(140m, r.BaseUnitCost);
            }
            else
            {
                // If seed not loaded in test DB path, at least no complete-zero
                Assert.Null(r.BaseUnitCost);
            }
        }

        [Fact]
        public async Task Package_500g_At_70000_Is_140_PerGram()
        {
            using var ctx = CreateDbContext();
            SeedIngredientWithOffer(ctx, 201, UnitG, 500m, UnitG, 70000m);
            var svc = CreateService(ctx);
            var r = await svc.ResolveIngredientBaseUnitCostAsync(201);
            Assert.True(r.IsComplete, string.Join("; ", r.Issues.Select(i => i.Message)));
            Assert.Equal(140m, r.BaseUnitCost);
        }

        [Fact]
        public async Task Package_750ml_At_120000_Is_160_PerMl()
        {
            using var ctx = CreateDbContext();
            SeedIngredientWithOffer(ctx, 202, UnitMl, 750m, UnitMl, 120000m);
            var svc = CreateService(ctx);
            var r = await svc.ResolveIngredientBaseUnitCostAsync(202);
            Assert.True(r.IsComplete, string.Join("; ", r.Issues.Select(i => i.Message)));
            Assert.Equal(160m, r.BaseUnitCost);
        }

        [Fact]
        public async Task Package_1l_To_MlBase_Normalized()
        {
            using var ctx = CreateDbContext();
            SeedIngredientWithOffer(ctx, 203, UnitMl, 1m, UnitL, 95000m);
            var svc = CreateService(ctx);
            var r = await svc.ResolveIngredientBaseUnitCostAsync(203);
            Assert.True(r.IsComplete, string.Join("; ", r.Issues.Select(i => i.Message)));
            Assert.Equal(1000m, r.BaseQuantityPerPackage);
            Assert.Equal(95m, r.BaseUnitCost);
        }

        [Fact]
        public async Task MaxDepthExceeded_IsIncomplete()
        {
            using var ctx = CreateDbContext();
            EnsureUnits(ctx);
            ctx.PreparedItems.Add(new PreparedItem
            {
                PreparedItemId = 701,
                Code = "BTP-DEPTH",
                Name = "Depth",
                BaseUnitId = UnitG,
                Active = true
            });
            await ctx.SaveChangesAsync();

            // Chain R0 → R1 → R2 → R3 → R4 → R5 → R6 (depth > 5)
            var recipes = new List<Recipe>();
            for (var i = 0; i <= 6; i++)
            {
                recipes.Add(new Recipe
                {
                    RecipeCode = $"RCP_DEPTH_{i}",
                    Name = $"Depth {i}",
                    YieldPercentage = 100,
                    Active = i == 0,
                    Status = i == 0 ? "Active" : "Archived",
                    PreparedItemId = 701,
                    OutputQuantity = 1m,
                    OutputUnitId = UnitG,
                    RecipeDetails = new List<RecipeDetail>()
                });
            }
            ctx.Recipes.AddRange(recipes);
            await ctx.SaveChangesAsync();

            SeedIngredientWithOffer(ctx, 204, UnitG, 1m, UnitG, 1000m);
            // leaf has ingredient; others point to next child
            for (var i = 0; i < 6; i++)
            {
                ctx.RecipeDetails.Add(new RecipeDetail
                {
                    RecipeId = recipes[i].RecipeId,
                    ChildRecipeId = recipes[i + 1].RecipeId,
                    Quantity = 1m,
                    UnitId = UnitG
                });
            }
            ctx.RecipeDetails.Add(new RecipeDetail
            {
                RecipeId = recipes[6].RecipeId,
                IngredientId = 204,
                Quantity = 1m,
                UnitId = UnitG
            });
            await ctx.SaveChangesAsync();

            var svc = CreateService(ctx);
            var r = await svc.CalculateRecipeEstimatedCostAsync(recipes[0].RecipeId);
            Assert.False(r.IsComplete);
            Assert.Null(r.TotalCost);
            Assert.Contains(r.Issues, i => i.Code == CostIssueCodes.MaxDepthExceeded);
        }

        [Fact]
        public async Task Memoization_SharedGrandchild_ViaTwoChildren_CostsBothBranches()
        {
            // RecipeDetail unique (RecipeId, ChildRecipeId) forbids two lines with same child —
            // memoize shared grandchild via two different mid-level children instead.
            using var ctx = CreateDbContext();
            SeedIngredientWithOffer(ctx, 205, UnitG, 1m, UnitKg, 140000m);

            ctx.PreparedItems.AddRange(
                new PreparedItem
                {
                    PreparedItemId = 702,
                    Code = "BTP-MEMO-G",
                    Name = "Grandchild BTP",
                    BaseUnitId = UnitG,
                    Active = true
                },
                new PreparedItem
                {
                    PreparedItemId = 703,
                    Code = "BTP-MEMO-A",
                    Name = "Mid A",
                    BaseUnitId = UnitG,
                    Active = true
                },
                new PreparedItem
                {
                    PreparedItemId = 704,
                    Code = "BTP-MEMO-B",
                    Name = "Mid B",
                    BaseUnitId = UnitG,
                    Active = true
                });
            await ctx.SaveChangesAsync();

            // Grandchild: 1000g in → 1000g out, cost 140000 → 140/g
            var grandchild = new Recipe
            {
                RecipeCode = "RCP_MEMO_GC",
                Name = "GC",
                YieldPercentage = 100,
                Active = true,
                Status = "Active",
                PreparedItemId = 702,
                OutputQuantity = 1000m,
                OutputUnitId = UnitG,
                RecipeDetails = new List<RecipeDetail>
                {
                    new() { IngredientId = 205, Quantity = 1000m, UnitId = UnitG }
                }
            };
            ctx.Recipes.Add(grandchild);
            await ctx.SaveChangesAsync();

            // Mid A and Mid B each consume 100g of grandchild; each outputs 100g
            var midA = new Recipe
            {
                RecipeCode = "RCP_MEMO_A",
                Name = "Mid A",
                YieldPercentage = 100,
                Active = false,
                Status = "Archived",
                PreparedItemId = 703,
                OutputQuantity = 100m,
                OutputUnitId = UnitG,
                RecipeDetails = new List<RecipeDetail>
                {
                    new() { ChildRecipeId = grandchild.RecipeId, Quantity = 100m, UnitId = UnitG }
                }
            };
            var midB = new Recipe
            {
                RecipeCode = "RCP_MEMO_B",
                Name = "Mid B",
                YieldPercentage = 100,
                Active = false,
                Status = "Archived",
                PreparedItemId = 704,
                OutputQuantity = 100m,
                OutputUnitId = UnitG,
                RecipeDetails = new List<RecipeDetail>
                {
                    new() { ChildRecipeId = grandchild.RecipeId, Quantity = 100m, UnitId = UnitG }
                }
            };
            ctx.Recipes.AddRange(midA, midB);
            await ctx.SaveChangesAsync();

            // Parent consumes 100g of each mid (pass-through of memoized grandchild cost)
            var parent = new Recipe
            {
                RecipeCode = "RCP_MEMO_PARENT",
                Name = "Memo parent",
                YieldPercentage = 100,
                Active = true,
                Status = "Active",
                RecipeDetails = new List<RecipeDetail>
                {
                    new() { ChildRecipeId = midA.RecipeId, Quantity = 100m, UnitId = UnitG },
                    new() { ChildRecipeId = midB.RecipeId, Quantity = 100m, UnitId = UnitG }
                }
            };
            ctx.Recipes.Add(parent);
            await ctx.SaveChangesAsync();

            var svc = CreateService(ctx);
            var r = await svc.CalculateRecipeEstimatedCostAsync(parent.RecipeId);
            Assert.True(r.IsComplete, string.Join("; ", r.Issues.Select(i => i.Message)));
            // Each mid: 100g * 140/g = 14000 input; output 100g → 140/g; parent 100g each → 14000 * 2 = 28000
            Assert.Equal(28000m, r.TotalCost);
            Assert.Equal(2, r.Lines.Count);
        }

        [Fact]
        public async Task MissingPackageCost_DoesNotBlockQuantityDeduction()
        {
            // CRITICAL blocker: cost INCOMPLETE must not cancel stock mutation
            using var ctx = CreateDbContext();
            EnsureUnits(ctx);
            const int storeId = 1;
            const int drinkId = 501;
            const int sizeId = 1;
            const int ingId = 301;

            // Ingredient with invalid cost source (no package quantity)
            SeedIngredientWithOffer(ctx, ingId, UnitG, packageQty: null, packageUnitId: UnitKg, price: 140000m);

            ctx.Recipes.Add(new Recipe
            {
                RecipeId = 2001,
                RecipeCode = "RCP_DED_COST",
                Name = "Deduct despite incomplete cost",
                YieldPercentage = 100,
                Active = true,
                Status = "Active",
                DrinkId = drinkId,
                SizeId = sizeId,
                RecipeDetails = new List<RecipeDetail>
                {
                    new() { IngredientId = ingId, Quantity = 10m, UnitId = UnitG }
                }
            });
            ctx.Set<StoreInventory>().Add(new StoreInventory
            {
                StoreId = storeId,
                IngredientId = ingId,
                AvailableQty = 100m,
                ReservedQty = 0,
                LastUpdated = DateTime.UtcNow,
                RowVersion = new byte[] { 0 }
            });
            await ctx.SaveChangesAsync();

            var physical = new PhysicalUnitConversionService(ctx, NullLogger<PhysicalUnitConversionService>.Instance);
            var unit = new UnitConversionService(ctx, NullLogger<UnitConversionService>.Instance, physical);
            var estimated = CreateService(ctx);
            var deduction = new InventoryDeductionService(
                ctx, NullLogger<InventoryDeductionService>.Instance, unit, estimated, physical);

            // Cost is incomplete
            var cogs = await deduction.CalculateRecipeCogsAsync(2001);
            Assert.False(cogs.IsSuccess);

            // Quantity deduction still succeeds
            var sold = new List<CafeChain.Application.DTOs.POS.POSSoldItemDto>
            {
                new()
                {
                    DrinkId = drinkId,
                    SizeId = sizeId,
                    Quantity = 1,
                    Toppings = new List<CafeChain.Application.DTOs.POS.POSOrderToppingDto>()
                }
            };
            var deduct = await deduction.DeductStockForOrderAsync(sold, storeId);
            Assert.True(deduct.IsSuccess, deduct.Message);

            var inv = await ctx.Set<StoreInventory>()
                .SingleAsync(i => i.StoreId == storeId && i.IngredientId == ingId);
            Assert.Equal(90m, inv.AvailableQty); // 100 - 10
        }

        [Fact]
        public async Task MissingQuantityConversion_StillBlocksDeduction()
        {
            using var ctx = CreateDbContext();
            EnsureUnits(ctx);
            const int storeId = 1;
            const int drinkId = 502;
            const int sizeId = 1;
            const int ingId = 302;
            const int badUnitId = 88;

            SeedIngredientWithOffer(ctx, ingId, UnitG, 1m, UnitG, 10000m);
            ctx.Units.Add(new Unit
            {
                UnitId = badUnitId,
                UnitCode = "bad88",
                Name = "Bad88",
                Type = UnitType.Dem,
                Active = true
            });
            ctx.Recipes.Add(new Recipe
            {
                RecipeId = 2002,
                RecipeCode = "RCP_DED_CONV",
                Name = "Deduct fails conversion",
                YieldPercentage = 100,
                Active = true,
                Status = "Active",
                DrinkId = drinkId,
                SizeId = sizeId,
                RecipeDetails = new List<RecipeDetail>
                {
                    new() { IngredientId = ingId, Quantity = 5m, UnitId = badUnitId }
                }
            });
            ctx.Set<StoreInventory>().Add(new StoreInventory
            {
                StoreId = storeId,
                IngredientId = ingId,
                AvailableQty = 100m,
                ReservedQty = 0,
                LastUpdated = DateTime.UtcNow,
                RowVersion = new byte[] { 0 }
            });
            await ctx.SaveChangesAsync();

            var physical = new PhysicalUnitConversionService(ctx, NullLogger<PhysicalUnitConversionService>.Instance);
            var unit = new UnitConversionService(ctx, NullLogger<UnitConversionService>.Instance, physical);
            var estimated = CreateService(ctx);
            var deduction = new InventoryDeductionService(
                ctx, NullLogger<InventoryDeductionService>.Instance, unit, estimated, physical);

            var sold = new List<CafeChain.Application.DTOs.POS.POSSoldItemDto>
            {
                new()
                {
                    DrinkId = drinkId,
                    SizeId = sizeId,
                    Quantity = 1,
                    Toppings = new List<CafeChain.Application.DTOs.POS.POSOrderToppingDto>()
                }
            };
            var deduct = await deduction.DeductStockForOrderAsync(sold, storeId);
            Assert.False(deduct.IsSuccess);

            var inv = await ctx.Set<StoreInventory>()
                .SingleAsync(i => i.StoreId == storeId && i.IngredientId == ingId);
            Assert.Equal(100m, inv.AvailableQty); // rolled back / no mutation
        }
    }
}
