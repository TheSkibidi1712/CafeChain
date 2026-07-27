using System.Text.Json;
using System.Text.Json.Serialization;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.DTOs.Admin.RestockRequests;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Inventories;
using CafeChain.Application.Services.Security;
using CafeChain.Data;
using CafeChain.Models.Customers;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Procurement;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Inventories.Suppliers;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CafeChain.Tests;

public sealed class LooseProcurementIssue233Tests : IntegrationTestBase
{
    private const int StoreId = 23301;
    private const int OtherStoreId = 23302;
    private const int StaffId = 23303;
    private const int GramUnitId = 1;
    private const int KilogramUnitId = 2;
    private const int MilliliterUnitId = 3;
    private const int LiterUnitId = 4;
    private const int IngredientId = 23308;
    private const int SupplierId = 23309;

    [Fact]
    public async Task PackagedPurchase_ExistingFlowStillWorks()
    {
        await using var db = CreateDbContext();
        var seed = await SeedAsync(db);

        var result = await PurchaseOrders(db).CreateDraftAsync(
            Request(seed.OfferId, PurchaseMode.Packaged, packageCount: 2m),
            StaffId,
            new[] { RoleConstants.AccountantWarehouse });

        Assert.True(result.IsSuccess, result.Message);
        var line = await db.PurchaseOrderLines.AsNoTracking().SingleAsync();
        Assert.Equal(PurchaseMode.Packaged, line.PurchaseMode);
        Assert.Equal(2m, line.OrderedPackageCount);
        Assert.Equal(240_000m, result.Data!.TotalAmount);
    }

    [Fact]
    public void ExistingRows_BackfilledAsPackaged()
    {
        var source = MigrationSource();
        Assert.Contains("UPDATE [PurchaseOrderLines]", source);
        Assert.Contains("[PurchaseMode] = N'Packaged'", source);
        Assert.Contains("[OrderedPackageCount] = [PackageCount]", source);
        Assert.Contains("[UnitPricePerPackage] = [PackagePriceSnapshot]", source);
    }

    [Fact]
    public async Task LooseMode_RequiresSupplierAllowsLoosePurchase()
    {
        await using var db = CreateDbContext();
        var seed = await SeedAsync(db, allowsLoose: false);

        var result = await PurchaseOrders(db).CreateDraftAsync(
            Request(seed.OfferId, PurchaseMode.Loose, procurementQuantity: 8.75m),
            StaffId,
            new[] { RoleConstants.AccountantWarehouse });

        Assert.False(result.IsSuccess);
        Assert.Contains("chưa cho phép mua rời", result.Message);
    }

    [Fact]
    public async Task LooseMode_DoesNotRequirePackageCount()
    {
        await using var db = CreateDbContext();
        var seed = await SeedAsync(db);

        var result = await PurchaseOrders(db).CreateDraftAsync(
            Request(seed.OfferId, PurchaseMode.Loose, procurementQuantity: 8.75m),
            StaffId,
            new[] { RoleConstants.AccountantWarehouse });

        Assert.True(result.IsSuccess, result.Message);
        var line = await db.PurchaseOrderLines.AsNoTracking().SingleAsync();
        Assert.Null(line.PackageCount);
        Assert.Null(line.OrderedPackageCount);
        Assert.Null(line.UnitPricePerPackage);
    }

    [Fact]
    public async Task LooseMode_RejectsPackageAuthorityFields()
    {
        await using var db = CreateDbContext();
        var seed = await SeedAsync(db);
        var request = Request(
            seed.OfferId,
            PurchaseMode.Loose,
            packageCount: 1m,
            procurementQuantity: 8.75m);

        var result = await PurchaseOrders(db).CreateDraftAsync(
            request,
            StaffId,
            new[] { RoleConstants.AccountantWarehouse });

        Assert.False(result.IsSuccess);
        Assert.Empty(await db.PurchaseOrders.ToListAsync());
    }

