using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Services.Inventories;
using CafeChain.Data;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Procurement;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Inventories.Suppliers;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CafeChain.Tests;

public sealed class PurchaseAdviceFulfillmentIssue193Tests : IntegrationTestBase
{
    private const int StoreId = 19301;
    private const int SupplierId = 19302;
    private const int UnitId = 19303;
    private const int IngredientId = 19304;

    [Fact]
    public async Task AcceptedBackPostCreatesLedgerUpdatesCachedQuantityAndIsIdempotent()
    {
        using var context = CreateDbContext();
        await SeedScenarioAsync(context, allocationCount: 1);
        var service = new PurchaseAdviceFulfillmentService(context);

        var first = await service.BackPostAcceptedAsync(20, 40, 4m, 99);
        var replay = await service.BackPostAcceptedAsync(20, 40, 4m, 99);

        Assert.True(first.IsSuccess, first.Message);
        Assert.True(replay.IsSuccess, replay.Message);
        Assert.Equal(1, await context.PurchaseAdviceFulfillmentPostings.CountAsync());
        var line = await context.PurchaseAdviceLines.SingleAsync();
        Assert.Equal(4m, line.AcceptedBaseQuantity);
        Assert.Equal(PurchaseAdviceStatuses.PartiallyFulfilled, (await context.PurchaseAdvices.SingleAsync()).Status);
    }

    [Fact]
    public async Task PurchaseOrderReceiptPostingPathBackPostsAcceptedInSameUnitOfWork()
    {
        using var context = CreateDbContext();
        await SeedScenarioAsync(context, allocationCount: 1);
        var fulfillment = new PurchaseAdviceFulfillmentService(context);
        var service = new PurchaseOrderService(
            context,
            Mock.Of<IUnitConversionService>(),
            Mock.Of<IRestockAllocationService>(),
            purchaseAdviceFulfillment: fulfillment);
        var receiptLine = await context.BranchReceiptLines
            .Include(x => x.BranchReceipt)
            .SingleAsync(x => x.BranchReceiptLineId == 40);

        var result = await service.RegisterReceiptPostingAsync(receiptLine.BranchReceipt, receiptLine, 99);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Single(await context.PurchaseOrderReceiptPostings.ToListAsync());
        Assert.Single(await context.PurchaseAdviceFulfillmentPostings.ToListAsync());
        Assert.Equal(10m, (await context.PurchaseAdviceLines.SingleAsync()).AcceptedBaseQuantity);
    }

    [Fact]
    public async Task AcceptedBackPostAggregatesAcrossMultiplePurchaseOrderAllocations()
    {
        using var context = CreateDbContext();
        await SeedScenarioAsync(context, allocationCount: 2);
        var service = new PurchaseAdviceFulfillmentService(context);

        var first = await service.BackPostAcceptedAsync(20, 40, 6m, 99);
        var second = await service.BackPostAcceptedAsync(21, 41, 4m, 99);

        Assert.True(first.IsSuccess, first.Message);
        Assert.True(second.IsSuccess, second.Message);
        Assert.Equal(10m, (await context.PurchaseAdviceLines.SingleAsync()).AcceptedBaseQuantity);
        Assert.Equal(PurchaseAdviceStatuses.Completed, (await context.PurchaseAdvices.SingleAsync()).Status);
    }

