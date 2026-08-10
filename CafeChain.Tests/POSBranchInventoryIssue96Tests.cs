using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Services.POS;
using CafeChain.Controllers.Api.v1;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CafeChain.Tests.POS
{
    /// <summary>
    /// Issue #96 — POS Kho chi nhánh read-only inventory.
    /// </summary>
    public class POSBranchInventoryIssue96Tests : IntegrationTestBase
    {
        private const int StoreA = 60;
        private const int StoreB = 61;
        private const int UnitId = 1;
        private const int IngredientId = 901;
        private const int RecipeId = 9100;

        [Fact]
        public async Task GetBranchInventory_ReturnsOnlyCurrentStoreRows()
        {
            using var ctx = CreateDbContext();
            SeedCatalog(ctx);
            await ctx.SaveChangesAsync();

            var service = new PosBranchInventoryService(ctx);
            var result = await service.GetBranchInventoryAsync(StoreA, null, null, 1, 50);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal(StoreA, result.Data!.StoreId);
            Assert.All(result.Data.Items, i => Assert.Equal(StoreA, i.StoreId));
            Assert.DoesNotContain(result.Data.Items, i => i.StoreId == StoreB);
            Assert.Equal(3, result.Data.Items.Count); // milk + btp + sugar (neg) at store A
            Assert.Contains(result.Data.Items, i => i.QuantityStatus == "Tồn âm");
            Assert.Contains(result.Data.Items, i => i.QuantityStatus == "Hết hàng");
            Assert.Contains(result.Data.Items, i => i.QuantityStatus == "Còn hàng");
        }

        [Fact]
        public async Task GetBranchInventory_SupportsIngredientAndRecipeRows()
        {
            using var ctx = CreateDbContext();
            SeedCatalog(ctx);
            await ctx.SaveChangesAsync();

            var service = new PosBranchInventoryService(ctx);
            var result = await service.GetBranchInventoryAsync(StoreA, null, null, 1, 50);

            Assert.True(result.IsSuccess);
            var ingredient = result.Data!.Items.Single(i =>
                i.ItemType == "Ingredient" && i.ItemId == IngredientId);
            var recipe = result.Data.Items.Single(i => i.ItemType == "Recipe");

            Assert.Equal(IngredientId, ingredient.ItemId);
            Assert.Equal("Sữa tươi test", ingredient.ItemName);
            Assert.Equal(12m, ingredient.OnHandQty);
            Assert.Equal(1m, ingredient.ReservedQty);
            Assert.Equal(11m, ingredient.UsableQty);
            // BaseUnitId=1 comes from seed data (UnitCode may be g/ml depending on seed).
            Assert.False(string.IsNullOrWhiteSpace(ingredient.UnitName));
            Assert.NotEqual("—", ingredient.UnitName);

            Assert.Equal(RecipeId, recipe.ItemId);
            Assert.Equal("Syrup BTP", recipe.ItemName);
            Assert.Equal("—", recipe.UnitName);
        }

        [Fact]
        public async Task GetBranchInventory_CanonicalLinkedPreparedItem_UsesConfirmedBaseUnit()
        {
            using var ctx = CreateDbContext();
            SeedCatalog(ctx);
            ctx.PreparedItems.Add(new PreparedItem
            {
                PreparedItemId = 9200,
                Code = "BTP-POS-UNIT",
                Name = "Cốt trà POS",
                BaseUnitId = UnitId,
                Active = true
            });
            ctx.Recipes.Add(new Recipe
            {
                RecipeId = 9201,
                RecipeCode = "RCP-POS-UNIT",
                Name = "Công thức cốt trà POS",
                PreparedItemId = 9200,
                Active = true,
                Status = "Active"
            });
            ctx.StoreInventories.Add(new StoreInventory
            {
                StoreId = StoreA,
                RecipeId = 9201,
                PreparedItemId = 9200,
                BtpIdentityState = BtpIdentityState.Canonical,
                QuantitySemanticsStatus = InventoryQuantitySemanticsStatus.BaseUnitConfirmed,
                AvailableQty = 11_290m,
                ReservedQty = 0m,
                LastUpdated = System.DateTime.UtcNow,
                RowVersion = new byte[] { 0 }
            });
            await ctx.SaveChangesAsync();
            var expectedUnitCode = await ctx.Units
                .Where(x => x.UnitId == UnitId)
                .Select(x => x.UnitCode)
                .SingleAsync();

            var result = await new PosBranchInventoryService(ctx)
                .GetBranchInventoryAsync(StoreA, "BTP-POS-UNIT", null, 1, 50);

            Assert.True(result.IsSuccess);
            var item = Assert.Single(result.Data!.Items);
            Assert.Equal(PosBranchInventoryService.ItemTypePreparedItem, item.ItemType);
            Assert.Equal(9200, item.ItemId);
            Assert.Equal(9201, item.LegacyRecipeId);
            Assert.Equal(expectedUnitCode, item.UnitName);
            Assert.Equal(QuantitySemanticsStatuses.BaseUnitQuantityConfirmed, item.QuantitySemanticsStatus);
            Assert.False(item.IsLegacyUnmapped);
        }

        [Fact]
        public void BranchInventoryUi_DoesNotExposeLegacyTechnicalTerminology()
        {
            var root = FindRepoRoot();
            var page = System.IO.File.ReadAllText(System.IO.Path.Combine(
                root,
                "CafeChain.Frontend",
                "src",
                "pages",
                "BranchInventory.tsx"));

            Assert.Contains("Công thức liên kết", page, System.StringComparison.Ordinal);
            Assert.DoesNotContain("Công thức legacy", page, System.StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("BTP legacy", page, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetBranchInventory_ThresholdAlwaysUnconfigured_InIssue96()
        {
            using var ctx = CreateDbContext();
            SeedCatalog(ctx);
            await ctx.SaveChangesAsync();

            var service = new PosBranchInventoryService(ctx);
            var result = await service.GetBranchInventoryAsync(StoreA, null, null, 1, 50);

            Assert.True(result.IsSuccess);
            Assert.All(result.Data!.Items, item =>
            {
                Assert.Null(item.MinStockLevel);
                Assert.False(item.ThresholdConfigured);
                Assert.Equal(
                    PosBranchInventoryService.ThresholdStatusUnconfigured,
                    item.ThresholdStatus);
            });
        }

        [Theory]
        [InlineData(-3, "Tồn âm")]
        [InlineData(0, "Hết hàng")]
        [InlineData(5, "Còn hàng")]
        public void MapQuantityStatus_NegativeZeroPositive(decimal qty, string expected)
        {
            Assert.Equal(expected, PosBranchInventoryService.MapQuantityStatus(qty));
        }

        [Fact]
        public void CalculateUsableQuantity_SubtractsReservedFromOnHand()
        {
            Assert.Equal(4m, PosBranchInventoryService.CalculateUsableQuantity(12m, 8m));
            Assert.Equal(-2m, PosBranchInventoryService.CalculateUsableQuantity(3m, 5m));
        }

        [Fact]
        public async Task GetBranchInventory_Search_MatchesIngredientAndRecipe()
        {
            using var ctx = CreateDbContext();
            SeedCatalog(ctx);
            await ctx.SaveChangesAsync();

            var service = new PosBranchInventoryService(ctx);

            var byIngredient = await service.GetBranchInventoryAsync(StoreA, "Sữa", null, 1, 50);
            Assert.True(byIngredient.IsSuccess);
            Assert.Single(byIngredient.Data!.Items);
            Assert.Equal("Ingredient", byIngredient.Data.Items[0].ItemType);

            var byRecipe = await service.GetBranchInventoryAsync(StoreA, "Syrup", null, 1, 50);
            Assert.True(byRecipe.IsSuccess);
            Assert.Single(byRecipe.Data!.Items);
            Assert.Equal("Recipe", byRecipe.Data.Items[0].ItemType);

            var byCode = await service.GetBranchInventoryAsync(StoreA, "RCP_BTP", null, 1, 50);
            Assert.True(byCode.IsSuccess);
            Assert.Single(byCode.Data!.Items);
            Assert.Equal(RecipeId, byCode.Data.Items[0].ItemId);
        }

        [Fact]
        public async Task GetBranchInventory_ItemTypeFilter_RecipeOnly()
        {
            using var ctx = CreateDbContext();
            SeedCatalog(ctx);
            await ctx.SaveChangesAsync();

            var service = new PosBranchInventoryService(ctx);
            var result = await service.GetBranchInventoryAsync(StoreA, null, "Recipe", 1, 50);

            Assert.True(result.IsSuccess);
            Assert.Single(result.Data!.Items);
            Assert.Equal("Recipe", result.Data.Items[0].ItemType);
        }

        [Fact]
        public async Task GetBranchInventory_StockStatusFilter_UsesUsableQuantity()
        {
            using var ctx = CreateDbContext();
            SeedCatalog(ctx);
            await ctx.SaveChangesAsync();

            var result = await new PosBranchInventoryService(ctx)
                .GetBranchInventoryAsync(StoreA, null, null, 1, 50, "OUT");

            Assert.True(result.IsSuccess);
            Assert.Equal(2, result.Data!.Total);
            Assert.All(result.Data.Items, item => Assert.True(item.UsableQty <= 0));
        }

        [Fact]
        public async Task Controller_Forbidden_ForUnauthorizedRole()
        {
            using var ctx = CreateDbContext();
            SeedCatalog(ctx);
            await ctx.SaveChangesAsync();

            var controller = CreateController(ctx, RoleConstants.Customer, StoreA);
            var result = await controller.GetBranchInventory();

            var status = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status403Forbidden, status.StatusCode);
        }

        [Fact]
        public async Task Controller_AllowsSalesStaff_AndScopesToClaimStore()
        {
            using var ctx = CreateDbContext();
            SeedCatalog(ctx);
            await ctx.SaveChangesAsync();

            var controller = CreateController(ctx, RoleConstants.SalesStaff, StoreA);
            var result = await controller.GetBranchInventory();

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(ok.Value);
        }

        [Fact]
        public void MapQuantityStatus_IsDisplayOnly_NoAlertSemantics()
        {
            // Guardrail: helpers only produce display labels — no LOW_STOCK codes.
            Assert.DoesNotContain("LOW", PosBranchInventoryService.MapQuantityStatus(0));
            Assert.DoesNotContain("OUT_OF_STOCK", PosBranchInventoryService.MapQuantityStatus(0));
            Assert.Equal("Hết hàng", PosBranchInventoryService.MapQuantityStatus(0));
        }

        private static POSBranchInventoryController CreateController(
            CafeChain.Data.AppDbContext ctx,
            string role,
            int storeId)
        {
            IPosBranchInventoryService service = new PosBranchInventoryService(ctx);
            var stockAlerts = new CafeChain.Application.Services.Inventories.StockAlertService(
                ctx,
                new Mock<ILogger<CafeChain.Application.Services.Inventories.StockAlertService>>().Object);
            var controller = new POSBranchInventoryController(service, stockAlerts);
            var claims = new List<Claim>
            {
                new(ClaimTypes.Role, role),
                new("StoreId", storeId.ToString()),
                new("StaffId", "1"),
            };
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
                }
            };
            return controller;
        }

        private static string FindRepoRoot()
        {
            var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
            while (dir != null && !System.IO.Directory.Exists(System.IO.Path.Combine(dir.FullName, "CafeChain")))
                dir = dir.Parent;

            return dir?.FullName
                ?? throw new System.IO.DirectoryNotFoundException("Không tìm thấy repo root.");
        }

        private static void SeedCatalog(CafeChain.Data.AppDbContext ctx)
        {
            // Units may already be seeded by EnsureCreated — only add if missing.
            if (!ctx.Units.Any(u => u.UnitId == UnitId))
            {
                ctx.Units.Add(new Unit
                {
                    UnitId = UnitId,
                    UnitCode = "ml",
                    Name = "Mililit",
                    Active = true
                });
            }

            ctx.Ingredients.Add(new Ingredient
            {
                IngredientId = IngredientId,
                Code = "MILK_T",
                Name = "Sữa tươi test",
                BaseUnitId = UnitId,
                Active = true
            });

            ctx.Recipes.Add(new Recipe
            {
                RecipeId = RecipeId,
                RecipeCode = "RCP_BTP96",
                Name = "Syrup BTP",
                Active = true,
                Status = "Active"
            });

            ctx.StoreInventories.AddRange(
                new StoreInventory
                {
                    StoreId = StoreA,
                    IngredientId = IngredientId,
                    AvailableQty = 12m,
                    ReservedQty = 1m,
                    LastUpdated = System.DateTime.UtcNow,
                    RowVersion = new byte[] { 0 }
                },
                new StoreInventory
                {
                    StoreId = StoreA,
                    RecipeId = RecipeId,
                    AvailableQty = 0m,
                    ReservedQty = 0m,
                    LastUpdated = System.DateTime.UtcNow,
                    RowVersion = new byte[] { 0 }
                },
                new StoreInventory
                {
                    StoreId = StoreA,
                    IngredientId = IngredientId + 1,
                    AvailableQty = -2m,
                    ReservedQty = 0m,
                    LastUpdated = System.DateTime.UtcNow,
                    RowVersion = new byte[] { 0 }
                },
                // Other store — must not leak
                new StoreInventory
                {
                    StoreId = StoreB,
                    IngredientId = IngredientId,
                    AvailableQty = 999m,
                    ReservedQty = 0m,
                    LastUpdated = System.DateTime.UtcNow,
                    RowVersion = new byte[] { 0 }
                });

            // Extra ingredient for negative qty row (store A)
            ctx.Ingredients.Add(new Ingredient
            {
                IngredientId = IngredientId + 1,
                Code = "SUGAR_T",
                Name = "Đường test",
                BaseUnitId = UnitId,
                Active = true
            });
        }
    }
}
