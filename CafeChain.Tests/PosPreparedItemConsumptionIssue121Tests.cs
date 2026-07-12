using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Services.Admin.Production;
using CafeChain.Application.Services.Admin.Recipes;
using CafeChain.Application.Services.Inventories;
using CafeChain.Application.Services.Inventory;
using CafeChain.Data;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Configuration;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Orders;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CafeChain.Tests
{
    /// <summary>Issue #121 — POS PreparedItem consumption writer.</summary>
    public sealed class PosPreparedItemConsumptionIssue121Tests : IntegrationTestBase
    {
        private const int StoreId = 12101;
        private const int DrinkId = 12110;
        private const int SizeId = 2;
        private const int ToppingId = 12120;
        private const int IngredientId = 12104;
        private const int PreparedItemId = 12105;
        private const int ParentRecipeId = 12103;
        private const int ChildRecipeId = 12106;
        private const int ChildRecipeIdB = 12107;
        private const int ToppingRecipeId = 12108;
        private const int UnitGram = 1;
        private const int UnitMl = 3;
        private const int UnitL = 4;

        [Fact]
        public async Task PreparedMode_IngredientOnly_DeductsIngredientBaseUnit()
        {
            using var ctx = CreateDbContext();
            await SeedStoreModeAsync(ctx, InventoryWriterMode.PreparedItem);
            await SeedIngredientOnlyCatalogAsync(ctx);
            SeedPaidOrder(ctx, 9101);
            await ctx.SaveChangesAsync();

            var result = await CreateService(ctx).DeductStockForCommittedOrderAsync(
                SoldItems(), StoreId, 9101);
            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(4900m, await IngredientQty(ctx));
            Assert.Equal(1, await ctx.InventoryTransactions.CountAsync(t => t.ReferenceOrderId == 9101));
        }

        [Fact]
        public async Task PreparedMode_ChildPreparedItem_DeductsCanonicalBaseUnit()
        {
            using var ctx = CreateDbContext();
            await SeedPreparedCatalogAsync(ctx, childQtyLitresOnBom: 0.5m); // 0.5 L → 500 ml
            SeedPaidOrder(ctx, 9102);
            await ctx.SaveChangesAsync();

            var result = await CreateService(ctx).DeductStockForCommittedOrderAsync(
                SoldItems(), StoreId, 9102);
            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(9500m, await CanonicalPiQty(ctx)); // 10000 - 500
            Assert.False(await ctx.StoreInventories.AnyAsync(x =>
                x.StoreId == StoreId && x.RecipeId == ChildRecipeId && x.PreparedItemId == null));
        }

        [Fact]
        public async Task PreparedMode_MixedIngredientAndPreparedItem_DeductsBoth()
        {
            using var ctx = CreateDbContext();
            await SeedPreparedCatalogAsync(ctx, includeIngredientOnParent: true, childQtyLitresOnBom: 0.1m);
            SeedPaidOrder(ctx, 9103);
            await ctx.SaveChangesAsync();

            var result = await CreateService(ctx).DeductStockForCommittedOrderAsync(
                SoldItems(), StoreId, 9103);
            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(4900m, await IngredientQty(ctx));
            Assert.Equal(9900m, await CanonicalPiQty(ctx));
            Assert.Equal(2, await ctx.InventoryTransactions.CountAsync(t => t.ReferenceOrderId == 9103));
        }

        [Fact]
        public async Task PreparedMode_UsesExactChildRecipePreparedItemId()
        {
            using var ctx = CreateDbContext();
            await SeedPreparedCatalogAsync(ctx, childQtyLitresOnBom: 0.1m);
            // Second active child for another PI must not be used
            ctx.PreparedItems.Add(new PreparedItem
            {
                PreparedItemId = 12999,
                Code = "PI-OTHER",
                Name = "Other",
                BaseUnitId = UnitMl,
                Active = true
            });
            ctx.Recipes.Add(new Recipe
            {
                RecipeId = 12998,
                RecipeCode = "CHILD-OTHER",
                Name = "Other child",
                Active = true,
                Status = "Active",
                PreparedItemId = 12999,
                OutputQuantity = 1m,
                OutputUnitId = UnitL
            });
            await ctx.SaveChangesAsync();
            SeedPaidOrder(ctx, 9104);
            await ctx.SaveChangesAsync();

            var result = await CreateService(ctx).DeductStockForCommittedOrderAsync(
                SoldItems(), StoreId, 9104);
            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(9900m, await CanonicalPiQty(ctx));
            Assert.Equal(0, await ctx.StoreInventories.CountAsync(x => x.PreparedItemId == 12999));
        }

        [Fact]
        public async Task PreparedMode_DoesNotSubstituteLatestActiveChildRecipe()
        {
            using var ctx = CreateDbContext();
            await SeedPreparedCatalogAsync(ctx, childQtyLitresOnBom: 0.1m);
            // Archive exact child mapping still used via ChildRecipeId on parent detail
            var child = await ctx.Recipes.SingleAsync(r => r.RecipeId == ChildRecipeId);
            child.Active = false;
            child.Status = "Archived";
            // Newer active child same PI
            ctx.Recipes.Add(new Recipe
            {
                RecipeId = 12800,
                RecipeCode = "CHILD-NEW",
                Name = "New active",
                Active = true,
                Status = "Active",
                PreparedItemId = PreparedItemId,
                OutputQuantity = 9m,
                OutputUnitId = UnitL
            });
            await ctx.SaveChangesAsync();
            SeedPaidOrder(ctx, 9105);
            await ctx.SaveChangesAsync();

            var result = await CreateService(ctx).DeductStockForCommittedOrderAsync(
                SoldItems(), StoreId, 9105);
            Assert.True(result.IsSuccess, result.Message);
            // Still 0.1 L = 100 ml from archived exact child detail qty, not 9 L
            Assert.Equal(9900m, await CanonicalPiQty(ctx));
            var tx = await ctx.InventoryTransactions.SingleAsync(t =>
                t.ReferenceOrderId == 9105
                && t.StoreInventory!.PreparedItemId == PreparedItemId);
            Assert.Equal(ChildRecipeId, tx.SourceRecipeId);
        }

        [Fact]
        public async Task PreparedMode_ArchivedExactChildRecipe_UsesCapturedMapping()
        {
            await PreparedMode_DoesNotSubstituteLatestActiveChildRecipe();
        }

        [Fact]
        public async Task PreparedMode_UnmappedChildRecipe_FailsClosedNoMutation()
        {
            using var ctx = CreateDbContext();
            await SeedPreparedCatalogAsync(ctx, childQtyLitresOnBom: 0.1m, mapChildPreparedItem: false);
            SeedPaidOrder(ctx, 9106);
            await ctx.SaveChangesAsync();
            var before = await CanonicalPiQty(ctx);

            var result = await CreateService(ctx).DeductStockForCommittedOrderAsync(
                SoldItems(), StoreId, 9106);
            Assert.False(result.IsSuccess);
            Assert.Equal(before, await CanonicalPiQty(ctx));
            Assert.Equal(0, await ctx.InventoryTransactions.CountAsync(t => t.ReferenceOrderId == 9106));
        }

        [Fact]
        public async Task PreparedMode_InactivePreparedItem_FailsClosed()
        {
            using var ctx = CreateDbContext();
            await SeedPreparedCatalogAsync(ctx, childQtyLitresOnBom: 0.1m);
            (await ctx.PreparedItems.SingleAsync(p => p.PreparedItemId == PreparedItemId)).Active = false;
            SeedPaidOrder(ctx, 9107);
            await ctx.SaveChangesAsync();

            var result = await CreateService(ctx).DeductStockForCommittedOrderAsync(
                SoldItems(), StoreId, 9107);
            Assert.False(result.IsSuccess);
            Assert.Equal(0, await ctx.InventoryTransactions.CountAsync(t => t.ReferenceOrderId == 9107));
        }

        [Fact]
        public async Task PreparedMode_InvalidChildOutputContract_FailsClosed()
        {
            using var ctx = CreateDbContext();
            await SeedPreparedCatalogAsync(ctx, childQtyLitresOnBom: 0.1m);
            var child = await ctx.Recipes.SingleAsync(r => r.RecipeId == ChildRecipeId);
            child.OutputQuantity = null;
            child.OutputUnitId = null;
            SeedPaidOrder(ctx, 9108);
            await ctx.SaveChangesAsync();

            var result = await CreateService(ctx).DeductStockForCommittedOrderAsync(
                SoldItems(), StoreId, 9108);
            Assert.False(result.IsSuccess);
            Assert.Equal(0, await ctx.InventoryTransactions.CountAsync(t => t.ReferenceOrderId == 9108));
        }

        [Fact]
        public async Task PreparedMode_MissingPhysicalConversion_FailsClosed()
        {
            using var ctx = CreateDbContext();
            await SeedPreparedCatalogAsync(ctx, childQtyLitresOnBom: 0.1m, detailUnitId: UnitGram);
            // gram → ml incompatible
            SeedPaidOrder(ctx, 9109);
            await ctx.SaveChangesAsync();

            var result = await CreateService(ctx).DeductStockForCommittedOrderAsync(
                SoldItems(), StoreId, 9109);
            Assert.False(result.IsSuccess);
            Assert.Equal(0, await ctx.InventoryTransactions.CountAsync(t => t.ReferenceOrderId == 9109));
        }

        [Fact]
        public async Task PreparedMode_IncompatibleUnitDimension_FailsClosed()
            => await PreparedMode_MissingPhysicalConversion_FailsClosed();

        [Fact]
        public async Task PreparedMode_ReusesResolverAcceptedInventoryIdentity()
        {
            using var ctx = CreateDbContext();
            await SeedPreparedCatalogAsync(ctx, childQtyLitresOnBom: 0.1m);
            var id = await ctx.StoreInventories
                .Where(x => x.PreparedItemId == PreparedItemId && x.BtpIdentityState == BtpIdentityState.Canonical)
                .Select(x => x.StoreInventoryId)
                .SingleAsync();
            SeedPaidOrder(ctx, 9110);
            await ctx.SaveChangesAsync();

            await CreateService(ctx).DeductStockForCommittedOrderAsync(SoldItems(), StoreId, 9110);
            Assert.Equal(1, await ctx.StoreInventories.CountAsync(x =>
                x.StoreId == StoreId && x.PreparedItemId == PreparedItemId && x.BtpIdentityState == BtpIdentityState.Canonical));
            Assert.Equal(id, await ctx.InventoryTransactions
                .Where(t => t.ReferenceOrderId == 9110)
                .Select(t => t.StoreInventoryId)
                .SingleAsync());
        }

        [Fact]
        public async Task PreparedMode_MissingCanonicalInventory_FailsClosedWithoutCreation()
        {
            using var ctx = CreateDbContext();
            await SeedPreparedCatalogAsync(ctx, childQtyLitresOnBom: 0.1m, seedCanonical: false);
            SeedPaidOrder(ctx, 9111);
            await ctx.SaveChangesAsync();

            var result = await CreateService(ctx).DeductStockForCommittedOrderAsync(
                SoldItems(), StoreId, 9111);
            Assert.False(result.IsSuccess);
            Assert.Equal(0, await ctx.StoreInventories.CountAsync(x =>
                x.StoreId == StoreId && x.PreparedItemId == PreparedItemId));
            Assert.Equal(0, await ctx.InventoryTransactions.CountAsync(t => t.ReferenceOrderId == 9111));
        }

        [Fact]
        public async Task PreparedMode_LegacyRecipeRow_NotUsedAsIdentity()
        {
            using var ctx = CreateDbContext();
            await SeedPreparedCatalogAsync(ctx, childQtyLitresOnBom: 0.1m, seedCanonical: false);
            ctx.StoreInventories.Add(new StoreInventory
            {
                StoreId = StoreId,
                RecipeId = ChildRecipeId,
                AvailableQty = 999m,
                ReservedQty = 0,
                LastUpdated = DateTime.UtcNow,
                RowVersion = new byte[] { 0 }
            });
            SeedPaidOrder(ctx, 9112);
            await ctx.SaveChangesAsync();

            var result = await CreateService(ctx).DeductStockForCommittedOrderAsync(
                SoldItems(), StoreId, 9112);
            Assert.False(result.IsSuccess);
            Assert.Equal(999m, await ctx.StoreInventories
                .Where(x => x.RecipeId == ChildRecipeId)
                .Select(x => x.AvailableQty)
                .SingleAsync());
        }

        [Fact]
        public async Task PreparedMode_SupersededOnly_FailsClosed()
        {
            using var ctx = CreateDbContext();
            await SeedPreparedCatalogAsync(ctx, childQtyLitresOnBom: 0.1m, seedCanonical: false);
            ctx.StoreInventories.Add(new StoreInventory
            {
                StoreId = StoreId,
                PreparedItemId = PreparedItemId,
                RecipeId = null,
                BtpIdentityState = BtpIdentityState.Superseded,
                QuantitySemanticsStatus = InventoryQuantitySemanticsStatus.BaseUnitConfirmed,
                AvailableQty = 5000m,
                ReservedQty = 0,
                LastUpdated = DateTime.UtcNow,
                RowVersion = new byte[] { 0 }
            });
            SeedPaidOrder(ctx, 9113);
            await ctx.SaveChangesAsync();

            Assert.False((await CreateService(ctx).DeductStockForCommittedOrderAsync(
                SoldItems(), StoreId, 9113)).IsSuccess);
        }

        [Fact]
        public async Task PreparedMode_UnknownQuantitySemantics_FailsClosed()
        {
            using var ctx = CreateDbContext();
            await SeedPreparedCatalogAsync(ctx, childQtyLitresOnBom: 0.1m, seedCanonical: false);
            ctx.StoreInventories.Add(MakeCanonical(available: 5000m, semantics: InventoryQuantitySemanticsStatus.Unknown));
            SeedPaidOrder(ctx, 9114);
            await ctx.SaveChangesAsync();

            Assert.False((await CreateService(ctx).DeductStockForCommittedOrderAsync(
                SoldItems(), StoreId, 9114)).IsSuccess);
        }

        [Fact]
        public async Task PreparedMode_IdentityCollision_FailsClosed()
        {
            using var ctx = CreateDbContext();
            await SeedPreparedCatalogAsync(ctx, childQtyLitresOnBom: 0.1m, seedCanonical: false);
            // Collision = Canonical + Legacy sharing PreparedItemId (unique index prevents two Canonical).
            ctx.StoreInventories.Add(MakeCanonical(available: 5000m));
            ctx.StoreInventories.Add(new StoreInventory
            {
                StoreId = StoreId,
                PreparedItemId = PreparedItemId,
                RecipeId = ChildRecipeId,
                BtpIdentityState = BtpIdentityState.Legacy,
                QuantitySemanticsStatus = InventoryQuantitySemanticsStatus.BaseUnitConfirmed,
                AvailableQty = 1000m,
                ReservedQty = 0,
                LastUpdated = DateTime.UtcNow,
                RowVersion = new byte[] { 0 }
            });
            SeedPaidOrder(ctx, 9115);
            await ctx.SaveChangesAsync();

            Assert.False((await CreateService(ctx).DeductStockForCommittedOrderAsync(
                SoldItems(), StoreId, 9115)).IsSuccess);
        }

        [Fact]
        public async Task PreparedMode_DuplicateIngredientLines_AggregatesToOneMovement()
        {
            using var ctx = CreateDbContext();
            await SeedStoreModeAsync(ctx, InventoryWriterMode.PreparedItem);
            await SeedIngredientOnlyCatalogAsync(ctx);
            // Second detail same ingredient (allowed by TestDbContext)
            ctx.RecipeDetails.Add(new RecipeDetail
            {
                RecipeId = ParentRecipeId,
                IngredientId = IngredientId,
                Quantity = 50m,
                UnitId = UnitGram
            });
            SeedPaidOrder(ctx, 9116);
            await ctx.SaveChangesAsync();

            var result = await CreateService(ctx).DeductStockForCommittedOrderAsync(
                SoldItems(), StoreId, 9116);
            Assert.True(result.IsSuccess, result.Message);
            // 100 + 50 = 150
            Assert.Equal(4850m, await IngredientQty(ctx));
            Assert.Equal(1, await ctx.InventoryTransactions.CountAsync(t => t.ReferenceOrderId == 9116));
            Assert.Equal(150m, await ctx.InventoryTransactions
                .Where(t => t.ReferenceOrderId == 9116)
                .Select(t => t.Quantity)
                .SingleAsync());
        }

        [Fact]
        public async Task PreparedMode_DuplicateChildPreparedItemPaths_AggregatesToOneMovement()
        {
            using var ctx = CreateDbContext();
            await SeedPreparedCatalogAsync(ctx, childQtyLitresOnBom: 0.1m);
            // Second child recipe same PI (archived to avoid one-active unique if any)
            ctx.Recipes.Add(new Recipe
            {
                RecipeId = ChildRecipeIdB,
                RecipeCode = "CHILD-B",
                Name = "Child B",
                Active = false,
                Status = "Archived",
                PreparedItemId = PreparedItemId,
                OutputQuantity = 1m,
                OutputUnitId = UnitL
            });
            ctx.RecipeDetails.Add(new RecipeDetail
            {
                RecipeId = ParentRecipeId,
                ChildRecipeId = ChildRecipeIdB,
                Quantity = 0.2m,
                UnitId = UnitL
            });
            SeedPaidOrder(ctx, 9117);
            await ctx.SaveChangesAsync();

            var result = await CreateService(ctx).DeductStockForCommittedOrderAsync(
                SoldItems(), StoreId, 9117);
            Assert.True(result.IsSuccess, result.Message);
            // 0.1 + 0.2 L = 300 ml
            Assert.Equal(9700m, await CanonicalPiQty(ctx));
            // Two ledger rows (different SourceRecipeId) or one if same — we use per SourceRecipeId
            Assert.Equal(2, await ctx.InventoryTransactions.CountAsync(t => t.ReferenceOrderId == 9117));
            var qtySum = (await ctx.InventoryTransactions
                .Where(t => t.ReferenceOrderId == 9117)
                .Select(t => t.Quantity)
                .ToListAsync()).Sum();
            Assert.Equal(300m, qtySum);
        }

        [Fact]
        public async Task PreparedMode_MultipleOrderItems_SameInventory_Aggregates()
        {
            using var ctx = CreateDbContext();
            await SeedIngredientOnlyCatalogAsync(ctx);
            await SeedStoreModeAsync(ctx, InventoryWriterMode.PreparedItem);
            SeedPaidOrder(ctx, 9118);
            await ctx.SaveChangesAsync();

            var sold = new List<POSSoldItemDto>
            {
                new() { DrinkId = DrinkId, SizeId = SizeId, Quantity = 2 },
                new() { DrinkId = DrinkId, SizeId = SizeId, Quantity = 3 }
            };
            var result = await CreateService(ctx).DeductStockForCommittedOrderAsync(sold, StoreId, 9118);
            Assert.True(result.IsSuccess, result.Message);
            // 100g * 5 = 500
            Assert.Equal(4500m, await IngredientQty(ctx));
            Assert.Equal(1, await ctx.InventoryTransactions.CountAsync(t => t.ReferenceOrderId == 9118));
            Assert.Equal(500m, (await ctx.InventoryTransactions.SingleAsync(t => t.ReferenceOrderId == 9118)).Quantity);
        }

        [Fact]
        public async Task PreparedMode_BaseAndToppingSameInventory_Aggregates()
        {
            using var ctx = CreateDbContext();
            await SeedStoreModeAsync(ctx, InventoryWriterMode.PreparedItem);
            await SeedIngredientOnlyCatalogAsync(ctx);
            ctx.Recipes.Add(new Recipe
            {
                RecipeId = ToppingRecipeId,
                RecipeCode = "TOP",
                Name = "Topping same ing",
                Active = true,
                Status = "Active",
                ToppingId = ToppingId,
                RecipeDetails = new List<RecipeDetail>
                {
                    new() { IngredientId = IngredientId, Quantity = 25m, UnitId = UnitGram }
                }
            });
            SeedPaidOrder(ctx, 9119);
            await ctx.SaveChangesAsync();

            var sold = new List<POSSoldItemDto>
            {
                new()
                {
                    DrinkId = DrinkId,
                    SizeId = SizeId,
                    Quantity = 1,
                    Toppings = new List<POSOrderToppingDto> { new() { ToppingId = ToppingId } }
                }
            };
            var result = await CreateService(ctx).DeductStockForCommittedOrderAsync(sold, StoreId, 9119);
            Assert.True(result.IsSuccess, result.Message);
            // 100 + 25 from base + topping paths, one inventory mutation
            Assert.Equal(4875m, await IngredientQty(ctx));
            var txs = await ctx.InventoryTransactions.Where(t => t.ReferenceOrderId == 9119).ToListAsync();
            Assert.Equal(125m, txs.Sum(t => t.Quantity));
            Assert.Single(txs.Select(t => t.StoreInventoryId).Distinct());
        }

        [Fact]
        public async Task PreparedMode_DoesNotReduceReservedQty()
        {
            using var ctx = CreateDbContext();
            await SeedPreparedCatalogAsync(ctx, childQtyLitresOnBom: 0.1m, reserved: 40m);
            SeedPaidOrder(ctx, 9120);
            await ctx.SaveChangesAsync();

            await CreateService(ctx).DeductStockForCommittedOrderAsync(SoldItems(), StoreId, 9120);
            var inv = await ctx.StoreInventories.SingleAsync(x =>
                x.PreparedItemId == PreparedItemId && x.BtpIdentityState == BtpIdentityState.Canonical);
            Assert.Equal(40m, inv.ReservedQty);
            Assert.Equal(9900m, inv.AvailableQty);
        }

        [Fact]
        public async Task PreparedMode_NegativeInventory_IsAllowed()
        {
            using var ctx = CreateDbContext();
            await SeedPreparedCatalogAsync(ctx, childQtyLitresOnBom: 0.5m, canonicalQty: 100m);
            SeedPaidOrder(ctx, 9121);
            await ctx.SaveChangesAsync();

            var result = await CreateService(ctx).DeductStockForCommittedOrderAsync(
                SoldItems(), StoreId, 9121);
            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(-400m, await CanonicalPiQty(ctx));
            Assert.NotEmpty(result.Errors!);
        }

        [Fact]
        public async Task PreparedMode_ExceedsMaxNegative_RemainsAllowedByBlindSellingPolicy()
        {
            using var ctx = CreateDbContext();
            await SeedPreparedCatalogAsync(ctx, childQtyLitresOnBom: 1m, canonicalQty: 50m, maxNegative: 10m);
            SeedPaidOrder(ctx, 9122);
            await ctx.SaveChangesAsync();

            var result = await CreateService(ctx).DeductStockForCommittedOrderAsync(
                SoldItems(), StoreId, 9122);
            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(-950m, await CanonicalPiQty(ctx)); // 50 - 1000
        }

        [Fact]
        public async Task PreparedMode_SecondRequirementFailure_RollsBackAllRows()
        {
            using var ctx = CreateDbContext();
            await SeedPreparedCatalogAsync(ctx, includeIngredientOnParent: true, childQtyLitresOnBom: 0.1m, detailUnitId: UnitGram);
            // ingredient converts; BTP gram→ml fails
            SeedPaidOrder(ctx, 9123);
            await ctx.SaveChangesAsync();
            var ingBefore = await IngredientQty(ctx);

            var result = await CreateService(ctx).DeductStockForCommittedOrderAsync(
                SoldItems(), StoreId, 9123);
            Assert.False(result.IsSuccess);
            Assert.Equal(ingBefore, await IngredientQty(ctx));
            Assert.Equal(10000m, await CanonicalPiQty(ctx));
            Assert.Equal(0, await ctx.InventoryTransactions.CountAsync(t => t.ReferenceOrderId == 9123));
        }

        [Fact]
        public async Task PreparedMode_Failure_CreatesNoInventoryTransactions()
        {
            using var ctx = CreateDbContext();
            await SeedPreparedCatalogAsync(ctx, childQtyLitresOnBom: 0.1m, mapChildPreparedItem: false);
            SeedPaidOrder(ctx, 9124);
            await ctx.SaveChangesAsync();
            Assert.False((await CreateService(ctx).DeductStockForCommittedOrderAsync(
                SoldItems(), StoreId, 9124)).IsSuccess);
            Assert.Equal(0, await ctx.InventoryTransactions.CountAsync());
        }

        [Fact]
        public async Task PreparedMode_Movements_HaveReferenceOrderId()
        {
            using var ctx = CreateDbContext();
            await SeedPreparedCatalogAsync(ctx, childQtyLitresOnBom: 0.1m);
            SeedPaidOrder(ctx, 9125);
            await ctx.SaveChangesAsync();
            await CreateService(ctx).DeductStockForCommittedOrderAsync(SoldItems(), StoreId, 9125);
            Assert.All(
                await ctx.InventoryTransactions.Where(t => t.ReferenceOrderId == 9125).ToListAsync(),
                t => Assert.Equal(9125, t.ReferenceOrderId));
        }

        [Fact]
        public async Task PreparedMode_Movements_PersistExactChildRecipeAudit()
        {
            using var ctx = CreateDbContext();
            await SeedPreparedCatalogAsync(ctx, childQtyLitresOnBom: 0.1m);
            SeedPaidOrder(ctx, 9126);
            await ctx.SaveChangesAsync();
            await CreateService(ctx).DeductStockForCommittedOrderAsync(SoldItems(), StoreId, 9126);
            var tx = await ctx.InventoryTransactions.SingleAsync(t => t.ReferenceOrderId == 9126);
            Assert.Equal(ChildRecipeId, tx.SourceRecipeId);
        }

        [Fact]
        public async Task PreparedMode_Replay_DoesNotMutateOrCreateMovements()
        {
            using var ctx = CreateDbContext();
            await SeedPreparedCatalogAsync(ctx, childQtyLitresOnBom: 0.1m);
            SeedPaidOrder(ctx, 9127);
            await ctx.SaveChangesAsync();
            var svc = CreateService(ctx);
            Assert.True((await svc.DeductStockForCommittedOrderAsync(SoldItems(), StoreId, 9127)).IsSuccess);
            var qty = await CanonicalPiQty(ctx);
            var count = await ctx.InventoryTransactions.CountAsync(t => t.ReferenceOrderId == 9127);
            Assert.True((await svc.DeductStockForCommittedOrderAsync(SoldItems(), StoreId, 9127)).IsSuccess);
            Assert.Equal(qty, await CanonicalPiQty(ctx));
            Assert.Equal(count, await ctx.InventoryTransactions.CountAsync(t => t.ReferenceOrderId == 9127));
        }

        [Fact]
        public async Task LegacyMode_ChildRecipe_StillUsesRecipeInventory()
        {
            using var ctx = CreateDbContext();
            await SeedStoreModeAsync(ctx, InventoryWriterMode.LegacyRecipe);
            // Minimal legacy catalog
            EnsureUnits(ctx);
            ctx.Recipes.Add(new Recipe
            {
                RecipeId = ParentRecipeId,
                RecipeCode = "P",
                Name = "P",
                Active = true,
                Status = "Active",
                DrinkId = DrinkId,
                SizeId = SizeId,
                RecipeDetails = new List<RecipeDetail>
                {
                    new() { ChildRecipeId = ChildRecipeId, Quantity = 2m, UnitId = UnitMl }
                }
            });
            ctx.Recipes.Add(new Recipe
            {
                RecipeId = ChildRecipeId,
                RecipeCode = "C",
                Name = "C",
                Active = true,
                Status = "Active"
            });
            ctx.StoreInventories.Add(new StoreInventory
            {
                StoreId = StoreId,
                RecipeId = ChildRecipeId,
                AvailableQty = 10m,
                ReservedQty = 0,
                LastUpdated = DateTime.UtcNow,
                RowVersion = new byte[] { 0 }
            });
            SeedPaidOrder(ctx, 9128);
            await ctx.SaveChangesAsync();

            var result = await CreateService(ctx).DeductStockForCommittedOrderAsync(
                SoldItems(), StoreId, 9128);
            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(8m, await ctx.StoreInventories
                .Where(x => x.RecipeId == ChildRecipeId)
                .Select(x => x.AvailableQty)
                .SingleAsync());
        }

        [Fact]
        public async Task BlockedMode_BtpOrder_FailsWithoutBtpMutation()
        {
            using var ctx = CreateDbContext();
            await SeedPreparedCatalogAsync(ctx, childQtyLitresOnBom: 0.1m);
            await SeedStoreModeAsync(ctx, InventoryWriterMode.Blocked);
            SeedPaidOrder(ctx, 9129);
            await ctx.SaveChangesAsync();

            var result = await CreateService(ctx).DeductStockForCommittedOrderAsync(
                SoldItems(), StoreId, 9129);
            Assert.False(result.IsSuccess);
            Assert.Equal(10000m, await CanonicalPiQty(ctx));
        }

        [Fact]
        public async Task BlockedMode_IngredientOnly_DeductsIngredient()
        {
            using var ctx = CreateDbContext();
            await SeedIngredientOnlyCatalogAsync(ctx);
            await SeedStoreModeAsync(ctx, InventoryWriterMode.Blocked);
            SeedPaidOrder(ctx, 9130);
            await ctx.SaveChangesAsync();

            // No BTP → no mode acquire path requiring BTP; ingredient-only succeeds
            var result = await CreateService(ctx).DeductStockForCommittedOrderAsync(
                SoldItems(), StoreId, 9130);
            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(4900m, await IngredientQty(ctx));
        }

        [Fact]
        public async Task PosPreparedCapability_DoesNotChangeWriterMode()
        {
            using var ctx = CreateDbContext();
            await SeedStoreModeAsync(ctx, InventoryWriterMode.LegacyRecipe);
            var before = await ctx.StoreInventoryWriterConfigurations.AsNoTracking()
                .SingleAsync(x => x.StoreId == StoreId);

            var cap = new PosPreparedWriterCapabilityProvider().GetStatus();
            Assert.True(cap.Ready);
            Assert.Equal(InventoryWriterCapabilityIds.PosPreparedWriter, cap.CapabilityId);
            Assert.Equal(PosPreparedWriterCapabilityProvider.ContractVersion, cap.ContractVersion);

            var after = await ctx.StoreInventoryWriterConfigurations.AsNoTracking()
                .SingleAsync(x => x.StoreId == StoreId);
            Assert.Equal(before.WriterMode, after.WriterMode);
        }

        [Fact]
        public async Task InventoryService_PreparedMode_DoesNotWriteRecipeBtpInventory()
        {
            using var ctx = CreateDbContext();
            await SeedStoreModeAsync(ctx, InventoryWriterMode.PreparedItem);
            EnsureUnits(ctx);
            ctx.Recipes.Add(new Recipe
            {
                RecipeId = ParentRecipeId,
                RecipeCode = "P2",
                Name = "P2",
                Active = true,
                Status = "Active",
                DrinkId = DrinkId,
                SizeId = SizeId,
                RecipeDetails = new List<RecipeDetail>
                {
                    new() { ChildRecipeId = ChildRecipeId, Quantity = 1m, UnitId = UnitMl }
                }
            });
            ctx.Recipes.Add(new Recipe
            {
                RecipeId = ChildRecipeId,
                RecipeCode = "C2",
                Name = "C2",
                Active = true,
                Status = "Active"
            });
            ctx.StoreInventories.Add(new StoreInventory
            {
                StoreId = StoreId,
                RecipeId = ChildRecipeId,
                AvailableQty = 50m,
                ReservedQty = 0,
                LastUpdated = DateTime.UtcNow,
                RowVersion = new byte[] { 0 }
            });
            ctx.Orders.Add(new Order
            {
                OrderId = 9200,
                StoreId = StoreId,
                OrderStatusId = SystemConstants.OrderStatuses.Completed,
                PaymentStatusId = SystemConstants.PaymentStatuses.Paid,
                OrderTypeId = SystemConstants.OrderTypes.Delivery,
                Source = "ONLINE",
                SubTotal = 1,
                Total = 1,
                CreatedAt = DateTime.UtcNow,
                OrderDetails = new List<OrderDetail>
                {
                    new()
                    {
                        DrinkId = DrinkId,
                        SizeId = SizeId,
                        DrinkName = "D",
                        Quantity = 1,
                        Price = 1,
                        Note = ""
                    }
                }
            });
            await ctx.SaveChangesAsync();

            var physical = new PhysicalUnitConversionService(ctx, NullLogger<PhysicalUnitConversionService>.Instance);
            var writer = new InventoryWriterModeService(
                ctx,
                physical,
                new IInventoryWriterCapabilityProvider[]
                {
                    new ProductionPreparedWriterCapabilityProvider(),
                    new PosPreparedWriterCapabilityProvider()
                });
            var invService = new InventoryService(ctx, writer);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                invService.ConfirmInventoryDeductionAsync(9200));
            Assert.Equal(50m, await ctx.StoreInventories
                .Where(x => x.RecipeId == ChildRecipeId)
                .Select(x => x.AvailableQty)
                .SingleAsync());
        }

        // -------- helpers --------

        private static InventoryDeductionService CreateService(AppDbContext context)
        {
            var physical = new PhysicalUnitConversionService(context, NullLogger<PhysicalUnitConversionService>.Instance);
            var unit = new UnitConversionService(context, NullLogger<UnitConversionService>.Instance, physical);
            var caps = new IInventoryWriterCapabilityProvider[]
            {
                new ProductionPreparedWriterCapabilityProvider(),
                new PosPreparedWriterCapabilityProvider()
            };
            var writer = new InventoryWriterModeService(context, physical, caps);
            var resolver = new StoreInventoryWriteResolver(context, writer);
            var normalizer = new RecipeOutputNormalizer(context, physical);
            var estimated = new EstimatedBomCostService(
                context, unit, physical, normalizer, NullLogger<EstimatedBomCostService>.Instance);
            return new InventoryDeductionService(
                context,
                NullLogger<InventoryDeductionService>.Instance,
                unit,
                estimated,
                physical,
                writerModeService: writer,
                writeResolver: resolver);
        }

        private static List<POSSoldItemDto> SoldItems(int qty = 1) => new()
        {
            new() { DrinkId = DrinkId, SizeId = SizeId, Quantity = qty }
        };

        private static void EnsureUnits(AppDbContext ctx)
        {
            // Units 1/3/4 are usually seeded by EnsureCreated HasData
        }

        private static async Task SeedStoreModeAsync(AppDbContext ctx, InventoryWriterMode mode)
        {
            EnsureUnits(ctx);
            if (!await ctx.Stores.AnyAsync(s => s.StoreId == StoreId))
            {
                ctx.Stores.Add(new Store
                {
                    StoreId = StoreId,
                    Name = "S121",
                    Address = "A",
                    Phone = "1",
                    Active = true,
                    CreatedAt = DateTime.UtcNow
                });
            }

            var cfg = await ctx.StoreInventoryWriterConfigurations
                .FirstOrDefaultAsync(x => x.StoreId == StoreId);
            if (cfg == null)
            {
                ctx.StoreInventoryWriterConfigurations.Add(new StoreInventoryWriterConfiguration
                {
                    StoreId = StoreId,
                    WriterMode = mode,
                    HasEverActivatedPreparedItem = mode == InventoryWriterMode.PreparedItem,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                cfg.WriterMode = mode;
                cfg.HasEverActivatedPreparedItem = mode != InventoryWriterMode.LegacyRecipe;
                cfg.UpdatedAt = DateTime.UtcNow;
            }

            await ctx.SaveChangesAsync();
        }

        private static async Task SeedIngredientOnlyCatalogAsync(AppDbContext ctx)
        {
            await SeedStoreModeAsync(ctx, InventoryWriterMode.PreparedItem);
            if (!await ctx.Ingredients.AnyAsync(i => i.IngredientId == IngredientId))
            {
                ctx.Ingredients.Add(new Ingredient
                {
                    IngredientId = IngredientId,
                    Code = "ING121",
                    Name = "Milk",
                    BaseUnitId = UnitGram,
                    Active = true
                });
            }

            if (!await ctx.Recipes.AnyAsync(r => r.RecipeId == ParentRecipeId))
            {
                ctx.Recipes.Add(new Recipe
                {
                    RecipeId = ParentRecipeId,
                    RecipeCode = "PARENT-ING",
                    Name = "Parent ingredient only",
                    Active = true,
                    Status = "Active",
                    DrinkId = DrinkId,
                    SizeId = SizeId,
                    RecipeDetails = new List<RecipeDetail>
                    {
                        new() { IngredientId = IngredientId, Quantity = 100m, UnitId = UnitGram }
                    }
                });
            }

            if (!await ctx.StoreInventories.AnyAsync(x => x.StoreId == StoreId && x.IngredientId == IngredientId))
            {
                ctx.StoreInventories.Add(new StoreInventory
                {
                    StoreId = StoreId,
                    IngredientId = IngredientId,
                    AvailableQty = 5000m,
                    ReservedQty = 0,
                    LastUpdated = DateTime.UtcNow,
                    RowVersion = new byte[] { 0 }
                });
            }

            await ctx.SaveChangesAsync();
        }

        private static async Task SeedPreparedCatalogAsync(
            AppDbContext ctx,
            decimal childQtyLitresOnBom = 0.1m,
            bool includeIngredientOnParent = false,
            bool mapChildPreparedItem = true,
            bool seedCanonical = true,
            decimal canonicalQty = 10000m,
            decimal reserved = 0m,
            decimal? maxNegative = null,
            int detailUnitId = UnitL)
        {
            await SeedStoreModeAsync(ctx, InventoryWriterMode.PreparedItem);

            if (!await ctx.PreparedItems.AnyAsync(p => p.PreparedItemId == PreparedItemId))
            {
                ctx.PreparedItems.Add(new PreparedItem
                {
                    PreparedItemId = PreparedItemId,
                    Code = "PI121",
                    Name = "Syrup PI",
                    BaseUnitId = UnitMl,
                    Active = true
                });
            }

            if (includeIngredientOnParent && !await ctx.Ingredients.AnyAsync(i => i.IngredientId == IngredientId))
            {
                ctx.Ingredients.Add(new Ingredient
                {
                    IngredientId = IngredientId,
                    Code = "ING121B",
                    Name = "Milk",
                    BaseUnitId = UnitGram,
                    Active = true
                });
                ctx.StoreInventories.Add(new StoreInventory
                {
                    StoreId = StoreId,
                    IngredientId = IngredientId,
                    AvailableQty = 5000m,
                    ReservedQty = 0,
                    LastUpdated = DateTime.UtcNow,
                    RowVersion = new byte[] { 0 }
                });
            }

            if (!await ctx.Recipes.AnyAsync(r => r.RecipeId == ChildRecipeId))
            {
                ctx.Recipes.Add(new Recipe
                {
                    RecipeId = ChildRecipeId,
                    RecipeCode = "CHILD121",
                    Name = "Child syrup",
                    Active = true,
                    Status = "Active",
                    PreparedItemId = mapChildPreparedItem ? PreparedItemId : null,
                    OutputQuantity = mapChildPreparedItem ? 1m : null,
                    OutputUnitId = mapChildPreparedItem ? UnitL : null
                });
            }

            if (!await ctx.Recipes.AnyAsync(r => r.RecipeId == ParentRecipeId))
            {
                var details = new List<RecipeDetail>
                {
                    new()
                    {
                        ChildRecipeId = ChildRecipeId,
                        Quantity = childQtyLitresOnBom,
                        UnitId = detailUnitId
                    }
                };
                if (includeIngredientOnParent)
                {
                    details.Insert(0, new RecipeDetail
                    {
                        IngredientId = IngredientId,
                        Quantity = 100m,
                        UnitId = UnitGram
                    });
                }

                ctx.Recipes.Add(new Recipe
                {
                    RecipeId = ParentRecipeId,
                    RecipeCode = "PARENT121",
                    Name = "Parent drink",
                    Active = true,
                    Status = "Active",
                    DrinkId = DrinkId,
                    SizeId = SizeId,
                    RecipeDetails = details
                });
            }

            if (seedCanonical
                && !await ctx.StoreInventories.AnyAsync(x =>
                    x.StoreId == StoreId
                    && x.PreparedItemId == PreparedItemId
                    && x.BtpIdentityState == BtpIdentityState.Canonical))
            {
                ctx.StoreInventories.Add(MakeCanonical(canonicalQty, reserved, maxNegative));
            }

            await ctx.SaveChangesAsync();
        }

        private static StoreInventory MakeCanonical(
            decimal available,
            decimal reserved = 0m,
            decimal? maxNegative = null,
            InventoryQuantitySemanticsStatus semantics = InventoryQuantitySemanticsStatus.BaseUnitConfirmed)
            => new()
            {
                StoreId = StoreId,
                PreparedItemId = PreparedItemId,
                RecipeId = null,
                IngredientId = null,
                BtpIdentityState = BtpIdentityState.Canonical,
                QuantitySemanticsStatus = semantics,
                QuantitySemanticsEvidenceType = QuantitySemanticsEvidenceType.SystemCanonicalCreation,
                QuantitySemanticsEvidenceReference = "seed-121",
                QuantitySemanticsReviewedAt = DateTime.UtcNow,
                QuantitySemanticsReviewedByAccountId = 1,
                AvailableQty = available,
                ReservedQty = reserved,
                MaxNegativeQty = maxNegative,
                MinStockLevel = 0,
                LastUpdated = DateTime.UtcNow,
                RowVersion = new byte[] { 0 }
            };

        private static void SeedPaidOrder(AppDbContext ctx, int orderId)
        {
            if (ctx.Orders.Any(o => o.OrderId == orderId))
                return;
            ctx.Orders.Add(new Order
            {
                OrderId = orderId,
                StoreId = StoreId,
                OrderStatusId = SystemConstants.OrderStatuses.Completed,
                PaymentStatusId = SystemConstants.PaymentStatuses.Paid,
                OrderTypeId = SystemConstants.OrderTypes.DineIn,
                Source = "POS",
                SubTotal = 1,
                Total = 1,
                CreatedAt = DateTime.UtcNow
            });
        }

        private static Task<decimal> IngredientQty(AppDbContext ctx)
            => ctx.StoreInventories
                .Where(x => x.StoreId == StoreId && x.IngredientId == IngredientId)
                .Select(x => x.AvailableQty)
                .SingleAsync();

        private static Task<decimal> CanonicalPiQty(AppDbContext ctx)
            => ctx.StoreInventories
                .Where(x => x.StoreId == StoreId
                            && x.PreparedItemId == PreparedItemId
                            && x.BtpIdentityState == BtpIdentityState.Canonical)
                .Select(x => x.AvailableQty)
                .SingleAsync();
    }
}
