// using CafeChain.Application.Constants;
// using CafeChain.Application.DTOs.POS;
// using CafeChain.Application.Interfaces.Inventories;
// using CafeChain.Application.Services.Admin.Production;
// using CafeChain.Application.Services.Admin.Recipes;
// using CafeChain.Application.Services.Inventories;
// using CafeChain.Data;
// using CafeChain.Models.Drinks;
// using CafeChain.Models.Enums.Inventory;
// using CafeChain.Models.Inventories.Configuration;
// using CafeChain.Models.Inventories.PreparedItems;
// using CafeChain.Models.Orders;
// using CafeChain.Models.Stores;
// using Microsoft.Data.SqlClient;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.Extensions.Logging.Abstractions;
// using Xunit;

// namespace CafeChain.Tests
// {
//     /// <summary>
//     /// Issue #121 — SQL Server concurrency proof for POS PreparedItem deduction.
//     /// Database: CafeChain_Issue121Tests on local SQLEXPRESS.
//     /// </summary>
//     [Trait("Category", "SqlServerIntegration")]
//     public sealed class PosPreparedItemConsumptionSqlServerIssue121Tests : IAsyncLifetime
//     {
//         private const string Server = @"DESKTOP-K038H12\SQLEXPRESS";
//         private const string Database = "CafeChain_Issue121Tests";

//         private static string ConnectionString =>
//             $"Server={Server};Database={Database};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

//         private static string MasterConnectionString =>
//             $"Server={Server};Database=master;Trusted_Connection=True;TrustServerCertificate=True";

//         private const int StoreId = 1;
//         private const int UnitMl = 3;
//         private const int UnitL = 4;
//         // Use EnsureCreated HasData drink/size to satisfy FKs
//         private const int DrinkId = 1;
//         private const int SizeId = 1;

//         public async Task InitializeAsync()
//         {
//             try
//             {
//                 await using (var master = new SqlConnection(MasterConnectionString))
//                 {
//                     await master.OpenAsync();
//                     await using var cmd = master.CreateCommand();
//                     cmd.CommandText = $@"
// IF DB_ID(N'{Database}') IS NULL
//     CREATE DATABASE [{Database}];";
//                     await cmd.ExecuteNonQueryAsync();
//                 }

//                 await using var ctx = CreateContext();
//                 await ctx.Database.EnsureDeletedAsync();
//                 await ctx.Database.EnsureCreatedAsync();
//             }
//             catch (Exception ex)
//             {
//                 throw new InvalidOperationException(
//                     $"SQL Server integration environment unavailable for #121. Server={Server}, Database={Database}. {ex.Message}",
//                     ex);
//             }
//         }

//         public Task DisposeAsync() => Task.CompletedTask;

//         [Fact]
//         public async Task SqlServer_SameOrder_ConcurrentDeduct_MutatesOnce()
//         {
//             int preparedItemId;
//             int orderId;
//             decimal initialQty;

//             await using (var seed = CreateContext())
//             {
//                 preparedItemId = await SeedPreparedSaleAsync(seed, bomLitres: 0.5m, stockMl: 10000m);
//                 orderId = await SeedPaidOrderAsync(seed);
//                 initialQty = 10000m;
//             }

//             await using var ctx1 = CreateContext();
//             await using var ctx2 = CreateContext();
//             var t1 = CreateService(ctx1).DeductStockForCommittedOrderAsync(SoldItems(), StoreId, orderId);
//             var t2 = CreateService(ctx2).DeductStockForCommittedOrderAsync(SoldItems(), StoreId, orderId);
//             var results = await Task.WhenAll(t1, t2);

//             Assert.All(results, r => Assert.True(r.IsSuccess, r.Message + " " + r.ErrorCode));

//             await using var verify = CreateContext();
//             var qty = await CanonicalQtyAsync(verify, preparedItemId);
//             Assert.Equal(initialQty - 500m, qty); // 0.5 L once
//             Assert.Equal(1, await verify.InventoryTransactions.CountAsync(t =>
//                 t.ReferenceOrderId == orderId
//                 && t.Type == InventoryTransactionTypeEnum.SALES_DEDUCTION));
//         }

