using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.StoreInventories;
using CafeChain.Infrastrusture.Repositories.Admin.StoreInventories;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Inventories.Suppliers;
using CafeChain.Models.Inventories.Transactions;
using CafeChain.Models.Stores;
using Xunit;

namespace CafeChain.Tests;

public sealed class StoreInventorySupplierProjectionTests : IntegrationTestBase
{
    [Fact]
    public async Task StoreInventory_UsesLatestConfirmedSupplierReceipt_NotNewerSaleTransaction()
    {
        using var context = CreateDbContext();
        var now = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc);
        var unit = new Unit
        {
            UnitCode = "sip",
            Name = "Supplier inventory projection unit",
            Type = UnitType.KhoiLuong,
            Active = true
        };
        var store = new Store
        {
            Name = "Store supplier projection",
            Address = "Test",
            Phone = "0900000417",
            Active = true,
            CreatedAt = now
        };
        var ingredient = new Ingredient
        {
            Code = "ING-SUPPLIER-PROJECTION",
            Name = "Hạt chia projection",
            BaseUnit = unit,
            Active = true
        };
        var supplier = new Supplier
        {
            Code = "SUP-PROJECTION",
            Name = "Nhà cung cấp lần nhập gần nhất",
            Active = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        context.AddRange(unit, store, ingredient, supplier);
        await context.SaveChangesAsync();

        var inventory = new StoreInventory
        {
            StoreId = store.StoreId,
            IngredientId = ingredient.IngredientId,
            AvailableQty = 7_990m,
            ReservedQty = 0m,
            LastUpdated = now
        };
        var receipt = new BranchReceipt
        {
            ReceiptCode = "BR-SUPPLIER-PROJECTION",
            ReceiptKey = "BR-SUPPLIER-PROJECTION",
            StoreId = store.StoreId,
            SupplierId = supplier.SupplierId,
            Status = BranchReceiptStatuses.Confirmed,
            ReceivedAt = now.AddDays(-1),
            ConfirmedAt = now.AddDays(-1),
            CreatedAt = now.AddDays(-1),
            CreatedByStaffId = 1
        };

        context.AddRange(inventory, receipt);
        await context.SaveChangesAsync();

        var receiptLine = new BranchReceiptLine
        {
            BranchReceiptId = receipt.BranchReceiptId,
            IngredientId = ingredient.IngredientId,
            InputQuantity = 8_000m,
            InputUnitId = unit.UnitId,
            ReceivedBaseQuantity = 8_000m,
            RejectedBaseQuantity = 0m,
            BaseUnitId = unit.UnitId,
            SupplierId = supplier.SupplierId,
            BaseUnitCostSnapshot = 180m,
            LineTotalCost = 1_440_000m,
            CreatedAt = now.AddDays(-1)
        };
        context.BranchReceiptLines.Add(receiptLine);
        await context.SaveChangesAsync();

        context.InventoryTransactions.AddRange(
            new InventoryTransaction
            {
                StoreInventoryId = inventory.StoreInventoryId,
                Type = InventoryTransactionTypeEnum.BRANCH_RECEIPT_IN,
                StockStatus = InventoryStockStatus.NORMAL,
                Quantity = 8_000m,
                BeforeQty = 0m,
                AfterQty = 8_000m,
                UnitCost = 180m,
                TotalCost = 1_440_000m,
                BranchReceiptLineId = receiptLine.BranchReceiptLineId,
                CreatedAt = now.AddDays(-1)
            },
            new InventoryTransaction
            {
                StoreInventoryId = inventory.StoreInventoryId,
                Type = InventoryTransactionTypeEnum.SALES_DEDUCTION,
                StockStatus = InventoryStockStatus.NORMAL,
                Quantity = 10m,
                BeforeQty = 8_000m,
                AfterQty = 7_990m,
                UnitCost = 999m,
                TotalCost = 9_990m,
                CreatedAt = now
            });
        await context.SaveChangesAsync();

        var repository = new AdminStoreInventoryRepository(context);
        var (rows, total) = await repository.GetPagedAsync(
            new List<int> { store.StoreId },
            store.StoreId,
            InventoryCatalogTypes.Ingredients,
            "Hạt chia projection",
            1,
            10);

        Assert.Equal(1, total);
        var row = Assert.Single(rows);
        Assert.Equal(180m, row.LastUnitPrice);
        Assert.Equal("Nhà cung cấp lần nhập gần nhất", row.LastSupplierName);
    }

    [Fact]
    public void InventoryTable_ClarifiesSupplierIsFromLatestReceipt()
    {
        var tablePath = Path.Combine(
            FindRepoRoot(),
            "CafeChain",
            "Areas",
            "Admin",
            "Views",
            "AdminStoreInventory",
            "Partials",
            "_InventoryTablePartial.cshtml");

        var table = File.ReadAllText(tablePath);

        Assert.Contains("NCC nhập gần nhất", table, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "CafeChain")))
            dir = dir.Parent;

        return dir?.FullName ?? throw new DirectoryNotFoundException("Không tìm thấy repo root.");
    }
}
