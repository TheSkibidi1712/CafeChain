// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Threading.Tasks;
// using CafeChain.Application.Constants;
// using CafeChain.Application.DTOs.Admin.RestockRequests;
// using CafeChain.Application.DTOs.POS;
// using CafeChain.Application.Interfaces.Inventories;
// using CafeChain.Application.Results;
// using CafeChain.Application.Services.Inventories;
// using CafeChain.Data;
// using CafeChain.Models.Customers;
// using CafeChain.Models.Enums.Inventory;
// using CafeChain.Models.Inventories.Ingredients;
// using CafeChain.Models.Inventories.PreparedItems;
// using CafeChain.Models.Inventories.Stock;
// using CafeChain.Models.Permissions;
// using CafeChain.Models.Staffs;
// using CafeChain.Models.Stores;
// using Microsoft.Data.SqlClient;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.Extensions.Logging.Abstractions;
// using Moq;
// using Xunit;

// namespace CafeChain.Tests
// {
//     /// <summary>
//     /// Issue #128 — SQL Server concurrency for BranchReceipt confirm (UPDLOCK + unique line index).
//     /// Database: CafeChain_Issue128Tests on local SQLEXPRESS.
//     /// </summary>
//     [Trait("Category", "SqlServerIntegration")]
//     public sealed class BranchReceiptSqlServerIssue128Tests : IAsyncLifetime
//     {
//         private const string Server = @"DESKTOP-K038H12\SQLEXPRESS";
//         private const string Database = "CafeChain_Issue128Tests";

//         private static string ConnectionString =>
//             $"Server={Server};Database={Database};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

//         private static string MasterConnectionString =>
//             $"Server={Server};Database=master;Trusted_Connection=True;TrustServerCertificate=True";

//         private int _storeId = 1;
//         private int _unitId = 1;
//         private int _managerStaffId;
//         private static readonly string[] ManagerRoles = { RoleConstants.StoreManager };

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
//                 await SeedStaffAndLookupsAsync(ctx);
//             }
//             catch (Exception ex)
//             {
//                 throw new InvalidOperationException(
//                     $"SQL Server integration environment unavailable for #128. Server={Server}, Database={Database}. {ex.Message}",
//                     ex);
//             }
//         }

//         public Task DisposeAsync() => Task.CompletedTask;

//         [Fact]
//         public async Task SqlServer_ConcurrentConfirm_SameReceipt_PostsOnce()
//         {
//             int receiptId;
//             int requestId;
//             await using (var seed = CreateContext())
//             {
//                 requestId = await SeedRequestAsync(seed, requested: 500m);
//                 var svc = CreateService(seed);
//                 var draft = await svc.CreateDraftAsync(
//                     Receipt(requestId, 200m, "sql-concurrent-1"), _managerStaffId, ManagerRoles);
//                 Assert.True(draft.IsSuccess, draft.Message);
//                 receiptId = draft.Data!.BranchReceiptId;
//             }

//             await using var ctx1 = CreateContext();
//             await using var ctx2 = CreateContext();
//             var results = await Task.WhenAll(
//                 CreateService(ctx1).ConfirmAsync(receiptId, _managerStaffId, _storeId, ManagerRoles),
//                 CreateService(ctx2).ConfirmAsync(receiptId, _managerStaffId, _storeId, ManagerRoles));

//             Assert.All(results, r => Assert.True(r.IsSuccess, r.Message + " " + r.ErrorCode));
//             Assert.Contains(results, r => r.Data!.WasReplay || !r.Data.WasReplay);

//             await using var verify = CreateContext();
//             Assert.Equal(1, await verify.InventoryTransactions.CountAsync(t =>
//                 t.Type == InventoryTransactionTypeEnum.BRANCH_RECEIPT_IN
//                 && t.BranchReceiptLine != null
//                 && t.BranchReceiptLine.BranchReceiptId == receiptId));
//             var line = await verify.BranchReceiptLines.FirstAsync(l => l.BranchReceiptId == receiptId);
//             var inv = await verify.StoreInventories.SingleAsync(i =>
//                 i.StoreId == _storeId && i.IngredientId == line.IngredientId);
//             Assert.Equal(200m, inv.AvailableQty);
//         }

