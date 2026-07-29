using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Inventories;
using CafeChain.Data;
using CafeChain.Models.Customers;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Procurement;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Inventories.Suppliers;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CafeChain.Tests;

public sealed class PurchaseOrderBatchIssue186Tests : IntegrationTestBase
{
    [Fact]
    public async Task Batch_CreateFromMultipleStoreAdvices()
    {
        using var db = CreateDbContext();
        var seed = await SeedAsync(db);
        var result = await BatchService(db).CreateAsync(Request(seed), Warehouse(seed));
        Assert.True(result.IsSuccess, result.Message);
        Assert.StartsWith("POB-", result.Data!.BatchNumber);
        Assert.Equal(PurchaseOrderBatchStatuses.PendingApproval, result.Data.Status);
    }

    [Fact]
    public async Task Batch_Create_SnapshotsTrackedAdvicesBeforeRecomputingStatuses()
    {
        using var db = CreateDbContext();
        var seed = await SeedAsync(db);
        var fulfillment = new Mock<IPurchaseAdviceFulfillmentService>();
        var attachedDuringRecompute = false;
        fulfillment
            .Setup(x => x.RecomputeHeaderStatusAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
            .Callback(() =>
            {
                if (attachedDuringRecompute) return;

                db.Attach(new PurchaseAdvice
                {
                    PurchaseAdviceId = int.MaxValue,
                    AdviceNumber = "PA-TRACKER-SNAPSHOT",
                    RequestKey = "TRACKER-SNAPSHOT",
                    StoreId = seed.Store1Id,
                    RequestedByStaffId = seed.ManagerId,
                    Status = PurchaseAdviceStatuses.Submitted,
                    NeededByDate = DateTime.UtcNow.Date.AddDays(1),
                    Priority = PurchaseAdvicePriorities.Normal,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                });
                attachedDuringRecompute = true;
            })
            .Returns(Task.CompletedTask);

        var result = await BatchService(db, fulfillment.Object)
            .CreateAsync(Request(seed), Warehouse(seed));

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(attachedDuringRecompute);
        fulfillment.Verify(
            x => x.RecomputeHeaderStatusAsync(It.IsAny<int>(), seed.WarehouseId, It.IsAny<string>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task Batch_CreatesOneChildPoPerStore()
    {
        using var db = CreateDbContext();
        var seed = await SeedAsync(db);
        var result = await BatchService(db).CreateAsync(Request(seed), Warehouse(seed));
        Assert.Equal(2, result.Data!.ChildPurchaseOrders.Count);
        Assert.Equal(2, result.Data.ChildPurchaseOrders.Select(x => x.StoreId).Distinct().Count());
        Assert.All(result.Data.ChildPurchaseOrders, x => Assert.Equal(PurchaseOrderStatuses.Draft, x.Status));
    }

    [Fact]
    public async Task Batch_CreatesCorrectBatchLinesAndOfferSnapshots()
    {
        using var db = CreateDbContext();
        var seed = await SeedAsync(db);
        var result = await BatchService(db).CreateAsync(Request(seed), Warehouse(seed));
        var line = Assert.Single(result.Data!.Lines);
        Assert.Equal(10m, line.TotalPackageCount);
        Assert.Equal(10m, line.TotalBaseQuantity);
        Assert.Equal(12000m, line.PackagePriceSnapshot);
        Assert.Equal(120000m, line.LineTotal);
    }

    [Fact]
    public async Task Batch_CreatesTraceableAllocations()
    {
        using var db = CreateDbContext();
        var seed = await SeedAsync(db);
        var result = await BatchService(db).CreateAsync(Request(seed), Warehouse(seed));
        var allocations = result.Data!.Lines.Single().Allocations;
        Assert.Equal(2, allocations.Count);
        Assert.All(allocations, x =>
        {
            Assert.True(x.PurchaseAdviceLineId > 0);
            Assert.True(x.PurchaseOrderId > 0);
            Assert.True(x.PurchaseOrderLineId > 0);
            Assert.Equal(5m, x.AllocatedBaseQuantity);
        });
    }

    [Fact]
    public async Task TwoStorePaConsolidatesBySupplierSku_AndRoundsEachStoreAllocation()
    {
        using var db = CreateDbContext();
        var seed = await SeedAsync(
            db,
            firstRequestedBaseQuantity: 2300m,
            secondRequestedBaseQuantity: 1400m,
            packageBaseQuantity: 1000m,
            packagePrice: 160000m,
            baseUnitName: "g");
        var request = new CreatePurchaseOrderBatchRequest
        {
            SupplierId = seed.SupplierId,
            RequestKey = "BATCH-219-" + Guid.NewGuid().ToString("N"),
            Lines =
            {
                new()
                {
                    PurchaseAdviceLineId = seed.Lines[0].LineId,
                    IngredientSupplierId = seed.OfferId,
                    PackageCount = 3,
                    RowVersion = seed.Lines[0].RowVersion
                },
                new()
                {
                    PurchaseAdviceLineId = seed.Lines[1].LineId,
                    IngredientSupplierId = seed.OfferId,
                    PackageCount = 2,
                    RowVersion = seed.Lines[1].RowVersion
                }
            }
        };

        var result = await BatchService(db).CreateAsync(request, Warehouse(seed));

        Assert.True(result.IsSuccess, result.Message);
        Assert.Single(await db.PurchaseOrderBatches.AsNoTracking().ToListAsync());
        Assert.Equal(2, await db.PurchaseOrders.AsNoTracking().CountAsync());
        Assert.Equal(2, result.Data!.ChildPurchaseOrders.Count);
        Assert.Equal(2, result.Data.ChildPurchaseOrders.Select(x => x.StoreId).Distinct().Count());

        var masterLine = Assert.Single(result.Data.Lines);
        Assert.Equal(5m, masterLine.TotalPackageCount);
        Assert.Equal(5000m, masterLine.TotalBaseQuantity);
        Assert.Equal(3700m, masterLine.DemandCoveredBaseQuantity);
        Assert.Equal(1300m, masterLine.RoundingSurplusBaseQuantity);
        Assert.Equal(160000m, masterLine.PackagePriceSnapshot);
        Assert.Equal(800000m, masterLine.LineTotal);
        Assert.Equal(800000m, result.Data.TotalAmount);

        var firstAllocation = masterLine.Allocations.Single(x => x.StoreId == seed.Store1Id);
        Assert.Equal(3m, firstAllocation.AllocatedPackageQuantity);
        Assert.Equal(3000m, firstAllocation.AllocatedBaseQuantity);
        Assert.Equal(2300m, firstAllocation.DemandCoveredBaseQuantity);
        Assert.Equal(700m, firstAllocation.RoundingSurplusBaseQuantity);

        var secondAllocation = masterLine.Allocations.Single(x => x.StoreId == seed.Store2Id);
        Assert.Equal(2m, secondAllocation.AllocatedPackageQuantity);
        Assert.Equal(2000m, secondAllocation.AllocatedBaseQuantity);
        Assert.Equal(1400m, secondAllocation.DemandCoveredBaseQuantity);
        Assert.Equal(600m, secondAllocation.RoundingSurplusBaseQuantity);

        var firstChild = result.Data.ChildPurchaseOrders.Single(x => x.StoreId == seed.Store1Id);
        Assert.Equal(3000m, firstChild.OrderedBaseQuantity);
        Assert.Equal(480000m, firstChild.TotalAmount);
        var secondChild = result.Data.ChildPurchaseOrders.Single(x => x.StoreId == seed.Store2Id);
        Assert.Equal(2000m, secondChild.OrderedBaseQuantity);
        Assert.Equal(320000m, secondChild.TotalAmount);

        var adviceLines = await db.PurchaseAdviceLines.AsNoTracking()
            .Include(x => x.PurchaseAdvice)
            .OrderBy(x => x.PurchaseAdvice.StoreId)
            .ToArrayAsync();
        Assert.Equal(2300m, adviceLines.Single(x => x.PurchaseAdvice.StoreId == seed.Store1Id).AllocatedToPoBaseQuantity);
        Assert.Equal(1400m, adviceLines.Single(x => x.PurchaseAdvice.StoreId == seed.Store2Id).AllocatedToPoBaseQuantity);
    }

    [Fact]
    public async Task ProcurementContract_875KgRoundsToNineKgWithoutLosingDemandAudit()
    {
        using var db = CreateDbContext();
        var seed = await SeedAsync(
            db,
            firstRequestedBaseQuantity: 8.75m,
            secondRequestedBaseQuantity: 1m,
            packageBaseQuantity: 1m,
            packagePrice: 100000m,
            baseUnitName: "kg");
        await EnableProcurementContractAsync(db, seed, first: 8.75m, second: 1m, unitName: "kg");

        var request = Request(seed);
        request.Lines[0].PackageCount = 9;
        request.Lines[1].PackageCount = 1;
        var result = await BatchService(db).CreateAsync(request, Warehouse(seed));

        Assert.True(result.IsSuccess, result.Message);
        var firstChild = result.Data!.ChildPurchaseOrders.Single(x => x.StoreId == seed.Store1Id);
        Assert.Equal(8.75m, (await db.PurchaseAdviceLines
            .Include(x => x.PurchaseAdvice)
            .SingleAsync(x => x.PurchaseAdvice.StoreId == seed.Store1Id))
            .RequestedProcurementQuantity);
        Assert.Equal(9m, firstChild.OrderedProcurementQuantity);
        Assert.Equal(0.25m, firstChild.OrderedProcurementQuantity - 8.75m);
        Assert.Equal("kg", firstChild.ProcurementUnitName);
    }

    [Fact]
    public async Task Packaged10Kg_With1KgPackage_Creates10Packages()
    {
        using var db = CreateDbContext();
        var seed = await SeedAsync(
            db,
            firstRequestedBaseQuantity: 10m,
            secondRequestedBaseQuantity: 5m,
            packageBaseQuantity: 1m,
            baseUnitName: "kg");
        await EnableProcurementContractAsync(db, seed, first: 10m, second: 5m, unitName: "kg");
        var first = seed.Lines[0];
        var request = new CreatePurchaseOrderBatchRequest
        {
            SupplierId = seed.SupplierId,
            RequestKey = "BATCH-254-" + Guid.NewGuid().ToString("N"),
            Lines =
            {
                new()
                {
                    PurchaseAdviceLineId = first.LineId,
                    IngredientSupplierId = seed.OfferId,
                    PurchaseMode = PurchaseMode.Packaged,
                    PackageCount = 10,
                    RowVersion = first.RowVersion
                }
            }
        };

        var result = await BatchService(db).CreateAsync(request, Warehouse(seed));

        Assert.True(result.IsSuccess, result.Message);
        var batchLine = Assert.Single(result.Data!.Lines);
        Assert.Equal(10m, batchLine.TotalPackageCount);
        Assert.Equal(10m, batchLine.TotalProcurementQuantity);
        Assert.Equal(10m, batchLine.DemandCoveredProcurementQuantity);
        Assert.Equal(0m, batchLine.RoundingSurplusProcurementQuantity);
        var child = Assert.Single(result.Data.ChildPurchaseOrders);
        Assert.Equal(10m, child.OrderedProcurementQuantity);
    }

    [Fact]
    public async Task PackagedPayload_WithBothFields_IsRejected()
    {
        using var db = CreateDbContext();
        var seed = await SeedAsync(db);
        var request = Request(seed);
        request.Lines[0].OrderedProcurementQuantity = 5m;

        var result = await BatchService(db).CreateAsync(request, Warehouse(seed));

        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseAdviceErrorCodes.ConsolidationInvalid, result.ErrorCode);
        Assert.Contains($"#{request.Lines[0].PurchaseAdviceLineId}", result.Message);
        Assert.Contains("đồng thời số gói và số lượng mua rời", result.Message);
        Assert.Empty(await db.PurchaseOrderBatches.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task PackagedPayload_WithoutPackageCount_IsRejected()
    {
        using var db = CreateDbContext();
        var seed = await SeedAsync(db);
        var request = Request(seed);
        request.Lines[0].PackageCount = null;

        var result = await BatchService(db).CreateAsync(request, Warehouse(seed));

        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseAdviceErrorCodes.ConsolidationInvalid, result.ErrorCode);
        Assert.Contains("số gói nguyên lớn hơn 0", result.Message);
        Assert.Empty(await db.PurchaseOrderBatches.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task LoosePayload_WithPackageCount_IsRejected()
    {
        using var db = CreateDbContext();
        var seed = await SeedAsync(db);
        var request = Request(seed);
        request.Lines[0].PurchaseMode = PurchaseMode.Loose;
        request.Lines[0].OrderedProcurementQuantity = 5m;

        var result = await BatchService(db).CreateAsync(request, Warehouse(seed));

        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseAdviceErrorCodes.ConsolidationInvalid, result.ErrorCode);
        Assert.Contains("mua rời nên không được gửi số gói", result.Message);
        Assert.Empty(await db.PurchaseOrderBatches.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task ProcurementContract_TwoStoresKeepSeparateProcurementAllocations()
    {
        using var db = CreateDbContext();
        var seed = await SeedAsync(
            db,
            firstRequestedBaseQuantity: 2300m,
            secondRequestedBaseQuantity: 1400m,
            packageBaseQuantity: 1000m,
            packagePrice: 160000m,
            baseUnitName: "g");
        await EnableProcurementContractAsync(db, seed, first: 2.30m, second: 1.40m, unitName: "kg");
        var request = Request(seed);
        request.Lines[0].PackageCount = 3;
        request.Lines[1].PackageCount = 2;

        var result = await BatchService(db).CreateAsync(request, Warehouse(seed));

        Assert.True(result.IsSuccess, result.Message);
        var masterLine = Assert.Single(result.Data!.Lines);
        Assert.Equal(5m, masterLine.TotalProcurementQuantity);
        Assert.Equal(3.70m, masterLine.DemandCoveredProcurementQuantity);
        Assert.Equal(1.30m, masterLine.RoundingSurplusProcurementQuantity);
        Assert.Equal("kg", masterLine.ProcurementUnitName);
        Assert.Equal(3m, result.Data.ChildPurchaseOrders.Single(x => x.StoreId == seed.Store1Id).OrderedProcurementQuantity);
        Assert.Equal(2m, result.Data.ChildPurchaseOrders.Single(x => x.StoreId == seed.Store2Id).OrderedProcurementQuantity);
    }

    [Fact]
    public async Task Batch_RejectsOverAllocation()
    {
        using var db = CreateDbContext();
        var seed = await SeedAsync(db);
        var request = Request(seed);
        request.Lines[0].PackageCount = 6;
        var result = await BatchService(db).CreateAsync(request, Warehouse(seed));
        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseAdviceErrorCodes.PackageCountMismatch, result.ErrorCode);
        Assert.Empty(db.PurchaseOrderBatches);
    }

    [Fact]
    public async Task Batch_DoubleCreateIdempotent()
    {
        using var db = CreateDbContext();
        var seed = await SeedAsync(db);
        var service = BatchService(db);
        var request = Request(seed);
        var first = await service.CreateAsync(request, Warehouse(seed));
        var second = await service.CreateAsync(request, Warehouse(seed));
        Assert.True(first.IsSuccess && second.IsSuccess);
        Assert.Equal(first.Data!.PurchaseOrderBatchId, second.Data!.PurchaseOrderBatchId);
        Assert.Single(db.PurchaseOrderBatches);
        Assert.Equal(2, db.PurchaseOrders.Count());
    }

    [Fact]
    public async Task CancelDoesNotCreateInventory_AndReleasesPaAllocations()
    {
        using var db = CreateDbContext();
        var seed = await SeedAsync(db);
        var service = BatchService(db);
        var created = (await service.CreateAsync(Request(seed), Warehouse(seed))).Data!;

        var cancelled = await service.CancelAsync(
            created.PurchaseOrderBatchId,
            new PurchaseOrderBatchTransitionRequest
            {
                RowVersion = created.RowVersion,
                Reason = "Nhà cung cấp không thể giao hàng"
            },
            Owner(seed));

        Assert.True(cancelled.IsSuccess, cancelled.Message);
        Assert.Equal(PurchaseOrderBatchStatuses.Cancelled, cancelled.Data!.Status);
        Assert.All(
            await db.PurchaseOrders.AsNoTracking().ToListAsync(),
            child => Assert.Equal(PurchaseOrderStatuses.Cancelled, child.Status));
        Assert.All(
            await db.PurchaseAdviceLines.AsNoTracking().ToListAsync(),
            line =>
            {
                Assert.Equal(0m, line.AllocatedToPoBaseQuantity);
                Assert.True(line.IsActiveReservation);
            });
        Assert.Empty(await db.PurchaseOrderReceiptPostings.AsNoTracking().ToListAsync());
        Assert.Empty(await db.InventoryTransactions.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Batch_AtomicRollbackOnChildPoFailure()
    {
        using var db = CreateDbContext();
        var seed = await SeedAsync(db);
        var request = Request(seed);
        request.Lines[1].IngredientSupplierId = int.MaxValue;
        var result = await BatchService(db).CreateAsync(request, Warehouse(seed));
        Assert.False(result.IsSuccess);
        Assert.Empty(db.PurchaseOrderBatches);
        Assert.Empty(db.PurchaseOrders);
        Assert.All(await db.PurchaseAdviceLines.ToListAsync(), x => Assert.Equal(0m, x.AllocatedToPoBaseQuantity));
    }

    [Fact]
    public async Task Batch_ApprovalUpdatesChildPoStatesWithoutSecondApproval()
    {
        using var db = CreateDbContext();
        var seed = await SeedAsync(db);
        var service = BatchService(db);
        var created = (await service.CreateAsync(Request(seed), Warehouse(seed))).Data!;
        var approved = await service.ApproveAsync(created.PurchaseOrderBatchId,
            new PurchaseOrderBatchTransitionRequest { RowVersion = created.RowVersion }, Owner(seed));
        Assert.True(approved.IsSuccess, approved.Message);
        Assert.Equal(PurchaseOrderBatchStatuses.Approved, approved.Data!.Status);
        Assert.All(approved.Data.ChildPurchaseOrders, x => Assert.Equal(PurchaseOrderStatuses.Approved, x.Status));
    }

    [Fact]
    public async Task Batch_StatusAggregatesFromChildPos()
    {
        using var db = CreateDbContext();
        var seed = await SeedAsync(db);
        var service = BatchService(db);
        var created = (await service.CreateAsync(Request(seed), Warehouse(seed))).Data!;
        var child = await db.PurchaseOrders.Include(x => x.Lines).FirstAsync(x => x.PurchaseOrderBatchId == created.PurchaseOrderBatchId);
        child.Status = PurchaseOrderStatuses.PartiallyReceived;
        db.PurchaseOrderReceiptPostings.Add(new PurchaseOrderReceiptPosting
        {
            PurchaseOrderLineId = child.Lines.First().PurchaseOrderLineId,
            BranchReceiptLineId = 186001,
            AcceptedBaseQuantity = 1m,
            CreatedByStaffId = seed.WarehouseId,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        await service.RefreshStatusAsync(created.PurchaseOrderBatchId);
        Assert.Equal(PurchaseOrderBatchStatuses.PartiallyReceived,
            (await db.PurchaseOrderBatches.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task ChildPo_UsesExistingReceiveGoodsWorkflowAndStoreScope()
    {
        using var db = CreateDbContext();
        var seed = await SeedAsync(db);
        var service = BatchService(db);
        var created = (await service.CreateAsync(Request(seed), Warehouse(seed))).Data!;
        var approved = await service.ApproveAsync(created.PurchaseOrderBatchId,
            new PurchaseOrderBatchTransitionRequest { RowVersion = created.RowVersion }, Owner(seed));
        Assert.True(approved.IsSuccess);
        Assert.All(approved.Data!.ChildPurchaseOrders, po =>
        {
            Assert.Contains(po.StoreId, new[] { seed.Store1Id, seed.Store2Id });
            Assert.True(po.RemainingBaseQuantity > 0);
        });
        Assert.All(await db.PurchaseOrderLines.AsNoTracking().ToListAsync(), line => Assert.True(line.RestockRequestId.HasValue));
    }

    [Fact]
    public async Task StoreManager_CannotCreateBatch()
    {
        using var db = CreateDbContext();
        var seed = await SeedAsync(db);
        var result = await BatchService(db).CreateAsync(Request(seed), new AdminActorContext
        {
            StaffId = seed.ManagerId,
            StoreId = seed.Store1Id,
            RoleNames = new[] { RoleConstants.StoreManager }
        });
        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseOrderBatchErrorCodes.Forbidden, result.ErrorCode);
    }

    private static PurchaseOrderBatchService BatchService(
        AppDbContext db,
        IPurchaseAdviceFulfillmentService? fulfillment = null)
    {
        var scope = new Mock<IScopeAuthorizationService>();
        scope.Setup(x => x.CanAccessStoreAsync(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(true);
        var physical = new Mock<IPhysicalUnitConversionService>();
        physical.Setup(x => x.ConvertAsync(It.IsAny<decimal>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((decimal quantity, int _, int _) => ServiceResult<decimal>.Success(quantity));
        var consolidation = new PurchaseAdviceConsolidationService(db, scope.Object, physical.Object);
        return new PurchaseOrderBatchService(db, consolidation, scope.Object, fulfillment);
    }

    private static CreatePurchaseOrderBatchRequest Request(Seed seed) => new()
    {
        SupplierId = seed.SupplierId,
        RequestKey = "BATCH-186-" + Guid.NewGuid().ToString("N"),
        Lines = seed.Lines.Select(x => new CreatePurchaseOrderBatchLineRequest
        {
            PurchaseAdviceLineId = x.LineId,
            IngredientSupplierId = seed.OfferId,
            PackageCount = 5,
            RowVersion = x.RowVersion
        }).ToList()
    };

    private static AdminActorContext Warehouse(Seed seed) => new() { StaffId = seed.WarehouseId, RoleNames = new[] { RoleConstants.AccountantWarehouse } };
    private static AdminActorContext Owner(Seed seed) => new() { StaffId = seed.OwnerId, RoleNames = new[] { RoleConstants.BusinessOwner } };

    private static async Task<Seed> SeedAsync(
        AppDbContext db,
        decimal firstRequestedBaseQuantity = 5m,
        decimal secondRequestedBaseQuantity = 5m,
        decimal packageBaseQuantity = 1m,
        decimal packagePrice = 12000m,
        string baseUnitName = "kg")
    {
        var now = DateTime.UtcNow;
        var store1 = new Store { Name = "Store 186 A", Address = "A", Phone = Guid.NewGuid().ToString("N")[..10], Active = true, CreatedAt = now };
        var store2 = new Store { Name = "Store 186 B", Address = "B", Phone = Guid.NewGuid().ToString("N")[..10], Active = true, CreatedAt = now };
        var unit = new Unit { UnitCode = "u186" + Guid.NewGuid().ToString("N")[..4], Name = baseUnitName, Active = true };
        var ingredient = new Ingredient { Code = "ING186" + Guid.NewGuid().ToString("N")[..4], Name = "Coffee 186", Active = true, BaseUnit = unit };
        var supplier = new Supplier { Code = "SUP186" + Guid.NewGuid().ToString("N")[..4], Name = "Supplier 186", Active = true, CreatedAt = now, UpdatedAt = now };
        var warehouseAccount = new Account { Email = Guid.NewGuid() + "@test.local", PasswordHash = "x", Active = true, CreatedAt = now };
        var ownerAccount = new Account { Email = Guid.NewGuid() + "@test.local", PasswordHash = "x", Active = true, CreatedAt = now };
        var managerAccount = new Account { Email = Guid.NewGuid() + "@test.local", PasswordHash = "x", Active = true, CreatedAt = now };
        db.AddRange(store1, store2, ingredient, supplier, warehouseAccount, ownerAccount, managerAccount);
        await db.SaveChangesAsync();
        var warehouse = new Staff { AccountId = warehouseAccount.AccountId, FullName = "Warehouse 186", Active = true, CreatedAt = now};
        var owner = new Staff { AccountId = ownerAccount.AccountId, FullName = "Owner 186", Active = true, CreatedAt = now};
        var manager = new Staff { AccountId = managerAccount.AccountId, StoreId = store1.StoreId, FullName = "Manager 186", Active = true, CreatedAt = now};
        var offer = new IngredientSupplier { IngredientId = ingredient.IngredientId, SupplierId = supplier.SupplierId, UnitId = unit.UnitId, PackageQuantity = packageBaseQuantity, CurrentPrice = packagePrice, MinimumOrderPackageCount = 1, LeadTimeDays = 2, Active = true, CreatedAt = now, UpdatedAt = now };
        db.AddRange(warehouse, owner, manager, offer,
            new SupplierStore { SupplierId = supplier.SupplierId, StoreId = store1.StoreId, Active = true, CreatedAt = now, UpdatedAt = now },
            new SupplierStore { SupplierId = supplier.SupplierId, StoreId = store2.StoreId, Active = true, CreatedAt = now, UpdatedAt = now });
        await db.SaveChangesAsync();

        var lineSeeds = new List<LineSeed>();
        var storesWithDemand = new[]
        {
            (Store: store1, RequestedBaseQuantity: firstRequestedBaseQuantity),
            (Store: store2, RequestedBaseQuantity: secondRequestedBaseQuantity)
        };
        foreach (var item in storesWithDemand)
        {
            var restock = new RestockRequest { StoreId = item.Store.StoreId, IngredientId = ingredient.IngredientId, RequestedQuantity = item.RequestedBaseQuantity, Status = RestockRequestStatuses.Processing, Priority = RestockRequestPriorities.Normal, CreatedByStaffId = manager.StaffId, CreatedAt = now, UpdatedAt = now };
            db.Add(restock); await db.SaveChangesAsync();
            var advice = new PurchaseAdvice
            {
                AdviceNumber = "PA-186-" + Guid.NewGuid().ToString("N")[..6], RequestKey = Guid.NewGuid().ToString("N"), StoreId = item.Store.StoreId,
                RequestedByStaffId = manager.StaffId, Status = PurchaseAdviceStatuses.Submitted, NeededByDate = now.Date.AddDays(4), Priority = PurchaseAdvicePriorities.Normal,
                SubmittedAtUtc = now, CreatedAtUtc = now, UpdatedAtUtc = now,
                Lines = new List<PurchaseAdviceLine> { new() { RestockRequestId = restock.RestockRequestId, IngredientId = ingredient.IngredientId, RequestedPurchaseBaseQuantity = item.RequestedBaseQuantity, BaseUnitId = unit.UnitId, NeededByDate = now.Date.AddDays(4), IsActiveReservation = true } }
            };
            db.Add(advice); await db.SaveChangesAsync();
            var line = advice.Lines.Single();
            lineSeeds.Add(new LineSeed(line.PurchaseAdviceLineId, Convert.ToBase64String(line.RowVersion)));
        }
        return new Seed(store1.StoreId, store2.StoreId, warehouse.StaffId, owner.StaffId, manager.StaffId, supplier.SupplierId, offer.IngredientSupplierId, lineSeeds);
    }

    private static async Task EnableProcurementContractAsync(
        AppDbContext db,
        Seed seed,
        decimal first,
        decimal second,
        string unitName)
    {
        var baseUnitId = await db.PurchaseAdviceLines
            .AsNoTracking()
            .Select(x => x.BaseUnitId)
            .FirstAsync();
        var procurementUnit = new Unit
        {
            UnitCode = "proc186" + Guid.NewGuid().ToString("N")[..4],
            Name = unitName,
            Active = true
        };
        db.Units.Add(procurementUnit);
        await db.SaveChangesAsync();

        var lines = await db.PurchaseAdviceLines
            .Include(x => x.PurchaseAdvice)
            .OrderBy(x => x.PurchaseAdvice.StoreId)
            .ToListAsync();
        var restocks = await db.RestockRequests
            .OrderBy(x => x.StoreId)
            .ToListAsync();
        var quantities = new[] { first, second };
        for (var i = 0; i < lines.Count; i++)
        {
            lines[i].RequestedProcurementQuantity = quantities[i];
            lines[i].ProcurementUnitId = procurementUnit.UnitId;
            restocks[i].RequestedProcurementQuantity = quantities[i];
            restocks[i].ProcurementUnitId = procurementUnit.UnitId;
        }
        await db.SaveChangesAsync();
    }

    private sealed record LineSeed(int LineId, string RowVersion);
    private sealed record Seed(int Store1Id, int Store2Id, int WarehouseId, int OwnerId, int ManagerId, int SupplierId, int OfferId, List<LineSeed> Lines);
}
