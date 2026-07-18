using System.Data;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Services.Inventories;
using CafeChain.Data;
using CafeChain.Models.Customers;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Inventories.Transfers;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CafeChain.Tests;

[Trait("Category", "SqlServerIntegration")]
public sealed class PurchaseAdviceSqlServerIssue184Tests : IAsyncLifetime
{
    private const string Database = "CafeChain_Issue184Tests";
    private static string ConnectionString => SqlServerTestConnection.Create(Database);

    public async Task InitializeAsync()
    {
        try
        {
            await using var master = new SqlConnection(SqlServerTestConnection.MasterConnectionString());
            await master.OpenAsync();
            await using var command = master.CreateCommand();
            command.CommandText = $"IF DB_ID(N'{Database}') IS NULL CREATE DATABASE [{Database}];";
            await command.ExecuteNonQueryAsync();
            await using var context = CreateContext();
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"SQL Server integration environment unavailable for #184. Database={Database}. {ex.Message}", ex);
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SqlServer_ConcurrentAdviceCreate_DoesNotExceedRestockRemaining()
    {
        var seed = await SeedAsync(10m);
        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        var results = await Task.WhenAll(
            CreateService(firstContext).CreateAsync(CreateRequest(seed, 6m), Manager(seed)),
            CreateService(secondContext).CreateAsync(CreateRequest(seed, 6m), Manager(seed)));

        Assert.Single(results.Where(x => x.IsSuccess));
        Assert.Single(results.Where(x => !x.IsSuccess));
        await using var verify = CreateContext();
        Assert.Equal(6m, await verify.PurchaseAdviceLines.SumAsync(x => x.RequestedPurchaseBaseQuantity));
        Assert.Single(await verify.PurchaseAdviceLines.Where(x => x.IsActiveReservation).ToListAsync());
    }

    [Fact]
    public async Task SqlServer_ConcurrentAdviceAndTransfer_OneWinner()
    {
        var seed = await SeedAsync(10m);
        await using var adviceContext = CreateContext();
        await using var transferContext = CreateContext();
        var results = await Task.WhenAll(
            CreateAdviceOutcomeAsync(adviceContext, seed, 6m),
            CreateTransferOutcomeAsync(transferContext, seed, 6m));

        Assert.Single(results.Where(x => x));
        await using var verify = CreateContext();
        var advice = await verify.PurchaseAdviceLines.SumAsync(x => (decimal?)x.RequestedPurchaseBaseQuantity) ?? 0m;
        var transfer = await verify.InventoryTransferDetails.SumAsync(x => (decimal?)x.BaseQuantity) ?? 0m;
        Assert.True(advice + transfer <= 10m);
    }

    [Fact]
    public async Task SqlServer_DoubleSubmit_OneTransition()
    {
        var seed = await SeedAsync(10m);
        PurchaseAdviceDetailDto created;
        await using (var context = CreateContext())
            created = (await CreateService(context).CreateAsync(CreateRequest(seed, 5m), Manager(seed))).Data!;
        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        var results = await Task.WhenAll(
            CreateService(firstContext).SubmitAsync(created.PurchaseAdviceId, new PurchaseAdviceTransitionRequest { RowVersion = created.RowVersion }, Manager(seed)),
            CreateService(secondContext).SubmitAsync(created.PurchaseAdviceId, new PurchaseAdviceTransitionRequest { RowVersion = created.RowVersion }, Manager(seed)));

        Assert.All(results, x => Assert.True(x.IsSuccess, x.Message));
        await using var verify = CreateContext();
        Assert.Equal(1, await verify.PurchaseAdviceTransitions.CountAsync(x => x.NewStatus == PurchaseAdviceStatuses.Submitted));
    }

    [Fact]
    public async Task SqlServer_StaleAdviceUpdateRejected()
    {
        var seed = await SeedAsync(10m);
        PurchaseAdviceDetailDto created;
        await using (var createContext = CreateContext())
            created = (await CreateService(createContext).CreateAsync(CreateRequest(seed, 5m), Manager(seed))).Data!;
        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        var firstService = CreateService(firstContext);
        var secondService = CreateService(secondContext);
        var first = await firstService.UpdateAsync(UpdateRequest(created, 6m), Manager(seed));
        var stale = await secondService.UpdateAsync(UpdateRequest(created, 7m), Manager(seed));

        Assert.True(first.IsSuccess, first.Message);
        Assert.False(stale.IsSuccess);
        Assert.Equal(PurchaseAdviceErrorCodes.StaleVersion, stale.ErrorCode);
    }

    private static async Task<bool> CreateAdviceOutcomeAsync(AppDbContext context, Seed seed, decimal quantity) =>
        (await CreateService(context).CreateAsync(CreateRequest(seed, quantity), Manager(seed))).IsSuccess;

    private static async Task<bool> CreateTransferOutcomeAsync(AppDbContext context, Seed seed, decimal quantity)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var allocation = new RestockAllocationService(context, new PurchaseOrderQuantityProvider(context));
        var validation = await allocation.ValidateAllocationAsync(new CafeChain.Application.DTOs.Admin.RestockRequests.RestockAllocationValidationRequest
        {
            RestockRequestId = seed.RestockRequestId,
            DestinationStoreId = seed.StoreId,
            IngredientId = seed.IngredientId,
            AllocationQuantity = quantity,
            ActorStaffId = seed.ManagerId,
            ActorRoles = new[] { RoleConstants.StoreManager }
        });
        if (!validation.IsSuccess) { await transaction.RollbackAsync(); return false; }
        context.InventoryTransfers.Add(new InventoryTransfer
        {
            Code = "TR-184-" + Guid.NewGuid().ToString("N")[..8], FromStoreId = seed.SourceStoreId, ToStoreId = seed.StoreId,
            Type = InventoryTransferType.STORE_TO_STORE, Purpose = InventoryTransferPurpose.REPLENISHMENT,
            Status = InventoryTransferStatus.DRAFT, DocumentDate = DateTime.UtcNow, CreatedByStaffId = seed.ManagerId, CreatedAt = DateTime.UtcNow,
            Details = new List<InventoryTransferDetail> { new() { RestockRequestId = seed.RestockRequestId, IngredientId = seed.IngredientId, UnitId = seed.UnitId, Quantity = quantity, BaseQuantity = quantity } }
        });
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return true;
    }

