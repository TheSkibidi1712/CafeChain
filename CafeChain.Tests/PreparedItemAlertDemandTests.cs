using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Permissions;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Admin.StoreInventories;
using CafeChain.Application.Services.Inventories;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CafeChain.Tests;

public sealed class PreparedItemAlertDemandTests : IntegrationTestBase
{
    private const int AccountId = 8201;
    private const int StaffId = 8202;
    private const int StoreId = 8203;
    private const int PreparedItemId = 8204;
    private const int UnitId = 8205;

    [Fact]
    public async Task PreparedItemSnapshot_UsesBaseUom()
    {
        using var context = CreateDbContext();
        var alertId = await SeedAsync(context, target: 8m);
        var service = CreateService(context);
        var stockVersion = await StockVersionAsync(context);

        var result = await service.CreatePreparedItemDemandFromConfirmedAlertAsync(
            alertId, StaffId, AccountId, StoreId, stockVersion, null, "HIGH");

        Assert.True(result.IsSuccess);
        var demand = await context.RestockRequests.SingleAsync();
        Assert.Equal(PreparedItemId, demand.PreparedItemId);
        Assert.Null(demand.RecipeId);
        Assert.Equal(UnitId, demand.ProcurementUnitId);
        Assert.Equal(6m, demand.RequestedQuantity);
        Assert.Equal(6m, demand.RequestedProcurementQuantity);
        Assert.Equal(2m, demand.SuggestionAvailableSnapshot);
        Assert.Equal(3m, demand.SuggestionMinLevelSnapshot);
        Assert.Equal(8m, demand.TargetStockProcurementQuantity);
    }

