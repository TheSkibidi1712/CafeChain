using CafeChain.Application.Constants;
using CafeChain.Application.Services.Inventories;
using CafeChain.Data;
using CafeChain.Models.Customers;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Procurement;
using CafeChain.Models.Inventories.Suppliers;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CafeChain.Tests;

public sealed class PurchaseOrderConsistencyTests : IntegrationTestBase
{
    [Fact]
    public async Task PurchaseOrderConsistencyRepair_FixesDeterministicPackageFixture_AndIsIdempotent()
    {
        await using var context = CreateDbContext();
        var actorId = await SeedInconsistentFixtureAsync(context);
        var service = CreateService(context);

        var dryRun = await service.DryRunAsync();

        Assert.Equal(2, dryRun.SafeAutoRepairCount);
        Assert.Contains(dryRun.Items, x => x.IssueCode == "PACKAGE_ORDERED_BASE_MISMATCH");
        Assert.Contains(dryRun.Items, x => x.IssueCode == "CLOSURE_EVENT_AGGREGATE_MISMATCH");

        var repaired = await service.RepairSafeAsync(actorId);
        var rerun = await service.RepairSafeAsync(actorId);

        Assert.True(repaired.IsSuccess, repaired.Message);
        Assert.Equal(1, repaired.Data!.RepairedCount);
        Assert.True(rerun.IsSuccess, rerun.Message);
        Assert.Equal(0, rerun.Data!.RepairedCount);
        var line = await context.PurchaseOrderLines.AsNoTracking().SingleAsync();
        Assert.Equal(5000m, line.OrderedBaseQuantity);
        Assert.Equal(0m, line.ClosedRemainingQuantity);
        Assert.Single(await context.AuditLogs.Where(x => x.Action == "PO_CONSISTENCY_SAFE_REPAIR").ToListAsync());
    }

    private static PurchaseOrderConsistencyService CreateService(AppDbContext context)
    {
        var physical = new PhysicalUnitConversionService(
            context,
            NullLogger<PhysicalUnitConversionService>.Instance);
        var conversion = new UnitConversionService(
            context,
            NullLogger<UnitConversionService>.Instance,
            physical);
        return new PurchaseOrderConsistencyService(context, conversion);
    }

    private static async Task<int> SeedInconsistentFixtureAsync(AppDbContext context)
    {
        var now = DateTime.UtcNow;
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var gram = await context.Units.SingleAsync(x => x.UnitCode == "g");
        var kilogram = await context.Units.SingleAsync(x => x.UnitCode == "kg");
        var store = new Store { Name = "PO Repair Store", Address = "Test", Phone = "0900000001", Active = true, CreatedAt = now };
        var account = new Account { Email = $"po-repair-{suffix}@test.local", PasswordHash = "x", Active = true, CreatedAt = now };
        var supplier = new Supplier { Code = "SUP-" + suffix, Name = "PO Repair Supplier", Active = true, CreatedAt = now, UpdatedAt = now };
        context.AddRange(store, account, supplier);
        await context.SaveChangesAsync();
        var staff = new Staff { AccountId = account.AccountId, StoreId = store.StoreId, FullName = "Owner Repair", Active = true, CreatedAt = now };
        var ingredient = new Ingredient { Code = "ING-" + suffix, Name = "Cà phê repair", BaseUnitId = gram.UnitId, Active = true };
        context.AddRange(staff, ingredient);
        await context.SaveChangesAsync();
        var offer = new IngredientSupplier
        {
            IngredientId = ingredient.IngredientId,
            SupplierId = supplier.SupplierId,
            UnitId = kilogram.UnitId,
            PackageQuantity = 1m,
            CurrentPrice = 180000m,
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
            Code = "PO-REPAIR-" + suffix,
            StoreId = store.StoreId,
            SupplierId = supplier.SupplierId,
            Status = PurchaseOrderStatuses.MarkedAsSent,
            OrderDate = now,
            CreatedByStaffId = staff.StaffId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        order.Lines.Add(new PurchaseOrderLine
        {
            IngredientId = ingredient.IngredientId,
            IngredientSupplierId = offer.IngredientSupplierId,
            PurchaseMode = PurchaseMode.Packaged,
            PackageUnitIdSnapshot = kilogram.UnitId,
            PackageQuantitySnapshot = 1m,
            PackagePriceSnapshot = 180000m,
            PackageCount = 5m,
            OrderedPackageCount = 5m,
            OrderedPackQuantity = 5m,
            UnitPricePerPackage = 180000m,
            OrderedBaseQuantity = 5m,
            ClosedRemainingQuantity = 5m,
            PromisedLeadTimeDaysSnapshot = 1,
            Note = "DEMO_AI_DASHBOARD_ROLLING_V1_LINE_TEST"
        });
        context.PurchaseOrders.Add(order);
        await context.SaveChangesAsync();
        return staff.StaffId;
    }
}