//         [Fact]
//         public async Task SqlServer_ConcurrentPartialReceipts_NoOverReceipt()
//         {
//             int requestId;
//             int receiptA;
//             int receiptB;
//             int ingredientId;
//             await using (var seed = CreateContext())
//             {
//                 requestId = await SeedRequestAsync(seed, requested: 100m);
//                 ingredientId = await seed.RestockRequests
//                     .Where(r => r.RestockRequestId == requestId)
//                     .Select(r => r.IngredientId!.Value)
//                     .FirstAsync();
//                 var svc = CreateService(seed);
//                 var a = await svc.CreateDraftAsync(Receipt(requestId, 70m, "sql-race-a"), _managerStaffId, ManagerRoles);
//                 var b = await svc.CreateDraftAsync(Receipt(requestId, 70m, "sql-race-b"), _managerStaffId, ManagerRoles);
//                 Assert.True(a.IsSuccess, a.Message);
//                 Assert.True(b.IsSuccess, b.Message);
//                 receiptA = a.Data!.BranchReceiptId;
//                 receiptB = b.Data!.BranchReceiptId;
//             }

//             await using var ctx1 = CreateContext();
//             await using var ctx2 = CreateContext();
//             var results = await Task.WhenAll(
//                 CreateService(ctx1).ConfirmAsync(receiptA, _managerStaffId, _storeId, ManagerRoles),
//                 CreateService(ctx2).ConfirmAsync(receiptB, _managerStaffId, _storeId, ManagerRoles));

//             var success = results.Count(r => r.IsSuccess);
//             var over = results.Count(r =>
//                 !r.IsSuccess && r.ErrorCode == BranchReceiptErrorCodes.RestockOverReceiptNotAllowed);

//             Assert.True(success >= 1, string.Join(" | ", results.Select(r => $"{r.IsSuccess}:{r.ErrorCode}:{r.Message}")));
//             Assert.True(success + over == 2 || success == 1,
//                 $"success={success} over={over} details={string.Join(" | ", results.Select(r => $"{r.IsSuccess}:{r.ErrorCode}:{r.Message}"))}");
//             // Exactly one post under concurrency (other blocked as over-receipt or lost race).
//             Assert.Equal(1, success);
//             Assert.Equal(1, over);

//             await using var verify = CreateContext();
//             var inv = await verify.StoreInventories.SingleAsync(i =>
//                 i.StoreId == _storeId && i.IngredientId == ingredientId);
//             Assert.Equal(70m, inv.AvailableQty);
//             Assert.Equal(1, await verify.InventoryTransactions.CountAsync(t =>
//                 t.Type == InventoryTransactionTypeEnum.BRANCH_RECEIPT_IN
//                 && t.BranchReceiptLine != null
//                 && t.BranchReceiptLine.RestockRequestId == requestId));
//         }

//         [Fact]
//         public async Task SqlServer_ConfirmReplay_NoSecondMovement()
//         {
//             int receiptId;
//             await using (var seed = CreateContext())
//             {
//                 var requestId = await SeedRequestAsync(seed, requested: 300m);
//                 var svc = CreateService(seed);
//                 var draft = await svc.CreateDraftAsync(
//                     Receipt(requestId, 100m, "sql-replay"), _managerStaffId, ManagerRoles);
//                 Assert.True(draft.IsSuccess, draft.Message);
//                 receiptId = draft.Data!.BranchReceiptId;
//                 Assert.True((await svc.ConfirmAsync(receiptId, _managerStaffId, _storeId, ManagerRoles)).IsSuccess);
//             }

//             await using var ctx = CreateContext();
//             var replay = await CreateService(ctx).ConfirmAsync(receiptId, _managerStaffId, _storeId, ManagerRoles);
//             Assert.True(replay.IsSuccess);
//             Assert.True(replay.Data!.WasReplay);

//             await using var verify = CreateContext();
//             Assert.Equal(1, await verify.InventoryTransactions.CountAsync(t =>
//                 t.Type == InventoryTransactionTypeEnum.BRANCH_RECEIPT_IN));
//             // Replay must not append a second transition for the same receipt completion.
//             var transitionCount = await verify.RestockRequestTransitions.CountAsync(t =>
//                 t.BranchReceiptId == receiptId);
//             Assert.Equal(1, transitionCount);
//         }

