using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Permissions;
using CafeChain.Application.DTOs.Admin.Replenishment;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Admin.StoreInventories;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Inventories.Production;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CafeChain.Tests;

public sealed class PreparedItemReplenishmentReadModelTests : IntegrationTestBase
{
    private const int AccountId = 8101;
    private const int StoreId = 8102;
    private const int PreparedItemId = 8103;
    private const int BaseUnitId = 8104;

    [Fact]
    public async Task CanonicalPreparedItem_AppearsInThresholdProjection()
    {
        using var context = CreateDbContext();
        await SeedStockAsync(context, available: 2m, reserved: 0.5m, low: 3m, target: 8m);

        var result = await CreateService(context).GetAsync(AccountId, StoreId, PreparedItemId);

        Assert.True(result.IsSuccess);
        Assert.Equal("Cốt trà đen", result.Data.PreparedItemName);
        Assert.Equal("BTP-BLACK-TEA", result.Data.PreparedItemCode);
        Assert.Equal(1.5m, result.Data.UsableBase);
        Assert.True(result.Data.IsLow);
        Assert.Equal(6.5m, result.Data.GrossNeedBase);
        Assert.Equal(6.5m, result.Data.NetNeedBase);
    }

    [Theory]
    [InlineData(3, false)]
    [InlineData(2.999, true)]
    [InlineData(3.001, false)]
    public async Task ThresholdBoundary_UsesStrictLessThan(decimal usable, bool expectedLow)
    {
        using var context = CreateDbContext();
        await SeedStockAsync(context, available: usable, reserved: 0m, low: 3m, target: 8m);

        var result = await CreateService(context).GetAsync(AccountId, StoreId, PreparedItemId);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedLow, result.Data.IsLow);
    }

    [Fact]
    public async Task ExactlyAtThreshold_IsNotLow()
    {
        using var context = CreateDbContext();
        await SeedStockAsync(context, available: 4m, reserved: 1m, low: 3m, target: 8m);

        var result = await CreateService(context).GetAsync(AccountId, StoreId, PreparedItemId);

        Assert.True(result.IsSuccess);
        Assert.False(result.Data.IsLow);
    }

    [Fact]
    public async Task MissingTarget_DoesNotInventSuggestedQuantity()
    {
        using var context = CreateDbContext();
        await SeedStockAsync(context, available: 2m, reserved: 0m, low: 3m, target: null);

        var result = await CreateService(context).GetAsync(AccountId, StoreId, PreparedItemId);

        Assert.True(result.IsSuccess);
        Assert.True(result.Data.IsLow);
        Assert.Null(result.Data.GrossNeedBase);
        Assert.Null(result.Data.NetNeedBase);
        Assert.Equal(PreparedItemReplenishmentDataStatuses.TargetNotConfigured, result.Data.DataStatus);
        Assert.Contains("mức tồn mục tiêu", result.Data.BusinessMessageVi, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MissingLowThreshold_IsNotPresentedAsReady()
    {
        using var context = CreateDbContext();
        await SeedStockAsync(context, available: 2m, reserved: 0m, low: null, target: 8m);

        var result = await CreateService(context).GetAsync(AccountId, StoreId, PreparedItemId);

        Assert.True(result.IsSuccess);
        Assert.False(result.Data.IsLow);
        Assert.Equal(
            PreparedItemReplenishmentDataStatuses.LowThresholdNotConfigured,
            result.Data.DataStatus);
        Assert.Contains("ngưỡng cảnh báo", result.Data.BusinessMessageVi, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NetNeed_SubtractsOnlyNonTerminalProductionCoverage()
    {
        using var context = CreateDbContext();
        await SeedStockAsync(context, available: 2m, reserved: 0m, low: 3m, target: 8m);
        var requestId = await SeedRequestAsync(context);
        await SeedAllocationAsync(context, requestId, ProductionRunStatus.Planned, 2m);
        await SeedAllocationAsync(context, requestId, ProductionRunStatus.InProgress, 1m);
        await SeedAllocationAsync(context, requestId, ProductionRunStatus.Completed, 7m);
        await SeedAllocationAsync(context, requestId, ProductionRunStatus.Cancelled, 9m);

        var result = await CreateService(context).GetAsync(AccountId, StoreId, PreparedItemId);

        Assert.True(result.IsSuccess);
        Assert.Equal(3m, result.Data.OpenProductionCoverageBase);
        Assert.Equal(3m, result.Data.NetNeedBase);
        Assert.Equal(2, result.Data.OpenProductionRunTotal);
    }

    [Fact]
    public async Task CancelledRun_DoesNotCountAsCoverage()
    {
        using var context = CreateDbContext();
        await SeedStockAsync(context, available: 2m, reserved: 0m, low: 3m, target: 8m);
        var requestId = await SeedRequestAsync(context);
        await SeedAllocationAsync(context, requestId, ProductionRunStatus.Cancelled, 6m);

        var result = await CreateService(context).GetAsync(AccountId, StoreId, PreparedItemId);

        Assert.True(result.IsSuccess);
        Assert.Equal(0m, result.Data.OpenProductionCoverageBase);
        Assert.Equal(6m, result.Data.NetNeedBase);
        Assert.Empty(result.Data.OpenProductionRuns);
    }

    [Fact]
    public async Task OpenCoverage_IsNeverPresentedAsInventory()
    {
        using var context = CreateDbContext();
        await SeedStockAsync(context, available: 2m, reserved: 0m, low: 3m, target: 8m);
        var requestId = await SeedRequestAsync(context);
        await SeedAllocationAsync(context, requestId, ProductionRunStatus.Released, 5m);

        var result = await CreateService(context).GetAsync(AccountId, StoreId, PreparedItemId);

        Assert.True(result.IsSuccess);
        Assert.Equal(2m, result.Data.OnHandBase);
        Assert.Equal(5m, result.Data.OpenProductionCoverageBase);
        Assert.Equal(1m, result.Data.NetNeedBase);
    }

    [Fact]
    public async Task OpenProductionRuns_AreBoundedAndReportHasMore()
    {
        using var context = CreateDbContext();
        await SeedStockAsync(context, available: 2m, reserved: 0m, low: 3m, target: 12m);
        var requestId = await SeedRequestAsync(context);
        await SeedAllocationAsync(context, requestId, ProductionRunStatus.Planned, 1m);
        await SeedAllocationAsync(context, requestId, ProductionRunStatus.Released, 1m);
        await SeedAllocationAsync(context, requestId, ProductionRunStatus.InProgress, 1m);

        var result = await CreateService(context).GetAsync(AccountId, StoreId, PreparedItemId, openRunLimit: 2);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Data.OpenProductionRunTotal);
        Assert.Equal(2, result.Data.OpenProductionRuns.Count);
        Assert.True(result.Data.HasMoreOpenProductionRuns);
    }

    [Fact]
    public async Task UnauthorizedStore_CannotReadReplenishment()
    {
        using var context = CreateDbContext();
        await SeedStockAsync(context, available: 2m, reserved: 0m, low: 3m, target: 8m);

        var result = await CreateService(context, allowed: false)
            .GetAsync(AccountId, StoreId, PreparedItemId);

        Assert.False(result.IsSuccess);
        Assert.Contains("quyền", result.Message);
    }

    private static PreparedItemReplenishmentReadService CreateService(
        CafeChain.Data.AppDbContext context,
        bool allowed = true)
    {
        var permissions = new Mock<IAdminPermissionService>(MockBehavior.Strict);
        permissions
            .Setup(x => x.HasPermissionAsync(AccountId, PermissionConstants.InventoryThresholdView, StoreId))
            .ReturnsAsync(ServiceResult<PermissionDecisionDto>.Success(new PermissionDecisionDto
            {
                AccountId = AccountId,
                PermissionCode = PermissionConstants.InventoryThresholdView,
                TargetStoreId = StoreId,
                Allowed = allowed,
                ScopeAllowed = allowed
            }));

        return new PreparedItemReplenishmentReadService(context, permissions.Object);
    }

    private static async Task SeedStockAsync(
        CafeChain.Data.AppDbContext context,
        decimal available,
        decimal reserved,
        decimal? low,
        decimal? target)
    {
        context.Units.Add(new Unit
        {
            UnitId = BaseUnitId,
            UnitCode = "r2-litre",
            Name = "Lít",
            Type = UnitType.TheTich,
            Active = true
        });
        context.Stores.Add(new Store
        {
            StoreId = StoreId,
            Name = "CafeChain Thủ Dầu Một",
            Address = "Kiểm thử",
            Phone = "000",
            Active = true,
            CreatedAt = DateTime.UtcNow
        });
        context.PreparedItems.Add(new PreparedItem
        {
            PreparedItemId = PreparedItemId,
            Code = "BTP-BLACK-TEA",
            Name = "Cốt trà đen",
            BaseUnitId = BaseUnitId,
            Active = true
        });
        context.StoreInventories.Add(new StoreInventory
        {
            StoreId = StoreId,
            PreparedItemId = PreparedItemId,
            BtpIdentityState = BtpIdentityState.Canonical,
            QuantitySemanticsStatus = InventoryQuantitySemanticsStatus.BaseUnitConfirmed,
            AvailableQty = available,
            ReservedQty = reserved,
            MinStockLevel = low,
            TargetStockLevel = target,
            LastUpdated = DateTime.UtcNow,
            RowVersion = [0]
        });
        await context.SaveChangesAsync();
    }

    private static async Task<int> SeedRequestAsync(CafeChain.Data.AppDbContext context)
    {
        var request = new RestockRequest
        {
            StoreId = StoreId,
            PreparedItemId = PreparedItemId,
            RequestedQuantity = 10m,
            Status = RestockRequestStatuses.Processing,
            Priority = RestockRequestPriorities.Normal,
            CreatedByStaffId = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RowVersion = [0]
        };
        context.RestockRequests.Add(request);
        await context.SaveChangesAsync();
        return request.RestockRequestId;
    }

    private static async Task SeedAllocationAsync(
        CafeChain.Data.AppDbContext context,
        int requestId,
        ProductionRunStatus status,
        decimal quantity)
    {
        var run = new ProductionRun
        {
            StoreId = StoreId,
            RecipeId = 9000 + (int)status + context.ProductionRuns.Local.Count,
            RequestedRunCount = 1m,
            RequestKey = Guid.NewGuid(),
            RequestFingerprint = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
            Status = status,
            CreatedByStaffId = 1,
            CreatedAt = DateTime.UtcNow.AddMinutes(context.ProductionRuns.Local.Count),
            ConfirmedAt = DateTime.UtcNow,
            RowVersion = [0]
        };
        context.ProductionRuns.Add(run);
        await context.SaveChangesAsync();

        context.RestockSourcingAllocations.Add(new RestockSourcingAllocation
        {
            RestockRequestId = requestId,
            DecisionType = RestockSourcingDecisionTypes.Production,
            ProcurementQuantity = quantity,
            ProcurementUnitId = BaseUnitId,
            Status = RestockSourcingAllocationStatuses.Active,
            ProductionRunId = run.ProductionRunId,
            CreatedByStaffId = 1,
            CreatedAtUtc = DateTime.UtcNow,
            RowVersion = [0]
        });
        await context.SaveChangesAsync();
    }
}
