using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Profitability;
using CafeChain.Application.DTOs.Admin.StoreMenu;
using CafeChain.Application.Interfaces.Admin.Profitability;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Admin.Profitability;
using CafeChain.Application.Services.Admin.StoreMenu;
using CafeChain.Application.Services.Security;
using CafeChain.Models.Customers;
using CafeChain.Models.Drinks;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Tests;

public sealed class StoreMenuPricingIssue162Tests : IntegrationTestBase
{
    private const int StoreA = 16201;
    private const int StoreB = 16202;
    private const int DrinkId = 16203;
    private const int SizeId = 16204;
    private const int DrinkSizeId = 16205;
    private const int MenuA = 16206;
    private const int MenuB = 16207;
    private const int OwnerId = 16208;
    private const int ManagerId = 16209;

    [Fact]
    public async Task StoreOverride_SetAndClear_UsesEffectivePriceAndInvalidatesOnlyTargetStore()
    {
        await SeedAsync();
        await using var context = CreateDbContext();
        var service = new StoreMenuPricingService(
            context,
            new StoreCatalogVersionService(context),
            new ScopeAuthorizationService(context));
        var rowVersion = Convert.ToBase64String((await context.StoreMenuItems.AsNoTracking().SingleAsync(x => x.StoreMenuItemId == MenuA)).RowVersion);

        var set = await service.UpdateOverrideAsync(new UpdateStoreMenuPriceOverrideRequest
        {
            StoreMenuItemId = MenuA,
            PriceOverride = 35_000m,
            ExpectedRowVersion = rowVersion,
            Reason = "Giá thử nghiệm tại cửa hàng A"
        }, OwnerId);

        Assert.True(set.IsSuccess, set.Message);
        Assert.Equal(30_000m, set.Data.GlobalPrice);
        Assert.Equal(35_000m, set.Data.StoreOverride);
        Assert.Equal(35_000m, set.Data.EffectivePrice);
        Assert.Equal(StoreMenuPriceSources.StoreOverride, set.Data.PriceSource);
        Assert.Equal(1, await context.PosCatalogStates.CountAsync(x => x.StoreId == StoreA));
        Assert.Empty(await context.PosCatalogStates.Where(x => x.StoreId == StoreB).ToListAsync());

        var clear = await service.UpdateOverrideAsync(new UpdateStoreMenuPriceOverrideRequest
        {
            StoreMenuItemId = MenuA,
            PriceOverride = null,
            ExpectedRowVersion = set.Data.RowVersion,
            Reason = "Quay lại giá toàn hệ thống"
        }, OwnerId);

        Assert.True(clear.IsSuccess, clear.Message);
        Assert.Null(clear.Data.StoreOverride);
        Assert.Equal(30_000m, clear.Data.EffectivePrice);
        Assert.Equal(StoreMenuPriceSources.Global, clear.Data.PriceSource);
        Assert.Equal(2, (await context.PosCatalogStates.SingleAsync(x => x.StoreId == StoreA)).Version);
        var audits = await context.StoreMenuItemAudits
            .Where(x => x.StoreMenuItemId == MenuA)
            .OrderBy(x => x.StoreMenuItemAuditId)
            .ToListAsync();
        Assert.Equal(2, audits.Count);
        Assert.Null(audits[0].OldPriceOverride);
        Assert.Equal(35_000m, audits[0].NewPriceOverride);
        Assert.Equal(0, audits[0].CatalogVersionBefore);
        Assert.Equal(1, audits[0].CatalogVersionAfter);
        Assert.Equal(35_000m, audits[1].OldPriceOverride);
        Assert.Null(audits[1].NewPriceOverride);
        Assert.Equal(1, audits[1].CatalogVersionBefore);
        Assert.Equal(2, audits[1].CatalogVersionAfter);
    }