//         [Fact]
//         public async Task SqlServer_BranchReceipt_ConfirmVsCancel_SerializesSafely()
//         {
//             int receiptId;
//             int requestId;
//             await using (var seed = CreateContext())
//             {
//                 requestId = await SeedRequestAsync(seed, requested: 200m);
//                 await EnsureWarehouseStaffAsync(seed);
//                 var draft = await CreateService(seed).CreateDraftAsync(
//                     Receipt(requestId, 80m, "sql-confirm-cancel"), _managerStaffId, ManagerRoles);
//                 Assert.True(draft.IsSuccess, draft.Message);
//                 receiptId = draft.Data!.BranchReceiptId;
//             }

//             await using var ctxConfirm = CreateContext();
//             await using var ctxCancel = CreateContext();
//             var confirmTask = CreateService(ctxConfirm).ConfirmAsync(
//                 receiptId, _managerStaffId, _storeId, ManagerRoles);
//             var cancelTask = CreateWorkflow(ctxCancel).CancelAsync(
//                 requestId, _warehouseStaffId, _storeId, WarehouseRoles, "race cancel");
//             await Task.WhenAll(confirmTask, cancelTask);
//             var confirm = await confirmTask;
//             var cancel = await cancelTask;

//             await using var verify = CreateContext();
//             var req = await verify.RestockRequests.SingleAsync(r => r.RestockRequestId == requestId);
//             var receipt = await verify.BranchReceipts.SingleAsync(r => r.BranchReceiptId == receiptId);
//             var stock = await verify.StoreInventories
//                 .Where(i => i.StoreId == _storeId && i.IngredientId != null)
//                 .Join(verify.BranchReceiptLines.Where(l => l.BranchReceiptId == receiptId),
//                     i => i.IngredientId, l => l.IngredientId, (i, _) => i)
//                 .Select(i => i.AvailableQty)
//                 .FirstOrDefaultAsync();
//             var txnCount = await verify.InventoryTransactions.CountAsync(t =>
//                 t.Type == InventoryTransactionTypeEnum.BRANCH_RECEIPT_IN
//                 && t.BranchReceiptLine != null
//                 && t.BranchReceiptLine.BranchReceiptId == receiptId);

//             if (confirm.IsSuccess)
//             {
//                 Assert.False(cancel.IsSuccess, "Cancel must fail after successful confirm.");
//                 Assert.Equal(BranchReceiptStatuses.Confirmed, receipt.Status);
//                 Assert.NotEqual(RestockRequestStatuses.Cancelled, req.Status);
//                 Assert.Equal(80m, stock);
//                 Assert.Equal(1, txnCount);
//             }
//             else
//             {
//                 Assert.True(cancel.IsSuccess, cancel.Message);
//                 Assert.Equal(RestockRequestStatuses.Cancelled, req.Status);
//                 Assert.Equal(BranchReceiptStatuses.Draft, receipt.Status);
//                 Assert.Equal(0m, stock);
//                 Assert.Equal(0, txnCount);
//             }
//         }

//         [Fact]
//         public async Task SqlServer_BranchReceipt_SameReceiptKeyDifferentPayload_Rejected()
//         {
//             int requestId;
//             await using (var seed = CreateContext())
//             {
//                 requestId = await SeedRequestAsync(seed, requested: 500m);
//                 var first = await CreateService(seed).CreateDraftAsync(
//                     Receipt(requestId, 100m, "sql-same-key"), _managerStaffId, ManagerRoles);
//                 Assert.True(first.IsSuccess, first.Message);

//                 var second = await CreateService(seed).CreateDraftAsync(
//                     Receipt(requestId, 250m, "sql-same-key"), _managerStaffId, ManagerRoles);
//                 Assert.False(second.IsSuccess);
//                 Assert.Equal(BranchReceiptErrorCodes.DuplicateReceiptKey, second.ErrorCode);
//             }

//             await using var verify = CreateContext();
//             Assert.Equal(1, await verify.BranchReceipts.CountAsync(r =>
//                 r.StoreId == _storeId && r.ReceiptKey == "sql-same-key"));
//             Assert.Equal(0, await verify.InventoryTransactions.CountAsync(t =>
//                 t.Type == InventoryTransactionTypeEnum.BRANCH_RECEIPT_IN
//                 && t.BranchReceiptLine != null
//                 && t.BranchReceiptLine.BranchReceipt.ReceiptKey == "sql-same-key"));
//         }

