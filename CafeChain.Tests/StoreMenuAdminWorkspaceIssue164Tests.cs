using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Profitability;
using CafeChain.Application.DTOs.Admin.StoreMenu;
using CafeChain.Application.Interfaces.Admin.Profitability;
using CafeChain.Application.Interfaces.Admin.StoreMenu;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Admin.StoreMenu;
using CafeChain.Models.Customers;
using CafeChain.Models.Drinks;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Tests;

public sealed class StoreMenuAdminWorkspaceIssue164Tests : IntegrationTestBase
{
    private const int StoreA = 16401;
    private const int StoreB = 16402;
    private const int DrinkId = 16403;
    private const int SizeId = 16404;
    private const int DrinkSizeId = 16405;
    private const int MenuA = 16406;
    private const int MenuB = 16407;
    private const int OwnerId = 16408;
    private const int ManagerId = 16409;
    private const int AccountantId = 16410;
    private const int AreaManagerId = 16412;
    private const int CashierId = 16413;
    private const int ShiftSupervisorId = 16414;

    [Fact]
    public async Task Owner_PublishesDraft_AtomicallyAuditsAndInvalidatesCatalog()
    {
        await SeedAsync();
        await using var context = CreateDbContext();
        var service = CreateService(context);
        var version = VersionOf(await context.StoreMenuItems.AsNoTracking().SingleAsync(x => x.StoreMenuItemId == MenuA));

        var result = await service.UpdateLifecycleAsync(new UpdateStoreMenuLifecycleRequest
        {
            StoreMenuItemId = MenuA,
            Action = StoreMenuLifecycleActions.Publish,
            ExpectedRowVersion = version,
            Reason = "Duyệt SKU cho cửa hàng A"
        }, OwnerId);

        Assert.True(result.IsSuccess, result.Message);
        var item = await context.StoreMenuItems.AsNoTracking().SingleAsync(x => x.StoreMenuItemId == MenuA);
        Assert.True(item.PublishedAtUtc.HasValue);
        Assert.True(item.IsEnabled);
        Assert.Equal(OwnerId, item.PublishedByStaffId);
        var audit = await context.StoreMenuItemAudits.SingleAsync(x => x.StoreMenuItemId == MenuA);
        Assert.Equal(StoreMenuLifecycleActions.Publish, audit.Action);
        Assert.False(audit.OldIsEnabled);
        Assert.True(audit.NewIsEnabled);
        Assert.Equal(0, audit.CatalogVersionBefore);
        Assert.Equal(1, audit.CatalogVersionAfter);
        Assert.Equal(version, Convert.ToBase64String(audit.ItemRowVersionBefore));
        Assert.NotEmpty(audit.ItemRowVersionAfter);
        Assert.Equal(1, (await context.PosCatalogStates.SingleAsync(x => x.StoreId == StoreA)).Version);
        Assert.False(await context.PosCatalogStates.AnyAsync(x => x.StoreId == StoreB));
    }

