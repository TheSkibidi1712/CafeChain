using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Constants;
using CafeChain.Application.Services.Admin.Recipes;
using CafeChain.Application.Services.Inventories;
using CafeChain.Application.Services.POS;
using CafeChain.Areas.Admin.Controllers;
using CafeChain.Data;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Orders;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CafeChain.Tests
{
    /// <summary>Issue #115: additive PreparedItem compatibility only, with no mapping writer.</summary>
    public sealed class PreparedItemInventoryCompatibilityIssue115Tests : IntegrationTestBase
    {
        private const int StoreId = 710;
        private const int UnitGram = 1;
        private const int UnitMl = 3;
        private const int IngredientId = 703;
        private const int PreparedItemId = 704;
        private const int RecipeId = 705;

        [Fact]
        public void StoreInventoryModel_HasTransitionalIdentityConstraintAndNonUniquePreparedItemIndex()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=CafeChainIssue115ModelOnly;Trusted_Connection=True;")
                .Options;
            using var context = new AppDbContext(options);
            var entity = context.GetService<IDesignTimeModel>().Model
                .FindEntityType(typeof(StoreInventory))!;

            var constraint = entity.GetCheckConstraints()
                .Single(x => x.Name == "CK_StoreInventories_XOR_Item");
            Assert.Contains("[PreparedItemId]", constraint.Sql);
            Assert.Contains("[RecipeId] IS NOT NULL AND [PreparedItemId] IS NOT NULL", constraint.Sql);

            var index = entity.GetIndexes()
                .Single(x => x.GetDatabaseName() == "IX_Store_PreparedItem_Compatibility");
            Assert.False(index.IsUnique);
            Assert.Contains("PreparedItemId", index.GetFilter()!);
        }

        [Fact]
        public async Task Resolver_IngredientRowRemainsIngredientIdentity()
        {
            using var context = CreateDbContext();
            await SeedAsync(context);
            var inventory = new StoreInventory
            {
                StoreId = StoreId,
                IngredientId = IngredientId,
                AvailableQty = 10m,
                ReservedQty = 0m,
                LastUpdated = DateTime.UtcNow
            };
            context.StoreInventories.Add(inventory);
            await context.SaveChangesAsync();

            var snapshot = await new InventoryItemIdentityResolver(context)
                .ResolveStoreInventoryAsync(inventory.StoreInventoryId);

            Assert.NotNull(snapshot);
            Assert.Equal(InventoryItemIdentityTypes.Ingredient, snapshot!.InventoryItemType);
            Assert.Equal(IngredientId, snapshot.IngredientId);
            Assert.Empty(snapshot.ValidationIssues);
        }

        [Fact]
        public async Task Resolver_LegacyRecipeRowFallsBackAndShowsLegacyStatus()
        {
            using var context = CreateDbContext();
            await SeedAsync(context);
            var inventory = await AddLegacyRecipeInventoryAsync(context);

            var snapshot = await new InventoryItemIdentityResolver(context)
                .ResolveStoreInventoryAsync(inventory.StoreInventoryId);

            Assert.NotNull(snapshot);
            Assert.Equal(InventoryItemIdentityTypes.LegacyRecipe, snapshot!.InventoryItemType);
            Assert.True(snapshot.IsLegacyUnmapped);
            Assert.Equal(QuantitySemanticsStatuses.Unknown, snapshot.QuantitySemanticsStatus);
        }

        [Fact]
        public async Task Resolver_CompatibilityRowUsesPreparedItemIdentityOnlyOnce()
        {
            using var context = CreateDbContext();
            await SeedAsync(context, linkRecipeToPreparedItem: true);
            var inventory = new StoreInventory
            {
                StoreId = StoreId,
                RecipeId = RecipeId,
                PreparedItemId = PreparedItemId,
                AvailableQty = 12m,
                ReservedQty = 2m,
                LastUpdated = DateTime.UtcNow
            };
            context.StoreInventories.Add(inventory);
            await context.SaveChangesAsync();

            var resolver = new InventoryItemIdentityResolver(context);
            var snapshot = await resolver.ResolveStoreInventoryAsync(inventory.StoreInventoryId);
            var list = await new PosBranchInventoryService(context)
                .GetBranchInventoryAsync(StoreId, null, null, 1, 50);

            Assert.NotNull(snapshot);
            Assert.Equal(InventoryItemIdentityTypes.PreparedItem, snapshot!.InventoryItemType);
            Assert.Equal("BTP-115", snapshot.Code);
            Assert.Equal("Nền trà test", snapshot.Name);
            Assert.Equal(UnitGram, snapshot.BaseUnitId);
            Assert.True(snapshot.HasCompatibilityRecipe);
            Assert.Equal(RecipeId, snapshot.LegacyRecipeId);

            var item = Assert.Single(list.Data!.Items);
            Assert.Equal(PreparedItemId, item.ItemId);
            Assert.Equal(PreparedItemId, item.PreparedItemId);
            Assert.Equal(RecipeId, item.LegacyRecipeId);
            Assert.False(item.IsLegacyUnmapped);
            Assert.Equal(QuantitySemanticsStatuses.Unknown, item.QuantitySemanticsStatus);
            Assert.Equal("—", item.UnitName);
        }

        [Fact]
        public async Task Resolver_PreparedItemOnlyRowIsAcceptedForFutureCutoverWithoutRecipeMetadata()
        {
            using var context = CreateDbContext();
            await SeedAsync(context);
            var inventory = new StoreInventory
            {
                StoreId = StoreId,
                PreparedItemId = PreparedItemId,
                AvailableQty = 6m,
                ReservedQty = 1m,
                LastUpdated = DateTime.UtcNow
            };
            context.StoreInventories.Add(inventory);
            await context.SaveChangesAsync();

            var snapshot = await new InventoryItemIdentityResolver(context)
                .ResolveStoreInventoryAsync(inventory.StoreInventoryId);

            Assert.NotNull(snapshot);
            Assert.Equal(InventoryItemIdentityTypes.PreparedItem, snapshot!.InventoryItemType);
            Assert.Equal(PreparedItemId, snapshot.PreparedItemId);
            Assert.False(snapshot.HasCompatibilityRecipe);
            Assert.Empty(snapshot.ValidationIssues);
        }

        [Fact]
        public async Task Resolver_RejectsUnrelatedRecipeAndPreparedItemPair()
        {
            using var context = CreateDbContext();
            await SeedAsync(context, linkRecipeToPreparedItem: false);
            var inventory = new StoreInventory
            {
                StoreId = StoreId,
                RecipeId = RecipeId,
                PreparedItemId = PreparedItemId,
                AvailableQty = 4m,
                ReservedQty = 0m,
                LastUpdated = DateTime.UtcNow
            };
            context.StoreInventories.Add(inventory);
            await context.SaveChangesAsync();

            var snapshot = await new InventoryItemIdentityResolver(context)
                .ResolveStoreInventoryAsync(inventory.StoreInventoryId);

            Assert.Contains(InventoryIdentityValidationIssueCodes.RecipePreparedItemMismatch,
                snapshot!.ValidationIssues);
        }

        [Fact]
        public async Task Analyzer_PhysicalUnitCompatibilityDoesNotOverrideUnknownLegacyQuantitySemantics()
        {
            using var context = CreateDbContext();
            await SeedAsync(context, linkRecipeToPreparedItem: true);
            var inventory = await AddLegacyRecipeInventoryAsync(context, availableQty: 9m, reservedQty: 1m);
            var analyzer = CreateAnalyzer(context);
            var before = new { inventory.AvailableQty, inventory.ReservedQty, inventory.PreparedItemId };

            var report = await analyzer.AnalyzeAsync(inventory.StoreInventoryId, PreparedItemId);
            var after = await context.StoreInventories.AsNoTracking()
                .SingleAsync(x => x.StoreInventoryId == inventory.StoreInventoryId);

            Assert.True(report.UnitsPhysicallyCompatible);
            Assert.Equal(QuantitySemanticsStatuses.Unknown, report.QuantitySemanticsStatus);
            Assert.Equal(CompatibilityProposedActions.Blocked, report.ProposedAction);
            Assert.Contains(InventoryIdentityValidationIssueCodes.QuantitySemanticsUnknown, report.BlockingIssues);
            Assert.Equal(report.BeforeAvailableTotal, report.HypotheticalAfterAvailableTotal);
            Assert.Equal(report.BeforeReservedTotal, report.HypotheticalAfterReservedTotal);
            Assert.Equal(before.AvailableQty, after.AvailableQty);
            Assert.Equal(before.ReservedQty, after.ReservedQty);
            Assert.Null(after.PreparedItemId);
            Assert.False(context.ChangeTracker.HasChanges());
            Assert.Equal(0, await context.InventoryTransactions.CountAsync());
        }

        [Fact]
        public async Task Analyzer_DetectsExistingPreparedItemTargetAndThresholdConflictsWithoutConsolidation()
        {
            using var context = CreateDbContext();
            await SeedAsync(context, linkRecipeToPreparedItem: true);
            var source = await AddLegacyRecipeInventoryAsync(context, availableQty: 5m, reservedQty: 1m,
                minStockLevel: 2m, maxNegativeQty: 4m);
            context.StoreInventories.Add(new StoreInventory
            {
                StoreId = StoreId,
                PreparedItemId = PreparedItemId,
                AvailableQty = 8m,
                ReservedQty = 0m,
                MinStockLevel = 3m,
                MaxNegativeQty = 5m,
                LastUpdated = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var report = await CreateAnalyzer(context).AnalyzeAsync(source.StoreInventoryId, PreparedItemId);

            Assert.Equal(CompatibilityCollisionStatuses.Collision, report.CollisionStatus);
            Assert.Contains(InventoryIdentityValidationIssueCodes.ExistingTargetRow, report.BlockingIssues);
            Assert.Contains(InventoryIdentityValidationIssueCodes.MinStockLevelConflict, report.BlockingIssues);
            Assert.Contains(InventoryIdentityValidationIssueCodes.MaxNegativeQtyConflict, report.BlockingIssues);
            Assert.Equal(13m, report.BeforeAvailableTotal);
            Assert.Equal(1m, report.BeforeReservedTotal);
            Assert.Equal(13m, report.HypotheticalAfterAvailableTotal);
            Assert.Equal(1m, report.HypotheticalAfterReservedTotal);
            Assert.Equal(2, report.InvolvedStoreInventoryIds.Count);
            Assert.Equal(2, await context.StoreInventories.CountAsync(x => x.StoreId == StoreId));
        }

        [Fact]
        public async Task Analyzer_UnitMismatchBlocksReadiness()
        {
            using var context = CreateDbContext();
            await SeedAsync(context, linkRecipeToPreparedItem: true, recipeOutputUnitId: UnitMl);
            var source = await AddLegacyRecipeInventoryAsync(context);

            var report = await CreateAnalyzer(context).AnalyzeAsync(source.StoreInventoryId, PreparedItemId);

            Assert.False(report.UnitsPhysicallyCompatible);
            Assert.Contains(InventoryIdentityValidationIssueCodes.UnitIncompatible, report.BlockingIssues);
            Assert.Equal(CompatibilityProposedActions.Blocked, report.ProposedAction);
        }

        [Fact]
        public async Task Analyzer_MismatchedProposalStillIncludesSourceAndConservesItsQuantities()
        {
            using var context = CreateDbContext();
            await SeedAsync(context, linkRecipeToPreparedItem: false);
            var source = await AddLegacyRecipeInventoryAsync(context, availableQty: 7m, reservedQty: 2m);

            var report = await CreateAnalyzer(context).AnalyzeAsync(source.StoreInventoryId, PreparedItemId);

            Assert.Contains(source.StoreInventoryId, report.InvolvedStoreInventoryIds);
            Assert.Equal(7m, report.BeforeAvailableTotal);
            Assert.Equal(2m, report.BeforeReservedTotal);
            Assert.Equal(report.BeforeAvailableTotal, report.HypotheticalAfterAvailableTotal);
            Assert.Equal(report.BeforeReservedTotal, report.HypotheticalAfterReservedTotal);
            Assert.Contains(InventoryIdentityValidationIssueCodes.RecipePreparedItemMismatch, report.BlockingIssues);
            Assert.Equal(CompatibilityProposedActions.Blocked, report.ProposedAction);
        }

        [Fact]
        public async Task PosLegacyWriter_UpdatesCompatibilityRowByRecipeIdWithoutCreatingDuplicate()
        {
            using var context = CreateDbContext();
            await SeedAsync(context, linkRecipeToPreparedItem: true);
            const int drinkId = 810;
            const int sizeId = 811;
            const int orderId = 812;
            const int mainRecipeId = 813;
            context.Recipes.Add(new Recipe
            {
                RecipeId = mainRecipeId,
                RecipeCode = "RCP-MAIN-115",
                Name = "Main POS recipe",
                Active = true,
                Status = "Active",
                DrinkId = drinkId,
                SizeId = sizeId,
                RecipeDetails = new List<RecipeDetail>
                {
                    new() { ChildRecipeId = RecipeId, Quantity = 2m, UnitId = UnitGram }
                }
            });
            var compatibilityRow = new StoreInventory
            {
                StoreId = StoreId,
                RecipeId = RecipeId,
                PreparedItemId = PreparedItemId,
                AvailableQty = 10m,
                ReservedQty = 0m,
                LastUpdated = DateTime.UtcNow
            };
            context.StoreInventories.Add(compatibilityRow);
            context.Orders.Add(new Order
            {
                OrderId = orderId,
                StoreId = StoreId,
                OrderStatusId = SystemConstants.OrderStatuses.Completed,
                PaymentStatusId = SystemConstants.PaymentStatuses.Paid,
                OrderTypeId = SystemConstants.OrderTypes.DineIn,
                Source = "POS",
                SubTotal = 1m,
                Total = 1m,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var result = await CreateDeductionService(context).DeductStockForCommittedOrderAsync(
                new List<POSSoldItemDto>
                {
                    new() { DrinkId = drinkId, SizeId = sizeId, Quantity = 1, Toppings = new List<POSOrderToppingDto>() }
                },
                StoreId,
                orderId);

            Assert.True(result.IsSuccess);
            var rows = await context.StoreInventories
                .Where(x => x.StoreId == StoreId && x.RecipeId == RecipeId)
                .ToListAsync();
            var updated = Assert.Single(rows);
            Assert.Equal(compatibilityRow.StoreInventoryId, updated.StoreInventoryId);
            Assert.Equal(PreparedItemId, updated.PreparedItemId);
            Assert.Equal(8m, updated.AvailableQty);
        }

        /// <summary>
        /// Issue #119: production confirm no longer mutates stock (stock apply deferred to #120 / 114C).
        /// Confirms production intent leaves compatibility inventory unchanged.
        /// Legacy RecipeId stock credit path will return under #120 tests.
        /// </summary>
        [Fact]
        public async Task ProductionRunConfirm_DoesNotMutateCompatibilityInventory_Issue119()
        {
            using var context = CreateDbContext();
            await SeedAsync(context, linkRecipeToPreparedItem: true);

            var now = DateTime.UtcNow;
            if (!await context.Stores.AnyAsync(x => x.StoreId == StoreId))
            {
                context.Stores.Add(new Store
                {
                    StoreId = StoreId,
                    Name = "Store 115",
                    Address = "A",
                    Phone = "1",
                    Active = true,
                    CreatedAt = now
                });
            }

            if (!await context.StoreInventoryWriterConfigurations.AnyAsync(x => x.StoreId == StoreId))
            {
                context.StoreInventoryWriterConfigurations.Add(
                    new CafeChain.Models.Inventories.Configuration.StoreInventoryWriterConfiguration
                    {
                        StoreId = StoreId,
                        WriterMode = CafeChain.Models.Enums.Inventory.InventoryWriterMode.LegacyRecipe,
                        HasEverActivatedPreparedItem = false,
                        CreatedAt = now,
                        UpdatedAt = now,
                        RowVersion = new byte[] { 0 }
                    });
            }

            const int staffId = 9101;
            context.Staffs.Add(new CafeChain.Models.Staffs.Staff
            {
                StaffId = staffId,
                AccountId = staffId,
                FullName = "Prod Staff",
                StoreId = StoreId,
                Active = true,
                CreatedAt = now
            });

            var compatibilityRow = new StoreInventory
            {
                StoreId = StoreId,
                RecipeId = RecipeId,
                PreparedItemId = PreparedItemId,
                AvailableQty = 10m,
                ReservedQty = 0m,
                LastUpdated = DateTime.UtcNow
            };
            context.StoreInventories.Add(compatibilityRow);
            await context.SaveChangesAsync();

            var physical = new PhysicalUnitConversionService(context, NullLogger<PhysicalUnitConversionService>.Instance);
            var writer = new InventoryWriterModeService(
                context,
                physical,
                Array.Empty<CafeChain.Application.Interfaces.Inventories.IInventoryWriterCapabilityProvider>());
            var scope = new CafeChain.Application.Services.Security.ScopeAuthorizationService(context);
            var service = new CafeChain.Application.Services.Admin.Production.ProductionRunService(
                context,
                scope,
                writer,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<CafeChain.Application.Services.Admin.Production.ProductionRunService>());

            var result = await service.CreateAndConfirmAsync(
                new CafeChain.Application.DTOs.Admin.Production.CreateAndConfirmProductionRunRequest
                {
                    RequestKey = Guid.NewGuid(),
                    StoreId = StoreId,
                    RecipeId = RecipeId,
                    RequestedRunCount = 2m
                },
                staffId: staffId,
                staffHomeStoreId: StoreId);

            Assert.True(result.IsSuccess, result.Message);
            Assert.False(result.Data!.StockApplied);

            var rows = await context.StoreInventories
                .Where(x => x.StoreId == StoreId && x.RecipeId == RecipeId)
                .ToListAsync();
            var updated = Assert.Single(rows);
            Assert.Equal(compatibilityRow.StoreInventoryId, updated.StoreInventoryId);
            Assert.Equal(PreparedItemId, updated.PreparedItemId);
            Assert.Equal(10m, updated.AvailableQty); // unchanged — no stock writer in #119
            Assert.Equal(0, await context.InventoryTransactions.CountAsync());
        }

        private static PreparedItemInventoryCompatibilityAnalyzer CreateAnalyzer(AppDbContext context)
        {
            var physical = new PhysicalUnitConversionService(
                context,
                NullLogger<PhysicalUnitConversionService>.Instance);
            return new PreparedItemInventoryCompatibilityAnalyzer(context, physical);
        }

        private static InventoryDeductionService CreateDeductionService(AppDbContext context)
        {
            var physical = new PhysicalUnitConversionService(context, NullLogger<PhysicalUnitConversionService>.Instance);
            var unitConversion = new UnitConversionService(context, NullLogger<UnitConversionService>.Instance, physical);
            var normalizer = new RecipeOutputNormalizer(context, physical);
            var estimated = new EstimatedBomCostService(
                context,
                unitConversion,
                physical,
                normalizer,
                NullLogger<EstimatedBomCostService>.Instance);
            return new InventoryDeductionService(
                context,
                NullLogger<InventoryDeductionService>.Instance,
                unitConversion,
                estimated);
        }

        private static async Task SeedAsync(
            AppDbContext context,
            bool linkRecipeToPreparedItem = false,
            int recipeOutputUnitId = UnitGram)
        {
            if (!context.Units.Any(x => x.UnitId == UnitGram))
            {
                context.Units.Add(new Unit
                {
                    UnitId = UnitGram,
                    UnitCode = "g",
                    Name = "Gram",
                    Type = UnitType.KhoiLuong,
                    Active = true
                });
            }
            if (!context.Units.Any(x => x.UnitId == UnitMl))
            {
                context.Units.Add(new Unit
                {
                    UnitId = UnitMl,
                    UnitCode = "ml",
                    Name = "Ml",
                    Type = UnitType.TheTich,
                    Active = true
                });
            }
            context.Ingredients.Add(new Ingredient
            {
                IngredientId = IngredientId,
                Code = "ING-115",
                Name = "Nguyên liệu 115",
                BaseUnitId = UnitGram,
                Active = true
            });
            context.PreparedItems.Add(new PreparedItem
            {
                PreparedItemId = PreparedItemId,
                Code = "BTP-115",
                Name = "Nền trà test",
                BaseUnitId = UnitGram,
                Active = true
            });
            context.Recipes.Add(new Recipe
            {
                RecipeId = RecipeId,
                RecipeCode = "RCP-115",
                Name = "Công thức nền trà",
                Active = true,
                Status = "Active",
                PreparedItemId = linkRecipeToPreparedItem ? PreparedItemId : null,
                OutputQuantity = linkRecipeToPreparedItem ? 1000m : null,
                OutputUnitId = linkRecipeToPreparedItem ? recipeOutputUnitId : null
            });
            await context.SaveChangesAsync();
        }

        private static async Task<StoreInventory> AddLegacyRecipeInventoryAsync(
            AppDbContext context,
            decimal availableQty = 10m,
            decimal reservedQty = 0m,
            decimal? minStockLevel = null,
            decimal? maxNegativeQty = null)
        {
            var inventory = new StoreInventory
            {
                StoreId = StoreId,
                RecipeId = RecipeId,
                AvailableQty = availableQty,
                ReservedQty = reservedQty,
                MinStockLevel = minStockLevel,
                MaxNegativeQty = maxNegativeQty,
                LastUpdated = DateTime.UtcNow
            };
            context.StoreInventories.Add(inventory);
            await context.SaveChangesAsync();
            return inventory;
        }
    }
}
