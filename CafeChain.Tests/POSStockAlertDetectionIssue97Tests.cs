using System.Linq;
using System.Threading.Tasks;
using CafeChain.Application.Constants;
using CafeChain.Application.Services.Inventories;
using CafeChain.Models.Drinks;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CafeChain.Tests.POS
{
    /// <summary>
    /// Issue #97 — Stock alert detection + duplicate guard.
    /// </summary>
    public class POSStockAlertDetectionIssue97Tests : IntegrationTestBase
    {
        private const int StoreId = 70;
        private const int IngredientId = 9701;
        private const int RecipeId = 9801;
        private const int UnitId = 1;

        [Fact]
        public async Task MinStockLevelNull_DoesNotCreateLowOrOutAlert()
        {
            using var ctx = CreateDbContext();
            SeedIngredientInventory(ctx, qty: 0m, min: null);
            await ctx.SaveChangesAsync();

            var service = CreateService(ctx);
            var result = await service.EvaluateStoreAsync(StoreId, StockAlertSources.ManualCheck);

            Assert.True(result.IsSuccess);
            Assert.Equal(0, result.Data!.CreatedCount);
            Assert.True(result.Data.SkippedUnconfiguredCount >= 1);
            Assert.Equal(0, await ctx.StockAlerts.CountAsync());
        }

        [Fact]
        public async Task AvailableQtyZero_WithMin_CreatesOutOfStock()
        {
            using var ctx = CreateDbContext();
            SeedIngredientInventory(ctx, qty: 0m, min: 10m);
            await ctx.SaveChangesAsync();

            var service = CreateService(ctx);
            var result = await service.EvaluateStoreInventoryItemAsync(
                await FirstInventoryIdAsync(ctx),
                StockAlertSources.ManualCheck);

            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.Data!.CreatedCount);

            var alert = await ctx.StockAlerts.SingleAsync();
            Assert.Equal(StockAlertTypes.OutOfStock, alert.AlertType);
            Assert.Equal(StockAlertSeverities.Urgent, alert.Severity);
            Assert.Equal(StockAlertStatuses.Open, alert.Status);
            Assert.Equal(IngredientId, alert.IngredientId);
            Assert.Null(alert.RecipeId);
        }

        [Fact]
        public async Task AvailableQtyBetweenZeroAndMin_CreatesLowStock()
        {
            using var ctx = CreateDbContext();
            SeedIngredientInventory(ctx, qty: 5m, min: 10m);
            await ctx.SaveChangesAsync();

            var service = CreateService(ctx);
            var result = await service.EvaluateStoreAsync(StoreId, StockAlertSources.Auto);

            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.Data!.CreatedCount);

            var alert = await ctx.StockAlerts.SingleAsync();
            Assert.Equal(StockAlertTypes.LowStock, alert.AlertType);
            Assert.Equal(StockAlertSeverities.Warning, alert.Severity);
        }

        [Fact]
        public async Task AvailableQtyAboveMin_DoesNotCreateAlert()
        {
            using var ctx = CreateDbContext();
            SeedIngredientInventory(ctx, qty: 50m, min: 10m);
            await ctx.SaveChangesAsync();

            var service = CreateService(ctx);
            var result = await service.EvaluateStoreAsync(StoreId, StockAlertSources.ManualCheck);

            Assert.True(result.IsSuccess);
            Assert.Equal(0, result.Data!.CreatedCount);
            Assert.Equal(0, await ctx.StockAlerts.CountAsync());
        }

        [Fact]
        public async Task DuplicateGuard_SecondEvaluate_DoesNotCreateSecondOpenAlert()
        {
            using var ctx = CreateDbContext();
            SeedIngredientInventory(ctx, qty: 3m, min: 10m);
            await ctx.SaveChangesAsync();

            var service = CreateService(ctx);
            var first = await service.EvaluateStoreAsync(StoreId, StockAlertSources.ManualCheck);
            var second = await service.EvaluateStoreAsync(StoreId, StockAlertSources.ManualCheck);

            Assert.True(first.IsSuccess);
            Assert.True(second.IsSuccess);
            Assert.Equal(1, first.Data!.CreatedCount);
            Assert.Equal(0, second.Data!.CreatedCount);
            Assert.Equal(1, await ctx.StockAlerts.CountAsync(a => a.Status == StockAlertStatuses.Open));
        }

        [Fact]
        public async Task LowStock_EscalatesToOutOfStock_OnSameAlert()
        {
            using var ctx = CreateDbContext();
            SeedIngredientInventory(ctx, qty: 4m, min: 10m);
            await ctx.SaveChangesAsync();

            var service = CreateService(ctx);
            await service.EvaluateStoreAsync(StoreId, StockAlertSources.PosSale);

            var inv = await ctx.StoreInventories.SingleAsync(i => i.StoreId == StoreId);
            inv.AvailableQty = 0m;
            await ctx.SaveChangesAsync();

            var result = await service.EvaluateStoreAsync(StoreId, StockAlertSources.PosSale);

            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.Data!.UpdatedCount);
            Assert.Equal(1, await ctx.StockAlerts.CountAsync());

            var alert = await ctx.StockAlerts.SingleAsync();
            Assert.Equal(StockAlertTypes.OutOfStock, alert.AlertType);
            Assert.Equal(StockAlertSeverities.Urgent, alert.Severity);
            Assert.Equal(StockAlertStatuses.Open, alert.Status);
        }

        [Fact]
        public async Task SupportsRecipeIdBtp_Target()
        {
            using var ctx = CreateDbContext();
            SeedRecipeInventory(ctx, qty: 2m, min: 5m);
            await ctx.SaveChangesAsync();

            var service = CreateService(ctx);
            var result = await service.EvaluateAfterInventoryChangeAsync(
                StoreId,
                ingredientId: null,
                recipeId: RecipeId,
                StockAlertSources.InventoryTransaction);

            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.Data!.CreatedCount);

            var alert = await ctx.StockAlerts.SingleAsync();
            Assert.Equal(RecipeId, alert.RecipeId);
            Assert.Null(alert.IngredientId);
            Assert.Equal(StockAlertTypes.LowStock, alert.AlertType);
        }

        [Fact]
        public async Task ReplenishAboveThreshold_ResolvesOpenAlert()
        {
            using var ctx = CreateDbContext();
            SeedIngredientInventory(ctx, qty: 2m, min: 10m);
            await ctx.SaveChangesAsync();

            var service = CreateService(ctx);
            await service.EvaluateStoreAsync(StoreId, StockAlertSources.ManualCheck);
            Assert.Equal(1, await ctx.StockAlerts.CountAsync(a => a.Status == StockAlertStatuses.Open));

            var inv = await ctx.StoreInventories.SingleAsync(i => i.StoreId == StoreId);
            inv.AvailableQty = 20m;
            await ctx.SaveChangesAsync();

            var result = await service.EvaluateStoreAsync(StoreId, StockAlertSources.ManualCheck);

            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.Data!.ResolvedCount);
            Assert.Equal(0, await ctx.StockAlerts.CountAsync(a => a.Status == StockAlertStatuses.Open));

            var resolved = await ctx.StockAlerts.SingleAsync();
            Assert.Equal(StockAlertStatuses.Resolved, resolved.Status);
            Assert.NotNull(resolved.ResolvedAt);
            Assert.Contains("replenished", resolved.ResolvedReason!, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ManualStoreCheck_ReturnsCounts()
        {
            using var ctx = CreateDbContext();
            SeedIngredientInventory(ctx, qty: 1m, min: 5m);
            SeedRecipeInventory(ctx, qty: 100m, min: null); // unconfigured skip
            await ctx.SaveChangesAsync();

            var service = CreateService(ctx);
            var result = await service.EvaluateStoreAsync(StoreId, StockAlertSources.ManualCheck);

            Assert.True(result.IsSuccess);
            Assert.Equal(StoreId, result.Data!.StoreId);
            Assert.Equal(1, result.Data.CreatedCount);
            Assert.True(result.Data.SkippedUnconfiguredCount >= 1);
            Assert.Equal(StockAlertSources.ManualCheck, result.Data.Source);
            Assert.Equal(2, result.Data.EvaluatedCount);
        }

        [Fact]
        public async Task MapThresholdStatus_ReflectsMinStockLevel()
        {
            Assert.Equal(
                "Chưa cấu hình ngưỡng tối thiểu",
                CafeChain.Application.Services.POS.PosBranchInventoryService.MapThresholdStatus(5, null));
            Assert.Equal(
                "Hết hàng",
                CafeChain.Application.Services.POS.PosBranchInventoryService.MapThresholdStatus(0, 10));
            Assert.Equal(
                "Gần hết",
                CafeChain.Application.Services.POS.PosBranchInventoryService.MapThresholdStatus(3, 10));
            Assert.Equal(
                "Bình thường",
                CafeChain.Application.Services.POS.PosBranchInventoryService.MapThresholdStatus(20, 10));
        }

        private static StockAlertService CreateService(CafeChain.Data.AppDbContext ctx)
        {
            return new StockAlertService(
                ctx,
                new Mock<ILogger<StockAlertService>>().Object);
        }

        private static async Task<int> FirstInventoryIdAsync(CafeChain.Data.AppDbContext ctx)
        {
            return await ctx.StoreInventories
                .Where(i => i.StoreId == StoreId)
                .Select(i => i.StoreInventoryId)
                .FirstAsync();
        }

        private static void SeedIngredientInventory(
            CafeChain.Data.AppDbContext ctx,
            decimal qty,
            decimal? min)
        {
            EnsureUnit(ctx);
            if (!ctx.Ingredients.Any(i => i.IngredientId == IngredientId))
            {
                ctx.Ingredients.Add(new Ingredient
                {
                    IngredientId = IngredientId,
                    Code = "ING97",
                    Name = "Ingredient #97",
                    BaseUnitId = UnitId,
                    Active = true
                });
            }

            if (!ctx.StoreInventories.Any(i =>
                    i.StoreId == StoreId && i.IngredientId == IngredientId))
            {
                ctx.StoreInventories.Add(new StoreInventory
                {
                    StoreId = StoreId,
                    IngredientId = IngredientId,
                    AvailableQty = qty,
                    ReservedQty = 0,
                    MinStockLevel = min,
                    LastUpdated = System.DateTime.UtcNow,
                    RowVersion = new byte[] { 0 }
                });
            }
            else
            {
                var row = ctx.StoreInventories.Local
                    .First(i => i.StoreId == StoreId && i.IngredientId == IngredientId);
                row.AvailableQty = qty;
                row.MinStockLevel = min;
            }
        }

        private static void SeedRecipeInventory(
            CafeChain.Data.AppDbContext ctx,
            decimal qty,
            decimal? min)
        {
            if (!ctx.Recipes.Any(r => r.RecipeId == RecipeId))
            {
                ctx.Recipes.Add(new Recipe
                {
                    RecipeId = RecipeId,
                    RecipeCode = "RCP97",
                    Name = "BTP #97",
                    Active = true,
                    Status = "Active"
                });
            }

            if (!ctx.StoreInventories.Any(i =>
                    i.StoreId == StoreId && i.RecipeId == RecipeId))
            {
                ctx.StoreInventories.Add(new StoreInventory
                {
                    StoreId = StoreId,
                    RecipeId = RecipeId,
                    AvailableQty = qty,
                    ReservedQty = 0,
                    MinStockLevel = min,
                    LastUpdated = System.DateTime.UtcNow,
                    RowVersion = new byte[] { 0 }
                });
            }
        }

        private static void EnsureUnit(CafeChain.Data.AppDbContext ctx)
        {
            if (!ctx.Units.Any(u => u.UnitId == UnitId))
            {
                ctx.Units.Add(new Unit
                {
                    UnitId = UnitId,
                    UnitCode = "g",
                    Name = "Gram",
                    Active = true
                });
            }
        }
    }
}