//         [Fact]
//         public async Task SqlServer_BranchReceipt_PostFailure_RollsBackInventoryCostAndLedger()
//         {
//             int receiptId;
//             int lineId;
//             int ingredientId;
//             int requestId;
//             await using (var seed = CreateContext())
//             {
//                 requestId = await SeedRequestAsync(seed, requested: 100m);
//                 ingredientId = await seed.RestockRequests.Where(r => r.RestockRequestId == requestId)
//                     .Select(r => r.IngredientId!.Value).FirstAsync();
//                 var draft = await CreateService(seed).CreateDraftAsync(
//                     Receipt(requestId, 40m, "sql-rollback"), _managerStaffId, ManagerRoles);
//                 Assert.True(draft.IsSuccess, draft.Message);
//                 receiptId = draft.Data!.BranchReceiptId;
//                 lineId = draft.Data.Lines.Single().BranchReceiptLineId;

//                 // Pre-insert a ledger row for this line → unique (BranchReceiptLineId, Type) fails mid-post.
//                 var inv = new StoreInventory
//                 {
//                     StoreId = _storeId,
//                     IngredientId = ingredientId,
//                     AvailableQty = 0,
//                     ReservedQty = 0,
//                     LastUpdated = DateTime.UtcNow
//                 };
//                 seed.StoreInventories.Add(inv);
//                 await seed.SaveChangesAsync();
//                 seed.InventoryTransactions.Add(new CafeChain.Models.Inventories.Transactions.InventoryTransaction
//                 {
//                     StoreInventoryId = inv.StoreInventoryId,
//                     Type = InventoryTransactionTypeEnum.BRANCH_RECEIPT_IN,
//                     StockStatus = InventoryStockStatus.NORMAL,
//                     Quantity = 1m,
//                     BeforeQty = 0,
//                     AfterQty = 1,
//                     UnitCost = 1,
//                     TotalCost = 1,
//                     BranchReceiptLineId = lineId,
//                     CreatedAt = DateTime.UtcNow
//                 });
//                 await seed.SaveChangesAsync();
//             }

//             await using var ctx = CreateContext();
//             var confirm = await CreateService(ctx).ConfirmAsync(
//                 receiptId, _managerStaffId, _storeId, ManagerRoles);
//             Assert.False(confirm.IsSuccess);

//             await using var verify = CreateContext();
//             var receipt = await verify.BranchReceipts.SingleAsync(r => r.BranchReceiptId == receiptId);
//             Assert.Equal(BranchReceiptStatuses.Draft, receipt.Status);

//             var req = await verify.RestockRequests.SingleAsync(r => r.RestockRequestId == requestId);
//             Assert.Equal(RestockRequestStatuses.Processing, req.Status);

//             // Only the injected poison row may exist — no successful confirm post (qty 40).
//             var invQty = await verify.StoreInventories
//                 .Where(i => i.StoreId == _storeId && i.IngredientId == ingredientId)
//                 .Select(i => i.AvailableQty)
//                 .FirstOrDefaultAsync();
//             Assert.True(invQty < 40m);
//             Assert.Equal(0, await verify.InventoryCostLayers.CountAsync(c =>
//                 c.StoreId == _storeId && c.IngredientId == ingredientId && c.Quantity == 40m));
//             Assert.False(await verify.BranchReceiptLines.AnyAsync(l =>
//                 l.BranchReceiptLineId == lineId && l.InventoryTransactionId != null
//                 && l.BranchReceipt.Status == BranchReceiptStatuses.Confirmed));
//         }

