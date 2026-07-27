using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Interfaces.Security;
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
public sealed class SupplierQualitySqlServerIssue179Tests : IAsyncLifetime
{
    private const string Database = "CafeChain_Issue179Tests";
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
                $"SQL Server integration environment unavailable for #179. Database={Database}. {ex.Message}",
                ex);
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SqlServer_ConcurrentIssueResolution_AllowsExactlyOneTransition()
    {
        var seeded = await SeedIssueAsync();
        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        var firstIssue = await firstContext.SupplierReceiptIssues
            .SingleAsync(x => x.SupplierReceiptIssueId == seeded.IssueId);
        var secondIssue = await secondContext.SupplierReceiptIssues
            .SingleAsync(x => x.SupplierReceiptIssueId == seeded.IssueId);
        var expectedVersion = Convert.ToBase64String(firstIssue.RowVersion);
        Assert.Equal(expectedVersion, Convert.ToBase64String(secondIssue.RowVersion));

        var results = await Task.WhenAll(
            CreateService(firstContext).TransitionAsync(seeded.IssueId,
                new SupplierReceiptIssueTransitionRequest
                {
                    TargetStatus = SupplierReceiptIssueStatuses.Resolved,
                    Reason = "Đã xác minh và thống nhất phương án xử lý.",
                    RowVersion = expectedVersion
                }, seeded.StaffId, new[] { RoleConstants.StoreManager }),
            CreateService(secondContext).TransitionAsync(seeded.IssueId,
                new SupplierReceiptIssueTransitionRequest
                {
                    TargetStatus = SupplierReceiptIssueStatuses.Dismissed,
                    Reason = "Chứng từ đối chiếu xác nhận đây không phải lỗi nhà cung cấp.",
                    RowVersion = expectedVersion
                }, seeded.StaffId, new[] { RoleConstants.StoreManager }));

        Assert.Single(results.Where(x => x.IsSuccess));
        Assert.Single(results.Where(x => !x.IsSuccess));
        Assert.Contains(results.Single(x => !x.IsSuccess).Message, new[]
        {
            "Sự cố vừa được cập nhật bởi người khác. Vui lòng tải lại."
        });

        await using var verify = CreateContext();
        var stored = await verify.SupplierReceiptIssues.AsNoTracking().SingleAsync();
        Assert.Contains(stored.Status, new[]
        {
            SupplierReceiptIssueStatuses.Resolved,
            SupplierReceiptIssueStatuses.Dismissed
        });
        Assert.Equal(2, await verify.SupplierReceiptIssueTransitions.CountAsync());
    }

    private static SupplierQualityService CreateService(AppDbContext context)
    {
        var scope = new Mock<IScopeAuthorizationService>();
        scope.Setup(x => x.CanAccessStoreAsync(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(true);
        return new SupplierQualityService(context, scope.Object);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<SeedResult> SeedIssueAsync()
    {
        await using var context = CreateContext();
        var now = DateTime.UtcNow;
        var store = new Store
        {
            Name = "Store #179 SQL",
            Address = "Test",
            Phone = "0900179000",
            Active = true,
            CreatedAt = now
        };
        var account = new Account
        {
            Email = $"quality179-{Guid.NewGuid():N}@test.local",
            PasswordHash = "x",
            Active = true,
            CreatedAt = now
        };
        var unit = new Unit
        {
            UnitCode = $"u{Guid.NewGuid().ToString("N")[..8]}",
            Name = "Kilogram #179 SQL",
            Active = true
        };
        var supplier = new Supplier
        {
            Code = $"SUP-{Guid.NewGuid().ToString("N")[..8]}",
            Name = "Supplier #179 SQL",
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
            FullName = "Quality Manager #179 SQL",
            Active = true,
            CreatedAt = now,
        };
        var ingredient = new Ingredient
        {
            Code = $"ING-{Guid.NewGuid().ToString("N")[..8]}",
            Name = "Ingredient #179 SQL",
            BaseUnitId = unit.UnitId,
            Active = true
        };
        context.AddRange(staff, ingredient);
        await context.SaveChangesAsync();

        var offer = new IngredientSupplier
        {
            IngredientId = ingredient.IngredientId,
            SupplierId = supplier.SupplierId,
            UnitId = unit.UnitId,
            PackageQuantity = 1m,
            CurrentPrice = 10m,
            MinimumOrderPackageCount = 1,
            LeadTimeDays = 1,
            Active = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        context.IngredientSuppliers.Add(offer);
        await context.SaveChangesAsync();

        var order = new PurchaseOrder
        {
            Code = $"PO-179-{Guid.NewGuid().ToString("N")[..8]}",
            StoreId = store.StoreId,
            SupplierId = supplier.SupplierId,
            Status = PurchaseOrderStatuses.Completed,
            OrderDate = now.AddDays(-2),
            ExpectedDeliveryAtUtc = now.AddDays(-1),
            CreatedByStaffId = staff.StaffId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            CompletedAtUtc = now
        };
        var orderLine = new PurchaseOrderLine
        {
            IngredientId = ingredient.IngredientId,
            IngredientSupplierId = offer.IngredientSupplierId,
            PackageUnitIdSnapshot = unit.UnitId,
            PackageQuantitySnapshot = 1m,
            PackagePriceSnapshot = 10m,
            PackageCount = 10m,
            OrderedPackageCount = 10m,
            UnitPricePerPackage = 10m,
            OrderedBaseQuantity = 10m,
            PromisedLeadTimeDaysSnapshot = 1
        };
        order.Lines.Add(orderLine);
        context.PurchaseOrders.Add(order);
        await context.SaveChangesAsync();

        var receipt = new BranchReceipt
        {
            ReceiptCode = $"BR-179-{Guid.NewGuid().ToString("N")[..8]}",
            ReceiptKey = $"BR-179-{Guid.NewGuid():N}",
            StoreId = store.StoreId,
            SupplierId = supplier.SupplierId,
            Status = BranchReceiptStatuses.Confirmed,
            ReceivedAt = now,
            ReceivedByStaffId = staff.StaffId,
            ConfirmedAt = now,
            ConfirmedByStaffId = staff.StaffId,
            CreatedAt = now,
            CreatedByStaffId = staff.StaffId
        };
        receipt.Lines.Add(new BranchReceiptLine
        {
            PurchaseOrderLineId = orderLine.PurchaseOrderLineId,
            IngredientId = ingredient.IngredientId,
            InputQuantity = 10m,
            InputUnitId = unit.UnitId,
            ReceivedBaseQuantity = 9m,
            RejectedBaseQuantity = 1m,
            BaseUnitId = unit.UnitId,
            BaseUnitCostSnapshot = 10m,
            LineTotalCost = 90m,
            CreatedAt = now
        });
        context.BranchReceipts.Add(receipt);
        await context.SaveChangesAsync();

        var created = await CreateService(context).CreateIssueAsync(new CreateSupplierReceiptIssueRequest
        {
            BranchReceiptLineId = receipt.Lines.Single().BranchReceiptLineId,
            IssueType = SupplierReceiptIssueTypes.QualityFailure,
            AffectedBaseQuantity = 1m,
            Description = "Sự cố dùng để kiểm chứng optimistic concurrency trên SQL Server."
        }, staff.StaffId, new[] { RoleConstants.StoreManager });
        Assert.True(created.IsSuccess, created.Message);
        return new SeedResult(created.Data!.SupplierReceiptIssueId, staff.StaffId);
    }

    private sealed record SeedResult(int IssueId, int StaffId);
}
