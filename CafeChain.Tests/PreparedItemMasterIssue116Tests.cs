using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.PreparedItems;
using CafeChain.Application.Interfaces.Admin.PreparedItems;
using CafeChain.Application.Services.Admin.PreparedItems;
using CafeChain.Areas.Admin.Controllers;
using CafeChain.Data;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CafeChain.Tests.POS
{
    /// <summary>Issue #116 — PreparedItem stable BTP master (no stock/Recipe cutover).</summary>
    public class PreparedItemMasterIssue116Tests : IntegrationTestBase
    {
        private const int UnitG = 1;
        private const int UnitKg = 2;
        private const int UnitMl = 3;
        private const int UnitL = 4;
        private const int UnitPcs = 9;
        private const int UnitBottle = 10;
        private const int UnitCan = 11;
        private const int UnitPack = 12;

        [Fact]
        public async Task Create_MassBaseUnit_Succeeds()
        {
            using var ctx = CreateDbContext();
            EnsureUnits(ctx);
            var svc = new AdminPreparedItemService(ctx);

            var id = await svc.CreateAsync(new AdminPreparedItemSaveDTO
            {
                Code = "btp-pearl",
                Name = "Trân châu đã nấu",
                BaseUnitId = UnitG
            });

            var row = await ctx.PreparedItems.FindAsync(id);
            Assert.NotNull(row);
            Assert.Equal("BTP-PEARL", row!.Code);
            Assert.Equal(UnitG, row.BaseUnitId);
            Assert.True(row.Active);
        }

        [Fact]
        public async Task Create_VolumeBaseUnit_Succeeds()
        {
            using var ctx = CreateDbContext();
            EnsureUnits(ctx);
            var svc = new AdminPreparedItemService(ctx);

            var id = await svc.CreateAsync(new AdminPreparedItemSaveDTO
            {
                Code = "BTP-COLDBREW",
                Name = "Cold brew cô đặc",
                BaseUnitId = UnitMl
            });

            Assert.True(id > 0);
            Assert.Equal("BTP-COLDBREW", (await ctx.PreparedItems.FindAsync(id))!.Code);
        }

        [Fact]
        public async Task Create_PcsBaseUnit_Succeeds()
        {
            using var ctx = CreateDbContext();
            EnsureUnits(ctx);
            var svc = new AdminPreparedItemService(ctx);

            var id = await svc.CreateAsync(new AdminPreparedItemSaveDTO
            {
                Code = "BTP-CAKE",
                Name = "Bánh sơ chế",
                BaseUnitId = UnitPcs
            });

            Assert.True(id > 0);
        }

        [Fact]
        public async Task MissingCode_Rejected()
        {
            using var ctx = CreateDbContext();
            EnsureUnits(ctx);
            var svc = new AdminPreparedItemService(ctx);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.CreateAsync(new AdminPreparedItemSaveDTO
                {
                    Code = "   ",
                    Name = "X",
                    BaseUnitId = UnitG
                }));
        }

        [Fact]
        public async Task WhitespaceCode_Rejected()
        {
            using var ctx = CreateDbContext();
            EnsureUnits(ctx);
            var svc = new AdminPreparedItemService(ctx);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.CreateAsync(new AdminPreparedItemSaveDTO
                {
                    Code = "\t\n",
                    Name = "X",
                    BaseUnitId = UnitG
                }));
        }

        [Fact]
        public async Task MissingName_Rejected()
        {
            using var ctx = CreateDbContext();
            EnsureUnits(ctx);
            var svc = new AdminPreparedItemService(ctx);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.CreateAsync(new AdminPreparedItemSaveDTO
                {
                    Code = "BTP-X",
                    Name = "  ",
                    BaseUnitId = UnitG
                }));
        }

        [Fact]
        public void NormalizeCode_TrimsAndUppercases()
        {
            Assert.Equal("BTP-COLDBREW", AdminPreparedItemService.NormalizeCode(" btp-coldbrew "));
            Assert.Equal("BTP-COLDBREW", AdminPreparedItemService.NormalizeCode("Btp-ColdBrew"));
        }

        [Fact]
        public async Task DuplicateCode_DifferentCase_Rejected()
        {
            using var ctx = CreateDbContext();
            EnsureUnits(ctx);
            var svc = new AdminPreparedItemService(ctx);

            await svc.CreateAsync(new AdminPreparedItemSaveDTO
            {
                Code = "BTP-A",
                Name = "A",
                BaseUnitId = UnitG
            });

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.CreateAsync(new AdminPreparedItemSaveDTO
                {
                    Code = "btp-a",
                    Name = "B",
                    BaseUnitId = UnitG
                }));
        }

        [Fact]
        public async Task DuplicateCode_WithSpaces_Rejected()
        {
            using var ctx = CreateDbContext();
            EnsureUnits(ctx);
            var svc = new AdminPreparedItemService(ctx);

            await svc.CreateAsync(new AdminPreparedItemSaveDTO
            {
                Code = "BTP-B",
                Name = "A",
                BaseUnitId = UnitG
            });

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.CreateAsync(new AdminPreparedItemSaveDTO
                {
                    Code = "  BTP-B  ",
                    Name = "B",
                    BaseUnitId = UnitG
                }));
        }

        [Fact]
        public async Task UnknownBaseUnit_Rejected()
        {
            using var ctx = CreateDbContext();
            EnsureUnits(ctx);
            var svc = new AdminPreparedItemService(ctx);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.CreateAsync(new AdminPreparedItemSaveDTO
                {
                    Code = "BTP-U",
                    Name = "U",
                    BaseUnitId = 999999
                }));
        }

        [Fact]
        public async Task InactiveBaseUnit_Rejected()
        {
            using var ctx = CreateDbContext();
            EnsureUnits(ctx);
            var unit = ctx.Units.First(u => u.UnitId == UnitG);
            unit.Active = false;
            ctx.SaveChanges();
            var svc = new AdminPreparedItemService(ctx);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.CreateAsync(new AdminPreparedItemSaveDTO
                {
                    Code = "BTP-I",
                    Name = "I",
                    BaseUnitId = UnitG
                }));
        }

        [Theory]
        [InlineData(UnitBottle, "bottle")]
        [InlineData(UnitCan, "can")]
        [InlineData(UnitPack, "pack")]
        public async Task PackagingBaseUnit_Rejected(int unitId, string code)
        {
            using var ctx = CreateDbContext();
            EnsureUnits(ctx);
            EnsureUnit(ctx, unitId, code, UnitType.Dem);
            var svc = new AdminPreparedItemService(ctx);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.CreateAsync(new AdminPreparedItemSaveDTO
                {
                    Code = "BTP-P-" + code.ToUpperInvariant(),
                    Name = "P",
                    BaseUnitId = unitId
                }));
            Assert.Contains("đóng gói", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Description_MaxLength_Enforced()
        {
            using var ctx = CreateDbContext();
            EnsureUnits(ctx);
            var svc = new AdminPreparedItemService(ctx);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.CreateAsync(new AdminPreparedItemSaveDTO
                {
                    Code = "BTP-D",
                    Name = "D",
                    BaseUnitId = UnitG,
                    Description = new string('x', 501)
                }));
        }

        [Fact]
        public async Task Disable_SetsActiveFalse_PreservesRow()
        {
            using var ctx = CreateDbContext();
            EnsureUnits(ctx);
            var svc = new AdminPreparedItemService(ctx);
            var id = await svc.CreateAsync(new AdminPreparedItemSaveDTO
            {
                Code = "BTP-OFF",
                Name = "Off",
                BaseUnitId = UnitG
            });

            await svc.SetActiveAsync(id, false);
            var row = await ctx.PreparedItems.FindAsync(id);
            Assert.NotNull(row);
            Assert.False(row!.Active);
            Assert.Equal("BTP-OFF", row.Code);
        }

        [Fact]
        public async Task ReEnable_SetsActiveTrue()
        {
            using var ctx = CreateDbContext();
            EnsureUnits(ctx);
            var svc = new AdminPreparedItemService(ctx);
            var id = await svc.CreateAsync(new AdminPreparedItemSaveDTO
            {
                Code = "BTP-ON",
                Name = "On",
                BaseUnitId = UnitG
            });
            await svc.SetActiveAsync(id, false);
            await svc.SetActiveAsync(id, true);
            Assert.True((await ctx.PreparedItems.FindAsync(id))!.Active);
        }

        [Fact]
        public async Task ReEnable_Rejects_WhenBaseUnitBecameInactive()
        {
            using var ctx = CreateDbContext();
            EnsureUnits(ctx);
            var svc = new AdminPreparedItemService(ctx);
            var id = await svc.CreateAsync(new AdminPreparedItemSaveDTO
            {
                Code = "BTP-REVAL",
                Name = "Reval",
                BaseUnitId = UnitG
            });
            await svc.SetActiveAsync(id, false);

            var unit = ctx.Units.First(u => u.UnitId == UnitG);
            unit.Active = false;
            ctx.SaveChanges();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.SetActiveAsync(id, true));
            Assert.False((await ctx.PreparedItems.FindAsync(id))!.Active);
        }

        [Fact]
        public async Task GetInventoryUnits_ExcludesPackagingCodes()
        {
            using var ctx = CreateDbContext();
            EnsureUnits(ctx);
            EnsureUnit(ctx, UnitBottle, "bottle", UnitType.Dem);
            EnsureUnit(ctx, UnitCan, "can", UnitType.Dem);
            EnsureUnit(ctx, UnitPack, "pack", UnitType.Dem);
            var svc = new AdminPreparedItemService(ctx);

            var units = await svc.GetInventoryUnitsAsync();
            var codes = units.Select(u => (u.UnitCode ?? "").ToLowerInvariant()).ToList();

            Assert.DoesNotContain("bottle", codes);
            Assert.DoesNotContain("can", codes);
            Assert.DoesNotContain("pack", codes);
            Assert.Contains("g", codes);
            Assert.Contains("pcs", codes);
        }

        [Fact]
        public void NoHardDelete_EndpointOrServiceMethod()
        {
            var controllerMethods = typeof(AdminPreparedItemController)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(m => m.Name);
            Assert.DoesNotContain(controllerMethods, n =>
                n.Contains("Delete", StringComparison.OrdinalIgnoreCase)
                || n.Contains("Remove", StringComparison.OrdinalIgnoreCase));

            var serviceMethods = typeof(IAdminPreparedItemService)
                .GetMethods()
                .Select(m => m.Name);
            Assert.DoesNotContain(serviceMethods, n =>
                n.Contains("Delete", StringComparison.OrdinalIgnoreCase)
                || n.Contains("Remove", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void WriteEndpoints_RequireBusinessPermissions_WithoutRoleAllowLists()
        {
            var expected = new Dictionary<string, string>
            {
                ["Create"] = PermissionConstants.PreparedItemCreate,
                ["Update"] = PermissionConstants.PreparedItemUpdate,
                ["SetActive"] = PermissionConstants.PreparedItemToggleStatus
            };
            foreach (var (name, permissionCode) in expected)
            {
                var method = typeof(AdminPreparedItemController).GetMethod(name);
                Assert.NotNull(method);
                var auth = method!.GetCustomAttribute<CafeChain.Application.Authorization.RequirePermissionAttribute>();
                Assert.NotNull(auth);
                Assert.Equal(
                    CafeChain.Application.Authorization.RequirePermissionAttribute.PolicyPrefix + permissionCode,
                    auth!.Policy);
                Assert.Null(auth.Roles);
            }
        }

        [Fact]
        public void Index_InheritsAdminPanelAccess_ForView()
        {
            var typeAuth = typeof(AdminPreparedItemController).BaseType!
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>()
                .ToList();
            Assert.Contains(typeAuth, a => a.Policy == "RequireAdminPanelAccess");
        }

        [Fact]
        public void WriteEndpoints_HaveValidateAntiForgeryToken()
        {
            foreach (var name in new[] { "Create", "Update", "SetActive" })
            {
                var method = typeof(AdminPreparedItemController).GetMethod(name);
                Assert.NotNull(method);
                Assert.NotNull(method!.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
            }
        }

        [Fact]
        public async Task Create_DoesNotCreateStoreInventory_OrRecipe()
        {
            using var ctx = CreateDbContext();
            EnsureUnits(ctx);
            var invBefore = await ctx.Set<StoreInventory>().CountAsync();
            var recipeBefore = await ctx.Recipes.CountAsync();
            var svc = new AdminPreparedItemService(ctx);

            await svc.CreateAsync(new AdminPreparedItemSaveDTO
            {
                Code = "BTP-SIDE",
                Name = "Side effect check",
                BaseUnitId = UnitMl
            });

            Assert.Equal(invBefore, await ctx.Set<StoreInventory>().CountAsync());
            Assert.Equal(recipeBefore, await ctx.Recipes.CountAsync());
        }

        [Fact]
        public async Task Create_DoesNotModifyExistingRecipeIdStock()
        {
            using var ctx = CreateDbContext();
            EnsureUnits(ctx);
            // Seed may have no store inventory; add one RecipeId stock row
            if (!ctx.Set<StoreInventory>().Any(i => i.RecipeId == 5))
            {
                ctx.Set<StoreInventory>().Add(new StoreInventory
                {
                    StoreId = 1,
                    RecipeId = 5,
                    AvailableQty = 12.5m,
                    ReservedQty = 0,
                    LastUpdated = DateTime.UtcNow,
                    RowVersion = new byte[] { 0 }
                });
                ctx.SaveChanges();
            }

            var before = await ctx.Set<StoreInventory>()
                .AsNoTracking()
                .Where(i => i.RecipeId != null)
                .Select(i => new { i.StoreInventoryId, i.RecipeId, i.AvailableQty })
                .ToListAsync();

            var svc = new AdminPreparedItemService(ctx);
            await svc.CreateAsync(new AdminPreparedItemSaveDTO
            {
                Code = "BTP-NOSTOCK",
                Name = "No stock touch",
                BaseUnitId = UnitG
            });

            var after = await ctx.Set<StoreInventory>()
                .AsNoTracking()
                .Where(i => i.RecipeId != null)
                .Select(i => new { i.StoreInventoryId, i.RecipeId, i.AvailableQty })
                .ToListAsync();

            Assert.Equal(before.Count, after.Count);
            foreach (var b in before)
            {
                var a = after.Single(x => x.StoreInventoryId == b.StoreInventoryId);
                Assert.Equal(b.RecipeId, a.RecipeId);
                Assert.Equal(b.AvailableQty, a.AvailableQty);
            }
        }

        [Fact]
        public async Task Create_DoesNotModifyRecipesOrDetails()
        {
            using var ctx = CreateDbContext();
            EnsureUnits(ctx);
            var recipesBefore = await ctx.Recipes.AsNoTracking().Select(r => r.RecipeId).OrderBy(x => x).ToListAsync();
            var detailsBefore = await ctx.RecipeDetails.AsNoTracking().Select(d => d.RecipeDetailId).OrderBy(x => x).ToListAsync();

            var svc = new AdminPreparedItemService(ctx);
            await svc.CreateAsync(new AdminPreparedItemSaveDTO
            {
                Code = "BTP-NORECIPE",
                Name = "No recipe touch",
                BaseUnitId = UnitG
            });

            var recipesAfter = await ctx.Recipes.AsNoTracking().Select(r => r.RecipeId).OrderBy(x => x).ToListAsync();
            var detailsAfter = await ctx.RecipeDetails.AsNoTracking().Select(d => d.RecipeDetailId).OrderBy(x => x).ToListAsync();
            Assert.Equal(recipesBefore, recipesAfter);
            Assert.Equal(detailsBefore, detailsAfter);
        }

        [Fact]
        public void NoPreparedItemSeeds()
        {
            using var ctx = CreateDbContext();
            Assert.Equal(0, ctx.PreparedItems.Count());
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