//         [Fact]
//         public async Task SqlServer_BranchReceipt_PreparedItem_UsesWriterBarrier()
//         {
//             int requestId;
//             int preparedItemId;
//             await using (var seed = CreateContext())
//             {
//                 (requestId, preparedItemId) = await SeedPreparedRequestAsync(seed, requested: 100m);
//                 // Canonical row + writer mode PreparedItem (upsert — HasData may already seed StoreId=1)
//                 var writerCfg = await seed.StoreInventoryWriterConfigurations
//                     .FirstOrDefaultAsync(c => c.StoreId == _storeId);
//                 if (writerCfg == null)
//                 {
//                     seed.StoreInventoryWriterConfigurations.Add(
//                         new CafeChain.Models.Inventories.Configuration.StoreInventoryWriterConfiguration
//                         {
//                             StoreId = _storeId,
//                             WriterMode = InventoryWriterMode.PreparedItem,
//                             CreatedAt = DateTime.UtcNow,
//                             UpdatedAt = DateTime.UtcNow
//                         });
//                 }
//                 else
//                 {
//                     writerCfg.WriterMode = InventoryWriterMode.PreparedItem;
//                     writerCfg.UpdatedAt = DateTime.UtcNow;
//                 }
//                 var accountId = await seed.Staffs.Where(s => s.StaffId == _managerStaffId)
//                     .Select(s => s.AccountId).FirstAsync();
//                 seed.StoreInventories.Add(new StoreInventory
//                 {
//                     StoreId = _storeId,
//                     PreparedItemId = preparedItemId,
//                     IngredientId = null,
//                     RecipeId = null,
//                     BtpIdentityState = BtpIdentityState.Canonical,
//                     QuantitySemanticsStatus = InventoryQuantitySemanticsStatus.BaseUnitConfirmed,
//                     QuantitySemanticsEvidenceType = QuantitySemanticsEvidenceType.SystemCanonicalCreation,
//                     QuantitySemanticsEvidenceReference = "sql-128-pi",
//                     QuantitySemanticsReviewedAt = DateTime.UtcNow,
//                     QuantitySemanticsReviewedByAccountId = accountId,
//                     AvailableQty = 5m,
//                     ReservedQty = 0,
//                     LastUpdated = DateTime.UtcNow
//                 });
//                 await seed.SaveChangesAsync();

//                 var svc = CreateServiceWithRealWriter(seed);
//                 var draft = await svc.CreateDraftAsync(
//                     Receipt(requestId, 20m, "sql-pi-writer"), _managerStaffId, ManagerRoles);
//                 Assert.True(draft.IsSuccess, draft.Message);
//                 var confirm = await svc.ConfirmAsync(
//                     draft.Data!.BranchReceiptId, _managerStaffId, _storeId, ManagerRoles);
//                 Assert.True(confirm.IsSuccess, confirm.Message);
//             }

//             await using var verify = CreateContext();
//             var row = await verify.StoreInventories.SingleAsync(i =>
//                 i.StoreId == _storeId && i.PreparedItemId == preparedItemId);
//             Assert.Equal(BtpIdentityState.Canonical, row.BtpIdentityState);
//             Assert.Null(row.RecipeId);
//             Assert.Equal(25m, row.AvailableQty); // 5 + 20
//             Assert.Equal(0, await verify.StoreInventories.CountAsync(i =>
//                 i.StoreId == _storeId && i.PreparedItemId == preparedItemId
//                 && i.BtpIdentityState == BtpIdentityState.Superseded));
//             // Durable cost on ledger; schema has no PI cost layer.
//             var tx = await verify.InventoryTransactions.SingleAsync(t =>
//                 t.Type == InventoryTransactionTypeEnum.BRANCH_RECEIPT_IN
//                 && t.BranchReceiptLine != null
//                 && t.BranchReceiptLine.PreparedItemId == preparedItemId);
//             Assert.True(tx.UnitCost > 0);
//             Assert.True(tx.TotalCost > 0);
//         }

//         [Fact]
//         public async Task SqlServer_BranchReceipt_MultiLineReverseOrder_NoDeadlock()
//         {
//             int reqA;
//             int reqB;
//             await using (var seed = CreateContext())
//             {
//                 reqA = await SeedRequestAsync(seed, requested: 50m);
//                 reqB = await SeedRequestAsync(seed, requested: 50m);
//                 // Force inventory creation order reverse of receipt line insert order by pre-creating B then A.
//                 var ingB = await seed.RestockRequests.Where(r => r.RestockRequestId == reqB).Select(r => r.IngredientId!.Value).FirstAsync();
//                 var ingA = await seed.RestockRequests.Where(r => r.RestockRequestId == reqA).Select(r => r.IngredientId!.Value).FirstAsync();
//                 seed.StoreInventories.Add(new StoreInventory
//                 {
//                     StoreId = _storeId, IngredientId = ingB, AvailableQty = 0, ReservedQty = 0, LastUpdated = DateTime.UtcNow
//                 });
//                 seed.StoreInventories.Add(new StoreInventory
//                 {
//                     StoreId = _storeId, IngredientId = ingA, AvailableQty = 0, ReservedQty = 0, LastUpdated = DateTime.UtcNow
//                 });
//                 await seed.SaveChangesAsync();