    [Fact]
    public void PackagedMode_RequiresIntegerPackageCount()
    {
        Assert.True(ProcurementPurchaseMath.IsWholePackageCount(9m));
        Assert.False(ProcurementPurchaseMath.IsWholePackageCount(8.75m));
        Assert.False(ProcurementPurchaseMath.IsWholePackageCount(0m));
    }

    [Fact]
    public void PackagedMode_CalculatesLineTotalPerPackage()
    {
        Assert.Equal(
            1_080_000m,
            ProcurementPurchaseMath.CalculateLineTotal(
                PurchaseMode.Packaged,
                9m,
                120_000m,
                8.75m,
                null));
    }

    [Fact]
    public void LooseMode_CalculatesLineTotalPerKg()
    {
        Assert.Equal(
            1_050_000m,
            ProcurementPurchaseMath.CalculateLineTotal(
                PurchaseMode.Loose,
                null,
                null,
                8.75m,
                120_000m));
    }

    [Fact]
    public void LooseMode_CalculatesLineTotalPerLiter()
    {
        Assert.Equal(
            147_500m,
            ProcurementPurchaseMath.CalculateLineTotal(
                PurchaseMode.Loose,
                null,
                null,
                2.5m,
                59_000m));
    }

    [Fact]
    public async Task LoosePo_8_75Kg_Remains8_75Kg()
    {
        await using var db = CreateDbContext();
        var seed = await SeedAsync(db);

        var created = await CreateLooseOrderAsync(db, seed.OfferId, 8.75m);
        Assert.True(created.IsSuccess, created.Message);

        var line = await db.PurchaseOrderLines.AsNoTracking().SingleAsync();
        Assert.Equal(PurchaseMode.Loose, line.PurchaseMode);
        Assert.Equal(8.75m, line.OrderedProcurementQuantity);
        Assert.Equal(8_750m, line.OrderedBaseQuantity);
        Assert.Null(line.ProcurementToInventoryFactor);
        Assert.Null(line.InventoryPostingBaseQuantity);
    }

    [Fact]
    public async Task LooseReceipt_Receives8_75Kg()
    {
        await using var db = CreateDbContext();
        var receipt = await SaveLooseReceiptAsync(db, 8.75m);
        var line = await db.BranchReceiptLines.AsNoTracking().SingleAsync();

        Assert.Equal(PurchaseMode.Loose, line.PurchaseMode);
        Assert.Equal(8.75m, line.ReceivedProcurementQuantity);
        Assert.Equal(8.75m, line.AcceptedProcurementQuantity);
        Assert.Equal(0m, line.ReceivedBaseQuantity);
        Assert.Null(line.ProcurementToInventoryFactor);
        Assert.Equal(
            BranchReceiptStatuses.Draft,
            (await db.BranchReceipts.AsNoTracking()
                .SingleAsync(x => x.BranchReceiptId == receipt.BranchReceiptId)).Status);
    }

    [Fact]
    public async Task LooseReceipt_Posts8750GramAtConfirm()
    {
        await using var db = CreateDbContext();
        var saved = await SaveLooseReceiptAsync(db, 8.75m);

        var confirmed = await Receipts(db).ConfirmAsync(
            saved.BranchReceiptId,
            StaffId,
            StoreId,
            new[] { RoleConstants.StoreManager },
            saved.RowVersion);

        Assert.True(confirmed.IsSuccess, confirmed.Message);
        var line = await db.BranchReceiptLines.AsNoTracking().SingleAsync();
        Assert.Equal(8_750m, line.InventoryPostingBaseQuantity);
        Assert.Equal(1_000m, line.ProcurementToInventoryFactor);
        Assert.Equal(
            8_750m,
            (await db.StoreInventories.AsNoTracking()
                .Where(x => x.StoreId == StoreId && x.IngredientId == IngredientId)
                .ToListAsync()).Sum(x => x.AvailableQty));
    }

    [Fact]
    public async Task LooseReceipt_DoesNotConvertBeforeConfirm()
    {
        await using var db = CreateDbContext();
        await SaveLooseReceiptAsync(db, 8.75m);
        var line = await db.BranchReceiptLines.AsNoTracking().SingleAsync();

        Assert.Equal(8.75m, line.AcceptedProcurementQuantity);
        Assert.Equal(0m, line.ReceivedBaseQuantity);
        Assert.Null(line.InventoryPostingBaseQuantity);
        Assert.Null(line.ProcurementToInventoryFactor);
        Assert.Empty(await db.InventoryTransactions.ToListAsync());
    }

