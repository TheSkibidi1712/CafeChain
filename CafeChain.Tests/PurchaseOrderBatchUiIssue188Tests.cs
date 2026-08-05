using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Services.Inventories;
using CafeChain.Areas.Admin.Controllers;
using CafeChain.Data;
using CafeChain.Models.Customers;
using CafeChain.Models.Enums.Inventory;
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
    public async Task BatchSend_SupportsOtherChannelAndRejectsCancelledBatch()
    {
        var seed = await SeedAsync();
        var other = await Service().MarkSentAsync(
            seed.BatchId,
            seed.RevisionId,
            Request(seed.RowVersion, "Gửi trực tiếp", PurchaseOrderBatchDocumentChannels.OtherManual),
            Actor(seed.StaffId, RoleConstants.AccountantWarehouse));
        Assert.True(other.IsSuccess, other.Message);
        Assert.Equal(
            PurchaseOrderBatchDocumentChannels.OtherManual,
            (await _db.PurchaseOrderBatchDocumentRevisions.AsNoTracking().SingleAsync()).SentChannel);

        _db.ChangeTracker.Clear();
        var batch = await _db.PurchaseOrderBatches.SingleAsync();
        batch.Status = PurchaseOrderBatchStatuses.Cancelled;
        var revision = await _db.PurchaseOrderBatchDocumentRevisions.SingleAsync();
        revision.Status = PurchaseOrderBatchDocumentStatuses.Generated;
        revision.SentChannel = null;
        revision.SentAtUtc = null;
        revision.SentByStaffId = null;
        revision.SentIdempotencyKey = null;
        await _db.SaveChangesAsync();

        var rejected = await Service().MarkSentAsync(
            seed.BatchId,
            seed.RevisionId,
            Request(Convert.ToBase64String(revision.RowVersion)),
            Actor(seed.StaffId, RoleConstants.AccountantWarehouse));
        Assert.False(rejected.IsSuccess);
        Assert.Equal(PurchaseOrderBatchErrorCodes.Invalid, rejected.ErrorCode);
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
        Assert.Contains("Sao chép nội dung gửi Zalo", view);
        Assert.Contains("Mở để in PDF", view);
        Assert.Contains("PurchaseOrderBatchDocumentChannels.OtherManual", view);
        Assert.Contains("Đánh dấu đã gửi Nhà cung cấp", view);
        Assert.Contains("không đồng nghĩa nhà cung cấp đã xác nhận", view);
    }

    [Fact]
    public void BatchUi_ShowsChildPoProgress_AndPdfRevisionHistory()
    {
        var view = ReadRepoFile("CafeChain/Areas/Admin/Views/AdminPurchaseOrderBatches/Details.cshtml");
        Assert.Contains("Tiến độ giao theo chi nhánh", view);
        Assert.Contains("child.AcceptedBaseQuantity", view);
        Assert.Contains("Lịch sử PDF và gửi Nhà cung cấp", view);
        Assert.Contains("revision.SentAtUtc", view);
    }

    [Fact]
    public void PurchaseAdviceUi_PreservesOfferAndDerivesPackageCountWithCommercialWarnings()
    {
        var view = ReadRepoFile("CafeChain/Areas/Admin/Views/AdminPurchaseAdviceConsolidation/Index.cshtml");
        Assert.Contains("submitted?.IngredientSupplierId", view);
        Assert.Contains("suggestedPackageCount", view);
        Assert.Contains("readonly", view);
        Assert.Contains("Dư quy cách", view);
        Assert.Contains("Các giá trị vừa nhập vẫn được giữ", view);
        Assert.Contains("tối thiểu", view);
        Assert.Contains("giao trong", view);
        Assert.Contains("PackagePriceSnapshot", view);
        Assert.Contains("group.Allocations", view);
        Assert.Contains("normalizeSelectionPayload", view);
        var controller = ReadRepoFile("CafeChain/Areas/Admin/Controllers/AdminPurchaseAdviceConsolidationController.cs");
        Assert.Contains("NormalizePurchaseModeFields(request.Lines)", controller);
        Assert.Contains("line.PackageCount = null", controller);
        Assert.Contains("line.OrderedProcurementQuantity = null", controller);
        Assert.Contains("1 gói =", view);
        Assert.Contains("AdminStatusDisplay.Unit", view);
        Assert.DoesNotContain("1 kiện =", view);
        Assert.Contains("preview?.LineCount > 1", view);
        Assert.Contains("? \"Tạo đơn đặt hàng gộp\"", view);
        Assert.Contains(": \"Tạo đơn đặt hàng\"", view);
    }

    [Fact]
    public void PurchaseAdvicePreview_NormalizesMutuallyExclusiveQuantityFields()
    {
        var lines = new List<PurchaseAdviceConsolidationSelectionRequest>
        {
            new()
            {
                PurchaseMode = PurchaseMode.Packaged,
                PackageCount = 10,
                OrderedProcurementQuantity = 10m
            },
            new()
            {
                PurchaseMode = PurchaseMode.Loose,
                PackageCount = 10,
                OrderedProcurementQuantity = 7.5m
            }
        };
        var normalize = typeof(AdminPurchaseAdviceConsolidationController).GetMethod(
            "NormalizePurchaseModeFields",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(normalize);
        normalize!.Invoke(null, new object[] { lines });

        Assert.Equal(10, lines[0].PackageCount);
        Assert.Null(lines[0].OrderedProcurementQuantity);
        Assert.Null(lines[1].PackageCount);
        Assert.Equal(7.5m, lines[1].OrderedProcurementQuantity);
    }

    [Fact]
    public void PackagedPreview_ToCreateForm_PostsOnlyPackageCount()
    {
        var createForm = ReadCreateForm();
        var packagedBranch = Slice(
            createForm,
            "@if (allocation.PurchaseMode == PurchaseMode.Packaged)",
            "else");

        Assert.Contains("Lines[@selectionIndex].PackageCount", packagedBranch);
        Assert.DoesNotContain("Lines[@selectionIndex].OrderedProcurementQuantity", packagedBranch);
    }

    [Fact]
    public void LoosePayload_PostsOnlyOrderedProcurementQuantity()
    {
        var createForm = ReadCreateForm();
        var looseBranch = Slice(
            createForm,
            "else",
            "Lines[@selectionIndex].RowVersion");

        Assert.Contains("Lines[@selectionIndex].OrderedProcurementQuantity", looseBranch);
        Assert.DoesNotContain("Lines[@selectionIndex].PackageCount", looseBranch);
    }

    [Fact]
    public void CreateForm_ModelBinding_DoesNotRoundTripDerivedFields()
    {
        Assert.Equal(typeof(object), typeof(CreatePurchaseOrderBatchRequest).BaseType);
        Assert.Equal(
            typeof(List<CreatePurchaseOrderBatchLineRequest>),
            typeof(CreatePurchaseOrderBatchRequest).GetProperty(nameof(CreatePurchaseOrderBatchRequest.Lines))!.PropertyType);

        var request = new CreatePurchaseOrderBatchRequest
        {
            SupplierId = 7,
            Lines =
            {
                new()
                {
                    PurchaseAdviceLineId = 11,
                    IngredientSupplierId = 13,
                    PurchaseMode = PurchaseMode.Packaged,
                    PackageCount = 10
                }
            }
        };

        var line = Assert.Single(request.ToPreviewRequest().Lines);
        Assert.Equal(10, line.PackageCount);
        Assert.Null(line.OrderedProcurementQuantity);
    }

    [Fact]
    public void MultipleLines_PreserveEachLinePurchaseMode()
    {
        var request = new CreatePurchaseOrderBatchRequest
        {
            SupplierId = 7,
            Lines =
            {
                new() { PurchaseAdviceLineId = 1, PurchaseMode = PurchaseMode.Packaged, PackageCount = 2 },
                new() { PurchaseAdviceLineId = 2, PurchaseMode = PurchaseMode.Loose, OrderedProcurementQuantity = 3.5m }
            }
        };

        var mapped = request.ToPreviewRequest();

        Assert.Equal(PurchaseMode.Packaged, mapped.Lines[0].PurchaseMode);
        Assert.Equal(2, mapped.Lines[0].PackageCount);
        Assert.Null(mapped.Lines[0].OrderedProcurementQuantity);
        Assert.Equal(PurchaseMode.Loose, mapped.Lines[1].PurchaseMode);
        Assert.Null(mapped.Lines[1].PackageCount);
        Assert.Equal(3.5m, mapped.Lines[1].OrderedProcurementQuantity);
    }

    [Fact]
    public void ModeAndSupplierChanges_ClearStaleQuantityContracts()
    {
        var view = ReadRepoFile("CafeChain/Areas/Admin/Views/AdminPurchaseAdviceConsolidation/Index.cshtml");

        Assert.Contains("if (packInput) packInput.value = '';", view);
        Assert.Contains("if (looseInput) looseInput.value = '';", view);
        Assert.Contains("const updatePackMath = (row, resetQuantity = false)", view);
        Assert.Contains("supplier.addEventListener('change', () => syncOffers(true))", view);
        Assert.Contains("updatePackMath(row, resetQuantity)", view);
        Assert.Contains("updatePackMath(select.closest('[data-line]'), true)", view);
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
        var staff = new Staff { AccountId = account.AccountId, StoreId = store.StoreId, FullName = "Kế toán kho", Active = true, CreatedAt = now };
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

    private static MarkPurchaseOrderBatchDocumentSentRequest Request(
        string rowVersion,
        string? note = null,
        string channel = PurchaseOrderBatchDocumentChannels.ZaloManual) => new()
    {
        Channel = channel,
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

    private static string ReadCreateForm()
    {
        var view = ReadRepoFile("CafeChain/Areas/Admin/Views/AdminPurchaseAdviceConsolidation/Index.cshtml");
        return Slice(
            view,
            "<form asp-controller=\"@(createsConsolidatedOrder",
            "</form>");
    }

    private static string Slice(string source, string start, string end)
    {
        var startIndex = source.IndexOf(start, StringComparison.Ordinal);
        Assert.True(startIndex >= 0, $"Không tìm thấy mốc bắt đầu: {start}");
        var endIndex = source.IndexOf(end, startIndex + start.Length, StringComparison.Ordinal);
        Assert.True(endIndex >= 0, $"Không tìm thấy mốc kết thúc: {end}");
        return source[startIndex..endIndex];
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private sealed record Seed(int BatchId, int RevisionId, int StaffId, string RowVersion);
}
