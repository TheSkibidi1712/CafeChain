using System.Collections.Concurrent;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.DTOs.Admin.RestockRequests;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Inventories;
using CafeChain.Application.Services.Security;
using CafeChain.Data;
using CafeChain.Models.Customers;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Procurement;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Inventories.Suppliers;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CafeChain.Tests;

[Trait("Category", "SqlServerIntegration")]
public sealed class PurchaseAdviceBatchPoE2EIssue189Tests : IAsyncLifetime
{
    private const string Database = "CafeChain_PurchaseAdviceBatchPoTests";
    private static string ConnectionString => SqlServerTestConnection.Create(Database);

    public async Task InitializeAsync()
    {
        await using var master = new SqlConnection(SqlServerTestConnection.MasterConnectionString());
        await master.OpenAsync();
        await using var create = master.CreateCommand();
        create.CommandText = $"IF DB_ID(N'{Database}') IS NULL CREATE DATABASE [{Database}];";
        await create.ExecuteNonQueryAsync();
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        await using var master = new SqlConnection(SqlServerTestConnection.MasterConnectionString());
        await master.OpenAsync();
        await using var drop = master.CreateCommand();
        drop.CommandText = $"IF DB_ID(N'{Database}') IS NOT NULL BEGIN ALTER DATABASE [{Database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{Database}]; END";
        await drop.ExecuteNonQueryAsync();
    }

    [Fact]
    public void RuntimeModel_ProcurementEntitiesSupportLazyLoadingProxies()
    {
        using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseLazyLoadingProxies()
            .UseSqlServer(ConnectionString)
            .Options);