    [Fact]
    public async Task LooseReceipt_NoDoubleConversion()
    {
        await using var db = CreateDbContext();
        var saved = await SaveLooseReceiptAsync(db, 8.75m);
        var service = Receipts(db);

        var first = await service.ConfirmAsync(
            saved.BranchReceiptId,
            StaffId,
            StoreId,
            new[] { RoleConstants.StoreManager },
            saved.RowVersion);
        var replay = await service.ConfirmAsync(
            saved.BranchReceiptId,
            StaffId,
            StoreId,
            new[] { RoleConstants.StoreManager },
            saved.RowVersion);

        Assert.True(first.IsSuccess, first.Message);
        Assert.True(replay.IsSuccess, replay.Message);
        Assert.Equal(
            8_750m,
            (await db.StoreInventories.AsNoTracking()
                .Where(x => x.StoreId == StoreId && x.IngredientId == IngredientId)
                .ToListAsync()).Sum(x => x.AvailableQty));
        Assert.Single(await db.InventoryTransactions.ToListAsync());
    }

    [Fact]
    public async Task LoosePartialReceipt_TracksRemainingKg()
    {
        await using var db = CreateDbContext();
        var seed = await SeedAsync(db);
        var order = await CreateAndSendLooseOrderAsync(db, seed.OfferId, 8.75m);
        var first = await SaveLooseReceiptForOrderAsync(db, order.PurchaseOrderId, 5m);
        Assert.True((await ConfirmAsync(db, first)).IsSuccess);

        db.ChangeTracker.Clear();
        var detail = await PurchaseOrders(db).GetDetailAsync(
            order.PurchaseOrderId,
            StaffId,
            new[] { RoleConstants.AccountantWarehouse });

        Assert.True(detail.IsSuccess, detail.Message);
        Assert.Equal(3.75m, detail.Data!.Lines.Single().RemainingProcurementQuantity);
    }

    [Fact]
    public async Task LooseRejectedQty_DoesNotIncreaseInventory()
    {
        await using var db = CreateDbContext();
        var saved = await SaveLooseReceiptAsync(
            db,
            8.75m,
            rejected: 1.25m);

        var confirmed = await ConfirmAsync(db, saved);

        Assert.True(confirmed.IsSuccess, confirmed.Message);
        Assert.Equal(
            7_500m,
            (await db.StoreInventories.AsNoTracking()
                .Where(x => x.StoreId == StoreId && x.IngredientId == IngredientId)
                .ToListAsync()).Sum(x => x.AvailableQty));
        var line = await db.BranchReceiptLines.AsNoTracking().SingleAsync();
        Assert.Equal(1_250m, line.RejectedBaseQuantity);
    }

    [Fact]
    public async Task PackagedReceipt_Regression()
    {
        await using var db = CreateDbContext();
        var seed = await SeedAsync(db);
        var created = await PurchaseOrders(db).CreateDraftAsync(
            Request(seed.OfferId, PurchaseMode.Packaged, packageCount: 2m),
            StaffId,
            new[] { RoleConstants.AccountantWarehouse });

        Assert.True(created.IsSuccess, created.Message);
        Assert.Equal(2_000m, created.Data!.Lines.Single().OrderedBaseQuantity);
        Assert.Equal(240_000m, created.Data.TotalAmount);
    }

