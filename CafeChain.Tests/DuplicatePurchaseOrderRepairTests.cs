using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Inventories;
using CafeChain.Data;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Customers;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Procurement;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Inventories.Suppliers;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CafeChain.Tests;

public sealed class DuplicatePurchaseOrderRepairTests : IntegrationTestBase
{
    [Fact]
    public async Task DuplicateOrderRepair_DryRun_DoesNotModify()
    {
        await using var context = CreateDbContext();
        await SeedDuplicateAsync(context);
        var service = new DuplicatePurchaseOrderRepairService(context, Mock.Of<IPurchaseOrderBatchService>());

        var report = await service.DryRunAsync();

        Assert.Equal(1, report.SafeToCancelCount);
        Assert.Equal(0, report.ManualReviewCount);
        Assert.Equal(2, await context.PurchaseOrderBatches.CountAsync(x => x.Status != PurchaseOrderBatchStatuses.Cancelled));
    }

    [Fact]
    public async Task DuplicateOrderRepair_ExecuteAndRerun_IsIdempotent()
    {
        await using var context = CreateDbContext();
        var seed = await SeedDuplicateAsync(context);
        var cancelCalls = 0;
        var batchService = new Mock<IPurchaseOrderBatchService>();
        batchService.Setup(x => x.CancelAsync(
                It.IsAny<int>(),
                It.IsAny<PurchaseOrderBatchTransitionRequest>(),
                It.IsAny<AdminActorContext>()))
            .Returns<int, PurchaseOrderBatchTransitionRequest, AdminActorContext>(async (id, _, _) =>
            {
                cancelCalls++;
                var batch = await context.PurchaseOrderBatches
                    .Include(x => x.ChildPurchaseOrders)
                    .SingleAsync(x => x.PurchaseOrderBatchId == id);
                batch.Status = PurchaseOrderBatchStatuses.Cancelled;
                foreach (var order in batch.ChildPurchaseOrders) order.Status = PurchaseOrderStatuses.Cancelled;
                await context.SaveChangesAsync();
                return ServiceResult<PurchaseOrderBatchDetailDto>.Success(new PurchaseOrderBatchDetailDto());
            });
        var service = new DuplicatePurchaseOrderRepairService(context, batchService.Object);
        var actor = new AdminActorContext { StaffId = seed.OwnerId, RoleNames = new[] { RoleConstants.BusinessOwner } };

        var first = await service.ExecuteAsync(actor);
        var second = await service.ExecuteAsync(actor);

        Assert.True(first.IsSuccess, first.Message);
        Assert.True(second.IsSuccess, second.Message);
        Assert.Equal(1, first.Data!.CancelledCount);
        Assert.Equal(0, second.Data!.CancelledCount);
        Assert.Equal(1, cancelCalls);
    }

    [Fact]
    public async Task DuplicateWithApprovedBatch_IsFlaggedForManualReview()
    {
        await using var context = CreateDbContext();
        var seed = await SeedDuplicateAsync(context);
        var duplicate = await context.PurchaseOrderBatches.SingleAsync(x => x.PurchaseOrderBatchId == seed.DuplicateBatchId);
        duplicate.Status = PurchaseOrderBatchStatuses.Approved;
        duplicate.ApprovedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync();

        var report = await new DuplicatePurchaseOrderRepairService(context, Mock.Of<IPurchaseOrderBatchService>())
            .DryRunAsync();

        Assert.Equal(0, report.SafeToCancelCount);
        Assert.Equal(1, report.ManualReviewCount);
    }