    [Fact]
    public async Task MissingTarget_BlocksDemandQuantity()
    {
        using var context = CreateDbContext();
        var alertId = await SeedAsync(context, target: null);

        var result = await CreateService(context).CreatePreparedItemDemandFromConfirmedAlertAsync(
            alertId, StaffId, AccountId, StoreId, await StockVersionAsync(context), null, null);

        Assert.False(result.IsSuccess);
        Assert.Contains("mức tồn mục tiêu", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(context.RestockRequests);
    }

    [Fact]
    public async Task ExistingActiveRequest_IsReused()
    {
        using var context = CreateDbContext();
        var alertId = await SeedAsync(context, target: 8m);
        var existing = await SeedExistingRequestAsync(context, alertId);

        var result = await CreateService(context).CreatePreparedItemDemandFromConfirmedAlertAsync(
            alertId, StaffId, AccountId, StoreId, "stale-is-ignored-for-existing", null, null);

        Assert.True(result.IsSuccess);
        Assert.True(result.Data.AlreadyExisted);
        Assert.Equal(existing, result.Data.RestockRequestId);
        Assert.Single(context.RestockRequests);
    }

    [Fact]
    public async Task BrowserQuantity_IsNotDemandAuthority()
    {
        using var context = CreateDbContext();
        var alertId = await SeedAsync(context, target: 8m);

        var result = await CreateService(context).CreatePreparedItemDemandFromConfirmedAlertAsync(
            alertId, StaffId, AccountId, StoreId, await StockVersionAsync(context), null, null);

        Assert.True(result.IsSuccess);
        Assert.Equal(6m, (await context.RestockRequests.SingleAsync()).RequestedQuantity);
    }

    [Fact]
    public async Task PreparedItemWithoutPurchaseContract_DoesNotCreatePO()
    {
        using var context = CreateDbContext();
        var alertId = await SeedAsync(context, target: 8m);

        var result = await CreateService(context).CreatePreparedItemDemandFromConfirmedAlertAsync(
            alertId, StaffId, AccountId, StoreId, await StockVersionAsync(context), null, null);

        Assert.True(result.IsSuccess);
        var demand = await context.RestockRequests.SingleAsync();
        Assert.Null(demand.SourcingDecision);
        Assert.Equal(RestockSourcingStatuses.Unallocated, demand.SourcingStatus);
        Assert.Empty(context.RestockSourcingAllocations);
        Assert.Empty(context.PurchaseOrders);
    }

    [Fact]
    public async Task StaleStockVersion_BlocksDemandCreation()
    {
        using var context = CreateDbContext();
        var alertId = await SeedAsync(context, target: 8m);

        var result = await CreateService(context).CreatePreparedItemDemandFromConfirmedAlertAsync(
            alertId, StaffId, AccountId, StoreId, Convert.ToBase64String([9]), null, null);

        Assert.False(result.IsSuccess);
        Assert.Equal("PREPARED_ITEM_STOCK_CHANGED", result.ErrorCode);
        Assert.Empty(context.RestockRequests);
    }

    [Fact]
    public void ThresholdUi_ConsumesStrictProjectedLowState()
    {
        var root = FindRepositoryRoot();
        var view = File.ReadAllText(Path.Combine(
            root, "CafeChain", "Areas", "Admin", "Views", "AdminInventoryThresholds", "Index.cshtml"));

        Assert.Contains("var isLow = item.IsLow;", view);
        Assert.DoesNotContain("UsableQty <= item.MinStockLevel", view);
        Assert.Contains("Mức tồn mục tiêu", view);
        Assert.Contains("Ngưỡng cảnh báo tồn thấp", view);
    }

    private static RestockRequestService CreateService(CafeChain.Data.AppDbContext context)
    {
        var permissions = new Mock<IAdminPermissionService>(MockBehavior.Strict);
        permissions
            .Setup(x => x.HasPermissionAsync(
                AccountId,
                It.Is<string>(p => p == PermissionConstants.InventoryThresholdView
                    || p == PermissionConstants.StockAlertCreateRestockRequest),
                StoreId))
            .ReturnsAsync((int _, string permission, int? _) =>
                ServiceResult<PermissionDecisionDto>.Success(new PermissionDecisionDto
                {
                    AccountId = AccountId,
                    PermissionCode = permission,
                    TargetStoreId = StoreId,
                    Allowed = true,
                    ScopeAllowed = true
                }));

        var readService = new PreparedItemReplenishmentReadService(context, permissions.Object);
        return new RestockRequestService(
            context,
            new Mock<IScopeAuthorizationService>(MockBehavior.Loose).Object,
            new Mock<ILogger<RestockRequestService>>().Object,
            preparedItemReplenishment: readService,
            permissions: permissions.Object);
    }

    private static async Task<int> SeedAsync(CafeChain.Data.AppDbContext context, decimal? target)
    {
        context.Units.Add(new Unit
        {
            UnitId = UnitId,
            UnitCode = "r3-litre",
            Name = "Lít",
            Type = UnitType.TheTich,
            Active = true
        });
        context.Stores.Add(new Store
        {
            StoreId = StoreId,
            Name = "Chi nhánh R3",
            Address = "Kiểm thử",
            Phone = "000",
            Active = true,
            CreatedAt = DateTime.UtcNow
        });
        context.PreparedItems.Add(new PreparedItem
        {
            PreparedItemId = PreparedItemId,
            Code = "BTP-R3",
            Name = "Cốt trà R3",
            BaseUnitId = UnitId,
            Active = true
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
            TargetStockLevel = target,
            LastUpdated = DateTime.UtcNow,
            RowVersion = [0]
        });
        var alert = new StockAlert
        {
            StoreId = StoreId,
            PreparedItemId = PreparedItemId,
            AlertType = StockAlertTypes.LowStock,
            Severity = StockAlertSeverities.Warning,
            Status = StockAlertStatuses.Confirmed,
            CurrentQtySnapshot = 2m,
            ThresholdSnapshot = 3m,
            Source = StockAlertSources.Auto,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RowVersion = [0]
        };
        context.StockAlerts.Add(alert);
        await context.SaveChangesAsync();
        return alert.StockAlertId;
    }

    private static async Task<int> SeedExistingRequestAsync(
        CafeChain.Data.AppDbContext context,
        int alertId)
    {
        var request = new RestockRequest
        {
            StockAlertId = alertId,
            StoreId = StoreId,
            PreparedItemId = PreparedItemId,
            RequestedQuantity = 6m,
            Status = RestockRequestStatuses.Draft,
            Priority = RestockRequestPriorities.Normal,
            CreatedByStaffId = StaffId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RowVersion = [0]
        };
        context.RestockRequests.Add(request);
        await context.SaveChangesAsync();
        return request.RestockRequestId;
    }

    private static async Task<string> StockVersionAsync(CafeChain.Data.AppDbContext context) =>
        Convert.ToBase64String(await context.StoreInventories
            .AsNoTracking()
            .Where(x => x.PreparedItemId == PreparedItemId)
            .Select(x => x.RowVersion)
            .SingleAsync());

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Không tìm thấy thư mục repository.");
    }
}
