using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Permissions;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Admin.Production;
using CafeChain.Application.Services.Admin.StoreInventories;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CafeChain.Tests;

public sealed class PreparedItemProductionCapabilityConfigurationTests : IntegrationTestBase
{
    [Fact]
    public async Task StoreCapability_EnableIsExplicitAndBootstrapsOnlySelectedStore()
    {
        await using var context = CreateDbContext();
        await SeedAsync(context);
        var service = CreateService(context, allowGlobal: true, allowedStoreId: 4721);

        var global = await service.SetGlobalProductionAsync(5, 5, 472, true, null);
        var store = await service.SetStoreProductionAsync(5, 5, 4721, 472, true, null);

        Assert.True(global.IsSuccess, global.Message);
        Assert.True(store.IsSuccess, store.Message);
        var source = await context.InventoryItemSourceCapabilities.SingleAsync(x => x.PreparedItemId == 472);
        Assert.True(source.CanProduce);
        Assert.False(source.CanPurchase);
        Assert.Single(await context.StoreProductionCapabilities
            .Where(x => x.StoreId == 4721 && x.PreparedItemId == 472 && x.Active)
            .ToListAsync());
        Assert.Empty(await context.StoreProductionCapabilities
            .Where(x => x.StoreId == 4722 && x.PreparedItemId == 472)
            .ToListAsync());
        var inventory = await context.StoreInventories.SingleAsync(x =>
            x.StoreId == 4721 && x.PreparedItemId == 472);
        Assert.Equal(0m, inventory.AvailableQty);
        Assert.Empty(await context.InventoryTransactions.ToListAsync());
        Assert.Empty(await context.InventoryCostLayers.ToListAsync());
    }

    [Fact]
    public async Task UnauthorizedStore_CannotConfigureOrBootstrapPreparedItemProduction()
    {
        await using var context = CreateDbContext();
        await SeedAsync(context);
        var service = CreateService(context, allowGlobal: true, allowedStoreId: 4721);
        Assert.True((await service.SetGlobalProductionAsync(5, 5, 472, true, null)).IsSuccess);

        var result = await service.SetStoreProductionAsync(5, 5, 4722, 472, true, null);

        Assert.False(result.IsSuccess);
        Assert.Empty(await context.StoreProductionCapabilities
            .Where(x => x.StoreId == 4722 && x.PreparedItemId == 472)
            .ToListAsync());
        Assert.Empty(await context.StoreInventories
            .Where(x => x.StoreId == 4722 && x.PreparedItemId == 472)
            .ToListAsync());
    }

    [Fact]
    public async Task StoreCapability_CannotBeEnabledBeforeGlobalProductionDecision()
    {
        await using var context = CreateDbContext();
        await SeedAsync(context);
        var service = CreateService(context, allowGlobal: true, allowedStoreId: 4721);

        var result = await service.SetStoreProductionAsync(5, 5, 4721, 472, true, null);

        Assert.False(result.IsSuccess);
        Assert.Contains("cấp toàn chuỗi", result.Message, StringComparison.Ordinal);
        Assert.Empty(await context.StoreProductionCapabilities.ToListAsync());
        Assert.Empty(await context.StoreInventories
            .Where(x => x.StoreId == 4721 && x.PreparedItemId == 472)
            .ToListAsync());
    }

    private static PreparedItemProductionCapabilityService CreateService(
        CafeChain.Data.AppDbContext context,
        bool allowGlobal,
        int allowedStoreId)
    {
        var permissions = new Mock<IAdminPermissionService>();
        permissions.Setup(x => x.HasPermissionAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync((int accountId, string code, int? storeId) =>
                ServiceResult<PermissionDecisionDto>.Success(new PermissionDecisionDto
                {
                    AccountId = accountId,
                    PermissionCode = code,
                    TargetStoreId = storeId,
                    Allowed = code == PermissionConstants.PreparedItemUpdate
                        ? allowGlobal
                        : code == PermissionConstants.ProductionOrderPlan && storeId == allowedStoreId,
                    ScopeAllowed = !storeId.HasValue || storeId == allowedStoreId
                }));
        return new PreparedItemProductionCapabilityService(
            context,
            permissions.Object,
            new PreparedItemInventoryBootstrapService(context));
    }

    private static async Task SeedAsync(CafeChain.Data.AppDbContext context)
    {
        var now = new DateTime(2026, 8, 16, 8, 0, 0, DateTimeKind.Utc);
        context.Stores.AddRange(
            new Store { StoreId = 4721, Name = "Cua hang A", Address = "A", Phone = "1", Active = true, CreatedAt = now },
            new Store { StoreId = 4722, Name = "Cua hang B", Address = "B", Phone = "2", Active = true, CreatedAt = now });
        context.PreparedItems.Add(new PreparedItem
        {
            PreparedItemId = 472,
            Code = "BTP-CAP-472",
            Name = "Cot tra nang luc",
            BaseUnitId = 1,
            Active = true
        });
        await context.SaveChangesAsync();
    }
}
