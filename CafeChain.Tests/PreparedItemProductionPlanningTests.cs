using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Permissions;
using CafeChain.Application.DTOs.Admin.Production;
using CafeChain.Application.DTOs.Admin.RestockRequests;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.Admin.Production;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Admin.StoreInventories;
using CafeChain.Application.Services.Inventories;
using CafeChain.Models.Customers;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Permissions;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CafeChain.Tests;

public sealed class PreparedItemProductionPlanningTests : IntegrationTestBase
{
    private const int AccountId = 8301;
    private const int StaffId = 8302;
    private const int StoreId = 8303;
    private const int PreparedItemId = 8304;
    private const int UnitId = 8305;
    private const int RecipeV1 = 8306;
    private const int RecipeV2 = 8307;

    [Fact]
    public async Task ProductionPlan_RequestKey_IsIdempotent()
    {
        using var context = CreateDbContext();
        var demandId = await SeedAsync(context);
        var service = CreateService(context, RecipeV1);
        var key = Guid.NewGuid();
        var command = Command(demandId, key, 6m);

        var first = await service.SetSourcingDecisionAsync(command, StaffId);
        var replay = await service.SetSourcingDecisionAsync(command, StaffId);

        Assert.True(first.IsSuccess, first.Message);
        Assert.True(replay.IsSuccess, replay.Message);
        Assert.Equal(first.Data.RestockSourcingAllocationId, replay.Data.RestockSourcingAllocationId);
        Assert.Single(context.ProductionRuns);
        Assert.Single(context.RestockSourcingAllocations);
    }

    [Fact]
    public async Task RecipeChangeBeforePlan_UsesNewCurrentRecipe()
    {
        using var context = CreateDbContext();
        var demandId = await SeedAsync(context);

        var result = await CreateService(context, RecipeV2)
            .SetSourcingDecisionAsync(Command(demandId, Guid.NewGuid(), 6m), StaffId);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(RecipeV2, (await context.ProductionRuns.SingleAsync()).RecipeId);
    }

    [Fact]
    public async Task RecipeChangeAfterPlan_DoesNotSwitchPinnedRecipe()
    {
        using var context = CreateDbContext();
        var demandId = await SeedAsync(context);
        var result = await CreateService(context, RecipeV1)
            .SetSourcingDecisionAsync(Command(demandId, Guid.NewGuid(), 6m), StaffId);
        Assert.True(result.IsSuccess, result.Message);

        context.Recipes.Single(x => x.RecipeId == RecipeV1).Status = "Archived";
        context.Recipes.Single(x => x.RecipeId == RecipeV2).Status = "Active";
        await context.SaveChangesAsync();

        var run = await context.ProductionRuns.AsNoTracking().SingleAsync();
        Assert.Equal(RecipeV1, run.RecipeId);
    }

    [Fact]
    public async Task ContinuedConsumption_DoesNotMutateOpenRun()
    {
        using var context = CreateDbContext();
        var demandId = await SeedAsync(context);
        var service = CreateService(context, RecipeV1);
        var result = await service.SetSourcingDecisionAsync(
            Command(demandId, Guid.NewGuid(), 6m), StaffId);
        Assert.True(result.IsSuccess, result.Message);
        var before = await context.ProductionRuns.AsNoTracking().SingleAsync();

        var stock = await context.StoreInventories
            .SingleAsync(x => x.StoreId == StoreId && x.PreparedItemId == PreparedItemId);
        stock.AvailableQty = 1m;
        stock.LastUpdated = DateTime.UtcNow;
        await context.SaveChangesAsync();

        var after = await context.ProductionRuns.AsNoTracking().SingleAsync();
        Assert.Equal(before.RecipeId, after.RecipeId);
        Assert.Equal(before.PlannedBatchCount, after.PlannedBatchCount);
        Assert.Equal(before.ExpectedOutputBase, after.ExpectedOutputBase);
        Assert.Equal(ProductionRunStatus.Planned, after.Status);
    }