        Assert.NotNull(context.Model.FindEntityType(typeof(PurchaseOrderBatch)));
        Assert.NotNull(context.Model.FindEntityType(typeof(PurchaseOrderBatchDocumentRevision)));
    }

    [Fact]
    public async Task SqlServer_PerStoreReceipt_UpdatesOnlyItsAllocationAndMasterProgress()
    {
        await using var db = CreateContext();
        var seed = await SeedFoundationAsync(db);
        var prepared = await CreateReviewedBatchAsync(db, seed);
        var approved = await BatchService(db).ApproveAsync(
            prepared.Batch.PurchaseOrderBatchId,
            new() { RowVersion = prepared.Batch.RowVersion },
            Owner(seed));
        Assert.True(approved.IsSuccess, approved.Message);

        var children = approved.Data!.ChildPurchaseOrders.OrderBy(x => x.StoreId).ToArray();
        var firstChild = children.Single(x => x.StoreId == seed.Store1Id);
        var secondChild = children.Single(x => x.StoreId == seed.Store2Id);
        var firstDraft = await PrepareReceiptAsync(
            db,
            firstChild.PurchaseOrderId,
            seed.Manager1Id,
            seed.Store1Id,
            5m);

        var firstConfirm = await ReceiptService(db).ConfirmAsync(
            firstDraft.BranchReceiptId,
            seed.Manager1Id,
            seed.Store1Id,
            new[] { RoleConstants.StoreManager },
            firstDraft.RowVersion);
        var replay = await ReceiptService(db).ConfirmAsync(
            firstDraft.BranchReceiptId,
            seed.Manager1Id,
            seed.Store1Id,
            new[] { RoleConstants.StoreManager },
            firstDraft.RowVersion);

        Assert.True(firstConfirm.IsSuccess, firstConfirm.Message);
        Assert.True(replay.IsSuccess, replay.Message);
        db.ChangeTracker.Clear();
        Assert.Equal(
            PurchaseOrderBatchStatuses.PartiallyReceived,
            (await db.PurchaseOrderBatches.AsNoTracking().SingleAsync()).Status);
        Assert.Equal(
            PurchaseOrderStatuses.Completed,
            (await db.PurchaseOrders.AsNoTracking().SingleAsync(x => x.PurchaseOrderId == firstChild.PurchaseOrderId)).Status);
        Assert.Equal(
            PurchaseOrderStatuses.Approved,
            (await db.PurchaseOrders.AsNoTracking().SingleAsync(x => x.PurchaseOrderId == secondChild.PurchaseOrderId)).Status);
        Assert.Equal(
            5m,
            await db.StoreInventories.AsNoTracking()
                .Where(x => x.StoreId == seed.Store1Id && x.IngredientId == seed.IngredientId)
                .SumAsync(x => x.AvailableQty));
        Assert.Equal(
            0m,
            await db.StoreInventories.AsNoTracking()
                .Where(x => x.StoreId == seed.Store2Id && x.IngredientId == seed.IngredientId)
                .SumAsync(x => x.AvailableQty));
        Assert.Single(await db.PurchaseOrderReceiptPostings.AsNoTracking().ToListAsync());
        Assert.Single(await db.PurchaseAdviceFulfillmentPostings.AsNoTracking().ToListAsync());
        Assert.Single(await db.InventoryTransactions.AsNoTracking().ToListAsync());

        var adviceLines = await db.PurchaseAdviceLines.AsNoTracking()
            .Include(x => x.PurchaseAdvice)
            .ToArrayAsync();
        Assert.Equal(
            5m,
            adviceLines.Single(x => x.PurchaseAdvice.StoreId == seed.Store1Id).AcceptedBaseQuantity);
        Assert.Equal(
            0m,
            adviceLines.Single(x => x.PurchaseAdvice.StoreId == seed.Store2Id).AcceptedBaseQuantity);

        var secondDraft = await PrepareReceiptAsync(
            db,
            secondChild.PurchaseOrderId,
            seed.Manager2Id,
            seed.Store2Id,
            5m);
        var secondConfirm = await ReceiptService(db).ConfirmAsync(
            secondDraft.BranchReceiptId,
            seed.Manager2Id,
            seed.Store2Id,
            new[] { RoleConstants.StoreManager },
            secondDraft.RowVersion);

        Assert.True(secondConfirm.IsSuccess, secondConfirm.Message);
        db.ChangeTracker.Clear();
        Assert.Equal(
            PurchaseOrderBatchStatuses.Completed,
            (await db.PurchaseOrderBatches.AsNoTracking().SingleAsync()).Status);
        Assert.Equal(2, await db.PurchaseOrderReceiptPostings.AsNoTracking().CountAsync());
        Assert.Equal(2, await db.PurchaseAdviceFulfillmentPostings.AsNoTracking().CountAsync());
        Assert.Equal(2, await db.InventoryTransactions.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task SqlServer_E2E_PaBatchPdfZaloAndConcurrentReceiving_AreConsistent()
    {
        await using var db = CreateContext();
        var seed = await SeedFoundationAsync(db);
        var prepared = await CreateReviewedBatchAsync(db, seed);

        var batchService = BatchService(db);
        var approved = await batchService.ApproveAsync(prepared.Batch.PurchaseOrderBatchId,
            new() { RowVersion = prepared.Batch.RowVersion }, Owner(seed));
        Assert.True(approved.IsSuccess, approved.Message);

        var storage = new MemoryStorage();
        var documents = DocumentService(db, storage);
        var generated = await documents.GenerateAsync(prepared.Batch.PurchaseOrderBatchId, Owner(seed));
        Assert.True(generated.IsSuccess, generated.Message);
        Assert.Equal(1, generated.Data!.RevisionNumber);
        var sent = await documents.MarkSentAsync(prepared.Batch.PurchaseOrderBatchId, generated.Data.RevisionId,
            SendRequest(generated.Data.RowVersion, "issue-189-e2e"), Warehouse(seed));
        Assert.True(sent.IsSuccess, sent.Message);
        Assert.Equal(PurchaseOrderBatchDocumentChannels.ZaloManual, sent.Data!.SentChannel);

        var children = approved.Data!.ChildPurchaseOrders.OrderBy(x => x.StoreId).ToArray();
        Assert.Equal(2, children.Length);
        var firstDraft = await PrepareReceiptAsync(db, children[0].PurchaseOrderId, seed.Manager1Id, seed.Store1Id, 5m);
        var secondDraft = await PrepareReceiptAsync(db, children[1].PurchaseOrderId, seed.Manager2Id, seed.Store2Id, 5m);

        var crossStore = await ReceiptService(db).CreateOrOpenPurchaseOrderDraftAsync(
            children[0].PurchaseOrderId, seed.Manager2Id, seed.Store2Id, new[] { RoleConstants.StoreManager });
        Assert.False(crossStore.IsSuccess);
        Assert.Equal(BranchReceiptErrorCodes.Unauthorized, crossStore.ErrorCode);

        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        var confirmations = await Task.WhenAll(
            ReceiptService(firstContext).ConfirmAsync(firstDraft.BranchReceiptId, seed.Manager1Id, seed.Store1Id,
                new[] { RoleConstants.StoreManager }, firstDraft.RowVersion),
            ReceiptService(secondContext).ConfirmAsync(secondDraft.BranchReceiptId, seed.Manager2Id, seed.Store2Id,
                new[] { RoleConstants.StoreManager }, secondDraft.RowVersion));
        Assert.All(confirmations, x => Assert.True(x.IsSuccess, x.Message));

        await using var verify = CreateContext();
        var batch = await verify.PurchaseOrderBatches.AsNoTracking().SingleAsync();
        Assert.Equal(PurchaseOrderBatchStatuses.Completed, batch.Status);
        Assert.Equal(2, await verify.PurchaseOrders.CountAsync(x => x.Status == PurchaseOrderStatuses.Completed));
        Assert.Equal(2, await verify.RestockRequests.CountAsync(x => x.Status == RestockRequestStatuses.Completed));
        Assert.Equal(10m, await verify.RestockFulfillmentPostings.SumAsync(x => x.Quantity));
        Assert.Equal(2, await verify.InventoryTransactions.CountAsync(x => x.Type == Models.Enums.Inventory.InventoryTransactionTypeEnum.BRANCH_RECEIPT_IN));
        Assert.Equal(2, await verify.InventoryCostLayers.CountAsync());
        Assert.Equal(5m, await verify.StoreInventories.Where(x => x.StoreId == seed.Store1Id && x.IngredientId == seed.IngredientId).SumAsync(x => x.AvailableQty));
        Assert.Equal(5m, await verify.StoreInventories.Where(x => x.StoreId == seed.Store2Id && x.IngredientId == seed.IngredientId).SumAsync(x => x.AvailableQty));
        Assert.Single(await verify.PurchaseOrderBatchDocumentRevisions.Where(x => x.Status == PurchaseOrderBatchDocumentStatuses.Sent).ToListAsync());
    }

    [Fact]
    public async Task SqlServer_ConcurrentSend_OneLogicalSendRecord()
    {
        await using var seedContext = CreateContext();
        var seed = await SeedFoundationAsync(seedContext);
        var prepared = await CreateReviewedBatchAsync(seedContext, seed);
        var approved = await BatchService(seedContext).ApproveAsync(prepared.Batch.PurchaseOrderBatchId,
            new() { RowVersion = prepared.Batch.RowVersion }, Owner(seed));
        var storage = new MemoryStorage();
        var generated = await DocumentService(seedContext, storage).GenerateAsync(prepared.Batch.PurchaseOrderBatchId, Owner(seed));
        Assert.True(approved.IsSuccess && generated.IsSuccess);

        await using var first = CreateContext();
        await using var second = CreateContext();
        var results = await Task.WhenAll(
            DocumentService(first, storage).MarkSentAsync(prepared.Batch.PurchaseOrderBatchId, generated.Data!.RevisionId,
                SendRequest(generated.Data.RowVersion, "issue-189-send-a"), Warehouse(seed)),
            DocumentService(second, storage).MarkSentAsync(prepared.Batch.PurchaseOrderBatchId, generated.Data.RevisionId,
                SendRequest(generated.Data.RowVersion, "issue-189-send-b"), Warehouse(seed)));
        Assert.All(results, x => Assert.True(x.IsSuccess, x.Message));

        await using var verify = CreateContext();
        var revision = Assert.Single(await verify.PurchaseOrderBatchDocumentRevisions.AsNoTracking().ToListAsync());
        Assert.Equal(PurchaseOrderBatchDocumentStatuses.Sent, revision.Status);
        Assert.Equal(PurchaseOrderBatchDocumentChannels.ZaloManual, revision.SentChannel);
        Assert.NotNull(revision.SentAtUtc);
        Assert.False(string.IsNullOrWhiteSpace(revision.SentIdempotencyKey));
    }

    [Fact]
    public async Task SqlServer_AuthorizationMatrix_RejectsCrossScopeAndBusinessMutations()
    {
        await using var db = CreateContext();
        var seed = await SeedFoundationAsync(db);
        var adviceService = AdviceService(db);
        var own = await adviceService.CreateAsync(CreateAdviceRequest(seed.Restock1Id, seed.Store1Id, seed.Restock1RowVersion), Warehouse(seed));
        Assert.True(own.IsSuccess, own.Message);

        var otherStore = await adviceService.CreateAsync(CreateAdviceRequest(seed.Restock2Id, seed.Store2Id, seed.Restock2RowVersion), Manager1(seed));
        var cashier = await adviceService.CreateAsync(CreateAdviceRequest(seed.Restock2Id, seed.Store2Id, seed.Restock2RowVersion),
            Actor(seed.Manager2Id, seed.Store2Id, RoleConstants.SalesStaff));
        var systemAdmin = await adviceService.CreateAsync(CreateAdviceRequest(seed.Restock2Id, seed.Store2Id, seed.Restock2RowVersion),
            Actor(seed.Manager2Id, seed.Store2Id, RoleConstants.SystemAdmin));
        Assert.All(new[] { otherStore, cashier }, x => Assert.False(x.IsSuccess));
        Assert.True(systemAdmin.IsSuccess, systemAdmin.Message);

        var submitted = await adviceService.SubmitAsync(own.Data!.PurchaseAdviceId,
            new() { RowVersion = own.Data.RowVersion }, Warehouse(seed));
        var reviewed = await adviceService.StartReviewAsync(submitted.Data!.PurchaseAdviceId,
            new() { RowVersion = submitted.Data.RowVersion }, Warehouse(seed));
        Assert.True(reviewed.IsSuccess, reviewed.Message);

        var supervisorConsolidation = await ConsolidationService(db).PreviewAsync(new()
        {
            SupplierId = seed.SupplierId,
            Lines = { new() { PurchaseAdviceLineId = reviewed.Data!.Lines.Single().PurchaseAdviceLineId, IngredientSupplierId = seed.OfferId, PackageCount = 5, RowVersion = reviewed.Data.Lines.Single().RowVersion } }
        }, Actor(seed.Manager1Id, seed.Store1Id, RoleConstants.ShiftSupervisor));
        Assert.False(supervisorConsolidation.IsSuccess);

        var accountantReceipt = await ReceiptService(db).CreateOrOpenPurchaseOrderDraftAsync(
            999999, seed.WarehouseId, null, new[] { RoleConstants.AccountantWarehouse });
        Assert.False(accountantReceipt.IsSuccess);
        Assert.Equal(BranchReceiptErrorCodes.Unauthorized, accountantReceipt.ErrorCode);
    }

    [Fact]
    public async Task SqlServer_SingleSourceCannotCreateConsolidatedPurchaseOrder()
    {
        await using var db = CreateContext();
        var seed = await SeedFoundationAsync(db);
        var reviewed = await CreateReviewedAdviceAsync(
            AdviceService(db),
            CreateAdviceRequest(seed.Restock1Id, seed.Store1Id, seed.Restock1RowVersion),
            Manager1(seed),
            Warehouse(seed));
        var line = Assert.Single(reviewed.Lines);

        var result = await BatchService(db).CreateAsync(new CreatePurchaseOrderBatchRequest
        {
            SupplierId = seed.SupplierId,
            RequestKey = "single-source-189",
            Lines =
            {
                new()
                {
                    PurchaseAdviceLineId = line.PurchaseAdviceLineId,
                    IngredientSupplierId = seed.OfferId,
                    PackageCount = 5,
                    RowVersion = line.RowVersion
                }
            }
        }, Warehouse(seed));

        Assert.False(result.IsSuccess);
        Assert.Contains("một nguồn nhu cầu", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await db.PurchaseOrderBatches.ToListAsync());
        Assert.Empty(await db.PurchaseOrders.ToListAsync());
    }

    [Fact]
    public async Task SqlServer_SinglePaCreatesOneNormalPoAndRetryReturnsExistingOrder()
    {
        await using var db = CreateContext();
        var seed = await SeedFoundationAsync(db);
        var reviewed = await CreateReviewedAdviceAsync(
            AdviceService(db),
            CreateAdviceRequest(seed.Restock1Id, seed.Store1Id, seed.Restock1RowVersion),
            Manager1(seed),
            Warehouse(seed));
        var adviceLine = Assert.Single(reviewed.Lines);
        var request = new CreatePurchaseOrderRequest
        {
            StoreId = seed.Store1Id,
            SupplierId = seed.SupplierId,
            Lines =
            {
                new()
                {
                    PurchaseAdviceLineId = adviceLine.PurchaseAdviceLineId,
                    PurchaseAdviceLineRowVersion = adviceLine.RowVersion,
                    RestockRequestId = seed.Restock1Id,
                    IngredientId = seed.IngredientId,
                    IngredientSupplierId = seed.OfferId,
                    PurchaseMode = PurchaseMode.Packaged,
                    PackageCount = 5,
                    ProcurementUnitId = adviceLine.ProcurementUnitId
                }
            }
        };
        var service = NormalOrderService(db);

        var first = await service.CreateDraftAsync(
            request,
            seed.WarehouseId,
            new[] { RoleConstants.AccountantWarehouse });
        var replay = await service.CreateDraftAsync(
            request,
            seed.WarehouseId,
            new[] { RoleConstants.AccountantWarehouse });

        Assert.True(first.IsSuccess, first.Message);
        Assert.True(replay.IsSuccess, replay.Message);
        Assert.Equal(first.Data!.PurchaseOrderId, replay.Data!.PurchaseOrderId);
        Assert.Single(await db.PurchaseOrders.ToListAsync());
        Assert.Empty(await db.PurchaseOrderBatches.ToListAsync());
        var persistedLine = Assert.Single(await db.PurchaseOrderLines.ToListAsync());
        Assert.Equal(adviceLine.PurchaseAdviceLineId, persistedLine.PurchaseAdviceLineId);
        Assert.Equal(5m, (await db.PurchaseAdviceLines.SingleAsync()).AllocatedToPoBaseQuantity);
    }

    private static async Task<PurchaseOrderReceiptDraftDto> PrepareReceiptAsync(
        AppDbContext db, int purchaseOrderId, int staffId, int storeId, decimal packageQuantity)
    {
        var service = ReceiptService(db);
        var draft = await service.CreateOrOpenPurchaseOrderDraftAsync(
            purchaseOrderId, staffId, storeId, new[] { RoleConstants.StoreManager });
        Assert.True(draft.IsSuccess, draft.Message);
        var line = Assert.Single(draft.Data!.Lines);
        var saved = await service.SavePurchaseOrderDraftAsync(new()
        {
            BranchReceiptId = draft.Data.BranchReceiptId,
            RowVersion = draft.Data.RowVersion,
            ReferenceNumber = "E2E-189",
            Lines = { new() { PurchaseOrderLineId = line.PurchaseOrderLineId, ActualReceivedQuantity = packageQuantity } }
        }, staffId, storeId, new[] { RoleConstants.StoreManager });
        Assert.True(saved.IsSuccess, saved.Message);
        return saved.Data!;
    }

    private static async Task<Prepared> CreateReviewedBatchAsync(AppDbContext db, Seed seed)
    {
        var advice = AdviceService(db);
        var first = await CreateReviewedAdviceAsync(advice, CreateAdviceRequest(seed.Restock1Id, seed.Store1Id, seed.Restock1RowVersion), Manager1(seed), Warehouse(seed));
        var second = await CreateReviewedAdviceAsync(advice, CreateAdviceRequest(seed.Restock2Id, seed.Store2Id, seed.Restock2RowVersion), Manager2(seed), Warehouse(seed));
        var request = new CreatePurchaseOrderBatchRequest
        {
            SupplierId = seed.SupplierId,
            RequestKey = "E2E-189-" + Guid.NewGuid().ToString("N"),
            Lines =
            {
                new() { PurchaseAdviceLineId = first.Lines.Single().PurchaseAdviceLineId, IngredientSupplierId = seed.OfferId, PackageCount = 5, RowVersion = first.Lines.Single().RowVersion },
                new() { PurchaseAdviceLineId = second.Lines.Single().PurchaseAdviceLineId, IngredientSupplierId = seed.OfferId, PackageCount = 5, RowVersion = second.Lines.Single().RowVersion }
            }
        };
        var batch = await BatchService(db).CreateAsync(request, Warehouse(seed));
        Assert.True(batch.IsSuccess, batch.Message);
        Assert.Equal(2, batch.Data!.ChildPurchaseOrders.Count);
        Assert.Equal(2, batch.Data.ChildPurchaseOrders.Select(x => x.StoreId).Distinct().Count());
        Assert.Equal(2, batch.Data.Lines.Single().Allocations.Count);
        return new(batch.Data, first, second);
    }

    private static async Task<PurchaseAdviceDetailDto> CreateReviewedAdviceAsync(
        PurchaseAdviceService service, CreatePurchaseAdviceRequest request, AdminActorContext manager, AdminActorContext warehouse)
    {
        var created = await service.CreateAsync(request, warehouse);
        Assert.True(created.IsSuccess, created.Message);
        var submitted = await service.SubmitAsync(created.Data!.PurchaseAdviceId,
            new() { RowVersion = created.Data.RowVersion }, warehouse);
        Assert.True(submitted.IsSuccess, submitted.Message);
        var reviewed = await service.StartReviewAsync(submitted.Data!.PurchaseAdviceId,
            new() { RowVersion = submitted.Data.RowVersion }, warehouse);
        Assert.True(reviewed.IsSuccess, reviewed.Message);
        return reviewed.Data!;
    }

    private static CreatePurchaseAdviceRequest CreateAdviceRequest(int restockId, int storeId, string rowVersion) => new()
    {
        StoreId = storeId,
        RequestKey = Guid.NewGuid().ToString("N"),
        NeededByDate = DateTime.UtcNow.Date.AddDays(3),
        Priority = PurchaseAdvicePriorities.Normal,
        Lines = { new() { RestockRequestId = restockId, RequestedPurchaseBaseQuantity = 5m, RestockRowVersion = rowVersion } }
    };

    private static PurchaseAdviceService AdviceService(AppDbContext db) => new(db, Scope(db));
    private static PurchaseAdviceConsolidationService ConsolidationService(AppDbContext db)
    {
        var physical = new Mock<IPhysicalUnitConversionService>();
        physical.Setup(x => x.ConvertAsync(It.IsAny<decimal>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((decimal quantity, int _, int _) => ServiceResult<decimal>.Success(quantity));
        return new(db, Scope(db), physical.Object);
    }
    private static PurchaseOrderBatchService BatchService(AppDbContext db) => new(db, ConsolidationService(db), Scope(db));
    private static PurchaseOrderService NormalOrderService(AppDbContext db)
    {
        var physical = new PhysicalUnitConversionService(db, NullLogger<PhysicalUnitConversionService>.Instance);
        var unit = new UnitConversionService(db, NullLogger<UnitConversionService>.Instance, physical);
        return new PurchaseOrderService(
            db,
            unit,
            new RestockAllocationService(db, new NoPurchaseOrderAllocationProvider()),
            Scope(db),
            new PurchaseAdviceFulfillmentService(db));
    }
    private static PurchaseOrderBatchDocumentService DocumentService(AppDbContext db, MemoryStorage storage) =>
        new(db, new DeterministicRenderer(), storage, Scope(db));

    private static BranchReceiptService ReceiptService(AppDbContext db)
    {
        var physical = new PhysicalUnitConversionService(db, NullLogger<PhysicalUnitConversionService>.Instance);
        var unit = new UnitConversionService(db, NullLogger<UnitConversionService>.Instance, physical);
        var mode = new Mock<IInventoryWriterModeService>();
        var resolver = new Mock<IStoreInventoryWriteResolver>();
        var alerts = new Mock<IStockAlertService>();
        alerts.Setup(x => x.EvaluateStoreInventoryItemAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(ServiceResult<StockAlertEvaluationResultDto>.Success(new()));
        var scope = Scope(db);
        var purchaseOrders = new PurchaseOrderService(db, unit,
            new RestockAllocationService(db, new NoPurchaseOrderAllocationProvider()), scope);
        return new(db, unit, physical, mode.Object, resolver.Object,
            new RestockFulfillmentPostingService(db), alerts.Object, scope,
            NullLogger<BranchReceiptService>.Instance, purchaseOrders);
    }

    private static ScopeAuthorizationService Scope(AppDbContext db) => new(db);
    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options);

    private static async Task<Seed> SeedFoundationAsync(AppDbContext db)
    {
        var now = DateTime.UtcNow;
        var store1 = new Store { Name = "E2E Store 1", Address = "Quận 1", Phone = "0900189001", Active = true, CreatedAt = now };
        var store2 = new Store { Name = "E2E Store 2", Address = "Quận 2", Phone = "0900189002", Active = true, CreatedAt = now };
        var unit = new Unit { UnitCode = "kg" + Guid.NewGuid().ToString("N")[..6], Name = "kg", Active = true };
        var ingredient = new Ingredient { Code = "I189" + Guid.NewGuid().ToString("N")[..6], Name = "Cà phê E2E", Active = true, BaseUnit = unit };
        var supplier = new Supplier { Code = "SUP189" + Guid.NewGuid().ToString("N")[..5], Name = "NCC E2E", TaxCode = "0318918918", Active = true, CreatedAt = now, UpdatedAt = now };
        var accounts = Enumerable.Range(1, 4).Select(i => new Account { Email = $"issue189-{i}-{Guid.NewGuid():N}@test.local", PasswordHash = "x", Active = true, CreatedAt = now }).ToArray();
        db.AddRange(store1, store2, ingredient, supplier);
        db.AddRange(accounts);
        await db.SaveChangesAsync();
        var manager1 = Staff(accounts[0], store1, "Quản lý Store 1", now);
        var manager2 = Staff(accounts[1], store2, "Quản lý Store 2", now);
        var warehouse = Staff(accounts[2], store1, "Kế toán kho", now);
        var owner = Staff(accounts[3], store1, "Chủ doanh nghiệp", now);
        db.AddRange(manager1, manager2, warehouse, owner);
        await db.SaveChangesAsync();
        db.StaffScopes.AddRange(
            new StaffScope
            {
                StaffId = manager1.StaffId,
                ScopeTypeId = (int)ScopeLevel.Store,
                ScopeRefId = store1.StoreId
            },
            new StaffScope
            {
                StaffId = manager2.StaffId,
                ScopeTypeId = (int)ScopeLevel.Store,
                ScopeRefId = store2.StoreId
            },
            new StaffScope
            {
                StaffId = warehouse.StaffId,
                ScopeTypeId = (int)ScopeLevel.Store,
                ScopeRefId = store1.StoreId
            },
            new StaffScope
            {
                StaffId = warehouse.StaffId,
                ScopeTypeId = (int)ScopeLevel.Store,
                ScopeRefId = store2.StoreId
            },
            new StaffScope
            {
                StaffId = owner.StaffId,
                ScopeTypeId = (int)ScopeLevel.Store,
                ScopeRefId = store1.StoreId
            },
            new StaffScope
            {
                StaffId = owner.StaffId,
                ScopeTypeId = (int)ScopeLevel.Store,
                ScopeRefId = store2.StoreId
            });
        var offer = new IngredientSupplier { IngredientId = ingredient.IngredientId, SupplierId = supplier.SupplierId, UnitId = unit.UnitId, PackageQuantity = 1m, CurrentPrice = 100000m, MinimumOrderPackageCount = 1, LeadTimeDays = 1, Active = true, CreatedAt = now, UpdatedAt = now };
        db.AddRange(offer,
            new SupplierStore { SupplierId = supplier.SupplierId, StoreId = store1.StoreId, Active = true, CreatedAt = now, UpdatedAt = now },
            new SupplierStore { SupplierId = supplier.SupplierId, StoreId = store2.StoreId, Active = true, CreatedAt = now, UpdatedAt = now });
        await db.SaveChangesAsync();
        var restock1 = Restock(store1, ingredient, manager1, now);
        var restock2 = Restock(store2, ingredient, manager2, now);
        db.AddRange(restock1, restock2);
        await db.SaveChangesAsync();
        return new(store1.StoreId, store2.StoreId, ingredient.IngredientId, supplier.SupplierId, offer.IngredientSupplierId,
            restock1.RestockRequestId, restock2.RestockRequestId, Convert.ToBase64String(restock1.RowVersion), Convert.ToBase64String(restock2.RowVersion),
            manager1.StaffId, manager2.StaffId, warehouse.StaffId, owner.StaffId);
    }

    private static Staff Staff(Account account, Store store, string name, DateTime now) => new()
    {
        AccountId = account.AccountId, StoreId = store.StoreId, FullName = name, Active = true, CreatedAt = now
    };
    private static RestockRequest Restock(Store store, Ingredient ingredient, Staff staff, DateTime now) => new()
    {
        StoreId = store.StoreId, IngredientId = ingredient.IngredientId, RequestedQuantity = 5m,
        Status = RestockRequestStatuses.Processing, Priority = RestockRequestPriorities.Normal,
        CreatedByStaffId = staff.StaffId, CreatedAt = now, UpdatedAt = now
    };

    private static AdminActorContext Manager1(Seed x) => Actor(x.Manager1Id, x.Store1Id, RoleConstants.StoreManager);
    private static AdminActorContext Manager2(Seed x) => Actor(x.Manager2Id, x.Store2Id, RoleConstants.StoreManager);
    private static AdminActorContext Warehouse(Seed x) => Actor(x.WarehouseId, x.Store1Id, RoleConstants.AccountantWarehouse);
    private static AdminActorContext Owner(Seed x) => Actor(x.OwnerId, x.Store1Id, RoleConstants.BusinessOwner);
    private static AdminActorContext Actor(int staffId, int storeId, string role) => new() { StaffId = staffId, StoreId = storeId, RoleNames = new[] { role } };
    private static MarkPurchaseOrderBatchDocumentSentRequest SendRequest(string rowVersion, string key) => new()
    {
        Channel = PurchaseOrderBatchDocumentChannels.ZaloManual, RowVersion = rowVersion, IdempotencyKey = key, Note = "Đã gửi thủ công qua Zalo"
    };

    private sealed record Seed(int Store1Id, int Store2Id, int IngredientId, int SupplierId, int OfferId,
        int Restock1Id, int Restock2Id, string Restock1RowVersion, string Restock2RowVersion,
        int Manager1Id, int Manager2Id, int WarehouseId, int OwnerId);
    private sealed record Prepared(PurchaseOrderBatchDetailDto Batch, PurchaseAdviceDetailDto FirstAdvice, PurchaseAdviceDetailDto SecondAdvice);

    private sealed class DeterministicRenderer : IPurchaseOrderBatchPdfRenderer
    {
        public byte[] Render(PurchaseOrderBatchDocumentSnapshot snapshot, int revisionNumber, DateTime generatedAtUtc, string contentHash) =>
            System.Text.Encoding.UTF8.GetBytes($"%PDF-R{revisionNumber}-{contentHash}-Tiếng Việt-{snapshot.BatchNumber}");
    }
    private sealed class MemoryStorage : IPurchaseOrderBatchDocumentStorage
    {
        public ConcurrentDictionary<string, byte[]> Files { get; } = new();
        public Task SaveAsync(string storageReference, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
        { Files.TryAdd(storageReference, content.ToArray()); return Task.CompletedTask; }
        public Task<byte[]?> ReadAsync(string storageReference, CancellationToken cancellationToken = default) =>
            Task.FromResult(Files.TryGetValue(storageReference, out var content) ? content : null);
        public Task DeleteAsync(string storageReference, CancellationToken cancellationToken = default)
        { Files.TryRemove(storageReference, out _); return Task.CompletedTask; }
    }
}
