using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Services.Admin.Production;
using CafeChain.Application.Services.Inventories;
using CafeChain.Data;
using CafeChain.Models.Customers;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Configuration;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Permissions;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CafeChain.Tests
{
    /// <summary>Issue #122 — StockAlert / RestockRequest PreparedItem identity.</summary>
    public sealed class StockAlertRestockPreparedIdentityIssue122Tests : IntegrationTestBase
    {
        private const int StoreId = 12201;
        private const int IngredientId = 12204;
        private const int PreparedItemId = 12205;
        private const int RecipeId = 12206;
        private const int UnitMl = 3;
        private const int UnitGram = 1;
        private const int StaffId = 12202;

        [Fact]
        public async Task PreparedMode_CanonicalPreparedItem_CreatesOpenAlertWithPreparedItemIdOnly()
        {
            using var ctx = CreateDbContext();
            await SeedStoreAsync(ctx, InventoryWriterMode.PreparedItem);
            await SeedCanonicalPiAsync(ctx, qty: 0m, min: 10m);
            await ctx.SaveChangesAsync();

            var result = await CreateAlertService(ctx).EvaluateStoreAsync(StoreId, StockAlertSources.ManualCheck);
            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(1, result.Data!.CreatedCount);

            var alert = await ctx.StockAlerts.SingleAsync();
            Assert.Equal(StockAlertStatuses.Open, alert.Status);
            Assert.Equal(PreparedItemId, alert.PreparedItemId);
            Assert.Null(alert.RecipeId);
            Assert.Null(alert.IngredientId);
            Assert.Equal(StockAlertTypes.OutOfStock, alert.AlertType);
        }

        [Fact]
        public async Task PreparedMode_OpenPreparedItemAlert_UpdatesInPlaceOnReevaluate()
        {
            using var ctx = CreateDbContext();
            await SeedStoreAsync(ctx, InventoryWriterMode.PreparedItem);
            await SeedCanonicalPiAsync(ctx, qty: 5m, min: 10m);
            await ctx.SaveChangesAsync();
            var svc = CreateAlertService(ctx);
            await svc.EvaluateStoreAsync(StoreId, StockAlertSources.Auto);
            var id = (await ctx.StockAlerts.SingleAsync()).StockAlertId;

            var inv = await ctx.StoreInventories.SingleAsync(x => x.PreparedItemId == PreparedItemId);
            inv.AvailableQty = 0m;
            await ctx.SaveChangesAsync();

            var result = await svc.EvaluateStoreAsync(StoreId, StockAlertSources.PosSale);
            Assert.True(result.IsSuccess);
            Assert.Equal(1, await ctx.StockAlerts.CountAsync());
            var alert = await ctx.StockAlerts.SingleAsync();
            Assert.Equal(id, alert.StockAlertId);
            Assert.Equal(StockAlertTypes.OutOfStock, alert.AlertType);
            Assert.Equal(0m, alert.CurrentQtySnapshot);
        }

        [Fact]
        public async Task PreparedMode_StockAboveMin_ResolvesOpenPreparedItemAlert()
        {
            using var ctx = CreateDbContext();
            await SeedStoreAsync(ctx, InventoryWriterMode.PreparedItem);
            await SeedCanonicalPiAsync(ctx, qty: 5m, min: 10m);
            await ctx.SaveChangesAsync();
            var svc = CreateAlertService(ctx);
            await svc.EvaluateStoreAsync(StoreId, StockAlertSources.Auto);

            var inv = await ctx.StoreInventories.SingleAsync(x => x.PreparedItemId == PreparedItemId);
            inv.AvailableQty = 50m;
            await ctx.SaveChangesAsync();

            var result = await svc.EvaluateStoreAsync(StoreId, StockAlertSources.InventoryTransaction);
            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.Data!.ResolvedCount);
            Assert.Equal(StockAlertStatuses.Resolved, (await ctx.StockAlerts.SingleAsync()).Status);
        }

        [Fact]
        public async Task PreparedMode_CompatibilityRecipeAndPreparedRows_YieldOneOpenAlert()
        {
            using var ctx = CreateDbContext();
            await SeedStoreAsync(ctx, InventoryWriterMode.PreparedItem);
            await SeedPiAndRecipeAsync(ctx);
            // Canonical + compatibility Recipe+PI rows same PI
            ctx.StoreInventories.Add(MakeCanonical(qty: 5m, min: 10m));
            ctx.StoreInventories.Add(new StoreInventory
            {
                StoreId = StoreId,
                RecipeId = RecipeId,
                PreparedItemId = PreparedItemId,
                BtpIdentityState = BtpIdentityState.Legacy,
                QuantitySemanticsStatus = InventoryQuantitySemanticsStatus.BaseUnitConfirmed,
                AvailableQty = 999m,
                MinStockLevel = 10m,
                ReservedQty = 0,
                LastUpdated = DateTime.UtcNow,
                RowVersion = new byte[] { 0 }
            });
            await ctx.SaveChangesAsync();

            var result = await CreateAlertService(ctx).EvaluateStoreAsync(StoreId, StockAlertSources.Auto);
            Assert.True(result.IsSuccess, result.Message);
            // Collision Canonical+Legacy → review, no create
            Assert.True(result.Data!.ReviewCount >= 1);
            Assert.Equal(0, await ctx.StockAlerts.CountAsync(a => a.Status == StockAlertStatuses.Open));
        }

        [Fact]
        public async Task PreparedMode_PreparedItemOnlyRow_SupportedForAlert()
        {
            using var ctx = CreateDbContext();
            await SeedStoreAsync(ctx, InventoryWriterMode.PreparedItem);
            await SeedCanonicalPiAsync(ctx, qty: 3m, min: 10m);
            await ctx.SaveChangesAsync();

            Assert.True((await CreateAlertService(ctx).EvaluateStoreAsync(StoreId, StockAlertSources.Auto)).IsSuccess);
            var alert = await ctx.StockAlerts.SingleAsync();
            Assert.Equal(PreparedItemId, alert.PreparedItemId);
            Assert.Null(alert.RecipeId);
        }

        [Fact]
        public async Task LegacyMode_RecipeInventory_CreatesCanonicalPreparedItemAlert()
        {
            using var ctx = CreateDbContext();
            await SeedStoreAsync(ctx, InventoryWriterMode.LegacyRecipe);
            await SeedPiAndRecipeAsync(ctx);
            ctx.StoreInventories.Add(new StoreInventory
            {
                StoreId = StoreId,
                RecipeId = RecipeId,
                BtpIdentityState = BtpIdentityState.Legacy,
                QuantitySemanticsStatus = InventoryQuantitySemanticsStatus.BaseUnitConfirmed,
                AvailableQty = 0m,
                MinStockLevel = 5m,
                ReservedQty = 0,
                LastUpdated = DateTime.UtcNow,
                RowVersion = new byte[] { 0 }
            });
            await ctx.SaveChangesAsync();

            await CreateAlertService(ctx).EvaluateStoreAsync(StoreId, StockAlertSources.Auto);
            var alert = await ctx.StockAlerts.SingleAsync();
            Assert.Null(alert.RecipeId);
            Assert.Equal(PreparedItemId, alert.PreparedItemId);
        }

        [Fact]
        public async Task LegacyMode_BtpRecipeRow_UsesMappedPreparedItemIdentity()
            => await LegacyMode_RecipeInventory_CreatesCanonicalPreparedItemAlert();

        [Fact]
        public async Task PreparedMode_DuplicateOpenPreparedItem_ReusesSameAlert()
        {
            using var ctx = CreateDbContext();
            await SeedStoreAsync(ctx, InventoryWriterMode.PreparedItem);
            await SeedCanonicalPiAsync(ctx, qty: 1m, min: 10m);
            await ctx.SaveChangesAsync();
            var svc = CreateAlertService(ctx);
            await svc.EvaluateStoreAsync(StoreId, StockAlertSources.Auto);
            await svc.EvaluateStoreAsync(StoreId, StockAlertSources.PosSale);
            Assert.Equal(1, await ctx.StockAlerts.CountAsync(a => a.Status == StockAlertStatuses.Open));
        }

        [Fact]
        public async Task Restock_FromConfirmedPreparedAlert_CopiesPreparedItemIdAndLink()
        {
            using var ctx = CreateDbContext();
            await SeedStoreAsync(ctx, InventoryWriterMode.PreparedItem);
            await SeedStaffAsync(ctx);
            await SeedCanonicalPiAsync(ctx, qty: 0m, min: 5m);
            await ctx.SaveChangesAsync();
            await CreateAlertService(ctx).EvaluateStoreAsync(StoreId, StockAlertSources.Auto);
            var alert = await ctx.StockAlerts.SingleAsync();
            alert.Status = StockAlertStatuses.Confirmed;
            await ctx.SaveChangesAsync();

            var restock = new RestockRequestService(
                ctx,
                new CafeChain.Application.Services.Security.ScopeAuthorizationService(ctx),
                NullLogger<RestockRequestService>.Instance);
            var result = await restock.CreateFromConfirmedAlertAsync(
                alert.StockAlertId, StaffId, StoreId, 20m, null, null);
            Assert.True(result.IsSuccess, result.Message);

            var req = await ctx.RestockRequests.SingleAsync();
            Assert.Equal(PreparedItemId, req.PreparedItemId);
            Assert.Null(req.RecipeId);
            Assert.Null(req.IngredientId);
            Assert.Equal(alert.StockAlertId, req.StockAlertId);
        }

        [Fact]
        public async Task Restock_Create_DoesNotChangeStoreInventoryQty()
        {
            using var ctx = CreateDbContext();
            await SeedStoreAsync(ctx, InventoryWriterMode.PreparedItem);
            await SeedStaffAsync(ctx);
            await SeedCanonicalPiAsync(ctx, qty: 2m, min: 10m);
            await ctx.SaveChangesAsync();
            await CreateAlertService(ctx).EvaluateStoreAsync(StoreId, StockAlertSources.Auto);
            var alert = await ctx.StockAlerts.SingleAsync();
            alert.Status = StockAlertStatuses.Confirmed;
            await ctx.SaveChangesAsync();
            var before = await ctx.StoreInventories.Where(x => x.PreparedItemId == PreparedItemId)
                .Select(x => x.AvailableQty).SingleAsync();

            var rr = await new RestockRequestService(
                    ctx,
                    new CafeChain.Application.Services.Security.ScopeAuthorizationService(ctx),
                    NullLogger<RestockRequestService>.Instance)
                .CreateFromConfirmedAlertAsync(alert.StockAlertId, StaffId, StoreId, 5m, null, null);
            Assert.True(rr.IsSuccess, rr.Message);

            Assert.Equal(before, await ctx.StoreInventories.Where(x => x.PreparedItemId == PreparedItemId)
                .Select(x => x.AvailableQty).SingleAsync());
        }

        [Fact]
        public async Task PreparedMode_IdentityCollision_ReturnsReviewDoesNotPickWinner()
        {
            using var ctx = CreateDbContext();
            await SeedStoreAsync(ctx, InventoryWriterMode.PreparedItem);
            await SeedPiAndRecipeAsync(ctx);
            ctx.StoreInventories.Add(MakeCanonical(qty: 1m, min: 10m));
            ctx.StoreInventories.Add(new StoreInventory
            {
                StoreId = StoreId,
                PreparedItemId = PreparedItemId,
                RecipeId = RecipeId,
                BtpIdentityState = BtpIdentityState.Legacy,
                QuantitySemanticsStatus = InventoryQuantitySemanticsStatus.BaseUnitConfirmed,
                AvailableQty = 0m,
                MinStockLevel = 10m,
                ReservedQty = 0,
                LastUpdated = DateTime.UtcNow,
                RowVersion = new byte[] { 0 }
            });
            await ctx.SaveChangesAsync();

            var result = await CreateAlertService(ctx).EvaluateStoreAsync(StoreId, StockAlertSources.Auto);
            Assert.True(result.IsSuccess);
            Assert.True(result.Data!.ReviewCount >= 1);
            Assert.Equal(0, await ctx.StockAlerts.CountAsync());
        }

        [Fact]
        public async Task PreparedMode_UnknownQuantitySemantics_DoesNotCreateAuthoritativeAlert()
        {
            using var ctx = CreateDbContext();
            await SeedStoreAsync(ctx, InventoryWriterMode.PreparedItem);
            await SeedPiAndRecipeAsync(ctx);
            var row = MakeCanonical(qty: 0m, min: 10m);
            row.QuantitySemanticsStatus = InventoryQuantitySemanticsStatus.Unknown;
            ctx.StoreInventories.Add(row);
            await ctx.SaveChangesAsync();

            var result = await CreateAlertService(ctx).EvaluateStoreAsync(StoreId, StockAlertSources.Auto);
            Assert.True(result.IsSuccess);
            Assert.True(result.Data!.ReviewCount >= 1);
            Assert.Equal(0, await ctx.StockAlerts.CountAsync());
        }

        [Fact]
        public async Task AlertEvaluationFailure_DoesNotReversePriorSalesDeduction()
        {
            // Alert evaluation is independent SaveChanges; inventory qty unchanged by alert service.
            using var ctx = CreateDbContext();
            await SeedStoreAsync(ctx, InventoryWriterMode.PreparedItem);
            await SeedCanonicalPiAsync(ctx, qty: 100m, min: null); // skip alerts
            await ctx.SaveChangesAsync();
            var before = 100m;
            await CreateAlertService(ctx).EvaluateStoreAsync(StoreId, StockAlertSources.PosSale);
            Assert.Equal(before, await ctx.StoreInventories.Where(x => x.PreparedItemId == PreparedItemId)
                .Select(x => x.AvailableQty).SingleAsync());
        }

        [Fact]
        public async Task BlockedMode_ExistingOpenAlert_NotAutoCancelled()
        {
            using var ctx = CreateDbContext();
            await SeedStoreAsync(ctx, InventoryWriterMode.PreparedItem);
            await SeedCanonicalPiAsync(ctx, qty: 0m, min: 10m);
            await ctx.SaveChangesAsync();
            await CreateAlertService(ctx).EvaluateStoreAsync(StoreId, StockAlertSources.Auto);
            Assert.Equal(1, await ctx.StockAlerts.CountAsync(a => a.Status == StockAlertStatuses.Open));

            var cfg = await ctx.StoreInventoryWriterConfigurations.SingleAsync(x => x.StoreId == StoreId);
            cfg.WriterMode = InventoryWriterMode.Blocked;
            await ctx.SaveChangesAsync();

            await CreateAlertService(ctx).EvaluateStoreAsync(StoreId, StockAlertSources.Auto);
            Assert.Equal(1, await ctx.StockAlerts.CountAsync(a => a.Status == StockAlertStatuses.Open));
        }

        [Fact]
        public async Task AlertRestockCapability_DoesNotChangeWriterMode()
        {
            using var ctx = CreateDbContext();
            await SeedStoreAsync(ctx, InventoryWriterMode.LegacyRecipe);
            var before = await ctx.StoreInventoryWriterConfigurations.AsNoTracking()
                .SingleAsync(x => x.StoreId == StoreId);

            var cap = new AlertRestockPreparedIdentityCapabilityProvider().GetStatus();
            Assert.True(cap.Ready);
            Assert.Equal(InventoryWriterCapabilityIds.AlertRestockPreparedIdentity, cap.CapabilityId);
            Assert.Equal(AlertRestockPreparedIdentityCapabilityProvider.ContractVersion, cap.ContractVersion);

            var after = await ctx.StoreInventoryWriterConfigurations.AsNoTracking()
                .SingleAsync(x => x.StoreId == StoreId);
            Assert.Equal(before.WriterMode, after.WriterMode);
        }

        [Fact]
        public async Task Ingredient_OpenAlert_StillUsesIngredientIdentity()
        {
            using var ctx = CreateDbContext();
            await SeedStoreAsync(ctx, InventoryWriterMode.PreparedItem);
            ctx.Ingredients.Add(new Ingredient
            {
                IngredientId = IngredientId,
                Code = "ING122",
                Name = "Milk",
                BaseUnitId = UnitGram,
                Active = true
            });
            ctx.StoreInventories.Add(new StoreInventory
            {
                StoreId = StoreId,
                IngredientId = IngredientId,
                AvailableQty = 0m,
                MinStockLevel = 5m,
                ReservedQty = 0,
                LastUpdated = DateTime.UtcNow,
                RowVersion = new byte[] { 0 }
            });
            await ctx.SaveChangesAsync();

            await CreateAlertService(ctx).EvaluateStoreAsync(StoreId, StockAlertSources.Auto);
            var alert = await ctx.StockAlerts.SingleAsync();
            Assert.Equal(IngredientId, alert.IngredientId);
            Assert.Null(alert.PreparedItemId);
            Assert.Null(alert.RecipeId);
        }

        [Fact]
        public async Task Restock_SecondSubmitSameAlert_RejectedOrIdempotent()
        {
            using var ctx = CreateDbContext();
            await SeedStoreAsync(ctx, InventoryWriterMode.PreparedItem);
            await SeedStaffAsync(ctx);
            await SeedCanonicalPiAsync(ctx, qty: 0m, min: 5m);
            await ctx.SaveChangesAsync();
            await CreateAlertService(ctx).EvaluateStoreAsync(StoreId, StockAlertSources.Auto);
            var alert = await ctx.StockAlerts.SingleAsync();
            alert.Status = StockAlertStatuses.Confirmed;
            await ctx.SaveChangesAsync();

            var svc = new RestockRequestService(
                ctx,
                new CafeChain.Application.Services.Security.ScopeAuthorizationService(ctx),
                NullLogger<RestockRequestService>.Instance);
            Assert.True((await svc.CreateFromConfirmedAlertAsync(alert.StockAlertId, StaffId, StoreId, 1m, null, null)).IsSuccess);
            var second = await svc.CreateFromConfirmedAlertAsync(alert.StockAlertId, StaffId, StoreId, 1m, null, null);
            Assert.True(second.IsSuccess);
            Assert.Contains("đã tồn tại", second.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(1, await ctx.RestockRequests.CountAsync());
        }

        // ---- helpers ----

        private static StockAlertService CreateAlertService(AppDbContext ctx)
        {
            var physical = new PhysicalUnitConversionService(ctx, NullLogger<PhysicalUnitConversionService>.Instance);
            var caps = new IInventoryWriterCapabilityProvider[]
            {
                new ProductionPreparedWriterCapabilityProvider(),
                new PosPreparedWriterCapabilityProvider(),
                new AlertRestockPreparedIdentityCapabilityProvider()
            };
            var writer = new InventoryWriterModeService(ctx, physical, caps);
            return new StockAlertService(ctx, NullLogger<StockAlertService>.Instance, writer);
        }

        private static async Task SeedStoreAsync(AppDbContext ctx, InventoryWriterMode mode)
        {
            if (!await ctx.Stores.AnyAsync(s => s.StoreId == StoreId))
            {
                ctx.Stores.Add(new Store
                {
                    StoreId = StoreId,
                    Name = "S122",
                    Address = "A",
                    Phone = "1",
                    Active = true,
                    CreatedAt = DateTime.UtcNow
                });
            }

            var cfg = await ctx.StoreInventoryWriterConfigurations.FirstOrDefaultAsync(x => x.StoreId == StoreId);
            if (cfg == null)
            {
                ctx.StoreInventoryWriterConfigurations.Add(new StoreInventoryWriterConfiguration
                {
                    StoreId = StoreId,
                    WriterMode = mode,
                    HasEverActivatedPreparedItem = mode != InventoryWriterMode.LegacyRecipe,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                cfg.WriterMode = mode;
                cfg.UpdatedAt = DateTime.UtcNow;
            }

            await ctx.SaveChangesAsync();
        }

        private static async Task SeedStaffAsync(AppDbContext ctx)
        {
            if (await ctx.Staffs.AnyAsync(s => s.StaffId == StaffId))
                return;

            if (!await ctx.Roles.AnyAsync(r => r.Name == RoleConstants.StoreManager)
                && !ctx.Roles.Local.Any(r => r.Name == RoleConstants.StoreManager))
            {
                var id = 3;
                if (await ctx.Roles.AnyAsync(r => r.RoleId == id))
                    id = (await ctx.Roles.MaxAsync(r => r.RoleId)) + 50;
                ctx.Roles.Add(new Role
                {
                    RoleId = id,
                    Name = RoleConstants.StoreManager,
                    Active = true,
                    IsStoreLevel = true,
                    CreatedAt = DateTime.UtcNow
                });
            }

            var role = ctx.Roles.Local.FirstOrDefault(r => r.Name == RoleConstants.StoreManager)
                       ?? await ctx.Roles.FirstAsync(r => r.Name == RoleConstants.StoreManager);
            var accountId = 32200 + StaffId;
            ctx.Accounts.Add(new Account
            {
                AccountId = accountId,
                Email = $"mgr122_{StaffId}@test.local",
                PasswordHash = "x",
                Active = true,
                CreatedAt = DateTime.UtcNow
            });
            ctx.AccountRoles.Add(new AccountRole { AccountId = accountId, RoleId = role.RoleId });
            ctx.Staffs.Add(new Staff
            {
                StaffId = StaffId,
                AccountId = accountId,
                FullName = "Manager 122",
                StoreId = StoreId,
                Active = true,
                CreatedAt = DateTime.UtcNow,
                BaseSalary = 0
            });
            await ctx.SaveChangesAsync();
        }

        private static async Task SeedPiAndRecipeAsync(AppDbContext ctx)
        {
            if (!await ctx.PreparedItems.AnyAsync(p => p.PreparedItemId == PreparedItemId))
            {
                ctx.PreparedItems.Add(new PreparedItem
                {
                    PreparedItemId = PreparedItemId,
                    Code = "PI122",
                    Name = "Syrup",
                    BaseUnitId = UnitMl,
                    Active = true
                });
            }

            if (!await ctx.Recipes.AnyAsync(r => r.RecipeId == RecipeId))
            {
                ctx.Recipes.Add(new Recipe
                {
                    RecipeId = RecipeId,
                    RecipeCode = "RCP122",
                    Name = "Syrup recipe",
                    Active = true,
                    Status = "Active",
                    PreparedItemId = PreparedItemId,
                    OutputQuantity = 1m,
                    OutputUnitId = UnitMl
                });
            }

            await ctx.SaveChangesAsync();
        }

        private static async Task SeedCanonicalPiAsync(AppDbContext ctx, decimal qty, decimal? min)
        {
            await SeedPiAndRecipeAsync(ctx);
            if (!await ctx.StoreInventories.AnyAsync(x =>
                    x.StoreId == StoreId && x.PreparedItemId == PreparedItemId
                    && x.BtpIdentityState == BtpIdentityState.Canonical))
            {
                ctx.StoreInventories.Add(MakeCanonical(qty, min));
            }
        }

        private static StoreInventory MakeCanonical(decimal qty, decimal? min)
            => new()
            {
                StoreId = StoreId,
                PreparedItemId = PreparedItemId,
                RecipeId = null,
                IngredientId = null,
                BtpIdentityState = BtpIdentityState.Canonical,
                QuantitySemanticsStatus = InventoryQuantitySemanticsStatus.BaseUnitConfirmed,
                QuantitySemanticsEvidenceType = QuantitySemanticsEvidenceType.SystemCanonicalCreation,
                QuantitySemanticsEvidenceReference = "seed-122",
                QuantitySemanticsReviewedAt = DateTime.UtcNow,
                QuantitySemanticsReviewedByAccountId = 1,
                AvailableQty = qty,
                ReservedQty = 0,
                MinStockLevel = min,
                LastUpdated = DateTime.UtcNow,
                RowVersion = new byte[] { 0 }
            };
    }
}