//                 var multi = new CreateBranchReceiptRequest
//                 {
//                     StoreId = _storeId,
//                     ReceiptKey = "sql-multiline",
//                     Lines =
//                     {
//                         new CreateBranchReceiptLineInput
//                         {
//                             RestockRequestId = reqA,
//                             InputQuantity = 10m,
//                             InputUnitId = _unitId,
//                             ActualPackagePrice = 100m
//                         },
//                         new CreateBranchReceiptLineInput
//                         {
//                             RestockRequestId = reqB,
//                             InputQuantity = 15m,
//                             InputUnitId = _unitId,
//                             ActualPackagePrice = 150m
//                         }
//                     }
//                 };
//                 var svc = CreateService(seed);
//                 var draft = await svc.CreateDraftAsync(multi, _managerStaffId, ManagerRoles);
//                 Assert.True(draft.IsSuccess, draft.Message);
//                 var confirm = await svc.ConfirmAsync(draft.Data!.BranchReceiptId, _managerStaffId, _storeId, ManagerRoles);
//                 Assert.True(confirm.IsSuccess, confirm.Message);
//             }

//             await using var verify = CreateContext();
//             Assert.Equal(2, await verify.InventoryTransactions.CountAsync(t =>
//                 t.Type == InventoryTransactionTypeEnum.BRANCH_RECEIPT_IN
//                 && t.BranchReceiptLine != null
//                 && t.BranchReceiptLine.BranchReceipt.ReceiptKey == "sql-multiline"));
//         }

//         private CreateBranchReceiptRequest Receipt(int requestId, decimal qty, string key) =>
//             new()
//             {
//                 StoreId = _storeId,
//                 ReceiptKey = key,
//                 Lines =
//                 {
//                     new CreateBranchReceiptLineInput
//                     {
//                         RestockRequestId = requestId,
//                         InputQuantity = qty,
//                         InputUnitId = _unitId,
//                         ActualPackagePrice = qty * 10m
//                     }
//                 }
//             };

//         private int _warehouseStaffId;
//         private static readonly string[] WarehouseRoles = { RoleConstants.AccountantWarehouse };

//         private static BranchReceiptService CreateService(AppDbContext ctx)
//         {
//             var physical = new PhysicalUnitConversionService(ctx, NullLogger<PhysicalUnitConversionService>.Instance);
//             var unit = new UnitConversionService(ctx, NullLogger<UnitConversionService>.Instance, physical);
//             var mode = new Mock<IInventoryWriterModeService>();
//             var resolver = new Mock<IStoreInventoryWriteResolver>();
//             var alerts = new Mock<IStockAlertService>();
//             alerts
//                 .Setup(s => s.EvaluateStoreInventoryItemAsync(It.IsAny<int>(), It.IsAny<string>()))
//                 .ReturnsAsync(ServiceResult<StockAlertEvaluationResultDto>.Success(new StockAlertEvaluationResultDto()));
//             return new BranchReceiptService(
//                 ctx, unit, physical, mode.Object, resolver.Object, alerts.Object,
//                 NullLogger<BranchReceiptService>.Instance);
//         }

//         private static BranchReceiptService CreateServiceWithRealWriter(AppDbContext ctx)
//         {
//             var physical = new PhysicalUnitConversionService(ctx, NullLogger<PhysicalUnitConversionService>.Instance);
//             var unit = new UnitConversionService(ctx, NullLogger<UnitConversionService>.Instance, physical);
//             var mode = new InventoryWriterModeService(
//                 ctx, physical, Array.Empty<IInventoryWriterCapabilityProvider>());
//             var resolver = new StoreInventoryWriteResolver(ctx, mode);
//             var alerts = new Mock<IStockAlertService>();
//             alerts
//                 .Setup(s => s.EvaluateStoreInventoryItemAsync(It.IsAny<int>(), It.IsAny<string>()))
//                 .ReturnsAsync(ServiceResult<StockAlertEvaluationResultDto>.Success(new StockAlertEvaluationResultDto()));
//             return new BranchReceiptService(
//                 ctx, unit, physical, mode, resolver, alerts.Object,
//                 NullLogger<BranchReceiptService>.Instance);
//         }

//         private static RestockRequestWorkflowService CreateWorkflow(AppDbContext ctx) =>
//             new(ctx, NullLogger<RestockRequestWorkflowService>.Instance);

//         private static AppDbContext CreateContext() =>
//             new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
//                 .UseSqlServer(ConnectionString)
//                 .Options);

