using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.RestockRequests;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Costing;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Inventories.Transactions;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CafeChain.Application.Services.Inventories
{
    /// <summary>
    /// Issue #128 — BranchReceipt draft + atomic confirm/post.
    /// Only CONFIRMED posts inventory / cost layers / ledger. RestockRequest is intent-only.
    /// </summary>
    public sealed class BranchReceiptService : IBranchReceiptService
    {
        private readonly AppDbContext _context;
        private readonly IUnitConversionService _unitConversion;
        private readonly IPhysicalUnitConversionService _physicalConversion;
        private readonly IInventoryWriterModeService _writerModeService;
        private readonly IStoreInventoryWriteResolver _writeResolver;
        private readonly IRestockFulfillmentPostingService _fulfillmentPostingService;
        private readonly IStockAlertService _stockAlertService;
        private readonly IScopeAuthorizationService _scopeAuthorization;
        private readonly ILogger<BranchReceiptService> _logger;

        public BranchReceiptService(
            AppDbContext context,
            IUnitConversionService unitConversion,
            IPhysicalUnitConversionService physicalConversion,
            IInventoryWriterModeService writerModeService,
            IStoreInventoryWriteResolver writeResolver,
            IRestockFulfillmentPostingService fulfillmentPostingService,
            IStockAlertService stockAlertService,
            IScopeAuthorizationService scopeAuthorization,
            ILogger<BranchReceiptService> logger)
        {
            _context = context;
            _unitConversion = unitConversion;
            _physicalConversion = physicalConversion;
            _writerModeService = writerModeService;
            _writeResolver = writeResolver;
            _fulfillmentPostingService = fulfillmentPostingService;
            _stockAlertService = stockAlertService;
            _scopeAuthorization = scopeAuthorization;
            _logger = logger;
        }

        public async Task<ServiceResult<BranchReceiptDetailDto>> CreateDraftAsync(
            CreateBranchReceiptRequest request,
            int actorStaffId,
            IReadOnlyCollection<string> roleNames)
        {
            if (request == null)
                return FailDetail("Thiếu dữ liệu phiếu nhận.");

            if (!CanCreateOrConfirmReceipt(roleNames))
                return FailDetail("Bạn không có quyền tạo phiếu nhận hàng.", BranchReceiptErrorCodes.Unauthorized);

            if (request.StoreId <= 0)
                return FailDetail("StoreId không hợp lệ.", BranchReceiptErrorCodes.StoreMismatch);

            var createAuth = await AuthorizeReceiptAccessAsync(
                request.StoreId,
                actorStaffId,
                actorStoreId: null,
                roleNames,
                mutation: true);
            if (!createAuth.IsSuccess)
                return FailDetail(createAuth.Message, createAuth.ErrorCode);

            if (request.SupplierId.HasValue
                && !await IsSupplierAssignedToStoreAsync(request.SupplierId.Value, request.StoreId))
            {
                return FailDetail(
                    "Nhà cung cấp không hoạt động hoặc chưa được gán cho cửa hàng này.",
                    BranchReceiptErrorCodes.SupplierNotAssigned);
            }

            var receiptKey = (request.ReceiptKey ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(receiptKey) || receiptKey.Length > 100)
                return FailDetail("ReceiptKey bắt buộc (tối đa 100 ký tự).", BranchReceiptErrorCodes.ReceiptKeyRequired);

            if (request.Lines == null || request.Lines.Count == 0)
                return FailDetail("Phiếu nhận cần ít nhất một dòng.", BranchReceiptErrorCodes.QuantityInvalid);

            var now = DateTime.UtcNow;
            var receivedAt = request.ReceivedAt?.ToUniversalTime() ?? now;

            // Pre-validate lines without inventory mutation.
            var preparedLines = new List<(CreateBranchReceiptLineInput Input, RestockRequest Request, decimal BaseQty, int BaseUnitId, decimal UnitCost, decimal LineTotal)>();
            foreach (var line in request.Lines)
            {
                var built = await BuildLineSnapshotAsync(line, request.StoreId, request.SupplierId);
                if (!built.IsSuccess)
                    return FailDetail(built.Message, built.ErrorCode);
                preparedLines.Add(built.Data!);
            }

            // Over-receipt check against remaining (drafts do not count; only confirmed).
            var byRequest = preparedLines.GroupBy(x => x.Request.RestockRequestId);
            foreach (var g in byRequest)
            {
                var req = g.First().Request;
                if (!IsReceivableStatus(req.Status))
                {
                    return FailDetail(
                        $"Yêu cầu #{req.RestockRequestId} không nhận hàng được (status={req.Status}).",
                        BranchReceiptErrorCodes.RequestStateInvalid);
                }

                if (req.StoreId != request.StoreId)
                {
                    return FailDetail(
                        $"Yêu cầu #{req.RestockRequestId} không thuộc cửa hàng phiếu nhận.",
                        BranchReceiptErrorCodes.StoreMismatch);
                }

                var confirmed = await SumFulfillmentPostingsAsync(req.RestockRequestId);
                var newSum = g.Sum(x => x.BaseQty);
                if (confirmed + newSum > req.RequestedQuantity)
                {
                    return FailDetail(
                        $"Vượt số lượng yêu cầu #{req.RestockRequestId}: đã nhận {confirmed:N3}, thêm {newSum:N3}, yêu cầu {req.RequestedQuantity:N3}.",
                        BranchReceiptErrorCodes.RestockOverReceiptNotAllowed);
                }
            }

            var existingKey = await _context.BranchReceipts
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.StoreId == request.StoreId && r.ReceiptKey == receiptKey);
            if (existingKey != null)
            {
                return FailDetail(
                    "ReceiptKey đã tồn tại cho cửa hàng này.",
                    BranchReceiptErrorCodes.DuplicateReceiptKey);
            }

            var receipt = new BranchReceipt
            {
                ReceiptCode = $"BR-{request.StoreId}-{now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
                StoreId = request.StoreId,
                SupplierId = request.SupplierId,
                Status = BranchReceiptStatuses.Draft,
                ReceiptKey = receiptKey,
                ReferenceNumber = string.IsNullOrWhiteSpace(request.ReferenceNumber) ? null : request.ReferenceNumber.Trim(),
                ReceivedAt = receivedAt,
                ReceivedByStaffId = actorStaffId,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                CreatedAt = now,
                CreatedByStaffId = actorStaffId
            };

            foreach (var p in preparedLines)
            {
                var line = new BranchReceiptLine
                {
                    RestockRequestId = p.Request.RestockRequestId,
                    RestockRequestFulfillmentId = p.Input.RestockRequestFulfillmentId,
                    IngredientId = p.Request.IngredientId,
                    // New lines: PreparedItem identity only when request is PI-based (no Recipe-only).
                    PreparedItemId = p.Request.IngredientId.HasValue ? null : p.Request.PreparedItemId,
                    RecipeId = p.Request.IngredientId.HasValue
                        ? null
                        : (p.Request.PreparedItemId.HasValue ? null : p.Request.RecipeId), // avoid Recipe-only if PI present
                    InputQuantity = p.Input.InputQuantity,
                    InputUnitId = p.Input.InputUnitId,
                    ReceivedBaseQuantity = p.BaseQty,
                    BaseUnitId = p.BaseUnitId,
                    SupplierId = p.Input.SupplierId ?? request.SupplierId,
                    IngredientSupplierId = p.Input.IngredientSupplierId,
                    ActualPackagePrice = p.Input.ActualPackagePrice,
                    PackageQuantitySnapshot = p.Input.PackageQuantity,
                    PackageUnitIdSnapshot = p.Input.PackageUnitId,
                    BaseUnitCostSnapshot = p.UnitCost,
                    LineTotalCost = p.LineTotal,
                    CreatedAt = now
                };

                // If request has both PreparedItem + Recipe (legacy), keep Recipe as compatibility metadata only.
                if (p.Request.PreparedItemId.HasValue && !p.Request.IngredientId.HasValue)
                {
                    line.PreparedItemId = p.Request.PreparedItemId;
                    line.RecipeId = p.Request.RecipeId; // may be null or set for legacy
                    line.IngredientId = null;
                }

                if (line.IngredientId == null && line.PreparedItemId == null)
                {
                    return FailDetail(
                        $"Yêu cầu #{p.Request.RestockRequestId} không có identity Ingredient/PreparedItem hợp lệ để nhận (Recipe-only không tạo receipt line mới).",
                        BranchReceiptErrorCodes.IdentityMismatch);
                }

                receipt.Lines.Add(line);
            }

            try
            {
                _context.BranchReceipts.Add(receipt);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                return FailDetail(
                    "ReceiptKey đã tồn tại (xung đột đồng thời).",
                    BranchReceiptErrorCodes.DuplicateReceiptKey);
            }

            _logger.LogInformation(
                "[BranchReceipt] Draft created Id={Id} Store={Store} Key={Key} Lines={Lines} (no inventory mutation)",
                receipt.BranchReceiptId, receipt.StoreId, receipt.ReceiptKey, receipt.Lines.Count);

            return ServiceResult<BranchReceiptDetailDto>.Success(
                await MapDetailAsync(receipt.BranchReceiptId),
                "Đã tạo phiếu nhận nháp (chưa nhập kho).");
        }

        public async Task<ServiceResult<BranchReceiptDetailDto>> GetDetailAsync(
            int branchReceiptId,
            int actorStaffId,
            int? actorStoreId,
            IReadOnlyCollection<string> roleNames)
        {
            var receipt = await _context.BranchReceipts
                .AsNoTracking()
                .Include(r => r.Supplier)
                .Include(r => r.Lines).ThenInclude(l => l.InputUnit)
                .Include(r => r.Lines).ThenInclude(l => l.BaseUnit)
                .FirstOrDefaultAsync(r => r.BranchReceiptId == branchReceiptId);

            if (receipt == null)
                return FailDetail("Không tìm thấy phiếu nhận.", BranchReceiptErrorCodes.ReceiptNotFound);

            var auth = await AuthorizeReceiptAccessAsync(
                receipt.StoreId, actorStaffId, actorStoreId, roleNames, mutation: false);
            if (!auth.IsSuccess)
                return FailDetail(auth.Message, auth.ErrorCode);

            return ServiceResult<BranchReceiptDetailDto>.Success(MapDetail(receipt));
        }

        public async Task<ServiceResult<List<BranchReceiptListItemDto>>> ListForStoreAsync(
            int storeId,
            int actorStaffId,
            int? actorStoreId,
            IReadOnlyCollection<string> roleNames,
            string? statusFilter = null)
        {
            var auth = await AuthorizeReceiptAccessAsync(
                storeId, actorStaffId, actorStoreId, roleNames, mutation: false);
            if (!auth.IsSuccess)
                return ServiceResult<List<BranchReceiptListItemDto>>.Failure(auth.Message, errorCode: auth.ErrorCode);

            var q = _context.BranchReceipts
                .AsNoTracking()
                .Include(r => r.Supplier)
                .Include(r => r.Lines)
                .Where(r => r.StoreId == storeId);

            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                var st = statusFilter.Trim().ToUpperInvariant();
                q = q.Where(r => r.Status == st);
            }

            var rows = await q
                .OrderByDescending(r => r.CreatedAt)
                .Take(100)
                .ToListAsync();

            return ServiceResult<List<BranchReceiptListItemDto>>.Success(
                rows.Select(MapListItem).ToList());
        }

        public async Task<ServiceResult<ConfirmBranchReceiptResultDto>> ConfirmAsync(
            int branchReceiptId,
            int actorStaffId,
            int? actorStoreId,
            IReadOnlyCollection<string> roleNames)
        {
            if (!CanCreateOrConfirmReceipt(roleNames))
            {
                return ServiceResult<ConfirmBranchReceiptResultDto>.Failure(
                    "Bạn không có quyền xác nhận phiếu nhận.",
                    errorCode: BranchReceiptErrorCodes.Unauthorized);
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var receipt = await LoadReceiptForUpdateAsync(branchReceiptId);
                if (receipt == null)
                {
                    await transaction.RollbackAsync();
                    return ServiceResult<ConfirmBranchReceiptResultDto>.Failure(
                        "Không tìm thấy phiếu nhận.",
                        errorCode: BranchReceiptErrorCodes.ReceiptNotFound);
                }

                var auth = await AuthorizeReceiptAccessAsync(
                    receipt.StoreId, actorStaffId, actorStoreId, roleNames, mutation: true);
                if (!auth.IsSuccess)
                {
                    await transaction.RollbackAsync();
                    return ServiceResult<ConfirmBranchReceiptResultDto>.Failure(auth.Message, errorCode: auth.ErrorCode);
                }

                // Idempotent replay: already CONFIRMED → return success without second post.
                if (receipt.Status == BranchReceiptStatuses.Confirmed)
                {
                    await transaction.CommitAsync();
                    var replayTxIds = receipt.Lines
                        .Where(l => l.InventoryTransactionId.HasValue)
                        .Select(l => l.InventoryTransactionId!.Value)
                        .ToList();
                    return ServiceResult<ConfirmBranchReceiptResultDto>.Success(new ConfirmBranchReceiptResultDto
                    {
                        BranchReceiptId = receipt.BranchReceiptId,
                        ReceiptCode = receipt.ReceiptCode,
                        Status = receipt.Status,
                        WasReplay = true,
                        InventoryTransactionIds = replayTxIds
                    }, "Phiếu nhận đã xác nhận trước đó (replay).");
                }

                if (receipt.Status != BranchReceiptStatuses.Draft)
                {
                    await transaction.RollbackAsync();
                    return ServiceResult<ConfirmBranchReceiptResultDto>.Failure(
                        $"Chỉ xác nhận phiếu DRAFT. Hiện tại: {receipt.Status}.",
                        errorCode: BranchReceiptErrorCodes.ReceiptNotDraft);
                }

                if (receipt.Lines == null || receipt.Lines.Count == 0)
                {
                    await transaction.RollbackAsync();
                    return ServiceResult<ConfirmBranchReceiptResultDto>.Failure(
                        "Phiếu nhận không có dòng.",
                        errorCode: BranchReceiptErrorCodes.QuantityInvalid);
                }

                // Lock restock requests in ascending ID order.
                var requestIds = receipt.Lines
                    .Select(l => l.RestockRequestId)
                    .Distinct()
                    .OrderBy(id => id)
                    .ToList();

                var requests = new Dictionary<int, RestockRequest>();
                foreach (var rid in requestIds)
                {
                    var req = await LoadRestockRequestForUpdateAsync(rid);
                    if (req == null)
                    {
                        await transaction.RollbackAsync();
                        return ServiceResult<ConfirmBranchReceiptResultDto>.Failure(
                            $"Không tìm thấy yêu cầu #{rid}.",
                            errorCode: BranchReceiptErrorCodes.RequestNotFound);
                    }

                    requests[rid] = req;
                }

                // Validate each line
                foreach (var line in receipt.Lines)
                {
                    var req = requests[line.RestockRequestId];
                    if (!IsReceivableStatus(req.Status))
                    {
                        await transaction.RollbackAsync();
                        return ServiceResult<ConfirmBranchReceiptResultDto>.Failure(
                            $"Yêu cầu #{req.RestockRequestId} không nhận hàng được (status={req.Status}).",
                            errorCode: BranchReceiptErrorCodes.RequestStateInvalid);
                    }

                    if (req.StoreId != receipt.StoreId)
                    {
                        await transaction.RollbackAsync();
                        return ServiceResult<ConfirmBranchReceiptResultDto>.Failure(
                            "Store không khớp giữa phiếu nhận và yêu cầu.",
                            errorCode: BranchReceiptErrorCodes.StoreMismatch);
                    }

                    if (!IdentityMatches(req, line))
                    {
                        await transaction.RollbackAsync();
                        return ServiceResult<ConfirmBranchReceiptResultDto>.Failure(
                            $"Identity dòng nhận không khớp yêu cầu #{req.RestockRequestId}.",
                            errorCode: BranchReceiptErrorCodes.IdentityMismatch);
                    }

                    if (line.ReceivedBaseQuantity <= 0)
                    {
                        await transaction.RollbackAsync();
                        return ServiceResult<ConfirmBranchReceiptResultDto>.Failure(
                            "Số lượng base phải > 0.",
                            errorCode: BranchReceiptErrorCodes.QuantityInvalid);
                    }

                    if (line.BaseUnitCostSnapshot <= 0 || line.LineTotalCost <= 0)
                    {
                        await transaction.RollbackAsync();
                        return ServiceResult<ConfirmBranchReceiptResultDto>.Failure(
                            "Chi phí snapshot không đầy đủ (không cho phép cost 0).",
                            errorCode: BranchReceiptErrorCodes.ReceiptCostIncomplete);
                    }

                    if (line.InventoryTransactionId.HasValue)
                    {
                        await transaction.RollbackAsync();
                        return ServiceResult<ConfirmBranchReceiptResultDto>.Failure(
                            "Dòng đã post — không post lại.",
                            errorCode: BranchReceiptErrorCodes.IdempotencyKeyReused);
                    }
                }

                // RestockFulfillmentPosting is the only authority for actual fulfilled quantity.
                // Each source line is idempotent and overfill is checked while the request is locked.
                var requestUpdates = new Dictionary<int, RestockFulfillmentPostingResult>();
                foreach (var line in receipt.Lines.OrderBy(l => l.BranchReceiptLineId))
                {
                    var posting = await _fulfillmentPostingService.RegisterAsync(new RegisterRestockFulfillmentPostingCommand
                    {
                        RestockRequestId = line.RestockRequestId,
                        DestinationStoreId = receipt.StoreId,
                        SourceDocumentType = RestockFulfillmentDocumentTypes.BranchReceipt,
                        SourceDocumentId = receipt.BranchReceiptId,
                        SourceDocumentLineId = line.BranchReceiptLineId,
                        IngredientId = line.IngredientId,
                        PreparedItemId = line.PreparedItemId,
                        Quantity = line.ReceivedBaseQuantity,
                        BaseUnitId = line.BaseUnitId,
                        ActorStaffId = actorStaffId,
                        Reason = $"BranchReceipt #{receipt.BranchReceiptId} CONFIRMED"
                    });
                    if (!posting.IsSuccess || posting.Data == null)
                    {
                        await transaction.RollbackAsync();
                        return ServiceResult<ConfirmBranchReceiptResultDto>.Failure(
                            posting.Message,
                            errorCode: posting.Message.Contains("vượt mục tiêu", StringComparison.OrdinalIgnoreCase)
                                ? BranchReceiptErrorCodes.RestockOverReceiptNotAllowed
                                : BranchReceiptErrorCodes.RequestStateInvalid);
                    }

                    requestUpdates[line.RestockRequestId] = posting.Data;
                }

                // Resolve / lock inventory rows ASC
                var inventoryByLine = new Dictionary<int, StoreInventory>();
                var writerSnapshots = new Dictionary<int, Application.DTOs.Inventories.InventoryWriterModeSnapshot>();
                var actorAccountId = await _context.Staffs.AsNoTracking()
                    .Where(s => s.StaffId == actorStaffId)
                    .Select(s => (int?)s.AccountId)
                    .FirstOrDefaultAsync();

                foreach (var line in receipt.Lines.OrderBy(l => l.BranchReceiptLineId))
                {
                    StoreInventory inv;
                    if (line.IngredientId.HasValue)
                    {
                        inv = await GetOrCreateIngredientInventoryAsync(receipt.StoreId, line.IngredientId.Value);
                    }
                    else if (line.PreparedItemId.HasValue)
                    {
                        if (!writerSnapshots.TryGetValue(receipt.StoreId, out var snap))
                        {
                            var snapResult = await _writerModeService.AcquireSnapshotAsync(receipt.StoreId);
                            if (!snapResult.IsSuccess || snapResult.Data == null)
                            {
                                await transaction.RollbackAsync();
                                return ServiceResult<ConfirmBranchReceiptResultDto>.Failure(
                                    snapResult.Message ?? "Không lấy được writer mode barrier.",
                                    errorCode: BranchReceiptErrorCodes.ConfirmFailed);
                            }

                            snap = snapResult.Data;
                            writerSnapshots[receipt.StoreId] = snap;
                        }

                        var resolve = await _writeResolver.ResolveAsync(new StoreInventoryWriteRequest
                        {
                            ModeSnapshot = snap,
                            StoreId = receipt.StoreId,
                            IdentityType = InventoryWriteIdentityTypes.PreparedItem,
                            PreparedItemId = line.PreparedItemId.Value,
                            NormalizedBaseUnitId = line.BaseUnitId,
                            AllowCreateIntent = true
                        });

                        if (resolve.Status == InventoryWriteResolutionStatuses.FoundCanonical
                            && resolve.StoreInventory != null)
                        {
                            inv = resolve.StoreInventory;
                        }
                        else if (resolve.Status == InventoryWriteResolutionStatuses.CreateAllowed)
                        {
                            if (!actorAccountId.HasValue)
                            {
                                await transaction.RollbackAsync();
                                return ServiceResult<ConfirmBranchReceiptResultDto>.Failure(
                                    "Thiếu AccountId actor để tạo canonical PreparedItem.",
                                    errorCode: BranchReceiptErrorCodes.ConfirmFailed);
                            }

                            inv = new StoreInventory
                            {
                                StoreId = receipt.StoreId,
                                PreparedItemId = line.PreparedItemId.Value,
                                RecipeId = null,
                                IngredientId = null,
                                BtpIdentityState = BtpIdentityState.Canonical,
                                QuantitySemanticsStatus = InventoryQuantitySemanticsStatus.BaseUnitConfirmed,
                                QuantitySemanticsEvidenceType = QuantitySemanticsEvidenceType.SystemCanonicalCreation,
                                QuantitySemanticsEvidenceReference = $"BRANCH_RECEIPT:{receipt.BranchReceiptId}",
                                QuantitySemanticsReviewedAt = DateTime.UtcNow,
                                QuantitySemanticsReviewedByAccountId = actorAccountId.Value,
                                AvailableQty = 0,
                                ReservedQty = 0,
                                LastUpdated = DateTime.UtcNow
                            };
                            _context.StoreInventories.Add(inv);
                            await _context.SaveChangesAsync();
                        }
                        else
                        {
                            await transaction.RollbackAsync();
                            return ServiceResult<ConfirmBranchReceiptResultDto>.Failure(
                                resolve.Message ?? "Không resolve được tồn PreparedItem.",
                                errorCode: BranchReceiptErrorCodes.IdentityMismatch);
                        }
                    }
                    else
                    {
                        await transaction.RollbackAsync();
                        return ServiceResult<ConfirmBranchReceiptResultDto>.Failure(
                            "Dòng nhận thiếu Ingredient/PreparedItem identity.",
                            errorCode: BranchReceiptErrorCodes.IdentityMismatch);
                    }

                    inventoryByLine[line.BranchReceiptLineId] = inv;
                }

                // Lock inventory by ascending StoreInventoryId
                var invIds = inventoryByLine.Values
                    .Select(i => i.StoreInventoryId)
                    .Distinct()
                    .OrderBy(id => id)
                    .ToList();

                var lockedInventories = await LockStoreInventoriesAsync(invIds);
                // Rebind after lock
                foreach (var key in inventoryByLine.Keys.ToList())
                {
                    var id = inventoryByLine[key].StoreInventoryId;
                    if (lockedInventories.TryGetValue(id, out var locked))
                        inventoryByLine[key] = locked;
                }

                var now = DateTime.UtcNow;
                var createdTxIds = new List<int>();
                foreach (var line in receipt.Lines.OrderBy(l => l.BranchReceiptLineId))
                {
                    var inv = inventoryByLine[line.BranchReceiptLineId];
                    var before = inv.AvailableQty;
                    inv.AvailableQty += line.ReceivedBaseQuantity;
                    inv.LastUpdated = now;

                    _context.InventoryCostLayers.Add(new InventoryCostLayer
                    {
                        StoreId = receipt.StoreId,
                        IngredientId = line.IngredientId,
                        PreparedItemId = line.PreparedItemId,
                        Quantity = line.ReceivedBaseQuantity,
                        RemainingQuantity = line.ReceivedBaseQuantity,
                        UnitCost = line.BaseUnitCostSnapshot,
                        CreatedAt = now
                    });

                    var tx = new InventoryTransaction
                    {
                        StoreInventoryId = inv.StoreInventoryId,
                        Type = InventoryTransactionTypeEnum.BRANCH_RECEIPT_IN,
                        StockStatus = InventoryStockStatus.NORMAL,
                        Quantity = line.ReceivedBaseQuantity,
                        BeforeQty = before,
                        AfterQty = inv.AvailableQty,
                        UnitCost = line.BaseUnitCostSnapshot,
                        TotalCost = line.LineTotalCost,
                        BranchReceiptLineId = line.BranchReceiptLineId,
                        CreatedAt = now
                    };
                    _context.InventoryTransactions.Add(tx);
                    await _context.SaveChangesAsync(); // need InventoryTransactionId

                    line.InventoryTransactionId = tx.InventoryTransactionId;
                    createdTxIds.Add(tx.InventoryTransactionId);
                }

                receipt.Status = BranchReceiptStatuses.Confirmed;
                receipt.ConfirmedAt = now;
                receipt.ConfirmedByStaffId = actorStaffId;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Post-commit alert evaluation — failures must NOT rollback receipt.
                var alertFailed = false;
                string? alertMsg = null;
                try
                {
                    foreach (var line in receipt.Lines)
                    {
                        if (!inventoryByLine.TryGetValue(line.BranchReceiptLineId, out var inv))
                            continue;

                        var eval = await _stockAlertService.EvaluateStoreInventoryItemAsync(
                            inv.StoreInventoryId,
                            "BRANCH_RECEIPT_CONFIRM");
                        if (!eval.IsSuccess)
                        {
                            alertFailed = true;
                            alertMsg = eval.Message ?? "Đánh giá cảnh báo thất bại.";
                            _logger.LogWarning(
                                "[BranchReceipt] Alert evaluation failed after confirm ReceiptId={Id} InvId={Inv}: {Msg}",
                                receipt.BranchReceiptId, inv.StoreInventoryId, alertMsg);
                        }
                    }
                }
                catch (Exception ex)
                {
                    alertFailed = true;
                    alertMsg = "Đánh giá cảnh báo thất bại.";
                    _logger.LogWarning(
                        ex,
                        "[BranchReceipt] Alert evaluation exception after confirm ReceiptId={Id}",
                        receipt.BranchReceiptId);
                }

                _logger.LogInformation(
                    "[BranchReceipt] CONFIRMED Id={Id} Store={Store} TxCount={Count} AlertFailed={Alert}",
                    receipt.BranchReceiptId, receipt.StoreId, createdTxIds.Count, alertFailed);

                var message = alertFailed
                    ? "Đã nhập kho nhưng cập nhật cảnh báo thất bại."
                    : "Đã xác nhận phiếu nhận và cập nhật tồn kho.";

                return ServiceResult<ConfirmBranchReceiptResultDto>.Success(new ConfirmBranchReceiptResultDto
                {
                    BranchReceiptId = receipt.BranchReceiptId,
                    ReceiptCode = receipt.ReceiptCode,
                    Status = BranchReceiptStatuses.Confirmed,
                    WasReplay = false,
                    AlertEvaluationFailed = alertFailed,
                    AlertEvaluationMessage = alertMsg,
                    InventoryTransactionIds = createdTxIds,
                    RequestUpdates = requestUpdates
                        .Select(x => (x.Key, x.Value.RequestStatus, x.Value.FulfilledQuantity))
                        .ToList()
                }, message);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                try { await transaction.RollbackAsync(); } catch { /* ignore */ }
                _context.ChangeTracker.Clear();

                // Concurrent confirm may have won — treat as replay if now confirmed.
                var existing = await _context.BranchReceipts
                    .AsNoTracking()
                    .Include(r => r.Lines)
                    .FirstOrDefaultAsync(r => r.BranchReceiptId == branchReceiptId);
                if (existing?.Status == BranchReceiptStatuses.Confirmed)
                {
                    return ServiceResult<ConfirmBranchReceiptResultDto>.Success(new ConfirmBranchReceiptResultDto
                    {
                        BranchReceiptId = existing.BranchReceiptId,
                        ReceiptCode = existing.ReceiptCode,
                        Status = existing.Status,
                        WasReplay = true,
                        InventoryTransactionIds = existing.Lines
                            .Where(l => l.InventoryTransactionId.HasValue)
                            .Select(l => l.InventoryTransactionId!.Value)
                            .ToList()
                    }, "Phiếu nhận đã xác nhận (concurrent replay).");
                }

                _logger.LogWarning(ex, "[BranchReceipt] Unique violation on confirm Id={Id}", branchReceiptId);
                return ServiceResult<ConfirmBranchReceiptResultDto>.Failure(
                    "Xung đột đồng thời khi xác nhận phiếu nhận.",
                    errorCode: BranchReceiptErrorCodes.ConcurrencyConflict);
            }
            catch (Exception ex)
            {
                try { await transaction.RollbackAsync(); } catch { /* ignore */ }
                _context.ChangeTracker.Clear();
                _logger.LogError(ex, "[BranchReceipt] Confirm failed Id={Id}", branchReceiptId);
                return ServiceResult<ConfirmBranchReceiptResultDto>.Failure(
                    "Không xác nhận được phiếu nhận. Vui lòng thử lại.",
                    errorCode: BranchReceiptErrorCodes.ConfirmFailed);
            }
        }

        public async Task<ServiceResult<List<BranchReceiptSupplierOptionDto>>> GetSupplierOptionsAsync(
            int storeId,
            int actorStaffId,
            int? actorStoreId,
            IReadOnlyCollection<string> roleNames)
        {
            var auth = await AuthorizeReceiptAccessAsync(
                storeId, actorStaffId, actorStoreId, roleNames, mutation: true);
            if (!auth.IsSuccess)
            {
                return ServiceResult<List<BranchReceiptSupplierOptionDto>>.Failure(
                    auth.Message,
                    errorCode: auth.ErrorCode);
            }

            var rows = await _context.SupplierStores
                .AsNoTracking()
                .Where(x => x.StoreId == storeId && x.Active && x.Supplier.Active)
                .OrderBy(x => x.Supplier.Name)
                .Select(x => new BranchReceiptSupplierOptionDto
                {
                    SupplierId = x.SupplierId,
                    SupplierCode = x.Supplier.Code ?? string.Empty,
                    SupplierName = x.Supplier.Name ?? string.Empty,
                    LeadTimeOverrideDays = x.LeadTimeOverrideDays,
                    DeliverySchedule = x.DeliverySchedule
                })
                .ToListAsync();

            return ServiceResult<List<BranchReceiptSupplierOptionDto>>.Success(rows);
        }

        public async Task<ServiceResult<List<BranchReceiptOfferOptionDto>>> GetOfferOptionsAsync(
            int storeId,
            int supplierId,
            int? restockRequestId,
            int actorStaffId,
            int? actorStoreId,
            IReadOnlyCollection<string> roleNames)
        {
            var auth = await AuthorizeReceiptAccessAsync(
                storeId, actorStaffId, actorStoreId, roleNames, mutation: true);
            if (!auth.IsSuccess)
            {
                return ServiceResult<List<BranchReceiptOfferOptionDto>>.Failure(
                    auth.Message,
                    errorCode: auth.ErrorCode);
            }

            if (!await IsSupplierAssignedToStoreAsync(supplierId, storeId))
            {
                return ServiceResult<List<BranchReceiptOfferOptionDto>>.Failure(
                    "Nhà cung cấp không hoạt động hoặc chưa được gán cho cửa hàng này.",
                    errorCode: BranchReceiptErrorCodes.SupplierNotAssigned);
            }

            int? ingredientId = null;
            if (restockRequestId.HasValue)
            {
                var request = await _context.RestockRequests
                    .AsNoTracking()
                    .Where(x => x.RestockRequestId == restockRequestId.Value && x.StoreId == storeId)
                    .Select(x => new { x.IngredientId })
                    .FirstOrDefaultAsync();
                if (request == null)
                {
                    return ServiceResult<List<BranchReceiptOfferOptionDto>>.Failure(
                        "Không tìm thấy yêu cầu nhập hàng của cửa hàng.",
                        errorCode: BranchReceiptErrorCodes.RequestNotFound);
                }
                ingredientId = request.IngredientId;
            }

            var query = _context.IngredientSuppliers
                .AsNoTracking()
                .Where(x => x.SupplierId == supplierId
                            && x.Active
                            && x.Supplier.Active
                            && x.Ingredient.Active
                            && x.PackageQuantity.HasValue
                            && x.PackageQuantity.Value > 0
                            && x.CurrentPrice > 0
                            && x.UnitId > 0);
            if (ingredientId.HasValue)
                query = query.Where(x => x.IngredientId == ingredientId.Value);
            else if (restockRequestId.HasValue)
                query = query.Where(_ => false);

            var rows = await query
                .OrderBy(x => x.Ingredient.Name)
                .ThenByDescending(x => x.IsPrimary)
                .Select(x => new BranchReceiptOfferOptionDto
                {
                    IngredientSupplierId = x.IngredientSupplierId,
                    SupplierId = x.SupplierId,
                    IngredientId = x.IngredientId,
                    IngredientName = x.Ingredient.Name,
                    PackageUnitId = x.UnitId,
                    PackageUnitName = x.Unit.Name,
                    PackageQuantity = x.PackageQuantity!.Value,
                    PackagePrice = x.CurrentPrice,
                    MinimumOrderPackageCount = x.MinimumOrderPackageCount ?? 0,
                    LeadTimeDays = x.LeadTimeDays ?? 0,
                    PackageDisplay = string.Empty
                })
                .ToListAsync();

            foreach (var row in rows)
                row.PackageDisplay = $"{row.PackageQuantity:0.####} {row.PackageUnitName} / gói";

            return ServiceResult<List<BranchReceiptOfferOptionDto>>.Success(rows);
        }

        private async Task<ServiceResult<(CreateBranchReceiptLineInput Input, RestockRequest Request, decimal BaseQty, int BaseUnitId, decimal UnitCost, decimal LineTotal)>> BuildLineSnapshotAsync(
            CreateBranchReceiptLineInput input,
            int storeId,
            int? headerSupplierId)
        {
            if (input.RestockRequestId <= 0)
            {
                return ServiceResult<(CreateBranchReceiptLineInput, RestockRequest, decimal, int, decimal, decimal)>.Failure(
                    "RestockRequestId không hợp lệ.",
                    errorCode: BranchReceiptErrorCodes.RequestNotFound);
            }

            if (input.InputQuantity <= 0)
            {
                return ServiceResult<(CreateBranchReceiptLineInput, RestockRequest, decimal, int, decimal, decimal)>.Failure(
                    "Số lượng thực nhận phải lớn hơn 0.",
                    errorCode: BranchReceiptErrorCodes.QuantityInvalid);
            }

            var request = await _context.RestockRequests
                .AsNoTracking()
                .Include(r => r.Ingredient)
                .Include(r => r.PreparedItem)
                .FirstOrDefaultAsync(r => r.RestockRequestId == input.RestockRequestId);

            if (request == null)
            {
                return ServiceResult<(CreateBranchReceiptLineInput, RestockRequest, decimal, int, decimal, decimal)>.Failure(
                    $"Không tìm thấy yêu cầu #{input.RestockRequestId}.",
                    errorCode: BranchReceiptErrorCodes.RequestNotFound);
            }

            if (request.StoreId != storeId)
            {
                return ServiceResult<(CreateBranchReceiptLineInput, RestockRequest, decimal, int, decimal, decimal)>.Failure(
                    "Store không khớp yêu cầu.",
                    errorCode: BranchReceiptErrorCodes.StoreMismatch);
            }

            // New supplier-package path: server owns the purchasing snapshot. Client values
            // are display hints only and must not override current package identity/price.
            if (input.IngredientSupplierId.HasValue)
            {
                if (!request.IngredientId.HasValue)
                {
                    return ServiceResult<(CreateBranchReceiptLineInput, RestockRequest, decimal, int, decimal, decimal)>.Failure(
                        "Gói mua nhà cung cấp chỉ áp dụng cho yêu cầu nguyên liệu.",
                        errorCode: BranchReceiptErrorCodes.IdentityMismatch);
                }

                var offer = await _context.IngredientSuppliers
                    .AsNoTracking()
                    .Include(x => x.Supplier)
                    .Include(x => x.Ingredient)
                    .Include(x => x.Unit)
                    .FirstOrDefaultAsync(x => x.IngredientSupplierId == input.IngredientSupplierId.Value);

                if (offer == null
                    || !offer.Active
                    || !offer.Supplier.Active
                    || !offer.Ingredient.Active
                    || !offer.PackageQuantity.HasValue
                    || offer.PackageQuantity.Value <= 0
                    || offer.CurrentPrice <= 0
                    || offer.UnitId <= 0)
                {
                    return ServiceResult<(CreateBranchReceiptLineInput, RestockRequest, decimal, int, decimal, decimal)>.Failure(
                        "Gói mua không tồn tại, đã ngừng hoạt động hoặc thiếu quy cách/giá.",
                        errorCode: BranchReceiptErrorCodes.OfferNotAvailable);
                }

                if (offer.IngredientId != request.IngredientId.Value)
                {
                    return ServiceResult<(CreateBranchReceiptLineInput, RestockRequest, decimal, int, decimal, decimal)>.Failure(
                        "Gói mua không khớp nguyên liệu của yêu cầu nhập.",
                        errorCode: BranchReceiptErrorCodes.IdentityMismatch);
                }

                if (headerSupplierId.HasValue && headerSupplierId.Value != offer.SupplierId)
                {
                    return ServiceResult<(CreateBranchReceiptLineInput, RestockRequest, decimal, int, decimal, decimal)>.Failure(
                        "Gói mua không thuộc nhà cung cấp trên phiếu nhận.",
                        errorCode: BranchReceiptErrorCodes.IdentityMismatch);
                }

                if (!await IsSupplierAssignedToStoreAsync(offer.SupplierId, storeId))
                {
                    return ServiceResult<(CreateBranchReceiptLineInput, RestockRequest, decimal, int, decimal, decimal)>.Failure(
                        "Nhà cung cấp của gói mua chưa được gán cho cửa hàng.",
                        errorCode: BranchReceiptErrorCodes.SupplierNotAssigned);
                }

                var minimumPackages = offer.MinimumOrderPackageCount.GetValueOrDefault();
                if (minimumPackages > 0 && input.InputQuantity < minimumPackages)
                {
                    return ServiceResult<(CreateBranchReceiptLineInput, RestockRequest, decimal, int, decimal, decimal)>.Failure(
                        $"Số gói nhận phải đạt MOQ tối thiểu {minimumPackages:N0} gói.",
                        errorCode: BranchReceiptErrorCodes.MinimumOrderNotMet);
                }

                input.SupplierId = offer.SupplierId;
                input.InputUnitId = offer.UnitId;
                input.PackageUnitId = offer.UnitId;
                input.PackageQuantity = offer.PackageQuantity.Value;
                input.ActualPackagePrice = offer.CurrentPrice;
            }

            if (input.InputUnitId <= 0)
            {
                return ServiceResult<(CreateBranchReceiptLineInput, RestockRequest, decimal, int, decimal, decimal)>.Failure(
                    "Đơn vị nhận không hợp lệ.",
                    errorCode: BranchReceiptErrorCodes.QuantityInvalid);
            }

            int baseUnitId;
            decimal baseQty;
            var hasPackageSnapshot = input.PackageQuantity.HasValue && input.PackageQuantity.Value > 0;
            var physicalQuantity = hasPackageSnapshot
                ? input.InputQuantity * input.PackageQuantity!.Value
                : input.InputQuantity;
            var physicalUnitId = hasPackageSnapshot
                ? input.PackageUnitId.GetValueOrDefault(input.InputUnitId)
                : input.InputUnitId;

            if (physicalUnitId <= 0)
            {
                return ServiceResult<(CreateBranchReceiptLineInput, RestockRequest, decimal, int, decimal, decimal)>.Failure(
                    "Đơn vị nội dung gói mua không hợp lệ.",
                    errorCode: BranchReceiptErrorCodes.QuantityInvalid);
            }

            if (request.IngredientId.HasValue)
            {
                baseUnitId = request.Ingredient!.BaseUnitId;
                var conv = await _unitConversion.ConvertAsync(
                    request.IngredientId.Value,
                    physicalQuantity,
                    physicalUnitId,
                    baseUnitId);
                if (!conv.IsSuccess)
                {
                    return ServiceResult<(CreateBranchReceiptLineInput, RestockRequest, decimal, int, decimal, decimal)>.Failure(
                        conv.Message ?? "Quy đổi đơn vị thất bại.",
                        errorCode: BranchReceiptErrorCodes.ConversionFailed);
                }

                baseQty = conv.Data;
            }
            else if (request.PreparedItemId.HasValue)
            {
                baseUnitId = request.PreparedItem!.BaseUnitId;
                var conv = await _physicalConversion.ConvertAsync(
                    physicalQuantity,
                    physicalUnitId,
                    baseUnitId);
                if (!conv.IsSuccess)
                {
                    return ServiceResult<(CreateBranchReceiptLineInput, RestockRequest, decimal, int, decimal, decimal)>.Failure(
                        conv.Message ?? "Quy đổi đơn vị BTP thất bại.",
                        errorCode: BranchReceiptErrorCodes.ConversionFailed);
                }

                baseQty = conv.Data;
            }
            else
            {
                return ServiceResult<(CreateBranchReceiptLineInput, RestockRequest, decimal, int, decimal, decimal)>.Failure(
                    "Yêu cầu Recipe-only không tạo receipt line mới trong PreparedItem mode.",
                    errorCode: BranchReceiptErrorCodes.IdentityMismatch);
            }

            if (baseQty <= 0)
            {
                return ServiceResult<(CreateBranchReceiptLineInput, RestockRequest, decimal, int, decimal, decimal)>.Failure(
                    "Số lượng base sau quy đổi phải > 0.",
                    errorCode: BranchReceiptErrorCodes.QuantityInvalid);
            }

            // Cost snapshot: fail-closed. Prefer explicit package price; compute base unit cost.
            if (input.ActualPackagePrice <= 0)
            {
                return ServiceResult<(CreateBranchReceiptLineInput, RestockRequest, decimal, int, decimal, decimal)>.Failure(
                    "ActualPackagePrice bắt buộc và phải > 0 (RECEIPT_COST_INCOMPLETE).",
                    errorCode: BranchReceiptErrorCodes.ReceiptCostIncomplete);
            }

            decimal unitCost;
            decimal lineTotal;

            if (hasPackageSnapshot)
            {
                // Package path: InputQuantity packages × package price = line total;
                // each package has PackageQuantity in package unit → convert content to base if needed.
                // Spec D5: InputQuantity packages × package content → base; cost from ActualPackagePrice.
                lineTotal = Math.Round(input.InputQuantity * input.ActualPackagePrice, 2, MidpointRounding.AwayFromZero);
                unitCost = baseQty > 0
                    ? Math.Round(lineTotal / baseQty, 4, MidpointRounding.AwayFromZero)
                    : 0m;
            }
            else
            {
                // Unit price already in input unit: total = input qty * price; unit cost in base.
                lineTotal = Math.Round(input.InputQuantity * input.ActualPackagePrice, 2, MidpointRounding.AwayFromZero);
                unitCost = baseQty > 0
                    ? Math.Round(lineTotal / baseQty, 4, MidpointRounding.AwayFromZero)
                    : 0m;
            }

            if (unitCost <= 0 || lineTotal <= 0)
            {
                return ServiceResult<(CreateBranchReceiptLineInput, RestockRequest, decimal, int, decimal, decimal)>.Failure(
                    "Không tạo cost layer giá 0 (RECEIPT_COST_INCOMPLETE).",
                    errorCode: BranchReceiptErrorCodes.ReceiptCostIncomplete);
            }

            return ServiceResult<(CreateBranchReceiptLineInput, RestockRequest, decimal, int, decimal, decimal)>.Success(
                (input, request, baseQty, baseUnitId, unitCost, lineTotal));
        }

        private Task<bool> IsSupplierAssignedToStoreAsync(int supplierId, int storeId)
        {
            return _context.SupplierStores
                .AsNoTracking()
                .AnyAsync(x => x.SupplierId == supplierId
                               && x.StoreId == storeId
                               && x.Active
                               && x.Supplier.Active
                               && x.Store.Active);
        }

        private async Task<decimal> SumFulfillmentPostingsAsync(int restockRequestId)
        {
            // SQLite cannot Sum(decimal) server-side — load then aggregate client-side.
            var qtys = await _context.RestockFulfillmentPostings
                .AsNoTracking()
                .Where(p => p.RestockRequestId == restockRequestId)
                .Select(p => p.Quantity)
                .ToListAsync();
            return qtys.Sum();
        }

        private async Task<BranchReceipt?> LoadReceiptForUpdateAsync(int branchReceiptId)
        {
            if (_context.Database.IsSqlServer())
            {
                var locked = await _context.BranchReceipts
                    .FromSqlInterpolated(
                        $@"SELECT * FROM BranchReceipts WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
                           WHERE BranchReceiptId = {branchReceiptId}")
                    .SingleOrDefaultAsync();
                if (locked == null) return null;
                await _context.Entry(locked).Collection(r => r.Lines).LoadAsync();
                return locked;
            }

            return await _context.BranchReceipts
                .Include(r => r.Lines)
                .SingleOrDefaultAsync(r => r.BranchReceiptId == branchReceiptId);
        }

        private async Task<RestockRequest?> LoadRestockRequestForUpdateAsync(int restockRequestId)
        {
            if (_context.Database.IsSqlServer())
            {
                return await _context.RestockRequests
                    .FromSqlInterpolated(
                        $@"SELECT * FROM RestockRequests WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
                           WHERE RestockRequestId = {restockRequestId}")
                    .SingleOrDefaultAsync();
            }

            return await _context.RestockRequests
                .SingleOrDefaultAsync(r => r.RestockRequestId == restockRequestId);
        }

        private async Task<StoreInventory> GetOrCreateIngredientInventoryAsync(int storeId, int ingredientId)
        {
            if (_context.Database.IsSqlServer())
            {
                var locked = await _context.StoreInventories
                    .FromSqlInterpolated(
                        $@"SELECT * FROM StoreInventories WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
                           WHERE StoreId = {storeId} AND IngredientId = {ingredientId}")
                    .FirstOrDefaultAsync();
                if (locked != null) return locked;
            }
            else
            {
                var existing = await _context.StoreInventories
                    .FirstOrDefaultAsync(x => x.StoreId == storeId && x.IngredientId == ingredientId);
                if (existing != null) return existing;
            }

            var inv = new StoreInventory
            {
                StoreId = storeId,
                IngredientId = ingredientId,
                AvailableQty = 0,
                ReservedQty = 0,
                LastUpdated = DateTime.UtcNow
            };
            _context.StoreInventories.Add(inv);
            await _context.SaveChangesAsync();
            return inv;
        }

        private async Task<Dictionary<int, StoreInventory>> LockStoreInventoriesAsync(List<int> storeInventoryIds)
        {
            var result = new Dictionary<int, StoreInventory>();
            if (storeInventoryIds.Count == 0) return result;

            if (_context.Database.IsSqlServer())
            {
                // Deterministic ASC locks — avoid unordered IN.
                foreach (var id in storeInventoryIds.OrderBy(x => x))
                {
                    var row = await _context.StoreInventories
                        .FromSqlInterpolated(
                            $@"SELECT * FROM StoreInventories WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
                               WHERE StoreInventoryId = {id}")
                        .SingleOrDefaultAsync();
                    if (row != null)
                        result[id] = row;
                }
            }
            else
            {
                var rows = await _context.StoreInventories
                    .Where(x => storeInventoryIds.Contains(x.StoreInventoryId))
                    .OrderBy(x => x.StoreInventoryId)
                    .ToListAsync();
                foreach (var r in rows)
                    result[r.StoreInventoryId] = r;
            }

            return result;
        }

        private async Task<BranchReceiptDetailDto> MapDetailAsync(int id)
        {
            var receipt = await _context.BranchReceipts
                .AsNoTracking()
                .Include(r => r.Supplier)
                .Include(r => r.Lines).ThenInclude(l => l.InputUnit)
                .Include(r => r.Lines).ThenInclude(l => l.BaseUnit)
                .FirstAsync(r => r.BranchReceiptId == id);
            return MapDetail(receipt);
        }

        private static BranchReceiptDetailDto MapDetail(BranchReceipt r)
        {
            var dto = new BranchReceiptDetailDto
            {
                BranchReceiptId = r.BranchReceiptId,
                ReceiptCode = r.ReceiptCode,
                ReceiptKey = r.ReceiptKey,
                Status = r.Status,
                StoreId = r.StoreId,
                SupplierId = r.SupplierId,
                SupplierName = r.Supplier?.Name,
                ReceivedAt = r.ReceivedAt,
                ConfirmedAt = r.ConfirmedAt,
                ReferenceNumber = r.ReferenceNumber,
                Notes = r.Notes,
                CreatedByStaffId = r.CreatedByStaffId,
                ConfirmedByStaffId = r.ConfirmedByStaffId,
                ReceivedByStaffId = r.ReceivedByStaffId,
                LineCount = r.Lines?.Count ?? 0,
                TotalBaseQuantity = r.Lines?.Sum(l => l.ReceivedBaseQuantity) ?? 0,
                TotalLineCost = r.Lines?.Sum(l => l.LineTotalCost) ?? 0,
                Lines = (r.Lines ?? Enumerable.Empty<BranchReceiptLine>()).Select(l => new BranchReceiptLineDto
                {
                    BranchReceiptLineId = l.BranchReceiptLineId,
                    RestockRequestId = l.RestockRequestId,
                    IngredientId = l.IngredientId,
                    PreparedItemId = l.PreparedItemId,
                    RecipeId = l.RecipeId,
                    InputQuantity = l.InputQuantity,
                    InputUnitId = l.InputUnitId,
                    InputUnitName = l.InputUnit?.Name,
                    ReceivedBaseQuantity = l.ReceivedBaseQuantity,
                    BaseUnitId = l.BaseUnitId,
                    BaseUnitName = l.BaseUnit?.Name,
                    ActualPackagePrice = l.ActualPackagePrice,
                    PackageQuantitySnapshot = l.PackageQuantitySnapshot,
                    PackageUnitIdSnapshot = l.PackageUnitIdSnapshot,
                    BaseUnitCostSnapshot = l.BaseUnitCostSnapshot,
                    LineTotalCost = l.LineTotalCost,
                    InventoryTransactionId = l.InventoryTransactionId
                }).ToList()
            };
            return dto;
        }

        private static BranchReceiptListItemDto MapListItem(BranchReceipt r) => new()
        {
            BranchReceiptId = r.BranchReceiptId,
            ReceiptCode = r.ReceiptCode,
            ReceiptKey = r.ReceiptKey,
            Status = r.Status,
            StoreId = r.StoreId,
            SupplierId = r.SupplierId,
            SupplierName = r.Supplier?.Name,
            ReceivedAt = r.ReceivedAt,
            ConfirmedAt = r.ConfirmedAt,
            LineCount = r.Lines?.Count ?? 0,
            TotalBaseQuantity = r.Lines?.Sum(l => l.ReceivedBaseQuantity) ?? 0,
            TotalLineCost = r.Lines?.Sum(l => l.LineTotalCost) ?? 0
        };

        private static bool IsReceivableStatus(string status) =>
            status is RestockRequestStatuses.Submitted
                or RestockRequestStatuses.Processing
                or RestockRequestStatuses.PartiallyReceived;

        private static bool IdentityMatches(RestockRequest req, BranchReceiptLine line)
        {
            if (req.IngredientId.HasValue)
                return line.IngredientId == req.IngredientId && line.PreparedItemId == null;

            if (req.PreparedItemId.HasValue)
                return line.PreparedItemId == req.PreparedItemId && line.IngredientId == null;

            return false;
        }

        private async Task<ServiceResult> AuthorizeReceiptAccessAsync(
            int storeId,
            int actorStaffId,
            int? actorStoreId,
            IReadOnlyCollection<string> roleNames,
            bool mutation)
        {
            if (roleNames.Contains(RoleConstants.BusinessOwner)
                || roleNames.Contains(RoleConstants.AccountantWarehouse))
                return ServiceResult.Success();

            if (roleNames.Contains(RoleConstants.AreaManager))
            {
                return actorStaffId > 0
                       && await _scopeAuthorization.CanAccessStoreAsync(actorStaffId, storeId)
                    ? ServiceResult.Success()
                    : ServiceResult.Failure(
                        "Cửa hàng nằm ngoài phạm vi quản lý vùng.",
                        errorCode: BranchReceiptErrorCodes.Unauthorized);
            }

            var allowedBranchRole = roleNames.Contains(RoleConstants.StoreManager)
                || (!mutation && (roleNames.Contains(RoleConstants.ShiftSupervisor)
                                   || roleNames.Contains(RoleConstants.SalesStaff)));
            if (allowedBranchRole)
            {
                var staffStoreId = await _context.Staffs
                    .AsNoTracking()
                    .Where(s => s.StaffId == actorStaffId && s.Active)
                    .Select(s => (int?)s.StoreId)
                    .FirstOrDefaultAsync();
                if (!staffStoreId.HasValue)
                    staffStoreId = actorStoreId;
                if (staffStoreId.HasValue && staffStoreId.Value == storeId)
                    return ServiceResult.Success();
            }

            return ServiceResult.Failure(
                "Không có quyền truy cập phiếu nhận cửa hàng này.",
                errorCode: BranchReceiptErrorCodes.Unauthorized);
        }

        private static bool CanCreateOrConfirmReceipt(IReadOnlyCollection<string> roles) =>
            roles.Contains(RoleConstants.StoreManager)
            || roles.Contains(RoleConstants.BusinessOwner)
            || roles.Contains(RoleConstants.AreaManager)
            || roles.Contains(RoleConstants.AccountantWarehouse);

        private static bool IsUniqueViolation(DbUpdateException ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            return msg.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
                   || msg.Contains("unique", StringComparison.OrdinalIgnoreCase)
                   || msg.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
                   || msg.Contains("2627") // SQL Server unique
                   || msg.Contains("2601");
        }

        private static ServiceResult<BranchReceiptDetailDto> FailDetail(string message, string? code = null) =>
            ServiceResult<BranchReceiptDetailDto>.Failure(message, errorCode: code);
    }
}
