using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Inventories;
using CafeChain.Data;
using CafeChain.Models.Customers;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Procurement;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Inventories.Suppliers;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CafeChain.Tests;

[Trait("Category", "SqlServerIntegration")]
public sealed class PurchaseOrderBatchSqlServerIssue186Tests : IAsyncLifetime
{
    private const string Database = "CafeChain_Issue186Tests";
    private static string ConnectionString => SqlServerTestConnection.Create(Database);

    public async Task InitializeAsync()
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

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SqlServer_ConcurrentBatchCreate_OneLogicalBatch()
    {
        var seed = await SeedAsync();
        var requestKey = "SQL186-SAME-" + Guid.NewGuid().ToString("N");
        await using var first = CreateContext();
        await using var second = CreateContext();
        var results = await Task.WhenAll(
            Service(first).CreateAsync(Request(seed, requestKey), Actor(seed)),
            Service(second).CreateAsync(Request(seed, requestKey), Actor(seed)));

        Assert.All(results, x => Assert.True(x.IsSuccess, x.Message));
        Assert.Equal(results[0].Data!.PurchaseOrderBatchId, results[1].Data!.PurchaseOrderBatchId);
        await using var verify = CreateContext();
        Assert.Equal(1, await verify.PurchaseOrderBatches.CountAsync());
        Assert.Equal(2, await verify.PurchaseOrders.CountAsync());
        Assert.Equal(2, await verify.PurchaseOrderLineAllocations.CountAsync());
    }

    [Fact]
    public async Task SqlServer_ConcurrentAllocation_DoesNotOverAllocate()
    {
        var seed = await SeedAsync();
        await using var first = CreateContext();
        await using var second = CreateContext();
        var results = await Task.WhenAll(
            Service(first).CreateAsync(Request(seed, "SQL186-A-" + Guid.NewGuid().ToString("N")), Actor(seed)),
            Service(second).CreateAsync(Request(seed, "SQL186-B-" + Guid.NewGuid().ToString("N")), Actor(seed)));

        Assert.Single(results.Where(x => x.IsSuccess));
        Assert.Single(results.Where(x => !x.IsSuccess));
        await using var verify = CreateContext();
        var lines = await verify.PurchaseAdviceLines.AsNoTracking().ToListAsync();
        Assert.Equal(2, lines.Count);
        Assert.All(lines, line => Assert.Equal(5m, line.AllocatedToPoBaseQuantity));
        Assert.Equal(10m, await verify.PurchaseOrderLineAllocations.SumAsync(x => x.AllocatedBaseQuantity));
    }

    [Fact]
    public async Task SqlServer_BatchAndChildPos_AreAtomicAndTraceable()
    {
        var seed = await SeedAsync();
        await using var context = CreateContext();
        var result = await Service(context).CreateAsync(Request(seed, "SQL186-ATOMIC-" + Guid.NewGuid().ToString("N")), Actor(seed));
        Assert.True(result.IsSuccess, result.Message);

        await using var verify = CreateContext();
        var allocations = await verify.PurchaseOrderLineAllocations.AsNoTracking().ToListAsync();
        Assert.Equal(2, allocations.Count);
        foreach (var allocation in allocations)
        {
            Assert.True(await verify.PurchaseOrderBatchLines.AnyAsync(x => x.PurchaseOrderBatchLineId == allocation.PurchaseOrderBatchLineId));
            Assert.True(await verify.PurchaseOrders.AnyAsync(x => x.PurchaseOrderId == allocation.PurchaseOrderId));
            Assert.True(await verify.PurchaseOrderLines.AnyAsync(x => x.PurchaseOrderLineId == allocation.PurchaseOrderLineId));
            Assert.True(await verify.PurchaseAdviceLines.AnyAsync(x => x.PurchaseAdviceLineId == allocation.PurchaseAdviceLineId));
        }
    }

