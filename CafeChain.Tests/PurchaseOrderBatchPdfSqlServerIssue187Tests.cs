using System.Collections.Concurrent;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Services.Inventories;
using CafeChain.Data;
using CafeChain.Models.Customers;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Procurement;
using CafeChain.Models.Inventories.Suppliers;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CafeChain.Tests;

[Trait("Category", "SqlServerIntegration")]
public sealed class PurchaseOrderBatchPdfSqlServerIssue187Tests : IAsyncLifetime
{
    private const string Database = "CafeChain_Issue187Tests";
    private static string ConnectionString => SqlServerTestConnection.Create(Database);
    private readonly SharedStorage _storage = new();

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
    public async Task SqlServer_ConcurrentPdfGeneration_OneRevisionNumber()
    {
        var seed = await SeedAsync();
        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        var results = await Task.WhenAll(
            Service(firstContext).GenerateAsync(seed.BatchId, Actor(seed.StaffId)),
            Service(secondContext).GenerateAsync(seed.BatchId, Actor(seed.StaffId)));

        Assert.All(results, result => Assert.True(result.IsSuccess, result.Message));
        Assert.Equal(results[0].Data!.RevisionId, results[1].Data!.RevisionId);
        await using var verify = CreateContext();
        var revision = Assert.Single(await verify.PurchaseOrderBatchDocumentRevisions.AsNoTracking().ToListAsync());
        Assert.Equal(1, revision.RevisionNumber);
        Assert.Single(_storage.Files);
    }

    private PurchaseOrderBatchDocumentService Service(AppDbContext context)
    {
        var scope = new Mock<IScopeAuthorizationService>();
        scope.Setup(x => x.CanAccessStoreAsync(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(true);
        return new(context, new DeterministicRenderer(), _storage, scope.Object);
    }

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options);

    private static async Task<Seed> SeedAsync()
    {
        await using var db = CreateContext();
        var now = DateTime.UtcNow;
        var store = new Store { Name = "Store 187 SQL", Address = "SQL", Phone = "0900187001", Active = true, CreatedAt = now };
        var unit = new CafeChain.Models.Inventories.Ingredients.Unit { UnitCode = "kg" + Guid.NewGuid().ToString("N")[..6], Name = "kg", Active = true };
        var ingredient = new Ingredient { Code = "I187" + Guid.NewGuid().ToString("N")[..6], Name = "Cà phê SQL", Active = true, BaseUnit = unit };
        var supplier = new Supplier { Code = "S187" + Guid.NewGuid().ToString("N")[..6], Name = "Supplier 187 SQL", TaxCode = "0318718718", Active = true, CreatedAt = now, UpdatedAt = now };
        var account = new Account { Email = Guid.NewGuid() + "@test.local", PasswordHash = "x", Active = true, CreatedAt = now };
        db.AddRange(store, ingredient, supplier, account);
        await db.SaveChangesAsync();
        var staff = new Staff { AccountId = account.AccountId, StoreId = store.StoreId, FullName = "Owner 187 SQL", Active = true, CreatedAt = now};
        var offer = new IngredientSupplier
        {
            IngredientId = ingredient.IngredientId, SupplierId = supplier.SupplierId, UnitId = unit.UnitId,
            PackageQuantity = 1m, CurrentPrice = 100000m, MinimumOrderPackageCount = 1, Active = true, CreatedAt = now, UpdatedAt = now
        };
        db.AddRange(staff, offer);
        await db.SaveChangesAsync();
        var batch = new PurchaseOrderBatch
        {
            BatchNumber = "POB-187-SQL", RequestKey = Guid.NewGuid().ToString("N"), SupplierId = supplier.SupplierId,
            Status = PurchaseOrderBatchStatuses.Approved, Currency = "VND", ExpectedDeliveryFrom = now.Date.AddDays(1), ExpectedDeliveryTo = now.Date.AddDays(2),
            CreatedByStaffId = staff.StaffId, ApprovedByStaffId = staff.StaffId, ApprovedAtUtc = now, CreatedAtUtc = now, UpdatedAtUtc = now,
            Lines = new List<PurchaseOrderBatchLine>
            {
                new() { IngredientId = ingredient.IngredientId, IngredientSupplierId = offer.IngredientSupplierId, PackageUnitId = unit.UnitId,
                    PackageQuantitySnapshot = 1, TotalPackageCount = 2, TotalBaseQuantity = 2, PackagePriceSnapshot = 100000,
                    LineTotal = 200000, Currency = "VND" }
            },
            ChildPurchaseOrders = new List<PurchaseOrder>
            {
                new()
                {
                    Code = "PO-187-SQL", StoreId = store.StoreId, SupplierId = supplier.SupplierId, Status = PurchaseOrderStatuses.Approved,
                    OrderDate = now, ExpectedDeliveryAtUtc = now.Date.AddDays(2), CreatedByStaffId = staff.StaffId, ApprovedByStaffId = staff.StaffId,
                    CreatedAtUtc = now, UpdatedAtUtc = now, ApprovedAtUtc = now,
                    Lines = new List<PurchaseOrderLine>
                    {
                        new() { IngredientId = ingredient.IngredientId, IngredientSupplierId = offer.IngredientSupplierId, PackageUnitIdSnapshot = unit.UnitId,
                            PackageQuantitySnapshot = 1, PackagePriceSnapshot = 100000, PackageCount = 2, OrderedBaseQuantity = 2, PromisedLeadTimeDaysSnapshot = 1 }
                    }
                }
            }
        };
        db.Add(batch);
        await db.SaveChangesAsync();
        return new(batch.PurchaseOrderBatchId, staff.StaffId);
    }

    private static AdminActorContext Actor(int staffId) => new() { StaffId = staffId, RoleNames = new[] { RoleConstants.BusinessOwner } };
    private sealed record Seed(int BatchId, int StaffId);

    private sealed class DeterministicRenderer : IPurchaseOrderBatchPdfRenderer
    {
        public byte[] Render(PurchaseOrderBatchDocumentSnapshot snapshot, int revisionNumber, DateTime generatedAtUtc, string contentHash) =>
            System.Text.Encoding.UTF8.GetBytes($"%PDF-{revisionNumber}-{contentHash}");
    }

    private sealed class SharedStorage : IPurchaseOrderBatchDocumentStorage
    {
        public ConcurrentDictionary<string, byte[]> Files { get; } = new();
        public Task SaveAsync(string storageReference, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
        {
            if (!Files.TryAdd(storageReference, content.ToArray())) throw new IOException("Duplicate storage reference.");
            return Task.CompletedTask;
        }
        public Task<byte[]?> ReadAsync(string storageReference, CancellationToken cancellationToken = default) =>
            Task.FromResult(Files.TryGetValue(storageReference, out var bytes) ? bytes : null);
        public Task DeleteAsync(string storageReference, CancellationToken cancellationToken = default)
        {
            Files.TryRemove(storageReference, out _);
            return Task.CompletedTask;
        }
    }
}