    [Fact]
    public async Task AcceptedBackPostRejectsReceiptLineFromDifferentPurchaseOrderLine()
    {
        using var context = CreateDbContext();
        await SeedScenarioAsync(context, allocationCount: 2);

        var result = await new PurchaseAdviceFulfillmentService(context)
            .BackPostAcceptedAsync(20, 41, 4m, 99);

        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseAdviceErrorCodes.BackPostTraceMissing, result.ErrorCode);
        Assert.Empty(context.PurchaseAdviceFulfillmentPostings);
    }

    [Fact]
    public async Task ClosedBackPostUsesRequestKeyAndRejectsDifferentPayload()
    {
        using var context = CreateDbContext();
        await SeedScenarioAsync(context, allocationCount: 1);
        var service = new PurchaseAdviceFulfillmentService(context);

        var first = await service.BackPostClosedAsync(20, 10m, "close-1", "hash-a", 99);
        var replay = await service.BackPostClosedAsync(20, 10m, "close-1", "hash-a", 99);
        var conflict = await service.BackPostClosedAsync(20, 9m, "close-1", "hash-b", 99);

        Assert.True(first.IsSuccess, first.Message);
        Assert.True(replay.IsSuccess, replay.Message);
        Assert.False(conflict.IsSuccess);
        Assert.Equal(PurchaseAdviceErrorCodes.BackPostConflict, conflict.ErrorCode);
        Assert.Equal(1, await context.PurchaseAdviceFulfillmentPostings.CountAsync());
        Assert.Equal(10m, (await context.PurchaseAdviceLines.SingleAsync()).ClosedBaseQuantity);
    }

    [Fact]
    public async Task ManualPurchaseOrderWithoutAdviceAllocationDoesNotRequireBackPost()
    {
        using var context = CreateDbContext();
        await SeedScenarioAsync(context, allocationCount: 1);
        context.PurchaseOrderLineAllocations.RemoveRange(context.PurchaseOrderLineAllocations);
        await context.SaveChangesAsync();

        var result = await new PurchaseAdviceFulfillmentService(context)
            .BackPostAcceptedAsync(20, 40, 10m, 99);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Empty(context.PurchaseAdviceFulfillmentPostings);
    }

    [Fact]
    public async Task BatchedPurchaseOrderMissingAdviceAllocationFailsTraceValidation()
    {
        using var context = CreateDbContext();
        await SeedScenarioAsync(context, allocationCount: 1);
        context.PurchaseOrderLineAllocations.RemoveRange(context.PurchaseOrderLineAllocations);
        (await context.PurchaseOrders.SingleAsync()).PurchaseOrderBatchId = 999;
        await context.SaveChangesAsync();

        var result = await new PurchaseAdviceFulfillmentService(context)
            .BackPostClosedAsync(20, 10m, "missing-allocation", "hash", 99);

        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseAdviceErrorCodes.AllocationNotFound, result.ErrorCode);
        Assert.Empty(context.PurchaseAdviceFulfillmentPostings);
    }

    [Fact]
    public async Task PurchaseOrderCloseRemainingPathBackPostsClosedAndDoesNotFulfillRestock()
    {
        using var context = CreateDbContext();
        await SeedScenarioAsync(context, allocationCount: 1);
        var fulfillment = new PurchaseAdviceFulfillmentService(context);
        var service = new PurchaseOrderService(
            context,
            Mock.Of<IUnitConversionService>(),
            Mock.Of<IRestockAllocationService>(),
            purchaseAdviceFulfillment: fulfillment);
        var line = await context.PurchaseOrderLines.SingleAsync(x => x.PurchaseOrderLineId == 20);

        var result = await service.CloseLineRemainingAsync(new ClosePurchaseOrderLineRemainingRequest
        {
            PurchaseOrderLineId = line.PurchaseOrderLineId,
            RowVersion = Convert.ToBase64String(line.RowVersion),
            Reason = "Nhà cung cấp không giao bù",
            RequestKey = "close-service-193"
        }, 99, new[] { RoleConstants.BusinessOwner });

        Assert.True(result.IsSuccess, result.Message);
        var posting = await context.PurchaseAdviceFulfillmentPostings.SingleAsync();
        Assert.Equal(PurchaseAdviceFulfillmentPostingTypes.Closed, posting.PostingType);
        Assert.Equal(10m, posting.Quantity);
        Assert.Equal(10m, (await context.PurchaseAdviceLines.SingleAsync()).ClosedBaseQuantity);
        Assert.Empty(context.RestockFulfillmentPostings);
    }

    [Fact]
    public void StatusPolicyDerivesAllocationAndFulfillmentStates()
    {
        var line = new PurchaseAdviceLine
        {
            RequestedPurchaseBaseQuantity = 10m,
            AllocatedToPoBaseQuantity = 5m
        };

        Assert.Equal(PurchaseAdviceStatuses.PartiallyAllocated,
            PurchaseAdviceStatusPolicy.DeriveLineStatus(line, PurchaseAdviceStatuses.Submitted));

        line.AllocatedToPoBaseQuantity = 10m;
        Assert.Equal(PurchaseAdviceStatuses.FullyAllocated,
            PurchaseAdviceStatusPolicy.DeriveLineStatus(line, PurchaseAdviceStatuses.Submitted));

        line.AcceptedBaseQuantity = 4m;
        Assert.Equal(PurchaseAdviceStatuses.PartiallyFulfilled,
            PurchaseAdviceStatusPolicy.DeriveLineStatus(line, PurchaseAdviceStatuses.Submitted));

        line.ClosedBaseQuantity = 6m;
        Assert.Equal(PurchaseAdviceStatuses.Completed,
            PurchaseAdviceStatusPolicy.DeriveLineStatus(line, PurchaseAdviceStatuses.Submitted));
    }

    [Fact]
    public async Task BackfillDryRunReportsTraceableCandidateWithoutWritingLedger()
    {
        using var context = CreateDbContext();
        await SeedScenarioAsync(context, allocationCount: 1);
        context.PurchaseOrderReceiptPostings.Add(new PurchaseOrderReceiptPosting
        {
            PurchaseOrderLineId = 20,
            BranchReceiptLineId = 40,
            AcceptedBaseQuantity = 4m,
            CreatedByStaffId = 99,
            CreatedAtUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var report = await new PurchaseAdviceFulfillmentService(context).BuildBackfillDryRunReportAsync();

        Assert.Equal(1, report.AcceptedCandidateCount);
        Assert.Equal(4m, report.AcceptedCandidateQuantity);
        Assert.Contains(report.Items, x => x.Status == PurchaseAdviceBackfillStatuses.Ready);
        Assert.Empty(context.PurchaseAdviceFulfillmentPostings);
    }

    [Fact]
    public async Task BackfillDryRunMarksUntraceableReceiptForManualReview()
    {
        using var context = CreateDbContext();
        await SeedScenarioAsync(context, allocationCount: 1);
        context.PurchaseOrderLineAllocations.RemoveRange(context.PurchaseOrderLineAllocations);
        context.PurchaseOrderReceiptPostings.Add(new PurchaseOrderReceiptPosting
        {
            PurchaseOrderLineId = 20,
            BranchReceiptLineId = 40,
            AcceptedBaseQuantity = 4m,
            CreatedByStaffId = 99,
            CreatedAtUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var report = await new PurchaseAdviceFulfillmentService(context).BuildBackfillDryRunReportAsync();

        Assert.Equal(1, report.ManualReviewCount);
        Assert.Contains(report.Items, x => x.Status == PurchaseAdviceBackfillStatuses.ManualReviewRequired);
        Assert.Empty(context.PurchaseAdviceFulfillmentPostings);
    }

    private static async Task SeedScenarioAsync(AppDbContext context, int allocationCount)
    {
        var now = DateTime.UtcNow;
        context.Stores.Add(new Store
        {
            StoreId = StoreId,
            Name = "Store SC-01",
            Address = "Test",
            Phone = "0900000193",
            Active = true,
            CreatedAt = now
        });
        context.Suppliers.Add(new Supplier
        {
            SupplierId = SupplierId,
            Code = "SUP-SC01",
            Name = "Supplier SC-01",
            Active = true,
            CreatedAt = now,
            UpdatedAt = now
        });
        context.Units.Add(new Unit
        {
            UnitId = UnitId,
            UnitCode = "unit-sc01",
            Name = "Unit SC-01",
            Active = true
        });
        context.Ingredients.Add(new Ingredient
        {
            IngredientId = IngredientId,
            Code = "ING-SC01",
            Name = "Ingredient SC-01",
            BaseUnitId = UnitId,
            Active = true
        });
        context.PurchaseAdvices.Add(new PurchaseAdvice
        {
            PurchaseAdviceId = 1,
            AdviceNumber = "PA-193-1",
            RequestKey = "pa-193-1",
            StoreId = StoreId,
            RequestedByStaffId = 99,
            Status = PurchaseAdviceStatuses.Allocated,
            NeededByDate = now.Date,
            Priority = PurchaseAdvicePriorities.Normal,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        context.PurchaseAdviceLines.Add(new PurchaseAdviceLine
        {
            PurchaseAdviceLineId = 1,
            PurchaseAdviceId = 1,
            RestockRequestId = 1,
            IngredientId = IngredientId,
            RequestedPurchaseBaseQuantity = 10m,
            BaseUnitId = UnitId,
            NeededByDate = now.Date,
            IsActiveReservation = false
        });

        for (var index = 0; index < allocationCount; index++)
        {
            var lineId = 20 + index;
            var receiptLineId = 40 + index;
            var quantity = allocationCount == 1 ? 10m : 6m - (index * 2m);
            context.PurchaseOrders.Add(new PurchaseOrder
            {
                PurchaseOrderId = 10 + index,
                Code = $"PO-193-{index}",
                StoreId = StoreId,
                SupplierId = SupplierId,
                Status = PurchaseOrderStatuses.Approved,
                OrderDate = now.Date,
                CreatedByStaffId = 99,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            context.PurchaseOrderLines.Add(new PurchaseOrderLine
            {
                PurchaseOrderLineId = lineId,
                PurchaseOrderId = 10 + index,
                IngredientId = IngredientId,
                IngredientSupplierId = 1,
                PackageUnitIdSnapshot = UnitId,
                PackageQuantitySnapshot = 1m,
                PackagePriceSnapshot = 1m,
                PackageCount = quantity,
                OrderedBaseQuantity = quantity,
                PromisedLeadTimeDaysSnapshot = 1
            });
            context.PurchaseOrderLineAllocations.Add(new PurchaseOrderLineAllocation
            {
                PurchaseOrderLineAllocationId = 100 + index,
                PurchaseAdviceLineId = 1,
                PurchaseOrderBatchLineId = 1,
                PurchaseOrderId = 10 + index,
                PurchaseOrderLineId = lineId,
                AllocatedBaseQuantity = quantity,
                AllocatedPackageQuantity = quantity,
                CreatedAtUtc = now
            });
            context.BranchReceipts.Add(new BranchReceipt
            {
                BranchReceiptId = 30 + index,
                ReceiptCode = $"BR-193-{index}",
                StoreId = StoreId,
                SupplierId = SupplierId,
                PurchaseOrderId = 10 + index,
                Status = "CONFIRMED",
                ReceiptKey = $"br-193-{index}",
                ReceivedAt = now,
                CreatedAt = now,
                CreatedByStaffId = 99
            });
            context.BranchReceiptLines.Add(new BranchReceiptLine
            {
                BranchReceiptLineId = receiptLineId,
                BranchReceiptId = 30 + index,
                PurchaseOrderLineId = lineId,
                IngredientId = IngredientId,
                InputQuantity = quantity,
                InputUnitId = UnitId,
                ReceivedBaseQuantity = quantity,
                BaseUnitId = UnitId,
                BaseUnitCostSnapshot = 1m,
                LineTotalCost = quantity
            });
        }

        await context.SaveChangesAsync();
    }
}