    [Fact]
    public async Task TwoStorePackagedPo_Regression()
    {
        await using var db = CreateDbContext();
        var seed = await SeedAsync(db);

        var first = await PurchaseOrders(db).CreateDraftAsync(
            Request(seed.OfferId, PurchaseMode.Packaged, packageCount: 2m),
            StaffId,
            new[] { RoleConstants.AccountantWarehouse });
        var secondRequest = Request(seed.OfferId, PurchaseMode.Packaged, packageCount: 3m);
        secondRequest.StoreId = OtherStoreId;
        var second = await PurchaseOrders(db).CreateDraftAsync(
            secondRequest,
            StaffId,
            new[] { RoleConstants.AccountantWarehouse });

        Assert.True(first.IsSuccess, first.Message);
        Assert.True(second.IsSuccess, second.Message);
        Assert.Equal(2, await db.PurchaseOrders.CountAsync());
        Assert.Equal(
            5m,
            (await db.PurchaseOrderLines.AsNoTracking().ToListAsync())
                .Sum(x => x.OrderedPackageCount.GetValueOrDefault()));
    }

    [Fact]
    public void Pdf_PackagedLineShowsPackages()
    {
        var source = PdfRendererSource();
        Assert.Contains("line.PackageCount", source);
        Assert.Contains("line.PackageQuantity", source);
        Assert.Contains("line.PackagePrice", source);
    }

    [Fact]
    public void Pdf_LooseLineShowsKgWithoutFakePackages()
    {
        var source = PdfRendererSource();
        Assert.Contains("line.PurchaseMode == PurchaseMode.Loose", source);
        Assert.Contains("line.TotalProcurementQuantity", source);
        Assert.Contains("line.UnitPricePerProcurementUnit", source);
        Assert.DoesNotContain("PackageCount = 1", source);
    }

    [Fact]
    public void Revision_PreservesPurchaseModeSnapshot()
    {
        var snapshot = new PurchaseOrderBatchDocumentSnapshot
        {
            Lines = new[]
            {
                new PurchaseOrderBatchDocumentLineSnapshot
                {
                    PurchaseMode = PurchaseMode.Loose,
                    IngredientName = "Cà phê",
                    TotalProcurementQuantity = 8.75m,
                    ProcurementUnitName = "kg",
                    UnitPricePerProcurementUnit = 120_000m,
                    LineTotal = 1_050_000m
                }
            }
        };
        var options = new JsonSerializerOptions();
        options.Converters.Add(new JsonStringEnumConverter());

        var json = JsonSerializer.Serialize(snapshot, options);
        var replay = JsonSerializer.Deserialize<PurchaseOrderBatchDocumentSnapshot>(json, options);

        Assert.Contains("\"ContractVersion\":\"2\"", json);
        Assert.Contains("\"PurchaseMode\":\"Loose\"", json);
        Assert.Equal(PurchaseMode.Loose, replay!.Lines.Single().PurchaseMode);
    }

    [Fact]
    public void Migration_BackfillsExistingData()
    {
        var source = MigrationSource();
        Assert.Contains("[OrderedPackageCount] = [TotalPackageCount]", source);
        Assert.Contains("UPDATE [PurchaseOrderLineAllocations]", source);
        Assert.Contains("UPDATE [PurchaseAdviceLines]", source);
        Assert.Contains("UPDATE [BranchReceiptLines]", source);
    }

    [Fact]
    public void Migration_AppliesOnLegacyDatabase()
    {
        var source = MigrationSource();
        var backfill = source.IndexOf("UPDATE [PurchaseOrderLines]", StringComparison.Ordinal);
        var constraint = source.IndexOf(
            "CK_PurchaseOrderLines_PurchaseModeAuthority",
            backfill,
            StringComparison.Ordinal);

        Assert.True(backfill >= 0);
        Assert.True(constraint > backfill);
        Assert.Contains("Cannot backfill Packaged", source);
        Assert.Contains("Cannot roll back AddLooseProcurementContract while loose procurement data exists", source);
    }

    private async Task<PurchaseOrderReceiptDraftDto> SaveLooseReceiptAsync(
        AppDbContext db,
        decimal received,
        decimal rejected = 0m)
    {
        var seed = await SeedAsync(db);
        var order = await CreateAndSendLooseOrderAsync(db, seed.OfferId, 8.75m);
        return await SaveLooseReceiptForOrderAsync(db, order.PurchaseOrderId, received, rejected);
    }

