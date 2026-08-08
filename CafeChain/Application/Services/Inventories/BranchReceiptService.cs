using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.RestockRequests;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Costing;
using CafeChain.Models.Inventories.Procurement;
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
        private readonly IPurchaseOrderService? _purchaseOrders;

        public BranchReceiptService(
            AppDbContext context,
            IUnitConversionService unitConversion,
            IPhysicalUnitConversionService physicalConversion,
            IInventoryWriterModeService writerModeService,
            IStoreInventoryWriteResolver writeResolver,
            IRestockFulfillmentPostingService fulfillmentPostingService,
            IStockAlertService stockAlertService,
            IScopeAuthorizationService scopeAuthorization,
            ILogger<BranchReceiptService> logger,
            IPurchaseOrderService? purchaseOrders = null)
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
            _purchaseOrders = purchaseOrders;
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
                return FailDetail("Mã chi nhánh không hợp lệ.", BranchReceiptErrorCodes.StoreMismatch);

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
                return FailDetail("Mã chống gửi trùng là bắt buộc và không được vượt quá 100 ký tự.", BranchReceiptErrorCodes.ReceiptKeyRequired);

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
                if (string.Equals(req.SourcingDecision, RestockSourcingDecisionTypes.Purchase, StringComparison.OrdinalIgnoreCase)
                    && g.Any(x => !x.Input.PurchaseOrderLineId.HasValue))
                {
                    return FailDetail(
                        $"Yêu cầu mua ngoài #{req.RestockRequestId} chỉ được nhận hàng từ dòng đơn đặt hàng đã liên kết.",
                        BranchReceiptErrorCodes.RequestStateInvalid);
                }
                if (!IsReceivableStatus(req.Status))
                {
                    return FailDetail(
                        $"Yêu cầu #{req.RestockRequestId} không ở trạng thái cho phép nhận hàng.",
                        BranchReceiptErrorCodes.RequestStateInvalid);
                }

                if (req.StoreId != request.StoreId)
                {
                    return FailDetail(
                        $"Yêu cầu #{req.RestockRequestId} không thuộc cửa hàng phiếu nhận.",
                        BranchReceiptErrorCodes.StoreMismatch);
                }

                if (req.RequestedProcurementQuantity.HasValue
                    && req.ProcurementUnitId.HasValue)
                {
                    var factorResult = await GetProcurementToInventoryFactorAsync(
                        req.IngredientId,
                        req.PreparedItemId,
                        req.ProcurementUnitId.Value,
                        req.Ingredient?.BaseUnitId ?? req.PreparedItem?.BaseUnitId ?? 0);
                    if (!factorResult.IsSuccess || factorResult.Data <= 0)
                        return FailDetail(
                            factorResult.Message ?? "Không xác định được hệ số quy đổi để kiểm tra phiếu nhận.",
                            BranchReceiptErrorCodes.ConversionFailed);

                    var confirmedBase = await SumFulfillmentPostingsAsync(req.RestockRequestId);
                    var confirmedProcurement = confirmedBase / factorResult.Data;
                    var newProcurement = 0m;
                    foreach (var item in g)
                    {
                        var snapshot = await BuildDirectProcurementSnapshotAsync(
                            item.Request,
                            item.Input,
                            item.BaseQty,
                            item.BaseUnitId);
                        if (!snapshot.IsSuccess || snapshot.Data == null)
                            return FailDetail(
                                snapshot.Message ?? "Không xác định được số lượng mua hàng của phiếu nhận.",
                                snapshot.ErrorCode);
                        newProcurement += snapshot.Data.AcceptedQuantity;
                    }

                    if (confirmedProcurement + newProcurement > req.RequestedProcurementQuantity.Value)
                    {
                        return FailDetail(
                            $"Vượt nhu cầu #{req.RestockRequestId}: đã nhận {confirmedProcurement:N3}, thêm {newProcurement:N3}, yêu cầu {req.RequestedProcurementQuantity.Value:N3} {req.ProcurementUnit?.Name}.",
                            BranchReceiptErrorCodes.RestockOverReceiptNotAllowed);
                    }
                }
                else
                {
                    var confirmed = await SumFulfillmentPostingsAsync(req.RestockRequestId);
                    var newSum = g.Sum(x => x.BaseQty);
                    if (confirmed + newSum > req.RequestedQuantity)
                    {
                        return FailDetail(
                            $"Vượt số lượng yêu cầu #{req.RestockRequestId}: đã nhận {confirmed:N3}, thêm {newSum:N3}, yêu cầu {req.RequestedQuantity:N3}.",
                            BranchReceiptErrorCodes.RestockOverReceiptNotAllowed);
                    }
                }
            }

            var existingKey = await _context.BranchReceipts
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.StoreId == request.StoreId && r.ReceiptKey == receiptKey);
            if (existingKey != null)
            {
                return FailDetail(
                    "Mã chống gửi trùng đã tồn tại cho chi nhánh này.",
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
                var procurementSnapshot = await BuildDirectProcurementSnapshotAsync(
                    p.Request,
                    p.Input,
                    p.BaseQty,
                    p.BaseUnitId);
                if (!procurementSnapshot.IsSuccess)
                    return FailDetail(procurementSnapshot.Message, procurementSnapshot.ErrorCode);

                var line = new BranchReceiptLine
                {
                    RestockRequestId = p.Request.RestockRequestId,
                    PurchaseOrderLineId = p.Input.PurchaseOrderLineId,
                    RestockRequestFulfillmentId = p.Input.RestockRequestFulfillmentId,
                    IngredientId = p.Request.IngredientId,
                    // New lines: PreparedItem identity only when request is PI-based (no Recipe-only).
                    PreparedItemId = p.Request.IngredientId.HasValue ? null : p.Request.PreparedItemId,
                    RecipeId = p.Request.IngredientId.HasValue
                        ? null
                        : (p.Request.PreparedItemId.HasValue ? null : p.Request.RecipeId), // avoid Recipe-only if PI present
                    InputQuantity = p.Input.ActualReceivedQuantity,
                    InputUnitId = p.Input.InputUnitId,
                    ReceivedBaseQuantity = p.BaseQty,
                    RejectedBaseQuantity = procurementSnapshot.Data == null
                        ? p.Input.RejectedQuantity
                        : 0m,
                    RejectionReason = p.Input.RejectionReason,
                    RejectionIssueType = p.Input.RejectionIssueType,
                    ReceivedProcurementQuantity = procurementSnapshot.Data?.ReceivedQuantity,
                    RejectedProcurementQuantity = procurementSnapshot.Data?.RejectedQuantity,
                    AcceptedProcurementQuantity = procurementSnapshot.Data?.AcceptedQuantity,
                    InventoryPostingBaseQuantity = procurementSnapshot.Data?.AcceptedBaseQuantity,
                    ProcurementUnitId = procurementSnapshot.Data?.ProcurementUnitId,
                    InventoryBaseUnitId = procurementSnapshot.Data?.InventoryBaseUnitId,
                    ProcurementToInventoryFactor = procurementSnapshot.Data?.ConversionFactor,
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
                        $"Yêu cầu #{p.Request.RestockRequestId} không có định danh nguyên liệu hoặc bán thành phẩm hợp lệ để nhận hàng.",
                        BranchReceiptErrorCodes.IdentityMismatch);
                }

                receipt.Lines.Add(line);
            }

            if (_purchaseOrders != null)
            {
                foreach (var line in receipt.Lines.Where(x => x.PurchaseOrderLineId.HasValue))
                {
                    var poValidation = await _purchaseOrders.ValidateReceiptLineAsync(receipt, line);
                    if (!poValidation.IsSuccess)
                        return FailDetail(poValidation.Message);
                }
            }

            try
            {
                _context.BranchReceipts.Add(receipt);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                return FailDetail(
                    "Mã chống gửi trùng đã tồn tại do một yêu cầu khác được xử lý đồng thời.",
                    BranchReceiptErrorCodes.DuplicateReceiptKey);
            }

            _logger.LogInformation(
                "[BranchReceipt] Draft created Id={Id} Store={Store} Key={Key} Lines={Lines} (no inventory mutation)",
                receipt.BranchReceiptId, receipt.StoreId, receipt.ReceiptKey, receipt.Lines.Count);

            return ServiceResult<BranchReceiptDetailDto>.Success(
                await MapDetailAsync(receipt.BranchReceiptId),
                "Đã tạo phiếu nhận nháp (chưa nhập kho).");
        }

        public async Task<ServiceResult<PurchaseOrderReceiptDraftDto>> CreateOrOpenPurchaseOrderDraftAsync(
            int purchaseOrderId,
            int actorStaffId,
            int? actorStoreId,
            IReadOnlyCollection<string> roleNames)
        {
            if (!CanCreateOrConfirmReceipt(roleNames))
                return FailPurchaseOrderDraft("Bạn không có quyền nhận hàng tại cửa hàng.", BranchReceiptErrorCodes.Unauthorized);
            if (purchaseOrderId <= 0)
                return FailPurchaseOrderDraft("Mã đơn đặt hàng không hợp lệ.");

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (_context.Database.IsSqlServer())
                {
                    await _context.PurchaseOrders.FromSqlInterpolated(
                            $@"SELECT * FROM PurchaseOrders WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
                               WHERE PurchaseOrderId = {purchaseOrderId}")
                        .SingleOrDefaultAsync();
                }

                var order = await _context.PurchaseOrders
                    .Include(x => x.Lines).ThenInclude(x => x.ReceiptPostings)
                    .SingleOrDefaultAsync(x => x.PurchaseOrderId == purchaseOrderId);
                if (order == null)
                    return FailPurchaseOrderDraft("Không tìm thấy đơn mua hàng.");

                var auth = await AuthorizeReceiptAccessAsync(
                    order.StoreId, actorStaffId, actorStoreId, roleNames, mutation: true);
                if (!auth.IsSuccess)
                    return FailPurchaseOrderDraft(auth.Message, auth.ErrorCode);
                if (order.Status is not (PurchaseOrderStatuses.MarkedAsSent
                    or PurchaseOrderStatuses.PartiallyReceived))
                    return FailPurchaseOrderDraft(
                        "Đơn đặt hàng chưa ở trạng thái cho phép nhận hàng.",
                        BranchReceiptErrorCodes.RequestStateInvalid);

                var hasRemaining = order.Lines.Any(x =>
                    x.OrderedProcurementQuantity.HasValue
                        ? x.OrderedProcurementQuantity.Value
                            - x.ReceiptPostings.Sum(p => p.AcceptedProcurementQuantity ?? 0m)
                            - x.ClosedProcurementQuantity > 0
                        : x.OrderedBaseQuantity
                            - x.ReceiptPostings.Sum(p => p.AcceptedBaseQuantity)
                            - x.ClosedRemainingQuantity > 0);
                if (!hasRemaining)
                    return FailPurchaseOrderDraft("Đơn đặt hàng đã nhận đủ hoặc đã đóng toàn bộ phần còn lại.");

                var existing = await _context.BranchReceipts
                    .Include(x => x.Lines)
                    .FirstOrDefaultAsync(x => x.PurchaseOrderId == purchaseOrderId
                        && x.Status == BranchReceiptStatuses.Draft);
                if (existing == null)
                {
                    var now = DateTime.UtcNow;
                    existing = new BranchReceipt
                    {
                        PurchaseOrderId = order.PurchaseOrderId,
                        StoreId = order.StoreId,
                        SupplierId = order.SupplierId,
                        ReceiptCode = $"BR-PO-{order.PurchaseOrderId}-{now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
                        ReceiptKey = $"PO-{order.PurchaseOrderId}-{Guid.NewGuid():N}",
                        Status = BranchReceiptStatuses.Draft,
                        ReceivedAt = now,
                        ReceivedByStaffId = actorStaffId,
                        CreatedAt = now,
                        CreatedByStaffId = actorStaffId
                    };
                    _context.BranchReceipts.Add(existing);
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();
                return ServiceResult<PurchaseOrderReceiptDraftDto>.Success(
                    await MapPurchaseOrderDraftAsync(existing.BranchReceiptId),
                    existing.Lines.Count > 0
                        ? "Đã mở lại phiếu kiểm đếm đang lưu."
                        : "Đã tạo phiếu kiểm đếm nháp từ đơn đặt hàng.");
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();
                var winner = await _context.BranchReceipts.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.PurchaseOrderId == purchaseOrderId
                        && x.Status == BranchReceiptStatuses.Draft);
                if (winner != null)
                    return ServiceResult<PurchaseOrderReceiptDraftDto>.Success(
                        await MapPurchaseOrderDraftAsync(winner.BranchReceiptId),
                        "Đã mở phiếu kiểm đếm được tạo bởi thao tác đồng thời.");
                throw;
            }
        }

        public async Task<ServiceResult<PurchaseOrderReceiptDraftDto>> GetPurchaseOrderDraftAsync(
            int branchReceiptId,
            int actorStaffId,
            int? actorStoreId,
            IReadOnlyCollection<string> roleNames)
        {
            var receipt = await _context.BranchReceipts.AsNoTracking()
                .FirstOrDefaultAsync(x => x.BranchReceiptId == branchReceiptId);
            if (receipt?.PurchaseOrderId == null || receipt.Status != BranchReceiptStatuses.Draft)
                return FailPurchaseOrderDraft("Không tìm thấy phiếu kiểm đếm đơn đặt hàng đang mở.");
            var auth = await AuthorizeReceiptAccessAsync(
                receipt.StoreId, actorStaffId, actorStoreId, roleNames, mutation: true);
            if (!auth.IsSuccess)
                return FailPurchaseOrderDraft(auth.Message, auth.ErrorCode);
            return ServiceResult<PurchaseOrderReceiptDraftDto>.Success(
                await MapPurchaseOrderDraftAsync(branchReceiptId));
        }

        public async Task<ServiceResult<PurchaseOrderReceiptDraftDto>> SavePurchaseOrderDraftAsync(
            SavePurchaseOrderReceiptDraftRequest request,
            int actorStaffId,
            int? actorStoreId,
            IReadOnlyCollection<string> roleNames)
        {
            if (!CanCreateOrConfirmReceipt(roleNames))
                return FailPurchaseOrderDraft("Bạn không có quyền lưu phiếu kiểm đếm.", BranchReceiptErrorCodes.Unauthorized);
            if (!TryParseRequiredRowVersion(request.RowVersion, out var expectedVersion))
                return FailPurchaseOrderDraft("Thiếu phiên bản dữ liệu. Vui lòng tải lại.", BranchReceiptErrorCodes.ValidationRowVersionRequired);

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var receipt = await LoadReceiptForUpdateAsync(request.BranchReceiptId);
                if (receipt?.PurchaseOrderId == null)
                    return FailPurchaseOrderDraft("Không tìm thấy phiếu kiểm đếm đơn đặt hàng.");
                var auth = await AuthorizeReceiptAccessAsync(
                    receipt.StoreId, actorStaffId, actorStoreId, roleNames, mutation: true);
                if (!auth.IsSuccess)
                    return FailPurchaseOrderDraft(auth.Message, auth.ErrorCode);
                if (receipt.Status != BranchReceiptStatuses.Draft)
                    return FailPurchaseOrderDraft("Chỉ được sửa phiếu DRAFT.", BranchReceiptErrorCodes.ReceiptNotDraft);
                if (!receipt.RowVersion.SequenceEqual(expectedVersion))
                    return FailPurchaseOrderDraft("Phiếu đã được người khác cập nhật. Vui lòng tải lại.", BranchReceiptErrorCodes.ResourceChanged);
                _context.Entry(receipt).Property(x => x.RowVersion).OriginalValue = expectedVersion;

                var order = await _context.PurchaseOrders
                    .Include(x => x.Lines).ThenInclude(x => x.Ingredient)
                    .Include(x => x.Lines).ThenInclude(x => x.PackageUnitSnapshot)
                    .Include(x => x.Lines).ThenInclude(x => x.ReceiptPostings)
                    .SingleAsync(x => x.PurchaseOrderId == receipt.PurchaseOrderId.Value);
                if (order.StoreId != receipt.StoreId || order.SupplierId != receipt.SupplierId)
                    return FailPurchaseOrderDraft("Đơn đặt hàng và phiếu nhận không cùng chi nhánh/nhà cung cấp.", BranchReceiptErrorCodes.PoLineScopeMismatch);

                var submitted = request.Lines
                    .Where(x => x.ActualReceivedQuantity.GetValueOrDefault() > 0)
                    .ToList();
                if (submitted.Count == 0)
                    return FailPurchaseOrderDraft("Nhập số lượng Nhà cung cấp giao cho ít nhất một dòng.", BranchReceiptErrorCodes.ActualReceivedNotPositive);
                if (submitted.GroupBy(x => x.PurchaseOrderLineId).Any(x => x.Count() > 1))
                    return FailPurchaseOrderDraft("Một dòng đơn đặt hàng không được xuất hiện nhiều lần trong phiếu.");

                var newLines = new List<BranchReceiptLine>();
                foreach (var input in submitted)
                {
                    var poLine = order.Lines.SingleOrDefault(x => x.PurchaseOrderLineId == input.PurchaseOrderLineId);
                    if (poLine == null)
                        return FailPurchaseOrderDraft("Dòng hàng không thuộc đơn đặt hàng đang nhận.", BranchReceiptErrorCodes.PoLineScopeMismatch);
                    var built = await BuildPurchaseOrderReceiptLineAsync(receipt, poLine, input);
                    if (!built.IsSuccess || built.Data == null)
                        return FailPurchaseOrderDraft(built.Message, built.ErrorCode);
                    newLines.Add(built.Data);
                }

                _context.BranchReceiptLines.RemoveRange(receipt.Lines);
                receipt.Lines.Clear();
                foreach (var line in newLines)
                    receipt.Lines.Add(line);
                receipt.ReferenceNumber = string.IsNullOrWhiteSpace(request.ReferenceNumber)
                    ? null : request.ReferenceNumber.Trim()[..Math.Min(request.ReferenceNumber.Trim().Length, 100)];
                receipt.Notes = string.IsNullOrWhiteSpace(request.Notes)
                    ? null : request.Notes.Trim()[..Math.Min(request.Notes.Trim().Length, 1000)];
                receipt.ReceivedByStaffId = actorStaffId;
                receipt.ReceivedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return ServiceResult<PurchaseOrderReceiptDraftDto>.Success(
                    await MapPurchaseOrderDraftAsync(receipt.BranchReceiptId),
                    "Đã lưu phiếu kiểm đếm nháp; tồn kho chưa thay đổi.");
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                return FailPurchaseOrderDraft("Phiếu đã được người khác cập nhật. Vui lòng tải lại.", BranchReceiptErrorCodes.ResourceChanged);
            }
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
                .Include(r => r.Lines).ThenInclude(l => l.ProcurementUnit)
                .Include(r => r.Lines).ThenInclude(l => l.RestockRequest)
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
            IReadOnlyCollection<string> roleNames,
            string? rowVersion)
        {
            if (!CanCreateOrConfirmReceipt(roleNames))
            {
                return ServiceResult<ConfirmBranchReceiptResultDto>.Failure(
                    "Bạn không có quyền xác nhận phiếu nhận.",
                    errorCode: BranchReceiptErrorCodes.Unauthorized);
            }

            if (!TryParseRequiredRowVersion(rowVersion, out var expectedVersion))
            {
                return ServiceResult<ConfirmBranchReceiptResultDto>.Failure(
                    "Thiếu phiên bản dữ liệu. Vui lòng tải lại trang.",
                    errorCode: BranchReceiptErrorCodes.ValidationRowVersionRequired);
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

                if (!receipt.RowVersion.SequenceEqual(expectedVersion))
                {
                    await transaction.RollbackAsync();
                    return ServiceResult<ConfirmBranchReceiptResultDto>.Failure(
                        "Phiếu nhận đã được người khác cập nhật. Vui lòng tải lại.",
                        errorCode: BranchReceiptErrorCodes.ResourceChanged);
                }
                _context.Entry(receipt).Property(x => x.RowVersion).OriginalValue = expectedVersion;

                if (receipt.Lines == null || receipt.Lines.Count == 0)
                {
                    await transaction.RollbackAsync();
                    return ServiceResult<ConfirmBranchReceiptResultDto>.Failure(
                        "Phiếu nhận không có dòng.",
                        errorCode: BranchReceiptErrorCodes.QuantityInvalid);
                }

                if (receipt.Lines.Any(x => !x.RestockRequestId.HasValue && !x.PurchaseOrderLineId.HasValue))
                {
                    await transaction.RollbackAsync();
                    return ServiceResult<ConfirmBranchReceiptResultDto>.Failure(
                        "Mỗi dòng nhận phải liên kết RestockRequest hoặc PurchaseOrderLine.",
                        errorCode: BranchReceiptErrorCodes.RequestNotFound);
                }

                // Lock restock requests in ascending ID order.
                var requestIds = receipt.Lines
                    .Where(l => l.RestockRequestId.HasValue)
                    .Select(l => l.RestockRequestId!.Value)
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
                    if (line.RestockRequestId.HasValue)
                    {
                        var req = requests[line.RestockRequestId.Value];
                        if (!IsReceivableStatus(req.Status))
                        {
                            await transaction.RollbackAsync();
                            return ServiceResult<ConfirmBranchReceiptResultDto>.Failure(
                                $"Yêu cầu #{req.RestockRequestId} không ở trạng thái cho phép nhận hàng.",
                                errorCode: BranchReceiptErrorCodes.RequestStateInvalid);
                        }
                        if (req.StoreId != receipt.StoreId)
                        {
                            await transaction.RollbackAsync();
                            return ServiceResult<ConfirmBranchReceiptResultDto>.Failure(
                                "Chi nhánh không khớp giữa phiếu nhận và yêu cầu.",
                                errorCode: BranchReceiptErrorCodes.StoreMismatch);
                        }
                        if (!IdentityMatches(req, line))
                        {
                            await transaction.RollbackAsync();
                            return ServiceResult<ConfirmBranchReceiptResultDto>.Failure(
                                $"Định danh dòng nhận không khớp yêu cầu #{req.RestockRequestId}.",
                                errorCode: BranchReceiptErrorCodes.IdentityMismatch);
                        }
                    }

                    if (line.AcceptedProcurementQuantity.HasValue)
                    {
                        var materialized = await MaterializeInventoryPostingAsync(line);
                        if (!materialized.IsSuccess)
                        {
                            await transaction.RollbackAsync();
                            return ServiceResult<ConfirmBranchReceiptResultDto>.Failure(
                                materialized.Message,
                                errorCode: materialized.ErrorCode ?? BranchReceiptErrorCodes.ConversionFailed);
                        }
                    }

                    if (line.ReceivedBaseQuantity < 0
                        || line.RejectedBaseQuantity < 0
                        || line.ReceivedBaseQuantity + line.RejectedBaseQuantity <= 0)
                    {
                        await transaction.RollbackAsync();
                        return ServiceResult<ConfirmBranchReceiptResultDto>.Failure(
                            "Số lượng chấp nhận/loại bỏ không hợp lệ.",
                            errorCode: BranchReceiptErrorCodes.QuantityInvalid);
                    }

                    if (line.BaseUnitCostSnapshot <= 0
                        || (line.ReceivedBaseQuantity > 0 && line.LineTotalCost <= 0))
                    {
                        await transaction.RollbackAsync();
                        return ServiceResult<ConfirmBranchReceiptResultDto>.Failure(
                            "Dữ liệu chi phí không đầy đủ; giá nhập phải lớn hơn 0.",
                            errorCode: BranchReceiptErrorCodes.ReceiptCostIncomplete);
                    }

                    if (line.InventoryTransactionId.HasValue)
                    {
                        await transaction.RollbackAsync();
                        return ServiceResult<ConfirmBranchReceiptResultDto>.Failure(
                            "Dòng đã post — không post lại.",
                            errorCode: BranchReceiptErrorCodes.IdempotencyKeyReused);
                    }

                    if (line.PurchaseOrderLineId.HasValue)
                    {
                        if (_purchaseOrders == null)
                        {
                            await transaction.RollbackAsync();
                            return ServiceResult<ConfirmBranchReceiptResultDto>.Failure(
                                "Dịch vụ đơn mua hàng chưa được cấu hình.",
                                errorCode: BranchReceiptErrorCodes.ConfirmFailed);
                        }
                        var poValidation = await _purchaseOrders.ValidateReceiptLineAsync(receipt, line);
                        if (!poValidation.IsSuccess)
                        {
                            await transaction.RollbackAsync();
                            return ServiceResult<ConfirmBranchReceiptResultDto>.Failure(
                                poValidation.Message,
                                errorCode: BranchReceiptErrorCodes.RestockOverReceiptNotAllowed);
                        }
                    }
                }

                // RestockFulfillmentPosting is the only authority for actual fulfilled quantity.
                // Each source line is idempotent and overfill is checked while the request is locked.
                var requestUpdates = new Dictionary<int, RestockFulfillmentPostingResult>();
                foreach (var line in receipt.Lines.OrderBy(l => l.BranchReceiptLineId))
                {
                    if (line.ReceivedBaseQuantity > 0 && line.RestockRequestId.HasValue)
                    {
                        var posting = await _fulfillmentPostingService.RegisterAsync(new RegisterRestockFulfillmentPostingCommand
                        {
                            RestockRequestId = line.RestockRequestId.Value,
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

                        requestUpdates[line.RestockRequestId!.Value] = posting.Data;
                    }

                    if (line.PurchaseOrderLineId.HasValue)
                    {
                        var poPosting = await _purchaseOrders!.RegisterReceiptPostingAsync(receipt, line, actorStaffId);
                        if (!poPosting.IsSuccess)
                        {
                            await transaction.RollbackAsync();
                            return ServiceResult<ConfirmBranchReceiptResultDto>.Failure(
                                poPosting.Message,
                                errorCode: BranchReceiptErrorCodes.ConfirmFailed);
                        }
                    }
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
                    if (line.ReceivedBaseQuantity <= 0)
                        continue;

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
                            "Dòng nhận thiếu định danh nguyên liệu hoặc bán thành phẩm.",
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
                    if (line.ReceivedBaseQuantity <= 0)
                        continue;

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
                        SourceBranchReceiptLineId = line.BranchReceiptLineId,
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
                foreach (var line in receipt.Lines)
                {
                    if (!inventoryByLine.TryGetValue(line.BranchReceiptLineId, out var inv))
                        continue;
                    var eval = await _stockAlertService.EvaluateStoreInventoryItemAsync(
                        inv.StoreInventoryId,
                        "BRANCH_RECEIPT_CONFIRM");
                    if (!eval.IsSuccess)
                    {
                        await transaction.RollbackAsync();
                        _context.ChangeTracker.Clear();
                        return ServiceResult<ConfirmBranchReceiptResultDto>.Failure(
                            eval.Message ?? "Không cập nhật được cảnh báo tồn kho; toàn bộ xác nhận đã rollback.",
                            errorCode: BranchReceiptErrorCodes.ConfirmFailed);
                    }
                }

                await transaction.CommitAsync();

                _logger.LogInformation(
                    "[BranchReceipt] CONFIRMED Id={Id} Store={Store} TxCount={Count}",
                    receipt.BranchReceiptId, receipt.StoreId, createdTxIds.Count);

                return ServiceResult<ConfirmBranchReceiptResultDto>.Success(new ConfirmBranchReceiptResultDto
                {
                    BranchReceiptId = receipt.BranchReceiptId,
                    ReceiptCode = receipt.ReceiptCode,
                    Status = BranchReceiptStatuses.Confirmed,
                    WasReplay = false,
                    AlertEvaluationFailed = false,
                    InventoryTransactionIds = createdTxIds,
                    RequestUpdates = requestUpdates
                        .Select(x => (x.Key, x.Value.RequestStatus, x.Value.FulfilledQuantity))
                        .ToList()
                }, "Đã xác nhận phiếu nhận và cập nhật tồn kho.");
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
            catch (DbUpdateConcurrencyException)
            {
                try { await transaction.RollbackAsync(); } catch { /* ignore */ }
                _context.ChangeTracker.Clear();
                return ServiceResult<ConfirmBranchReceiptResultDto>.Failure(
                    "Phiếu nhận đã được người khác cập nhật. Vui lòng tải lại.",
                    errorCode: BranchReceiptErrorCodes.ResourceChanged);
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
                storeId, actorStaffId, actorStoreId, roleNames, mutation: false);
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
                storeId, actorStaffId, actorStoreId, roleNames, mutation: false);
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

        private async Task<PurchaseOrderReceiptDraftDto> MapPurchaseOrderDraftAsync(int branchReceiptId)
        {
            var receipt = await _context.BranchReceipts.AsNoTracking()
                .Include(x => x.Store)
                .Include(x => x.Supplier)
                .Include(x => x.Lines)
                .SingleAsync(x => x.BranchReceiptId == branchReceiptId);
            var order = await _context.PurchaseOrders.AsNoTracking()
                .Include(x => x.Lines).ThenInclude(x => x.Ingredient)
                .Include(x => x.Lines).ThenInclude(x => x.PackageUnitSnapshot)
                .Include(x => x.Lines).ThenInclude(x => x.ProcurementUnit)
                .Include(x => x.Lines).ThenInclude(x => x.InventoryBaseUnit)
                .Include(x => x.Lines).ThenInclude(x => x.ReceiptPostings)
                .SingleAsync(x => x.PurchaseOrderId == receipt.PurchaseOrderId);
            var savedByPoLine = receipt.Lines
                .Where(x => x.PurchaseOrderLineId.HasValue)
                .ToDictionary(x => x.PurchaseOrderLineId!.Value);

            return new PurchaseOrderReceiptDraftDto
            {
                BranchReceiptId = receipt.BranchReceiptId,
                PurchaseOrderId = order.PurchaseOrderId,
                PurchaseOrderCode = order.Code,
                StoreId = order.StoreId,
                StoreName = receipt.Store.Name,
                SupplierId = order.SupplierId,
                SupplierName = receipt.Supplier?.Name ?? $"NCC #{order.SupplierId}",
                ReceiptCode = receipt.ReceiptCode,
                RowVersion = Convert.ToBase64String(receipt.RowVersion ?? Array.Empty<byte>()),
                ReferenceNumber = receipt.ReferenceNumber,
                Notes = receipt.Notes,
                Lines = order.Lines
                    .Select(x =>
                    {
                        savedByPoLine.TryGetValue(x.PurchaseOrderLineId, out var saved);
                        var accepted = x.ReceiptPostings.Sum(p => p.AcceptedBaseQuantity);
                        var acceptedProcurement = x.ReceiptPostings.Sum(
                            p => p.AcceptedProcurementQuantity ?? 0m);
                        return new PurchaseOrderReceiptDraftLineDto
                        {
                            PurchaseMode = x.PurchaseMode,
                            PurchaseOrderLineId = x.PurchaseOrderLineId,
                            RestockRequestId = x.RestockRequestId,
                            RestockReferenceCode = x.RestockRequest != null ? x.RestockRequest.ReferenceCode : null,
                            IngredientId = x.IngredientId,
                            IngredientName = x.Ingredient.Name,
                            BaseUnitName = x.InventoryBaseUnit?.Name ?? string.Empty,
                            PackageUnitName = x.PackageUnitSnapshot?.Name ?? string.Empty,
                            PackageQuantitySnapshot = x.PackageQuantitySnapshot,
                            PackagePriceSnapshot = x.PackagePriceSnapshot,
                            OrderedBaseQuantity = x.OrderedBaseQuantity,
                            PreviouslyAcceptedBaseQuantity = accepted,
                            ClosedRemainingQuantity = x.ClosedRemainingQuantity,
                            RemainingBaseQuantity = Math.Max(0m, x.OrderedBaseQuantity - accepted - x.ClosedRemainingQuantity),
                            OrderedProcurementQuantity = x.OrderedProcurementQuantity,
                            PreviouslyAcceptedProcurementQuantity = x.OrderedProcurementQuantity.HasValue
                                ? acceptedProcurement
                                : null,
                            RemainingProcurementQuantity = x.OrderedProcurementQuantity.HasValue
                                ? Math.Max(0m, x.OrderedProcurementQuantity.Value - acceptedProcurement)
                                : null,
                            ProcurementUnitName = x.ProcurementUnit?.Name,
                            ActualReceivedQuantity = saved?.InputQuantity,
                            RejectedQuantity = saved == null
                                ? 0m
                                : saved.PurchaseMode == PurchaseMode.Loose
                                    ? saved.RejectedProcurementQuantity.GetValueOrDefault()
                                    : saved.InputQuantity <= 0
                                        || saved.ReceivedBaseQuantity + saved.RejectedBaseQuantity <= 0
                                            ? 0m
                                            : Math.Round(
                                                saved.InputQuantity * saved.RejectedBaseQuantity
                                                    / (saved.ReceivedBaseQuantity + saved.RejectedBaseQuantity),
                                                3,
                                                MidpointRounding.AwayFromZero),
                            RejectionReason = saved?.RejectionReason,
                            RejectionIssueType = saved?.RejectionIssueType
                        };
                    })
                    .Where(x => x.RemainingBaseQuantity > 0 || savedByPoLine.ContainsKey(x.PurchaseOrderLineId))
                    .OrderBy(x => x.PurchaseOrderLineId)
                    .ToList()
            };
        }

        private async Task<ServiceResult<BranchReceiptLine>> BuildPurchaseOrderReceiptLineAsync(
            BranchReceipt receipt,
            PurchaseOrderLine poLine,
            SavePurchaseOrderReceiptDraftLineRequest input)
        {
            var actual = input.ActualReceivedQuantity.GetValueOrDefault();
            if (actual <= 0)
                return ServiceResult<BranchReceiptLine>.Failure(
                    "Số lượng Nhà cung cấp giao phải lớn hơn 0.",
                    errorCode: BranchReceiptErrorCodes.ActualReceivedNotPositive);
            if (input.RejectedQuantity < 0)
                return ServiceResult<BranchReceiptLine>.Failure(
                    "Số lượng từ chối không được âm.",
                    errorCode: BranchReceiptErrorCodes.RejectedQuantityNegative);
            if (input.RejectedQuantity > actual)
                return ServiceResult<BranchReceiptLine>.Failure(
                    "Số lượng từ chối không được vượt số lượng Nhà cung cấp giao.",
                    errorCode: BranchReceiptErrorCodes.RejectedExceedsActualReceived);
            if (input.RejectedQuantity > 0
                && (string.IsNullOrWhiteSpace(input.RejectionReason)
                    || string.IsNullOrWhiteSpace(input.RejectionIssueType)))
                return ServiceResult<BranchReceiptLine>.Failure(
                    "Hàng bị từ chối phải có loại sự cố và lý do.",
                    errorCode: BranchReceiptErrorCodes.RejectionReasonRequired);
            if (!string.IsNullOrWhiteSpace(input.RejectionIssueType)
                && !SupplierReceiptIssueTypes.All.Contains(input.RejectionIssueType))
                return ServiceResult<BranchReceiptLine>.Failure(
                    "Loại sự cố hàng bị từ chối không hợp lệ.",
                    errorCode: BranchReceiptErrorCodes.RejectionReasonRequired);

            decimal actualBaseQuantity;
            decimal rejectedBase;
            decimal acceptedBase;
            decimal? receivedProcurement = null;
            decimal? rejectedProcurement = null;
            decimal? acceptedProcurement = null;
            decimal? conversionFactor = null;
            if (poLine.PurchaseMode == PurchaseMode.Loose)
            {
                if (!poLine.OrderedProcurementQuantity.HasValue
                    || !poLine.ProcurementUnitId.HasValue
                    || poLine.UnitPricePerProcurementUnit <= 0m)
                {
                    return ServiceResult<BranchReceiptLine>.Failure(
                        "Dòng mua rời thiếu số lượng, đơn vị hoặc đơn giá mua hàng.",
                        errorCode: BranchReceiptErrorCodes.ReceiptCostIncomplete);
                }

                receivedProcurement = actual;
                rejectedProcurement = input.RejectedQuantity;
                acceptedProcurement = ProcurementPurchaseMath.GetAcceptedProcurementQuantity(
                    receivedProcurement.Value,
                    rejectedProcurement.Value);
                actualBaseQuantity = 0m;
                rejectedBase = 0m;
                acceptedBase = 0m;
            }
            else if (poLine.OrderedProcurementQuantity.HasValue
                && poLine.PackageCount > 0
                && poLine.ProcurementUnitId.HasValue)
            {
                var procurementPerPack = poLine.PackSizeProcurementQuantity
                    ?? poLine.OrderedProcurementQuantity.Value / poLine.PackageCount.Value;
                receivedProcurement = actual * procurementPerPack;
                rejectedProcurement = Math.Round(
                    receivedProcurement.Value * input.RejectedQuantity / actual,
                    3,
                    MidpointRounding.AwayFromZero);
                acceptedProcurement = receivedProcurement.Value - rejectedProcurement.Value;
                actualBaseQuantity = 0m;
                rejectedBase = 0m;
                acceptedBase = 0m;
            }
            else
            {
                var actualBase = await _unitConversion.ConvertAsync(
                    poLine.IngredientId,
                    actual * poLine.PackageQuantitySnapshot!.Value,
                    poLine.PackageUnitIdSnapshot!.Value,
                    poLine.Ingredient.BaseUnitId);
                if (!actualBase.IsSuccess || actualBase.Data <= 0)
                    return ServiceResult<BranchReceiptLine>.Failure(
                        actualBase.Message ?? "Không quy đổi được số lượng thực nhận.",
                        errorCode: BranchReceiptErrorCodes.ConversionFailed);
                actualBaseQuantity = actualBase.Data;
                rejectedBase = Math.Round(
                    actualBase.Data * input.RejectedQuantity / actual,
                    3,
                    MidpointRounding.AwayFromZero);
                acceptedBase = actualBase.Data - rejectedBase;
            }
            var acceptedBefore = poLine.ReceiptPostings.Sum(x => x.AcceptedBaseQuantity);
            var remaining = receivedProcurement.HasValue
                ? Math.Max(0m, poLine.OrderedProcurementQuantity!.Value
                    - poLine.ReceiptPostings.Sum(x => x.AcceptedProcurementQuantity ?? 0m)
                    - poLine.ClosedProcurementQuantity)
                : Math.Max(0m, poLine.OrderedBaseQuantity - acceptedBefore - poLine.ClosedRemainingQuantity);
            if (receivedProcurement.HasValue
                ? acceptedProcurement > remaining
                : acceptedBase > remaining)
                return ServiceResult<BranchReceiptLine>.Failure(
                    $"Số lượng chấp nhận vượt phần đơn đặt hàng còn phải giao {remaining:N3}.",
                    errorCode: BranchReceiptErrorCodes.ReceiptExceedsRemaining);

            var actualLineTotal = poLine.PurchaseMode == PurchaseMode.Loose
                ? Math.Round(
                    actual * poLine.UnitPricePerProcurementUnit!.Value,
                    2,
                    MidpointRounding.AwayFromZero)
                : Math.Round(
                    actual * (poLine.UnitPricePerPackage ?? poLine.PackagePriceSnapshot)!.Value,
                    2,
                    MidpointRounding.AwayFromZero);
            var baseUnitCost = receivedProcurement.HasValue
                ? 0m
                : Math.Round(actualLineTotal / actualBaseQuantity, 4, MidpointRounding.AwayFromZero);
            var acceptedLineTotal = receivedProcurement.HasValue
                ? Math.Round(
                    actualLineTotal * acceptedProcurement.GetValueOrDefault() / receivedProcurement.Value,
                    2,
                    MidpointRounding.AwayFromZero)
                : Math.Round(acceptedBase * baseUnitCost, 2, MidpointRounding.AwayFromZero);
            return ServiceResult<BranchReceiptLine>.Success(new BranchReceiptLine
            {
                PurchaseMode = poLine.PurchaseMode,
                PurchaseOrderLineId = poLine.PurchaseOrderLineId,
                RestockRequestId = poLine.RestockRequestId,
                IngredientId = poLine.IngredientId,
                InputQuantity = actual,
                InputUnitId = poLine.PurchaseMode == PurchaseMode.Loose
                    ? poLine.ProcurementUnitId!.Value
                    : poLine.PackageUnitIdSnapshot!.Value,
                ReceivedBaseQuantity = acceptedBase,
                RejectedBaseQuantity = rejectedBase,
                ReceivedPackQuantity = poLine.PurchaseMode == PurchaseMode.Packaged ? actual : null,
                AcceptedPackQuantity = poLine.PurchaseMode == PurchaseMode.Packaged
                    ? actual - input.RejectedQuantity
                    : null,
                ReceivedProcurementQuantity = receivedProcurement,
                RejectedProcurementQuantity = rejectedProcurement,
                AcceptedProcurementQuantity = acceptedProcurement,
                InventoryPostingBaseQuantity = receivedProcurement.HasValue ? null : acceptedBase,
                ProcurementUnitId = poLine.ProcurementUnitId,
                InventoryBaseUnitId = poLine.InventoryBaseUnitId ?? poLine.Ingredient.BaseUnitId,
                ProcurementToInventoryFactor = conversionFactor,
                RejectionReason = string.IsNullOrWhiteSpace(input.RejectionReason) ? null : input.RejectionReason.Trim(),
                RejectionIssueType = string.IsNullOrWhiteSpace(input.RejectionIssueType) ? null : input.RejectionIssueType.Trim(),
                BaseUnitId = poLine.Ingredient.BaseUnitId,
                SupplierId = receipt.SupplierId,
                IngredientSupplierId = poLine.IngredientSupplierId,
                ActualPackagePrice = poLine.PurchaseMode == PurchaseMode.Loose
                    ? poLine.UnitPricePerProcurementUnit
                    : poLine.UnitPricePerPackage ?? poLine.PackagePriceSnapshot,
                PackageQuantitySnapshot = poLine.PurchaseMode == PurchaseMode.Packaged
                    ? poLine.PackageQuantitySnapshot
                    : null,
                PackageUnitIdSnapshot = poLine.PurchaseMode == PurchaseMode.Packaged
                    ? poLine.PackageUnitIdSnapshot
                    : null,
                BaseUnitCostSnapshot = baseUnitCost,
                LineTotalCost = acceptedLineTotal,
                CreatedAt = DateTime.UtcNow
            });
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

            if (input.ActualReceivedQuantity <= 0)
            {
                return ServiceResult<(CreateBranchReceiptLineInput, RestockRequest, decimal, int, decimal, decimal)>.Failure(
                    "Số lượng thực nhận phải lớn hơn 0.",
                    errorCode: BranchReceiptErrorCodes.ActualReceivedNotPositive);
            }

            if (input.RejectedQuantity < 0)
            {
                return ServiceResult<(CreateBranchReceiptLineInput, RestockRequest, decimal, int, decimal, decimal)>.Failure(
                    "Số lượng loại bỏ không được âm.",
                    errorCode: BranchReceiptErrorCodes.RejectedQuantityNegative);
            }

            if (input.RejectedQuantity > input.ActualReceivedQuantity)
            {
                return ServiceResult<(CreateBranchReceiptLineInput, RestockRequest, decimal, int, decimal, decimal)>.Failure(
                    "Số lượng loại bỏ không được vượt số lượng thực nhận.",
                    errorCode: BranchReceiptErrorCodes.RejectedExceedsActualReceived);
            }

            if (input.RejectedQuantity > 0
                && (string.IsNullOrWhiteSpace(input.RejectionReason)
                    || string.IsNullOrWhiteSpace(input.RejectionIssueType)))
            {
                return ServiceResult<(CreateBranchReceiptLineInput, RestockRequest, decimal, int, decimal, decimal)>.Failure(
                    "Hàng bị loại phải có loại sự cố và lý do.",
                    errorCode: BranchReceiptErrorCodes.RejectionReasonRequired);
            }

            if (!string.IsNullOrWhiteSpace(input.RejectionIssueType)
                && !SupplierReceiptIssueTypes.All.Contains(input.RejectionIssueType))
            {
                return ServiceResult<(CreateBranchReceiptLineInput, RestockRequest, decimal, int, decimal, decimal)>.Failure(
                    "Loại sự cố hàng bị loại không hợp lệ.",
                    errorCode: BranchReceiptErrorCodes.RejectionReasonRequired);
            }

            input.RejectionReason = string.IsNullOrWhiteSpace(input.RejectionReason)
                ? null
                : input.RejectionReason.Trim();
            if (input.RejectionReason?.Length > 500)
            {
                return ServiceResult<(CreateBranchReceiptLineInput, RestockRequest, decimal, int, decimal, decimal)>.Failure(
                    "Lý do loại bỏ không được vượt quá 500 ký tự.",
                    errorCode: BranchReceiptErrorCodes.RejectionReasonRequired);
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
                    "Chi nhánh không khớp yêu cầu.",
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
                if (minimumPackages > 0 && input.ActualReceivedQuantity < minimumPackages)
                {
                    return ServiceResult<(CreateBranchReceiptLineInput, RestockRequest, decimal, int, decimal, decimal)>.Failure(
                        $"Số gói nhận phải đạt mức tối thiểu {minimumPackages:N0} gói.",
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
                ? input.ActualReceivedQuantity * input.PackageQuantity!.Value
                : input.ActualReceivedQuantity;
            var physicalUnitId = hasPackageSnapshot
                ? input.PackageUnitId.GetValueOrDefault(input.InputUnitId)
                : input.InputUnitId;

            if (physicalUnitId <= 0)
            {
                return ServiceResult<(CreateBranchReceiptLineInput, RestockRequest, decimal, int, decimal, decimal)>.Failure(
                    "Đơn vị nội dung gói mua không hợp lệ.",
                    errorCode: BranchReceiptErrorCodes.QuantityInvalid);
            }

            if (request.RequestedProcurementQuantity.HasValue
                && request.RequestedProcurementQuantity.Value > 0
                && request.ProcurementUnitId.HasValue)
            {
                var procurementBaseUnitId = request.IngredientId.HasValue
                    ? request.Ingredient!.BaseUnitId
                    : request.PreparedItem?.BaseUnitId;
                if (!procurementBaseUnitId.HasValue)
                {
                    return ServiceResult<(CreateBranchReceiptLineInput, RestockRequest, decimal, int, decimal, decimal)>.Failure(
                        "Không xác định được đơn vị tồn kho đích của dòng mua hàng.",
                        errorCode: BranchReceiptErrorCodes.IdentityMismatch);
                }

                if (input.ActualPackagePrice <= 0)
                {
                    return ServiceResult<(CreateBranchReceiptLineInput, RestockRequest, decimal, int, decimal, decimal)>.Failure(
                        "Giá gói thực tế là bắt buộc và phải lớn hơn 0.",
                        errorCode: BranchReceiptErrorCodes.ReceiptCostIncomplete);
                }

                var acceptedInputQuantity = input.ActualReceivedQuantity - input.RejectedQuantity;
                var procurementLineTotal = Math.Round(
                    Math.Max(0m, acceptedInputQuantity) * input.ActualPackagePrice,
                    2,
                    MidpointRounding.AwayFromZero);
                return ServiceResult<(CreateBranchReceiptLineInput, RestockRequest, decimal, int, decimal, decimal)>.Success(
                    (input, request, 0m, procurementBaseUnitId.Value, 0m, procurementLineTotal));
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
                    "Số lượng tồn kho sau quy đổi phải lớn hơn 0.",
                    errorCode: BranchReceiptErrorCodes.QuantityInvalid);
            }

            var rejectedBaseQty = Math.Round(
                baseQty * input.RejectedQuantity / input.ActualReceivedQuantity,
                3,
                MidpointRounding.AwayFromZero);
            var acceptedBaseQty = baseQty - rejectedBaseQty;
            if (acceptedBaseQty < 0)
            {
                return ServiceResult<(CreateBranchReceiptLineInput, RestockRequest, decimal, int, decimal, decimal)>.Failure(
                    "Số lượng chấp nhận sau quy đổi không hợp lệ.",
                    errorCode: BranchReceiptErrorCodes.RejectedExceedsActualReceived);
            }

            // From this point the DTO carries the canonical rejected quantity in base units.
            input.RejectedQuantity = rejectedBaseQty;

            // Cost snapshot: fail-closed. Prefer explicit package price; compute base unit cost.
            if (input.ActualPackagePrice <= 0)
            {
                return ServiceResult<(CreateBranchReceiptLineInput, RestockRequest, decimal, int, decimal, decimal)>.Failure(
                    "Giá gói thực tế là bắt buộc và phải lớn hơn 0.",
                    errorCode: BranchReceiptErrorCodes.ReceiptCostIncomplete);
            }

            decimal unitCost;
            decimal lineTotal;

            if (hasPackageSnapshot)
            {
                // Package path: InputQuantity packages × package price = line total;
                // each package has PackageQuantity in package unit → convert content to base if needed.
                // Spec D5: InputQuantity packages × package content → base; cost from ActualPackagePrice.
                var actualLineTotal = Math.Round(input.ActualReceivedQuantity * input.ActualPackagePrice, 2, MidpointRounding.AwayFromZero);
                unitCost = baseQty > 0
                    ? Math.Round(actualLineTotal / baseQty, 4, MidpointRounding.AwayFromZero)
                    : 0m;
                lineTotal = Math.Round(acceptedBaseQty * unitCost, 2, MidpointRounding.AwayFromZero);
            }
            else
            {
                // Unit price already in input unit: total = input qty * price; unit cost in base.
                var actualLineTotal = Math.Round(input.ActualReceivedQuantity * input.ActualPackagePrice, 2, MidpointRounding.AwayFromZero);
                unitCost = baseQty > 0
                    ? Math.Round(actualLineTotal / baseQty, 4, MidpointRounding.AwayFromZero)
                    : 0m;
                lineTotal = Math.Round(acceptedBaseQty * unitCost, 2, MidpointRounding.AwayFromZero);
            }

            if (unitCost <= 0 || (acceptedBaseQty > 0 && lineTotal <= 0))
            {
                return ServiceResult<(CreateBranchReceiptLineInput, RestockRequest, decimal, int, decimal, decimal)>.Failure(
                    "Không thể ghi nhận lớp giá vốn có giá bằng 0.",
                    errorCode: BranchReceiptErrorCodes.ReceiptCostIncomplete);
            }

            return ServiceResult<(CreateBranchReceiptLineInput, RestockRequest, decimal, int, decimal, decimal)>.Success(
                (input, request, acceptedBaseQty, baseUnitId, unitCost, lineTotal));
        }

        private async Task<ServiceResult<DirectProcurementSnapshot?>> BuildDirectProcurementSnapshotAsync(
            RestockRequest request,
            CreateBranchReceiptLineInput input,
            decimal _,
            int inventoryBaseUnitId)
        {
            if (!request.RequestedProcurementQuantity.HasValue
                || request.RequestedProcurementQuantity.Value <= 0
                || !request.ProcurementUnitId.HasValue)
            {
                return ServiceResult<DirectProcurementSnapshot?>.Success(null);
            }

            var procurementUnitId = request.ProcurementUnitId.Value;
            decimal receivedProcurementQuantity;
            if (input.PackageQuantity.HasValue && input.PackageQuantity.Value > 0)
            {
                var packageUnitId = input.PackageUnitId.GetValueOrDefault(input.InputUnitId);
                var packageContent = input.ActualReceivedQuantity * input.PackageQuantity.Value;
                var converted = await ConvertToProcurementUnitAsync(
                    request,
                    packageContent,
                    packageUnitId,
                    procurementUnitId);
                if (!converted.IsSuccess || converted.Data <= 0)
                    return ServiceResult<DirectProcurementSnapshot?>.Failure(
                        converted.Message ?? "Không quy đổi được số lượng theo đơn vị mua hàng.",
                        errorCode: BranchReceiptErrorCodes.ConversionFailed);
                receivedProcurementQuantity = converted.Data;
            }
            else if (input.InputUnitId == procurementUnitId)
            {
                receivedProcurementQuantity = input.ActualReceivedQuantity;
            }
            else
            {
                var converted = await ConvertToProcurementUnitAsync(
                    request,
                    input.ActualReceivedQuantity,
                    input.InputUnitId,
                    procurementUnitId);
                if (!converted.IsSuccess || converted.Data <= 0)
                    return ServiceResult<DirectProcurementSnapshot?>.Failure(
                        converted.Message ?? "Không quy đổi được số lượng theo đơn vị mua hàng.",
                        errorCode: BranchReceiptErrorCodes.ConversionFailed);
                receivedProcurementQuantity = converted.Data;
            }

            if (receivedProcurementQuantity <= 0)
                return ServiceResult<DirectProcurementSnapshot?>.Failure(
                    "Dữ liệu số lượng mua hàng sau quy đổi không hợp lệ.",
                    errorCode: BranchReceiptErrorCodes.QuantityInvalid);

            var rejectedProcurementQuantity = Math.Round(
                receivedProcurementQuantity * input.RejectedQuantity / input.ActualReceivedQuantity,
                3,
                MidpointRounding.AwayFromZero);
            var acceptedProcurementQuantity = receivedProcurementQuantity - rejectedProcurementQuantity;
            if (rejectedProcurementQuantity < 0 || acceptedProcurementQuantity < 0)
                return ServiceResult<DirectProcurementSnapshot?>.Failure(
                    "Số lượng mua hàng bị từ chối không hợp lệ.",
                    errorCode: BranchReceiptErrorCodes.RejectedExceedsActualReceived);

            return ServiceResult<DirectProcurementSnapshot?>.Success(new DirectProcurementSnapshot
            {
                ReceivedQuantity = receivedProcurementQuantity,
                RejectedQuantity = rejectedProcurementQuantity,
                AcceptedQuantity = acceptedProcurementQuantity,
                ProcurementUnitId = procurementUnitId,
                InventoryBaseUnitId = inventoryBaseUnitId
            });
        }

        private async Task<ServiceResult<decimal>> ConvertToProcurementUnitAsync(
            RestockRequest request,
            decimal quantity,
            int fromUnitId,
            int toUnitId)
        {
            if (fromUnitId == toUnitId)
                return ServiceResult<decimal>.Success(quantity);

            if (request.IngredientId.HasValue)
                return await _unitConversion.ConvertAsync(
                    request.IngredientId.Value,
                    quantity,
                    fromUnitId,
                    toUnitId);

            return await _physicalConversion.ConvertAsync(quantity, fromUnitId, toUnitId);
        }

        private async Task<ServiceResult<decimal>> GetProcurementToInventoryFactorAsync(
            int? ingredientId,
            int? preparedItemId,
            int procurementUnitId,
            int inventoryBaseUnitId)
        {
            if (procurementUnitId <= 0 || inventoryBaseUnitId <= 0)
                return ServiceResult<decimal>.Failure(
                    "Thiếu đơn vị mua hàng hoặc đơn vị tồn kho.",
                    errorCode: BranchReceiptErrorCodes.ConversionFailed);

            if (ingredientId.HasValue)
                return await _unitConversion.ConvertAsync(
                    ingredientId.Value,
                    1m,
                    procurementUnitId,
                    inventoryBaseUnitId);

            if (preparedItemId.HasValue)
                return await _physicalConversion.ConvertAsync(
                    1m,
                    procurementUnitId,
                    inventoryBaseUnitId);

            return ServiceResult<decimal>.Failure(
                "Dòng mua hàng không có định danh nguyên liệu hoặc bán thành phẩm.",
                errorCode: BranchReceiptErrorCodes.IdentityMismatch);
        }

        private async Task<ServiceResult> MaterializeInventoryPostingAsync(BranchReceiptLine line)
        {
            if (!line.AcceptedProcurementQuantity.HasValue)
                return ServiceResult.Success();
            if (!line.ProcurementUnitId.HasValue)
                return ServiceResult.Failure(
                    "Phiếu nhận thiếu đơn vị mua hàng.",
                    errorCode: BranchReceiptErrorCodes.ConversionFailed);

            if (line.InventoryPostingBaseQuantity.HasValue
                && line.ProcurementToInventoryFactor.GetValueOrDefault() > 0)
                return ServiceResult.Success();

            var inventoryBaseUnitId = line.InventoryBaseUnitId ?? line.BaseUnitId;
            var factorResult = await GetProcurementToInventoryFactorAsync(
                line.IngredientId,
                line.PreparedItemId,
                line.ProcurementUnitId.Value,
                inventoryBaseUnitId);
            if (!factorResult.IsSuccess || factorResult.Data <= 0)
                return ServiceResult.Failure(
                    factorResult.Message ?? "Không quy đổi được đơn vị mua hàng sang đơn vị tồn kho.",
                    errorCode: BranchReceiptErrorCodes.ConversionFailed);

            var factor = factorResult.Data;
            var receivedProcurement = line.ReceivedProcurementQuantity
                ?? line.AcceptedProcurementQuantity.Value
                    + line.RejectedProcurementQuantity.GetValueOrDefault();
            if (receivedProcurement <= 0)
                return ServiceResult.Failure(
                    "Số lượng mua hàng thực giao phải lớn hơn 0.",
                    errorCode: BranchReceiptErrorCodes.QuantityInvalid);

            var acceptedBase = Math.Round(
                line.AcceptedProcurementQuantity.Value * factor,
                3,
                MidpointRounding.AwayFromZero);
            var rejectedBase = Math.Round(
                line.RejectedProcurementQuantity.GetValueOrDefault() * factor,
                3,
                MidpointRounding.AwayFromZero);
            var receivedBase = acceptedBase + rejectedBase;
            if (receivedBase <= 0)
                return ServiceResult.Failure(
                    "Số lượng tồn kho sau quy đổi phải lớn hơn 0.",
                    errorCode: BranchReceiptErrorCodes.QuantityInvalid);

            var actualTotalCost = line.ActualPackagePrice.GetValueOrDefault() > 0
                ? Math.Round(
                    line.InputQuantity * line.ActualPackagePrice!.Value,
                    2,
                    MidpointRounding.AwayFromZero)
                : line.LineTotalCost;
            if (actualTotalCost <= 0)
                return ServiceResult.Failure(
                    "Dữ liệu chi phí không đầy đủ để xác nhận nhập kho.",
                    errorCode: BranchReceiptErrorCodes.ReceiptCostIncomplete);

            line.ReceivedBaseQuantity = acceptedBase;
            line.RejectedBaseQuantity = rejectedBase;
            line.InventoryPostingBaseQuantity = acceptedBase;
            line.InventoryBaseUnitId = inventoryBaseUnitId;
            line.ProcurementToInventoryFactor = factor;
            line.BaseUnitCostSnapshot = Math.Round(
                actualTotalCost / receivedBase,
                4,
                MidpointRounding.AwayFromZero);
            line.LineTotalCost = Math.Round(
                acceptedBase * line.BaseUnitCostSnapshot,
                2,
                MidpointRounding.AwayFromZero);
            return ServiceResult.Success();
        }

        private sealed class DirectProcurementSnapshot
        {
            public decimal ReceivedQuantity { get; init; }
            public decimal RejectedQuantity { get; init; }
            public decimal AcceptedQuantity { get; init; }
            public decimal? AcceptedBaseQuantity { get; init; }
            public int ProcurementUnitId { get; init; }
            public int InventoryBaseUnitId { get; init; }
            public decimal? ConversionFactor { get; init; }
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
                .Include(r => r.Lines).ThenInclude(l => l.RestockRequest)
                .FirstAsync(r => r.BranchReceiptId == id);
            return MapDetail(receipt);
        }

        private static BranchReceiptDetailDto MapDetail(BranchReceipt r)
        {
            var dto = new BranchReceiptDetailDto
            {
                BranchReceiptId = r.BranchReceiptId,
                PurchaseOrderId = r.PurchaseOrderId,
                ReceiptCode = r.ReceiptCode,
                ReceiptKey = r.ReceiptKey,
                Status = r.Status,
                RowVersion = Convert.ToBase64String(r.RowVersion ?? Array.Empty<byte>()),
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
                    PurchaseMode = l.PurchaseMode,
                    BranchReceiptLineId = l.BranchReceiptLineId,
                    PurchaseOrderLineId = l.PurchaseOrderLineId,
                    RestockRequestId = l.RestockRequestId,
                    RestockReferenceCode = l.RestockRequest?.ReferenceCode,
                    IngredientId = l.IngredientId,
                    PreparedItemId = l.PreparedItemId,
                    RecipeId = l.RecipeId,
                    InputQuantity = l.InputQuantity,
                    InputUnitId = l.InputUnitId,
                    InputUnitName = l.InputUnit?.Name,
                    ReceivedBaseQuantity = l.ReceivedBaseQuantity,
                    RejectedBaseQuantity = l.RejectedBaseQuantity,
                    ReceivedProcurementQuantity = l.ReceivedProcurementQuantity,
                    RejectedProcurementQuantity = l.RejectedProcurementQuantity,
                    AcceptedProcurementQuantity = l.AcceptedProcurementQuantity,
                    InventoryPostingBaseQuantity = l.InventoryPostingBaseQuantity,
                    ProcurementUnitId = l.ProcurementUnitId,
                    ProcurementUnitName = l.ProcurementUnit?.Name,
                    ProcurementToInventoryFactor = l.ProcurementToInventoryFactor,
                    RejectionReason = l.RejectionReason,
                    RejectionIssueType = l.RejectionIssueType,
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
            PurchaseOrderId = r.PurchaseOrderId,
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

        private static bool TryParseRequiredRowVersion(string? value, out byte[] rowVersion)
        {
            rowVersion = Array.Empty<byte>();
            if (string.IsNullOrWhiteSpace(value))
                return false;

            try
            {
                rowVersion = Convert.FromBase64String(value);
                return rowVersion.Length > 0;
            }
            catch (FormatException)
            {
                return false;
            }
        }

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
            if (roleNames.Contains(RoleConstants.SystemAdmin))
                return ServiceResult.Success();

            if (roleNames.Contains(RoleConstants.BusinessOwner))
                return ServiceResult.Success();

            if (!mutation && roleNames.Contains(RoleConstants.AccountantWarehouse))
                return ServiceResult.Success();

            if (roleNames.Contains(RoleConstants.AreaManager))
            {
                if (mutation)
                {
                    return ServiceResult.Failure(
                        "Quản lý vùng chỉ có quyền xem phiếu nhận.",
                        errorCode: BranchReceiptErrorCodes.Unauthorized);
                }
                return actorStaffId > 0
                       && await _scopeAuthorization.CanAccessStoreAsync(actorStaffId, storeId)
                    ? ServiceResult.Success()
                    : ServiceResult.Failure(
                        "Cửa hàng nằm ngoài phạm vi quản lý vùng.",
                        errorCode: BranchReceiptErrorCodes.Unauthorized);
            }

            var allowedBranchRole = roleNames.Contains(RoleConstants.StoreManager)
                                   || roleNames.Contains(RoleConstants.ShiftSupervisor);
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
            roles.Contains(RoleConstants.SystemAdmin)
            || roles.Contains(RoleConstants.BusinessOwner)
            || roles.Contains(RoleConstants.StoreManager)
            || roles.Contains(RoleConstants.ShiftSupervisor);

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

        private static ServiceResult<PurchaseOrderReceiptDraftDto> FailPurchaseOrderDraft(string message, string? code = null) =>
            ServiceResult<PurchaseOrderReceiptDraftDto>.Failure(message, errorCode: code);
    }
}
