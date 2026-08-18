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
using System.Data;

namespace CafeChain.Application.Services.Inventories
{
    public sealed class PurchaseOrderService : IPurchaseOrderService
    {
        private readonly AppDbContext _context;
        private readonly IUnitConversionService _conversion;
        private readonly IRestockAllocationService _allocations;
        private readonly IScopeAuthorizationService? _scopeAuthorization;
        private readonly IPurchaseAdviceFulfillmentService _purchaseAdviceFulfillment;
        private readonly IIngredientSupplierPackageValidator _packageEligibility;

        public PurchaseOrderService(
            AppDbContext context,
            IUnitConversionService conversion,
            IRestockAllocationService allocations,
            IScopeAuthorizationService? scopeAuthorization = null,
            IPurchaseAdviceFulfillmentService? purchaseAdviceFulfillment = null,
            IIngredientSupplierPackageValidator? packageEligibility = null)
        {
            _context = context;
            _conversion = conversion;
            _allocations = allocations;
            _scopeAuthorization = scopeAuthorization;
            _purchaseAdviceFulfillment = purchaseAdviceFulfillment
                ?? new PurchaseAdviceFulfillmentService(context);
            _packageEligibility = packageEligibility
                ?? new IngredientSupplierPackageValidator(
                    context,
                    new PhysicalUnitConversionService(
                        context,
                        Microsoft.Extensions.Logging.Abstractions.NullLogger<PhysicalUnitConversionService>.Instance),
                    conversion);
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

            await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var requestedAdviceLineIds = input.Lines
                    .Where(x => x.PurchaseAdviceLineId.HasValue)
                    .Select(x => x.PurchaseAdviceLineId!.Value)
                    .Distinct()
                    .ToArray();
                if (requestedAdviceLineIds.Length > 0 && requestedAdviceLineIds.Length != input.Lines.Count)
                    return Fail("Không thể trộn dòng đề nghị mua với dòng tạo thủ công trong cùng đơn đặt hàng.");

                if (_context.Database.IsSqlServer())
                {
                    foreach (var adviceLineId in requestedAdviceLineIds.OrderBy(x => x))
                    {
                        var lockResource = $"NormalPurchaseOrder:PurchaseAdviceLine:{adviceLineId}";
                        await _context.Database.ExecuteSqlInterpolatedAsync(
                            $@"DECLARE @lockResult int;
                               EXEC @lockResult = sp_getapplock
                                   @Resource={lockResource},
                                   @LockMode='Exclusive',
                                   @LockOwner='Transaction',
                                   @LockTimeout=15000;
                               IF @lockResult < 0 THROW 51000, 'Không thể khóa đề nghị mua để tạo đơn đặt hàng.', 1;");
                    }
                }

                var adviceLines = requestedAdviceLineIds.Length == 0
                    ? new Dictionary<int, PurchaseAdviceLine>()
                    : await _context.PurchaseAdviceLines
                        .Include(x => x.PurchaseAdvice)
                        .Where(x => requestedAdviceLineIds.Contains(x.PurchaseAdviceLineId))
                        .ToDictionaryAsync(x => x.PurchaseAdviceLineId);
                if (adviceLines.Count != requestedAdviceLineIds.Length)
                    return Fail("Không tìm thấy đề nghị mua đã chọn.");

                var existingOrderId = requestedAdviceLineIds.Length == 0
                    ? 0
                    : await _context.PurchaseOrderLines
                        .Where(x => x.PurchaseAdviceLineId.HasValue
                            && requestedAdviceLineIds.Contains(x.PurchaseAdviceLineId.Value)
                            && x.PurchaseOrder.Status != PurchaseOrderStatuses.Cancelled)
                        .Select(x => x.PurchaseOrderId)
                        .FirstOrDefaultAsync();
                if (existingOrderId > 0)
                {
                    await tx.CommitAsync();
                    return ServiceResult<PurchaseOrderDetailDto>.Success(
                        await MapAsync(existingOrderId),
                        "Đề nghị mua này đã được dùng để tạo đơn đặt hàng.");
                }

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
                    PurchaseAdviceLine? adviceLine = null;
                    if (requested.PurchaseAdviceLineId.HasValue)
                    {
                        adviceLine = adviceLines[requested.PurchaseAdviceLineId.Value];
                        if (!PurchaseAdviceStatuses.ActiveReservationStatuses.Contains(adviceLine.PurchaseAdvice.Status))
                            return Fail("Đề nghị mua chưa ở trạng thái sẵn sàng để tạo đơn đặt hàng.");
                        if (adviceLine.PurchaseAdvice.StoreId != input.StoreId
                            || adviceLine.RestockRequestId != requested.RestockRequestId
                            || adviceLine.IngredientId != requested.IngredientId)
                            return Fail("Đề nghị mua không khớp cửa hàng, yêu cầu nhập hoặc nguyên liệu.");
                        if (!string.IsNullOrWhiteSpace(requested.PurchaseAdviceLineRowVersion)
                            && (!TryParseRowVersion(requested.PurchaseAdviceLineRowVersion, out var expectedAdviceVersion)
                                || !RowVersionMatches(adviceLine.RowVersion, expectedAdviceVersion)))
                            return Fail("Đề nghị mua đã được người khác xử lý. Vui lòng tải lại dữ liệu.");
                    }

                    var offer = await _context.IngredientSuppliers.AsNoTracking()
                        .Include(x => x.Ingredient)
                        .Include(x => x.Supplier)
                        .Include(x => x.Unit)
                        .Include(x => x.LooseProcurementUnit)
                        .SingleOrDefaultAsync(x => x.IngredientSupplierId == requested.IngredientSupplierId);
                    if (offer == null || !offer.Active || offer.SupplierId != input.SupplierId
                        || offer.IngredientId != requested.IngredientId)
                        return Fail("Gói mua không khớp nhà cung cấp hoặc nguyên liệu.");
                    var packageEligibility = await _packageEligibility
                        .EvaluateProcurementEligibilityAsync(
                            offer,
                            requested.PurchaseMode,
                            input.StoreId);
                    if (!packageEligibility.IsProcurementEligible)
                        return Fail($"Gói mua chưa sẵn sàng để tạo đơn đặt hàng. {packageEligibility.Message}");

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
                    decimal? looseDemandProcurement = null;
                    int sourceUnitId;
                    decimal sourceQuantity;

                    if (requested.PurchaseMode == PurchaseMode.Loose)
                    {
                        if (!offer.AllowsLoosePurchase
                            || offer.CurrentProcurementUnitPrice <= 0m
                            || !offer.LooseProcurementUnitId.HasValue
                            || procurementUnitId != offer.LooseProcurementUnitId)
                            return Fail("Nhà cung cấp chưa cho phép mua rời theo đúng đơn vị mua hàng của nhu cầu.");

                        looseDemandProcurement = requested.OrderedProcurementQuantity!.Value;
                        if (!LoosePurchaseMath.TryPlan(
                                looseDemandProcurement.Value,
                                offer.LooseMinimumOrderQuantity,
                                offer.LooseQuantityStep,
                                out var loosePlan))
                            return Fail("Không thể áp dụng MOQ hoặc bước số lượng mua lẻ của Nhà cung cấp.");

                        orderedProcurement = loosePlan.OrderedQuantity;
                        sourceUnitId = offer.LooseProcurementUnitId.Value;
                        sourceQuantity = orderedProcurement.Value;
                        unitPricePerProcurement = offer.CurrentProcurementUnitPrice;
                        roundingSurplus = loosePlan.RoundingSurplusQuantity;
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

                    var demandCoveredBaseQuantity = converted.Data;
                    if (requested.PurchaseMode == PurchaseMode.Loose
                        && looseDemandProcurement.HasValue
                        && orderedProcurement != looseDemandProcurement)
                    {
                        var demandConversion = await _conversion.ConvertAsync(
                            offer.IngredientId,
                            looseDemandProcurement.Value,
                            sourceUnitId,
                            offer.Ingredient.BaseUnitId);
                        if (!demandConversion.IsSuccess || demandConversion.Data <= 0m)
                            return Fail(demandConversion.Message ?? "Không quy đổi được nhu cầu mua lẻ về đơn vị tồn kho.");
                        demandCoveredBaseQuantity = demandConversion.Data;
                    }

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
                        if (looseDemandProcurement > remainingProcurement)
                            return Fail($"Số lượng mua rời vượt {remainingProcurement:N3} đơn vị mua hàng còn lại.");
                    }

                    decimal? demandCoveredProcurementQuantity = looseDemandProcurement ?? orderedProcurement;
                    if (adviceLine != null)
                    {
                        var remainingBase = Math.Max(0m,
                            adviceLine.RequestedPurchaseBaseQuantity
                                - adviceLine.AllocatedToPoBaseQuantity
                                - adviceLine.ClosedBaseQuantity);
                        if (remainingBase <= 0m)
                            return Fail("Đề nghị mua này đã được dùng để tạo đơn đặt hàng.");
                        demandCoveredBaseQuantity = Math.Min(remainingBase, converted.Data);

                        if (adviceLine.RequestedProcurementQuantity.HasValue)
                        {
                            var remainingProcurement = Math.Max(0m,
                                adviceLine.RequestedProcurementQuantity.Value
                                    - adviceLine.AllocatedToPoProcurementQuantity
                                    - adviceLine.ClosedProcurementQuantity);
                            if (!orderedProcurement.HasValue || orderedProcurement.Value <= 0m)
                                return Fail("Không xác định được số lượng mua theo đơn vị của đề nghị mua.");
                            demandCoveredProcurementQuantity = Math.Min(
                                remainingProcurement,
                                looseDemandProcurement ?? orderedProcurement.Value);
                        }
                    }
                    else if (requested.RestockRequestId.HasValue)
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
                            if (demandCoveredBaseQuantity > summary.RemainingUnallocatedQuantity)
                                return Fail("Số lượng mua rời vượt phần nhu cầu chưa phân bổ.");
                            allocationQuantity = demandCoveredBaseQuantity;
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
                        PurchaseAdviceLineId = requested.PurchaseAdviceLineId,
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

                    if (adviceLine != null)
                    {
                        adviceLine.AllocatedToPoBaseQuantity = Math.Min(
                            adviceLine.RequestedPurchaseBaseQuantity,
                            adviceLine.AllocatedToPoBaseQuantity + demandCoveredBaseQuantity);
                        if (adviceLine.RequestedProcurementQuantity.HasValue
                            && demandCoveredProcurementQuantity.HasValue)
                        {
                            adviceLine.AllocatedToPoProcurementQuantity = Math.Min(
                                adviceLine.RequestedProcurementQuantity.Value,
                                adviceLine.AllocatedToPoProcurementQuantity + demandCoveredProcurementQuantity.Value);
                        }
                        adviceLine.PurchaseMode = requested.PurchaseMode;
                        var remaining = adviceLine.RequestedProcurementQuantity.HasValue
                            ? adviceLine.RequestedProcurementQuantity.Value
                                - adviceLine.AllocatedToPoProcurementQuantity
                                - adviceLine.ClosedProcurementQuantity
                            : adviceLine.RequestedPurchaseBaseQuantity
                                - adviceLine.AllocatedToPoBaseQuantity
                                - adviceLine.ClosedBaseQuantity;
                        adviceLine.IsActiveReservation = remaining > 0m;
                    }
                }

                _context.PurchaseOrders.Add(order);
                foreach (var purchaseAdviceId in adviceLines.Values
                    .Select(x => x.PurchaseAdviceId)
                    .Distinct())
                {
                    await _purchaseAdviceFulfillment.RecomputeHeaderStatusAsync(
                        purchaseAdviceId,
                        actorStaffId,
                        $"Cập nhật phân bổ từ đơn đặt hàng thường {order.Code}.");
                }
                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                return ServiceResult<PurchaseOrderDetailDto>.Success(await MapAsync(order.PurchaseOrderId), "Đã tạo đơn mua hàng nháp.");
            }
            catch (DbUpdateConcurrencyException)
            {
                await tx.RollbackAsync();
                return Fail("Đề nghị mua đã được người khác xử lý. Vui lòng tải lại dữ liệu.");
            }
            catch (Exception)
            {
                await tx.RollbackAsync();
                return Fail("Không thể tạo đơn đặt hàng lúc này. Vui lòng thử lại.");
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
                var affectedAdviceIds = order.Lines
                    .Where(x => x.PurchaseAdviceLineId.HasValue)
                    .Select(x => x.PurchaseAdviceLineId!.Value)
                    .Distinct()
                    .ToArray();
                await _context.SaveChangesAsync();
                foreach (var adviceLineId in affectedAdviceIds)
                    await RefreshAdviceAllocationAfterCancellationAsync(adviceLineId, actorStaffId, order.Code);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Fail("Đơn mua hàng đã được cập nhật bởi người khác. Vui lòng tải lại.", BranchReceiptErrorCodes.ResourceChanged);
            }
            return ServiceResult<PurchaseOrderDetailDto>.Success(await MapAsync(id), "Đã hủy đơn mua hàng.");
        }

