using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Inventories;
using CafeChain.Data;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Procurement;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Inventories.Suppliers;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CafeChain.Tests;

public sealed class PurchaseAdviceConsolidationIssue185Tests : IntegrationTestBase
{
    [Fact]
    public async Task Consolidation_ListsSubmittedAndReviewAdviceLines()
    {
        using var db = CreateDbContext();
        var seed = await SeedAsync(db, PurchaseAdviceStatuses.Submitted);
        var second = await AddAdviceLineAsync(db, seed, PurchaseAdviceStatuses.UnderReview, 6m);

        var result = await Service(db).GetQueueAsync(new(), Warehouse(seed));

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(2, result.Data!.Lines.Count);
        Assert.Contains(result.Data.Lines, x => x.PurchaseAdviceLineId == second);
    }

    [Fact]
    public async Task Consolidation_CalculatesRemainingServerSide()
    {
        using var db = CreateDbContext();
        var seed = await SeedAsync(db);
        var line = await db.PurchaseAdviceLines.SingleAsync(x => x.PurchaseAdviceLineId == seed.LineId);
        line.AllocatedToPoBaseQuantity = 2m;
        line.ClosedBaseQuantity = 1m;
        await db.SaveChangesAsync();

        var result = await Service(db).GetQueueAsync(new(), Warehouse(seed));

        Assert.Equal(7m, result.Data!.Lines.Single().RemainingToOrderBaseQuantity);
    }

