using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces.Inventories;
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
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CafeChain.Tests;

[Trait("Category", "SqlServerIntegration")]
public sealed class PurchaseOrderSqlServerIssue178Tests : IAsyncLifetime
{
    private const string Database = "CafeChain_Issue178Tests";
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
                $"SQL Server integration environment unavailable for #178. Database={Database}. {ex.Message}",
                ex);
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SqlServer_ConcurrentPoReceipts_DoNotOverReceive()
    {
        var seeded = await SeedOrderAsync(10m, 6m, 6m);
        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        var firstLine = await firstContext.BranchReceiptLines
            .Include(x => x.BranchReceipt)
            .SingleAsync(x => x.BranchReceiptLineId == seeded.ReceiptLineIds[0]);
        var secondLine = await secondContext.BranchReceiptLines
            .Include(x => x.BranchReceipt)
            .SingleAsync(x => x.BranchReceiptLineId == seeded.ReceiptLineIds[1]);

        var results = await Task.WhenAll(
            CreateService(firstContext).RegisterReceiptPostingAsync(firstLine.BranchReceipt, firstLine, seeded.StaffId),
            CreateService(secondContext).RegisterReceiptPostingAsync(secondLine.BranchReceipt, secondLine, seeded.StaffId));

        Assert.Single(results.Where(x => x.IsSuccess));
        Assert.Single(results.Where(x => !x.IsSuccess));
        await using var verify = CreateContext();
        Assert.Equal(1, await verify.PurchaseOrderReceiptPostings.CountAsync());
        var quantities = await verify.PurchaseOrderReceiptPostings
            .Select(x => x.AcceptedBaseQuantity + x.RejectedBaseQuantity)
            .ToListAsync();
        Assert.Equal(6m, quantities.Sum());
    }

    [Fact]
    public async Task SqlServer_ReceiptReplay_DoesNotDuplicatePoPosting()
    {
        var seeded = await SeedOrderAsync(10m, 10m);
        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        var firstLine = await firstContext.BranchReceiptLines
            .Include(x => x.BranchReceipt)
            .SingleAsync(x => x.BranchReceiptLineId == seeded.ReceiptLineIds[0]);
        var secondLine = await secondContext.BranchReceiptLines
            .Include(x => x.BranchReceipt)
            .SingleAsync(x => x.BranchReceiptLineId == seeded.ReceiptLineIds[0]);

        var results = await Task.WhenAll(
            CreateService(firstContext).RegisterReceiptPostingAsync(firstLine.BranchReceipt, firstLine, seeded.StaffId),
            CreateService(secondContext).RegisterReceiptPostingAsync(secondLine.BranchReceipt, secondLine, seeded.StaffId));

        Assert.All(results, x => Assert.True(x.IsSuccess, x.Message));
        await using var verify = CreateContext();
        Assert.Equal(1, await verify.PurchaseOrderReceiptPostings.CountAsync());
        Assert.Equal(PurchaseOrderStatuses.Completed, (await verify.PurchaseOrders.SingleAsync()).Status);
    }

    [Fact]
    public async Task SqlServer_ReceiptCreatesPoAndRestockPostingsAtomically()
    {
        var seeded = await SeedOrderAsync(10m, 10m);
        await using var context = CreateContext();
        var line = await context.BranchReceiptLines
            .Include(x => x.BranchReceipt)
            .SingleAsync(x => x.BranchReceiptLineId == seeded.ReceiptLineIds[0]);
        await using var transaction = await context.Database.BeginTransactionAsync();

        var poResult = await CreateService(context)
            .RegisterReceiptPostingAsync(line.BranchReceipt, line, seeded.StaffId);
        var restockResult = await new RestockFulfillmentPostingService(context).RegisterAsync(
            new RegisterRestockFulfillmentPostingCommand
            {
                RestockRequestId = seeded.RestockRequestId,
                DestinationStoreId = seeded.StoreId,
                SourceDocumentType = RestockFulfillmentDocumentTypes.BranchReceipt,
                SourceDocumentId = line.BranchReceiptId,
                SourceDocumentLineId = line.BranchReceiptLineId,
                IngredientId = seeded.IngredientId,
                Quantity = line.ReceivedBaseQuantity,
                BaseUnitId = seeded.UnitId,
                ActorStaffId = seeded.StaffId,
                Reason = "Issue #178 atomic evidence"
            });
        Assert.True(poResult.IsSuccess, poResult.Message);
        Assert.True(restockResult.IsSuccess, restockResult.Message);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        await using var verify = CreateContext();
        Assert.Equal(1, await verify.PurchaseOrderReceiptPostings.CountAsync());
        Assert.Equal(1, await verify.RestockFulfillmentPostings.CountAsync());
        Assert.Equal(RestockRequestStatuses.Completed, (await verify.RestockRequests.SingleAsync()).Status);
    }

    private static PurchaseOrderService CreateService(AppDbContext context)
    {
        var conversion = new PhysicalUnitConversionService(
            context,
            NullLogger<PhysicalUnitConversionService>.Instance);
        return new PurchaseOrderService(
            context,
            conversion,
            new RestockAllocationService(context, new NoPurchaseOrderAllocationProvider()));
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<SeedResult> SeedOrderAsync(decimal ordered, params decimal[] receipts)
    {
        await using var context = CreateContext();
        var now = DateTime.UtcNow;
        var store = new Store
        {
            Name = "Store #178 SQL",
            Address = "Test",
            Phone = "0900178000",
            Active = true,
            CreatedAt = now
        };
        var account = new Account
        {
            Email = $"issue178-{Guid.NewGuid():N}@test.local",
            PasswordHash = "x",
            Active = true,
            CreatedAt = now
        };
        var unit = new Unit
        {
            UnitCode = $"u{Guid.NewGuid().ToString("N")[..8]}",
            Name = "Kilogram #178",
            Active = true
        };
        var supplier = new Supplier
        {
            Code = $"SUP-{Guid.NewGuid().ToString("N")[..8]}",
            Name = "Supplier #178 SQL",
            Active = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        context.AddRange(store, account, unit, supplier);
        await context.SaveChangesAsync();

        var staff = new Staff
        {
            AccountId = account.AccountId,
            StoreId = store.StoreId,
            FullName = "Warehouse #178 SQL",
            Active = true,
            CreatedAt = now,
            BaseSalary = 0
        };
        var ingredient = new Ingredient
        {
            Code = $"ING-{Guid.NewGuid().ToString("N")[..8]}",
            Name = "Ingredient #178 SQL",
            BaseUnitId = unit.UnitId,
            Active = true
        };
        context.AddRange(staff, ingredient);
        context.SupplierStores.Add(new SupplierStore
        {
            SupplierId = supplier.SupplierId,
            StoreId = store.StoreId,
            Active = true,
            CreatedAt = now,
            UpdatedAt = now
        });
        await context.SaveChangesAsync();

        var offer = new IngredientSupplier
        {
            IngredientId = ingredient.IngredientId,
            SupplierId = supplier.SupplierId,
            UnitId = unit.UnitId,
            PackageQuantity = 1m,
            CurrentPrice = 10m,
            MinimumOrderPackageCount = 1,
            LeadTimeDays = 2,
            Active = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        var request = new RestockRequest
        {
            StoreId = store.StoreId,
            IngredientId = ingredient.IngredientId,
            RequestedQuantity = ordered,
            Status = RestockRequestStatuses.Processing,
            Priority = RestockRequestPriorities.Normal,
            CreatedByStaffId = staff.StaffId,
            CreatedAt = now,
            UpdatedAt = now
        };
        context.AddRange(offer, request);
        await context.SaveChangesAsync();

        var order = new PurchaseOrder
        {
            Code = $"PO-178-{Guid.NewGuid().ToString("N")[..8]}",
            StoreId = store.StoreId,
            SupplierId = supplier.SupplierId,
            Status = PurchaseOrderStatuses.MarkedAsSent,
            OrderDate = now,
            CreatedByStaffId = staff.StaffId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        var orderLine = new PurchaseOrderLine
        {
            RestockRequestId = request.RestockRequestId,
            IngredientId = ingredient.IngredientId,
            IngredientSupplierId = offer.IngredientSupplierId,
            PackageUnitIdSnapshot = unit.UnitId,
            PackageQuantitySnapshot = 1m,
            PackagePriceSnapshot = 10m,
            PackageCount = ordered,
            OrderedBaseQuantity = ordered,
            PromisedLeadTimeDaysSnapshot = 2
        };
        order.Lines.Add(orderLine);
        context.PurchaseOrders.Add(order);
        await context.SaveChangesAsync();

        var receiptLineIds = new List<int>();
        for (var index = 0; index < receipts.Length; index++)
        {
            var receipt = new BranchReceipt
            {
                ReceiptCode = $"BR-178-{Guid.NewGuid().ToString("N")[..8]}",
                ReceiptKey = $"BR-178-{Guid.NewGuid():N}",
                StoreId = store.StoreId,
                SupplierId = supplier.SupplierId,
                Status = BranchReceiptStatuses.Draft,
                ReceivedAt = now,
                ReceivedByStaffId = staff.StaffId,
                CreatedAt = now,
                CreatedByStaffId = staff.StaffId
            };
            receipt.Lines.Add(new BranchReceiptLine
            {
                PurchaseOrderLineId = orderLine.PurchaseOrderLineId,
                RestockRequestId = request.RestockRequestId,
                IngredientId = ingredient.IngredientId,
                InputQuantity = receipts[index],
                InputUnitId = unit.UnitId,
                ReceivedBaseQuantity = receipts[index],
                BaseUnitId = unit.UnitId,
                BaseUnitCostSnapshot = 10m,
                LineTotalCost = receipts[index] * 10m,
                CreatedAt = now
            });
            context.BranchReceipts.Add(receipt);
            await context.SaveChangesAsync();
            receiptLineIds.Add(receipt.Lines.Single().BranchReceiptLineId);
        }

        return new SeedResult(
            store.StoreId,
            staff.StaffId,
            unit.UnitId,
            ingredient.IngredientId,
            request.RestockRequestId,
            receiptLineIds);
    }

    private sealed record SeedResult(
        int StoreId,
        int StaffId,
        int UnitId,
        int IngredientId,
        int RestockRequestId,
        IReadOnlyList<int> ReceiptLineIds);
}