        private async Task RefreshAdviceAllocationAfterCancellationAsync(
            int purchaseAdviceLineId,
            int actorStaffId,
            string orderCode)
        {
            var adviceLine = await _context.PurchaseAdviceLines
                .Include(x => x.PurchaseAdvice)
                .SingleAsync(x => x.PurchaseAdviceLineId == purchaseAdviceLineId);
            var batchBase = await _context.PurchaseOrderLineAllocations
                .Where(x => x.PurchaseAdviceLineId == purchaseAdviceLineId
                    && x.PurchaseOrder.Status != PurchaseOrderStatuses.Cancelled)
                .SumAsync(x => (decimal?)x.AllocatedBaseQuantity) ?? 0m;
            var normalBase = await _context.PurchaseOrderLines
                .Where(x => x.PurchaseAdviceLineId == purchaseAdviceLineId
                    && x.PurchaseOrder.PurchaseOrderBatchId == null
                    && x.PurchaseOrder.Status != PurchaseOrderStatuses.Cancelled)
                .SumAsync(x => (decimal?)x.OrderedBaseQuantity) ?? 0m;
            adviceLine.AllocatedToPoBaseQuantity = Math.Min(
                adviceLine.RequestedPurchaseBaseQuantity,
                batchBase + normalBase);

            if (adviceLine.RequestedProcurementQuantity.HasValue)
            {
                var batchProcurement = await _context.PurchaseOrderLineAllocations
                    .Where(x => x.PurchaseAdviceLineId == purchaseAdviceLineId
                        && x.PurchaseOrder.Status != PurchaseOrderStatuses.Cancelled)
                    .SumAsync(x => x.DemandCoveredProcurementQuantity) ?? 0m;
                var normalProcurement = await _context.PurchaseOrderLines
                    .Where(x => x.PurchaseAdviceLineId == purchaseAdviceLineId
                        && x.PurchaseOrder.PurchaseOrderBatchId == null
                        && x.PurchaseOrder.Status != PurchaseOrderStatuses.Cancelled)
                    .SumAsync(x => x.OrderedProcurementQuantity) ?? 0m;
                adviceLine.AllocatedToPoProcurementQuantity = Math.Min(
                    adviceLine.RequestedProcurementQuantity.Value,
                    batchProcurement + normalProcurement);
            }

            var remaining = adviceLine.RequestedProcurementQuantity.HasValue
                ? adviceLine.RequestedProcurementQuantity.Value
                    - adviceLine.AllocatedToPoProcurementQuantity
                    - adviceLine.ClosedProcurementQuantity
                : adviceLine.RequestedPurchaseBaseQuantity
                    - adviceLine.AllocatedToPoBaseQuantity
                    - adviceLine.ClosedBaseQuantity;
            adviceLine.IsActiveReservation = remaining > 0m;
            await _purchaseAdviceFulfillment.RecomputeHeaderStatusAsync(
                adviceLine.PurchaseAdviceId,
                actorStaffId,
                $"Trả lại phần chưa đặt sau khi hủy đơn {orderCode}.");
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
            if (input.CloseBaseQuantity <= 0m)
                return Fail("Số lượng đóng nghĩa vụ phải lớn hơn 0.");
            if (string.IsNullOrWhiteSpace(input.Reason))
                return Fail("Lý do không yêu cầu giao bù là bắt buộc.");
            if (string.IsNullOrWhiteSpace(input.RequestKey) || input.RequestKey.Trim().Length > 100)
                return Fail("Khóa yêu cầu ổn định là bắt buộc và không được vượt quá 100 ký tự.", PurchaseAdviceErrorCodes.BackPostRequestKeyRequired);
            if (!TryParseRowVersion(input.RowVersion, out var expectedVersion))
                return Fail("Thiếu hoặc sai phiên bản dữ liệu.", BranchReceiptErrorCodes.ValidationRowVersionRequired);

            var requestKey = input.RequestKey.Trim();
            var payloadHash = PurchaseAdviceFulfillmentService.ComputeClosePayloadHash(
                input.PurchaseOrderLineId,
                input.CloseBaseQuantity,
                input.RowVersion,
                input.Reason);
            var legacyPayloadHash = PurchaseAdviceFulfillmentService.ComputeLegacyClosePayloadHash(
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

                var closureReplay = await _context.PurchaseOrderLineClosures.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.RequestKey == requestKey);
                if (closureReplay != null)
                {
                    if (closureReplay.PurchaseOrderLineId != input.PurchaseOrderLineId
                        || closureReplay.PayloadHash != payloadHash
                        || closureReplay.ClosedBaseQuantity != input.CloseBaseQuantity)
                    {
                        await transaction.RollbackAsync();
                        return Fail("Khóa yêu cầu đã được dùng cho một thao tác đóng nghĩa vụ khác.", PurchaseAdviceErrorCodes.BackPostConflict);
                    }

                    var replayPurchaseOrderId = await _context.PurchaseOrderLines.AsNoTracking()
                        .Where(x => x.PurchaseOrderLineId == input.PurchaseOrderLineId)
                        .Select(x => x.PurchaseOrderId)
                        .SingleOrDefaultAsync();
                    await transaction.CommitAsync();
                    return ServiceResult<PurchaseOrderDetailDto>.Success(
                        await MapAsync(replayPurchaseOrderId),
                        "Thao tác đóng nghĩa vụ đã được xử lý trước đó.");
                }

                var replay = await _purchaseAdviceFulfillment.FindClosedReplayAsync(requestKey);
                if (replay != null)
                {
                    if (replay.PurchaseOrderLineId != input.PurchaseOrderLineId
                        || replay.Quantity != input.CloseBaseQuantity
                        || (replay.PayloadHash != payloadHash && replay.PayloadHash != legacyPayloadHash))
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
                        || replayAfterLock.Quantity != input.CloseBaseQuantity
                        || (replayAfterLock.PayloadHash != payloadHash && replayAfterLock.PayloadHash != legacyPayloadHash))
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
                if (line.PurchaseOrder.Status is not (PurchaseOrderStatuses.MarkedAsSent or PurchaseOrderStatuses.PartiallyReceived))
                    return Fail("Chỉ được đóng nghĩa vụ chưa giao sau khi đơn đã gửi nhà cung cấp.");
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

                if (input.CloseBaseQuantity > remainingBase)
                    return Fail($"Số lượng đóng vượt phần còn phải giao {remainingBase:N3} {line.Ingredient?.BaseUnit?.Name ?? "đơn vị cơ sở"}.");

                var closeBaseQuantity = input.CloseBaseQuantity;
                decimal? closeProcurementQuantity = null;
                if (remainingProcurement.HasValue
                    && line.OrderedProcurementQuantity.GetValueOrDefault() > 0m
                    && line.OrderedBaseQuantity > 0m)
                {
                    closeProcurementQuantity = Math.Min(
                        remainingProcurement.Value,
                        closeBaseQuantity * line.OrderedProcurementQuantity!.Value / line.OrderedBaseQuantity);
                }

                line.ClosedRemainingQuantity += closeBaseQuantity;
                if (closeProcurementQuantity.HasValue)
                    line.ClosedProcurementQuantity += closeProcurementQuantity.Value;
                line.CloseRemainingReason = Trim(input.Reason, 500);
                line.ClosedRemainingByStaffId = actorStaffId;
                line.ClosedRemainingAtUtc = DateTime.UtcNow;
                line.PurchaseOrder.UpdatedAtUtc = DateTime.UtcNow;

                _context.PurchaseOrderLineClosures.Add(new PurchaseOrderLineClosure
                {
                    PurchaseOrderLineId = line.PurchaseOrderLineId,
                    ClosedBaseQuantity = closeBaseQuantity,
                    ClosedProcurementQuantity = closeProcurementQuantity,
                    ProcurementUnitId = closeProcurementQuantity.HasValue ? line.ProcurementUnitId : null,
                    Reason = Trim(input.Reason, 500)!,
                    RequestKey = requestKey,
                    PayloadHash = payloadHash,
                    ActorStaffId = actorStaffId,
                    CreatedAtUtc = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                var backPost = await _purchaseAdviceFulfillment.BackPostClosedAsync(
                    line.PurchaseOrderLineId,
                    closeBaseQuantity,
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
                    "Đã đóng số lượng nghĩa vụ đã chọn; thao tác này không nhập kho.");
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
            if (poLine.PurchaseOrder.Status is not (PurchaseOrderStatuses.MarkedAsSent or PurchaseOrderStatuses.PartiallyReceived))
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
                var acceptedBaseQuantity = line.InventoryPostingBaseQuantity
                    ?? line.ReceivedBaseQuantity;
                _context.PurchaseOrderReceiptPostings.Add(new PurchaseOrderReceiptPosting
                {
                    PurchaseMode = line.PurchaseMode,
                    PurchaseOrderLineId = poLine!.PurchaseOrderLineId,
                    BranchReceiptLineId = line.BranchReceiptLineId,
                    AcceptedBaseQuantity = acceptedBaseQuantity,
                    RejectedBaseQuantity = line.RejectedBaseQuantity,
                    AcceptedProcurementQuantity = line.AcceptedProcurementQuantity,
                    RejectedProcurementQuantity = line.RejectedProcurementQuantity,
                    InventoryPostingBaseQuantity = acceptedBaseQuantity,
                    ProcurementUnitId = line.ProcurementUnitId,
                    InventoryBaseUnitId = line.InventoryBaseUnitId,
                    ProcurementToInventoryFactor = line.ProcurementToInventoryFactor,
                    CreatedByStaffId = actorStaffId,
                    CreatedAtUtc = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                if (acceptedBaseQuantity > 0)
                {
                    var backPost = await _purchaseAdviceFulfillment.BackPostAcceptedAsync(
                        poLine.PurchaseOrderLineId,
                        line.BranchReceiptLineId,
                        acceptedBaseQuantity,
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
            if (next == PurchaseOrderStatuses.Approved && order.CreatedByStaffId == actorStaffId)
                return Fail("Bạn không thể tự duyệt đơn đặt hàng do chính mình tạo.");
            if (!await CanAccessStoreAsync(actorStaffId, order.StoreId))
                return Fail("Bạn không có quyền cập nhật đơn mua hàng của cửa hàng này.");
            if (next == PurchaseOrderStatuses.MarkedAsSent
                && order.Status is (PurchaseOrderStatuses.MarkedAsSent or PurchaseOrderStatuses.PartiallyReceived or PurchaseOrderStatuses.Completed))
            {
                return ServiceResult<PurchaseOrderDetailDto>.Success(
                    await MapAsync(id),
                    "Đơn đặt hàng đã được gửi nhà cung cấp trước đó.");
            }
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
            var message = next == PurchaseOrderStatuses.Approved
                ? "Đã duyệt đơn đặt hàng."
                : next == PurchaseOrderStatuses.MarkedAsSent
                    ? "Đã đánh dấu đơn đặt hàng đã gửi nhà cung cấp."
                    : "Đã cập nhật đơn đặt hàng.";
            return ServiceResult<PurchaseOrderDetailDto>.Success(await MapAsync(id), message);
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
            var order = await _context.PurchaseOrders.AsNoTracking().AsSplitQuery()
                .Include(x => x.Store)
                .Include(x => x.Supplier).ThenInclude(s => s.Contacts)
                .Include(x => x.Supplier).ThenInclude(s => s.Phones)
                .Include(x => x.CreatedByStaff)
                .Include(x => x.ApprovedByStaff)
                .Include(x => x.SentByStaff)
                .Include(x => x.Lines).ThenInclude(x => x.Ingredient).ThenInclude(x => x.BaseUnit)
                .Include(x => x.Lines).ThenInclude(x => x.PackageUnitSnapshot)
                .Include(x => x.Lines).ThenInclude(x => x.ProcurementUnit)
                .Include(x => x.Lines).ThenInclude(x => x.ReceiptPostings)
                    .ThenInclude(x => x.BranchReceiptLine)
                .Include(x => x.Lines).ThenInclude(x => x.Closures)
                    .ThenInclude(x => x.ActorStaff)
                .SingleOrDefaultAsync(x => x.PurchaseOrderId == id);
            if (order == null) return new PurchaseOrderDetailDto();
            var restockIds = order.Lines
                .Where(x => x.RestockRequestId.HasValue)
                .Select(x => x.RestockRequestId!.Value)
                .Distinct()
                .ToArray();
            var restockReferences = await _context.RestockRequests.AsNoTracking()
                .Where(x => restockIds.Contains(x.RestockRequestId))
                .ToDictionaryAsync(x => x.RestockRequestId, x => x.ReferenceCode);
            var adviceIds = order.Lines
                .Where(x => x.PurchaseAdviceLineId.HasValue)
                .Select(x => x.PurchaseAdviceLineId!.Value)
                .Distinct()
                .ToArray();
            var linkedAdvices = await _context.PurchaseAdviceLines.AsNoTracking()
                .Where(x => adviceIds.Contains(x.PurchaseAdviceLineId))
                .Select(x => new PurchaseOrderLinkedAdviceDto
                {
                    PurchaseAdviceId = x.PurchaseAdviceId,
                    AdviceNumber = x.PurchaseAdvice.AdviceNumber
                })
                .Distinct()
                .OrderBy(x => x.AdviceNumber)
                .ToListAsync();
            var receiptRows = await _context.BranchReceipts.AsNoTracking()
                .Include(x => x.Lines)
                .Where(x => x.PurchaseOrderId == order.PurchaseOrderId)
                .OrderBy(x => x.CreatedAt)
                .ToListAsync();
            var receipts = receiptRows.Select(x => new PurchaseOrderReceiptSummaryDto
                {
                    BranchReceiptId = x.BranchReceiptId,
                    ReceiptCode = x.ReceiptCode,
                    Status = x.Status,
                    CreatedAt = x.CreatedAt,
                    ConfirmedAt = x.ConfirmedAt,
                    AcceptedBaseQuantity = x.Lines.Sum(line => line.ReceivedBaseQuantity),
                    RejectedBaseQuantity = x.Lines.Sum(line => line.RejectedBaseQuantity)
                })
                .ToList();
            var activeReceiptDraftId = await _context.BranchReceipts
                .AsNoTracking()
                .Where(x => x.PurchaseOrderId == order.PurchaseOrderId
                    && x.Status == BranchReceiptStatuses.Draft)
                .OrderBy(x => x.BranchReceiptId)
                .Select(x => (int?)x.BranchReceiptId)
                .FirstOrDefaultAsync();

            var firstContact = order.Supplier.Contacts.FirstOrDefault();
            var firstPhone = order.Supplier.Phones.FirstOrDefault();
            string? contactInfo = null;
            if (firstContact != null)
            {
                contactInfo = !string.IsNullOrWhiteSpace(firstContact.PhoneNumber)
                    ? $"{firstContact.Name} · {firstContact.PhoneNumber}"
                    : firstContact.Name;
            }
            else if (firstPhone != null)
            {
                contactInfo = firstPhone.PhoneNumber;
            }

            var history = new List<PurchaseOrderHistoryItemDto>
            {
                new()
                {
                    EventType = "CREATED",
                    Title = "Đơn đặt hàng được tạo",
                    Description = $"Đơn {order.Code} được tạo cho {order.Store.Name}.",
                    ActorName = order.CreatedByStaff.FullName,
                    OccurredAtUtc = order.CreatedAtUtc
                }
            };
            if (order.ApprovedAtUtc.HasValue)
                history.Add(new PurchaseOrderHistoryItemDto
                {
                    EventType = "APPROVED",
                    Title = "Đơn đặt hàng được duyệt",
                    Description = "Các điều khoản thương mại đã được duyệt.",
                    ActorName = order.ApprovedByStaff?.FullName ?? "Người có thẩm quyền",
                    OccurredAtUtc = order.ApprovedAtUtc.Value
                });
            if (order.SentAtUtc.HasValue)
                history.Add(new PurchaseOrderHistoryItemDto
                {
                    EventType = "SENT",
                    Title = "Đã gửi nhà cung cấp",
                    Description = "Nghĩa vụ giao hàng bắt đầu; thao tác này không làm tăng tồn kho.",
                    ActorName = order.SentByStaff?.FullName ?? "Kế toán kho",
                    OccurredAtUtc = order.SentAtUtc.Value
                });
            history.AddRange(receipts.Select(x => new PurchaseOrderHistoryItemDto
            {
                EventType = x.Status == BranchReceiptStatuses.Confirmed ? "RECEIPT_CONFIRMED" : "RECEIPT_CREATED",
                Title = x.Status == BranchReceiptStatuses.Confirmed
                    ? $"Đã xác nhận phiếu nhận {x.ReceiptCode}"
                    : $"Đã tạo phiếu nhận {x.ReceiptCode}",
                Description = $"Chấp nhận {x.AcceptedBaseQuantity:0.###}; từ chối {x.RejectedBaseQuantity:0.###} theo đơn vị tồn cơ sở.",
                ActorName = string.Empty,
                OccurredAtUtc = x.ConfirmedAt ?? x.CreatedAt
            }));
            history.AddRange(order.Lines.SelectMany(x => x.Closures).Select(x => new PurchaseOrderHistoryItemDto
            {
                EventType = "REMAINDER_CLOSED",
                Title = "Đã đóng một phần nghĩa vụ chưa giao",
                Description = $"Đóng {x.ClosedBaseQuantity:0.###} đơn vị cơ sở. Lý do: {x.Reason}",
                ActorName = x.ActorStaff.FullName,
                OccurredAtUtc = x.CreatedAtUtc
            }));

            return new PurchaseOrderDetailDto
            {
                PurchaseOrderId = order.PurchaseOrderId,
                Code = order.Code,
                StoreId = order.StoreId,
                StoreName = order.Store.Name,
                StoreAddress = order.Store.Address,
                StorePhone = order.Store.Phone,
                SupplierId = order.SupplierId,
                SupplierName = order.Supplier.Name,
                SupplierTaxCode = order.Supplier.TaxCode,
                SupplierAddress = order.Supplier.Address,
                SupplierContactInfo = contactInfo,
                SupplierEmail = firstContact?.Email,
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
                CreatedByStaffId = order.CreatedByStaffId,
                CreatedByStaffName = order.CreatedByStaff.FullName,
                ApprovedByStaffName = order.ApprovedByStaff?.FullName,
                SentByStaffName = order.SentByStaff?.FullName,
                CreatedAtUtc = order.CreatedAtUtc,
                ApprovedAtUtc = order.ApprovedAtUtc,
                SentAtUtc = order.SentAtUtc,
                CompletedAtUtc = order.CompletedAtUtc,
                CancelledAtUtc = order.CancelledAtUtc,
                RowVersion = Convert.ToBase64String(order.RowVersion ?? Array.Empty<byte>()),
                ActiveReceiptDraftId = activeReceiptDraftId,
                LinkedAdvices = linkedAdvices,
                Receipts = receipts,
                History = history.OrderBy(x => x.OccurredAtUtc).ToList(),
                Lines = order.Lines.Select(x =>
                {
                    var accepted = x.ReceiptPostings.Sum(p => p.AcceptedBaseQuantity);
                    var rejected = x.ReceiptPostings.Sum(p => p.RejectedBaseQuantity);
                    var packageEquivalent = x.PurchaseMode == PurchaseMode.Packaged
                        ? x.PackageCount.GetValueOrDefault() * x.PackageQuantitySnapshot.GetValueOrDefault()
                        : (decimal?)null;
                    var usesProcurementDisplay = x.PurchaseMode == PurchaseMode.Loose
                        && x.OrderedProcurementQuantity.GetValueOrDefault() > 0m
                        && !string.IsNullOrWhiteSpace(x.ProcurementUnit?.Name);
                    var displayOrdered = usesProcurementDisplay
                        ? x.OrderedProcurementQuantity!.Value
                        : packageEquivalent.GetValueOrDefault() > 0m
                            ? packageEquivalent.Value
                            : x.OrderedBaseQuantity;
                    var basePerDisplayUnit = displayOrdered > 0m
                        ? x.OrderedBaseQuantity / displayOrdered
                        : 1m;
                    var acceptedDisplay = usesProcurementDisplay
                        ? x.ReceiptPostings.Sum(p => p.AcceptedProcurementQuantity ?? 0m)
                        : accepted / basePerDisplayUnit;
                    if (usesProcurementDisplay && acceptedDisplay <= 0m && accepted > 0m)
                        acceptedDisplay = accepted / basePerDisplayUnit;
                    var rejectedDisplay = usesProcurementDisplay
                        ? x.ReceiptPostings.Sum(p => p.RejectedProcurementQuantity ?? 0m)
                        : rejected / basePerDisplayUnit;
                    if (usesProcurementDisplay && rejectedDisplay <= 0m && rejected > 0m)
                        rejectedDisplay = rejected / basePerDisplayUnit;
                    var displayUnitName = usesProcurementDisplay
                        ? x.ProcurementUnit?.Name ?? x.Ingredient.BaseUnit.Name
                        : packageEquivalent.GetValueOrDefault() > 0m
                            ? x.PackageUnitSnapshot?.Name ?? x.Ingredient.BaseUnit.Name
                            : x.Ingredient.BaseUnit.Name;
                    return new PurchaseOrderLineDto
                    {
                        PurchaseMode = x.PurchaseMode,
                        PurchaseOrderLineId = x.PurchaseOrderLineId,
                        RestockRequestId = x.RestockRequestId,
                        RestockReferenceCode = x.RestockRequestId.HasValue
                            ? restockReferences.GetValueOrDefault(x.RestockRequestId.Value)
                            : null,
                        IngredientId = x.IngredientId,
                        IngredientName = x.Ingredient.Name,
                        BaseUnitName = x.Ingredient.BaseUnit.Name,
                        PackageCount = x.PackageCount,
                        PackageQuantitySnapshot = x.PackageQuantitySnapshot,
                        PackageUnitName = x.PackageUnitSnapshot?.Name ?? string.Empty,
                        PackagePriceSnapshot = x.PackagePriceSnapshot,
                        PackageEquivalentQuantity = packageEquivalent,
                        UnitPricePerProcurementUnit = x.UnitPricePerProcurementUnit,
                        LineTotal = ProcurementPurchaseMath.CalculateLineTotal(
                            x.PurchaseMode,
                            x.PackageCount,
                            x.UnitPricePerPackage ?? x.PackagePriceSnapshot,
                            x.OrderedProcurementQuantity,
                            x.UnitPricePerProcurementUnit),
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
                        AcceptedDisplayQuantity = acceptedDisplay,
                        RejectedDisplayQuantity = rejectedDisplay,
                        ClosedDisplayQuantity = x.ClosedRemainingQuantity / basePerDisplayUnit,
                        RemainingDisplayQuantity = Math.Max(0m, x.OrderedBaseQuantity - accepted - x.ClosedRemainingQuantity) / basePerDisplayUnit,
                        FulfillmentDisplayUnitName = displayUnitName,
                        ReceiptCount = x.ReceiptPostings.Select(p => p.BranchReceiptLine.BranchReceiptId).Distinct().Count(),
                        RowVersion = Convert.ToBase64String(x.RowVersion ?? Array.Empty<byte>()),
                        PromisedLeadTimeDaysSnapshot = x.PromisedLeadTimeDaysSnapshot,
                        Closures = x.Closures.OrderBy(c => c.CreatedAtUtc).Select(c => new PurchaseOrderClosureDto
                        {
                            ClosedBaseQuantity = c.ClosedBaseQuantity,
                            DisplayQuantity = c.ClosedBaseQuantity / basePerDisplayUnit,
                            DisplayUnitName = displayUnitName,
                            Reason = c.Reason,
                            ActorName = c.ActorStaff.FullName,
                            CreatedAtUtc = c.CreatedAtUtc
                        }).ToList()
                    };
                }).ToList()
            };
        }

        private Task<bool> CanAccessStoreAsync(int actorStaffId, int storeId) =>
            _scopeAuthorization?.CanAccessStoreAsync(actorStaffId, storeId) ?? Task.FromResult(true);

        private static bool CanRead(IReadOnlyCollection<string> roles) =>
            roles.Any(x => x is RoleConstants.AccountantWarehouse or RoleConstants.BusinessOwner
                or RoleConstants.AreaManager or RoleConstants.StoreManager or RoleConstants.ShiftSupervisor
                or RoleConstants.SystemAdmin);

        private static bool CanCreate(IReadOnlyCollection<string> roles) =>
            roles.Any(x => x is RoleConstants.AccountantWarehouse or RoleConstants.SystemAdmin);

        private static bool CanApprove(IReadOnlyCollection<string> roles) =>
            roles.Contains(RoleConstants.BusinessOwner)
            || roles.Contains(RoleConstants.SystemAdmin);
        private static bool CanSend(IReadOnlyCollection<string> roles) =>
            roles.Contains(RoleConstants.AccountantWarehouse) || roles.Contains(RoleConstants.SystemAdmin);
        private static bool CanCancel(IReadOnlyCollection<string> roles) =>
            roles.Contains(RoleConstants.BusinessOwner)
            || roles.Contains(RoleConstants.AccountantWarehouse)
            || roles.Contains(RoleConstants.SystemAdmin);
        private static bool CanCloseRemaining(IReadOnlyCollection<string> roles) =>
            roles.Contains(RoleConstants.BusinessOwner) || roles.Contains(RoleConstants.SystemAdmin);

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
