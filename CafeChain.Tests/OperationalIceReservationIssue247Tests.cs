using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Services.Inventories;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Ice;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Transactions;
using CafeChain.Models.Orders;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CafeChain.Tests;

public sealed class OperationalIceReservationIssue247Tests : IntegrationTestBase
{
    private const int StoreId = 901;
    private const int IngredientId = 902;
    private const int UnitId = 903;
    private const int StaffId = 904;

    [Fact]
    public async Task OpenAllocation_ReservesUsableStockWithoutReducingPhysicalOnHand()
    {
        using var context = CreateDbContext();
        var setup = SeedOpenSetup(context, available: 100m, reserved: 15m);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = await service.OpenAllocationAsync(
            new OpenIceAllocationRequest
            {
                OperationalShiftId = setup.OperationalShiftId,
                InitialIssuedQuantity = 30m
            },
            ManagerActor());

        Assert.True(result.IsSuccess, result.Message);
        var inventory = await context.StoreInventories.SingleAsync(x => x.StoreInventoryId == setup.StoreInventoryId);
        Assert.Equal(100m, inventory.AvailableQty);
        Assert.Equal(45m, inventory.ReservedQty);
        Assert.Equal(30m, result.Data.ReservedOutstandingQuantity);
        Assert.Equal(OperationalIceStatuses.Open, result.Data.Status);
    }

    [Fact]
    public async Task OpenAllocation_WhenUsableStockIsInsufficient_FailsWithoutMutation()
    {
        using var context = CreateDbContext();
        var setup = SeedOpenSetup(context, available: 20m, reserved: 15m);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = await service.OpenAllocationAsync(
            new OpenIceAllocationRequest
            {
                OperationalShiftId = setup.OperationalShiftId,
                InitialIssuedQuantity = 6m
            },
            ManagerActor());

        Assert.False(result.IsSuccess);
        Assert.Equal(OperationalIceErrorCodes.InsufficientUsableStock, result.ErrorCode);
        var inventory = await context.StoreInventories.SingleAsync(x => x.StoreInventoryId == setup.StoreInventoryId);
        Assert.Equal(20m, inventory.AvailableQty);
        Assert.Equal(15m, inventory.ReservedQty);
        Assert.Empty(context.IceAllocations);
    }

    [Fact]
    public async Task SupplementalIssue_RequiresApprovalAndOnlyApprovalAddsReservation()
    {
        using var context = CreateDbContext();
        var setup = SeedOpenSetup(context, available: 100m, reserved: 0m);
        await context.SaveChangesAsync();
        var service = CreateService(context);
        var opened = await service.OpenAllocationAsync(
            new OpenIceAllocationRequest { OperationalShiftId = setup.OperationalShiftId, InitialIssuedQuantity = 20m },
            ManagerActor());

        var requested = await service.RequestSupplementalAsync(
            new RequestSupplementalIceRequest
            {
                IceAllocationId = opened.Data.IceAllocationId,
                Quantity = 10m,
                Reason = "Khách tăng đột biến"
            },
            ShiftLeadActor());

        Assert.True(requested.IsSuccess, requested.Message);
        Assert.Equal(20m, await ReservedQtyAsync(context, setup.StoreInventoryId));

        var approved = await service.DecideSupplementalAsync(
            new DecideSupplementalIceRequest
            {
                SupplementalIssuePublicId = requested.Data.PublicId,
                Approve = true
            },
            ManagerActor());

        Assert.True(approved.IsSuccess, approved.Message);
        Assert.True(approved.Data.ReservationApplied);
        Assert.Equal(30m, await ReservedQtyAsync(context, setup.StoreInventoryId));
        var allocation = await context.IceAllocations.SingleAsync();
        Assert.Equal(10m, allocation.SupplementalIssuedQuantity);
        Assert.Equal(30m, allocation.ReservedOutstandingQuantity);
    }

    [Fact]
    public async Task Cashier_CannotOpenOperationalIceAllocation()
    {
        using var context = CreateDbContext();
        var setup = SeedOpenSetup(context, available: 100m, reserved: 0m);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = await service.OpenAllocationAsync(
            new OpenIceAllocationRequest { OperationalShiftId = setup.OperationalShiftId, InitialIssuedQuantity = 10m },
            new AdminActorContext { StaffId = StaffId, StoreId = StoreId, RoleNames = [RoleConstants.SalesStaff] });

        Assert.False(result.IsSuccess);
        Assert.Equal(OperationalIceErrorCodes.Forbidden, result.ErrorCode);
    }

