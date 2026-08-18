using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.RestockRequests;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Inventories;
using CafeChain.Application.Services.Security;
using CafeChain.Data;
using CafeChain.Models.Customers;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Permissions;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CafeChain.Tests;

[Trait("Category", "SqlServerIntegration")]
public sealed class RestockRequestDuplicateAdjustmentIssue237SqlServerTests : IAsyncLifetime
{
    private const string Database = "CafeChain_Issue237Tests";
    private const int ProcurementUnitId = 2;

    private int _storeId;
    private int _managerStaffId;
    private int _createIngredientId;
    private int _adjustIngredientId;
    private int _doubleSubmitIngredientId;

    private static string ConnectionString => SqlServerTestConnection.Create(Database);

    public async Task InitializeAsync()
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
        await SeedAsync(context);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SqlServer_ConcurrentManualCreate_OneWinnerAndOneBusinessConflict()
    {
        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();

        var results = await Task.WhenAll(
            CreateService(firstContext).CreateManualAsync(Request(_createIngredientId, "race-create-a"), _managerStaffId),
            CreateService(secondContext).CreateManualAsync(Request(_createIngredientId, "race-create-b"), _managerStaffId));

        Assert.Single(results.Where(x => x.IsSuccess));
        var conflict = Assert.Single(results.Where(x => !x.IsSuccess));
        Assert.Equal(RestockRequestErrorCodes.ActiveRequestExists, conflict.ErrorCode);
        Assert.NotNull(conflict.Data?.ExistingActiveRequest);

        await using var verify = CreateContext();
        var active = await verify.RestockRequests
            .Where(x => x.StoreId == _storeId
                && x.IngredientId == _createIngredientId
                && RestockRequestStatuses.ActiveValues.Contains(x.Status))
            .ToListAsync();
        Assert.Single(active);
        Assert.Equal(active[0].RestockRequestId, conflict.Data!.ExistingActiveRequest!.RestockRequestId);
    }

    [Fact]
    public async Task SqlServer_ConcurrentAdjustment_DoesNotLoseUpdate()
    {
        int requestId;
        string rowVersion;
        await using (var seed = CreateContext())
        {
            var created = await CreateService(seed).CreateManualAsync(
                Request(_adjustIngredientId, "race-adjust-create"),
                _managerStaffId);
            Assert.True(created.IsSuccess, created.Message);
            requestId = created.Data!.RestockRequestId;
            rowVersion = Convert.ToBase64String(await seed.RestockRequests
                .Where(x => x.RestockRequestId == requestId)
                .Select(x => x.RowVersion)
                .SingleAsync());
        }

        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        var results = await Task.WhenAll(
            CreateService(firstContext).AddDemandAdjustmentAsync(
                Adjustment(requestId, rowVersion, "race-adjust-a", 2m),
                _managerStaffId),
            CreateService(secondContext).AddDemandAdjustmentAsync(
                Adjustment(requestId, rowVersion, "race-adjust-b", 3m),
                _managerStaffId));

        Assert.Single(results.Where(x => x.IsSuccess));
        Assert.Single(results.Where(x => x.ErrorCode == RestockRequestErrorCodes.ResourceChanged));
        await using var verify = CreateContext();
        var quantity = await verify.RestockRequests
            .Where(x => x.RestockRequestId == requestId)
            .Select(x => x.RequestedProcurementQuantity)
            .SingleAsync();
        Assert.Contains(quantity, new decimal?[] { 12m, 13m });
        Assert.Equal(1, await verify.RestockRequestTransitions.CountAsync(x =>
            x.RestockRequestId == requestId
            && x.RequestKey!.StartsWith(RestockRequestAuditKeys.DemandAdjustmentPrefix)));
    }

