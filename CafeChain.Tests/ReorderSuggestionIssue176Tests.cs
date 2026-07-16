using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Inventories;
using CafeChain.Data;
using CafeChain.Models.Customers;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Suppliers;
using CafeChain.Models.Inventories.Transactions;
using CafeChain.Models.Permissions;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CafeChain.Tests;

public sealed class ReorderSuggestionIssue176Tests : IntegrationTestBase
{
    private const int StoreId = 1760;
    private const int OtherStoreId = 1761;
    private const int IngredientId = 17601;
    private const int UnitId = 17602;
    private const int SupplierId = 17603;
    private const int ManagerStaffId = 17604;

    [Fact]
    public async Task Formula_UsesAvailable_MinLevel_LeadOverride_Incoming_PackageAndMoq()
    {
        using var context = CreateDbContext();
        await SeedAsync(context, available: 5m, minLevel: 10m, consumption: 300m);
        var before = await context.StoreInventories.SingleAsync(x => x.StoreId == StoreId);
        var service = CreateSuggestionService(context, incoming: 5m);

        var result = await service.GetForStoreAsync(
            StoreId, ManagerStaffId, new[] { RoleConstants.BusinessOwner });

        Assert.True(result.IsSuccess, result.Message);
        var item = Assert.Single(result.Data!.Items);
        Assert.Equal(ReorderSuggestionStatuses.Ready, item.Status);
        Assert.Equal(5m, item.AvailableQuantity);
        Assert.Equal(10m, item.MinLevel);
        Assert.Equal(10m, item.AverageDailyUsage);
        Assert.Equal(2, item.LeadTimeDays);
        Assert.Equal(30m, item.ReorderPoint);
        Assert.Equal(5m, item.IncomingApprovedPoQuantity);
        Assert.Equal(20m, item.SuggestedBaseQuantity);
        Assert.Equal(6m, item.PackageBaseQuantity);
        Assert.Equal(5, item.SuggestedPackageCount);
        Assert.Equal(500m, item.EstimatedAmount);

        var after = await context.StoreInventories.AsNoTracking()
            .SingleAsync(x => x.StoreInventoryId == before.StoreInventoryId);
        Assert.Equal(before.AvailableQty, after.AvailableQty);
        Assert.Equal(before.ReservedQty, after.ReservedQty);
        Assert.Equal(1, await context.InventoryTransactions.CountAsync());
    }

    [Fact]
    public async Task IncomingThatCoversDemand_DoesNotCreateFalseReorder()
    {
        using var context = CreateDbContext();
        await SeedAsync(context, available: 5m, minLevel: 10m, consumption: 300m);

        var result = await CreateSuggestionService(context, incoming: 25m)
            .GetForStoreAsync(StoreId, ManagerStaffId, new[] { RoleConstants.BusinessOwner });

        var item = Assert.Single(result.Data!.Items);
        Assert.Equal(ReorderSuggestionStatuses.IncomingCoversDemand, item.Status);
        Assert.Equal(0m, item.SuggestedBaseQuantity);
        Assert.Equal(0, item.SuggestedPackageCount);
    }

    [Fact]
    public async Task MissingThresholdAndHistory_AreNotRepresentedAsZero()
    {
        using var context = CreateDbContext();
        await SeedAsync(context, available: 5m, minLevel: null, consumption: null);

        var result = await CreateSuggestionService(context)
            .GetForStoreAsync(StoreId, ManagerStaffId, new[] { RoleConstants.BusinessOwner });

        var item = Assert.Single(result.Data!.Items);
        Assert.Equal(ReorderSuggestionStatuses.MissingThreshold, item.Status);
        Assert.Null(item.MinLevel);
        Assert.Null(item.AverageDailyUsage);
        Assert.Null(item.SuggestedBaseQuantity);
    }