    [Fact]
    public async Task ReservationConsumption_UsesWorkShiftLinkAndReplayEvidencePreventsSecondConsumption()
    {
        using var context = CreateDbContext();
        var setup = SeedOpenSetup(context, available: 100m, reserved: 10m);
        const int workShiftId = 920;
        const int orderId = 930;
        context.WorkShifts.Add(new WorkShift
        {
            ShiftId = workShiftId,
            StoreId = StoreId,
            UserId = StaffId,
            StartTime = DateTime.UtcNow,
            StartingCash = 0,
            ExpectedEndingCash = 0,
            Status = "Open"
        });
        context.OperationalShiftWorkShifts.Add(new OperationalShiftWorkShift
        {
            OperationalShiftId = setup.OperationalShiftId,
            WorkShiftId = workShiftId,
            LinkedByStaffId = StaffId,
            LinkedAtUtc = DateTime.UtcNow
        });
        context.IceAllocations.Add(new IceAllocation
        {
            PublicId = Guid.NewGuid(),
            OperationalShiftId = setup.OperationalShiftId,
            IcePolicyId = setup.IcePolicyId,
            StoreInventoryId = setup.StoreInventoryId,
            IngredientId = IngredientId,
            InitialIssuedQuantity = 10m,
            ReservedOutstandingQuantity = 10m,
            ReservationReference = "ICE:TEST-CONSUME",
            Status = OperationalIceStatuses.Open,
            CreatedByStaffId = StaffId,
            OpenedByStaffId = StaffId,
            CreatedAtUtc = DateTime.UtcNow,
            OpenedAtUtc = DateTime.UtcNow,
            Revision = 1
        });
        var order = new Order
        {
            OrderId = orderId,
            StoreId = StoreId,
            WorkShiftId = workShiftId,
            OrderStatusId = SystemConstants.OrderStatuses.Completed,
            PaymentStatusId = SystemConstants.PaymentStatuses.Paid,
            OrderTypeId = SystemConstants.OrderTypes.DineIn,
            Total = 10000,
            CreatedAt = DateTime.UtcNow
        };
        context.Orders.Add(order);
        await context.SaveChangesAsync();
        var service = new OperationalIceReservationConsumptionService(context);

        var first = await service.ConsumeForCommittedOrderAsync(order, new Dictionary<int, decimal> { [IngredientId] = 3m });
        await context.SaveChangesAsync();

        Assert.True(first.IsSuccess, first.Message);
        var allocation = await context.IceAllocations.SingleAsync();
        Assert.Equal(7m, allocation.ReservedOutstandingQuantity);
        Assert.Equal(3m, allocation.TheoreticalUsageQuantity);
        Assert.Equal(7m, await ReservedQtyAsync(context, setup.StoreInventoryId));
        Assert.Equal(100m, await context.StoreInventories.Where(x => x.StoreInventoryId == setup.StoreInventoryId).Select(x => x.AvailableQty).SingleAsync());

        context.InventoryTransactions.Add(new InventoryTransaction
        {
            StoreInventoryId = setup.StoreInventoryId,
            Type = InventoryTransactionTypeEnum.SALES_DEDUCTION,
            StockStatus = InventoryStockStatus.NORMAL,
            Quantity = 3m,
            BeforeQty = 100m,
            AfterQty = 97m,
            ReferenceOrderId = orderId,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var replay = await service.ConsumeForCommittedOrderAsync(order, new Dictionary<int, decimal> { [IngredientId] = 3m });
        await context.SaveChangesAsync();
        Assert.True(replay.IsSuccess);
        Assert.Equal(7m, allocation.ReservedOutstandingQuantity);
        Assert.Equal(3m, allocation.TheoreticalUsageQuantity);
    }

    private static OperationalIceService CreateService(CafeChain.Data.AppDbContext context)
    {
        var scope = new Mock<IScopeAuthorizationService>();
        scope.Setup(x => x.CanAccessStoreAsync(It.IsAny<int>(), StoreId)).ReturnsAsync(true);
        return new OperationalIceService(context, scope.Object);
    }

    private static AdminActorContext ManagerActor() => new()
    {
        StaffId = StaffId,
        StoreId = StoreId,
        RoleNames = [RoleConstants.StoreManager]
    };

    private static AdminActorContext ShiftLeadActor() => new()
    {
        StaffId = StaffId,
        StoreId = StoreId,
        RoleNames = [RoleConstants.ShiftSupervisor]
    };

    private static (int OperationalShiftId, int IcePolicyId, int StoreInventoryId) SeedOpenSetup(
        CafeChain.Data.AppDbContext context,
        decimal available,
        decimal reserved)
    {
        context.Stores.Add(new Store
        {
            StoreId = StoreId,
            Name = "Ice test store",
            Active = true,
            CreatedAt = DateTime.UtcNow
        });
        context.Units.Add(new Unit { UnitId = UnitId, UnitCode = "ICE_U", Name = "Gram", Active = true });
        context.Ingredients.Add(new Ingredient
        {
            IngredientId = IngredientId,
            Code = "ICE_TEST",
            Name = "Đá viên test",
            BaseUnitId = UnitId,
            Active = true
        });
        var inventory = new StoreInventory
        {
            StoreId = StoreId,
            IngredientId = IngredientId,
            AvailableQty = available,
            ReservedQty = reserved,
            LastUpdated = DateTime.UtcNow,
            RowVersion = [0]
        };
        context.StoreInventories.Add(inventory);
        var policy = new IcePolicy
        {
            StoreId = StoreId,
            IngredientId = IngredientId,
            DisplayUnitId = UnitId,
            SuggestedDailyQuantity = 100,
            SuggestedShiftQuantity = 20,
            AllowSupplementalIssue = true,
            AllowSameDayCarryOver = true,
            RequireVarianceApproval = true,
            UpdatedByStaffId = StaffId,
            UpdatedAtUtc = DateTime.UtcNow,
            Active = true,
            RowVersion = [0]
        };
        context.IcePolicies.Add(policy);
        var shift = new OperationalShift
        {
            StoreId = StoreId,
            BusinessDate = DateTime.UtcNow.Date,
            Name = $"Ca test {Guid.NewGuid():N}",
            StartAtUtc = DateTime.UtcNow.AddHours(-1),
            EndAtUtc = DateTime.UtcNow.AddHours(7),
            Status = OperationalIceStatuses.Draft,
            CreatedByStaffId = StaffId,
            CreatedAtUtc = DateTime.UtcNow,
            RowVersion = [0]
        };
        context.OperationalShifts.Add(shift);
        context.SaveChanges();
        return (shift.OperationalShiftId, policy.IcePolicyId, inventory.StoreInventoryId);
    }

    private static Task<decimal> ReservedQtyAsync(CafeChain.Data.AppDbContext context, int storeInventoryId) =>
        context.StoreInventories.Where(x => x.StoreInventoryId == storeInventoryId).Select(x => x.ReservedQty).SingleAsync();
}