    [Fact]
    public async Task SqlServer_DoubleSubmitSameKey_AddsDemandOnce()
    {
        int requestId;
        string rowVersion;
        await using (var seed = CreateContext())
        {
            var created = await CreateService(seed).CreateManualAsync(
                Request(_doubleSubmitIngredientId, "double-adjust-create"),
                _managerStaffId);
            Assert.True(created.IsSuccess, created.Message);
            requestId = created.Data!.RestockRequestId;
            rowVersion = Convert.ToBase64String(await seed.RestockRequests
                .Where(x => x.RestockRequestId == requestId)
                .Select(x => x.RowVersion)
                .SingleAsync());
        }

        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        var results = await Task.WhenAll(
            CreateService(firstContext).AddDemandAdjustmentAsync(
                Adjustment(requestId, rowVersion, "same-submit", 2m),
                _managerStaffId),
            CreateService(secondContext).AddDemandAdjustmentAsync(
                Adjustment(requestId, rowVersion, "same-submit", 2m),
                _managerStaffId));

        Assert.All(results, x => Assert.True(x.IsSuccess, x.Message));
        Assert.Single(results.Where(x => x.Data!.WasReplay));
        await using var verify = CreateContext();
        Assert.Equal(12m, await verify.RestockRequests
            .Where(x => x.RestockRequestId == requestId)
            .Select(x => x.RequestedProcurementQuantity)
            .SingleAsync());
        Assert.Equal(1, await verify.RestockRequestTransitions.CountAsync(x =>
            x.RestockRequestId == requestId
            && x.RequestKey == RestockRequestAuditKeys.DemandAdjustmentPrefix + "same-submit"));
    }

    private static AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString)
            .Options);

    private static RestockRequestService CreateService(AppDbContext context)
    {
        var conversion = new Mock<IUnitConversionService>();
        conversion.Setup(x => x.ConvertAsync(
                It.IsAny<int>(),
                It.IsAny<decimal>(),
                ProcurementUnitId,
                It.IsAny<int?>()))
            .Returns((int _, decimal quantity, int _, int? _) =>
                Task.FromResult(ServiceResult<decimal>.Success(quantity * 1000m)));
        return new RestockRequestService(
            context,
            new ScopeAuthorizationService(context),
            NullLogger<RestockRequestService>.Instance,
            conversion.Object);
    }

    private CreateProcurementDemandRequest Request(int ingredientId, string requestKey) => new()
    {
        StoreId = _storeId,
        IngredientId = ingredientId,
        RequestedProcurementQuantity = 10m,
        ProcurementUnitId = ProcurementUnitId,
        SourceReferenceId = requestKey,
        NeedByDate = DateTime.UtcNow.AddDays(3),
        Priority = RestockRequestPriorities.High,
        Note = "Nhu cầu kiểm thử SQL Server"
    };

    private static AddRestockDemandAdjustmentRequest Adjustment(
        int requestId,
        string rowVersion,
        string requestKey,
        decimal quantity) => new()
    {
        RestockRequestId = requestId,
        AdjustmentProcurementQuantity = quantity,
        ProcurementUnitId = ProcurementUnitId,
        Reason = "Bổ sung nhu cầu đồng thời",
        RowVersion = rowVersion,
        RequestKey = requestKey
    };

    private async Task SeedAsync(AppDbContext context)
    {
        var store = new Store
        {
            Name = "Chi nhánh SQL #237",
            Address = "SQL",
            Phone = "0237",
            Active = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Stores.Add(store);
        await context.SaveChangesAsync();
        _storeId = store.StoreId;

        var ingredients = new[]
        {
            new Ingredient
            {
                Code = "ISSUE237-CREATE",
                Name = "Nguyên liệu concurrent create",
                BaseUnitId = 1,
                Active = true
            },
            new Ingredient
            {
                Code = "ISSUE237-ADJUST",
                Name = "Nguyên liệu concurrent adjustment",
                BaseUnitId = 1,
                Active = true
            },
            new Ingredient
            {
                Code = "ISSUE237-DOUBLE",
                Name = "Nguyên liệu double submit",
                BaseUnitId = 1,
                Active = true
            }
        };
        context.Ingredients.AddRange(ingredients);
        await context.SaveChangesAsync();
        _createIngredientId = ingredients[0].IngredientId;
        _adjustIngredientId = ingredients[1].IngredientId;
        _doubleSubmitIngredientId = ingredients[2].IngredientId;

        var managerRole = new Role
        {
            Name = RoleConstants.StoreManager,
            Active = true,
            IsStoreLevel = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Roles.Add(managerRole);
        await context.SaveChangesAsync();

        var account = new Account
        {
            Email = "manager-issue237@test.local",
            PasswordHash = "x",
            Active = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Accounts.Add(account);
        await context.SaveChangesAsync();
        context.AccountRoles.Add(new AccountRole { AccountId = account.AccountId, RoleId = managerRole.RoleId });
        var staff = new Staff
        {
            AccountId = account.AccountId,
            StoreId = _storeId,
            FullName = "Quản lý chi nhánh #237",
            Active = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Staffs.Add(staff);
        await context.SaveChangesAsync();
        _managerStaffId = staff.StaffId;
    }
}
