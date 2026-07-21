using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Profitability;
using CafeChain.Application.DTOs.Admin.StoreMenu;
using CafeChain.Application.Interfaces.Admin.Profitability;
using CafeChain.Application.Interfaces.Admin.StoreMenu;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Admin.Profitability;
using CafeChain.Application.Services.Admin.StoreMenu;
using CafeChain.Data;
using CafeChain.Models.Customers;
using CafeChain.Models.Drinks;
using CafeChain.Models.Orders;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Tests;

[Trait("Category", "SqlServerIntegration")]
public sealed class StoreMenuHardeningIssue166SqlServerTests : IAsyncLifetime
{
    private const string Database = "CafeChain_StoreMenuIssue166Tests";
    private static string ConnectionString => SqlServerTestConnection.Create(Database);

    public async Task InitializeAsync()
    {
        try
        {
            await using (var master = new SqlConnection(SqlServerTestConnection.MasterConnectionString()))
            {
                await master.OpenAsync();
                await using var command = master.CreateCommand();
                command.CommandText = $"IF DB_ID(N'{Database}') IS NULL CREATE DATABASE [{Database}];";
                await command.ExecuteNonQueryAsync();
            }

            await using var context = CreateContext();
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"SQL Server integration environment unavailable. Database={Database}. {ex.Message}", ex);
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SqlServer_OneStoreMenuItemPerStoreDrinkSize()
    {
        var graph = await SeedGraphAsync("UNIQUE");
        await using var context = CreateContext();
        context.StoreMenuItems.Add(new StoreMenuItem
        {
            StoreId = graph.StoreId,
            DrinkSizeId = graph.DrinkSizeId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Contains("UX_StoreMenuItems_Store_DrinkSize", exception.InnerException?.Message ?? exception.Message);
    }

    [Fact]
    public async Task SqlServer_ConcurrentMenuUpdate_AllowsOneWinner()
    {
        var graph = await SeedGraphAsync("ITEM-RACE", published: true, seedCatalogState: true);
        var expected = await ReadItemVersionAsync(graph.MenuItemIds[0]);

        var results = await Task.WhenAll("AB".Select(async marker =>
        {
            await using var context = CreateContext();
            return await CreateWorkspace(context).UpdateLifecycleAsync(new UpdateStoreMenuLifecycleRequest
            {
                StoreMenuItemId = graph.MenuItemIds[0],
                Action = StoreMenuLifecycleActions.ChangeDisplayOrder,
                DisplayOrder = marker,
                ExpectedRowVersion = expected,
                Reason = $"Concurrent item update {marker}"
            }, graph.OwnerStaffId);
        }));

        Assert.Single(results.Where(x => x.IsSuccess));
        Assert.Single(results.Where(x => !x.IsSuccess && x.ErrorCode == "STORE_MENU_CHANGED_BY_ANOTHER_USER"));
        await using var verify = CreateContext();
        Assert.Single(await verify.StoreMenuItemAudits.Where(x => x.StoreMenuItemId == graph.MenuItemIds[0]).ToListAsync());
    }

    [Fact]
    public async Task SqlServer_PublishAuditVersion_AreAtomic()
    {
        var graph = await SeedGraphAsync("ATOMIC", seedCatalogState: true);
        await using var setup = CreateContext();
        await setup.Database.ExecuteSqlRawAsync("""
            CREATE OR ALTER TRIGGER TR_SM166_RejectAudit ON StoreMenuItemAudits
            INSTEAD OF INSERT AS
            THROW 51000, 'Store menu audit failure', 1;
            """);

        try
        {
            var expected = await ReadItemVersionAsync(graph.MenuItemIds[0]);
            await using var context = CreateContext();
            await Assert.ThrowsAnyAsync<Exception>(() => CreateWorkspace(context).UpdateLifecycleAsync(
                new UpdateStoreMenuLifecycleRequest
                {
                    StoreMenuItemId = graph.MenuItemIds[0],
                    Action = StoreMenuLifecycleActions.Publish,
                    ExpectedRowVersion = expected,
                    Reason = "Atomic publish"
                }, graph.OwnerStaffId));

            await using var verify = CreateContext();
            var item = await verify.StoreMenuItems.AsNoTracking().SingleAsync(x => x.StoreMenuItemId == graph.MenuItemIds[0]);
            Assert.False(item.IsEnabled);
            Assert.Null(item.PublishedAtUtc);
            Assert.Equal(0, (await verify.PosCatalogStates.AsNoTracking().SingleAsync(x => x.StoreId == graph.StoreId)).Version);
            Assert.Empty(await verify.StoreMenuItemAudits.Where(x => x.StoreMenuItemId == graph.MenuItemIds[0]).ToListAsync());
        }
        finally
        {
            await using var cleanup = CreateContext();
            await cleanup.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS TR_SM166_RejectAudit;");
        }
    }

    [Fact]
    public async Task SqlServer_StoreCatalogVersions_AreIsolated()
    {
        var first = await SeedGraphAsync("VERSION-A", seedCatalogState: true);
        var second = await SeedGraphAsync("VERSION-B", seedCatalogState: true);
        await using var context = CreateContext();
        await new StoreCatalogVersionService(context).InvalidateAsync(new[] { first.StoreId }, DateTime.UtcNow);
        await context.SaveChangesAsync();

        await using var verify = CreateContext();
        Assert.Equal(1, (await verify.PosCatalogStates.AsNoTracking().SingleAsync(x => x.StoreId == first.StoreId)).Version);
        Assert.Equal(0, (await verify.PosCatalogStates.AsNoTracking().SingleAsync(x => x.StoreId == second.StoreId)).Version);
    }

    [Fact]
    public async Task SqlServer_ConcurrentVersionIncrement_UpdatesOnce()
    {
        var graph = await SeedGraphAsync("VERSION-RACE", published: true, seedCatalogState: true, menuItemCount: 2);
        var versions = await Task.WhenAll(graph.MenuItemIds.Select(ReadItemVersionAsync));

        var results = await Task.WhenAll(graph.MenuItemIds.Select(async (itemId, index) =>
        {
            await using var context = CreateContext();
            return await CreateWorkspace(context).UpdateLifecycleAsync(new UpdateStoreMenuLifecycleRequest
            {
                StoreMenuItemId = itemId,
                Action = StoreMenuLifecycleActions.ChangeDisplayOrder,
                DisplayOrder = 10 + index,
                ExpectedRowVersion = versions[index],
                Reason = "Concurrent catalog version"
            }, graph.OwnerStaffId);
        }));

        Assert.Single(results.Where(x => x.IsSuccess));
        Assert.Single(results.Where(x => !x.IsSuccess && x.ErrorCode == "STORE_MENU_CHANGED_BY_ANOTHER_USER"));
        await using var verify = CreateContext();
        Assert.Equal(1, (await verify.PosCatalogStates.AsNoTracking().SingleAsync(x => x.StoreId == graph.StoreId)).Version);
        Assert.Single(await verify.StoreMenuItemAudits.Where(x => x.StoreId == graph.StoreId).ToListAsync());
    }

    [Fact]
    public async Task SqlServer_GlobalPriceInvalidatesAffectedStores()
    {
        var graph = await SeedGraphAsync("GLOBAL", published: true, seedCatalogState: true);
        var overrideGraph = await AddSecondStoreForDrinkSizeAsync(graph, "GLOBAL-OVERRIDE");
        var expected = await ReadDrinkSizeVersionAsync(graph.DrinkSizeId);

        await using var context = CreateContext();
        var service = new DrinkSizePricingService(
            context,
            new ProfitabilityStub(graph.DrinkSizeId),
            new StoreCatalogVersionService(context));
        var result = await service.UpdatePriceAsync(new UpdateDrinkSizePriceRequest
        {
            DrinkSizeId = graph.DrinkSizeId,
            NewSellingPrice = 42_000m,
            ExpectedRowVersion = expected,
            Reason = "Global price SQL hardening"
        }, graph.StoreId, graph.OwnerStaffId);

        Assert.True(result.IsSuccess, result.Message);
        await using var verify = CreateContext();
        Assert.Equal(1, (await verify.PosCatalogStates.AsNoTracking().SingleAsync(x => x.StoreId == graph.StoreId)).Version);
        Assert.Equal(0, (await verify.PosCatalogStates.AsNoTracking().SingleAsync(x => x.StoreId == overrideGraph.StoreId)).Version);
    }

    [Fact]
    public async Task SqlServer_OfflineSnapshotPersistsWithoutRepricing()
    {
        var graph = await SeedGraphAsync("OFFLINE", published: true);
        var clientOrderId = Guid.NewGuid();
        await using (var context = CreateContext())
        {
            var drinkSize = await context.DrinkSizes.SingleAsync(x => x.DrinkSizeId == graph.DrinkSizeId);
            drinkSize.Price = 49_000m;
            context.Orders.Add(new Order
            {
                StoreId = graph.StoreId,
                OrderStatusId = SystemConstants.OrderStatuses.Completed,
                PaymentStatusId = SystemConstants.PaymentStatuses.Paid,
                OrderTypeId = 1,
                ClientOrderId = clientOrderId,
                Source = "POS",
                SubTotal = 32_000m,
                Total = 32_000m,
                CreatedAt = DateTime.UtcNow,
                OrderDetails = new List<OrderDetail>
                {
                    new()
                    {
                        DrinkId = graph.DrinkId,
                        SizeId = graph.SizeId,
                        StoreMenuItemId = graph.MenuItemIds[0],
                        DrinkSizeId = graph.DrinkSizeId,
                        DrinkName = "Offline accepted snapshot",
                        SizeName = "M",
                        AcceptedBasePrice = 30_000m,
                        Price = 32_000m,
                        PriceSource = StoreMenuPriceSources.Global,
                        AcceptedCatalogVersion = 7,
                        Quantity = 1,
                        Note = string.Empty
                    }
                }
            });
            await context.SaveChangesAsync();
        }

        await using var verify = CreateContext();
        var detail = await verify.OrderDetails.AsNoTracking()
            .SingleAsync(x => x.Order.ClientOrderId == clientOrderId);
        Assert.Equal(49_000m, (await verify.DrinkSizes.AsNoTracking().SingleAsync(x => x.DrinkSizeId == graph.DrinkSizeId)).Price);
        Assert.Equal(30_000m, detail.AcceptedBasePrice);
        Assert.Equal(32_000m, detail.Price);
        Assert.Equal(7, detail.AcceptedCatalogVersion);
    }

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlServer(ConnectionString)
        .Options);

    private static StoreMenuWorkspaceService CreateWorkspace(AppDbContext context) => new(
        context,
        new AvailabilityStub(),
        new ProfitabilityStub(),
        new StoreCatalogVersionService(context),
        new ScopeStub());

    private static async Task<string> ReadItemVersionAsync(int itemId)
    {
        await using var context = CreateContext();
        return Convert.ToBase64String((await context.StoreMenuItems.AsNoTracking()
            .SingleAsync(x => x.StoreMenuItemId == itemId)).RowVersion);
    }

    private static async Task<string> ReadDrinkSizeVersionAsync(int drinkSizeId)
    {
        await using var context = CreateContext();
        return Convert.ToBase64String((await context.DrinkSizes.AsNoTracking()
            .SingleAsync(x => x.DrinkSizeId == drinkSizeId)).RowVersion);
    }

    private static async Task<Graph> SeedGraphAsync(
        string suffix,
        bool published = false,
        bool seedCatalogState = false,
        int menuItemCount = 1)
    {
        await using var context = CreateContext();
        var token = $"{suffix}-{Guid.NewGuid():N}"[..Math.Min(40, suffix.Length + 9)];
        var store = new Store
        {
            Name = $"Store {token}", Address = "SQL hardening", Phone = Guid.NewGuid().ToString("N")[..10],
            Active = true, CreatedAt = DateTime.UtcNow
        };
        var size = new Size { SizeCode = $"S{Guid.NewGuid():N}"[..20], Name = $"Size {token}", Description = "SQL", Active = true };
        var drink = new Drink
        {
            DrinkCode = $"D{Guid.NewGuid():N}"[..20], Name = $"Drink {token}", Description = "SQL",
            ProductTypeId = 1, Active = true, CreatedAt = DateTime.UtcNow
        };
        var account = new Account
        {
            Email = $"{Guid.NewGuid():N}@storemenu.test", PasswordHash = "x", Active = true, CreatedAt = DateTime.UtcNow
        };
        context.AddRange(store, size, drink, account);
        await context.SaveChangesAsync();

        var ownerRole = await context.Roles.SingleAsync(x => x.Name == RoleConstants.BusinessOwner);
        var owner = new Staff
        {
            AccountId = account.AccountId, StoreId = store.StoreId, FullName = "Store Menu SQL Owner",
            Active = true, CreatedAt = DateTime.UtcNow, EmployeeStatus = 2};
        var drinkSize = new DrinkSize
        {
            DrinkId = drink.DrinkId, SizeId = size.SizeId, Price = 30_000m,
            Active = true, UpdatedAtUtc = DateTime.UtcNow
        };
        context.AddRange(owner, drinkSize);
        context.AccountRoles.Add(new AccountRole { AccountId = account.AccountId, RoleId = ownerRole.RoleId });
        await context.SaveChangesAsync();

        var publishedAt = published ? DateTime.UtcNow.AddMinutes(-5) : (DateTime?)null;
        var items = Enumerable.Range(0, menuItemCount).Select(index => new StoreMenuItem
        {
            StoreId = store.StoreId,
            DrinkSizeId = drinkSize.DrinkSizeId,
            IsEnabled = published,
            PublishedAtUtc = publishedAt,
            DisplayOrder = index,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        }).ToList();
        if (menuItemCount > 1)
        {
            var secondSize = new Size
            {
                SizeCode = $"S{Guid.NewGuid():N}"[..20], Name = $"Second {token}", Description = "SQL", Active = true
            };
            context.Sizes.Add(secondSize);
            await context.SaveChangesAsync();
            var secondDrinkSize = new DrinkSize
            {
                DrinkId = drink.DrinkId, SizeId = secondSize.SizeId, Price = 31_000m,
                Active = true, UpdatedAtUtc = DateTime.UtcNow
            };
            context.DrinkSizes.Add(secondDrinkSize);
            await context.SaveChangesAsync();
            items[1].DrinkSizeId = secondDrinkSize.DrinkSizeId;
        }
        context.StoreMenuItems.AddRange(items);
        if (seedCatalogState)
        {
            context.PosCatalogStates.Add(new PosCatalogState
            {
                StoreId = store.StoreId, Version = 0, UpdatedAtUtc = DateTime.UtcNow
            });
        }
        await context.SaveChangesAsync();
        return new Graph(store.StoreId, drink.DrinkId, size.SizeId, drinkSize.DrinkSizeId, owner.StaffId,
            items.Select(x => x.StoreMenuItemId).ToArray());
    }

    private static async Task<Graph> AddSecondStoreForDrinkSizeAsync(Graph source, string suffix)
    {
        await using var context = CreateContext();
        var store = new Store
        {
            Name = $"Store {suffix} {Guid.NewGuid():N}"[..50], Address = "SQL hardening",
            Phone = Guid.NewGuid().ToString("N")[..10], Active = true, CreatedAt = DateTime.UtcNow
        };
        context.Stores.Add(store);
        await context.SaveChangesAsync();
        var item = new StoreMenuItem
        {
            StoreId = store.StoreId,
            DrinkSizeId = source.DrinkSizeId,
            IsEnabled = true,
            PriceOverride = 55_000m,
            PublishedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        context.StoreMenuItems.Add(item);
        context.PosCatalogStates.Add(new PosCatalogState
        {
            StoreId = store.StoreId, Version = 0, UpdatedAtUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        return source with { StoreId = store.StoreId, MenuItemIds = new[] { item.StoreMenuItemId } };
    }

    private sealed record Graph(
        int StoreId,
        int DrinkId,
        int SizeId,
        int DrinkSizeId,
        int OwnerStaffId,
        int[] MenuItemIds);

    private sealed class AvailabilityStub : IStoreMenuAvailabilityEvaluator
    {
        public Task<StoreMenuAvailabilityDto> EvaluateAsync(
            int storeId, int drinkSizeId, DateTime asOfUtc, CancellationToken cancellationToken = default) =>
            Task.FromResult(new StoreMenuAvailabilityDto
            {
                StoreId = storeId,
                DrinkSizeId = drinkSizeId,
                ConfiguredStatus = StoreMenuConfiguredStatuses.Active,
                OperationalStatus = StoreMenuAvailabilityStatuses.Available,
                Reason = "Sẵn sàng.",
                IsSellable = true
            });

        public Task<IReadOnlyDictionary<int, StoreMenuAvailabilityDto>> EvaluateStoreAsync(
            int storeId, DateTime asOfUtc, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<int, StoreMenuAvailabilityDto>>(
                new Dictionary<int, StoreMenuAvailabilityDto>());
    }

    private sealed class ProfitabilityStub : IDrinkSizeProfitabilityQueryService
    {
        private readonly int? _drinkSizeId;

        public ProfitabilityStub(int? drinkSizeId = null) => _drinkSizeId = drinkSizeId;

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
                Sizes = _drinkSizeId.HasValue
                    ? new[]
                    {
                        new DrinkSizeProfitabilityRowDto
                        {
                            DrinkSizeId = _drinkSizeId.Value,
                            CostStatus = ProfitabilityCostStatuses.Complete,
                            EstimatedCost = 10_000m
                        }
                    }
                    : Array.Empty<DrinkSizeProfitabilityRowDto>()
            }));
    }

    private sealed class ScopeStub : IScopeAuthorizationService
    {
        public Task<List<Store>> GetAllowedStoresAsync(int currentStaffId) => Task.FromResult(new List<Store>());
        public Task<bool> CheckIfStoreIsWithinManagerScopeAsync(int currentStaffId, int targetStoreId) => Task.FromResult(false);
        public Task<bool> CanAccessStoreAsync(int currentStaffId, int targetStoreId) => Task.FromResult(false);
    }
}
