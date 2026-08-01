using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Inventories.Procurement;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Inventories;

public sealed class PurchaseOrderBatchDocumentService : IPurchaseOrderBatchDocumentService
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly AppDbContext _context;
    private readonly IPurchaseOrderBatchPdfRenderer _renderer;
    private readonly IPurchaseOrderBatchDocumentStorage _storage;
    private readonly IScopeAuthorizationService _scopeAuthorization;

    public PurchaseOrderBatchDocumentService(
        AppDbContext context,
        IPurchaseOrderBatchPdfRenderer renderer,
        IPurchaseOrderBatchDocumentStorage storage,
        IScopeAuthorizationService scopeAuthorization)
    {
        _context = context;
        _renderer = renderer;
        _storage = storage;
        _scopeAuthorization = scopeAuthorization;
    }

    public async Task<ServiceResult<PurchaseOrderBatchDocumentRevisionDto>> GenerateAsync(int batchId, AdminActorContext actor)
    {
        if (!CanGenerate(actor))
            return Failure<PurchaseOrderBatchDocumentRevisionDto>(PurchaseOrderBatchErrorCodes.Forbidden, "Chỉ Kế toán/kho hoặc Chủ doanh nghiệp được tạo PDF cho đơn đặt hàng gộp.");

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        string? storedReference = null;
        try
        {
            if (_context.Database.IsSqlServer())
            {
                var lockResource = "PurchaseOrderBatchDocument:" + batchId;
                await _context.Database.ExecuteSqlInterpolatedAsync($@"
                    DECLARE @result int;
                    EXEC @result = sp_getapplock
                        @Resource = {lockResource},
                        @LockMode = 'Exclusive',
                        @LockOwner = 'Transaction',
                        @LockTimeout = 15000;
                    IF @result < 0 THROW 51000, 'Không thể khóa batch để tạo PDF.', 1;");
            }

            var batch = await LoadBatchAsync(batchId);
            if (batch == null)
                return Failure<PurchaseOrderBatchDocumentRevisionDto>(PurchaseOrderBatchErrorCodes.NotFound, "Không tìm thấy đơn đặt hàng gộp.");
            if (!PurchaseOrderBatchStatuses.ApprovedOrLater.Contains(batch.Status))
                return Failure<PurchaseOrderBatchDocumentRevisionDto>(PurchaseOrderBatchErrorCodes.Invalid, "Chỉ đơn đặt hàng gộp đã duyệt mới được tạo PDF chính thức.");

            var snapshot = await BuildSnapshotAsync(batch);
            var snapshotJson = JsonSerializer.Serialize(snapshot, SnapshotJsonOptions);
            var contentHash = Hash(snapshotJson);
            var existing = await _context.PurchaseOrderBatchDocumentRevisions
                .Include(x => x.GeneratedByStaff)
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.PurchaseOrderBatchId == batchId && x.ContentHash == contentHash);
            if (existing != null)
            {
                await transaction.CommitAsync();
                return ServiceResult<PurchaseOrderBatchDocumentRevisionDto>.Success(Map(existing), "Nội dung không đổi; sử dụng lại phiên bản PDF hiện có.");
            }

            var previous = await _context.PurchaseOrderBatchDocumentRevisions
                .Where(x => x.PurchaseOrderBatchId == batchId && x.Status != PurchaseOrderBatchDocumentStatuses.Superseded)
                .OrderByDescending(x => x.RevisionNumber)
                .FirstOrDefaultAsync();
            var revisionNumber = (await _context.PurchaseOrderBatchDocumentRevisions
                .Where(x => x.PurchaseOrderBatchId == batchId)
                .MaxAsync(x => (int?)x.RevisionNumber) ?? 0) + 1;
            var generatedAt = DateTime.UtcNow;
            var supplierSegment = SafeSegment(batch.Supplier.Code ?? batch.Supplier.Name ?? $"SUP-{batch.SupplierId}");
            var fileName = $"PO-{SafeSegment(batch.BatchNumber)}-{supplierSegment}-v{revisionNumber}.pdf";
            storedReference = $"purchase-order-batches/{SafeSegment(batch.BatchNumber)}/{fileName}";
            var pdf = _renderer.Render(snapshot, revisionNumber, generatedAt, contentHash);

            var revision = new PurchaseOrderBatchDocumentRevision
            {
                PurchaseOrderBatchId = batchId,
                RevisionNumber = revisionNumber,
                GeneratedAtUtc = generatedAt,
                GeneratedByStaffId = actor.StaffId,
                FileName = fileName,
                StorageReference = storedReference,
                ContentHash = contentHash,
                SnapshotJson = snapshotJson,
                Status = PurchaseOrderBatchDocumentStatuses.Generated,
                CreatedAtUtc = generatedAt
            };
            _context.PurchaseOrderBatchDocumentRevisions.Add(revision);
            if (batch.Status is PurchaseOrderBatchStatuses.Approved or PurchaseOrderBatchStatuses.SentToSupplier)
                batch.Status = PurchaseOrderBatchStatuses.PdfGenerated;
            batch.UpdatedAtUtc = generatedAt;
            await _context.SaveChangesAsync();

            if (previous != null)
            {
                previous.Status = PurchaseOrderBatchDocumentStatuses.Superseded;
                previous.SupersededAtUtc = generatedAt;
                previous.SupersededByRevisionId = revision.PurchaseOrderBatchDocumentRevisionId;
                await _context.SaveChangesAsync();
            }

            await _storage.SaveAsync(storedReference, pdf);
            await transaction.CommitAsync();
            var generatedByName = await _context.Staffs.AsNoTracking()
                .Where(x => x.StaffId == actor.StaffId)
                .Select(x => x.FullName)
                .SingleAsync();
            return ServiceResult<PurchaseOrderBatchDocumentRevisionDto>.Success(
                Map(revision, generatedByName),
                $"Đã tạo phiên bản PDF R{revisionNumber}.");
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            if (storedReference != null) await SafeDeleteAsync(storedReference);
            return Failure<PurchaseOrderBatchDocumentRevisionDto>(PurchaseOrderBatchErrorCodes.Conflict, "Có xung đột khi tạo phiên bản PDF. Hãy tải lại.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            await transaction.RollbackAsync();
            if (storedReference != null) await SafeDeleteAsync(storedReference);
            return Failure<PurchaseOrderBatchDocumentRevisionDto>(PurchaseOrderBatchErrorCodes.DocumentStorageFailure, "Không thể tạo hoặc lưu PDF của đơn đặt hàng gộp.");
        }
    }

    public async Task<ServiceResult<IReadOnlyList<PurchaseOrderBatchDocumentRevisionDto>>> ListAsync(int batchId, AdminActorContext actor)
    {
        if (!await CanReadBatchAsync(batchId, actor))
            return Failure<IReadOnlyList<PurchaseOrderBatchDocumentRevisionDto>>(PurchaseOrderBatchErrorCodes.Forbidden, "Bạn không có quyền xem PDF của đơn đặt hàng gộp này.");
        var revisions = await _context.PurchaseOrderBatchDocumentRevisions.AsNoTracking()
            .Include(x => x.GeneratedByStaff)
            .Include(x => x.SentByStaff)
            .Where(x => x.PurchaseOrderBatchId == batchId)
            .OrderByDescending(x => x.RevisionNumber)
            .ToListAsync();
        return ServiceResult<IReadOnlyList<PurchaseOrderBatchDocumentRevisionDto>>.Success(revisions.Select(x => Map(x)).ToArray());
    }

    public async Task<ServiceResult<PurchaseOrderBatchDocumentDownloadDto>> DownloadAsync(int revisionId, AdminActorContext actor)
    {
        var revision = await _context.PurchaseOrderBatchDocumentRevisions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.PurchaseOrderBatchDocumentRevisionId == revisionId);
        if (revision == null)
            return Failure<PurchaseOrderBatchDocumentDownloadDto>(PurchaseOrderBatchErrorCodes.DocumentNotFound, "Không tìm thấy phiên bản PDF.");
        if (!await CanReadBatchAsync(revision.PurchaseOrderBatchId, actor))
            return Failure<PurchaseOrderBatchDocumentDownloadDto>(PurchaseOrderBatchErrorCodes.Forbidden, "Bạn không có quyền tải PDF của đơn đặt hàng gộp này.");
        try
        {
            var content = await _storage.ReadAsync(revision.StorageReference);
            if (content == null)
                return Failure<PurchaseOrderBatchDocumentDownloadDto>(PurchaseOrderBatchErrorCodes.DocumentNotFound, "File PDF không còn trong kho tài liệu.");
            return ServiceResult<PurchaseOrderBatchDocumentDownloadDto>.Success(new()
            {
                Content = content,
                FileName = revision.FileName
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return Failure<PurchaseOrderBatchDocumentDownloadDto>(PurchaseOrderBatchErrorCodes.DocumentStorageFailure, "Không thể đọc PDF của đơn đặt hàng gộp.");
        }
    }

    public async Task<ServiceResult<PurchaseOrderBatchDocumentRevisionDto>> MarkSentAsync(
        int batchId,
        int revisionId,
        MarkPurchaseOrderBatchDocumentSentRequest request,
        AdminActorContext actor)
    {
        if (!CanGenerate(actor))
            return Failure<PurchaseOrderBatchDocumentRevisionDto>(PurchaseOrderBatchErrorCodes.Forbidden, "Chỉ Kế toán/kho hoặc Chủ doanh nghiệp được ghi nhận gửi PDF.");
        var channel = request.Channel?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!PurchaseOrderBatchDocumentChannels.All.Contains(channel))
            return Failure<PurchaseOrderBatchDocumentRevisionDto>(PurchaseOrderBatchErrorCodes.Invalid, "Kênh gửi tài liệu không hợp lệ.");
        var idempotencyKey = Clean(request.IdempotencyKey, 64);
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Failure<PurchaseOrderBatchDocumentRevisionDto>(PurchaseOrderBatchErrorCodes.Invalid, "Thiếu khóa chống gửi lặp. Hãy tải lại trang.");

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            if (_context.Database.IsSqlServer())
            {
                var lockResource = $"PurchaseOrderBatchSend:{batchId}";
                await _context.Database.ExecuteSqlInterpolatedAsync($@"
                    DECLARE @result int;
                    EXEC @result = sp_getapplock
                        @Resource = {lockResource},
                        @LockMode = 'Exclusive',
                        @LockOwner = 'Transaction',
                        @LockTimeout = 15000;
                    IF @result < 0 THROW 51000, 'Không thể khóa batch để ghi nhận gửi.', 1;");
            }

            var replay = await _context.PurchaseOrderBatchDocumentRevisions
                .AsNoTracking()
                .Include(x => x.GeneratedByStaff)
                .Include(x => x.SentByStaff)
                .SingleOrDefaultAsync(x => x.PurchaseOrderBatchId == batchId && x.SentIdempotencyKey == idempotencyKey);
            if (replay != null)
            {
                if (replay.PurchaseOrderBatchDocumentRevisionId != revisionId ||
                    !string.Equals(replay.SentChannel, channel, StringComparison.OrdinalIgnoreCase))
                    return Failure<PurchaseOrderBatchDocumentRevisionDto>(PurchaseOrderBatchErrorCodes.Conflict, "Yêu cầu gửi đã được dùng cho một phiên bản PDF hoặc kênh khác.");
                await transaction.CommitAsync();
                return ServiceResult<PurchaseOrderBatchDocumentRevisionDto>.Success(Map(replay), "Yêu cầu gửi đã được ghi nhận trước đó.");
            }

            var revision = await _context.PurchaseOrderBatchDocumentRevisions
                .Include(x => x.GeneratedByStaff)
                .Include(x => x.SentByStaff)
                .Include(x => x.PurchaseOrderBatch)
                .SingleOrDefaultAsync(x => x.PurchaseOrderBatchDocumentRevisionId == revisionId && x.PurchaseOrderBatchId == batchId);
            if (revision == null)
                return Failure<PurchaseOrderBatchDocumentRevisionDto>(PurchaseOrderBatchErrorCodes.DocumentNotFound, "Không tìm thấy phiên bản PDF thuộc đơn đặt hàng gộp này.");
            if (!PurchaseOrderBatchStatuses.ApprovedOrLater.Contains(revision.PurchaseOrderBatch.Status))
                return Failure<PurchaseOrderBatchDocumentRevisionDto>(PurchaseOrderBatchErrorCodes.Invalid, "Chỉ đơn đặt hàng gộp đã duyệt và còn hiệu lực mới được ghi nhận gửi.");
            if (revision.Status == PurchaseOrderBatchDocumentStatuses.Sent)
            {
                if (!string.Equals(revision.SentChannel, channel, StringComparison.OrdinalIgnoreCase))
                    return Failure<PurchaseOrderBatchDocumentRevisionDto>(PurchaseOrderBatchErrorCodes.Invalid, "Revision đã được ghi nhận qua một kênh gửi khác.");
                await transaction.CommitAsync();
                return ServiceResult<PurchaseOrderBatchDocumentRevisionDto>.Success(Map(revision), "Phiên bản PDF đã được ghi nhận gửi trước đó.");
            }
            if (revision.Status != PurchaseOrderBatchDocumentStatuses.Generated)
                return Failure<PurchaseOrderBatchDocumentRevisionDto>(PurchaseOrderBatchErrorCodes.Invalid, "Chỉ phiên bản PDF sẵn sàng gửi mới được ghi nhận gửi.");
            if (!VersionMatches(revision.RowVersion, request.RowVersion))
                return Failure<PurchaseOrderBatchDocumentRevisionDto>(PurchaseOrderBatchErrorCodes.StaleVersion, "Revision đã thay đổi. Hãy tải lại trước khi ghi nhận gửi.");

            var now = DateTime.UtcNow;
            revision.Status = PurchaseOrderBatchDocumentStatuses.Sent;
            revision.SentChannel = channel;
            revision.SentAtUtc = now;
            revision.SentByStaffId = actor.StaffId;
            revision.SentNote = Clean(request.Note, 500);
            revision.SentIdempotencyKey = idempotencyKey;
            if (revision.PurchaseOrderBatch.Status == PurchaseOrderBatchStatuses.PdfGenerated)
                revision.PurchaseOrderBatch.Status = PurchaseOrderBatchStatuses.SentToSupplier;
            revision.PurchaseOrderBatch.UpdatedAtUtc = now;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return ServiceResult<PurchaseOrderBatchDocumentRevisionDto>.Success(
                Map(revision),
                $"Đã ghi nhận gửi phiên bản PDF qua {ChannelLabel(channel)}.");
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            return Failure<PurchaseOrderBatchDocumentRevisionDto>(PurchaseOrderBatchErrorCodes.StaleVersion, "Revision đã thay đổi. Hãy tải lại trước khi thử lại.");
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            return Failure<PurchaseOrderBatchDocumentRevisionDto>(PurchaseOrderBatchErrorCodes.Conflict, "Yêu cầu gửi bị trùng hoặc xung đột. Hãy tải lại.");
        }
    }

    private async Task<PurchaseOrderBatch?> LoadBatchAsync(int batchId) =>
        await _context.PurchaseOrderBatches
            .Include(x => x.Supplier).ThenInclude(x => x.Contacts)
            .Include(x => x.Supplier).ThenInclude(x => x.Phones)
            .Include(x => x.CreatedByStaff)
            .Include(x => x.ApprovedByStaff)
            .Include(x => x.Lines).ThenInclude(x => x.Ingredient)
            .Include(x => x.Lines).ThenInclude(x => x.PackageUnit)
            .Include(x => x.Lines).ThenInclude(x => x.ProcurementUnit)
            .Include(x => x.ChildPurchaseOrders).ThenInclude(x => x.Store)
            .Include(x => x.ChildPurchaseOrders).ThenInclude(x => x.Lines).ThenInclude(x => x.Ingredient)
            .Include(x => x.ChildPurchaseOrders).ThenInclude(x => x.Lines).ThenInclude(x => x.PackageUnitSnapshot)
            .Include(x => x.ChildPurchaseOrders).ThenInclude(x => x.Lines).ThenInclude(x => x.ProcurementUnit)
            .Include(x => x.ChildPurchaseOrders).ThenInclude(x => x.Lines).ThenInclude(x => x.BatchAllocations)
                .ThenInclude(x => x.PurchaseAdviceLine)
            .AsSplitQuery()
            .SingleOrDefaultAsync(x => x.PurchaseOrderBatchId == batchId);

    private async Task<PurchaseOrderBatchDocumentSnapshot> BuildSnapshotAsync(PurchaseOrderBatch batch)
    {
        var storeIds = batch.ChildPurchaseOrders.Select(x => x.StoreId).Distinct().ToArray();
        var contacts = await _context.Staffs.AsNoTracking()
            .Include(x => x.Account).ThenInclude(x => x.AccountRoles).ThenInclude(x => x.Role)
            .Include(x => x.StaffPhones)
            .Where(x => storeIds.Contains(x.StoreId) && x.Active && x.Account.Active)
            .Where(x => x.Account.AccountRoles.Any(r => r.Role.Name == RoleConstants.StoreManager))
            .OrderBy(x => x.StaffId)
            .ToListAsync();
        var contactsByStore = contacts.GroupBy(x => x.StoreId).ToDictionary(x => x.Key, x => x.First());
        var supplierContact = batch.Supplier.Contacts
            .Where(x => x.Active)
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.SupplierContactId)
            .FirstOrDefault();
        var supplierPhone = batch.Supplier.Phones
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.SupplierPhoneId)
            .FirstOrDefault()?.PhoneNumber;

        var lines = batch.Lines.OrderBy(x => x.IngredientId).ThenBy(x => x.PurchaseOrderBatchLineId)
            .Select(x => new PurchaseOrderBatchDocumentLineSnapshot
            {
                PurchaseMode = x.PurchaseMode,
                IngredientId = x.IngredientId,
                IngredientName = x.Ingredient.Name,
                PackageUnitName = x.PackageUnit?.Name ?? string.Empty,
                PackageQuantity = x.PackageQuantitySnapshot,
                PackageCount = x.TotalPackageCount,
                TotalBaseQuantity = x.TotalBaseQuantity,
                TotalProcurementQuantity = x.TotalProcurementQuantity,
                DemandCoveredProcurementQuantity = x.DemandCoveredProcurementQuantity,
                RoundingSurplusProcurementQuantity = x.RoundingSurplusProcurementQuantity,
                ProcurementUnitName = x.ProcurementUnit?.Name,
                PackagePrice = x.PackagePriceSnapshot,
                UnitPricePerProcurementUnit = x.UnitPricePerProcurementUnit,
                LineTotal = x.LineTotal,
                Note = x.Note
            }).ToArray();
        var stores = batch.ChildPurchaseOrders.OrderBy(x => x.StoreId).Select(po =>
        {
            contactsByStore.TryGetValue(po.StoreId, out var contact);
            var storeLines = po.Lines.OrderBy(x => x.IngredientId).ThenBy(x => x.PurchaseOrderLineId)
                .Select(line => new PurchaseOrderBatchDocumentStoreLineSnapshot
                {
                    PurchaseMode = line.PurchaseMode,
                    IngredientId = line.IngredientId,
                    IngredientName = line.Ingredient.Name,
                    PackageUnitName = line.PackageUnitSnapshot?.Name ?? string.Empty,
                    PackageQuantity = line.PackageQuantitySnapshot,
                    PackageCount = line.PackageCount,
                    BaseQuantity = line.OrderedBaseQuantity,
                    ProcurementQuantity = line.OrderedProcurementQuantity,
                    ProcurementUnitName = line.ProcurementUnit?.Name,
                    NeededByDate = line.BatchAllocations.Select(x => x.PurchaseAdviceLine.NeededByDate).DefaultIfEmpty(po.ExpectedDeliveryAtUtc ?? batch.ExpectedDeliveryTo).Min()
                }).ToArray();
            return new PurchaseOrderBatchDocumentStoreSnapshot
            {
                StoreId = po.StoreId,
                StoreName = po.Store.Name,
                PurchaseOrderCode = po.Code,
                DeliveryAddress = po.Store.Address,
                ContactName = contact?.FullName ?? string.Empty,
                ContactPhone = contact?.StaffPhones.OrderByDescending(x => x.IsDefault).FirstOrDefault()?.Phone ?? po.Store.Phone,
                NeededByDate = storeLines.Select(x => (DateTime?)x.NeededByDate).DefaultIfEmpty(po.ExpectedDeliveryAtUtc).Min(),
                Note = po.Note,
                Lines = storeLines
            };
        }).ToArray();

        return new PurchaseOrderBatchDocumentSnapshot
        {
            PurchaseOrderBatchId = batch.PurchaseOrderBatchId,
            BatchNumber = batch.BatchNumber,
            Currency = batch.Currency,
            CreatedAtUtc = batch.CreatedAtUtc,
            CreatedByName = batch.CreatedByStaff.FullName,
            ApprovedAtUtc = batch.ApprovedAtUtc,
            ApprovedByName = batch.ApprovedByStaff?.FullName ?? string.Empty,
            ExpectedDeliveryFrom = batch.ExpectedDeliveryFrom,
            ExpectedDeliveryTo = batch.ExpectedDeliveryTo,
            Note = batch.Note,
            Supplier = new()
            {
                Code = batch.Supplier.Code ?? string.Empty,
                Name = batch.Supplier.Name ?? string.Empty,
                TaxCode = batch.Supplier.TaxCode ?? string.Empty,
                Address = batch.Supplier.Address ?? string.Empty,
                ContactName = supplierContact?.Name ?? string.Empty,
                ContactEmail = supplierContact?.Email ?? string.Empty,
                ContactPhone = supplierContact?.PhoneNumber ?? supplierPhone ?? string.Empty
            },
            Lines = lines,
            Stores = stores,
            TotalAmount = lines.Sum(x => x.LineTotal)
        };
    }

    private async Task<bool> CanReadBatchAsync(int batchId, AdminActorContext actor)
    {
        var stores = await _context.PurchaseOrders.AsNoTracking()
            .Where(x => x.PurchaseOrderBatchId == batchId)
            .Select(x => x.StoreId).Distinct().ToListAsync();
        if (stores.Count == 0) return false;
        if (!actor.RoleNames.Any(role => role is RoleConstants.AccountantWarehouse
                or RoleConstants.BusinessOwner
                or RoleConstants.SystemAdmin
                or RoleConstants.StoreManager
                or RoleConstants.AreaManager))
            return false;
        foreach (var storeId in stores)
            if (!await _scopeAuthorization.CanAccessStoreAsync(actor.StaffId, storeId)) return false;
        return true;
    }

    private async Task SafeDeleteAsync(string storageReference)
    {
        try { await _storage.DeleteAsync(storageReference); }
        catch { /* Best-effort cleanup after a failed transaction. */ }
    }

    private static bool CanGenerate(AdminActorContext actor) =>
        HasRole(actor, RoleConstants.AccountantWarehouse)
        || HasRole(actor, RoleConstants.BusinessOwner)
        || HasRole(actor, RoleConstants.SystemAdmin);
    private static string ChannelLabel(string channel) => channel switch
    {
        PurchaseOrderBatchDocumentChannels.ZaloManual => "Zalo",
        PurchaseOrderBatchDocumentChannels.EmailManual => "Email",
        PurchaseOrderBatchDocumentChannels.OtherManual => "kênh khác",
        _ => "kênh đã chọn"
    };
    private static bool HasRole(AdminActorContext actor, string role) => actor.RoleNames.Contains(role, StringComparer.OrdinalIgnoreCase);
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static string SafeSegment(string value)
    {
        var safe = new string(value.Where(x => char.IsLetterOrDigit(x) || x is '-' or '_').ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "batch" : safe;
    }
    private static PurchaseOrderBatchDocumentRevisionDto Map(PurchaseOrderBatchDocumentRevision revision, string? generatedByName = null) => new()
    {
        RevisionId = revision.PurchaseOrderBatchDocumentRevisionId,
        PurchaseOrderBatchId = revision.PurchaseOrderBatchId,
        RevisionNumber = revision.RevisionNumber,
        FileName = revision.FileName,
        ContentHash = revision.ContentHash,
        Status = revision.Status,
        GeneratedAtUtc = revision.GeneratedAtUtc,
        GeneratedByStaffId = revision.GeneratedByStaffId,
        GeneratedByName = generatedByName ?? revision.GeneratedByStaff?.FullName ?? string.Empty,
        SentChannel = revision.SentChannel,
        SentAtUtc = revision.SentAtUtc,
        SentByStaffId = revision.SentByStaffId,
        SentByName = revision.SentByStaff?.FullName,
        SentNote = revision.SentNote,
        SupersededAtUtc = revision.SupersededAtUtc,
        SupersededByRevisionId = revision.SupersededByRevisionId,
        RowVersion = Convert.ToBase64String(revision.RowVersion)
    };
    private static bool VersionMatches(byte[] current, string? provided)
    {
        if (current.Length == 0 && string.IsNullOrWhiteSpace(provided)) return true;
        if (string.IsNullOrWhiteSpace(provided)) return false;
        try { return current.SequenceEqual(Convert.FromBase64String(provided)); }
        catch (FormatException) { return false; }
    }
    private static string? Clean(string? value, int maxLength)
    {
        var clean = value?.Trim();
        if (string.IsNullOrWhiteSpace(clean)) return null;
        return clean.Length <= maxLength ? clean : clean[..maxLength];
    }
    private static ServiceResult<T> Failure<T>(string code, string message) => ServiceResult<T>.Failure(message, errorCode: code);
}