    [Fact]
    public async Task ContinuedConsumption_AdjustsDemandWithoutMutatingOpenRun()
    {
        using var context = CreateDbContext();
        var demandId = await SeedAsync(context);
        var service = CreateService(context, RecipeV1);
        var planned = await service.SetSourcingDecisionAsync(
            Command(demandId, Guid.NewGuid(), 6m), StaffId);
        Assert.True(planned.IsSuccess, planned.Message);
        var runBefore = await context.ProductionRuns.AsNoTracking().SingleAsync();

        var stock = await context.StoreInventories
            .SingleAsync(x => x.StoreId == StoreId && x.PreparedItemId == PreparedItemId);
        stock.AvailableQty = 1m;
        await context.SaveChangesAsync();
        var demand = await context.RestockRequests.AsNoTracking().SingleAsync();
        var adjusted = await service.AddDemandAdjustmentAsync(new AddRestockDemandAdjustmentRequest
        {
            RestockRequestId = demandId,
            AdjustmentProcurementQuantity = 1m,
            ProcurementUnitId = UnitId,
            Reason = "Tiêu thụ tiếp trong lúc đang sản xuất",
            RowVersion = Convert.ToBase64String(demand.RowVersion),
            RequestKey = Guid.NewGuid().ToString("N")
        }, StaffId);

        Assert.True(adjusted.IsSuccess, adjusted.Message);
        Assert.Equal(7m, adjusted.Data!.QuantityAfter);
        var runAfter = await context.ProductionRuns.AsNoTracking().SingleAsync();
        Assert.Equal(runBefore.RecipeId, runAfter.RecipeId);
        Assert.Equal(runBefore.PlannedBatchCount, runAfter.PlannedBatchCount);
        Assert.Equal(runBefore.ExpectedOutputBase, runAfter.ExpectedOutputBase);
    }

    [Fact]
    public async Task RecipeChangeDuringPlan_RevalidatesBeforePin()
    {
        using var context = CreateDbContext();
        var demandId = await SeedAsync(context);
        var eligibility = new Mock<IProductionSourceEligibilityService>();
        eligibility.SetupSequence(x => x.EvaluateAsync(It.IsAny<ProductionSourceEligibilityRequest>()))
            .ReturnsAsync(Eligible(RecipeV1))
            .ReturnsAsync(Eligible(RecipeV2));

        var result = await CreateService(context, RecipeV1, eligibility.Object)
            .SetSourcingDecisionAsync(Command(demandId, Guid.NewGuid(), 6m), StaffId);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(RecipeV2, (await context.ProductionRuns.SingleAsync()).RecipeId);
    }

    [Fact]
    public async Task CurrentNeed_BoundsProductionAllocation()
    {
        using var context = CreateDbContext();
        var demandId = await SeedAsync(context);

        var result = await CreateService(context, RecipeV1)
            .SetSourcingDecisionAsync(Command(demandId, Guid.NewGuid(), 7m), StaffId);

        Assert.False(result.IsSuccess);
        Assert.Contains("vượt", result.Message);
        Assert.Empty(context.ProductionRuns);
    }

    [Fact]
    public async Task UnauthorizedStore_CannotPlanReplenishment()
    {
        using var context = CreateDbContext();
        var demandId = await SeedAsync(context);
        var eligibility = new Mock<IProductionSourceEligibilityService>();
        eligibility
            .Setup(x => x.EvaluateAsync(It.IsAny<ProductionSourceEligibilityRequest>()))
            .ReturnsAsync(ServiceResult<ProductionSourceEligibilityDto>.Success(new ProductionSourceEligibilityDto
            {
                Eligible = false,
                ReasonCode = ProductionEligibilityReasonCodes.PermissionDenied,
                Message = "Bạn không có quyền lập kế hoạch sản xuất tại chi nhánh này."
            }));

        var result = await CreateService(context, RecipeV1, eligibility.Object)
            .SetSourcingDecisionAsync(Command(demandId, Guid.NewGuid(), 6m), StaffId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ProductionEligibilityReasonCodes.PermissionDenied, result.ErrorCode);
        Assert.Empty(context.ProductionRuns);
    }