    private static async Task<(int OwnerId, int DuplicateBatchId)> SeedDuplicateAsync(AppDbContext context)
    {
        var now = DateTime.UtcNow;
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var store = new Store { Name = "Repair Store", Address = "Test", Phone = Guid.NewGuid().ToString("N")[..10], Active = true, CreatedAt = now };
        var account = new Account { Email = Guid.NewGuid() + "@test.local", PasswordHash = "x", Active = true, CreatedAt = now };
        var unit = new Unit { UnitCode = "rep" + suffix, Name = "Kilogram", Active = true };
        context.AddRange(store, account, unit);
        await context.SaveChangesAsync();
        var owner = new Staff { AccountId = account.AccountId, FullName = "Owner", Active = true, CreatedAt = now };
        var ingredient = new Ingredient { Code = "ING-" + suffix, Name = "Ingredient", BaseUnitId = unit.UnitId, Active = true };
        var supplier = new Supplier { Code = "SUP-" + suffix, Name = "Supplier", Active = true, CreatedAt = now, UpdatedAt = now };
        context.AddRange(owner, ingredient, supplier);
        await context.SaveChangesAsync();
        var offer = new IngredientSupplier
        {
            IngredientId = ingredient.IngredientId,
            SupplierId = supplier.SupplierId,
            UnitId = unit.UnitId,
            PackageQuantity = 1m,
            CurrentPrice = 1m,
            MinimumOrderPackageCount = 1,
            LeadTimeDays = 1,
            Active = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        var advice = new PurchaseAdvice
        {
            AdviceNumber = "PA-REPAIR-" + suffix,
            StoreId = store.StoreId,
            RequestedByStaffId = owner.StaffId,
            Status = PurchaseAdviceStatuses.FullyAllocated,
            Priority = PurchaseAdvicePriorities.Normal,
            NeededByDate = now.AddDays(1),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        context.AddRange(offer, advice);
        await context.SaveChangesAsync();
        var restock = new RestockRequest
        {
            StoreId = store.StoreId,
            IngredientId = ingredient.IngredientId,
            RequestedQuantity = 1m,
            Status = RestockRequestStatuses.Processing,
            Priority = RestockRequestPriorities.Normal,
            CreatedByStaffId = owner.StaffId,
            CreatedAt = now,
            UpdatedAt = now
        };
        context.RestockRequests.Add(restock);
        await context.SaveChangesAsync();
        var adviceLine = new PurchaseAdviceLine
        {
            PurchaseAdviceId = advice.PurchaseAdviceId,
            RestockRequestId = restock.RestockRequestId,
            IngredientId = ingredient.IngredientId,
            BaseUnitId = unit.UnitId,
            RequestedPurchaseBaseQuantity = 1m,
            NeededByDate = now.AddDays(1),
            IsActiveReservation = false
        };
        context.PurchaseAdviceLines.Add(adviceLine);
        await context.SaveChangesAsync();

        var batches = new List<PurchaseOrderBatch>();
        for (var i = 1; i <= 2; i++)
        {
            var batch = new PurchaseOrderBatch
            {
                BatchNumber = $"POB-REPAIR-{i}",
                RequestKey = Guid.NewGuid().ToString("N"),
                SupplierId = supplier.SupplierId,
                Status = PurchaseOrderBatchStatuses.PendingApproval,
                ExpectedDeliveryFrom = now.AddDays(1),
                ExpectedDeliveryTo = now.AddDays(2),
                CreatedByStaffId = owner.StaffId,
                CreatedAtUtc = now.AddMinutes(i),
                UpdatedAtUtc = now.AddMinutes(i)
            };
            var batchLine = new PurchaseOrderBatchLine
            {
                IngredientId = ingredient.IngredientId,
                IngredientSupplierId = offer.IngredientSupplierId,
                PackageUnitId = unit.UnitId,
                PackageQuantitySnapshot = 1m,
                TotalPackageCount = 1m,
                OrderedPackageCount = 1m,
                TotalBaseQuantity = 1m,
                UnitPricePerPackage = 1m,
                PackagePriceSnapshot = 1m,
                LineTotal = 1m
            };
            batch.Lines.Add(batchLine);
            var order = new PurchaseOrder
            {
                Code = $"PO-REPAIR-{i}",
                StoreId = store.StoreId,
                SupplierId = supplier.SupplierId,
                Status = PurchaseOrderStatuses.Draft,
                OrderDate = now,
                CreatedByStaffId = owner.StaffId,
                CreatedAtUtc = now.AddMinutes(i),
                UpdatedAtUtc = now.AddMinutes(i)
            };
            var orderLine = new PurchaseOrderLine
            {
                PurchaseAdviceLineId = adviceLine.PurchaseAdviceLineId,
                IngredientId = ingredient.IngredientId,
                IngredientSupplierId = offer.IngredientSupplierId,
                PackageUnitIdSnapshot = unit.UnitId,
                PackageQuantitySnapshot = 1m,
                PackagePriceSnapshot = 1m,
                PackageCount = 1m,
                OrderedPackageCount = 1m,
                UnitPricePerPackage = 1m,
                OrderedBaseQuantity = 1m
            };
            order.Lines.Add(orderLine);
            batch.ChildPurchaseOrders.Add(order);
            context.PurchaseOrderBatches.Add(batch);
            await context.SaveChangesAsync();
            batchLine.Allocations.Add(new PurchaseOrderLineAllocation
            {
                PurchaseAdviceLineId = adviceLine.PurchaseAdviceLineId,
                PurchaseOrderId = order.PurchaseOrderId,
                PurchaseOrderLineId = orderLine.PurchaseOrderLineId,
                AllocatedBaseQuantity = 1m,
                AllocatedPackageQuantity = 1m,
                CreatedAtUtc = now.AddMinutes(i)
            });
            await context.SaveChangesAsync();
            batches.Add(batch);
        }

        return (owner.StaffId, batches[1].PurchaseOrderBatchId);
    }
}