//         [Fact]
//         public async Task SqlServer_TwoOrders_SamePreparedItem_BothSucceedWithoutLostUpdate()
//         {
//             int preparedItemId;
//             int orderA;
//             int orderB;

//             await using (var seed = CreateContext())
//             {
//                 preparedItemId = await SeedPreparedSaleAsync(seed, bomLitres: 0.2m, stockMl: 10000m);
//                 orderA = await SeedPaidOrderAsync(seed);
//                 orderB = await SeedPaidOrderAsync(seed);
//             }

//             await using var ctx1 = CreateContext();
//             await using var ctx2 = CreateContext();
//             var results = await Task.WhenAll(
//                 CreateService(ctx1).DeductStockForCommittedOrderAsync(SoldItems(), StoreId, orderA),
//                 CreateService(ctx2).DeductStockForCommittedOrderAsync(SoldItems(), StoreId, orderB));

//             Assert.All(results, r => Assert.True(r.IsSuccess, r.Message));

//             await using var verify = CreateContext();
//             // 0.2 L * 2 = 400 ml
//             Assert.Equal(9600m, await CanonicalQtyAsync(verify, preparedItemId));
//             Assert.Equal(1, await verify.InventoryTransactions.CountAsync(t => t.ReferenceOrderId == orderA));
//             Assert.Equal(1, await verify.InventoryTransactions.CountAsync(t => t.ReferenceOrderId == orderB));
//             Assert.Equal(1, await verify.StoreInventories.CountAsync(x =>
//                 x.PreparedItemId == preparedItemId && x.BtpIdentityState == BtpIdentityState.Canonical));
//             Assert.Equal(0, await verify.StoreInventories.CountAsync(x =>
//                 x.StoreId == StoreId && x.RecipeId != null && x.PreparedItemId == null));
//         }

//         [Fact]
//         public async Task SqlServer_TwoOrders_TotalDemandExceedsStock_BothFollowBlindSellingPolicy()
//         {
//             int preparedItemId;
//             int orderA;
//             int orderB;
//             const decimal stock = 100m;
//             const decimal perOrderMl = 500m; // 0.5 L

//             await using (var seed = CreateContext())
//             {
//                 preparedItemId = await SeedPreparedSaleAsync(seed, bomLitres: 0.5m, stockMl: stock, reserved: 10m);
//                 orderA = await SeedPaidOrderAsync(seed);
//                 orderB = await SeedPaidOrderAsync(seed);
//             }

//             await using var ctx1 = CreateContext();
//             await using var ctx2 = CreateContext();
//             var results = await Task.WhenAll(
//                 CreateService(ctx1).DeductStockForCommittedOrderAsync(SoldItems(), StoreId, orderA),
//                 CreateService(ctx2).DeductStockForCommittedOrderAsync(SoldItems(), StoreId, orderB));

//             Assert.All(results, r => Assert.True(r.IsSuccess, r.Message));

//             await using var verify = CreateContext();
//             var inv = await verify.StoreInventories.SingleAsync(x =>
//                 x.PreparedItemId == preparedItemId && x.BtpIdentityState == BtpIdentityState.Canonical);
//             Assert.Equal(stock - perOrderMl - perOrderMl, inv.AvailableQty);
//             Assert.Equal(10m, inv.ReservedQty);
//             Assert.Equal(2, await verify.InventoryTransactions.CountAsync(t =>
//                 t.Type == InventoryTransactionTypeEnum.SALES_DEDUCTION
//                 && (t.ReferenceOrderId == orderA || t.ReferenceOrderId == orderB)));
//         }