    [Fact]
    public async Task PreparedItemWithoutPurchaseContract_DoesNotFallbackToPO()
    {
        using var context = CreateDbContext();
        var demandId = await SeedAsync(context);
        var purchase = new Mock<IPurchaseSourceEligibilityService>();
        purchase
            .Setup(x => x.EvaluateAsync(It.IsAny<PurchaseSourceEligibilityRequest>()))
            .ReturnsAsync(ServiceResult<PurchaseSourceEligibilityDto>.Success(new PurchaseSourceEligibilityDto
            {
                Eligible = false,
                ReasonCode = PurchaseEligibilityReasonCodes.PackageMissing,
                Message = "Bán thành phẩm chưa có hợp đồng mua ngoài được xác nhận."
            }));

        var result = await CreateService(context, RecipeV1, purchaseEligibility: purchase.Object)
            .SetSourcingDecisionAsync(new SourcingDecisionRequest
            {
                RestockRequestId = demandId,
                DecisionType = RestockSourcingDecisionTypes.Purchase,
                ProcurementQuantity = 6m,
                ProcurementUnitId = UnitId
            }, StaffId);

        Assert.False(result.IsSuccess);
        Assert.Empty(context.PurchaseOrders);
        Assert.Empty(context.RestockSourcingAllocations);
    }

    private static RestockRequestService CreateService(
        CafeChain.Data.AppDbContext context,
        int recipeId,
        IProductionSourceEligibilityService? eligibility = null,
        IPurchaseSourceEligibilityService? purchaseEligibility = null)
    {
        var permissions = new Mock<IAdminPermissionService>();
        permissions
            .Setup(x => x.HasPermissionAsync(AccountId, PermissionConstants.InventoryThresholdView, StoreId))
            .ReturnsAsync(ServiceResult<PermissionDecisionDto>.Success(new PermissionDecisionDto
            {
                AccountId = AccountId,
                PermissionCode = PermissionConstants.InventoryThresholdView,
                TargetStoreId = StoreId,
                Allowed = true,
                ScopeAllowed = true
            }));
        permissions
            .Setup(x => x.HasPermissionAsync(AccountId, PermissionConstants.StockAlertCreateRestockRequest, StoreId))
            .ReturnsAsync(ServiceResult<PermissionDecisionDto>.Success(new PermissionDecisionDto
            {
                AccountId = AccountId,
                PermissionCode = PermissionConstants.StockAlertCreateRestockRequest,
                TargetStoreId = StoreId,
                Allowed = true,
                ScopeAllowed = true
            }));
        var read = new PreparedItemReplenishmentReadService(context, permissions.Object);

        if (eligibility == null)
        {
            var mock = new Mock<IProductionSourceEligibilityService>();
            mock.Setup(x => x.EvaluateAsync(It.IsAny<ProductionSourceEligibilityRequest>()))
                .ReturnsAsync(ServiceResult<ProductionSourceEligibilityDto>.Success(new ProductionSourceEligibilityDto
                {
                    Eligible = true,
                    ReasonCode = ProductionEligibilityReasonCodes.Eligible,
                    Message = "Có thể lập kế hoạch sản xuất.",
                    StoreId = StoreId,
                    PreparedItemId = PreparedItemId,
                    RecipeId = recipeId,
                    ExpectedOutputPerBatchBase = 5m,
                    OutputBaseUnitId = UnitId,
                    OutputBaseUnitCode = "r4-litre"
                }));
            eligibility = mock.Object;
        }

        return new RestockRequestService(
            context,
            new Mock<IScopeAuthorizationService>().Object,
            new Mock<ILogger<RestockRequestService>>().Object,
            productionEligibility: eligibility,
            purchaseEligibility: purchaseEligibility,
            preparedItemReplenishment: read,
            permissions: permissions.Object);
    }

    private static SourcingDecisionRequest Command(int demandId, Guid key, decimal quantity) => new()
    {
        RestockRequestId = demandId,
        DecisionType = RestockSourcingDecisionTypes.Production,
        ProcurementQuantity = quantity,
        ProcurementUnitId = UnitId,
        RequestKey = key,
        Reason = "Bổ sung cốt trà"
    };