    [Fact]
    public async Task StoreManager_CanPauseOwnStore_ButCannotPublishOrMutateAnotherStore()
    {
        await SeedAsync(published: true);
        await using var context = CreateDbContext();
        var service = CreateService(context);

        var publishDenied = await service.UpdateLifecycleAsync(new UpdateStoreMenuLifecycleRequest
        {
            StoreMenuItemId = MenuA,
            Action = StoreMenuLifecycleActions.Publish,
            ExpectedRowVersion = VersionOf(await context.StoreMenuItems.AsNoTracking().SingleAsync(x => x.StoreMenuItemId == MenuA)),
            Reason = "Manager thử publish"
        }, ManagerId);
        var crossStoreDenied = await service.UpdateLifecycleAsync(new UpdateStoreMenuLifecycleRequest
        {
            StoreMenuItemId = MenuB,
            Action = StoreMenuLifecycleActions.Pause,
            ExpectedRowVersion = VersionOf(await context.StoreMenuItems.AsNoTracking().SingleAsync(x => x.StoreMenuItemId == MenuB)),
            Reason = "Manager thử pause store B"
        }, ManagerId);
        var ownStore = await service.UpdateLifecycleAsync(new UpdateStoreMenuLifecycleRequest
        {
            StoreMenuItemId = MenuA,
            Action = StoreMenuLifecycleActions.Pause,
            ExpectedRowVersion = VersionOf(await context.StoreMenuItems.AsNoTracking().SingleAsync(x => x.StoreMenuItemId == MenuA)),
            Reason = "Tạm hết nguyên liệu"
        }, ManagerId);

        Assert.False(publishDenied.IsSuccess);
        Assert.Equal("STORE_MENU_PUBLISH_FORBIDDEN", publishDenied.ErrorCode);
        Assert.False(crossStoreDenied.IsSuccess);
        Assert.Equal("STORE_MENU_OPERATION_FORBIDDEN", crossStoreDenied.ErrorCode);
        Assert.True(ownStore.IsSuccess, ownStore.Message);
        var item = await context.StoreMenuItems.AsNoTracking().SingleAsync(x => x.StoreMenuItemId == MenuA);
        Assert.False(item.IsEnabled);
        Assert.Equal("Tạm hết nguyên liệu", item.PauseReason);
    }

    [Fact]
    public async Task ReadOnlyRole_CanReadWorkspace_ButCannotChangeDisplayOrder()
    {
        await SeedAsync(published: true);
        await using var context = CreateDbContext();
        var service = CreateService(context);

        var rows = await service.GetRowsAsync(StoreA, AccountantId, DateTime.UtcNow);
        var denied = await service.UpdateLifecycleAsync(new UpdateStoreMenuLifecycleRequest
        {
            StoreMenuItemId = MenuA,
            Action = StoreMenuLifecycleActions.ChangeDisplayOrder,
            DisplayOrder = 9,
            ExpectedRowVersion = VersionOf(await context.StoreMenuItems.AsNoTracking().SingleAsync(x => x.StoreMenuItemId == MenuA)),
            Reason = "Read-only thử sửa"
        }, AccountantId);

        Assert.True(rows.IsSuccess, rows.Message);
        var row = Assert.Single(rows.Data);
        Assert.Equal(31_000m, row.EffectivePrice);
        Assert.Equal(12_000m, row.FifoCost);
        Assert.Equal(61.29m, row.EstimatedGrossMarginPercent);
        Assert.False(denied.IsSuccess);
        Assert.Equal("STORE_MENU_OPERATION_FORBIDDEN", denied.ErrorCode);
    }

    [Theory]
    [InlineData(AreaManagerId)]
    [InlineData(AccountantId)]
    [InlineData(CashierId)]
    [InlineData(ShiftSupervisorId)]
    public async Task NonMutationRoles_CannotChangeStoreMenu(int actorStaffId)
    {
        await SeedAsync(published: true);
        await using var context = CreateDbContext();
        var service = CreateService(context);

        var denied = await service.UpdateLifecycleAsync(new UpdateStoreMenuLifecycleRequest
        {
            StoreMenuItemId = MenuA,
            Action = StoreMenuLifecycleActions.Pause,
            ExpectedRowVersion = VersionOf(await context.StoreMenuItems.AsNoTracking()
                .SingleAsync(x => x.StoreMenuItemId == MenuA)),
            Reason = "Vai trò chỉ đọc thử thay đổi menu"
        }, actorStaffId);

        Assert.False(denied.IsSuccess);
        Assert.Equal("STORE_MENU_OPERATION_FORBIDDEN", denied.ErrorCode);
        Assert.Empty(await context.StoreMenuItemAudits.ToListAsync());
    }