    private static UpdatePurchaseAdviceRequest UpdateRequest(PurchaseAdviceDetailDto detail, decimal quantity) => new()
    {
        PurchaseAdviceId = detail.PurchaseAdviceId,
        NeededByDate = detail.NeededByDate,
        Priority = detail.Priority,
        RowVersion = detail.RowVersion,
        Lines = detail.Lines.Select(x => new UpdatePurchaseAdviceLineRequest
        {
            PurchaseAdviceLineId = x.PurchaseAdviceLineId,
            RequestedPurchaseBaseQuantity = quantity,
            NeededByDate = x.NeededByDate,
            RowVersion = x.RowVersion
        }).ToList()
    };

    private static PurchaseAdviceService CreateService(AppDbContext context)
    {
        var scope = new Mock<IScopeAuthorizationService>();
        return new PurchaseAdviceService(context, scope.Object);
    }

    private static CreatePurchaseAdviceRequest CreateRequest(Seed seed, decimal quantity) => new()
    {
        StoreId = seed.StoreId, RequestKey = Guid.NewGuid().ToString("N"), NeededByDate = DateTime.Today.AddDays(2), Priority = PurchaseAdvicePriorities.Normal,
        Lines = new List<CreatePurchaseAdviceLineRequest> { new() { RestockRequestId = seed.RestockRequestId, RequestedPurchaseBaseQuantity = quantity, RestockRowVersion = seed.RestockRowVersion } }
    };

    private static AdminActorContext Manager(Seed seed) => new() { StaffId = seed.ManagerId, StoreId = seed.StoreId, RoleNames = new[] { RoleConstants.StoreManager } };

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options);

    private static async Task<Seed> SeedAsync(decimal requested)
    {
        await using var context = CreateContext();
        var now = DateTime.UtcNow;
        var store = new Store { Name = "Store #184 SQL", Address = "Test", Phone = "0900184001", Active = true, CreatedAt = now };
        var sourceStore = new Store { Name = "Source #184 SQL", Address = "Test", Phone = "0900184002", Active = true, CreatedAt = now };
        var account = new Account { Email = $"issue184-{Guid.NewGuid():N}@test.local", PasswordHash = "x", Active = true, CreatedAt = now };
        var unit = new Unit { UnitCode = "u" + Guid.NewGuid().ToString("N")[..7], Name = "kg", Active = true };
        context.AddRange(store, sourceStore, account, unit); await context.SaveChangesAsync();
        var manager = new Staff { AccountId = account.AccountId, StoreId = store.StoreId, FullName = "Manager #184 SQL", Active = true, CreatedAt = now, BaseSalary = 0 };
        var ingredient = new Ingredient { Code = "ING-" + Guid.NewGuid().ToString("N")[..8], Name = "Coffee #184 SQL", BaseUnitId = unit.UnitId, Active = true };
        context.AddRange(manager, ingredient); await context.SaveChangesAsync();
        var request = new RestockRequest { StoreId = store.StoreId, IngredientId = ingredient.IngredientId, RequestedQuantity = requested, Status = RestockRequestStatuses.Processing, Priority = RestockRequestPriorities.Normal, CreatedByStaffId = manager.StaffId, CreatedAt = now, UpdatedAt = now };
        context.RestockRequests.Add(request); await context.SaveChangesAsync();
        return new Seed(store.StoreId, sourceStore.StoreId, manager.StaffId, unit.UnitId, ingredient.IngredientId, request.RestockRequestId, Convert.ToBase64String(request.RowVersion));
    }

    private sealed record Seed(int StoreId, int SourceStoreId, int ManagerId, int UnitId, int IngredientId, int RestockRequestId, string RestockRowVersion);
}