//         [Fact]
//         public async Task SqlServer_TwoOrders_ReverseRequirementOrder_UsesDeterministicLocks()
//         {
//             // Two prepared items X,Y; Order A needs X then Y via recipe details order;
//             // Order B needs Y then X — aggregation sorts by StoreInventoryId ASC.
//             int piX;
//             int piY;
//             int orderA;
//             int orderB;

//             await using (var seed = CreateContext())
//             {
//                 await PutPreparedModeAsync(seed);
//                 piX = await SeedPiAsync(seed, "PI-X", 5000m);
//                 piY = await SeedPiAsync(seed, "PI-Y", 5000m);
//                 var childX = await SeedChildAsync(seed, piX, "CX");
//                 var childY = await SeedChildAsync(seed, piY, "CY");

//                 await ArchiveActiveDrinkRecipesAsync(seed);

//                 // Parent: details listed Y then X — aggregation still locks by StoreInventoryId ASC
//                 seed.Recipes.Add(new Recipe
//                 {
//                     RecipeCode = "PA-MULTI",
//                     Name = "Parent multi",
//                     Active = true,
//                     Status = "Active",
//                     DrinkId = DrinkId,
//                     SizeId = SizeId,
//                     RecipeDetails = new List<RecipeDetail>
//                     {
//                         new() { ChildRecipeId = childY, Quantity = 0.1m, UnitId = UnitL },
//                         new() { ChildRecipeId = childX, Quantity = 0.1m, UnitId = UnitL }
//                     }
//                 });
//                 await seed.SaveChangesAsync();

//                 orderA = await SeedPaidOrderAsync(seed);
//                 orderB = await SeedPaidOrderAsync(seed);
//             }

//             await using var ctx1 = CreateContext();
//             await using var ctx2 = CreateContext();
//             var results = await Task.WhenAll(
//                 CreateService(ctx1).DeductStockForCommittedOrderAsync(SoldItems(), StoreId, orderA),
//                 CreateService(ctx2).DeductStockForCommittedOrderAsync(SoldItems(), StoreId, orderB));

//             Assert.All(results, r => Assert.True(r.IsSuccess, r.Message));

//             await using var verify = CreateContext();
//             Assert.Equal(4800m, await CanonicalQtyAsync(verify, piX)); // 100ml * 2
//             Assert.Equal(4800m, await CanonicalQtyAsync(verify, piY));
//         }

//         [Fact]
//         public async Task SqlServer_WebhookAndRepair_SameOrder_DeductExactlyOnce()
//         {
//             // Two independent service calls simulate webhook + repair
//             int preparedItemId;
//             int orderId;
//             await using (var seed = CreateContext())
//             {
//                 preparedItemId = await SeedPreparedSaleAsync(seed, bomLitres: 0.3m, stockMl: 3000m);
//                 orderId = await SeedPaidOrderAsync(seed);
//             }

//             await using var ctx1 = CreateContext();
//             await using var ctx2 = CreateContext();
//             var results = await Task.WhenAll(
//                 CreateService(ctx1).DeductStockForCommittedOrderAsync(SoldItems(), StoreId, orderId),
//                 CreateService(ctx2).DeductStockForCommittedOrderAsync(SoldItems(), StoreId, orderId));

//             Assert.All(results, r => Assert.True(r.IsSuccess, r.Message));
//             await using var verify = CreateContext();
//             Assert.Equal(2700m, await CanonicalQtyAsync(verify, preparedItemId));
//             Assert.Equal(1, await verify.InventoryTransactions.CountAsync(t => t.ReferenceOrderId == orderId));
//         }

//         [Fact]
//         public async Task SqlServer_OfflineDuplicate_SameOrder_DeductExactlyOnce()
//         {
//             // Offline created + duplicate repair = same DeductStockForCommittedOrderAsync path
//             await SqlServer_WebhookAndRepair_SameOrder_DeductExactlyOnce();
//         }

//         private static AppDbContext CreateContext()
//         {
//             var options = new DbContextOptionsBuilder<AppDbContext>()
//                 .UseSqlServer(ConnectionString)
//                 .Options;
//             return new AppDbContext(options);
//         }