    private static ServiceResult<ProductionSourceEligibilityDto> Eligible(int recipeId) =>
        ServiceResult<ProductionSourceEligibilityDto>.Success(new ProductionSourceEligibilityDto
        {
            Eligible = true,
            ReasonCode = ProductionEligibilityReasonCodes.Eligible,
            Message = "Có thể lập kế hoạch sản xuất.",
            StoreId = StoreId,
            PreparedItemId = PreparedItemId,
            RecipeId = recipeId,
            ExpectedOutputPerBatchBase = 5m,
            OutputBaseUnitId = UnitId,
            OutputBaseUnitCode = "r4-litre"
        });

    private static async Task<int> SeedAsync(CafeChain.Data.AppDbContext context)
    {
        context.Units.Add(new Unit
        {
            UnitId = UnitId,
            UnitCode = "r4-litre",
            Name = "Lít",
            Type = UnitType.TheTich,
            Active = true
        });
        context.Stores.Add(new Store
        {
            StoreId = StoreId,
            Name = "Chi nhánh R4",
            Address = "Kiểm thử",
            Phone = "000",
            Active = true,
            CreatedAt = DateTime.UtcNow
        });
        context.PreparedItems.Add(new PreparedItem
        {
            PreparedItemId = PreparedItemId,
            Code = "BTP-R4",
            Name = "Cốt trà R4",
            BaseUnitId = UnitId,
            Active = true
        });
        context.Recipes.AddRange(
            new Recipe
            {
                RecipeId = RecipeV1,
                RecipeCode = "RCP-R4-V1",
                Name = "Công thức R4 v1",
                PreparedItemId = PreparedItemId,
                OutputQuantity = 5m,
                OutputUnitId = UnitId,
                Active = true,
                Status = "Active"
            },
            new Recipe
            {
                RecipeId = RecipeV2,
                RecipeCode = "RCP-R4-V2",
                Name = "Công thức R4 v2",
                PreparedItemId = PreparedItemId,
                OutputQuantity = 5m,
                OutputUnitId = UnitId,
                Active = false,
                Status = "Archived"
            });
        context.StoreInventories.Add(new StoreInventory
        {
            StoreId = StoreId,
            PreparedItemId = PreparedItemId,
            BtpIdentityState = BtpIdentityState.Canonical,
            QuantitySemanticsStatus = InventoryQuantitySemanticsStatus.BaseUnitConfirmed,
            AvailableQty = 2m,
            ReservedQty = 0m,
            MinStockLevel = 3m,
            TargetStockLevel = 8m,
            LastUpdated = DateTime.UtcNow,
            RowVersion = [0]
        });

        var account = new Account
        {
            AccountId = AccountId,
            Email = "r4-manager@test.local",
            PasswordHash = "x",
            Active = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Accounts.Add(account);
        context.AccountRoles.Add(new AccountRole { AccountId = AccountId, RoleId = 3 });
        context.Staffs.Add(new Staff
        {
            StaffId = StaffId,
            AccountId = AccountId,
            StoreId = StoreId,
            FullName = "Quản lý R4",
            Active = true,
            CreatedAt = DateTime.UtcNow
        });

        var demand = new RestockRequest
        {
            StoreId = StoreId,
            PreparedItemId = PreparedItemId,
            RequestedQuantity = 6m,
            RequestedProcurementQuantity = 6m,
            ProcurementUnitId = UnitId,
            TargetStockProcurementQuantity = 8m,
            Status = RestockRequestStatuses.Processing,
            Priority = RestockRequestPriorities.High,
            SourceType = RestockRequestSourceTypes.StockAlert,
            SourcingStatus = RestockSourcingStatuses.Unallocated,
            CreatedByStaffId = StaffId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RowVersion = [0]
        };
        context.RestockRequests.Add(demand);
        await context.SaveChangesAsync();
        return demand.RestockRequestId;
    }
}