    private async Task<PurchaseOrderReceiptDraftDto> SaveLooseReceiptForOrderAsync(
        AppDbContext db,
        int purchaseOrderId,
        decimal received,
        decimal rejected = 0m)
    {
        var service = Receipts(db);
        var draft = await service.CreateOrOpenPurchaseOrderDraftAsync(
            purchaseOrderId,
            StaffId,
            StoreId,
            new[] { RoleConstants.StoreManager });
        Assert.True(draft.IsSuccess, draft.Message);
        var line = Assert.Single(draft.Data!.Lines);

        var saved = await service.SavePurchaseOrderDraftAsync(
            new SavePurchaseOrderReceiptDraftRequest
            {
                BranchReceiptId = draft.Data.BranchReceiptId,
                RowVersion = draft.Data.RowVersion,
                ReferenceNumber = "LOOSE-233",
                Lines =
                {
                    new SavePurchaseOrderReceiptDraftLineRequest
                    {
                        PurchaseOrderLineId = line.PurchaseOrderLineId,
                        ActualReceivedQuantity = received,
                        RejectedQuantity = rejected,
                        RejectionIssueType = rejected > 0m ? SupplierReceiptIssueTypes.All.First() : null,
                        RejectionReason = rejected > 0m ? "Không đạt chất lượng" : null
                    }
                }
            },
            StaffId,
            StoreId,
            new[] { RoleConstants.StoreManager });
        Assert.True(saved.IsSuccess, saved.Message);
        return saved.Data!;
    }

    private async Task<PurchaseOrderDetailDto> CreateAndSendLooseOrderAsync(
        AppDbContext db,
        int offerId,
        decimal quantity)
    {
        var service = PurchaseOrders(db);
        var created = await CreateLooseOrderAsync(db, offerId, quantity);
        Assert.True(created.IsSuccess, created.Message);
        var approved = await service.ApproveAsync(
            created.Data!.PurchaseOrderId,
            created.Data.RowVersion,
            StaffId,
            new[] { RoleConstants.BusinessOwner });
        Assert.True(approved.IsSuccess, approved.Message);
        var sent = await service.MarkSentAsync(
            approved.Data!.PurchaseOrderId,
            approved.Data.RowVersion,
            StaffId,
            new[] { RoleConstants.AccountantWarehouse });
        Assert.True(sent.IsSuccess, sent.Message);
        return sent.Data!;
    }

    private Task<ServiceResult<PurchaseOrderDetailDto>> CreateLooseOrderAsync(
        AppDbContext db,
        int offerId,
        decimal quantity) =>
        PurchaseOrders(db).CreateDraftAsync(
            Request(offerId, PurchaseMode.Loose, procurementQuantity: quantity),
            StaffId,
            new[] { RoleConstants.AccountantWarehouse });

    private Task<ServiceResult<ConfirmBranchReceiptResultDto>> ConfirmAsync(
        AppDbContext db,
        PurchaseOrderReceiptDraftDto saved) =>
        Receipts(db).ConfirmAsync(
            saved.BranchReceiptId,
            StaffId,
            StoreId,
            new[] { RoleConstants.StoreManager },
            saved.RowVersion);

    private static CreatePurchaseOrderRequest Request(
        int offerId,
        PurchaseMode mode,
        decimal? packageCount = null,
        decimal? procurementQuantity = null) =>
        new()
        {
            StoreId = StoreId,
            SupplierId = SupplierId,
            Lines =
            {
                new CreatePurchaseOrderLineRequest
                {
                    IngredientId = IngredientId,
                    IngredientSupplierId = offerId,
                    PurchaseMode = mode,
                    PackageCount = packageCount,
                    OrderedProcurementQuantity = procurementQuantity,
                    ProcurementUnitId = KilogramUnitId
                }
            }
        };

    private static PurchaseOrderService PurchaseOrders(AppDbContext db)
    {
        var physical = new PhysicalUnitConversionService(
            db,
            NullLogger<PhysicalUnitConversionService>.Instance);
        var conversion = new UnitConversionService(
            db,
            NullLogger<UnitConversionService>.Instance,
            physical);
        return new PurchaseOrderService(
            db,
            conversion,
            new RestockAllocationService(db, new NoPurchaseOrderAllocationProvider()));
    }

