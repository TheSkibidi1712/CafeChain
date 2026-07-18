using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Services.Inventories;
using CafeChain.Data;
using CafeChain.Models.Customers;
using CafeChain.Models.Inventories.Procurement;
using CafeChain.Models.Inventories.Suppliers;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CafeChain.Tests;

public sealed class PurchaseOrderBatchUiIssue188Tests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly AppDbContext _db;
    private readonly Mock<IPurchaseOrderBatchPdfRenderer> _renderer = new();
    private readonly Mock<IPurchaseOrderBatchDocumentStorage> _storage = new();
    private readonly Mock<IScopeAuthorizationService> _scope = new();

    public PurchaseOrderBatchUiIssue188Tests()
    {
        _connection.Open();
        _db = new TestDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
    }

    [Fact]
    public async Task BatchSend_RequiresExistingRevision()
    {
        var seed = await SeedAsync();
        var result = await Service().MarkSentAsync(seed.BatchId, 999999, Request(seed.RowVersion), Actor(seed.StaffId, RoleConstants.AccountantWarehouse));
        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseOrderBatchErrorCodes.DocumentNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task BatchSend_MarksZaloChannel()
    {
        var seed = await SeedAsync();
        var result = await Service().MarkSentAsync(seed.BatchId, seed.RevisionId, Request(seed.RowVersion, "Đã gửi nhóm Zalo NCC"), Actor(seed.StaffId, RoleConstants.AccountantWarehouse));
        Assert.True(result.IsSuccess, result.Message);
        var revision = await _db.PurchaseOrderBatchDocumentRevisions.AsNoTracking().SingleAsync();
        Assert.Equal(PurchaseOrderBatchDocumentStatuses.Sent, revision.Status);
        Assert.Equal(PurchaseOrderBatchDocumentChannels.ZaloManual, revision.SentChannel);
        Assert.Equal("Đã gửi nhóm Zalo NCC", revision.SentNote);
        Assert.Equal(seed.StaffId, revision.SentByStaffId);
        Assert.NotNull(revision.SentAtUtc);
    }

    [Fact]
    public async Task BatchSend_IsIdempotent()
    {
        var seed = await SeedAsync();
        var request = Request(seed.RowVersion);
        var first = await Service().MarkSentAsync(seed.BatchId, seed.RevisionId, request, Actor(seed.StaffId, RoleConstants.AccountantWarehouse));
        var second = await Service().MarkSentAsync(seed.BatchId, seed.RevisionId, request, Actor(seed.StaffId, RoleConstants.AccountantWarehouse));
        Assert.True(first.IsSuccess && second.IsSuccess);
        Assert.Equal(first.Data!.RevisionId, second.Data!.RevisionId);
        Assert.Equal(1, await _db.PurchaseOrderBatchDocumentRevisions.CountAsync(x => x.SentAtUtc != null));
    }

    [Fact]
    public async Task BatchSend_DoesNotConfirmSupplier()
    {
        var seed = await SeedAsync();
        var result = await Service().MarkSentAsync(seed.BatchId, seed.RevisionId, Request(seed.RowVersion), Actor(seed.StaffId, RoleConstants.BusinessOwner));
        Assert.True(result.IsSuccess, result.Message);
        var batch = await _db.PurchaseOrderBatches.AsNoTracking().SingleAsync();
        Assert.Equal(PurchaseOrderBatchStatuses.SentToSupplier, batch.Status);
        Assert.DoesNotContain("CONFIRMED", batch.Status, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BatchSend_WrongRoleRejected()
    {
        var seed = await SeedAsync();
        var result = await Service().MarkSentAsync(seed.BatchId, seed.RevisionId, Request(seed.RowVersion), Actor(seed.StaffId, RoleConstants.StoreManager));
        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseOrderBatchErrorCodes.Forbidden, result.ErrorCode);
        Assert.Equal(PurchaseOrderBatchDocumentStatuses.Generated,
            (await _db.PurchaseOrderBatchDocumentRevisions.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public void BatchUi_ShowsActionsByState_AndManualZaloSemantics()
    {
        var view = ReadRepoFile("CafeChain/Areas/Admin/Views/AdminPurchaseOrderBatches/Details.cshtml");
        Assert.Contains("canApprove", view);
        Assert.Contains("canGenerate", view);
        Assert.Contains("canCancel", view);
        Assert.Contains("Sao chép nội dung Zalo", view);
        Assert.Contains("Đánh dấu đã gửi NCC qua Zalo", view);
        Assert.Contains("không đồng nghĩa nhà cung cấp đã xác nhận", view);
    }

    [Fact]
    public void BatchUi_ShowsChildPoProgress_AndPdfRevisionHistory()
    {
        var view = ReadRepoFile("CafeChain/Areas/Admin/Views/AdminPurchaseOrderBatches/Details.cshtml");
        Assert.Contains("Tiến độ giao theo chi nhánh", view);
        Assert.Contains("child.AcceptedBaseQuantity", view);
        Assert.Contains("Lịch sử revision", view);
        Assert.Contains("revision.SentAtUtc", view);
    }

    [Fact]
    public void PurchaseAdviceUi_PreservesValuesOnValidationError_AndShowsCommercialWarnings()
    {
        var view = ReadRepoFile("CafeChain/Areas/Admin/Views/AdminPurchaseAdviceConsolidation/Index.cshtml");
        Assert.Contains("submitted?.IngredientSupplierId", view);
        Assert.Contains("submitted?.PackageCount", view);
        Assert.Contains("Các giá trị vừa nhập vẫn được giữ", view);
        Assert.Contains("MOQ", view);
        Assert.Contains("lead time", view);
        Assert.Contains("PackagePriceSnapshot", view);
        Assert.Contains("group.Allocations", view);
    }

    private PurchaseOrderBatchDocumentService Service() => new(_db, _renderer.Object, _storage.Object, _scope.Object);

    private async Task<Seed> SeedAsync()
    {
        var now = DateTime.UtcNow;
        var store = new Store { Name = "Store 188", Address = "Quận 1", Phone = "0900000188", Active = true, CreatedAt = now };
        var supplier = new Supplier { Code = "SUP188" + Guid.NewGuid().ToString("N")[..4], Name = "NCC Zalo", Active = true, CreatedAt = now, UpdatedAt = now };
        var account = new Account { Email = Guid.NewGuid() + "@test.local", PasswordHash = "x", Active = true, CreatedAt = now };
        _db.AddRange(store, supplier, account);
        await _db.SaveChangesAsync();
        var staff = new Staff { AccountId = account.AccountId, StoreId = store.StoreId, FullName = "Kế toán kho", Active = true, BaseSalary = 0, CreatedAt = now };
        _db.Add(staff);
        await _db.SaveChangesAsync();
        var batch = new PurchaseOrderBatch
        {
            BatchNumber = "POB-188-" + Guid.NewGuid().ToString("N")[..6],
            RequestKey = Guid.NewGuid().ToString("N"), SupplierId = supplier.SupplierId,
            Status = PurchaseOrderBatchStatuses.PdfGenerated, Currency = "VND",
            ExpectedDeliveryFrom = now.Date.AddDays(1), ExpectedDeliveryTo = now.Date.AddDays(2),
            CreatedByStaffId = staff.StaffId, ApprovedByStaffId = staff.StaffId,
            ApprovedAtUtc = now, CreatedAtUtc = now, UpdatedAtUtc = now
        };
        _db.Add(batch);
        await _db.SaveChangesAsync();
        var revision = new PurchaseOrderBatchDocumentRevision
        {
            PurchaseOrderBatchId = batch.PurchaseOrderBatchId, RevisionNumber = 1,
            GeneratedAtUtc = now, GeneratedByStaffId = staff.StaffId,
            FileName = "POB-188-R1.pdf", StorageReference = "tests/POB-188-R1.pdf",
            ContentHash = new string('a', 64), SnapshotJson = "{}",
            Status = PurchaseOrderBatchDocumentStatuses.Generated, CreatedAtUtc = now
        };
        _db.Add(revision);
        await _db.SaveChangesAsync();
        return new(batch.PurchaseOrderBatchId, revision.PurchaseOrderBatchDocumentRevisionId, staff.StaffId, Convert.ToBase64String(revision.RowVersion));
    }

    private static MarkPurchaseOrderBatchDocumentSentRequest Request(string rowVersion, string? note = null) => new()
    {
        Channel = PurchaseOrderBatchDocumentChannels.ZaloManual,
        IdempotencyKey = Guid.NewGuid().ToString("N"),
        RowVersion = rowVersion,
        Note = note
    };

    private static AdminActorContext Actor(int staffId, string role) => new()
    {
        StaffId = staffId,
        StoreId = 1,
        RoleNames = new[] { role }
    };

    private static string ReadRepoFile(string relativePath)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private sealed record Seed(int BatchId, int RevisionId, int StaffId, string RowVersion);
}
