using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Permissions;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Admin.StoreMenu;
using CafeChain.Models.Drinks;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CafeChain.Tests;

public sealed class StoreMenuProvisioningFinalClosureTests : IntegrationTestBase
{
    [Fact]
    public async Task NewProductSize_CanBeProvisionedToStoreMenu()
    {
        await using var context = CreateDbContext();
        await SeedCatalogAsync(context);
        var service = CreateService(context, allowedStoreId: 7101);

        var result = await service.ProvisionMissingAsync(7101, actorAccountId: 3, actorStaffId: 7103);

        Assert.True(result.IsSuccess, result.Message);
        var item = await context.StoreMenuItems.SingleAsync(x => x.StoreId == 7101 && x.DrinkSizeId == 7111);
        Assert.Equal(7111, item.DrinkSizeId);
        Assert.False(item.IsEnabled);
        Assert.Null(item.PublishedAtUtc);
        var storeDrink = await context.StoreDrinks.SingleAsync(x => x.StoreId == 7101 && x.DrinkId == 7110);
        Assert.Equal(7110, storeDrink.DrinkId);
        Assert.False(storeDrink.Active);
    }

    [Fact]
    public async Task StoreMenuProvisioning_IsIdempotent_AndExistingStoreMenuIsNotDuplicated()
    {
        await using var context = CreateDbContext();
        await SeedCatalogAsync(context);
        var service = CreateService(context, allowedStoreId: 7101);

        var first = await service.ProvisionMissingAsync(7101, 3, 7103);
        var second = await service.ProvisionMissingAsync(7101, 3, 7103);

        Assert.True(first.IsSuccess, first.Message);
        Assert.True(second.IsSuccess, second.Message);
        Assert.Single(await context.StoreDrinks.Where(x => x.StoreId == 7101 && x.DrinkId == 7110).ToListAsync());
        Assert.Single(await context.StoreMenuItems.Where(x => x.StoreId == 7101 && x.DrinkSizeId == 7111).ToListAsync());
        Assert.Equal(0, second.Data!.CreatedCount);
    }

    [Fact]
    public async Task NewSku_IsNotAutomaticallyActivatedInEveryStore()
    {
        await using var context = CreateDbContext();
        await SeedCatalogAsync(context);
        var service = CreateService(context, allowedStoreId: 7101);

        var result = await service.ProvisionMissingAsync(7101, 3, 7103);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Empty(await context.StoreMenuItems.Where(x => x.StoreId == 7102).ToListAsync());
        Assert.Empty(await context.StoreDrinks.Where(x => x.StoreId == 7102).ToListAsync());
    }

    [Fact]
    public async Task StoreManager_CannotConfigureOtherStoreSku()
    {
        await using var context = CreateDbContext();
        await SeedCatalogAsync(context);
        var service = CreateService(context, allowedStoreId: 7101);

        var result = await service.ProvisionMissingAsync(7102, 3, 7103);

        Assert.False(result.IsSuccess);
        Assert.Equal("STORE_MENU_PROVISION_FORBIDDEN", result.ErrorCode);
        Assert.Empty(await context.StoreMenuItems.ToListAsync());
    }

    private static StoreMenuProvisioningService CreateService(
        CafeChain.Data.AppDbContext context,
        int allowedStoreId)
    {
        var permissions = new Mock<IAdminPermissionService>();
        permissions.Setup(x => x.HasPermissionAsync(It.IsAny<int>(), PermissionConstants.StoreMenuUpdate, It.IsAny<int?>()))
            .ReturnsAsync((int accountId, string code, int? storeId) =>
                ServiceResult<PermissionDecisionDto>.Success(new PermissionDecisionDto
                {
                    AccountId = accountId,
                    PermissionCode = code,
                    TargetStoreId = storeId,
                    Allowed = storeId == allowedStoreId,
                    ScopeAllowed = storeId == allowedStoreId
                }));
        return new StoreMenuProvisioningService(
            context,
            new StoreMenuBackfillPlanner(context),
            permissions.Object);
    }

    private static async Task SeedCatalogAsync(CafeChain.Data.AppDbContext context)
    {
        var now = new DateTime(2026, 8, 17, 8, 0, 0, DateTimeKind.Utc);
        context.Stores.AddRange(
            new Store { StoreId = 7101, Name = "Store A", Address = "A", Phone = "7101", Active = true, CreatedAt = now },
            new Store { StoreId = 7102, Name = "Store B", Address = "B", Phone = "7102", Active = true, CreatedAt = now });
        context.Sizes.Add(new Size
        {
            SizeId = 7109,
            SizeCode = "FINAL-M",
            Name = "FINAL M 7109",
            Description = "Final closure",
            Active = true
        });
        context.Drinks.Add(new Drink
        {
            DrinkId = 7110,
            DrinkCode = "FINAL-DRINK",
            Name = "Tra sua final",
            Description = "Final closure",
            ProductTypeId = 1,
            Active = true,
            CreatedAt = now
        });
        context.DrinkSizes.Add(new DrinkSize
        {
            DrinkSizeId = 7111,
            DrinkId = 7110,
            SizeId = 7109,
            Price = 35_000m,
            Active = true
        });
        await context.SaveChangesAsync();
    }
}
