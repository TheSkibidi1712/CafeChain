using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.RestockRequests;
using CafeChain.Application.DTOs.Admin.Suppliers;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Admin.Suppliers;
using CafeChain.Application.Services.Inventories;
using CafeChain.Application.Services.Security;
using CafeChain.Data;
using CafeChain.Infrastrusture.Repositories.Admin.Suppliers;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Inventories.Suppliers;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CafeChain.Tests;

[Trait("Category", "SqlServerIntegration")]
public sealed class SupplierFoundationPhaseOneSqlServerTests : IAsyncLifetime
{
    private const string Database = "CafeChain_SupplierFoundationTests";
    private static readonly string[] WarehouseRoles = { RoleConstants.AccountantWarehouse };

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
                $"BLOCKED_ON_SQL_SERVER: Supplier Foundation database unavailable. Database={Database}. {ex.Message}",
                ex);
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SqlServer_ConcurrentSupplierCodeGeneration()
    {
        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        var first = CreateSupplierService(firstContext);
        var second = CreateSupplierService(secondContext);

        var ids = await Task.WhenAll(
            first.CreateAsync(NewSupplier("Concurrent A", "0910000001")),
            second.CreateAsync(NewSupplier("Concurrent B", "0910000002")));

        await using var verify = CreateContext();
        var codes = await verify.Suppliers
            .Where(x => ids.Contains(x.SupplierId))
            .Select(x => x.Code)
            .ToListAsync();
        Assert.Equal(2, codes.Count);
        Assert.Equal(2, codes.Distinct().Count());
    }

    [Fact]
    public async Task SqlServer_OneCurrentPricePerOffer()
    {
        await using var context = CreateContext();
        var current = await context.IngredientSupplierPriceHistories
            .AsNoTracking()
            .FirstAsync(x => x.IsCurrent);

        context.IngredientSupplierPriceHistories.Add(new IngredientSupplierPriceHistory
        {
            IngredientSupplierId = current.IngredientSupplierId,
            Price = current.Price + 1,
            PackageQuantity = current.PackageQuantity,
            PackageUnitId = current.PackageUnitId,
            EffectiveDate = DateTime.UtcNow,
            IsCurrent = true,
            Note = "Duplicate current must fail",
            CreatedAtUtc = DateTime.UtcNow
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task SqlServer_SupplierStoreUnique()
    {
        await using var context = CreateContext();
        context.SupplierStores.AddRange(
            NewSupplierStore(1, 1),
            NewSupplierStore(1, 1));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task SqlServer_ReceiptConfirmIdempotent_AndPackageConversionProducesCorrectFifoCost()
    {
        await using var context = CreateContext();
        var actorStaffId = await context.Staffs.OrderBy(x => x.StaffId).Select(x => x.StaffId).FirstAsync();
        const int storeId = 1;
        const int supplierId = 1;
        var catalog = await SeedReceiptCatalogAsync(context, storeId, supplierId);
        var requestId = await SeedProcessingRequestAsync(
            context, storeId, actorStaffId, catalog.IngredientId);
        var service = CreateReceiptService(context);

        var draft = await service.CreateDraftAsync(new CreateBranchReceiptRequest
        {
            StoreId = storeId,
            SupplierId = supplierId,
            ReceiptKey = "sql-supplier-2x24",
            Lines =
            {
                new CreateBranchReceiptLineInput
                {
                    RestockRequestId = requestId,
                    IngredientSupplierId = catalog.OfferId,
                    ActualReceivedQuantity = 2,
                    InputUnitId = catalog.UnitId,
                    ActualPackagePrice = 1
                }
            }
        }, actorStaffId, WarehouseRoles);
        Assert.True(draft.IsSuccess, draft.Message);

        var first = await service.ConfirmAsync(
            draft.Data!.BranchReceiptId, actorStaffId, storeId, WarehouseRoles, draft.Data.RowVersion);
        var replay = await service.ConfirmAsync(
            draft.Data.BranchReceiptId, actorStaffId, storeId, WarehouseRoles, draft.Data.RowVersion);
        Assert.True(first.IsSuccess, first.Message);
        Assert.False(first.Data!.WasReplay);
        Assert.True(replay.IsSuccess, replay.Message);
        Assert.True(replay.Data!.WasReplay);

        Assert.Equal(1, await context.InventoryTransactions.CountAsync(x =>
            x.Type == InventoryTransactionTypeEnum.BRANCH_RECEIPT_IN
            && x.StoreInventory.IngredientId == catalog.IngredientId));
        var transaction = await context.InventoryTransactions
            .SingleAsync(x => x.Type == InventoryTransactionTypeEnum.BRANCH_RECEIPT_IN
                              && x.StoreInventory.IngredientId == catalog.IngredientId);
        Assert.Equal(48m, transaction.Quantity);
        Assert.Equal(27_000m, transaction.UnitCost);
        Assert.Equal(1_296_000m, transaction.TotalCost);

        var fifo = await context.InventoryCostLayers.SingleAsync(x =>
            x.StoreId == storeId && x.IngredientId == catalog.IngredientId);
        Assert.Equal(48m, fifo.Quantity);
        Assert.Equal(27_000m, fifo.UnitCost);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new AppDbContext(options);
    }

    private static AdminSupplierService CreateSupplierService(AppDbContext context)
    {
        var physical = new PhysicalUnitConversionService(
            context, NullLogger<PhysicalUnitConversionService>.Instance);
        return new AdminSupplierService(
            new AdminSupplierRepository(context),
            context,
            new IngredientSupplierPackageValidator(context, physical));
    }

    private static BranchReceiptService CreateReceiptService(AppDbContext context)
    {
        var physical = new PhysicalUnitConversionService(
            context, NullLogger<PhysicalUnitConversionService>.Instance);
        var unit = new UnitConversionService(
            context, NullLogger<UnitConversionService>.Instance, physical);
        var mode = new Mock<IInventoryWriterModeService>();
        var resolver = new Mock<IStoreInventoryWriteResolver>();
        var alerts = new Mock<IStockAlertService>();
        alerts
            .Setup(x => x.EvaluateStoreInventoryItemAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(ServiceResult<CafeChain.Application.DTOs.POS.StockAlertEvaluationResultDto>.Success(
                new CafeChain.Application.DTOs.POS.StockAlertEvaluationResultDto()));

        return new BranchReceiptService(
            context,
            unit,
            physical,
            mode.Object,
            resolver.Object,
            new RestockFulfillmentPostingService(context),
            alerts.Object,
            new ScopeAuthorizationService(context),
            NullLogger<BranchReceiptService>.Instance);
    }

    private static AdminSupplierCreateDTO NewSupplier(string name, string phone) => new()
    {
        Name = name,
        Address = "SQL integration test",
        PrimaryPhone = phone,
        PrimaryContactName = name + " contact",
        PrimaryContactPhone = phone
    };

    private static SupplierStore NewSupplierStore(int supplierId, int storeId) => new()
    {
        SupplierId = supplierId,
        StoreId = storeId,
        Active = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static async Task<(int IngredientId, int UnitId, int OfferId)> SeedReceiptCatalogAsync(
        AppDbContext context,
        int storeId,
        int supplierId)
    {
        var unit = new Unit
        {
            UnitCode = "lon_count",
            Name = "Lon",
            Type = UnitType.Dem,
            Active = true
        };
        context.Units.Add(unit);
        await context.SaveChangesAsync();

        var ingredient = new Ingredient
        {
            Code = "ING-SQL-SUP-167",
            Name = "Sữa lon SQL Supplier",
            BaseUnitId = unit.UnitId,
            Active = true
        };
        context.Ingredients.Add(ingredient);
        context.SupplierStores.Add(NewSupplierStore(supplierId, storeId));
        await context.SaveChangesAsync();

        var offer = new IngredientSupplier
        {
            SupplierId = supplierId,
            IngredientId = ingredient.IngredientId,
            UnitId = unit.UnitId,
            PackageQuantity = 24m,
            CurrentPrice = 648_000m,
            MinimumOrderPackageCount = 2,
            LeadTimeDays = 2,
            IsPrimary = true,
            Active = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.IngredientSuppliers.Add(offer);
        await context.SaveChangesAsync();
        return (ingredient.IngredientId, unit.UnitId, offer.IngredientSupplierId);
    }

    private static async Task<int> SeedProcessingRequestAsync(
        AppDbContext context,
        int storeId,
        int actorStaffId,
        int ingredientId)
    {
        var now = DateTime.UtcNow;
        var alert = new StockAlert
        {
            StoreId = storeId,
            IngredientId = ingredientId,
            AlertType = StockAlertTypes.LowStock,
            Severity = StockAlertSeverities.Warning,
            Status = StockAlertStatuses.Confirmed,
            Source = StockAlertSources.ManualCheck,
            CurrentQtySnapshot = 0,
            ThresholdSnapshot = 48,
            CreatedAt = now,
            UpdatedAt = now
        };
        context.StockAlerts.Add(alert);
        await context.SaveChangesAsync();

        var request = new RestockRequest
        {
            StockAlertId = alert.StockAlertId,
            StoreId = storeId,
            IngredientId = ingredientId,
            RequestedQuantity = 48,
            Status = RestockRequestStatuses.Processing,
            Priority = RestockRequestPriorities.Normal,
            CreatedByStaffId = actorStaffId,
            CreatedAt = now,
            UpdatedAt = now,
            HandledByStaffId = actorStaffId,
            HandledAt = now
        };
        context.RestockRequests.Add(request);
        await context.SaveChangesAsync();
        return request.RestockRequestId;
    }
}