    private static PurchaseOrderBatchService Service(AppDbContext context)
    {
        var scope = new Mock<IScopeAuthorizationService>();
        scope.Setup(x => x.CanAccessStoreAsync(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(true);
        var physical = new Mock<IPhysicalUnitConversionService>();
        physical.Setup(x => x.ConvertAsync(It.IsAny<decimal>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((decimal quantity, int _, int _) => ServiceResult<decimal>.Success(quantity));
        return new PurchaseOrderBatchService(context,
            new PurchaseAdviceConsolidationService(context, scope.Object, physical.Object), scope.Object);
    }

    private static CreatePurchaseOrderBatchRequest Request(Seed seed, string key) => new()
    {
        SupplierId = seed.SupplierId,
        RequestKey = key,
        Lines = new()
        {
            new() { PurchaseAdviceLineId = seed.LineId, IngredientSupplierId = seed.OfferId, PackageCount = 5, RowVersion = seed.RowVersion },
            new() { PurchaseAdviceLineId = seed.SecondLineId, IngredientSupplierId = seed.OfferId, PackageCount = 5, RowVersion = seed.SecondRowVersion }
        }
    };

    private static AdminActorContext Actor(Seed seed) => new()
    {
        StaffId = seed.StaffId,
        RoleNames = new[] { RoleConstants.AccountantWarehouse }
    };

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options);

    private static async Task<Seed> SeedAsync()
    {
        await using var db = CreateContext();
        var now = DateTime.UtcNow;
        var store = new Store { Name = "Store 186 SQL", Address = "SQL", Phone = "0900186001", Active = true, CreatedAt = now };
        var secondStore = new Store { Name = "Store 186 SQL B", Address = "SQL B", Phone = "0900186002", Active = true, CreatedAt = now };
        var unit = new Unit { UnitCode = "kg" + Guid.NewGuid().ToString("N")[..6], Name = "kg", Active = true };
        var ingredient = new Ingredient { Code = "I186" + Guid.NewGuid().ToString("N")[..6], Name = "Coffee 186 SQL", Active = true, BaseUnit = unit };
        var supplier = new Supplier { Code = "S186" + Guid.NewGuid().ToString("N")[..6], Name = "Supplier 186 SQL", Active = true, CreatedAt = now, UpdatedAt = now };
        var account = new Account { Email = Guid.NewGuid() + "@test.local", PasswordHash = "x", Active = true, CreatedAt = now };
        db.AddRange(store, secondStore, ingredient, supplier, account);
        await db.SaveChangesAsync();
        var staff = new Staff
        {
            AccountId = account.AccountId,
            StoreId = store.StoreId,
            FullName = "Warehouse 186 SQL",
            Active = true,
            CreatedAt = now,
        };
        var offer = new IngredientSupplier { IngredientId = ingredient.IngredientId, SupplierId = supplier.SupplierId, UnitId = unit.UnitId, PackageQuantity = 1m, CurrentPrice = 15000m, MinimumOrderPackageCount = 1, LeadTimeDays = 1, Active = true, CreatedAt = now, UpdatedAt = now };
        db.AddRange(staff, offer,
            new SupplierStore { SupplierId = supplier.SupplierId, StoreId = store.StoreId, Active = true, CreatedAt = now, UpdatedAt = now },
            new SupplierStore { SupplierId = supplier.SupplierId, StoreId = secondStore.StoreId, Active = true, CreatedAt = now, UpdatedAt = now });
        await db.SaveChangesAsync();
        var restock = new RestockRequest { StoreId = store.StoreId, IngredientId = ingredient.IngredientId, RequestedQuantity = 5m, Status = RestockRequestStatuses.Processing, Priority = RestockRequestPriorities.Normal, CreatedByStaffId = staff.StaffId, CreatedAt = now, UpdatedAt = now };
        var secondRestock = new RestockRequest { StoreId = secondStore.StoreId, IngredientId = ingredient.IngredientId, RequestedQuantity = 5m, Status = RestockRequestStatuses.Processing, Priority = RestockRequestPriorities.Normal, CreatedByStaffId = staff.StaffId, CreatedAt = now, UpdatedAt = now };
        db.AddRange(restock, secondRestock); await db.SaveChangesAsync();
        var advice = new PurchaseAdvice
        {
            AdviceNumber = "PA-186-SQL", RequestKey = Guid.NewGuid().ToString("N"), StoreId = store.StoreId, RequestedByStaffId = staff.StaffId,
            Status = PurchaseAdviceStatuses.Submitted, NeededByDate = now.Date.AddDays(2), Priority = PurchaseAdvicePriorities.Normal,
            SubmittedAtUtc = now, CreatedAtUtc = now, UpdatedAtUtc = now,
            Lines = new List<PurchaseAdviceLine> { new() { RestockRequestId = restock.RestockRequestId, IngredientId = ingredient.IngredientId, RequestedPurchaseBaseQuantity = 5m, BaseUnitId = unit.UnitId, NeededByDate = now.Date.AddDays(2), IsActiveReservation = true } }
        };
        var secondAdvice = new PurchaseAdvice
        {
            AdviceNumber = "PA-186-SQL-B", RequestKey = Guid.NewGuid().ToString("N"), StoreId = secondStore.StoreId, RequestedByStaffId = staff.StaffId,
            Status = PurchaseAdviceStatuses.Submitted, NeededByDate = now.Date.AddDays(2), Priority = PurchaseAdvicePriorities.Normal,
            SubmittedAtUtc = now, CreatedAtUtc = now, UpdatedAtUtc = now,
            Lines = new List<PurchaseAdviceLine> { new() { RestockRequestId = secondRestock.RestockRequestId, IngredientId = ingredient.IngredientId, RequestedPurchaseBaseQuantity = 5m, BaseUnitId = unit.UnitId, NeededByDate = now.Date.AddDays(2), IsActiveReservation = true } }
        };
        db.AddRange(advice, secondAdvice); await db.SaveChangesAsync();
        var line = advice.Lines.Single();
        var secondLine = secondAdvice.Lines.Single();
        return new Seed(staff.StaffId, supplier.SupplierId, offer.IngredientSupplierId,
            line.PurchaseAdviceLineId, Convert.ToBase64String(line.RowVersion),
            secondLine.PurchaseAdviceLineId, Convert.ToBase64String(secondLine.RowVersion));
    }

    private sealed record Seed(int StaffId, int SupplierId, int OfferId, int LineId, string RowVersion, int SecondLineId, string SecondRowVersion);
}