//         private static InventoryDeductionService CreateService(AppDbContext context)
//         {
//             var physical = new PhysicalUnitConversionService(context, NullLogger<PhysicalUnitConversionService>.Instance);
//             var unit = new UnitConversionService(context, NullLogger<UnitConversionService>.Instance, physical);
//             var caps = new IInventoryWriterCapabilityProvider[]
//             {
//                 new ProductionPreparedWriterCapabilityProvider(),
//                 new PosPreparedWriterCapabilityProvider()
//             };
//             var writer = new InventoryWriterModeService(context, physical, caps);
//             var resolver = new StoreInventoryWriteResolver(context, writer);
//             var normalizer = new RecipeOutputNormalizer(context, physical);
//             var estimated = new EstimatedBomCostService(
//                 context, unit, physical, normalizer, NullLogger<EstimatedBomCostService>.Instance);
//             return new InventoryDeductionService(
//                 context,
//                 NullLogger<InventoryDeductionService>.Instance,
//                 unit,
//                 estimated,
//                 physical,
//                 writerModeService: writer,
//                 writeResolver: resolver);
//         }

//         private static List<POSSoldItemDto> SoldItems() => new()
//         {
//             new() { DrinkId = DrinkId, SizeId = SizeId, Quantity = 1 }
//         };

//         private static async Task PutPreparedModeAsync(AppDbContext ctx)
//         {
//             var cfg = await ctx.StoreInventoryWriterConfigurations.SingleAsync(x => x.StoreId == StoreId);
//             cfg.WriterMode = InventoryWriterMode.PreparedItem;
//             cfg.HasEverActivatedPreparedItem = true;
//             cfg.UpdatedAt = DateTime.UtcNow;
//             await ctx.SaveChangesAsync();
//         }

//         private static async Task<int> SeedPiAsync(AppDbContext ctx, string code, decimal stockMl)
//         {
//             var pi = new PreparedItem
//             {
//                 Code = code,
//                 Name = code,
//                 BaseUnitId = UnitMl,
//                 Active = true
//             };
//             ctx.PreparedItems.Add(pi);
//             await ctx.SaveChangesAsync();
//             ctx.StoreInventories.Add(new StoreInventory
//             {
//                 StoreId = StoreId,
//                 PreparedItemId = pi.PreparedItemId,
//                 RecipeId = null,
//                 BtpIdentityState = BtpIdentityState.Canonical,
//                 QuantitySemanticsStatus = InventoryQuantitySemanticsStatus.BaseUnitConfirmed,
//                 QuantitySemanticsEvidenceType = QuantitySemanticsEvidenceType.SystemCanonicalCreation,
//                 QuantitySemanticsEvidenceReference = "sql-121",
//                 QuantitySemanticsReviewedAt = DateTime.UtcNow,
//                 QuantitySemanticsReviewedByAccountId = 1,
//                 AvailableQty = stockMl,
//                 ReservedQty = 0,
//                 LastUpdated = DateTime.UtcNow
//             });
//             await ctx.SaveChangesAsync();
//             return pi.PreparedItemId;
//         }

//         private static async Task<int> SeedChildAsync(AppDbContext ctx, int preparedItemId, string code)
//         {
//             // Only one Active recipe per PreparedItem — use Active for mapping validity only.
//             var child = new Recipe
//             {
//                 RecipeCode = code,
//                 Name = code,
//                 Active = true,
//                 Status = "Active",
//                 PreparedItemId = preparedItemId,
//                 OutputQuantity = 1m,
//                 OutputUnitId = UnitL
//             };
//             ctx.Recipes.Add(child);
//             await ctx.SaveChangesAsync();
//             return child.RecipeId;
//         }

//         private static async Task<int> SeedPreparedSaleAsync(
//             AppDbContext ctx,
//             decimal bomLitres,
//             decimal stockMl,
//             decimal reserved = 0m)
//         {
//             await PutPreparedModeAsync(ctx);