    [Fact]
    public async Task Suggestion_CreatesDraftWithEvidence_AndReusesActiveRequest()
    {
        using var context = CreateDbContext();
        await SeedAsync(context, available: 5m, minLevel: 10m, consumption: 300m, seedManager: true);
        var restock = CreateRestockService(context);
        var input = new CreateRestockDraftFromSuggestionDto
        {
            StoreId = StoreId,
            IngredientId = IngredientId,
            RequestedQuantity = 20m,
            SuggestedQuantity = 20m,
            AnalysisWindowDays = 30,
            AvailableSnapshot = 5m,
            MinLevelSnapshot = 10m,
            AverageDailyUsageSnapshot = 10m,
            LeadTimeDaysSnapshot = 2,
            IncomingQuantitySnapshot = 5m,
            SuggestionReason = "Đủ dữ liệu để tạo yêu cầu nhập nháp."
        };

        var first = await restock.CreateDraftFromSuggestionAsync(input, ManagerStaffId);
        var second = await restock.CreateDraftFromSuggestionAsync(input, ManagerStaffId);

        Assert.True(first.IsSuccess, first.Message);
        Assert.True(second.IsSuccess, second.Message);
        Assert.True(second.Data!.AlreadyExisted);
        Assert.Equal(first.Data!.RestockRequestId, second.Data.RestockRequestId);
        var request = await context.RestockRequests.SingleAsync();
        Assert.Equal(RestockRequestStatuses.Draft, request.Status);
        Assert.Null(request.StockAlertId);
        Assert.Equal(20m, request.RequestedQuantity);
        Assert.Equal(20m, request.SuggestedQuantity);
        Assert.Equal(5m, request.SuggestionAvailableSnapshot);
        Assert.Equal(10m, request.SuggestionMinLevelSnapshot);
        Assert.Equal(10m, request.SuggestionAverageDailyUsageSnapshot);
        Assert.Equal(2, request.SuggestionLeadTimeDaysSnapshot);
        Assert.Equal(5m, request.SuggestionIncomingQuantitySnapshot);
        Assert.Equal(1, await context.RestockRequests.CountAsync());
    }

