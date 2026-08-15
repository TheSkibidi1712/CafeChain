using CafeChain.Application.Services.Admin.StoreInventories;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CafeChain.Tests;

public sealed class PreparedItemInventoryBootstrapTests : IntegrationTestBase
{
    [Fact]
    public async Task PreparedItemOperationalizedForStore_CreatesCanonicalZeroInventory()
    {
        await using var context = CreateDbContext();
        var now = new DateTime(2026, 8, 16, 8, 0, 0, DateTimeKind.Utc);
        context.Stores.Add(new Store
        {
            StoreId = 471,
            Name = "Cua hang bootstrap",
            Address = "Test",
            Phone = "000",
            Active = true,
            CreatedAt = now
        });
        context.PreparedItems.Add(new PreparedItem
        {
            PreparedItemId = 471,
            Code = "BTP-BOOTSTRAP-471",
            Name = "Cot tra bootstrap",
            BaseUnitId = 1,
            Active = true
        });
        await context.SaveChangesAsync();

        var service = new PreparedItemInventoryBootstrapService(context, TimeProvider.System);
        var result = await service.EnsureAsync(471, 471, 1, "PreparedItemOperationalization");

        Assert.True(result.IsSuccess, result.Message);
        var row = await context.StoreInventories.SingleAsync(x =>
            x.StoreId == 471 && x.PreparedItemId == 471);
        Assert.Equal(BtpIdentityState.Canonical, row.BtpIdentityState);
        Assert.Equal(InventoryQuantitySemanticsStatus.BaseUnitConfirmed, row.QuantitySemanticsStatus);
        Assert.Equal(0m, row.AvailableQty);
        Assert.Equal(0m, row.ReservedQty);
        Assert.Null(row.MinStockLevel);
        Assert.Null(row.TargetStockLevel);
        Assert.Empty(await context.InventoryTransactions.ToListAsync());
        Assert.Empty(await context.InventoryCostLayers.ToListAsync());
    }

    [Fact]
    public async Task PreparedItemBootstrap_IsIdempotent()
    {
        await using var context = CreateDbContext();
        await SeedAsync(context);
        var service = new PreparedItemInventoryBootstrapService(context, TimeProvider.System);

        var first = await service.EnsureAsync(471, 471, 1, "PreparedItemOperationalization");
        var second = await service.EnsureAsync(471, 471, 1, "PreparedItemOperationalization");

        Assert.True(first.IsSuccess, first.Message);
        Assert.True(second.IsSuccess, second.Message);
        Assert.Equal(first.Data.StoreInventoryId, second.Data.StoreInventoryId);
        Assert.Single(await context.StoreInventories
            .Where(x => x.StoreId == 471 && x.PreparedItemId == 471)
            .ToListAsync());
    }

    private static async Task SeedAsync(CafeChain.Data.AppDbContext context)
    {
        var now = new DateTime(2026, 8, 16, 8, 0, 0, DateTimeKind.Utc);
        context.Stores.Add(new Store
        {
            StoreId = 471,
            Name = "Cua hang bootstrap",
            Address = "Test",
            Phone = "000",
            Active = true,
            CreatedAt = now
        });
        context.PreparedItems.Add(new PreparedItem
        {
            PreparedItemId = 471,
            Code = "BTP-BOOTSTRAP-471",
            Name = "Cot tra bootstrap",
            BaseUnitId = 1,
            Active = true
        });
        await context.SaveChangesAsync();
    }
}