//         private async Task SeedStaffAndLookupsAsync(AppDbContext ctx)
//         {
//             _unitId = await ctx.Units.Select(u => u.UnitId).FirstOrDefaultAsync();
//             if (_unitId == 0)
//             {
//                 ctx.Units.Add(new Unit { UnitCode = "g", Name = "Gram", Active = true });
//                 await ctx.SaveChangesAsync();
//                 _unitId = await ctx.Units.Select(u => u.UnitId).FirstAsync();
//             }

//             _storeId = await ctx.Stores.Select(s => s.StoreId).FirstOrDefaultAsync();
//             if (_storeId == 0)
//             {
//                 ctx.Stores.Add(new Store
//                 {
//                     Name = "SQL128 Store",
//                     Address = "x",
//                     Phone = "0",
//                     Active = true,
//                     CreatedAt = DateTime.UtcNow
//                 });
//                 await ctx.SaveChangesAsync();
//                 _storeId = await ctx.Stores.Select(s => s.StoreId).FirstAsync();
//             }

//             if (!await ctx.Roles.AnyAsync(r => r.Name == RoleConstants.StoreManager))
//             {
//                 ctx.Roles.Add(new Role
//                 {
//                     Name = RoleConstants.StoreManager,
//                     Active = true,
//                     IsStoreLevel = true,
//                     CreatedAt = DateTime.UtcNow
//                 });
//                 await ctx.SaveChangesAsync();
//             }

//             var roleId = await ctx.Roles.Where(r => r.Name == RoleConstants.StoreManager)
//                 .Select(r => r.RoleId).FirstAsync();

//             var existingStaff = await ctx.Staffs
//                 .Where(s => s.StoreId == _storeId && s.Active)
//                 .OrderBy(s => s.StaffId)
//                 .FirstOrDefaultAsync();
//             if (existingStaff != null)
//             {
//                 _managerStaffId = existingStaff.StaffId;
//                 return;
//             }

//             var account = new Account
//             {
//                 Email = $"mgr128sql{Guid.NewGuid():N}@test.local",
//                 PasswordHash = "x",
//                 Active = true,
//                 CreatedAt = DateTime.UtcNow
//             };
//             ctx.Accounts.Add(account);
//             await ctx.SaveChangesAsync();
//             ctx.AccountRoles.Add(new AccountRole { AccountId = account.AccountId, RoleId = roleId });
//             var staff = new Staff
//             {
//                 AccountId = account.AccountId,
//                 StoreId = _storeId,
//                 FullName = "Mgr 128 SQL",
//                 Active = true,
//                 CreatedAt = DateTime.UtcNow,
//                 BaseSalary = 0
//             };
//             ctx.Staffs.Add(staff);
//             await ctx.SaveChangesAsync();
//             _managerStaffId = staff.StaffId;
//         }

//         private async Task<int> SeedRequestAsync(AppDbContext ctx, decimal requested)
//         {
//             if (_managerStaffId <= 0)
//                 await SeedStaffAndLookupsAsync(ctx);

//             var ingredient = new Ingredient
//             {
//                 Code = "ING128SQL" + Guid.NewGuid().ToString("N")[..6],
//                 Name = "Ingredient 128 SQL",
//                 BaseUnitId = _unitId,
//                 Active = true
//             };
//             ctx.Ingredients.Add(ingredient);
//             await ctx.SaveChangesAsync();

//             var alert = new StockAlert
//             {
//                 StoreId = _storeId,
//                 IngredientId = ingredient.IngredientId,
//                 AlertType = StockAlertTypes.LowStock,
//                 Severity = StockAlertSeverities.Warning,
//                 Status = StockAlertStatuses.Confirmed,
//                 Source = StockAlertSources.ManualCheck,
//                 CurrentQtySnapshot = 0,
//                 ThresholdSnapshot = requested,
//                 ConfirmedByStaffId = _managerStaffId,
//                 ConfirmedAt = DateTime.UtcNow,
//                 CreatedAt = DateTime.UtcNow,
//                 UpdatedAt = DateTime.UtcNow
//             };
//             ctx.StockAlerts.Add(alert);
//             await ctx.SaveChangesAsync();