    [Fact]
    public async Task GlobalPriceChange_InvalidatesOnlyFallbackStores()
    {
        await SeedAsync();
        await using var context = CreateDbContext();
        var pricing = new DrinkSizePricingService(
            context,
            new CompleteProfitabilityStub(),
            new StoreCatalogVersionService(context));
        var rowVersion = Convert.ToBase64String((await context.DrinkSizes.AsNoTracking().SingleAsync(x => x.DrinkSizeId == DrinkSizeId)).RowVersion);

        var result = await pricing.UpdatePriceAsync(new UpdateDrinkSizePriceRequest
        {
            DrinkSizeId = DrinkSizeId,
            NewSellingPrice = 32_000m,
            ExpectedRowVersion = rowVersion,
            Reason = "Cập nhật giá toàn hệ thống"
        }, StoreA, OwnerId);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(1, (await context.PosCatalogStates.SingleAsync(x => x.StoreId == StoreA)).Version);
        Assert.False(await context.PosCatalogStates.AnyAsync(x => x.StoreId == StoreB));
        var menus = await context.StoreMenuItems.Include(x => x.DrinkSize).OrderBy(x => x.StoreId).ToListAsync();
        Assert.Equal(32_000m, menus[0].GetEffectivePrice());
        Assert.Equal(40_000m, menus[1].GetEffectivePrice());
    }

    [Fact]
    public async Task StoreManager_CannotEditGlobalOrStoreOverride()
    {
        await SeedAsync();
        await using var context = CreateDbContext();
        var overrideService = new StoreMenuPricingService(
            context,
            new StoreCatalogVersionService(context),
            new ScopeAuthorizationService(context));
        var menuVersion = Convert.ToBase64String((await context.StoreMenuItems.AsNoTracking().SingleAsync(x => x.StoreMenuItemId == MenuA)).RowVersion);

        var deniedOverride = await overrideService.UpdateOverrideAsync(new UpdateStoreMenuPriceOverrideRequest
        {
            StoreMenuItemId = MenuA,
            PriceOverride = 31_000m,
            ExpectedRowVersion = menuVersion,
            Reason = "Manager thử sửa giá"
        }, ManagerId);

        var globalService = new DrinkSizePricingService(context, new CompleteProfitabilityStub(), new StoreCatalogVersionService(context));
        var globalVersion = Convert.ToBase64String((await context.DrinkSizes.AsNoTracking().SingleAsync(x => x.DrinkSizeId == DrinkSizeId)).RowVersion);
        var deniedGlobal = await globalService.UpdatePriceAsync(new UpdateDrinkSizePriceRequest
        {
            DrinkSizeId = DrinkSizeId,
            NewSellingPrice = 31_000m,
            ExpectedRowVersion = globalVersion,
            Reason = "Manager thử sửa global"
        }, StoreA, ManagerId);

        Assert.False(deniedOverride.IsSuccess);
        Assert.Equal("STORE_PRICE_OVERRIDE_FORBIDDEN", deniedOverride.ErrorCode);
        Assert.False(deniedGlobal.IsSuccess);
        Assert.Equal("GLOBAL_PRICE_FORBIDDEN", deniedGlobal.ErrorCode);
    }

    [Fact]
    public async Task UnchangedGlobalPrice_DoesNotCreateAuditOrCatalogVersion()
    {
        await SeedAsync();
        await using var context = CreateDbContext();
        var pricing = new DrinkSizePricingService(context, new CompleteProfitabilityStub(), new StoreCatalogVersionService(context));
        var rowVersion = Convert.ToBase64String((await context.DrinkSizes.AsNoTracking().SingleAsync(x => x.DrinkSizeId == DrinkSizeId)).RowVersion);

        var result = await pricing.UpdatePriceAsync(new UpdateDrinkSizePriceRequest
        {
            DrinkSizeId = DrinkSizeId,
            NewSellingPrice = 30_000m,
            ExpectedRowVersion = rowVersion,
            Reason = "Không đổi"
        }, StoreA, OwnerId);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Empty(await context.DrinkSizePriceAudits.ToListAsync());
        Assert.Empty(await context.PosCatalogStates.ToListAsync());
    }