    [Fact]
    public async Task StoreManager_CannotReadOrCreateForAnotherStore()
    {
        using var context = CreateDbContext();
        await SeedAsync(context, available: 5m, minLevel: 10m, consumption: 300m, seedManager: true);
        context.Stores.Add(new Store
        {
            StoreId = OtherStoreId,
            Name = "Store other #176",
            Address = "Test",
            Phone = "0900176100",
            Active = true,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var read = await CreateSuggestionService(context)
            .GetForStoreAsync(OtherStoreId, ManagerStaffId, new[] { RoleConstants.StoreManager });
        var create = await CreateRestockService(context).CreateDraftFromSuggestionAsync(
            new CreateRestockDraftFromSuggestionDto
            {
                StoreId = OtherStoreId,
                IngredientId = IngredientId,
                RequestedQuantity = 1,
                SuggestedQuantity = 1,
                AnalysisWindowDays = 30
            },
            ManagerStaffId);

        Assert.False(read.IsSuccess);
        Assert.False(create.IsSuccess);
    }

    private static ReorderSuggestionService CreateSuggestionService(AppDbContext context, decimal incoming = 0)
    {
        var conversion = new Mock<IPhysicalUnitConversionService>();
        conversion.Setup(x => x.ConvertAsync(It.IsAny<decimal>(), UnitId, UnitId))
            .ReturnsAsync((decimal quantity, int _, int _) => ServiceResult<decimal>.Success(quantity));
        var scope = new Mock<IScopeAuthorizationService>();
        scope.Setup(x => x.CanAccessStoreAsync(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(true);
        return new ReorderSuggestionService(
            context,
            conversion.Object,
            new FixedIncomingProvider(IngredientId, incoming),
            scope.Object);
    }

    private static RestockRequestService CreateRestockService(AppDbContext context)
    {
        var scope = new Mock<IScopeAuthorizationService>();
        scope.Setup(x => x.CanAccessStoreAsync(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(true);
        return new RestockRequestService(context, scope.Object, NullLogger<RestockRequestService>.Instance);
    }

    private static async Task SeedAsync(
        AppDbContext context,
        decimal available,
        decimal? minLevel,
        decimal? consumption,
        bool seedManager = false)
    {
        context.Units.Add(new Unit
        {
            UnitId = UnitId,
            UnitCode = "kg176",
            Name = "Kilogram #176",
            Type = UnitType.KhoiLuong,
            Active = true
        });
        context.Stores.Add(new Store
        {
            StoreId = StoreId,
            Name = "Store #176",
            Address = "Test",
            Phone = "0900176000",
            Active = true,
            CreatedAt = DateTime.UtcNow
        });
        context.Ingredients.Add(new Ingredient
        {
            IngredientId = IngredientId,
            Code = "ING-176",
            Name = "Cà phê hạt #176",
            BaseUnitId = UnitId,
            Active = true
        });
        var inventory = new StoreInventory
        {
            StoreId = StoreId,
            IngredientId = IngredientId,
            AvailableQty = available,
            ReservedQty = 0,
            MinStockLevel = minLevel,
            LastUpdated = DateTime.UtcNow
        };
        context.StoreInventories.Add(inventory);
        context.Suppliers.Add(new Supplier
        {
            SupplierId = SupplierId,
            Code = "SUP-176",
            Name = "Supplier #176",
            Active = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        context.SupplierStores.Add(new SupplierStore
        {
            SupplierId = SupplierId,
            StoreId = StoreId,
            Active = true,
            LeadTimeOverrideDays = 2,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        context.IngredientSuppliers.Add(new IngredientSupplier
        {
            IngredientId = IngredientId,
            SupplierId = SupplierId,
            UnitId = UnitId,
            PackageQuantity = 6m,
            CurrentPrice = 100m,
            MinimumOrderPackageCount = 5,
            LeadTimeDays = 7,
            IsPrimary = true,
            Active = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        if (seedManager)
            SeedManager(context);
        await context.SaveChangesAsync();

        if (consumption.HasValue)
        {
            context.InventoryTransactions.Add(new InventoryTransaction
            {
                StoreInventoryId = inventory.StoreInventoryId,
                Type = InventoryTransactionTypeEnum.SALES_DEDUCTION,
                StockStatus = InventoryStockStatus.NORMAL,
                Quantity = consumption.Value,
                BeforeQty = available + consumption.Value,
                AfterQty = available,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            });
            await context.SaveChangesAsync();
        }
    }

    private static void SeedManager(AppDbContext context)
    {
        const int accountId = 17606;
        var role = context.Roles.Local.FirstOrDefault(x => x.Name == RoleConstants.StoreManager)
                   ?? context.Roles.FirstOrDefault(x => x.Name == RoleConstants.StoreManager);
        if (role == null)
        {
            role = new Role
            {
                RoleId = 17605,
                Name = RoleConstants.StoreManager,
                Active = true,
                IsStoreLevel = true,
                CreatedAt = DateTime.UtcNow
            };
            context.Roles.Add(role);
        }
        context.Accounts.Add(new Account
        {
            AccountId = accountId,
            Email = "manager176@test.local",
            PasswordHash = "x",
            Active = true,
            CreatedAt = DateTime.UtcNow
        });
        context.AccountRoles.Add(new AccountRole { AccountId = accountId, RoleId = role.RoleId });
        context.Staffs.Add(new Staff
        {
            StaffId = ManagerStaffId,
            AccountId = accountId,
            StoreId = StoreId,
            FullName = "Manager #176",
            Active = true,
            CreatedAt = DateTime.UtcNow,
            BaseSalary = 0
        });
    }

    private sealed class FixedIncomingProvider : IReorderIncomingQuantityProvider
    {
        private readonly int _ingredientId;
        private readonly decimal _quantity;

        public FixedIncomingProvider(int ingredientId, decimal quantity)
        {
            _ingredientId = ingredientId;
            _quantity = quantity;
        }

        public Task<IReadOnlyDictionary<int, decimal>> GetIncomingBaseQuantitiesAsync(
            int storeId,
            IReadOnlyCollection<int> ingredientIds)
        {
            IReadOnlyDictionary<int, decimal> result = _quantity > 0
                ? new Dictionary<int, decimal> { [_ingredientId] = _quantity }
                : new Dictionary<int, decimal>();
            return Task.FromResult(result);
        }
    }
}