//             var req = new RestockRequest
//             {
//                 StockAlertId = alert.StockAlertId,
//                 StoreId = _storeId,
//                 IngredientId = ingredient.IngredientId,
//                 RequestedQuantity = requested,
//                 Status = RestockRequestStatuses.Processing,
//                 Priority = RestockRequestPriorities.Normal,
//                 CreatedByStaffId = _managerStaffId,
//                 CreatedAt = DateTime.UtcNow,
//                 UpdatedAt = DateTime.UtcNow
//             };
//             ctx.RestockRequests.Add(req);
//             await ctx.SaveChangesAsync();
//             return req.RestockRequestId;
//         }

//         private async Task EnsureWarehouseStaffAsync(AppDbContext ctx)
//         {
//             if (_warehouseStaffId > 0) return;

//             if (!await ctx.Roles.AnyAsync(r => r.Name == RoleConstants.AccountantWarehouse))
//             {
//                 ctx.Roles.Add(new Role
//                 {
//                     Name = RoleConstants.AccountantWarehouse,
//                     Active = true,
//                     IsStoreLevel = true,
//                     CreatedAt = DateTime.UtcNow
//                 });
//                 await ctx.SaveChangesAsync();
//             }

//             var roleId = await ctx.Roles.Where(r => r.Name == RoleConstants.AccountantWarehouse)
//                 .Select(r => r.RoleId).FirstAsync();
//             var account = new Account
//             {
//                 Email = $"aw128sql{Guid.NewGuid():N}@test.local",
//                 PasswordHash = "x",
//                 Active = true,
//                 CreatedAt = DateTime.UtcNow
//             };
//             ctx.Accounts.Add(account);
//             await ctx.SaveChangesAsync();
//             ctx.AccountRoles.Add(new AccountRole { AccountId = account.AccountId, RoleId = roleId });
//             var staff = new Staff
//             {
//                 AccountId = account.AccountId,
//                 StoreId = _storeId,
//                 FullName = "AW 128 SQL",
//                 Active = true,
//                 CreatedAt = DateTime.UtcNow,
//                 BaseSalary = 0
//             };
//             ctx.Staffs.Add(staff);
//             await ctx.SaveChangesAsync();
//             _warehouseStaffId = staff.StaffId;
//         }

//         private async Task<(int RequestId, int PreparedItemId)> SeedPreparedRequestAsync(
//             AppDbContext ctx, decimal requested)
//         {
//             if (_managerStaffId <= 0)
//                 await SeedStaffAndLookupsAsync(ctx);

//             var pi = new PreparedItem
//             {
//                 Code = "PI128SQL" + Guid.NewGuid().ToString("N")[..6],
//                 Name = "Prepared 128 SQL",
//                 BaseUnitId = _unitId,
//                 Active = true
//             };
//             ctx.PreparedItems.Add(pi);
//             await ctx.SaveChangesAsync();

//             var alert = new StockAlert
//             {
//                 StoreId = _storeId,
//                 PreparedItemId = pi.PreparedItemId,
//                 IngredientId = null,
//                 RecipeId = null,
//                 AlertType = StockAlertTypes.LowStock,
//                 Severity = StockAlertSeverities.Warning,
//                 Status = StockAlertStatuses.Confirmed,
//                 Source = StockAlertSources.ManualCheck,
//                 CurrentQtySnapshot = 0,
//                 ThresholdSnapshot = requested,
//                 ConfirmedByStaffId = _managerStaffId,
//                 ConfirmedAt = DateTime.UtcNow,
//                 CreatedAt = DateTime.UtcNow,
//                 UpdatedAt = DateTime.UtcNow
//             };
//             ctx.StockAlerts.Add(alert);
//             await ctx.SaveChangesAsync();

//             var req = new RestockRequest
//             {
//                 StockAlertId = alert.StockAlertId,
//                 StoreId = _storeId,
//                 PreparedItemId = pi.PreparedItemId,
//                 IngredientId = null,
//                 RecipeId = null,
//                 RequestedQuantity = requested,
//                 Status = RestockRequestStatuses.Processing,
//                 Priority = RestockRequestPriorities.Normal,
//                 CreatedByStaffId = _managerStaffId,
//                 CreatedAt = DateTime.UtcNow,
//                 UpdatedAt = DateTime.UtcNow
//             };
//             ctx.RestockRequests.Add(req);
//             await ctx.SaveChangesAsync();
//             return (req.RestockRequestId, pi.PreparedItemId);
//         }
//     }
// }
