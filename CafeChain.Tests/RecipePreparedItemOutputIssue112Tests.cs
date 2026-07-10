using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CafeChain.Application.Interfaces.Admin.Recipes;
using CafeChain.Application.Services.Admin.PreparedItems;
using CafeChain.Application.Services.Admin.Recipes;
using CafeChain.Application.Services.Inventories;
using CafeChain.Application.DTOs.Admin.PreparedItems;
using CafeChain.Areas.Admin.Controllers;
using CafeChain.Data;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Stores;
using CafeChain.ViewModels.Admin.Recipes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CafeChain.Tests.POS
{
    /// <summary>Issue #112 — Recipe PreparedItem output model (no inventory/COGS cutover).</summary>
    public class RecipePreparedItemOutputIssue112Tests : IntegrationTestBase
    {
        private const int UnitG = 1;
        private const int UnitKg = 2;
        private const int UnitMl = 3;
        private const int UnitL = 4;
        private const int UnitPcs = 9;
        private const int UnitBottle = 10;
        private const int UnitCan = 11;
        private const int UnitPack = 12;

        private static AdminRecipeService CreateRecipeService(AppDbContext ctx)
        {
            var physical = new PhysicalUnitConversionService(ctx, NullLogger<PhysicalUnitConversionService>.Instance);
            var normalizer = new RecipeOutputNormalizer(ctx, physical);
            return new AdminRecipeService(ctx, normalizer);
        }

        private static IRecipeOutputNormalizer CreateNormalizer(AppDbContext ctx)
        {
            var physical = new PhysicalUnitConversionService(ctx, NullLogger<PhysicalUnitConversionService>.Instance);
            return new RecipeOutputNormalizer(ctx, physical);
        }

        private static List<RecipeDetailVM> OneIngredientDetail(int ingredientId = 1, int unitId = UnitG, decimal qty = 10m)
            => new()
            {
                new RecipeDetailVM
                {
                    ItemCode = $"ING_{ingredientId}",
                    Quantity = qty,
                    UnitId = unitId,
                    YieldPercentage = 100
                }
            };

        private async Task<int> CreateActivePreparedItemAsync(AppDbContext ctx, string code, string name, int baseUnitId)
        {
            EnsureUnits(ctx);
            var svc = new AdminPreparedItemService(ctx);
            return await svc.CreateAsync(new AdminPreparedItemSaveDTO
            {
                Code = code,
                Name = name,
                BaseUnitId = baseUnitId
            });
        }

        private static RecipeCreateVM BtpVm(int preparedItemId, decimal qty, int outputUnitId)
            => new()
            {
                RecipeType = "SUBRECIPE",
                PreparedItemId = preparedItemId,
                ExpectedYield = qty,
                OutputUnitId = outputUnitId,
                Active = true,
                EffectiveDate = DateTime.Today,
                Details = OneIngredientDetail()
            };

        [Fact]
        public async Task Btp_RequiresPreparedItem()
        {
            using var ctx = CreateDbContext();
            EnsureUnits(ctx);
            var svc = CreateRecipeService(ctx);
            var vm = BtpVm(0, 4.5m, UnitL);
            vm.PreparedItemId = null;

            var result = await svc.CreateRecipeAsync(vm);
            Assert.False(result.IsSuccess);
            Assert.Contains("Bán thành phẩm", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Btp_RequiresPositiveOutputQuantity()
        {
            using var ctx = CreateDbContext();
            var pi = await CreateActivePreparedItemAsync(ctx, "BTP-OUT1", "Cold brew", UnitMl);
            var svc = CreateRecipeService(ctx);
            var vm = BtpVm(pi, 0m, UnitL);

            var result = await svc.CreateRecipeAsync(vm);
            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task Btp_RequiresOutputUnit()
        {
            using var ctx = CreateDbContext();
            var pi = await CreateActivePreparedItemAsync(ctx, "BTP-OUT2", "Cold brew", UnitMl);
            var svc = CreateRecipeService(ctx);
            var vm = BtpVm(pi, 4.5m, 0);
            vm.OutputUnitId = null;

            var result = await svc.CreateRecipeAsync(vm);
            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task Drink_RejectsPreparedItemAndOutput()
        {
            using var ctx = CreateDbContext();
            EnsureUnits(ctx);
            var pi = await CreateActivePreparedItemAsync(ctx, "BTP-DRINK", "X", UnitMl);
            var svc = CreateRecipeService(ctx);

            var result = await svc.CreateRecipeAsync(new RecipeCreateVM
            {
                RecipeType = "POS",
                DrinkId = 1,
                SizeId = 1,
                PreparedItemId = pi,
                ExpectedYield = 1m,
                OutputUnitId = UnitL,
                Active = true,
                EffectiveDate = DateTime.Today,
                Details = OneIngredientDetail()
            });

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task Topping_RejectsPreparedItemAndOutput()
        {
            using var ctx = CreateDbContext();
            var pi = await CreateActivePreparedItemAsync(ctx, "BTP-TOP", "X", UnitG);
            var svc = CreateRecipeService(ctx);

            var result = await svc.CreateRecipeAsync(new RecipeCreateVM
            {
                RecipeType = "TOPPING",
                ToppingId = 1,
                PreparedItemId = pi,
                ExpectedYield = 1m,
                OutputUnitId = UnitG,
                Active = true,
                EffectiveDate = DateTime.Today,
                Details = OneIngredientDetail()
            });

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task Btp_RejectsDrinkSizeTopping()
        {
            using var ctx = CreateDbContext();
            var pi = await CreateActivePreparedItemAsync(ctx, "BTP-IDS", "X", UnitMl);
            var svc = CreateRecipeService(ctx);
            var vm = BtpVm(pi, 1m, UnitMl);
            vm.DrinkId = 1;

            var result = await svc.CreateRecipeAsync(vm);
            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task InactivePreparedItem_Rejected()
        {
            using var ctx = CreateDbContext();
            var pi = await CreateActivePreparedItemAsync(ctx, "BTP-INACT", "X", UnitMl);
            var row = await ctx.PreparedItems.FindAsync(pi);
            row!.Active = false;
            await ctx.SaveChangesAsync();

            var svc = CreateRecipeService(ctx);
            var result = await svc.CreateRecipeAsync(BtpVm(pi, 1m, UnitMl));
            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task UnknownPreparedItem_Rejected()
        {
            using var ctx = CreateDbContext();
            EnsureUnits(ctx);
            var svc = CreateRecipeService(ctx);
            var result = await svc.CreateRecipeAsync(BtpVm(99999, 1m, UnitMl));
            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task InactiveOutputUnit_Rejected()
        {
            using var ctx = CreateDbContext();
            var pi = await CreateActivePreparedItemAsync(ctx, "BTP-UNITI", "X", UnitMl);
            var unit = await ctx.Units.FindAsync(UnitL);
            unit!.Active = false;
            await ctx.SaveChangesAsync();

            var svc = CreateRecipeService(ctx);
            var result = await svc.CreateRecipeAsync(BtpVm(pi, 1m, UnitL));
            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task LiterToMlBase_Accepted()
        {
            using var ctx = CreateDbContext();
            var pi = await CreateActivePreparedItemAsync(ctx, "BTP-LML", "Cold brew", UnitMl);
            var norm = CreateNormalizer(ctx);
            var result = await norm.NormalizeAsync(pi, 4.5m, UnitL);
            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(4500m, result.Data.NormalizedQuantityInBase);
        }

        [Fact]
        public async Task KgToGBase_Accepted()
        {
            using var ctx = CreateDbContext();
            var pi = await CreateActivePreparedItemAsync(ctx, "BTP-KGG", "Syrup base", UnitG);
            var norm = CreateNormalizer(ctx);
            var result = await norm.NormalizeAsync(pi, 2m, UnitKg);
            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(2000m, result.Data.NormalizedQuantityInBase);
        }

        [Fact]
        public async Task SameCountUnit_Accepted()
        {
            using var ctx = CreateDbContext();
            var pi = await CreateActivePreparedItemAsync(ctx, "BTP-PCS", "Cake", UnitPcs);
            var norm = CreateNormalizer(ctx);
            var result = await norm.NormalizeAsync(pi, 12m, UnitPcs);
            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(12m, result.Data.NormalizedQuantityInBase);
        }

        [Fact]
        public async Task DifferentCountUnit_Rejected()
        {
            using var ctx = CreateDbContext();
            EnsureUnit(ctx, UnitBottle, "bottle", UnitType.Dem);
            // Create a second count unit not packaging for different-count test — use bottle as packaging rejected first;
            // for pure different count: pcs base vs a custom count unit if exists. Use bottle after ensuring code not packaging rejection path for convert:
            // Spec: different count units rejected. Use pcs base + bottle output → packaging reject OR dem convert fail.
            var pi = await CreateActivePreparedItemAsync(ctx, "BTP-CNT", "Count item", UnitPcs);
            var norm = CreateNormalizer(ctx);
            // bottle is packaging — rejected
            var bottle = await norm.NormalizeAsync(pi, 1m, UnitBottle);
            Assert.False(bottle.IsSuccess);

            // Add non-packaging second count unit "ea"
            EnsureUnit(ctx, 99, "ea", UnitType.Dem);
            var diff = await norm.NormalizeAsync(pi, 1m, 99);
            Assert.False(diff.IsSuccess);
        }

        [Theory]
        [InlineData(UnitBottle)]
        [InlineData(UnitCan)]
        [InlineData(UnitPack)]
        public async Task PackagingOutputUnit_Rejected(int packageUnitId)
        {
            using var ctx = CreateDbContext();
            EnsureUnit(ctx, UnitBottle, "bottle", UnitType.Dem);
            EnsureUnit(ctx, UnitCan, "can", UnitType.Dem);
            EnsureUnit(ctx, UnitPack, "pack", UnitType.Dem);
            var pi = await CreateActivePreparedItemAsync(ctx, $"BTP-PKG{packageUnitId}", "X", UnitMl);
            var norm = CreateNormalizer(ctx);
            var result = await norm.NormalizeAsync(pi, 1m, packageUnitId);
            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task MissingConversion_FailsClosed()
        {
            using var ctx = CreateDbContext();
            var pi = await CreateActivePreparedItemAsync(ctx, "BTP-DIM", "X", UnitMl);
            var norm = CreateNormalizer(ctx);
            // mass output → volume base
            var result = await norm.NormalizeAsync(pi, 1m, UnitKg);
            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task NormalizedOutput_DoesNotApplyYieldPercentage()
        {
            using var ctx = CreateDbContext();
            var pi = await CreateActivePreparedItemAsync(ctx, "BTP-YLD", "X", UnitMl);
            // Even if a recipe would have yield 50%, normalizer never uses it
            var norm = CreateNormalizer(ctx);
            var result = await norm.NormalizeAsync(pi, 4.5m, UnitL);
            Assert.True(result.IsSuccess);
            Assert.Equal(4500m, result.Data.NormalizedQuantityInBase);
            // not 4500 * 0.5 or /0.5
        }

        [Fact]
        public async Task NewBtp_StoresYieldPercentage100()
        {
            using var ctx = CreateDbContext();
            var pi = await CreateActivePreparedItemAsync(ctx, "BTP-Y100", "X", UnitMl);
            var svc = CreateRecipeService(ctx);
            var result = await svc.CreateRecipeAsync(BtpVm(pi, 1m, UnitMl));
            Assert.True(result.IsSuccess, result.Message);

            var recipe = await ctx.Recipes.SingleAsync(r => r.PreparedItemId == pi && r.Active);
            Assert.Equal(100m, recipe.YieldPercentage);
            Assert.Equal(1m, recipe.OutputQuantity);
            Assert.Equal(UnitMl, recipe.OutputUnitId);
        }

        [Fact]
        public async Task Version_KeepsSamePreparedItemId()
        {
            using var ctx = CreateDbContext();
            var pi = await CreateActivePreparedItemAsync(ctx, "BTP-VER1", "X", UnitMl);
            var svc = CreateRecipeService(ctx);
            Assert.True((await svc.CreateRecipeAsync(BtpVm(pi, 1m, UnitMl))).IsSuccess);

            var old = await ctx.Recipes.SingleAsync(r => r.PreparedItemId == pi && r.Active);
            var update = BtpVm(pi, 2m, UnitMl);
            var up = await svc.UpdateRecipeAsync(old.RecipeId, update);
            Assert.True(up.IsSuccess, up.Message);

            var neu = await ctx.Recipes.SingleAsync(r => r.PreparedItemId == pi && r.Active);
            Assert.Equal(pi, neu.PreparedItemId);
            Assert.Equal(old.RecipeId, neu.ParentVersionId);
            Assert.Equal(2m, neu.OutputQuantity);
        }

        [Fact]
        public async Task Version_MayChangeOutputQuantityAndUnit()
        {
            using var ctx = CreateDbContext();
            var pi = await CreateActivePreparedItemAsync(ctx, "BTP-VER2", "X", UnitMl);
            var svc = CreateRecipeService(ctx);
            Assert.True((await svc.CreateRecipeAsync(BtpVm(pi, 1m, UnitMl))).IsSuccess);
            var old = await ctx.Recipes.SingleAsync(r => r.PreparedItemId == pi && r.Active);

            var up = await svc.UpdateRecipeAsync(old.RecipeId, BtpVm(pi, 4.5m, UnitL));
            Assert.True(up.IsSuccess, up.Message);
            var neu = await ctx.Recipes.SingleAsync(r => r.PreparedItemId == pi && r.Active);
            Assert.Equal(4.5m, neu.OutputQuantity);
            Assert.Equal(UnitL, neu.OutputUnitId);
        }

        [Fact]
        public async Task Version_CannotChangePreparedItemId()
        {
            using var ctx = CreateDbContext();
            var pi1 = await CreateActivePreparedItemAsync(ctx, "BTP-A", "A", UnitMl);
            var pi2 = await CreateActivePreparedItemAsync(ctx, "BTP-B", "B", UnitMl);
            var svc = CreateRecipeService(ctx);
            Assert.True((await svc.CreateRecipeAsync(BtpVm(pi1, 1m, UnitMl))).IsSuccess);
            var old = await ctx.Recipes.SingleAsync(r => r.PreparedItemId == pi1 && r.Active);

            var result = await svc.UpdateRecipeAsync(old.RecipeId, BtpVm(pi2, 1m, UnitMl));
            Assert.False(result.IsSuccess);
            Assert.Contains("đổi", result.Message, StringComparison.OrdinalIgnoreCase);

            var still = await ctx.Recipes.FindAsync(old.RecipeId);
            Assert.True(still!.Active);
            Assert.Equal("Active", still.Status);
        }

        [Fact]
        public async Task Version_OldArchived_NewActive()
        {
            using var ctx = CreateDbContext();
            var pi = await CreateActivePreparedItemAsync(ctx, "BTP-ARCH", "X", UnitMl);
            var svc = CreateRecipeService(ctx);
            Assert.True((await svc.CreateRecipeAsync(BtpVm(pi, 1m, UnitMl))).IsSuccess);
            var oldId = (await ctx.Recipes.SingleAsync(r => r.PreparedItemId == pi && r.Active)).RecipeId;

            Assert.True((await svc.UpdateRecipeAsync(oldId, BtpVm(pi, 2m, UnitMl))).IsSuccess);
            var old = await ctx.Recipes.FindAsync(oldId);
            Assert.False(old!.Active);
            Assert.Equal("Archived", old.Status);

            var neu = await ctx.Recipes.SingleAsync(r => r.PreparedItemId == pi && r.Active);
            Assert.True(neu.Active);
            Assert.Equal("Active", neu.Status);
        }

        [Fact]
        public async Task Version_WhenValidationFails_OldRemainsActive()
        {
            using var ctx = CreateDbContext();
            var pi = await CreateActivePreparedItemAsync(ctx, "BTP-RB", "X", UnitMl);
            var svc = CreateRecipeService(ctx);
            Assert.True((await svc.CreateRecipeAsync(BtpVm(pi, 1m, UnitMl))).IsSuccess);
            var oldId = (await ctx.Recipes.SingleAsync(r => r.PreparedItemId == pi && r.Active)).RecipeId;

            var bad = BtpVm(pi, 1m, UnitMl);
            bad.Details = new List<RecipeDetailVM>(); // fail before archive
            var result = await svc.UpdateRecipeAsync(oldId, bad);
            Assert.False(result.IsSuccess);

            var old = await ctx.Recipes.FindAsync(oldId);
            Assert.True(old!.Active);
            Assert.Equal("Active", old.Status);
        }

        [Fact]
        public async Task OnlyOneActiveRecipe_PerPreparedItem()
        {
            using var ctx = CreateDbContext();
            var pi = await CreateActivePreparedItemAsync(ctx, "BTP-ONE", "X", UnitMl);
            var svc = CreateRecipeService(ctx);
            Assert.True((await svc.CreateRecipeAsync(BtpVm(pi, 1m, UnitMl))).IsSuccess);

            var second = await svc.CreateRecipeAsync(BtpVm(pi, 2m, UnitMl));
            Assert.False(second.IsSuccess);
            Assert.Contains("hoạt động", second.Message, StringComparison.OrdinalIgnoreCase);

            Assert.Equal(1, await ctx.Recipes.CountAsync(r => r.PreparedItemId == pi && r.Active));
        }

        [Fact]
        public async Task UniqueConflict_FriendlyMessage()
        {
            using var ctx = CreateDbContext();
            var pi = await CreateActivePreparedItemAsync(ctx, "BTP-UQ", "X", UnitMl);
            var svc = CreateRecipeService(ctx);
            Assert.True((await svc.CreateRecipeAsync(BtpVm(pi, 1m, UnitMl))).IsSuccess);

            // Direct insert second Active to force DB unique if service guard bypassed
            ctx.Recipes.Add(new Recipe
            {
                RecipeCode = "RCP_FORCE_UQ",
                Name = "Force",
                YieldPercentage = 100,
                Active = true,
                Status = "Active",
                PreparedItemId = pi,
                OutputQuantity = 1m,
                OutputUnitId = UnitMl,
                RecipeDetails = new List<RecipeDetail>
                {
                    new() { IngredientId = 1, Quantity = 1, UnitId = UnitG }
                }
            });

            try
            {
                await ctx.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // filtered unique may fire — OK
            }

            // Service path always friendly
            var r = await svc.CreateRecipeAsync(BtpVm(pi, 3m, UnitMl));
            Assert.False(r.IsSuccess);
            Assert.DoesNotContain("IX_", r.Message);
        }

        [Fact]
        public async Task LegacySubRecipe_RemainsReadable()
        {
            using var ctx = CreateDbContext();
            EnsureUnits(ctx);
            ctx.Recipes.Add(new Recipe
            {
                RecipeCode = "RCP_LEGACY_SUB",
                Name = "Legacy unmapped BTP",
                YieldPercentage = 100,
                Active = true,
                Status = "Active",
                PreparedItemId = null,
                OutputQuantity = null,
                OutputUnitId = null,
                DrinkId = null,
                ToppingId = null,
                RecipeDetails = new List<RecipeDetail>
                {
                    new() { IngredientId = 1, Quantity = 5, UnitId = UnitG }
                }
            });
            await ctx.SaveChangesAsync();

            var legacy = await ctx.Recipes.AsNoTracking()
                .SingleAsync(r => r.RecipeCode == "RCP_LEGACY_SUB");
            Assert.Null(legacy.PreparedItemId);
            Assert.True(legacy.Active);
            Assert.Equal("Legacy unmapped BTP", legacy.Name);
        }

        [Fact]
        public async Task LegacySubRecipe_CannotVersionWithoutMapping()
        {
            using var ctx = CreateDbContext();
            EnsureUnits(ctx);
            var legacy = new Recipe
            {
                RecipeCode = "RCP_LEGACY2",
                Name = "Legacy 2",
                YieldPercentage = 100,
                Active = true,
                Status = "Active",
                RecipeDetails = new List<RecipeDetail>
                {
                    new() { IngredientId = 1, Quantity = 5, UnitId = UnitG }
                }
            };
            ctx.Recipes.Add(legacy);
            await ctx.SaveChangesAsync();

            var svc = CreateRecipeService(ctx);
            var result = await svc.UpdateRecipeAsync(legacy.RecipeId, new RecipeCreateVM
            {
                RecipeType = "SUBRECIPE",
                SubRecipeName = "Legacy 2",
                Active = true,
                EffectiveDate = DateTime.Today,
                Details = OneIngredientDetail()
            });

            Assert.False(result.IsSuccess);
            Assert.Contains("liên kết", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.True((await ctx.Recipes.FindAsync(legacy.RecipeId))!.Active);
        }

        [Fact]
        public async Task LegacySubRecipe_CanVersion_WithExplicitMapping()
        {
            using var ctx = CreateDbContext();
            var pi = await CreateActivePreparedItemAsync(ctx, "BTP-LEGMAP", "Mapped", UnitMl);
            var legacy = new Recipe
            {
                RecipeCode = "RCP_LEGACY3",
                Name = "Legacy 3",
                YieldPercentage = 100,
                Active = true,
                Status = "Active",
                RecipeDetails = new List<RecipeDetail>
                {
                    new() { IngredientId = 1, Quantity = 5, UnitId = UnitG }
                }
            };
            ctx.Recipes.Add(legacy);
            await ctx.SaveChangesAsync();

            var svc = CreateRecipeService(ctx);
            var result = await svc.UpdateRecipeAsync(legacy.RecipeId, BtpVm(pi, 4.5m, UnitL));
            Assert.True(result.IsSuccess, result.Message);

            Assert.False((await ctx.Recipes.FindAsync(legacy.RecipeId))!.Active);
            var neu = await ctx.Recipes.SingleAsync(r => r.PreparedItemId == pi && r.Active);
            Assert.Equal(4.5m, neu.OutputQuantity);
            Assert.Equal(UnitL, neu.OutputUnitId);
            Assert.Equal(legacy.RecipeId, neu.ParentVersionId);
        }

        [Fact]
        public async Task LegacyMapping_RequiresQuantityAndUnit()
        {
            using var ctx = CreateDbContext();
            var pi = await CreateActivePreparedItemAsync(ctx, "BTP-LEGQ", "Need qty", UnitMl);
            var legacy = new Recipe
            {
                RecipeCode = "RCP_LEGACY_Q",
                Name = "Legacy Q",
                YieldPercentage = 100,
                Active = true,
                Status = "Active",
                RecipeDetails = new List<RecipeDetail>
                {
                    new() { IngredientId = 1, Quantity = 5, UnitId = UnitG }
                }
            };
            ctx.Recipes.Add(legacy);
            await ctx.SaveChangesAsync();

            var svc = CreateRecipeService(ctx);
            var missingQty = new RecipeCreateVM
            {
                RecipeType = "SUBRECIPE",
                PreparedItemId = pi,
                ExpectedYield = null,
                OutputUnitId = UnitMl,
                Active = true,
                EffectiveDate = DateTime.Today,
                Details = OneIngredientDetail()
            };
            var r1 = await svc.UpdateRecipeAsync(legacy.RecipeId, missingQty);
            Assert.False(r1.IsSuccess);
            Assert.True((await ctx.Recipes.FindAsync(legacy.RecipeId))!.Active);

            var missingUnit = new RecipeCreateVM
            {
                RecipeType = "SUBRECIPE",
                PreparedItemId = pi,
                ExpectedYield = 1m,
                OutputUnitId = null,
                Active = true,
                EffectiveDate = DateTime.Today,
                Details = OneIngredientDetail()
            };
            var r2 = await svc.UpdateRecipeAsync(legacy.RecipeId, missingUnit);
            Assert.False(r2.IsSuccess);
            Assert.True((await ctx.Recipes.FindAsync(legacy.RecipeId))!.Active);
        }

        [Fact]
        public async Task LegacyMapping_ConflictsWhenAnotherActiveProducesPreparedItem()
        {
            using var ctx = CreateDbContext();
            var pi = await CreateActivePreparedItemAsync(ctx, "BTP-LEGCF", "Conflict", UnitMl);
            var svc = CreateRecipeService(ctx);
            Assert.True((await svc.CreateRecipeAsync(BtpVm(pi, 1m, UnitMl))).IsSuccess);

            var legacy = new Recipe
            {
                RecipeCode = "RCP_LEGACY_CF",
                Name = "Legacy CF",
                YieldPercentage = 100,
                Active = true,
                Status = "Active",
                RecipeDetails = new List<RecipeDetail>
                {
                    new() { IngredientId = 1, Quantity = 5, UnitId = UnitG }
                }
            };
            ctx.Recipes.Add(legacy);
            await ctx.SaveChangesAsync();

            var result = await svc.UpdateRecipeAsync(legacy.RecipeId, BtpVm(pi, 2m, UnitMl));
            Assert.False(result.IsSuccess);
            Assert.Contains("hoạt động", result.Message, StringComparison.OrdinalIgnoreCase);

            var old = await ctx.Recipes.FindAsync(legacy.RecipeId);
            Assert.True(old!.Active);
            Assert.Equal("Active", old.Status);
            Assert.Null(old.PreparedItemId);
        }

        [Fact]
        public async Task AfterLegacyMapping_PreparedItemIdIsImmutable()
        {
            using var ctx = CreateDbContext();
            var pi1 = await CreateActivePreparedItemAsync(ctx, "BTP-IMM1", "First", UnitMl);
            var pi2 = await CreateActivePreparedItemAsync(ctx, "BTP-IMM2", "Second", UnitMl);
            var legacy = new Recipe
            {
                RecipeCode = "RCP_LEGACY_IMM",
                Name = "Legacy IMM",
                YieldPercentage = 100,
                Active = true,
                Status = "Active",
                RecipeDetails = new List<RecipeDetail>
                {
                    new() { IngredientId = 1, Quantity = 5, UnitId = UnitG }
                }
            };
            ctx.Recipes.Add(legacy);
            await ctx.SaveChangesAsync();

            var svc = CreateRecipeService(ctx);
            Assert.True((await svc.UpdateRecipeAsync(legacy.RecipeId, BtpVm(pi1, 1m, UnitMl))).IsSuccess);

            var linked = await ctx.Recipes.SingleAsync(r => r.PreparedItemId == pi1 && r.Active);
            var change = await svc.UpdateRecipeAsync(linked.RecipeId, BtpVm(pi2, 1m, UnitMl));
            Assert.False(change.IsSuccess);
            Assert.Contains("đổi", change.Message, StringComparison.OrdinalIgnoreCase);
            Assert.True((await ctx.Recipes.FindAsync(linked.RecipeId))!.Active);
            Assert.Equal(pi1, (await ctx.Recipes.FindAsync(linked.RecipeId))!.PreparedItemId);
        }

        [Fact]
        public async Task LinkedChain_OmittedPreparedItemId_FilledFromOldVersion()
        {
            using var ctx = CreateDbContext();
            var pi = await CreateActivePreparedItemAsync(ctx, "BTP-FILL", "Fill", UnitMl);
            var svc = CreateRecipeService(ctx);
            Assert.True((await svc.CreateRecipeAsync(BtpVm(pi, 1m, UnitMl))).IsSuccess);
            var old = await ctx.Recipes.SingleAsync(r => r.PreparedItemId == pi && r.Active);

            var vm = BtpVm(pi, 3m, UnitL);
            vm.PreparedItemId = null; // omit — service must fill from old linked chain
            var result = await svc.UpdateRecipeAsync(old.RecipeId, vm);
            Assert.True(result.IsSuccess, result.Message);

            var neu = await ctx.Recipes.SingleAsync(r => r.PreparedItemId == pi && r.Active);
            Assert.Equal(pi, neu.PreparedItemId);
            Assert.Equal(3m, neu.OutputQuantity);
            Assert.Equal(UnitL, neu.OutputUnitId);
        }

        [Fact]
        public void NoPreparedItemOrRecipeOutputSeedMapping()
        {
            using var ctx = CreateDbContext();
            Assert.Equal(0, ctx.PreparedItems.Count());
            Assert.Equal(0, ctx.Recipes.Count(r => r.PreparedItemId != null));
            // Seeded recipes 5/6 remain topping-only
            var r5 = ctx.Recipes.Find(5);
            var r6 = ctx.Recipes.Find(6);
            Assert.NotNull(r5);
            Assert.NotNull(r6);
            Assert.Equal(1, r5!.ToppingId);
            Assert.Equal(2, r6!.ToppingId);
            Assert.Null(r5.PreparedItemId);
            Assert.Null(r6.PreparedItemId);
        }

        [Fact]
        public async Task CreateBtp_DoesNotCreateOrChangeStoreInventory()
        {
            using var ctx = CreateDbContext();
            var before = await ctx.Set<StoreInventory>().AsNoTracking()
                .Select(i => new { i.StoreInventoryId, i.RecipeId, i.AvailableQty })
                .ToListAsync();

            var pi = await CreateActivePreparedItemAsync(ctx, "BTP-NOINV", "X", UnitMl);
            var svc = CreateRecipeService(ctx);
            Assert.True((await svc.CreateRecipeAsync(BtpVm(pi, 1m, UnitMl))).IsSuccess);

            var after = await ctx.Set<StoreInventory>().AsNoTracking()
                .Select(i => new { i.StoreInventoryId, i.RecipeId, i.AvailableQty })
                .ToListAsync();
            Assert.Equal(before.Count, after.Count);
        }

        [Fact]
        public async Task Version_DoesNotMigrateChildRecipeDetails()
        {
            using var ctx = CreateDbContext();
            var detailIdsBefore = await ctx.RecipeDetails.AsNoTracking()
                .Select(d => d.RecipeDetailId)
                .OrderBy(x => x)
                .ToListAsync();

            var pi = await CreateActivePreparedItemAsync(ctx, "BTP-CHD", "X", UnitMl);
            var svc = CreateRecipeService(ctx);
            Assert.True((await svc.CreateRecipeAsync(BtpVm(pi, 1m, UnitMl))).IsSuccess);

            // Existing seed ChildRecipeId rows untouched for recipe 3
            var childLinks = await ctx.RecipeDetails.AsNoTracking()
                .Where(d => d.ChildRecipeId != null)
                .Select(d => new { d.RecipeDetailId, d.ChildRecipeId })
                .ToListAsync();
            Assert.Contains(childLinks, x => x.ChildRecipeId == 5);

            // No PreparedItemId column on RecipeDetail — property must not exist
            Assert.Null(typeof(RecipeDetail).GetProperty("PreparedItemId"));
            Assert.True(detailIdsBefore.All(id => ctx.RecipeDetails.Any(d => d.RecipeDetailId == id)));
        }

        [Fact]
        public void CreateEditDelete_HaveValidateAntiForgeryToken()
        {
            var t = typeof(AdminRecipeController);
            Assert.NotNull(t.GetMethod(nameof(AdminRecipeController.Create), new[] { typeof(RecipeCreateVM) })!
                .GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
            Assert.NotNull(t.GetMethod(nameof(AdminRecipeController.Edit), new[] { typeof(int), typeof(RecipeCreateVM) })!
                .GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
            Assert.NotNull(t.GetMethod(nameof(AdminRecipeController.Delete), new[] { typeof(int) })!
                .GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
            Assert.NotNull(t.GetMethod(nameof(AdminRecipeController.PreviewNormalizedOutput), new[] { typeof(RecipeOutputPreviewRequest) })!
                .GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        }

        [Fact]
        public async Task ExistingDrinkSizeFlow_StillWorks()
        {
            using var ctx = CreateDbContext();
            EnsureUnits(ctx);
            var svc = CreateRecipeService(ctx);
            // Use a free drink/size pair not already seeded active uniquely — version of drink 1 size 1 ok as create new with different? 
            // Seed already has drink1 size1. Create still allowed today (no unique). Use drink 1 size 1 still.
            var result = await svc.CreateRecipeAsync(new RecipeCreateVM
            {
                RecipeType = "POS",
                DrinkId = 1,
                SizeId = 1,
                Active = true,
                EffectiveDate = DateTime.Today,
                Details = OneIngredientDetail()
            });
            Assert.True(result.IsSuccess, result.Message);
        }

        [Fact]
        public async Task ExistingToppingFlow_StillWorks()
        {
            using var ctx = CreateDbContext();
            EnsureUnits(ctx);
            var svc = CreateRecipeService(ctx);
            var result = await svc.CreateRecipeAsync(new RecipeCreateVM
            {
                RecipeType = "TOPPING",
                ToppingId = 1,
                Active = true,
                EffectiveDate = DateTime.Today,
                Details = OneIngredientDetail()
            });
            Assert.True(result.IsSuccess, result.Message);
        }

        [Fact]
        public async Task CycleDetection_StillWorks()
        {
            using var ctx = CreateDbContext();
            var pi = await CreateActivePreparedItemAsync(ctx, "BTP-CYC", "Cyc", UnitMl);
            var svc = CreateRecipeService(ctx);
            Assert.True((await svc.CreateRecipeAsync(BtpVm(pi, 1m, UnitMl))).IsSuccess);
            var r = await ctx.Recipes.SingleAsync(x => x.PreparedItemId == pi && x.Active);

            // Self-reference as child
            var update = BtpVm(pi, 1m, UnitMl);
            update.Details = new List<RecipeDetailVM>
            {
                new()
                {
                    ItemCode = $"REC_{r.RecipeId}",
                    Quantity = 1,
                    UnitId = UnitG,
                    YieldPercentage = 100
                }
            };
            var result = await svc.UpdateRecipeAsync(r.RecipeId, update);
            Assert.False(result.IsSuccess);
        }

        private static void EnsureUnits(AppDbContext ctx)
        {
            EnsureUnit(ctx, UnitG, "g", UnitType.KhoiLuong);
            EnsureUnit(ctx, UnitKg, "kg", UnitType.KhoiLuong);
            EnsureUnit(ctx, UnitMl, "ml", UnitType.TheTich);
            EnsureUnit(ctx, UnitL, "l", UnitType.TheTich);
            EnsureUnit(ctx, UnitPcs, "pcs", UnitType.Dem);
        }

        private static void EnsureUnit(AppDbContext ctx, int unitId, string code, UnitType type)
        {
            var u = ctx.Units.FirstOrDefault(x => x.UnitId == unitId);
            if (u != null)
            {
                u.UnitCode = code;
                u.Type = type;
                u.Active = true;
                u.Name = code;
                ctx.SaveChanges();
                return;
            }

            if (ctx.Units.Any(x => x.UnitCode.ToLower() == code.ToLower()))
                return;

            ctx.Units.Add(new Unit
            {
                UnitId = unitId,
                UnitCode = code,
                Name = code,
                Type = type,
                Active = true
            });
            ctx.SaveChanges();
        }
    }
}
