using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.DTOs.Admin.RestockRequests;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Procurement;
using CafeChain.Models.Inventories.Stock;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Inventories
{
    public sealed class PurchaseOrderService : IPurchaseOrderService
    {
        private readonly AppDbContext _context;
        private readonly IUnitConversionService _conversion;
        private readonly IRestockAllocationService _allocations;
        private readonly IScopeAuthorizationService? _scopeAuthorization;
        private readonly IPurchaseAdviceFulfillmentService _purchaseAdviceFulfillment;

        public PurchaseOrderService(
            AppDbContext context,
            IUnitConversionService conversion,
            IRestockAllocationService allocations,
            IScopeAuthorizationService? scopeAuthorization = null,
            IPurchaseAdviceFulfillmentService? purchaseAdviceFulfillment = null)
        {
            _context = context;
            _conversion = conversion;
            _allocations = allocations;
            _scopeAuthorization = scopeAuthorization;
            _purchaseAdviceFulfillment = purchaseAdviceFulfillment
                ?? new PurchaseAdviceFulfillmentService(context);
        }

        public async Task<ServiceResult<PurchaseOrderDetailDto>> CreateDraftAsync(
            CreatePurchaseOrderRequest input,
            int actorStaffId,
            IReadOnlyCollection<string> roles)
        {
            if (!CanCreate(roles)) return Fail("Bạn không có quyền tạo đơn mua hàng.");
            if (input.StoreId <= 0 || input.SupplierId <= 0 || input.Lines.Count == 0)
                return Fail("Cửa hàng, nhà cung cấp và ít nhất một dòng hàng là bắt buộc.");
            if (input.Lines.Any(x =>
                    (x.PurchaseMode == PurchaseMode.Packaged
                        && (!ProcurementPurchaseMath.IsWholePackageCount(x.PackageCount)
                            || x.OrderedProcurementQuantity.HasValue))
                    || (x.PurchaseMode == PurchaseMode.Loose
                        && (x.PackageCount.HasValue || x.OrderedProcurementQuantity <= 0m))))
                return Fail("Mua đóng gói yêu cầu số gói phải là số nguyên; mua rời phải dùng trực tiếp số lượng kg/L và không được gửi số gói.");
            if (!await CanAccessStoreAsync(actorStaffId, input.StoreId))
                return Fail("Bạn không có quyền tạo đơn mua hàng cho cửa hàng này.");
            if (input.ExpectedDeliveryAtUtc.HasValue && input.ExpectedDeliveryAtUtc.Value < DateTime.UtcNow)
                return Fail("Ngày giao dự kiến không được trước ngày đặt hàng.");
            if (input.Lines.GroupBy(x => x.IngredientId).Any(x => x.Key <= 0 || x.Count() > 1))
                return Fail("Mỗi nguyên liệu chỉ được xuất hiện một lần trong đơn mua hàng.");
            if (input.Lines.Where(x => x.RestockRequestId.HasValue)
                .GroupBy(x => x.RestockRequestId!.Value)
                .Any(x => x.Count() > 1))
                return Fail("Mỗi yêu cầu nhập chỉ được liên kết một lần trong cùng đơn mua hàng.");

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var supplierStore = await _context.SupplierStores.AsNoTracking()
                    .AnyAsync(x => x.StoreId == input.StoreId && x.SupplierId == input.SupplierId && x.Active);
                if (!supplierStore) return Fail("Nhà cung cấp không hoạt động tại cửa hàng đã chọn.");

                var order = new PurchaseOrder
                {
                    Code = $"PO-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
                    StoreId = input.StoreId,
                    SupplierId = input.SupplierId,
                    Status = PurchaseOrderStatuses.Draft,
                    OrderDate = DateTime.UtcNow,
                    ExpectedDeliveryAtUtc = input.ExpectedDeliveryAtUtc,
                    CreatedByStaffId = actorStaffId,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow,
                    RowVersion = Guid.NewGuid().ToByteArray(),
                    Note = Trim(input.Note, 1000)
                };

                foreach (var requested in input.Lines)
                {
                    var offer = await _context.IngredientSuppliers.AsNoTracking()
                        .Include(x => x.Ingredient)
                        .Include(x => x.Supplier)
                        .Include(x => x.Unit)
                        .Include(x => x.LooseProcurementUnit)
                        .SingleOrDefaultAsync(x => x.IngredientSupplierId == requested.IngredientSupplierId);
                    if (offer == null || !offer.Active || offer.SupplierId != input.SupplierId
                        || offer.IngredientId != requested.IngredientId)
                        return Fail("Gói mua không khớp nhà cung cấp hoặc nguyên liệu.");
                    if (!offer.Supplier.Active || !offer.Ingredient.Active || !offer.Unit.Active)
                        return Fail("Nhà cung cấp, nguyên liệu hoặc đơn vị của gói mua không còn hoạt động.");

                    var demand = requested.RestockRequestId.HasValue
                        ? await _context.RestockRequests.AsNoTracking()
                            .SingleOrDefaultAsync(x => x.RestockRequestId == requested.RestockRequestId.Value)
                        : null;
                    if (demand?.ProcurementUnitId.HasValue == true
                        && requested.ProcurementUnitId.HasValue
                        && requested.ProcurementUnitId.Value != demand.ProcurementUnitId.Value)
                        return Fail("Đơn vị mua hàng của dòng đơn đặt hàng không khớp nhu cầu.");

                    var procurementUnitId = demand?.ProcurementUnitId ?? requested.ProcurementUnitId;
                    decimal? orderedProcurement;
                    decimal? procurementPerPack = null;
                    decimal? roundingSurplus = null;
                    decimal? packageCount = null;
                    decimal? unitPricePerPackage = null;
                    decimal? unitPricePerProcurement = null;
                    int sourceUnitId;
                    decimal sourceQuantity;

                    if (requested.PurchaseMode == PurchaseMode.Loose)
                    {
                        if (!offer.AllowsLoosePurchase
                            || offer.CurrentProcurementUnitPrice <= 0m
                            || !offer.LooseProcurementUnitId.HasValue
                            || procurementUnitId != offer.LooseProcurementUnitId)
                            return Fail("Nhà cung cấp chưa cho phép mua rời theo đúng đơn vị mua hàng của nhu cầu.");

                        orderedProcurement = requested.OrderedProcurementQuantity!.Value;
                        sourceUnitId = offer.LooseProcurementUnitId.Value;
                        sourceQuantity = orderedProcurement.Value;
                        unitPricePerProcurement = offer.CurrentProcurementUnitPrice;
                    }
                    else
                    {
                        if (!offer.PackageQuantity.HasValue || offer.PackageQuantity.Value <= 0m)
                            return Fail("Gói mua chưa cấu hình lượng trong gói.");
                        if (offer.CurrentPrice <= 0m)
                            return Fail("Gói mua chưa có giá hợp lệ.");
                        packageCount = requested.PackageCount!.Value;
                        if (packageCount < offer.MinimumOrderPackageCount.GetValueOrDefault())
                            return Fail($"Số gói đặt thấp hơn mức tối thiểu {offer.MinimumOrderPackageCount:N3}.");
                        sourceUnitId = offer.UnitId;
                        sourceQuantity = packageCount.Value * offer.PackageQuantity.Value;
                        unitPricePerPackage = offer.CurrentPrice;
                        orderedProcurement = null;
                    }

                    var converted = await _conversion.ConvertAsync(
                        offer.IngredientId,
                        sourceQuantity,
                        sourceUnitId,
                        offer.Ingredient.BaseUnitId);
                    if (!converted.IsSuccess || converted.Data <= 0)
                        return Fail(converted.Message ?? "Không quy đổi được số lượng đặt về đơn vị tồn kho.");

                    if (requested.PurchaseMode == PurchaseMode.Packaged && procurementUnitId.HasValue)
                    {
                        var procurementConverted = await _conversion.ConvertAsync(
                            offer.IngredientId,
                            sourceQuantity,
                            offer.UnitId,
                            procurementUnitId.Value);
                        if (!procurementConverted.IsSuccess || procurementConverted.Data <= 0)
                            return Fail(procurementConverted.Message ?? "Không quy đổi được số lượng đặt về đơn vị mua hàng.");
                        orderedProcurement = procurementConverted.Data;
                        procurementPerPack = orderedProcurement / packageCount;
                        if (demand?.RequestedProcurementQuantity is decimal requestedProcurement)
                            roundingSurplus = Math.Max(0m, orderedProcurement.Value - requestedProcurement);
                    }
                    else if (requested.PurchaseMode == PurchaseMode.Loose
                        && demand?.RequestedProcurementQuantity is decimal requestedProcurement)
                    {
                        var remainingProcurement = Math.Max(0m, requestedProcurement);
                        if (orderedProcurement > remainingProcurement)
                            return Fail($"Số lượng mua rời vượt {remainingProcurement:N3} đơn vị procurement còn lại.");
                    }

                    if (requested.RestockRequestId.HasValue)
                    {
                        var summary = await _allocations.GetSummaryAsync(requested.RestockRequestId.Value);
                        if (summary == null || summary.RemainingUnallocatedQuantity <= 0)
                            return Fail("Yêu cầu nhập không còn số lượng chưa phân bổ.");
                        decimal allocationQuantity;
                        if (requested.PurchaseMode == PurchaseMode.Packaged)
                        {
                            var packageBaseQuantity = converted.Data / packageCount!.Value;
                            if (!PurchasePackMath.TryPlan(
                                    summary.RemainingUnallocatedQuantity,
                                    packageBaseQuantity,
                                    out var packPlan))
                                return Fail("Không thể tính số gói cần đặt từ phần chưa phân bổ.");
                            if (packageCount < packPlan.PackageCount)
                                return Fail($"Cần ít nhất {packPlan.PackageCount} gói để phủ {summary.RemainingUnallocatedQuantity:N3} đơn vị cơ sở.");
                            allocationQuantity = packageCount == packPlan.PackageCount
                                ? packPlan.DemandCoveredBaseQuantity
                                : converted.Data;
                        }
                        else
                        {
                            if (converted.Data > summary.RemainingUnallocatedQuantity)
                                return Fail("Số lượng mua rời vượt phần nhu cầu chưa phân bổ.");
                            allocationQuantity = converted.Data;
                        }
                        var allocation = await _allocations.ValidateAllocationAsync(new RestockAllocationValidationRequest
                        {
                            RestockRequestId = requested.RestockRequestId.Value,
                            DestinationStoreId = input.StoreId,
                            IngredientId = requested.IngredientId,
                            AllocationQuantity = allocationQuantity,
                            ActorStaffId = actorStaffId,
                            ActorRoles = roles,
                            AllowOverallocationOverride = input.AllowOverallocationOverride,
                            OverrideReason = input.OverallocationOverrideReason,
                            RequestKey = order.Code
                        });
                        if (!allocation.IsSuccess) return Fail(allocation.Message ?? "Phân bổ đơn mua không hợp lệ.");
                    }

                    order.Lines.Add(new PurchaseOrderLine
                    {
                        PurchaseMode = requested.PurchaseMode,
                        RestockRequestId = requested.RestockRequestId,
                        IngredientId = requested.IngredientId,
                        IngredientSupplierId = requested.IngredientSupplierId,
                        PackageUnitIdSnapshot = requested.PurchaseMode == PurchaseMode.Packaged ? offer.UnitId : null,
                        PackageQuantitySnapshot = requested.PurchaseMode == PurchaseMode.Packaged ? offer.PackageQuantity : null,
                        PackagePriceSnapshot = unitPricePerPackage,
                        PackageCount = packageCount,
                        OrderedPackageCount = packageCount,
                        OrderedBaseQuantity = converted.Data,
                        OrderedPackQuantity = packageCount,
                        PackSizeProcurementQuantity = procurementPerPack,
                        ProcurementUnitId = procurementUnitId,
                        OrderedProcurementQuantity = orderedProcurement,
                        UnitPricePerPackage = unitPricePerPackage,
                        UnitPricePerProcurementUnit = unitPricePerProcurement,
                        RoundingSurplusProcurementQuantity = roundingSurplus,
                        InventoryBaseUnitId = offer.Ingredient.BaseUnitId,
                        ProcurementToInventoryFactor = null,
                        PromisedLeadTimeDaysSnapshot = offer.LeadTimeDays.GetValueOrDefault(),
                        Note = Trim(requested.Note, 500)
                    });
                }

                _context.PurchaseOrders.Add(order);
                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                return ServiceResult<PurchaseOrderDetailDto>.Success(await MapAsync(order.PurchaseOrderId), "Đã tạo đơn mua hàng nháp.");
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return Fail($"Không tạo được đơn mua hàng: {ex.Message}");
            }
        }

        public Task<ServiceResult<PurchaseOrderDetailDto>> ApproveAsync(
            int id, string rowVersion, int actorStaffId, IReadOnlyCollection<string> roles) =>
            TransitionAsync(id, rowVersion, PurchaseOrderStatuses.Draft, PurchaseOrderStatuses.Approved, actorStaffId, roles, CanApprove);

        public Task<ServiceResult<PurchaseOrderDetailDto>> MarkSentAsync(
            int id, string rowVersion, int actorStaffId, IReadOnlyCollection<string> roles) =>
            TransitionAsync(id, rowVersion, PurchaseOrderStatuses.Approved, PurchaseOrderStatuses.MarkedAsSent, actorStaffId, roles, CanSend);

        public async Task<ServiceResult<PurchaseOrderDetailDto>> CancelAsync(
            int id, string rowVersion, int actorStaffId, IReadOnlyCollection<string> roles, string reason)
        {
            if (!CanCancel(roles)) return Fail("Bạn không có quyền hủy đơn mua hàng.");
            if (string.IsNullOrWhiteSpace(reason)) return Fail("Lý do hủy là bắt buộc.");
            if (!TryParseRowVersion(rowVersion, out var expectedVersion))
                return Fail("Thiếu hoặc sai phiên bản dữ liệu.", BranchReceiptErrorCodes.ValidationRowVersionRequired);
            var order = await _context.PurchaseOrders.Include(x => x.Lines).ThenInclude(x => x.ReceiptPostings)
                .SingleOrDefaultAsync(x => x.PurchaseOrderId == id);
            if (order == null) return Fail("Không tìm thấy đơn mua hàng.");
            if (!await CanAccessStoreAsync(actorStaffId, order.StoreId))
                return Fail("Bạn không có quyền hủy đơn mua hàng của cửa hàng này.");
            if (order.Status is PurchaseOrderStatuses.Completed or PurchaseOrderStatuses.Cancelled
                || order.Lines.Any(x => x.ReceiptPostings.Count > 0))
                return Fail("Không thể hủy đơn đã nhận hàng hoặc đã kết thúc.");
            if (!RowVersionMatches(order.RowVersion, expectedVersion))
                return Fail("Đơn mua hàng đã được cập nhật bởi người khác. Vui lòng tải lại.", BranchReceiptErrorCodes.ResourceChanged);
            SetExpectedRowVersion(order, expectedVersion);
            order.Status = PurchaseOrderStatuses.Cancelled;
            order.CancelledAtUtc = DateTime.UtcNow;
            order.UpdatedAtUtc = DateTime.UtcNow;
            order.Note = Trim($"{order.Note}\nCANCEL: {reason.Trim()}", 1000);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Fail("Đơn mua hàng đã được cập nhật bởi người khác. Vui lòng tải lại.", BranchReceiptErrorCodes.ResourceChanged);
            }
            return ServiceResult<PurchaseOrderDetailDto>.Success(await MapAsync(id), "Đã hủy đơn mua hàng.");
        }

        public async Task<ServiceResult<PurchaseOrderDetailDto>> CloseLineRemainingAsync(
            ClosePurchaseOrderLineRemainingRequest input,
            int actorStaffId,
            IReadOnlyCollection<string> roles)
        {
            if (!CanCloseRemaining(roles))
                return Fail("Chỉ Chủ doanh nghiệp được đóng phần còn lại của đơn mua hàng.");
            if (input.PurchaseOrderLineId <= 0)
                return Fail("Dòng đơn mua hàng không hợp lệ.");
            if (string.IsNullOrWhiteSpace(input.Reason))
                return Fail("Lý do không yêu cầu giao bù là bắt buộc.");
            if (string.IsNullOrWhiteSpace(input.RequestKey) || input.RequestKey.Trim().Length > 100)
                return Fail("Khóa yêu cầu ổn định là bắt buộc và không được vượt quá 100 ký tự.", PurchaseAdviceErrorCodes.BackPostRequestKeyRequired);
            if (!TryParseRowVersion(input.RowVersion, out var expectedVersion))
                return Fail("Thiếu hoặc sai phiên bản dữ liệu.", BranchReceiptErrorCodes.ValidationRowVersionRequired);

            var requestKey = input.RequestKey.Trim();
            var payloadHash = PurchaseAdviceFulfillmentService.ComputeClosePayloadHash(
                input.PurchaseOrderLineId,
                input.RowVersion,
                input.Reason);

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (_context.Database.IsSqlServer())
                {
                    var lockResource = $"PurchaseOrderCloseRemaining:{requestKey}";
                    await _context.Database.ExecuteSqlInterpolatedAsync(
                        $@"DECLARE @lockResult int;
                           EXEC @lockResult = sp_getapplock
                               @Resource={lockResource},
                               @LockMode='Exclusive',
                               @LockOwner='Transaction',
                               @LockTimeout=15000;
                           IF @lockResult < 0 THROW 51000, 'Không thể khóa RequestKey đóng phần còn lại.', 1;");
                }

                var replay = await _purchaseAdviceFulfillment.FindClosedReplayAsync(requestKey);
                if (replay != null)
                {
                    if (replay.PurchaseOrderLineId != input.PurchaseOrderLineId
                        || replay.PayloadHash != payloadHash)
                    {
                        await transaction.RollbackAsync();
                        return Fail("Khóa yêu cầu đã được dùng cho một thao tác đóng phần còn lại khác.", PurchaseAdviceErrorCodes.BackPostConflict);
                    }

                    var replayPurchaseOrderId = await _context.PurchaseOrderLines
                        .Where(x => x.PurchaseOrderLineId == input.PurchaseOrderLineId)
                        .Select(x => x.PurchaseOrderId)
                        .SingleOrDefaultAsync();
                    if (replayPurchaseOrderId <= 0)
                        return Fail("Không tìm thấy đơn mua hàng của thao tác đã xử lý trước đó.");

                    await transaction.CommitAsync();
                    return ServiceResult<PurchaseOrderDetailDto>.Success(
                        await MapAsync(replayPurchaseOrderId),
                        "Thao tác đóng phần còn lại đã được xử lý trước đó.");
                }

                var line = await LoadLineForUpdateAsync(input.PurchaseOrderLineId);
                if (line == null)
                    return Fail("Không tìm thấy dòng đơn mua hàng.");
                var replayAfterLock = await _purchaseAdviceFulfillment.FindClosedReplayAsync(requestKey);
                if (replayAfterLock != null)
                {
                    if (replayAfterLock.PurchaseOrderLineId != input.PurchaseOrderLineId
                        || replayAfterLock.PayloadHash != payloadHash)
                    {
                        await transaction.RollbackAsync();
                        return Fail("Khóa yêu cầu đã được dùng cho một thao tác đóng phần còn lại khác.", PurchaseAdviceErrorCodes.BackPostConflict);
                    }

                    await transaction.CommitAsync();
                    return ServiceResult<PurchaseOrderDetailDto>.Success(
                        await MapAsync(line.PurchaseOrderId),
                        "Thao tác đóng phần còn lại đã được xử lý trước đó.");
                }
                if (!await CanAccessStoreAsync(actorStaffId, line.PurchaseOrder.StoreId))
                    return Fail("Bạn không có quyền đóng phần còn lại tại cửa hàng này.");
                if (line.PurchaseOrder.Status is PurchaseOrderStatuses.Cancelled or PurchaseOrderStatuses.Completed)
                    return Fail("Không thể đóng phần còn lại khi đơn đặt hàng ở trạng thái hiện tại.");
                if (!RowVersionMatches(line.RowVersion, expectedVersion))
                    return Fail("Dòng đơn mua đã được cập nhật bởi người khác. Vui lòng tải lại.", BranchReceiptErrorCodes.ResourceChanged);

                _context.Entry(line).Property(x => x.RowVersion).OriginalValue = expectedVersion;
                var receiptEvidence = await _context.PurchaseOrderReceiptPostings
                    .Where(x => x.PurchaseOrderLineId == line.PurchaseOrderLineId)
                    .Select(x => new
                    {
                        x.AcceptedBaseQuantity,
                        x.AcceptedProcurementQuantity
                    })
                    .ToListAsync();

                var acceptedBase = receiptEvidence.Sum(x => x.AcceptedBaseQuantity);
                var remainingBase = Math.Max(
                    0m,
                    line.OrderedBaseQuantity - acceptedBase - line.ClosedRemainingQuantity);
                decimal? remainingProcurement = null;
                if (line.OrderedProcurementQuantity.GetValueOrDefault() > 0
                    && line.ProcurementUnitId.HasValue)
                {
                    var acceptedProcurement = receiptEvidence
                        .Where(x => x.AcceptedProcurementQuantity.HasValue)
                        .Sum(x => x.AcceptedProcurementQuantity!.Value);
                    if (acceptedProcurement <= 0m && acceptedBase > 0m && line.OrderedBaseQuantity > 0m)
                    {
                        acceptedProcurement = acceptedBase
                            * line.OrderedProcurementQuantity!.Value
                            / line.OrderedBaseQuantity;
                    }

                    remainingProcurement = Math.Max(
                        0m,
                        line.OrderedProcurementQuantity!.Value
                        - acceptedProcurement
                        - line.ClosedProcurementQuantity);
                    remainingBase = line.OrderedBaseQuantity > 0m
                        ? line.OrderedBaseQuantity
                            * remainingProcurement.Value
                            / line.OrderedProcurementQuantity.Value
                        : 0m;
                }

                if (remainingBase <= 0m
                    || (remainingProcurement.HasValue && remainingProcurement.Value <= 0m))
                    return Fail("Dòng đơn mua không còn số lượng để đóng.");

                line.ClosedRemainingQuantity += remainingBase;
                if (remainingProcurement.HasValue)
                    line.ClosedProcurementQuantity += remainingProcurement.Value;
                line.CloseRemainingReason = Trim(input.Reason, 500);
                line.ClosedRemainingByStaffId = actorStaffId;
                line.ClosedRemainingAtUtc = DateTime.UtcNow;
                line.PurchaseOrder.UpdatedAtUtc = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                var backPost = await _purchaseAdviceFulfillment.BackPostClosedAsync(
                    line.PurchaseOrderLineId,
                    remainingBase,
                    requestKey,
                    payloadHash,
                    actorStaffId);
                if (!backPost.IsSuccess)
                {
                    await transaction.RollbackAsync();
                    return Fail(backPost.Message, backPost.ErrorCode);
                }

                await _context.SaveChangesAsync();
                await RecalculateOrderStatusAsync(line.PurchaseOrder);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return ServiceResult<PurchaseOrderDetailDto>.Success(
                    await MapAsync(line.PurchaseOrderId),
                    "Đã đóng phần còn lại; không phát sinh nhập kho hoặc ghi nhận hoàn tất.");
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                return Fail("Dòng đơn mua đã được cập nhật bởi người khác. Vui lòng tải lại.", BranchReceiptErrorCodes.ResourceChanged);
            }
        }

        public async Task<ServiceResult<PurchaseOrderDetailDto>> GetDetailAsync(
            int id, int actorStaffId, IReadOnlyCollection<string> roles)
        {
            var dto = await MapAsync(id);
            if (dto.PurchaseOrderId == 0) return Fail("Không tìm thấy đơn mua hàng.");
            if (!CanRead(roles) || !await CanAccessStoreAsync(actorStaffId, dto.StoreId))
                return Fail("Bạn không có quyền xem đơn mua hàng của cửa hàng này.");
            return ServiceResult<PurchaseOrderDetailDto>.Success(dto);
        }

        public async Task<IReadOnlyList<PurchaseOrderListItemDto>> ListAsync(
            int? storeId, string? status, int actorStaffId, IReadOnlyCollection<string> roles)
        {
            if (!CanRead(roles)) return Array.Empty<PurchaseOrderListItemDto>();
            var allowedStoreIds = _scopeAuthorization == null
                ? await _context.Stores.AsNoTracking().Select(x => x.StoreId).ToListAsync()
                : (await _scopeAuthorization.GetAllowedStoresAsync(actorStaffId)).Select(x => x.StoreId).ToList();
            var query = _context.PurchaseOrders.AsNoTracking()
                .Include(x => x.Store)
                .Include(x => x.Supplier)
                .Include(x => x.Lines)
                .Where(x => allowedStoreIds.Contains(x.StoreId))
                .AsQueryable();
            if (storeId.HasValue) query = query.Where(x => x.StoreId == storeId);
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
            var orders = await query.OrderByDescending(x => x.CreatedAtUtc).ToListAsync();
            return orders.Select(x => new PurchaseOrderListItemDto
            {
                PurchaseOrderId = x.PurchaseOrderId,
                Code = x.Code,
                StoreName = x.Store.Name,
                SupplierName = x.Supplier.Name,
                Status = x.Status,
                OrderDate = x.OrderDate,
                TotalAmount = x.Lines.Sum(l => ProcurementPurchaseMath.CalculateLineTotal(
                    l.PurchaseMode,
                    l.PackageCount,
                    l.UnitPricePerPackage ?? l.PackagePriceSnapshot,
                    l.OrderedProcurementQuantity,
                    l.UnitPricePerProcurementUnit))
            }).ToList();
        }

        public async Task<ServiceResult> ValidateReceiptLineAsync(BranchReceipt receipt, BranchReceiptLine line)
        {
            if (!line.PurchaseOrderLineId.HasValue) return ServiceResult.Success();
            var poLine = await LoadLineForUpdateAsync(line.PurchaseOrderLineId.Value);
            if (poLine == null) return ServiceResult.Failure("Không tìm thấy dòng đơn mua hàng.");
            if (poLine.PurchaseOrder.Status is not (PurchaseOrderStatuses.Approved or PurchaseOrderStatuses.MarkedAsSent or PurchaseOrderStatuses.PartiallyReceived))
                return ServiceResult.Failure("Đơn mua hàng chưa ở trạng thái cho phép nhận.");
            if (poLine.PurchaseOrder.StoreId != receipt.StoreId || poLine.PurchaseOrder.SupplierId != receipt.SupplierId)
                return ServiceResult.Failure("Cửa hàng hoặc nhà cung cấp trên phiếu nhận không khớp đơn mua.");
            if (poLine.IngredientId != line.IngredientId || poLine.RestockRequestId != line.RestockRequestId)
                return ServiceResult.Failure("Nguyên liệu hoặc yêu cầu nhập không khớp dòng đơn mua.");
            var acceptedRows = await _context.PurchaseOrderReceiptPostings.AsNoTracking()
                .Where(x => x.PurchaseOrderLineId == poLine.PurchaseOrderLineId)
                .Select(x => x.AcceptedBaseQuantity)
                .ToListAsync();
            var accepted = acceptedRows.Sum();
            if (poLine.OrderedProcurementQuantity.HasValue)
            {
                if (!line.AcceptedProcurementQuantity.HasValue
                    || line.AcceptedProcurementQuantity.Value < 0
                    || line.RejectedProcurementQuantity.GetValueOrDefault() < 0
                    || line.AcceptedProcurementQuantity.Value
                        + line.RejectedProcurementQuantity.GetValueOrDefault() <= 0)
                    return ServiceResult.Failure("Số lượng mua hàng chấp nhận/loại bỏ không hợp lệ.");

                var acceptedProcurementRows = await _context.PurchaseOrderReceiptPostings.AsNoTracking()
                    .Where(x => x.PurchaseOrderLineId == poLine.PurchaseOrderLineId)
                    .Select(x => x.AcceptedProcurementQuantity)
                    .ToListAsync();
                var acceptedProcurement = acceptedProcurementRows.Sum(x => x ?? 0m);
                var remainingProcurement = Math.Max(
                    0m,
                    poLine.OrderedProcurementQuantity.Value
                        - acceptedProcurement
                        - poLine.ClosedProcurementQuantity);
                if (line.AcceptedProcurementQuantity.Value > remainingProcurement)
                    return ServiceResult.Failure("Tổng số lượng mua hàng đã nhận vượt nghĩa vụ còn lại của dòng đơn mua.");
            }
            else
            {
                if (line.ReceivedBaseQuantity < 0 || line.RejectedBaseQuantity < 0
                    || line.ReceivedBaseQuantity + line.RejectedBaseQuantity <= 0)
                    return ServiceResult.Failure("Số lượng chấp nhận/loại bỏ không hợp lệ.");
                if (accepted + poLine.ClosedRemainingQuantity + line.ReceivedBaseQuantity > poLine.OrderedBaseQuantity)
                    return ServiceResult.Failure("Tổng số lượng nhận vượt số lượng còn lại của dòng đơn mua.");
            }
            return ServiceResult.Success();
        }

        public async Task<ServiceResult> RegisterReceiptPostingAsync(BranchReceipt receipt, BranchReceiptLine line, int actorStaffId)
        {
            if (!line.PurchaseOrderLineId.HasValue) return ServiceResult.Success();
            var ownsTransaction = _context.Database.CurrentTransaction == null;
            await using var transaction = ownsTransaction
                ? await _context.Database.BeginTransactionAsync()
                : null;
            try
            {
                if (await _context.PurchaseOrderReceiptPostings
                    .AnyAsync(x => x.BranchReceiptLineId == line.BranchReceiptLineId))
                    return ServiceResult.Success("Dòng nhận đã được ghi nhận trước đó.");

                // Serialize on the PO line before the second replay check. A concurrent
                // confirmation may complete the PO while this transaction is waiting.
                await LoadLineForUpdateAsync(line.PurchaseOrderLineId.Value);
                if (await _context.PurchaseOrderReceiptPostings
                    .AnyAsync(x => x.BranchReceiptLineId == line.BranchReceiptLineId))
                    return ServiceResult.Success("Dòng nhận đã được ghi nhận trước đó.");

                var validation = await ValidateReceiptLineAsync(receipt, line);
                if (!validation.IsSuccess) return validation;

                var poLine = await LoadLineForUpdateAsync(line.PurchaseOrderLineId.Value);
                _context.PurchaseOrderReceiptPostings.Add(new PurchaseOrderReceiptPosting
                {
                    PurchaseMode = line.PurchaseMode,
                    PurchaseOrderLineId = poLine!.PurchaseOrderLineId,
                    BranchReceiptLineId = line.BranchReceiptLineId,
                    AcceptedBaseQuantity = line.ReceivedBaseQuantity,
                    RejectedBaseQuantity = line.RejectedBaseQuantity,
                    AcceptedProcurementQuantity = line.AcceptedProcurementQuantity,
                    RejectedProcurementQuantity = line.RejectedProcurementQuantity,
                    InventoryPostingBaseQuantity = line.InventoryPostingBaseQuantity ?? line.ReceivedBaseQuantity,
                    ProcurementUnitId = line.ProcurementUnitId,
                    InventoryBaseUnitId = line.InventoryBaseUnitId,
                    ProcurementToInventoryFactor = line.ProcurementToInventoryFactor,
                    CreatedByStaffId = actorStaffId,
                    CreatedAtUtc = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                if (line.ReceivedBaseQuantity > 0)
                {
                    var backPost = await _purchaseAdviceFulfillment.BackPostAcceptedAsync(
                        poLine.PurchaseOrderLineId,
                        line.BranchReceiptLineId,
                        line.ReceivedBaseQuantity,
                        actorStaffId);
                    if (!backPost.IsSuccess)
                        return backPost;
                }

                var order = poLine.PurchaseOrder;
                await RecalculateOrderStatusAsync(order);
                await PurchaseOrderBatchStatusUpdater.RefreshAsync(_context, order.PurchaseOrderBatchId);
                await _context.SaveChangesAsync();
                if (transaction != null) await transaction.CommitAsync();
                return ServiceResult.Success("Đã ghi nhận số lượng nhận theo đơn mua.");
            }
            catch
            {
                if (transaction != null) await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task<ServiceResult<PurchaseOrderDetailDto>> TransitionAsync(
            int id, string rowVersion, string expected, string next, int actorStaffId, IReadOnlyCollection<string> roles,
            Func<IReadOnlyCollection<string>, bool> permission)
        {
            if (!permission(roles)) return Fail("Bạn không có quyền cập nhật đơn mua hàng.");
            if (!TryParseRowVersion(rowVersion, out var expectedVersion))
                return Fail("Thiếu hoặc sai phiên bản dữ liệu.", BranchReceiptErrorCodes.ValidationRowVersionRequired);
            var order = await _context.PurchaseOrders.SingleOrDefaultAsync(x => x.PurchaseOrderId == id);
            if (order == null) return Fail("Không tìm thấy đơn mua hàng.");
            if (next == PurchaseOrderStatuses.Approved && order.PurchaseOrderBatchId.HasValue)
                return Fail("Đơn đặt hàng con được duyệt theo đơn gộp; không duyệt lại từng đơn.");
            if (!await CanAccessStoreAsync(actorStaffId, order.StoreId))
                return Fail("Bạn không có quyền cập nhật đơn mua hàng của cửa hàng này.");
            if (order.Status != expected)
                return Fail("Trạng thái đơn mua hàng đã thay đổi. Vui lòng tải lại trước khi thao tác.");
            if (!RowVersionMatches(order.RowVersion, expectedVersion))
                return Fail("Đơn mua hàng đã được cập nhật bởi người khác. Vui lòng tải lại.", BranchReceiptErrorCodes.ResourceChanged);
            SetExpectedRowVersion(order, expectedVersion);
            order.Status = next;
            order.UpdatedAtUtc = DateTime.UtcNow;
            if (next == PurchaseOrderStatuses.Approved) { order.ApprovedByStaffId = actorStaffId; order.ApprovedAtUtc = DateTime.UtcNow; }
            if (next == PurchaseOrderStatuses.MarkedAsSent) { order.SentByStaffId = actorStaffId; order.SentAtUtc = DateTime.UtcNow; }
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Fail("Đơn mua hàng đã được cập nhật bởi người khác. Vui lòng tải lại.", BranchReceiptErrorCodes.ResourceChanged);
            }
            return ServiceResult<PurchaseOrderDetailDto>.Success(await MapAsync(id), $"Đã chuyển đơn mua sang {next}.");
        }

        private async Task<PurchaseOrderLine?> LoadLineForUpdateAsync(int id)
        {
            if (_context.Database.IsSqlServer())
            {
                var line = await _context.PurchaseOrderLines.FromSqlInterpolated(
                    $@"SELECT * FROM PurchaseOrderLines WITH (UPDLOCK, HOLDLOCK, ROWLOCK) WHERE PurchaseOrderLineId = {id}")
                    .SingleOrDefaultAsync();
                if (line != null) await _context.Entry(line).Reference(x => x.PurchaseOrder).LoadAsync();
                return line;
            }
            return await _context.PurchaseOrderLines.Include(x => x.PurchaseOrder).SingleOrDefaultAsync(x => x.PurchaseOrderLineId == id);
        }

        private async Task<PurchaseOrderDetailDto> MapAsync(int id)
        {
            var order = await _context.PurchaseOrders.AsNoTracking()
                .Include(x => x.Store).Include(x => x.Supplier)
                .Include(x => x.Lines).ThenInclude(x => x.Ingredient).ThenInclude(x => x.BaseUnit)
                .Include(x => x.Lines).ThenInclude(x => x.PackageUnitSnapshot)
                .Include(x => x.Lines).ThenInclude(x => x.ProcurementUnit)
                .Include(x => x.Lines).ThenInclude(x => x.ReceiptPostings)
                    .ThenInclude(x => x.BranchReceiptLine)
                .SingleOrDefaultAsync(x => x.PurchaseOrderId == id);
            if (order == null) return new PurchaseOrderDetailDto();
            var activeReceiptDraftId = await _context.BranchReceipts
                .AsNoTracking()
                .Where(x => x.PurchaseOrderId == order.PurchaseOrderId
                    && x.Status == BranchReceiptStatuses.Draft)
                .OrderBy(x => x.BranchReceiptId)
                .Select(x => (int?)x.BranchReceiptId)
                .FirstOrDefaultAsync();
            return new PurchaseOrderDetailDto
            {
                PurchaseOrderId = order.PurchaseOrderId,
                Code = order.Code,
                StoreId = order.StoreId,
                StoreName = order.Store.Name,
                SupplierId = order.SupplierId,
                SupplierName = order.Supplier.Name,
                Status = order.Status,
                OrderDate = order.OrderDate,
                ExpectedDeliveryAtUtc = order.ExpectedDeliveryAtUtc,
                Note = order.Note,
                TotalAmount = order.Lines.Sum(x => ProcurementPurchaseMath.CalculateLineTotal(
                    x.PurchaseMode,
                    x.PackageCount,
                    x.UnitPricePerPackage ?? x.PackagePriceSnapshot,
                    x.OrderedProcurementQuantity,
                    x.UnitPricePerProcurementUnit)),
                RowVersion = Convert.ToBase64String(order.RowVersion ?? Array.Empty<byte>()),
                ActiveReceiptDraftId = activeReceiptDraftId,
                Lines = order.Lines.Select(x =>
                {
                    var accepted = x.ReceiptPostings.Sum(p => p.AcceptedBaseQuantity);
                    var rejected = x.ReceiptPostings.Sum(p => p.RejectedBaseQuantity);
                    return new PurchaseOrderLineDto
                    {
                        PurchaseMode = x.PurchaseMode,
                        PurchaseOrderLineId = x.PurchaseOrderLineId,
                        RestockRequestId = x.RestockRequestId,
                        IngredientId = x.IngredientId,
                        IngredientName = x.Ingredient.Name,
                        BaseUnitName = x.Ingredient.BaseUnit.Name,
                        PackageCount = x.PackageCount,
                        PackageQuantitySnapshot = x.PackageQuantitySnapshot,
                        PackageUnitName = x.PackageUnitSnapshot?.Name ?? string.Empty,
                        PackagePriceSnapshot = x.PackagePriceSnapshot,
                        UnitPricePerProcurementUnit = x.UnitPricePerProcurementUnit,
                        OrderedBaseQuantity = x.OrderedBaseQuantity,
                        OrderedProcurementQuantity = x.OrderedProcurementQuantity,
                        PackSizeProcurementQuantity = x.PackSizeProcurementQuantity,
                        ProcurementUnitId = x.ProcurementUnitId,
                        ProcurementUnitName = x.ProcurementUnit?.Name,
                        RoundingSurplusProcurementQuantity = x.RoundingSurplusProcurementQuantity,
                        AcceptedBaseQuantity = accepted,
                        AcceptedProcurementQuantity = x.ReceiptPostings.Sum(p => p.AcceptedProcurementQuantity ?? 0m),
                        InventoryPostingBaseQuantity = x.InventoryPostingBaseQuantity,
                        RejectedBaseQuantity = rejected,
                        ClosedRemainingQuantity = x.ClosedRemainingQuantity,
                        CloseRemainingReason = x.CloseRemainingReason,
                        ClosedRemainingByStaffId = x.ClosedRemainingByStaffId,
                        ClosedRemainingAtUtc = x.ClosedRemainingAtUtc,
                        RemainingBaseQuantity = Math.Max(0m, x.OrderedBaseQuantity - accepted - x.ClosedRemainingQuantity),
                        RemainingProcurementQuantity = x.OrderedProcurementQuantity.HasValue
                            ? Math.Max(
                                0m,
                                x.OrderedProcurementQuantity.Value
                                    - x.ReceiptPostings.Sum(p => p.AcceptedProcurementQuantity ?? 0m)
                                    - x.ClosedProcurementQuantity)
                            : null,
                        ReceiptCount = x.ReceiptPostings.Select(p => p.BranchReceiptLine.BranchReceiptId).Distinct().Count(),
                        RowVersion = Convert.ToBase64String(x.RowVersion ?? Array.Empty<byte>()),
                        PromisedLeadTimeDaysSnapshot = x.PromisedLeadTimeDaysSnapshot
                    };
                }).ToList()
            };
        }

        private Task<bool> CanAccessStoreAsync(int actorStaffId, int storeId) =>
            _scopeAuthorization?.CanAccessStoreAsync(actorStaffId, storeId) ?? Task.FromResult(true);

        private static bool CanRead(IReadOnlyCollection<string> roles) =>
            roles.Any(x => x is RoleConstants.AccountantWarehouse or RoleConstants.BusinessOwner
                or RoleConstants.AreaManager or RoleConstants.StoreManager or RoleConstants.ShiftSupervisor);

        private static bool CanCreate(IReadOnlyCollection<string> roles) =>
            roles.Any(x => x is RoleConstants.AccountantWarehouse or RoleConstants.BusinessOwner);

        private static bool CanApprove(IReadOnlyCollection<string> roles) => roles.Contains(RoleConstants.BusinessOwner);
        private static bool CanSend(IReadOnlyCollection<string> roles) => roles.Contains(RoleConstants.AccountantWarehouse);
        private static bool CanCancel(IReadOnlyCollection<string> roles) => roles.Contains(RoleConstants.BusinessOwner);
        private static bool CanCloseRemaining(IReadOnlyCollection<string> roles) => roles.Contains(RoleConstants.BusinessOwner);

        private async Task RecalculateOrderStatusAsync(PurchaseOrder order)
        {
            var lines = await _context.PurchaseOrderLines
                .Where(x => x.PurchaseOrderId == order.PurchaseOrderId)
                .Select(x => new { x.PurchaseOrderLineId, x.OrderedBaseQuantity, x.ClosedRemainingQuantity })
                .ToListAsync();
            var acceptedRows = await _context.PurchaseOrderReceiptPostings
                .Where(x => x.PurchaseOrderLine.PurchaseOrderId == order.PurchaseOrderId)
                .Select(x => new { x.PurchaseOrderLineId, x.AcceptedBaseQuantity })
                .ToListAsync();
            var acceptedByLine = acceptedRows
                .GroupBy(x => x.PurchaseOrderLineId)
                .ToDictionary(x => x.Key, x => x.Sum(y => y.AcceptedBaseQuantity));
            var completed = lines.All(x =>
                acceptedByLine.GetValueOrDefault(x.PurchaseOrderLineId) + x.ClosedRemainingQuantity >= x.OrderedBaseQuantity);
            var progressed = lines.Any(x =>
                acceptedByLine.GetValueOrDefault(x.PurchaseOrderLineId) > 0 || x.ClosedRemainingQuantity > 0);

            order.Status = completed
                ? PurchaseOrderStatuses.Completed
                : progressed ? PurchaseOrderStatuses.PartiallyReceived : order.Status;
            order.CompletedAtUtc = completed ? DateTime.UtcNow : null;
            order.UpdatedAtUtc = DateTime.UtcNow;
        }

        private static bool TryParseRowVersion(string? value, out byte[] rowVersion)
        {
            rowVersion = Array.Empty<byte>();
            if (string.IsNullOrWhiteSpace(value)) return false;
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

        private static bool RowVersionMatches(byte[] current, byte[] expected) =>
            current != null && current.SequenceEqual(expected);

        private void SetExpectedRowVersion(PurchaseOrder order, byte[] expectedVersion) =>
            _context.Entry(order).Property(x => x.RowVersion).OriginalValue = expectedVersion;

        private static string? Trim(string? value, int max) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, max)];

        private static ServiceResult<PurchaseOrderDetailDto> Fail(string message, string? code = null) =>
            ServiceResult<PurchaseOrderDetailDto>.Failure(message, errorCode: code);
    }
}
