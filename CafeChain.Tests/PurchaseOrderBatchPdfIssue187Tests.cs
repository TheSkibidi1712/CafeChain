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
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using QuestPDF.Infrastructure;
using Xunit;

namespace CafeChain.Tests;

public sealed class PurchaseOrderBatchPdfIssue187Tests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly AppDbContext _db;
    private readonly MemoryDocumentStorage _storage = new();
    private readonly CapturingRenderer _renderer = new();
    private readonly Mock<IScopeAuthorizationService> _scope = new();

    public PurchaseOrderBatchPdfIssue187Tests()
    {
        _connection.Open();
        _db = new TestDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _scope.Setup(x => x.CanAccessStoreAsync(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(true);
    }

    [Fact]
    public async Task BatchPdf_RequiresApprovedBatch()
    {
        var seed = await SeedAsync(PurchaseOrderBatchStatuses.PendingApproval);
        var result = await Service().GenerateAsync(seed.BatchId, Owner(seed.StaffId));
        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseOrderBatchErrorCodes.Invalid, result.ErrorCode);
        Assert.Empty(await _db.PurchaseOrderBatchDocumentRevisions.ToListAsync());
    }

    [Fact]
    public async Task BatchPdf_UsesSnapshotData_AndContainsSupplierTaxCodeAndStoreAllocations()
    {
        var seed = await SeedAsync();
        var result = await Service().GenerateAsync(seed.BatchId, Owner(seed.StaffId));
        Assert.True(result.IsSuccess, result.Message);
        var snapshot = Assert.Single(_renderer.Snapshots);
        Assert.Equal("0312345678", snapshot.Supplier.TaxCode);
        Assert.Equal("Cà phê rang xay Đắk Lắk", Assert.Single(snapshot.Lines).IngredientName);
        Assert.Equal(2, snapshot.Stores.Count);
        Assert.All(snapshot.Stores, store => Assert.NotEmpty(store.Lines));
        Assert.Contains("Cửa hàng Nguyễn Huệ", snapshot.Stores.Select(x => x.StoreName));
        var persisted = await _db.PurchaseOrderBatchDocumentRevisions.AsNoTracking().SingleAsync();
        Assert.Contains("0312345678", persisted.SnapshotJson);
        Assert.Contains("Đắk Lắk", persisted.SnapshotJson);
    }

    [Fact]
    public void BatchPdf_SupportsVietnameseText()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var renderer = new PurchaseOrderBatchPdfRenderer();
        var pdf = renderer.Render(SampleSnapshot(), 1, DateTime.UtcNow, new string('a', 64));
        Assert.True(pdf.Length > 1_000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public async Task BatchPdf_CreatesRevisionOne()
    {
        var seed = await SeedAsync();
        var result = await Service().GenerateAsync(seed.BatchId, Owner(seed.StaffId));
        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(1, result.Data!.RevisionNumber);
        Assert.Equal(PurchaseOrderBatchDocumentStatuses.Generated, result.Data.Status);
        Assert.Matches($"CafeChain_PO_.*_R1\\.pdf", result.Data.FileName);
        Assert.Single(_storage.Files);
    }

    [Fact]
    public async Task BatchPdf_SameSnapshotIsIdempotent_AndContentHashStable()
    {
        var seed = await SeedAsync();
        var first = await Service().GenerateAsync(seed.BatchId, Owner(seed.StaffId));
        var second = await Service().GenerateAsync(seed.BatchId, Owner(seed.StaffId));
        Assert.True(first.IsSuccess && second.IsSuccess);
        Assert.Equal(first.Data!.RevisionId, second.Data!.RevisionId);
        Assert.Equal(first.Data.ContentHash, second.Data.ContentHash);
        Assert.Equal(1, await _db.PurchaseOrderBatchDocumentRevisions.CountAsync());
        Assert.Single(_storage.Files);
    }

    [Fact]
    public async Task BatchPdf_ChangedSnapshotCreatesNextRevision()
    {
        var seed = await SeedAsync();
        var first = await Service().GenerateAsync(seed.BatchId, Owner(seed.StaffId));
        var line = await _db.PurchaseOrderBatchLines.SingleAsync(x => x.PurchaseOrderBatchId == seed.BatchId);
        line.TotalPackageCount += 1;
        line.TotalBaseQuantity += line.PackageQuantitySnapshot;
        line.LineTotal = line.TotalPackageCount * line.PackagePriceSnapshot;
        await _db.SaveChangesAsync();
        var second = await Service().GenerateAsync(seed.BatchId, Owner(seed.StaffId));
        Assert.True(second.IsSuccess, second.Message);
        Assert.Equal(2, second.Data!.RevisionNumber);
        var old = await _db.PurchaseOrderBatchDocumentRevisions.SingleAsync(x => x.PurchaseOrderBatchDocumentRevisionId == first.Data!.RevisionId);
        Assert.Equal(PurchaseOrderBatchDocumentStatuses.Superseded, old.Status);
        Assert.Equal(second.Data.RevisionId, old.SupersededByRevisionId);
        Assert.Equal(2, _storage.Files.Count);
    }

    [Fact]
    public async Task BatchPdf_SentRevisionCannotBeOverwritten()
    {
        var seed = await SeedAsync();
        var generated = await Service().GenerateAsync(seed.BatchId, Owner(seed.StaffId));
        var before = _storage.Files.Single().Value.ToArray();
        var sent = await Service().MarkSentAsync(seed.BatchId, generated.Data!.RevisionId,
            new()
            {
                Channel = PurchaseOrderBatchDocumentChannels.ZaloManual,
                RowVersion = generated.Data.RowVersion,
                IdempotencyKey = Guid.NewGuid().ToString("N")
            }, Owner(seed.StaffId));
        var replay = await Service().GenerateAsync(seed.BatchId, Owner(seed.StaffId));
        Assert.True(sent.IsSuccess && replay.IsSuccess);
        Assert.Equal(PurchaseOrderBatchDocumentStatuses.Sent, replay.Data!.Status);
        Assert.Equal(generated.Data.RevisionId, replay.Data.RevisionId);
        Assert.Equal(before, _storage.Files.Single().Value);
        Assert.Single(_storage.Files);

        var line = await _db.PurchaseOrderBatchLines.SingleAsync(x => x.PurchaseOrderBatchId == seed.BatchId);
        line.PackagePriceSnapshot += 10_000m;
        line.LineTotal = line.TotalPackageCount * line.PackagePriceSnapshot;
        await _db.SaveChangesAsync();
        var next = await Service().GenerateAsync(seed.BatchId, Owner(seed.StaffId));
        Assert.True(next.IsSuccess, next.Message);
        Assert.Equal(2, next.Data!.RevisionNumber);
        var old = await _db.PurchaseOrderBatchDocumentRevisions.AsNoTracking()
            .SingleAsync(x => x.PurchaseOrderBatchDocumentRevisionId == generated.Data.RevisionId);
        Assert.Equal(PurchaseOrderBatchDocumentStatuses.Superseded, old.Status);
        Assert.Equal(next.Data.RevisionId, old.SupersededByRevisionId);
        Assert.Equal(before, _storage.Files.Single(x => x.Key.EndsWith("_R1.pdf", StringComparison.Ordinal)).Value);
        Assert.Equal(2, _storage.Files.Count);
    }

    [Fact]
    public async Task BatchPdf_DownloadRequiresAuthorization()
    {
        var seed = await SeedAsync();
        var generated = await Service().GenerateAsync(seed.BatchId, Owner(seed.StaffId));
        var denied = await Service().DownloadAsync(generated.Data!.RevisionId,
            new() { StaffId = 999, StoreId = seed.Store1Id, RoleNames = new[] { RoleConstants.StoreManager } });
        var allowed = await Service().DownloadAsync(generated.Data.RevisionId, Owner(seed.StaffId));
        Assert.False(denied.IsSuccess);
        Assert.Equal(PurchaseOrderBatchErrorCodes.Forbidden, denied.ErrorCode);
        Assert.True(allowed.IsSuccess);
        Assert.Equal("application/pdf", allowed.Data!.ContentType);
    }

    private PurchaseOrderBatchDocumentService Service() => new(_db, _renderer, _storage, _scope.Object);

    private async Task<Seed> SeedAsync(string status = PurchaseOrderBatchStatuses.Approved)
    {
        var now = DateTime.UtcNow;
        var store1 = new Store { Name = "Cửa hàng Nguyễn Huệ", Address = "01 Nguyễn Huệ, Quận 1", Phone = "0901000001", Active = true, CreatedAt = now };
        var store2 = new Store { Name = "Cửa hàng Thảo Điền", Address = "02 Xuân Thủy, Thủ Đức", Phone = "0901000002", Active = true, CreatedAt = now };
        var unit = new CafeChain.Models.Inventories.Ingredients.Unit { UnitCode = "kg" + Guid.NewGuid().ToString("N")[..6], Name = "kg", Active = true };
        var ingredient = new Ingredient { Code = "I187" + Guid.NewGuid().ToString("N")[..6], Name = "Cà phê rang xay Đắk Lắk", Active = true, BaseUnit = unit };
        var supplier = new Supplier
        {
            Code = "SUP187" + Guid.NewGuid().ToString("N")[..4], Name = "Nhà cung cấp Việt", TaxCode = "0312345678",
            Address = "10 Lê Lợi, TP.HCM", Active = true, CreatedAt = now, UpdatedAt = now,
            Contacts = new List<SupplierContact> { new() { Name = "Nguyễn An", Email = "an@supplier.test", PhoneNumber = "0909000000", IsPrimary = true, Active = true } }
        };
        var account = new Account { Email = Guid.NewGuid() + "@test.local", PasswordHash = "x", Active = true, CreatedAt = now };
        _db.AddRange(store1, store2, ingredient, supplier, account);
        await _db.SaveChangesAsync();
        var staff = new Staff { AccountId = account.AccountId, StoreId = store1.StoreId, FullName = "Chủ doanh nghiệp", Active = true, CreatedAt = now};
        var offer = new IngredientSupplier
        {
            IngredientId = ingredient.IngredientId, SupplierId = supplier.SupplierId, UnitId = unit.UnitId,
            PackageQuantity = 1m, CurrentPrice = 120000m, MinimumOrderPackageCount = 1, Active = true, CreatedAt = now, UpdatedAt = now
        };
        _db.AddRange(staff, offer);
        await _db.SaveChangesAsync();
        var batch = new PurchaseOrderBatch
        {
            BatchNumber = "POB-2026-0187", RequestKey = Guid.NewGuid().ToString("N"), SupplierId = supplier.SupplierId,
            Status = status, Currency = "VND", ExpectedDeliveryFrom = now.Date.AddDays(1), ExpectedDeliveryTo = now.Date.AddDays(3),
            Note = "Giao giờ hành chính", CreatedByStaffId = staff.StaffId, ApprovedByStaffId = status == PurchaseOrderBatchStatuses.Approved ? staff.StaffId : null,
            ApprovedAtUtc = status == PurchaseOrderBatchStatuses.Approved ? now : null, CreatedAtUtc = now, UpdatedAtUtc = now,
            Lines = new List<PurchaseOrderBatchLine>
            {
                new() { IngredientId = ingredient.IngredientId, IngredientSupplierId = offer.IngredientSupplierId, PackageUnitId = unit.UnitId,
                    PackageQuantitySnapshot = 1m, TotalPackageCount = 10m, TotalBaseQuantity = 10m, PackagePriceSnapshot = 120000m,
                    LineTotal = 1200000m, Currency = "VND" }
            },
            ChildPurchaseOrders = new List<PurchaseOrder>
            {
                Child(store1.StoreId, "PO-S1-187", 4m, now, staff.StaffId, supplier.SupplierId, ingredient.IngredientId, offer.IngredientSupplierId, unit.UnitId),
                Child(store2.StoreId, "PO-S2-187", 6m, now, staff.StaffId, supplier.SupplierId, ingredient.IngredientId, offer.IngredientSupplierId, unit.UnitId)
            }
        };
        _db.Add(batch);
        await _db.SaveChangesAsync();
        return new(batch.PurchaseOrderBatchId, staff.StaffId, store1.StoreId);
    }

    private static PurchaseOrder Child(int storeId, string code, decimal quantity, DateTime now, int staffId, int supplierId, int ingredientId, int offerId, int unitId) => new()
    {
        Code = code, StoreId = storeId, SupplierId = supplierId, Status = PurchaseOrderStatuses.Approved,
        OrderDate = now, ExpectedDeliveryAtUtc = now.Date.AddDays(2), CreatedByStaffId = staffId, ApprovedByStaffId = staffId,
        CreatedAtUtc = now, UpdatedAtUtc = now, ApprovedAtUtc = now,
        Lines = new List<PurchaseOrderLine>
        {
            new() { IngredientId = ingredientId, IngredientSupplierId = offerId, PackageUnitIdSnapshot = unitId,
                PackageQuantitySnapshot = 1m, PackagePriceSnapshot = 120000m, PackageCount = quantity,
                OrderedBaseQuantity = quantity, PromisedLeadTimeDaysSnapshot = 2 }
        }
    };

    private static AdminActorContext Owner(int staffId) => new() { StaffId = staffId, RoleNames = new[] { RoleConstants.BusinessOwner } };

    private static PurchaseOrderBatchDocumentSnapshot SampleSnapshot() => new()
    {
        BatchNumber = "POB-VIETNAMESE", CreatedAtUtc = DateTime.UtcNow, CreatedByName = "Nguyễn Văn An", ApprovedByName = "Trần Thị Bình",
        ExpectedDeliveryFrom = DateTime.Today, ExpectedDeliveryTo = DateTime.Today.AddDays(2), Currency = "VND", TotalAmount = 120000m,
        Supplier = new() { Name = "Nhà cung cấp Cà phê Việt", TaxCode = "0312345678", Address = "Đắk Lắk", ContactName = "Nguyễn An" },
        Lines = new[] { new PurchaseOrderBatchDocumentLineSnapshot { IngredientName = "Cà phê rang xay", PackageUnitName = "kg", PackageQuantity = 1, PackageCount = 1, PackagePrice = 120000, LineTotal = 120000 } },
        Stores = new[] { new PurchaseOrderBatchDocumentStoreSnapshot { StoreName = "Cửa hàng Nguyễn Huệ", PurchaseOrderCode = "PO-01", DeliveryAddress = "Quận 1", Lines = new[] { new PurchaseOrderBatchDocumentStoreLineSnapshot { IngredientName = "Cà phê", PackageUnitName = "kg", PackageQuantity = 1, PackageCount = 1, BaseQuantity = 1 } } } }
    };

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private sealed record Seed(int BatchId, int StaffId, int Store1Id);

    private sealed class CapturingRenderer : IPurchaseOrderBatchPdfRenderer
    {
        public List<PurchaseOrderBatchDocumentSnapshot> Snapshots { get; } = new();
        public byte[] Render(PurchaseOrderBatchDocumentSnapshot snapshot, int revisionNumber, DateTime generatedAtUtc, string contentHash)
        {
            Snapshots.Add(snapshot);
            return System.Text.Encoding.UTF8.GetBytes($"%PDF-{revisionNumber}-{contentHash}");
        }
    }

    internal sealed class MemoryDocumentStorage : IPurchaseOrderBatchDocumentStorage
    {
        public ConcurrentDictionary<string, byte[]> Files { get; } = new();
        public Task SaveAsync(string storageReference, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
        {
            if (!Files.TryAdd(storageReference, content.ToArray())) throw new IOException("File already exists.");
            return Task.CompletedTask;
        }
        public Task<byte[]?> ReadAsync(string storageReference, CancellationToken cancellationToken = default) =>
            Task.FromResult(Files.TryGetValue(storageReference, out var bytes) ? bytes.ToArray() : null);
        public Task DeleteAsync(string storageReference, CancellationToken cancellationToken = default)
        {
            Files.TryRemove(storageReference, out _);
            return Task.CompletedTask;
        }
    }
}
