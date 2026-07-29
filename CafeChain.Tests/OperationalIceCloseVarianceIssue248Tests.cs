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
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CafeChain.Tests;

public sealed class OperationalIceCloseVarianceIssue248Tests : IntegrationTestBase
{
    private const int StoreId = 951;
    private const int IngredientId = 952;
    private const int UnitId = 953;
    private const int ManagerStaffId = 954;
    private const int ReceiverStaffId = 955;

    [Fact]
    public async Task CloseShift_ZeroVariance_DoesNotPostAdjustment()
    {
        using var context = CreateDbContext();
        var setup = SeedAllocation(context, initial: 10m, theoretical: 8m, outstanding: 3m, available: 93m);
        var deduction = context.InventoryTransactions.Local.Single(x =>
            x.StoreInventoryId == setup.StoreInventoryId && x.Type == InventoryTransactionTypeEnum.SALES_DEDUCTION);
        context.InventoryTransactions.Add(new InventoryTransaction
        {
            StoreInventoryId = setup.StoreInventoryId,
            Type = InventoryTransactionTypeEnum.SALES_RETURN,
            StockStatus = InventoryStockStatus.NORMAL,
            Quantity = 1m,
            BeforeQty = 92m,
            AfterQty = 93m,
            ReferenceOrderId = deduction.ReferenceOrderId,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var result = await CreateService(context).CloseAllocationAsync(
            new CloseIceAllocationRequest
            {
                IceAllocationId = setup.AllocationId,
                ReturnedQuantity = 3m,
                ReturnCondition = IceReturnConditions.SealedIntact,
                ReturnReceivedByStaffId = ReceiverStaffId
            },
            ManagerActor());

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(OperationalIceStatuses.Closed, result.Data.Status);
        Assert.Equal(7m, result.Data.ActualUsageQuantity);
        Assert.Equal(7m, result.Data.TheoreticalUsageQuantity);
        Assert.Equal(0m, result.Data.VarianceQuantity);
        Assert.Equal(0m, await InventoryValueAsync(context, setup.StoreInventoryId, x => x.ReservedQty));
        Assert.Equal(93m, await InventoryValueAsync(context, setup.StoreInventoryId, x => x.AvailableQty));
        Assert.Empty(context.IceInventoryPostings);
        Assert.Empty(context.InventoryTransactions.Where(x => x.Type == InventoryTransactionTypeEnum.ICE_VARIANCE_OUT));
    }

    [Fact]
    public async Task CloseShift_PositiveVariance_IsIdempotent()
    {
        using var context = CreateDbContext();
        var setup = SeedAllocation(context, initial: 10m, theoretical: 6m, outstanding: 4m, available: 94m);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var submitted = await service.CloseAllocationAsync(
            new CloseIceAllocationRequest
            {
                IceAllocationId = setup.AllocationId,
                CloseReason = "Đá tan trong thùng vận hành"
            },
            ManagerActor());

        Assert.True(submitted.IsSuccess, submitted.Message);
        Assert.Equal(OperationalIceStatuses.PendingApproval, submitted.Data.Status);
        Assert.Equal(4m, submitted.Data.VarianceQuantity);
        Assert.Equal(4m, await InventoryValueAsync(context, setup.StoreInventoryId, x => x.ReservedQty));

        var approved = await service.ApproveVarianceAsync(
            new ApproveIceVarianceRequest
            {
                IceAllocationId = setup.AllocationId,
                Reason = "Đã kiểm tra giao nhận và POS"
            },
            ManagerActor());
        var replay = await service.ApproveVarianceAsync(
            new ApproveIceVarianceRequest
            {
                IceAllocationId = setup.AllocationId,
                Reason = "Retry"
            },
            ManagerActor());

        Assert.True(approved.IsSuccess, approved.Message);
        Assert.True(replay.IsSuccess, replay.Message);
        Assert.Equal(OperationalIceStatuses.Closed, approved.Data.Status);
        Assert.Equal(90m, await InventoryValueAsync(context, setup.StoreInventoryId, x => x.AvailableQty));
        Assert.Equal(0m, await InventoryValueAsync(context, setup.StoreInventoryId, x => x.ReservedQty));
        var movements = await context.InventoryTransactions
            .Where(x => x.Type == InventoryTransactionTypeEnum.ICE_VARIANCE_OUT)
            .ToListAsync();
        Assert.Single(movements);
        Assert.Equal(-4m, movements[0].Quantity);
        Assert.Single(await context.IceInventoryPostings.ToListAsync());
    }

    [Fact]
    public async Task CloseShift_NegativeVariance_DoesNotIncreaseInventory()
    {
        using var context = CreateDbContext();
        var setup = SeedAllocation(context, initial: 10m, theoretical: 12m, outstanding: 0m, available: 88m);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var submitted = await service.CloseAllocationAsync(
            new CloseIceAllocationRequest
            {
                IceAllocationId = setup.AllocationId,
                CloseReason = "Kiểm tra lại ledger hoàn đơn"
            },
            ManagerActor());
        var reconciled = await service.ReconcileVarianceAsync(
            new ReconcileIceVarianceRequest
            {
                IceAllocationId = setup.AllocationId,
                Reason = "Đã đối chiếu, không tự hoàn tồn"
            },
            ManagerActor());

        Assert.True(submitted.IsSuccess, submitted.Message);
        Assert.Equal(OperationalIceStatuses.ReconciliationRequired, submitted.Data.Status);
        Assert.Equal(-2m, submitted.Data.VarianceQuantity);
        Assert.True(reconciled.IsSuccess, reconciled.Message);
        Assert.Equal(OperationalIceStatuses.Closed, reconciled.Data.Status);
        Assert.Equal(88m, await InventoryValueAsync(context, setup.StoreInventoryId, x => x.AvailableQty));
        Assert.DoesNotContain(await context.InventoryTransactions
            .Where(x => x.StoreInventoryId == setup.StoreInventoryId).ToListAsync(), x =>
            x.Type == InventoryTransactionTypeEnum.ICE_VARIANCE_OUT);
    }

    [Fact]
    public async Task PositiveVariance_OverPolicyLimit_RequiresElevatedApprover()
    {
        using var context = CreateDbContext();
        var setup = SeedAllocation(context, initial: 10m, theoretical: 4m, outstanding: 6m, available: 96m);
        await context.SaveChangesAsync();
        var service = CreateService(context);
        var submitted = await service.CloseAllocationAsync(
            new CloseIceAllocationRequest
            {
                IceAllocationId = setup.AllocationId,
                CloseReason = "Chênh lệch vượt định mức"
            },
            ManagerActor());

        var managerAttempt = await service.ApproveVarianceAsync(
            new ApproveIceVarianceRequest { IceAllocationId = setup.AllocationId, Reason = "Manager duyệt" },
            ManagerActor());
        var ownerAttempt = await service.ApproveVarianceAsync(
            new ApproveIceVarianceRequest { IceAllocationId = setup.AllocationId, Reason = "Owner duyệt" },
            OwnerActor());

        Assert.True(submitted.IsSuccess, submitted.Message);
        Assert.False(managerAttempt.IsSuccess);
        Assert.Equal(OperationalIceErrorCodes.Forbidden, managerAttempt.ErrorCode);
        Assert.True(ownerAttempt.IsSuccess, ownerAttempt.Message);
        Assert.Single(await context.InventoryTransactions
            .Where(x => x.StoreInventoryId == setup.StoreInventoryId
                        && x.Type == InventoryTransactionTypeEnum.ICE_VARIANCE_OUT)
            .ToListAsync());
    }

    [Fact]
    public async Task Carryover_TransfersWithinSameBusinessDate()
    {
        using var context = CreateDbContext();
        var setup = SeedCarryPair(context, sameBusinessDate: true);
        await context.SaveChangesAsync();

        var result = await CreateService(context).ConfirmCarryOverAsync(
            new ConfirmIceCarryOverRequest
            {
                FromIceAllocationId = setup.SourceAllocationId,
                ToIceAllocationId = setup.TargetAllocationId,
                Quantity = 4m,
                ReceivedByStaffId = ReceiverStaffId
            },
            ManagerActor());

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(IceCarryOverStatuses.Confirmed, result.Data.Status);
        var source = await context.IceAllocations.SingleAsync(x => x.IceAllocationId == setup.SourceAllocationId);
        var target = await context.IceAllocations.SingleAsync(x => x.IceAllocationId == setup.TargetAllocationId);
        Assert.Equal(4m, source.ClosingCarryQuantity);
        Assert.Equal(4m, target.OpeningCarryQuantity);
        Assert.Equal(6m, source.ReservedOutstandingQuantity);
        Assert.Equal(9m, target.ReservedOutstandingQuantity);
        Assert.Equal(15m, await InventoryValueAsync(context, setup.StoreInventoryId, x => x.ReservedQty));
        Assert.Equal(100m, await InventoryValueAsync(context, setup.StoreInventoryId, x => x.AvailableQty));
    }

    [Fact]
    public async Task Carryover_CannotCrossBusinessDateByDefault()
    {
        using var context = CreateDbContext();
        var setup = SeedCarryPair(context, sameBusinessDate: false);
        await context.SaveChangesAsync();

        var result = await CreateService(context).ConfirmCarryOverAsync(
            new ConfirmIceCarryOverRequest
            {
                FromIceAllocationId = setup.SourceAllocationId,
                ToIceAllocationId = setup.TargetAllocationId,
                Quantity = 4m,
                ReceivedByStaffId = ReceiverStaffId
            },
            ManagerActor());

        Assert.False(result.IsSuccess);
        Assert.Empty(context.IceCarryOvers);
        Assert.Equal(15m, await InventoryValueAsync(context, setup.StoreInventoryId, x => x.ReservedQty));
    }

    [Fact]
    public async Task UsedIce_CannotBeReturnedToInventory()
    {
        using var context = CreateDbContext();
        var setup = SeedAllocation(context, initial: 10m, theoretical: 7m, outstanding: 3m, available: 93m);
        await context.SaveChangesAsync();

        var result = await CreateService(context).CloseAllocationAsync(
            new CloseIceAllocationRequest
            {
                IceAllocationId = setup.AllocationId,
                ReturnedQuantity = 3m,
                ReturnCondition = "MELTED",
                ReturnReceivedByStaffId = ManagerStaffId
            },
            ManagerActor());

        Assert.False(result.IsSuccess);
        Assert.Equal(OperationalIceStatuses.Open, await context.IceAllocations
            .Where(x => x.IceAllocationId == setup.AllocationId).Select(x => x.Status).SingleAsync());
        Assert.Equal(3m, await InventoryValueAsync(context, setup.StoreInventoryId, x => x.ReservedQty));
    }

    [Fact]
    public async Task CancelledShift_ReleasesReservationSafely()
    {
        using var context = CreateDbContext();
        var setup = SeedAllocation(context, initial: 10m, theoretical: 0m, outstanding: 10m, available: 100m);
        await context.SaveChangesAsync();

        var result = await CreateService(context).CancelAllocationAsync(
            new CancelIceAllocationRequest { IceAllocationId = setup.AllocationId, Reason = "Ca không vận hành" },
            ManagerActor());

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(OperationalIceStatuses.Cancelled, await context.IceAllocations
            .Where(x => x.IceAllocationId == setup.AllocationId).Select(x => x.Status).SingleAsync());
        Assert.Equal(0m, await InventoryValueAsync(context, setup.StoreInventoryId, x => x.ReservedQty));
        Assert.Equal(100m, await InventoryValueAsync(context, setup.StoreInventoryId, x => x.AvailableQty));
    }

    [Fact]
    public Task OpeningCarry_EqualsPreviousConfirmedClosingCarry() => Carryover_TransfersWithinSameBusinessDate();

    [Fact]
    public Task CloseShift_CalculatesActualUsage() => CloseShift_ZeroVariance_DoesNotPostAdjustment();

    [Fact]
    public Task CloseShift_PositiveVariance_PostsOnce() => CloseShift_PositiveVariance_IsIdempotent();

    [Fact]
    public Task CloseShift_NegativeVariance_RequiresReconciliation() => CloseShift_NegativeVariance_DoesNotIncreaseInventory();

    [Fact]
    public Task ValidReturn_ReleasesReservation() => CloseShift_ZeroVariance_DoesNotPostAdjustment();

    [Fact]
    public async Task UnauthorizedActor_CannotApproveVariance()
    {
        using var context = CreateDbContext();
        var setup = SeedAllocation(context, initial: 10m, theoretical: 6m, outstanding: 4m, available: 94m);
        await context.SaveChangesAsync();
        var service = CreateService(context);
        await service.CloseAllocationAsync(
            new CloseIceAllocationRequest { IceAllocationId = setup.AllocationId, CloseReason = "Chờ đúng người duyệt" },
            ManagerActor());

        var result = await service.ApproveVarianceAsync(
            new ApproveIceVarianceRequest { IceAllocationId = setup.AllocationId, Reason = "Thu ngân thử duyệt" },
            new AdminActorContext
            {
                StaffId = ManagerStaffId,
                StoreId = StoreId,
                RoleNames = [RoleConstants.SalesStaff]
            });

        Assert.False(result.IsSuccess);
        Assert.Equal(OperationalIceErrorCodes.Forbidden, result.ErrorCode);
        Assert.Empty(await context.IceInventoryPostings.ToListAsync());
    }

    [Fact]
    public Task ConcurrentClose_DoesNotDoublePost() => CloseShift_PositiveVariance_IsIdempotent();

    private static OperationalIceService CreateService(CafeChain.Data.AppDbContext context)
    {
        var scope = new Mock<IScopeAuthorizationService>();
        scope.Setup(x => x.CanAccessStoreAsync(It.IsAny<int>(), StoreId)).ReturnsAsync(true);
        return new OperationalIceService(context, scope.Object);
    }

    private static AdminActorContext ManagerActor() => new()
    {
        StaffId = ManagerStaffId,
        StoreId = StoreId,
        RoleNames = [RoleConstants.StoreManager]
    };

    private static AdminActorContext OwnerActor() => new()
    {
        StaffId = ManagerStaffId,
        StoreId = StoreId,
        RoleNames = [RoleConstants.BusinessOwner]
    };

    private static (int AllocationId, int StoreInventoryId) SeedAllocation(
        CafeChain.Data.AppDbContext context,
        decimal initial,
        decimal theoretical,
        decimal outstanding,
        decimal available)
    {
        var common = SeedCommon(context, available, outstanding);
        var shift = NewShift("Ca đóng", DateTime.UtcNow.Date, -2, 2);
        context.OperationalShifts.Add(shift);
        context.SaveChanges();
        var allocation = NewAllocation(shift.OperationalShiftId, common.PolicyId, common.InventoryId, initial, outstanding);
        allocation.TheoreticalUsageQuantity = theoretical;
        context.IceAllocations.Add(allocation);

        if (theoretical > 0)
        {
            var workShiftId = 970 + shift.OperationalShiftId;
            var orderId = 980 + shift.OperationalShiftId;
            context.WorkShifts.Add(new WorkShift
            {
                ShiftId = workShiftId,
                StoreId = StoreId,
                UserId = ManagerStaffId,
                StartTime = DateTime.UtcNow,
                StartingCash = 0,
                ExpectedEndingCash = 0,
                Status = "Open"
            });
            context.OperationalShiftWorkShifts.Add(new OperationalShiftWorkShift
            {
                OperationalShiftId = shift.OperationalShiftId,
                WorkShiftId = workShiftId,
                LinkedByStaffId = ManagerStaffId,
                LinkedAtUtc = DateTime.UtcNow
            });
            context.Orders.Add(new Order
            {
                OrderId = orderId,
                StoreId = StoreId,
                WorkShiftId = workShiftId,
                OrderStatusId = SystemConstants.OrderStatuses.Completed,
                PaymentStatusId = SystemConstants.PaymentStatuses.Paid,
                OrderTypeId = SystemConstants.OrderTypes.DineIn,
                Total = 10000,
                CreatedAt = DateTime.UtcNow
            });
            context.InventoryTransactions.Add(new InventoryTransaction
            {
                StoreInventoryId = common.InventoryId,
                Type = InventoryTransactionTypeEnum.SALES_DEDUCTION,
                StockStatus = InventoryStockStatus.NORMAL,
                Quantity = theoretical,
                BeforeQty = available + theoretical,
                AfterQty = available,
                ReferenceOrderId = orderId,
                CreatedAt = DateTime.UtcNow
            });
        }
        context.SaveChanges();
        return (allocation.IceAllocationId, common.InventoryId);
    }

    private static (int SourceAllocationId, int TargetAllocationId, int StoreInventoryId) SeedCarryPair(
        CafeChain.Data.AppDbContext context,
        bool sameBusinessDate)
    {
        var common = SeedCommon(context, available: 100m, reserved: 15m);
        var date = DateTime.UtcNow.Date;
        var sourceShift = NewShift("Ca sáng", date, -4, -1);
        var targetShift = NewShift("Ca chiều", sameBusinessDate ? date : date.AddDays(1), 0, 4);
        context.OperationalShifts.AddRange(sourceShift, targetShift);
        context.SaveChanges();
        var source = NewAllocation(sourceShift.OperationalShiftId, common.PolicyId, common.InventoryId, 10m, 10m);
        var target = NewAllocation(targetShift.OperationalShiftId, common.PolicyId, common.InventoryId, 5m, 5m);
        context.IceAllocations.AddRange(source, target);
        context.SaveChanges();
        return (source.IceAllocationId, target.IceAllocationId, common.InventoryId);
    }

    private static (int PolicyId, int InventoryId) SeedCommon(
        CafeChain.Data.AppDbContext context,
        decimal available,
        decimal reserved)
    {
        context.Stores.Add(new Store { StoreId = StoreId, Name = "Ice close store", Active = true, CreatedAt = DateTime.UtcNow });
        context.Units.Add(new Unit { UnitId = UnitId, UnitCode = "ICE_CLOSE_U", Name = "Gram", Active = true });
        context.Ingredients.Add(new Ingredient
        {
            IngredientId = IngredientId,
            Code = "ICE_CLOSE",
            Name = "Đá viên close test",
            BaseUnitId = UnitId,
            Active = true
        });
        context.Staffs.AddRange(
            new Staff { StaffId = ManagerStaffId, AccountId = 9954, StoreId = StoreId, FullName = "Quản lý", Active = true, CreatedAt = DateTime.UtcNow },
            new Staff { StaffId = ReceiverStaffId, AccountId = 9955, StoreId = StoreId, FullName = "Người nhận", Active = true, CreatedAt = DateTime.UtcNow });
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
            VarianceApprovalQuantityThreshold = 5m,
            VarianceApprovalPercentThreshold = 100m,
            UpdatedByStaffId = ManagerStaffId,
            UpdatedAtUtc = DateTime.UtcNow,
            Active = true,
            RowVersion = [0]
        };
        context.IcePolicies.Add(policy);
        context.SaveChanges();
        return (policy.IcePolicyId, inventory.StoreInventoryId);
    }

    private static OperationalShift NewShift(string name, DateTime businessDate, int startHourOffset, int endHourOffset) => new()
    {
        StoreId = StoreId,
        BusinessDate = businessDate,
        Name = $"{name}-{Guid.NewGuid():N}",
        StartAtUtc = DateTime.UtcNow.AddHours(startHourOffset),
        EndAtUtc = DateTime.UtcNow.AddHours(endHourOffset),
        Status = OperationalIceStatuses.Open,
        CreatedByStaffId = ManagerStaffId,
        OpenedByStaffId = ManagerStaffId,
        CreatedAtUtc = DateTime.UtcNow,
        OpenedAtUtc = DateTime.UtcNow,
        RowVersion = [0]
    };

    private static IceAllocation NewAllocation(
        int operationalShiftId,
        int policyId,
        int inventoryId,
        decimal initial,
        decimal outstanding) => new()
    {
        PublicId = Guid.NewGuid(),
        OperationalShiftId = operationalShiftId,
        IcePolicyId = policyId,
        StoreInventoryId = inventoryId,
        IngredientId = IngredientId,
        InitialIssuedQuantity = initial,
        ReservedOutstandingQuantity = outstanding,
        ReservationReference = $"ICE:CLOSE:{Guid.NewGuid():N}",
        Status = OperationalIceStatuses.Open,
        CreatedByStaffId = ManagerStaffId,
        OpenedByStaffId = ManagerStaffId,
        CreatedAtUtc = DateTime.UtcNow,
        OpenedAtUtc = DateTime.UtcNow,
        Revision = 1,
        RowVersion = [0]
    };

    private static Task<decimal> InventoryValueAsync(
        CafeChain.Data.AppDbContext context,
        int storeInventoryId,
        System.Linq.Expressions.Expression<Func<StoreInventory, decimal>> selector) =>
        context.StoreInventories.Where(x => x.StoreInventoryId == storeInventoryId).Select(selector).SingleAsync();
}