    private static BranchReceiptService Receipts(AppDbContext db)
    {
        var physical = new PhysicalUnitConversionService(
            db,
            NullLogger<PhysicalUnitConversionService>.Instance);
        var conversion = new UnitConversionService(
            db,
            NullLogger<UnitConversionService>.Instance,
            physical);
        var alerts = new Mock<IStockAlertService>();
        alerts
            .Setup(x => x.EvaluateStoreInventoryItemAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(ServiceResult<StockAlertEvaluationResultDto>.Success(new()));
        return new BranchReceiptService(
            db,
            conversion,
            physical,
            Mock.Of<IInventoryWriterModeService>(),
            Mock.Of<IStoreInventoryWriteResolver>(),
            new RestockFulfillmentPostingService(db),
            alerts.Object,
            new ScopeAuthorizationService(db),
            NullLogger<BranchReceiptService>.Instance,
            PurchaseOrders(db));
    }

    private static async Task<Seed> SeedAsync(AppDbContext db, bool allowsLoose = true)
    {
        var now = DateTime.UtcNow;
        var store = new Store
        {
            StoreId = StoreId,
            Name = "Store #233",
            Address = "Test",
            Phone = "0900233001",
            Active = true,
            CreatedAt = now
        };
        var otherStore = new Store
        {
            StoreId = OtherStoreId,
            Name = "Store #233 B",
            Address = "Test",
            Phone = "0900233002",
            Active = true,
            CreatedAt = now
        };
        var account = new Account
        {
            AccountId = StaffId,
            Email = "staff233@test.local",
            PasswordHash = "x",
            Active = true,
            CreatedAt = now
        };
        var staff = new Staff
        {
            StaffId = StaffId,
            AccountId = StaffId,
            StoreId = StoreId,
            FullName = "Warehouse #233",
            Active = true,
            CreatedAt = now
        };
        var ingredient = new Ingredient
        {
            IngredientId = IngredientId,
            Code = "ING-233",
            Name = "Cà phê mua lẻ",
            BaseUnitId = GramUnitId,
            Active = true
        };
        var supplier = new Supplier
        {
            SupplierId = SupplierId,
            Code = "SUP-233",
            Name = "Supplier #233",
            Active = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.AddRange(store, otherStore, account, staff, ingredient, supplier);
        db.SupplierStores.AddRange(
            new SupplierStore
            {
                SupplierId = SupplierId,
                StoreId = StoreId,
                Active = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new SupplierStore
            {
                SupplierId = SupplierId,
                StoreId = OtherStoreId,
                Active = true,
                CreatedAt = now,
                UpdatedAt = now
            });
        await db.SaveChangesAsync();

        var offer = new IngredientSupplier
        {
            IngredientId = IngredientId,
            SupplierId = SupplierId,
            UnitId = KilogramUnitId,
            PackageQuantity = 1m,
            CurrentPrice = 120_000m,
            MinimumOrderPackageCount = 1,
            LeadTimeDays = 2,
            Active = true,
            AllowsLoosePurchase = allowsLoose,
            LooseProcurementUnitId = allowsLoose ? KilogramUnitId : null,
            CurrentProcurementUnitPrice = allowsLoose ? 120_000m : null,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.IngredientSuppliers.Add(offer);
        await db.SaveChangesAsync();
        return new Seed(offer.IngredientSupplierId);
    }

    private static string MigrationSource()
    {
        var path = Directory
            .EnumerateFiles(
                Path.Combine(FindRepoRoot(), "CafeChain", "Migrations"),
                "*_AddLooseProcurementContract.cs")
            .Single();
        return File.ReadAllText(path);
    }

    private static string PdfRendererSource() =>
        File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "CafeChain",
            "Application",
            "Services",
            "Inventories",
            "PurchaseOrderBatchPdfRenderer.cs"));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CafeChain", "CafeChain.slnx")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Không tìm thấy thư mục gốc CafeChain.slnx.");
    }

    private sealed record Seed(int OfferId);
}