    [Fact]
    public void AdminWorkspace_SourceContract_HasStatesAndNoRecipeEditing()
    {
        var root = FindRepositoryRoot();
        var view = File.ReadAllText(Path.Combine(root, "CafeChain", "Areas", "Admin", "Views", "AdminStoreMenu", "Index.cshtml"));
        var script = File.ReadAllText(Path.Combine(root, "CafeChain", "wwwroot", "js", "Admin", "StoreMenu", "store-menu.js"));
        var css = File.ReadAllText(Path.Combine(root, "CafeChain", "wwwroot", "css", "Admin", "StoreMenu", "store-menu.css"));
        var controller = File.ReadAllText(Path.Combine(root, "CafeChain", "Areas", "Admin", "Controllers", "AdminStoreMenuController.cs"));

        Assert.Contains("Đang tải menu cửa hàng", view);
        Assert.Contains("Không tải được menu cửa hàng", view);
        Assert.Contains("STORE_MENU_CHANGED_BY_ANOTHER_USER", controller);
        Assert.Contains("data-action", script);
        Assert.DoesNotContain("UpdateRecipe", view + script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("border-radius: 10px", css, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("linear-gradient", css, StringComparison.OrdinalIgnoreCase);
    }

    private StoreMenuWorkspaceService CreateService(CafeChain.Data.AppDbContext context) => new(
        context,
        new AvailabilityStub(),
        new ProfitabilityStub(),
        new StoreCatalogVersionService(context),
        new ScopeStub());

    private async Task SeedAsync(bool published = false)
    {
        await using var context = CreateDbContext();
        context.Stores.AddRange(
            new Store { StoreId = StoreA, Name = "Store 164 A", Address = "A", Phone = "16401", Active = true, CreatedAt = DateTime.UtcNow },
            new Store { StoreId = StoreB, Name = "Store 164 B", Address = "B", Phone = "16402", Active = true, CreatedAt = DateTime.UtcNow });
        context.Sizes.Add(new Size { SizeId = SizeId, SizeCode = "SM164", Name = "Store Menu 164", Description = "Test", Active = true });
        context.Drinks.Add(new Drink
        {
            DrinkId = DrinkId, DrinkCode = "SM164", Name = "Drink 164", Description = "Test",
            ProductTypeId = 1, Active = true, CreatedAt = DateTime.UtcNow
        });
        context.DrinkSizes.Add(new DrinkSize
        {
            DrinkSizeId = DrinkSizeId, DrinkId = DrinkId, SizeId = SizeId,
            Price = 30_000m, Active = true, UpdatedAtUtc = DateTime.UtcNow
        });
        var publishedAt = published ? DateTime.UtcNow.AddDays(-1) : (DateTime?)null;
        context.StoreMenuItems.AddRange(
            new StoreMenuItem
            {
                StoreMenuItemId = MenuA, StoreId = StoreA, DrinkSizeId = DrinkSizeId,
                IsEnabled = published, PriceOverride = 31_000m, PublishedAtUtc = publishedAt,
                CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
            },
            new StoreMenuItem
            {
                StoreMenuItemId = MenuB, StoreId = StoreB, DrinkSizeId = DrinkSizeId,
                IsEnabled = published, PublishedAtUtc = publishedAt,
                CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
            });

        var ownerRole = await context.Roles.SingleAsync(x => x.Name == RoleConstants.BusinessOwner);
        var managerRole = await context.Roles.SingleAsync(x => x.Name == RoleConstants.StoreManager);
        var accountantRole = await context.Roles.SingleAsync(x => x.Name == RoleConstants.AccountantWarehouse);
        var areaManagerRole = await context.Roles.SingleAsync(x => x.Name == RoleConstants.AreaManager);
        var cashierRole = await context.Roles.SingleAsync(x => x.Name == RoleConstants.SalesStaff);
        var shiftSupervisorRole = await context.Roles.SingleAsync(x => x.Name == RoleConstants.ShiftSupervisor);
        context.Accounts.AddRange(
            new Account { AccountId = OwnerId, Email = "owner164@test.local", PasswordHash = "x", Active = true, CreatedAt = DateTime.UtcNow },
            new Account { AccountId = ManagerId, Email = "manager164@test.local", PasswordHash = "x", Active = true, CreatedAt = DateTime.UtcNow },
            new Account { AccountId = AccountantId, Email = "accountant164@test.local", PasswordHash = "x", Active = true, CreatedAt = DateTime.UtcNow },
            new Account { AccountId = AreaManagerId, Email = "area164@test.local", PasswordHash = "x", Active = true, CreatedAt = DateTime.UtcNow },
            new Account { AccountId = CashierId, Email = "cashier164@test.local", PasswordHash = "x", Active = true, CreatedAt = DateTime.UtcNow },
            new Account { AccountId = ShiftSupervisorId, Email = "supervisor164@test.local", PasswordHash = "x", Active = true, CreatedAt = DateTime.UtcNow });
        context.Staffs.AddRange(
            Staff(OwnerId, StoreA, "Owner"), Staff(ManagerId, StoreA, "Manager"), Staff(AccountantId, StoreA, "Accountant"),
            Staff(AreaManagerId, StoreA, "Area manager"), Staff(CashierId, StoreA, "Cashier"),
            Staff(ShiftSupervisorId, StoreA, "Shift supervisor"));
        context.AccountRoles.AddRange(
            new AccountRole { AccountId = OwnerId, RoleId = ownerRole.RoleId },
            new AccountRole { AccountId = ManagerId, RoleId = managerRole.RoleId },
            new AccountRole { AccountId = AccountantId, RoleId = accountantRole.RoleId },
            new AccountRole { AccountId = AreaManagerId, RoleId = areaManagerRole.RoleId },
            new AccountRole { AccountId = CashierId, RoleId = cashierRole.RoleId },
            new AccountRole { AccountId = ShiftSupervisorId, RoleId = shiftSupervisorRole.RoleId });
        await context.SaveChangesAsync();
    }

    private static Staff Staff(int id, int storeId, string name) => new()
    {
        StaffId = id, AccountId = id, StoreId = storeId, FullName = name, Active = true,
        CreatedAt = DateTime.UtcNow, EmployeeStatus = 2, SalaryType = 1
    };

    private static string VersionOf(StoreMenuItem item) => Convert.ToBase64String(item.RowVersion);

    private sealed class AvailabilityStub : IStoreMenuAvailabilityEvaluator
    {
        public Task<StoreMenuAvailabilityDto> EvaluateAsync(int storeId, int drinkSizeId, DateTime asOfUtc, CancellationToken cancellationToken = default) =>
            Task.FromResult(new StoreMenuAvailabilityDto
            {
                StoreId = storeId, DrinkSizeId = drinkSizeId, ConfiguredStatus = StoreMenuConfiguredStatuses.Active,
                OperationalStatus = StoreMenuAvailabilityStatuses.Available, Reason = "Sẵn sàng.", IsSellable = true
            });

        public Task<IReadOnlyDictionary<int, StoreMenuAvailabilityDto>> EvaluateStoreAsync(int storeId, DateTime asOfUtc, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<int, StoreMenuAvailabilityDto>>(new Dictionary<int, StoreMenuAvailabilityDto>());
    }

    private sealed class ProfitabilityStub : IDrinkSizeProfitabilityQueryService
    {
        public Task<ServiceResult<DrinkProfitabilityPreviewDto>> PreviewAsync(int storeId, int drinkId, DateTime asOfUtc, int actorStaffId, CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult<DrinkProfitabilityPreviewDto>.Success(new DrinkProfitabilityPreviewDto
            {
                StoreId = storeId, DrinkId = drinkId,
                Sizes = new[] { new DrinkSizeProfitabilityRowDto { DrinkSizeId = DrinkSizeId, EstimatedCost = 12_000m, CostStatus = "COMPLETE", RecipeId = 16411 } }
            }));
    }

    private sealed class ScopeStub : IScopeAuthorizationService
    {
        public Task<List<Store>> GetAllowedStoresAsync(int currentStaffId) => Task.FromResult(new List<Store>());
        public Task<bool> CheckIfStoreIsWithinManagerScopeAsync(int currentStaffId, int targetStoreId) => Task.FromResult(false);
        public Task<bool> CanAccessStoreAsync(int currentStaffId, int targetStoreId) => Task.FromResult(false);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "CafeChain")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Không tìm thấy repository root.");
    }
}