    private async Task SeedAsync()
    {
        await using var context = CreateDbContext();
        context.Stores.AddRange(
            new Store { StoreId = StoreA, Name = "Store 162 A", Address = "A", Phone = "16201", Active = true, CreatedAt = DateTime.UtcNow },
            new Store { StoreId = StoreB, Name = "Store 162 B", Address = "B", Phone = "16202", Active = true, CreatedAt = DateTime.UtcNow });
        context.Sizes.Add(new Size { SizeId = SizeId, SizeCode = "SM162", Name = "Store Menu 162", Description = "Test", Active = true });
        context.Drinks.Add(new Drink
        {
            DrinkId = DrinkId, DrinkCode = "SM162", Name = "Drink 162", Description = "Test",
            ProductTypeId = 1, Active = true, CreatedAt = DateTime.UtcNow
        });
        context.DrinkSizes.Add(new DrinkSize
        {
            DrinkSizeId = DrinkSizeId, DrinkId = DrinkId, SizeId = SizeId,
            Price = 30_000m, Active = true, UpdatedAtUtc = DateTime.UtcNow
        });
        var published = DateTime.UtcNow.AddDays(-1);
        context.StoreMenuItems.AddRange(
            new StoreMenuItem
            {
                StoreMenuItemId = MenuA, StoreId = StoreA, DrinkSizeId = DrinkSizeId,
                IsEnabled = true, PublishedAtUtc = published, CreatedAtUtc = published, UpdatedAtUtc = published
            },
            new StoreMenuItem
            {
                StoreMenuItemId = MenuB, StoreId = StoreB, DrinkSizeId = DrinkSizeId,
                IsEnabled = true, PriceOverride = 40_000m, PublishedAtUtc = published,
                CreatedAtUtc = published, UpdatedAtUtc = published
            });

        var ownerRole = await context.Roles.SingleAsync(x => x.Name == RoleConstants.BusinessOwner);
        var managerRole = await context.Roles.SingleAsync(x => x.Name == RoleConstants.StoreManager);
        context.Accounts.AddRange(
            new Account { AccountId = OwnerId, Email = "owner162@test.local", PasswordHash = "x", Active = true, CreatedAt = DateTime.UtcNow },
            new Account { AccountId = ManagerId, Email = "manager162@test.local", PasswordHash = "x", Active = true, CreatedAt = DateTime.UtcNow });
        context.Staffs.AddRange(
            new Staff { StaffId = OwnerId, AccountId = OwnerId, StoreId = StoreA, FullName = "Owner", Active = true, CreatedAt = DateTime.UtcNow, EmployeeStatus = 2},
            new Staff { StaffId = ManagerId, AccountId = ManagerId, StoreId = StoreA, FullName = "Manager", Active = true, CreatedAt = DateTime.UtcNow, EmployeeStatus = 2});
        context.AccountRoles.AddRange(
            new AccountRole { AccountId = OwnerId, RoleId = ownerRole.RoleId },
            new AccountRole { AccountId = ManagerId, RoleId = managerRole.RoleId });
        context.StaffScopes.AddRange(
            new StaffScope
            {
                StaffId = OwnerId,
                ScopeTypeId = (int)CafeChain.Application.Interfaces.Security.ScopeLevel.Store,
                ScopeRefId = StoreA
            },
            new StaffScope
            {
                StaffId = ManagerId,
                ScopeTypeId = (int)CafeChain.Application.Interfaces.Security.ScopeLevel.Store,
                ScopeRefId = StoreA
            });
        await context.SaveChangesAsync();
    }

    private sealed class CompleteProfitabilityStub : IDrinkSizeProfitabilityQueryService
    {
        public Task<ServiceResult<DrinkProfitabilityPreviewDto>> PreviewAsync(
            int storeId,
            int drinkId,
            DateTime asOfUtc,
            int actorStaffId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult<DrinkProfitabilityPreviewDto>.Success(new DrinkProfitabilityPreviewDto
            {
                StoreId = storeId,
                DrinkId = drinkId,
                Sizes = new[]
                {
                    new DrinkSizeProfitabilityRowDto
                    {
                        DrinkSizeId = DrinkSizeId,
                        CostStatus = ProfitabilityCostStatuses.Complete
                    }
                }
            }));
    }
}