    [Fact]
    public async Task Consolidation_RejectsSupplierStoreMismatch()
    {
        using var db = CreateDbContext();
        var seed = await SeedAsync(db);
        db.SupplierStores.RemoveRange(db.SupplierStores);
        await db.SaveChangesAsync();

        var result = await PreviewAsync(db, seed, 1);

        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseAdviceErrorCodes.SupplierStoreMismatch, result.ErrorCode);
    }

    [Fact]
    public async Task Consolidation_RejectsInactiveSupplier()
    {
        using var db = CreateDbContext();
        var seed = await SeedAsync(db);
        (await db.Suppliers.SingleAsync(x => x.SupplierId == seed.SupplierId)).Active = false;
        await db.SaveChangesAsync();

        var result = await PreviewAsync(db, seed, 1);

        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseAdviceErrorCodes.SupplierInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task Consolidation_RejectsInvalidOffer()
    {
        using var db = CreateDbContext();
        var seed = await SeedAsync(db);
        (await db.IngredientSuppliers.SingleAsync(x => x.IngredientSupplierId == seed.OfferId)).Active = false;
        await db.SaveChangesAsync();

        var result = await PreviewAsync(db, seed, 1);

        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseAdviceErrorCodes.OfferInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task Consolidation_RejectsPackageMismatch()
    {
        using var db = CreateDbContext();
        var seed = await SeedAsync(db);
        var result = await Service(db, conversionSucceeds: false).PreviewAsync(Request(seed, 1), Warehouse(seed));
        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseAdviceErrorCodes.PackageMismatch, result.ErrorCode);
    }

    [Fact]
    public async Task Consolidation_DifferentPricesRemainSeparateGroups()
    {
        using var db = CreateDbContext();
        var seed = await SeedAsync(db, requested: 20m, moq: 1);
        var second = await AddSecondIngredientAsync(db, seed, 7m, 25000m);
        var request = Request(seed, 1);
        request.Lines.Add(new PurchaseAdviceConsolidationSelectionRequest
        {
            PurchaseAdviceLineId = second.LineId,
            IngredientSupplierId = second.OfferId,
            PackageCount = 1,
            RowVersion = second.RowVersion
        });

        var result = await Service(db).PreviewAsync(request, Warehouse(seed));

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(2, result.Data!.Groups.Count);
        Assert.Equal(35000m, result.Data.TotalAmount);
    }

    [Fact]
    public async Task Consolidation_RejectsMoqViolation()
    {
        using var db = CreateDbContext();
        var seed = await SeedAsync(db, requested: 20m, moq: 3);
        var result = await PreviewAsync(db, seed, 2);
        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseAdviceErrorCodes.MoqViolation, result.ErrorCode);
    }

    [Fact]
    public async Task Consolidation_CannotAllocateAboveRemaining()
    {
        using var db = CreateDbContext();
        var seed = await SeedAsync(db, requested: 2m);
        var result = await PreviewAsync(db, seed, 3);
        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseAdviceErrorCodes.ExceedsRemaining, result.ErrorCode);
    }

    [Fact]
    public async Task Consolidation_OtherAreaRejected()
    {
        using var db = CreateDbContext();
        var seed = await SeedAsync(db);
        var result = await Service(db).GetQueueAsync(new(), new AdminActorContext
        {
            StaffId = 999,
            RoleNames = new[] { RoleConstants.AreaManager }
        });
        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseAdviceErrorCodes.Forbidden, result.ErrorCode);
    }

    [Fact]
    public async Task AccountantWarehouse_CanConsolidate()
    {
        using var db = CreateDbContext();
        var seed = await SeedAsync(db);
        var result = await PreviewAsync(db, seed, 1);
        Assert.True(result.IsSuccess, result.Message);
        Assert.Empty(db.PurchaseOrders);
    }

    [Fact]
    public async Task StoreManager_CannotConsolidate()
    {
        using var db = CreateDbContext();
        var seed = await SeedAsync(db);
        var result = await Service(db).PreviewAsync(Request(seed, 1), new AdminActorContext
        {
            StaffId = seed.ManagerId,
            StoreId = seed.StoreId,
            RoleNames = new[] { RoleConstants.StoreManager }
        });
        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseAdviceErrorCodes.Forbidden, result.ErrorCode);
    }

    private static async Task<ServiceResult<PurchaseAdviceConsolidationPreviewDto>> PreviewAsync(
        AppDbContext db, Seed seed, int packageCount) =>
        await Service(db).PreviewAsync(Request(seed, packageCount), Warehouse(seed));

    private static PurchaseAdviceConsolidationPreviewRequest Request(Seed seed, int packageCount) => new()
    {
        SupplierId = seed.SupplierId,
        Lines = new()
        {
            new()
            {
                PurchaseAdviceLineId = seed.LineId,
                IngredientSupplierId = seed.OfferId,
                PackageCount = packageCount,
                RowVersion = seed.LineRowVersion
            }
        }
    };

    private static PurchaseAdviceConsolidationService Service(AppDbContext db, bool conversionSucceeds = true)
    {
        var scope = new Mock<IScopeAuthorizationService>();
        scope.Setup(x => x.CanAccessStoreAsync(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(false);
        var physical = new Mock<IPhysicalUnitConversionService>();
        physical.Setup(x => x.ConvertAsync(It.IsAny<decimal>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((decimal quantity, int _, int _) => conversionSucceeds
                ? ServiceResult<decimal>.Success(quantity)
                : ServiceResult<decimal>.Failure("incompatible"));
        return new PurchaseAdviceConsolidationService(db, scope.Object, physical.Object);
    }

    private static AdminActorContext Warehouse(Seed seed) => new()
    {
        StaffId = seed.WarehouseId,
        RoleNames = new[] { RoleConstants.AccountantWarehouse }
    };

    private static async Task<Seed> SeedAsync(
        AppDbContext db,
        string status = PurchaseAdviceStatuses.Submitted,
        decimal requested = 10m,
        int moq = 1)
    {
        var now = DateTime.UtcNow;
        var store = new Store { Name = "Store 185", Address = "Test", Phone = Guid.NewGuid().ToString("N")[..10], Active = true, CreatedAt = now };
        var unit = new Unit { UnitCode = "kg185" + Guid.NewGuid().ToString("N")[..4], Name = "kg", Active = true };
        var ingredient = new Ingredient { Code = "ING185" + Guid.NewGuid().ToString("N")[..5], Name = "Coffee 185", Active = true, BaseUnit = unit };
        var supplier = new Supplier { Code = "SUP185" + Guid.NewGuid().ToString("N")[..5], Name = "Supplier 185", Active = true, CreatedAt = now, UpdatedAt = now };
        db.AddRange(store, ingredient, supplier);
        await db.SaveChangesAsync();
        var offer = new IngredientSupplier { IngredientId = ingredient.IngredientId, SupplierId = supplier.SupplierId, UnitId = unit.UnitId, PackageQuantity = 1m, CurrentPrice = 10000m, MinimumOrderPackageCount = moq, Active = true, CreatedAt = now, UpdatedAt = now };
        var supplierStore = new SupplierStore { SupplierId = supplier.SupplierId, StoreId = store.StoreId, Active = true, CreatedAt = now, UpdatedAt = now };
        var restock = new RestockRequest { StoreId = store.StoreId, IngredientId = ingredient.IngredientId, RequestedQuantity = requested, Status = RestockRequestStatuses.Processing, Priority = RestockRequestPriorities.Normal, CreatedByStaffId = 1, CreatedAt = now, UpdatedAt = now };
        db.AddRange(offer, supplierStore, restock);
        await db.SaveChangesAsync();
        var advice = new PurchaseAdvice
        {
            AdviceNumber = "PA-185-" + Guid.NewGuid().ToString("N")[..6], RequestKey = Guid.NewGuid().ToString("N"), StoreId = store.StoreId,
            RequestedByStaffId = 1, Status = status, NeededByDate = now.Date.AddDays(3), Priority = PurchaseAdvicePriorities.Normal,
            CreatedAtUtc = now, UpdatedAtUtc = now,
            Lines = new List<PurchaseAdviceLine>
            {
                new() { RestockRequestId = restock.RestockRequestId, IngredientId = ingredient.IngredientId, RequestedPurchaseBaseQuantity = requested, BaseUnitId = unit.UnitId, NeededByDate = now.Date.AddDays(3), IsActiveReservation = true }
            }
        };
        db.Add(advice);
        await db.SaveChangesAsync();
        var line = advice.Lines.Single();
        return new Seed(store.StoreId, 1, 2, supplier.SupplierId, offer.IngredientSupplierId, line.PurchaseAdviceLineId, Convert.ToBase64String(line.RowVersion), advice.PurchaseAdviceId, unit.UnitId);
    }

    private static async Task<int> AddAdviceLineAsync(AppDbContext db, Seed seed, string status, decimal quantity)
    {
        var ingredient = new Ingredient { Code = "ING185Q" + Guid.NewGuid().ToString("N")[..4], Name = "Queue item 185", Active = true, BaseUnitId = seed.UnitId };
        db.Add(ingredient); await db.SaveChangesAsync();
        var restock = new RestockRequest { StoreId = seed.StoreId, IngredientId = ingredient.IngredientId, RequestedQuantity = quantity, Status = RestockRequestStatuses.Processing, Priority = RestockRequestPriorities.Normal, CreatedByStaffId = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.Add(restock); await db.SaveChangesAsync();
        var advice = new PurchaseAdvice { AdviceNumber = "PA-185-" + Guid.NewGuid().ToString("N")[..6], RequestKey = Guid.NewGuid().ToString("N"), StoreId = seed.StoreId, RequestedByStaffId = 1, Status = status, NeededByDate = DateTime.UtcNow.Date.AddDays(2), Priority = PurchaseAdvicePriorities.High, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow, Lines = new List<PurchaseAdviceLine> { new() { RestockRequestId = restock.RestockRequestId, IngredientId = ingredient.IngredientId, RequestedPurchaseBaseQuantity = quantity, BaseUnitId = seed.UnitId, NeededByDate = DateTime.UtcNow.Date.AddDays(2), IsActiveReservation = true } } };
        db.Add(advice); await db.SaveChangesAsync();
        return advice.Lines.Single().PurchaseAdviceLineId;
    }

    private static async Task<(int LineId, int OfferId, string RowVersion)> AddSecondIngredientAsync(AppDbContext db, Seed seed, decimal quantity, decimal price)
    {
        var unit = await db.Units.SingleAsync(x => x.UnitId == seed.UnitId);
        var ingredient = new Ingredient { Code = "ING185B" + Guid.NewGuid().ToString("N")[..4], Name = "Milk 185", Active = true, BaseUnitId = unit.UnitId };
        db.Add(ingredient); await db.SaveChangesAsync();
        var offer = new IngredientSupplier { IngredientId = ingredient.IngredientId, SupplierId = seed.SupplierId, UnitId = unit.UnitId, PackageQuantity = 1m, CurrentPrice = price, MinimumOrderPackageCount = 1, Active = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var restock = new RestockRequest { StoreId = seed.StoreId, IngredientId = ingredient.IngredientId, RequestedQuantity = quantity, Status = RestockRequestStatuses.Processing, Priority = RestockRequestPriorities.Normal, CreatedByStaffId = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.AddRange(offer, restock); await db.SaveChangesAsync();
        var advice = await db.PurchaseAdvices.Include(x => x.Lines).SingleAsync(x => x.PurchaseAdviceId == seed.AdviceId);
        var line = new PurchaseAdviceLine { RestockRequestId = restock.RestockRequestId, IngredientId = ingredient.IngredientId, RequestedPurchaseBaseQuantity = quantity, BaseUnitId = unit.UnitId, NeededByDate = DateTime.UtcNow.Date.AddDays(3), IsActiveReservation = true };
        advice.Lines.Add(line); await db.SaveChangesAsync();
        return (line.PurchaseAdviceLineId, offer.IngredientSupplierId, Convert.ToBase64String(line.RowVersion));
    }

    private sealed record Seed(int StoreId, int ManagerId, int WarehouseId, int SupplierId, int OfferId, int LineId, string LineRowVersion, int AdviceId, int UnitId);
}