//             var pi = new PreparedItem
//             {
//                 Code = "PI-SQL-121-" + Guid.NewGuid().ToString("N")[..8],
//                 Name = "SQL PI",
//                 BaseUnitId = UnitMl,
//                 Active = true
//             };
//             ctx.PreparedItems.Add(pi);
//             await ctx.SaveChangesAsync();

//             var child = new Recipe
//             {
//                 RecipeCode = "CHILD-" + Guid.NewGuid().ToString("N")[..6],
//                 Name = "Child",
//                 Active = true,
//                 Status = "Active",
//                 PreparedItemId = pi.PreparedItemId,
//                 OutputQuantity = 1m,
//                 OutputUnitId = UnitL
//             };
//             ctx.Recipes.Add(child);
//             await ctx.SaveChangesAsync();

//             await ArchiveActiveDrinkRecipesAsync(ctx);

//             ctx.Recipes.Add(new Recipe
//             {
//                 RecipeCode = "PARENT-" + Guid.NewGuid().ToString("N")[..6],
//                 Name = "Parent drink",
//                 Active = true,
//                 Status = "Active",
//                 DrinkId = DrinkId,
//                 SizeId = SizeId,
//                 RecipeDetails = new List<RecipeDetail>
//                 {
//                     new() { ChildRecipeId = child.RecipeId, Quantity = bomLitres, UnitId = UnitL }
//                 }
//             });

//             ctx.StoreInventories.Add(new StoreInventory
//             {
//                 StoreId = StoreId,
//                 PreparedItemId = pi.PreparedItemId,
//                 RecipeId = null,
//                 BtpIdentityState = BtpIdentityState.Canonical,
//                 QuantitySemanticsStatus = InventoryQuantitySemanticsStatus.BaseUnitConfirmed,
//                 QuantitySemanticsEvidenceType = QuantitySemanticsEvidenceType.SystemCanonicalCreation,
//                 QuantitySemanticsEvidenceReference = "sql-121",
//                 QuantitySemanticsReviewedAt = DateTime.UtcNow,
//                 QuantitySemanticsReviewedByAccountId = 1,
//                 AvailableQty = stockMl,
//                 ReservedQty = reserved,
//                 LastUpdated = DateTime.UtcNow
//             });
//             await ctx.SaveChangesAsync();
//             return pi.PreparedItemId;
//         }

//         private static async Task<int> SeedPaidOrderAsync(AppDbContext ctx)
//         {
//             var order = new Order
//             {
//                 StoreId = StoreId,
//                 OrderStatusId = SystemConstants.OrderStatuses.Completed,
//                 PaymentStatusId = SystemConstants.PaymentStatuses.Paid,
//                 OrderTypeId = SystemConstants.OrderTypes.DineIn,
//                 Source = "POS",
//                 SubTotal = 1,
//                 Total = 1,
//                 CreatedAt = DateTime.UtcNow
//             };
//             ctx.Orders.Add(order);
//             await ctx.SaveChangesAsync();
//             return order.OrderId;
//         }

//         private static Task<decimal> CanonicalQtyAsync(AppDbContext ctx, int preparedItemId)
//             => ctx.StoreInventories
//                 .Where(x => x.StoreId == StoreId
//                             && x.PreparedItemId == preparedItemId
//                             && x.BtpIdentityState == BtpIdentityState.Canonical)
//                 .Select(x => x.AvailableQty)
//                 .SingleAsync();

//         private static async Task ArchiveActiveDrinkRecipesAsync(AppDbContext ctx)
//         {
//             var oldParents = await ctx.Recipes
//                 .Where(r => r.DrinkId == DrinkId && r.SizeId == SizeId && r.Active)
//                 .ToListAsync();
//             foreach (var op in oldParents)
//             {
//                 op.Active = false;
//                 op.Status = "Archived";
//             }

//             if (oldParents.Count > 0)
//                 await ctx.SaveChangesAsync();
//         }
//     }
// }
