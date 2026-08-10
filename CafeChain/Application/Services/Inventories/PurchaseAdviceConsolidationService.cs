using System.Data;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Procurement;
using CafeChain.Models.Inventories.Suppliers;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Inventories;

public sealed class PurchaseAdviceConsolidationService : IPurchaseAdviceConsolidationService
{
    private readonly AppDbContext _context;
    private readonly IScopeAuthorizationService _scopeAuthorization;
    private readonly IPhysicalUnitConversionService _physicalConversion;
    private readonly IUnitConversionService? _unitConversion;
    private readonly IAdminPermissionService? _permissions;
    private readonly IIngredientSupplierPackageValidator _packageEligibility;

    public PurchaseAdviceConsolidationService(
        AppDbContext context,
        IScopeAuthorizationService scopeAuthorization,
        IPhysicalUnitConversionService physicalConversion,
        IUnitConversionService? unitConversion = null,
        IAdminPermissionService? permissions = null,
        IIngredientSupplierPackageValidator? packageEligibility = null)
    {
        _context = context;
        _scopeAuthorization = scopeAuthorization;
        _physicalConversion = physicalConversion;
        _unitConversion = unitConversion;
        _permissions = permissions;
        _packageEligibility = packageEligibility
            ?? new IngredientSupplierPackageValidator(
                context,
                physicalConversion,
                unitConversion);
    }

    public async Task<ServiceResult<PurchaseAdviceConsolidationPageDto>> GetQueueAsync(
        PurchaseAdviceConsolidationFilterDto filter,
        AdminActorContext actor)
    {
        var stores = await ResolveReadableStoresAsync(actor);
        if (stores.Count == 0)
            return Failure<PurchaseAdviceConsolidationPageDto>(PurchaseAdviceErrorCodes.Forbidden, "Bạn không có quyền xem danh sách tổng hợp đề nghị mua.");

        var storeIds = stores.Select(x => x.Id).ToArray();
        if (filter.StoreId.HasValue && !storeIds.Contains(filter.StoreId.Value))
            return Failure<PurchaseAdviceConsolidationPageDto>(PurchaseAdviceErrorCodes.StoreScopeMismatch, "Chi nhánh không thuộc phạm vi truy cập của bạn.");

        var statuses = new[] { PurchaseAdviceStatuses.Submitted, PurchaseAdviceStatuses.UnderReview };
        var query = _context.PurchaseAdviceLines.AsNoTracking()
            .Where(x => storeIds.Contains(x.PurchaseAdvice.StoreId)
                && x.IsActiveReservation
                && statuses.Contains(x.PurchaseAdvice.Status));
        if (filter.StoreId.HasValue) query = query.Where(x => x.PurchaseAdvice.StoreId == filter.StoreId.Value);
        if (filter.AreaId.HasValue) query = query.Where(x => x.PurchaseAdvice.Store.ProvinceId == filter.AreaId.Value);
        if (!string.IsNullOrWhiteSpace(filter.Status) && statuses.Contains(filter.Status))
            query = query.Where(x => x.PurchaseAdvice.Status == filter.Status);
        if (filter.NeededByDate.HasValue) query = query.Where(x => x.NeededByDate.Date <= filter.NeededByDate.Value.Date);
        if (filter.IngredientId.HasValue) query = query.Where(x => x.IngredientId == filter.IngredientId.Value);
        if (!string.IsNullOrWhiteSpace(filter.Priority)) query = query.Where(x => x.PurchaseAdvice.Priority == filter.Priority);

        var rows = await query
            .OrderBy(x => x.NeededByDate)
            .ThenByDescending(x => x.PurchaseAdvice.Priority)
            .ThenBy(x => x.PurchaseAdvice.AdviceNumber)
            .Select(x => new PurchaseAdviceConsolidationLineDto
            {
                PurchaseAdviceLineId = x.PurchaseAdviceLineId,
                PurchaseAdviceId = x.PurchaseAdviceId,
                AdviceNumber = x.PurchaseAdvice.AdviceNumber,
                AdviceStatus = x.PurchaseAdvice.Status,
                StoreId = x.PurchaseAdvice.StoreId,
                StoreName = x.PurchaseAdvice.Store.Name,
                AreaId = x.PurchaseAdvice.Store.ProvinceId,
                IngredientId = x.IngredientId,
                IngredientName = x.Ingredient.Name,
                RequestedPurchaseBaseQuantity = x.RequestedPurchaseBaseQuantity,
                AllocatedToPoBaseQuantity = x.AllocatedToPoBaseQuantity,
                ClosedBaseQuantity = x.ClosedBaseQuantity,
                BaseUnitId = x.BaseUnitId,
                BaseUnitName = x.BaseUnit.Name,
                RequestedProcurementQuantity = x.RequestedProcurementQuantity,
                AllocatedToPoProcurementQuantity = x.AllocatedToPoProcurementQuantity,
                ClosedProcurementQuantity = x.ClosedProcurementQuantity,
                ProcurementUnitId = x.ProcurementUnitId,
                ProcurementUnitName = x.ProcurementUnit != null ? x.ProcurementUnit.Name : null,
                NeededByDate = x.NeededByDate,
                Priority = x.PurchaseAdvice.Priority,
                RestockRequestId = x.RestockRequestId,
                RowVersion = Convert.ToBase64String(x.RowVersion)
            })
            .ToListAsync();

        foreach (var row in rows)
        {
            row.RemainingToOrderBaseQuantity = Remaining(row.RequestedPurchaseBaseQuantity, row.AllocatedToPoBaseQuantity, row.ClosedBaseQuantity);
            row.RemainingToOrderProcurementQuantity = row.RequestedProcurementQuantity.HasValue
                ? Remaining(
                    row.RequestedProcurementQuantity.Value,
                    row.AllocatedToPoProcurementQuantity.GetValueOrDefault(),
                    row.ClosedProcurementQuantity.GetValueOrDefault())
                : null;
        }
        rows = rows.Where(x => x.RemainingToOrderProcurementQuantity.GetValueOrDefault() > 0
            || (!x.RemainingToOrderProcurementQuantity.HasValue && x.RemainingToOrderBaseQuantity > 0)).ToList();

        var ingredientIds = rows.Select(x => x.IngredientId).Distinct().ToArray();
        var procurementUnitsByIngredient = rows
            .Where(x => x.ProcurementUnitId.HasValue)
            .GroupBy(x => x.IngredientId)
            .Select(group => new
            {
                group.Key,
                UnitIds = group.Select(row => row.ProcurementUnitId!.Value).Distinct().ToArray()
            })
            .Where(x => x.UnitIds.Length == 1)
            .ToDictionary(x => x.Key, x => x.UnitIds[0]);
        var offers = await LoadOfferDtosAsync(ingredientIds, procurementUnitsByIngredient);
        var supplierStorePairs = await _context.SupplierStores.AsNoTracking()
            .Where(x => x.Active && storeIds.Contains(x.StoreId))
            .Select(x => new { x.SupplierId, x.StoreId })
            .ToListAsync();
        var compatiblePairs = supplierStorePairs.Select(x => (x.SupplierId, x.StoreId)).ToHashSet();
        foreach (var row in rows)
        {
            row.CompatibleSupplierIds = offers
                .Where(x => x.IngredientId == row.IngredientId && compatiblePairs.Contains((x.SupplierId, row.StoreId)))
                .Select(x => x.SupplierId)
                .Distinct()
                .OrderBy(x => x)
                .ToArray();
        }
        if (filter.SupplierId.HasValue)
            rows = rows.Where(x => x.CompatibleSupplierIds.Contains(filter.SupplierId.Value)).ToList();

        var areas = stores.Where(x => x.AreaId.HasValue)
            .GroupBy(x => new { Id = x.AreaId!.Value, x.AreaName })
            .Select(x => new PurchaseAdviceConsolidationOptionDto { Id = x.Key.Id, Label = x.Key.AreaName ?? $"Khu vực #{x.Key.Id}" })
            .OrderBy(x => x.Label).ToArray();
        var suppliers = offers.GroupBy(x => new { x.SupplierId, x.SupplierName })
            .Select(x => new PurchaseAdviceConsolidationOptionDto { Id = x.Key.SupplierId, Label = x.Key.SupplierName })
            .OrderBy(x => x.Label).ToArray();

        return ServiceResult<PurchaseAdviceConsolidationPageDto>.Success(new PurchaseAdviceConsolidationPageDto
        {
            Filter = filter,
            Lines = rows,
            Stores = stores.Select(x => new PurchaseAdviceConsolidationOptionDto { Id = x.Id, Label = x.Name }).ToArray(),
            Areas = areas,
            Ingredients = rows.GroupBy(x => new { x.IngredientId, x.IngredientName })
                .Select(x => new PurchaseAdviceConsolidationOptionDto { Id = x.Key.IngredientId, Label = x.Key.IngredientName })
                .OrderBy(x => x.Label).ToArray(),
            Suppliers = suppliers,
            Offers = offers,
            Actor = actor
        });
    }

    public async Task<ServiceResult<PurchaseAdviceConsolidationPreviewDto>> PreviewAsync(
        PurchaseAdviceConsolidationPreviewRequest request,
        AdminActorContext actor)
    {
        if (!await HasAnyPermissionAsync(actor, PermissionConstants.PurchaseAdviceConsolidate))
            return Failure<PurchaseAdviceConsolidationPreviewDto>(PurchaseAdviceErrorCodes.Forbidden, "Chỉ Kế toán/kho hoặc Chủ doanh nghiệp được tổng hợp đề nghị mua.");
        if (request.SupplierId <= 0 || request.Lines.Count == 0)
            return Failure<PurchaseAdviceConsolidationPreviewDto>(PurchaseAdviceErrorCodes.ConsolidationInvalid, "Hãy chọn Nhà cung cấp và ít nhất một dòng đề nghị mua.");
        if (request.Lines.Select(x => x.PurchaseAdviceLineId).Distinct().Count() != request.Lines.Count)
            return Failure<PurchaseAdviceConsolidationPreviewDto>(PurchaseAdviceErrorCodes.ConsolidationInvalid, "Mỗi dòng đề nghị mua chỉ được chọn một lần.");
        var invalidInput = request.Lines.FirstOrDefault(x =>
            (x.PurchaseMode == PurchaseMode.Packaged
                && (!ProcurementPurchaseMath.IsWholePackageCount(x.PackageCount)
                    || x.OrderedProcurementQuantity.HasValue))
            || (x.PurchaseMode == PurchaseMode.Loose
                && (x.PackageCount.HasValue || x.OrderedProcurementQuantity <= 0m)));
        if (invalidInput != null)
        {
            var message = invalidInput.PurchaseMode == PurchaseMode.Packaged
                && invalidInput.PackageCount.HasValue
                && invalidInput.OrderedProcurementQuantity.HasValue
                    ? $"Dòng đề nghị mua #{invalidInput.PurchaseAdviceLineId} đang gửi đồng thời số gói và số lượng mua rời. Vui lòng chọn đúng một phương thức mua."
                    : invalidInput.PurchaseMode == PurchaseMode.Packaged
                        ? $"Dòng đề nghị mua #{invalidInput.PurchaseAdviceLineId} phải có số gói nguyên lớn hơn 0 và không được gửi số lượng mua rời."
                        : invalidInput.PackageCount.HasValue
                            ? $"Dòng đề nghị mua #{invalidInput.PurchaseAdviceLineId} đang mua rời nên không được gửi số gói."
                            : $"Dòng đề nghị mua #{invalidInput.PurchaseAdviceLineId} phải có số lượng mua rời lớn hơn 0.";
            return Failure<PurchaseAdviceConsolidationPreviewDto>(
                PurchaseAdviceErrorCodes.ConsolidationInvalid,
                message);
        }

        var ownsTransaction = _context.Database.CurrentTransaction == null;
        await using var transaction = ownsTransaction
            ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable)
            : null;
        var supplier = await _context.Suppliers.AsNoTracking().SingleOrDefaultAsync(x => x.SupplierId == request.SupplierId);
        if (supplier == null || !supplier.Active)
            return Failure<PurchaseAdviceConsolidationPreviewDto>(PurchaseAdviceErrorCodes.SupplierInvalid, "Nhà cung cấp không tồn tại hoặc đã ngưng hoạt động.");

        var ids = request.Lines.Select(x => x.PurchaseAdviceLineId).OrderBy(x => x).ToArray();
        var lines = new List<PurchaseAdviceLine>();
        foreach (var id in ids)
        {
            IQueryable<PurchaseAdviceLine> lineQuery = _context.Database.IsSqlServer()
                ? _context.PurchaseAdviceLines.FromSqlInterpolated(
                    $"SELECT * FROM PurchaseAdviceLines WITH (UPDLOCK, HOLDLOCK, ROWLOCK) WHERE PurchaseAdviceLineId = {id}")
                : _context.PurchaseAdviceLines;
            var line = await lineQuery
                .Include(x => x.PurchaseAdvice).ThenInclude(x => x.Store)
                .Include(x => x.Ingredient).ThenInclude(x => x.BaseUnit)
                .Include(x => x.ProcurementUnit)
                .SingleOrDefaultAsync(x => x.PurchaseAdviceLineId == id);
            if (line != null) lines.Add(line);
        }
        if (lines.Count != ids.Length)
            return Failure<PurchaseAdviceConsolidationPreviewDto>(PurchaseAdviceErrorCodes.NotFound, "Một hoặc nhiều dòng đề nghị mua không còn tồn tại.");

        var offerIds = request.Lines.Select(x => x.IngredientSupplierId).Distinct().ToArray();
        var offers = await _context.IngredientSuppliers.AsNoTracking()
            .Include(x => x.Unit)
            .Include(x => x.LooseProcurementUnit)
            .Where(x => offerIds.Contains(x.IngredientSupplierId))
            .ToDictionaryAsync(x => x.IngredientSupplierId);
        var supplierStores = (await _context.SupplierStores.AsNoTracking()
            .Where(x => x.SupplierId == request.SupplierId && x.Active)
            .Select(x => x.StoreId).ToListAsync()).ToHashSet();

        var allocations = new List<(PurchaseAdviceConsolidationAllocationDto Allocation, PurchaseAdviceOfferDto Offer)>();
        foreach (var selected in request.Lines)
        {
            var line = lines.Single(x => x.PurchaseAdviceLineId == selected.PurchaseAdviceLineId);
            if (!PurchaseAdviceStatuses.ActiveReservationStatuses.Contains(line.PurchaseAdvice.Status)
                || (line.PurchaseAdvice.Status != PurchaseAdviceStatuses.Submitted && line.PurchaseAdvice.Status != PurchaseAdviceStatuses.UnderReview))
                return Failure<PurchaseAdviceConsolidationPreviewDto>(PurchaseAdviceErrorCodes.ConsolidationInvalid, $"Đề nghị {line.PurchaseAdvice.AdviceNumber} không ở trạng thái chờ tổng hợp.");
            if (!VersionMatches(line.RowVersion, selected.RowVersion))
                return Failure<PurchaseAdviceConsolidationPreviewDto>(PurchaseAdviceErrorCodes.StaleVersion, $"Dòng đề nghị {line.PurchaseAdvice.AdviceNumber} đã thay đổi. Hãy tải lại.");
            if (!supplierStores.Contains(line.PurchaseAdvice.StoreId))
                return Failure<PurchaseAdviceConsolidationPreviewDto>(PurchaseAdviceErrorCodes.SupplierStoreMismatch, $"Nhà cung cấp không phục vụ chi nhánh {line.PurchaseAdvice.Store.Name}.");
            if (!offers.TryGetValue(selected.IngredientSupplierId, out var offer)
                || !offer.Active || offer.SupplierId != request.SupplierId || offer.IngredientId != line.IngredientId)
                return Failure<PurchaseAdviceConsolidationPreviewDto>(PurchaseAdviceErrorCodes.OfferInvalid, $"Quy cách cung cấp cho {line.Ingredient.Name} không hợp lệ hoặc đã hết hiệu lực.");

            var eligibility = await _packageEligibility.EvaluateProcurementEligibilityAsync(
                offer,
                selected.PurchaseMode,
                line.PurchaseAdvice.StoreId);
            if (!eligibility.IsProcurementEligible)
            {
                return Failure<PurchaseAdviceConsolidationPreviewDto>(
                    PurchaseAdviceErrorCodes.OfferInvalid,
                    $"Gói mua của {line.Ingredient.Name} chưa sẵn sàng. {eligibility.Message}");
            }

            if (selected.PurchaseMode == PurchaseMode.Loose)
            {
                if (!offer.AllowsLoosePurchase
                    || offer.CurrentProcurementUnitPrice <= 0m
                    || !offer.LooseProcurementUnitId.HasValue
                    || !line.ProcurementUnitId.HasValue
                    || offer.LooseProcurementUnitId != line.ProcurementUnitId)
                {
                    return Failure<PurchaseAdviceConsolidationPreviewDto>(
                        PurchaseAdviceErrorCodes.OfferInvalid,
                        $"{line.Ingredient.Name} chưa được Nhà cung cấp cho phép mua rời theo đúng đơn vị yêu cầu.");
                }

                var remainingProcurement = Remaining(
                    line.RequestedProcurementQuantity.GetValueOrDefault(),
                    line.AllocatedToPoProcurementQuantity,
                    line.ClosedProcurementQuantity);
                var demandProcurement = selected.OrderedProcurementQuantity!.Value;
                if (remainingProcurement <= 0m || demandProcurement > remainingProcurement)
                {
                    return Failure<PurchaseAdviceConsolidationPreviewDto>(
                        PurchaseAdviceErrorCodes.PackageCountMismatch,
                        $"{line.Ingredient.Name} chỉ còn {remainingProcurement:N3} {line.ProcurementUnit?.Name}; không được mua rời vượt nhu cầu.");
                }

                if (!LoosePurchaseMath.TryPlan(
                        demandProcurement,
                        offer.LooseMinimumOrderQuantity,
                        offer.LooseQuantityStep,
                        out var loosePlan))
                {
                    return Failure<PurchaseAdviceConsolidationPreviewDto>(
                        PurchaseAdviceErrorCodes.MoqViolation,
                        $"Không thể áp dụng MOQ hoặc bước số lượng mua lẻ cho {line.Ingredient.Name}.");
                }

                var orderedProcurement = loosePlan.OrderedQuantity;

                var looseBaseConversion = await ConvertForIngredientAsync(
                    line.IngredientId,
                    orderedProcurement,
                    offer.LooseProcurementUnitId.Value,
                    line.BaseUnitId);
                if (!looseBaseConversion.IsSuccess || looseBaseConversion.Data <= 0m)
                {
                    return Failure<PurchaseAdviceConsolidationPreviewDto>(
                        PurchaseAdviceErrorCodes.PackageMismatch,
                        $"Số lượng mua rời của {line.Ingredient.Name} không quy đổi được sang {line.BaseUnit.Name}.");
                }

                var demandBaseConversion = orderedProcurement == demandProcurement
                    ? looseBaseConversion
                    : await ConvertForIngredientAsync(
                        line.IngredientId,
                        demandProcurement,
                        offer.LooseProcurementUnitId.Value,
                        line.BaseUnitId);
                if (!demandBaseConversion.IsSuccess || demandBaseConversion.Data <= 0m)
                {
                    return Failure<PurchaseAdviceConsolidationPreviewDto>(
                        PurchaseAdviceErrorCodes.PackageMismatch,
                        $"Nhu cầu mua lẻ của {line.Ingredient.Name} không quy đổi được sang {line.BaseUnit.Name}.");
                }

                allocations.Add((new PurchaseAdviceConsolidationAllocationDto
                {
                    PurchaseMode = PurchaseMode.Loose,
                    PurchaseAdviceLineId = line.PurchaseAdviceLineId,
                    AdviceNumber = line.PurchaseAdvice.AdviceNumber,
                    StoreId = line.PurchaseAdvice.StoreId,
                    StoreName = line.PurchaseAdvice.Store.Name,
                    RestockRequestId = line.RestockRequestId,
                    SuggestedPackageCount = null,
                    PackageCount = null,
                    DemandCoveredBaseQuantity = demandBaseConversion.Data,
                    OrderedBaseQuantity = looseBaseConversion.Data,
                    RoundingSurplusBaseQuantity = looseBaseConversion.Data - demandBaseConversion.Data,
                    DemandCoveredProcurementQuantity = demandProcurement,
                    OrderedProcurementQuantity = orderedProcurement,
                    RoundingSurplusProcurementQuantity = loosePlan.RoundingSurplusQuantity,
                    ProcurementUnitId = line.ProcurementUnitId,
                    ProcurementUnitName = line.ProcurementUnit?.Name,
                    AllocatedBaseQuantity = looseBaseConversion.Data,
                    RemainingBeforeAllocation = line.RequestedPurchaseBaseQuantity > 0m
                        && line.RequestedProcurementQuantity > 0m
                        ? remainingProcurement * line.RequestedPurchaseBaseQuantity / line.RequestedProcurementQuantity.Value
                        : looseBaseConversion.Data,
                    RemainingProcurementBeforeAllocation = remainingProcurement,
                    NeededByDate = line.NeededByDate,
                    LineRowVersion = Convert.ToBase64String(line.RowVersion)
                }, new PurchaseAdviceOfferDto
                {
                    IngredientSupplierId = offer.IngredientSupplierId,
                    SupplierId = offer.SupplierId,
                    SupplierName = supplier.Name ?? $"Nhà cung cấp #{supplier.SupplierId}",
                    IngredientId = offer.IngredientId,
                    PackageUnitId = offer.UnitId,
                    PackageUnitName = offer.Unit.Name,
                    PackageQuantity = offer.PackageQuantity,
                    PackageBaseQuantity = 0m,
                    ProcurementUnitId = offer.LooseProcurementUnitId,
                    ProcurementUnitName = offer.LooseProcurementUnit?.Name,
                    MinimumOrderPackageCount = 0,
                    LeadTimeDays = offer.LeadTimeDays ?? 0,
                    CurrentPackagePrice = offer.CurrentPrice,
                    AllowsLoosePurchase = true,
                    CurrentProcurementUnitPrice = offer.CurrentProcurementUnitPrice,
                    LooseProcurementUnitId = offer.LooseProcurementUnitId,
                    LooseProcurementUnitName = offer.LooseProcurementUnit?.Name,
                    LooseMinimumOrderQuantity = offer.LooseMinimumOrderQuantity,
                    LooseQuantityStep = offer.LooseQuantityStep,
                    Specification = offer.Note
                }));
                continue;
            }

            if (!offer.PackageQuantity.HasValue || offer.PackageQuantity <= 0m || offer.CurrentPrice <= 0m)
                return Failure<PurchaseAdviceConsolidationPreviewDto>(PurchaseAdviceErrorCodes.OfferInvalid, $"Quy cách đóng gói cho {line.Ingredient.Name} không hợp lệ hoặc đã hết hiệu lực.");

            var baseConversion = await ConvertForIngredientAsync(
                line.IngredientId,
                offer.PackageQuantity.Value,
                offer.UnitId,
                line.BaseUnitId);
            if (!baseConversion.IsSuccess || baseConversion.Data <= 0)
                return Failure<PurchaseAdviceConsolidationPreviewDto>(PurchaseAdviceErrorCodes.PackageMismatch, $"Gói mua của {line.Ingredient.Name} không quy đổi được sang {line.BaseUnit.Name}.");

            var usesProcurementContract = line.RequestedProcurementQuantity.HasValue
                && line.ProcurementUnitId.HasValue;
            decimal remainingForPlan;
            decimal packageQuantityForPlan;
            decimal? packageProcurementQuantity = null;
            if (usesProcurementContract)
            {
                var procurementQuantity = offer.UnitId == line.BaseUnitId
                    && line.RequestedPurchaseBaseQuantity > 0
                    ? offer.PackageQuantity.Value
                        * line.RequestedProcurementQuantity!.Value
                        / line.RequestedPurchaseBaseQuantity
                    : 0m;
                if (procurementQuantity <= 0)
                {
                    var procurementConversion = await ConvertForIngredientAsync(
                        line.IngredientId,
                        offer.PackageQuantity.Value,
                        offer.UnitId,
                        line.ProcurementUnitId!.Value);
                    if (procurementConversion.IsSuccess)
                        procurementQuantity = procurementConversion.Data;
                }
                if (procurementQuantity <= 0)
                {
                    return Failure<PurchaseAdviceConsolidationPreviewDto>(
                        PurchaseAdviceErrorCodes.PackageMismatch,
                        $"Gói mua của {line.Ingredient.Name} không quy đổi được sang {line.ProcurementUnit?.Name ?? "đơn vị mua hàng"}.");
                }

                packageProcurementQuantity = procurementQuantity;
                remainingForPlan = Remaining(
                    line.RequestedProcurementQuantity!.Value,
                    line.AllocatedToPoProcurementQuantity,
                    line.ClosedProcurementQuantity);
                packageQuantityForPlan = packageProcurementQuantity.Value;
            }
            else
            {
                remainingForPlan = Remaining(
                    line.RequestedPurchaseBaseQuantity,
                    line.AllocatedToPoBaseQuantity,
                    line.ClosedBaseQuantity);
                packageQuantityForPlan = baseConversion.Data;
            }

            if (!PurchasePackMath.TryPlan(remainingForPlan, packageQuantityForPlan, out var packPlan))
                return Failure<PurchaseAdviceConsolidationPreviewDto>(PurchaseAdviceErrorCodes.PackageMismatch, $"Không thể tính quy cách mua cho {line.Ingredient.Name}.");
            if (selected.PackageCount > packPlan.PackageCount)
            {
                var planningUnit = usesProcurementContract
                    ? line.ProcurementUnit?.Name
                    : line.BaseUnit.Name;
                return Failure<PurchaseAdviceConsolidationPreviewDto>(
                    PurchaseAdviceErrorCodes.PackageCountMismatch,
                    $"{line.Ingredient.Name} chỉ cần tối đa {packPlan.PackageCount} gói để phủ {remainingForPlan:N3} {planningUnit}; mua vượt đề xuất cần luồng override riêng.");
            }

            decimal? orderedProcurementQuantity = null;
            decimal? demandCoveredProcurementQuantity = null;
            decimal? roundingSurplusProcurementQuantity = null;
            if (usesProcurementContract)
            {
                orderedProcurementQuantity = packageQuantityForPlan * selected.PackageCount!.Value;
                demandCoveredProcurementQuantity = Math.Min(
                    remainingForPlan,
                    orderedProcurementQuantity.Value);
                roundingSurplusProcurementQuantity = Math.Max(
                    0m,
                    orderedProcurementQuantity.Value - remainingForPlan);
            }

            var procurementFactor = line.RequestedProcurementQuantity.HasValue
                && line.RequestedProcurementQuantity.Value > 0
                && line.RequestedPurchaseBaseQuantity > 0
                ? line.RequestedPurchaseBaseQuantity / line.RequestedProcurementQuantity.Value
                : (decimal?)null;
            var orderedBaseQuantity = usesProcurementContract && procurementFactor.HasValue
                ? orderedProcurementQuantity!.Value * procurementFactor.Value
                : baseConversion.Data * selected.PackageCount!.Value;
            var demandCoveredBaseQuantity = usesProcurementContract && procurementFactor.HasValue
                ? demandCoveredProcurementQuantity!.Value * procurementFactor.Value
                : Math.Min(remainingForPlan, orderedBaseQuantity);
            var roundingSurplusBaseQuantity = Math.Max(
                0m,
                orderedBaseQuantity - demandCoveredBaseQuantity);

            allocations.Add((new PurchaseAdviceConsolidationAllocationDto
            {
                PurchaseMode = PurchaseMode.Packaged,
                PurchaseAdviceLineId = line.PurchaseAdviceLineId,
                AdviceNumber = line.PurchaseAdvice.AdviceNumber,
                StoreId = line.PurchaseAdvice.StoreId,
                StoreName = line.PurchaseAdvice.Store.Name,
                RestockRequestId = line.RestockRequestId,
                SuggestedPackageCount = packPlan.PackageCount,
                PackageCount = selected.PackageCount,
                DemandCoveredBaseQuantity = demandCoveredBaseQuantity,
                OrderedBaseQuantity = orderedBaseQuantity,
                RoundingSurplusBaseQuantity = roundingSurplusBaseQuantity,
                DemandCoveredProcurementQuantity = demandCoveredProcurementQuantity,
                OrderedProcurementQuantity = orderedProcurementQuantity,
                RoundingSurplusProcurementQuantity = roundingSurplusProcurementQuantity,
                ProcurementUnitId = line.ProcurementUnitId,
                ProcurementUnitName = line.ProcurementUnit?.Name,
                AllocatedBaseQuantity = orderedBaseQuantity,
                RemainingBeforeAllocation = usesProcurementContract && procurementFactor.HasValue
                    ? remainingForPlan * procurementFactor.Value
                    : remainingForPlan,
                RemainingProcurementBeforeAllocation = usesProcurementContract
                    ? remainingForPlan
                    : null,
                NeededByDate = line.NeededByDate,
                LineRowVersion = Convert.ToBase64String(line.RowVersion)
            }, new PurchaseAdviceOfferDto
            {
                IngredientSupplierId = offer.IngredientSupplierId,
                SupplierId = offer.SupplierId,
                SupplierName = supplier.Name ?? $"Nhà cung cấp #{supplier.SupplierId}",
                IngredientId = offer.IngredientId,
                PackageUnitId = offer.UnitId,
                PackageUnitName = offer.Unit.Name,
                PackageQuantity = offer.PackageQuantity.Value,
                PackageBaseQuantity = baseConversion.Data,
                PackageProcurementQuantity = packageProcurementQuantity,
                ProcurementUnitId = line.ProcurementUnitId,
                ProcurementUnitName = line.ProcurementUnit?.Name,
                MinimumOrderPackageCount = offer.MinimumOrderPackageCount ?? 1,
                LeadTimeDays = offer.LeadTimeDays ?? 0,
                CurrentPackagePrice = offer.CurrentPrice,
                AllowsLoosePurchase = offer.AllowsLoosePurchase,
                CurrentProcurementUnitPrice = offer.CurrentProcurementUnitPrice,
                LooseProcurementUnitId = offer.LooseProcurementUnitId,
                LooseProcurementUnitName = offer.LooseProcurementUnit?.Name,
                LooseMinimumOrderQuantity = offer.LooseMinimumOrderQuantity,
                LooseQuantityStep = offer.LooseQuantityStep,
                Specification = offer.Note
            }));
        }

        var groups = allocations
            .GroupBy(x => new
            {
                x.Offer.IngredientId,
                x.Offer.IngredientSupplierId,
                x.Allocation.PurchaseMode,
                x.Offer.PackageUnitId,
                x.Offer.PackageUnitName,
                x.Offer.PackageQuantity,
                x.Offer.PackageBaseQuantity,
                x.Offer.PackageProcurementQuantity,
                x.Offer.ProcurementUnitId,
                x.Offer.ProcurementUnitName,
                x.Offer.CurrentPackagePrice,
                x.Offer.CurrentProcurementUnitPrice,
                x.Offer.Currency,
                x.Offer.Specification,
                x.Offer.MinimumOrderPackageCount,
                x.Offer.LeadTimeDays
            })
            .Select(group =>
            {
                var count = group.Key.PurchaseMode == PurchaseMode.Packaged
                    ? group.Sum(x => x.Allocation.PackageCount.GetValueOrDefault())
                    : (int?)null;
                var orderedProcurement = group.All(x => x.Allocation.OrderedProcurementQuantity.HasValue)
                    ? group.Sum(x => x.Allocation.OrderedProcurementQuantity!.Value)
                    : (decimal?)null;
                return new PurchaseAdviceConsolidationGroupDto
                {
                    PurchaseMode = group.Key.PurchaseMode,
                    IngredientId = group.Key.IngredientId,
                    IngredientName = lines.First(x => x.IngredientId == group.Key.IngredientId).Ingredient.Name,
                    IngredientSupplierId = group.Key.IngredientSupplierId,
                    PackageUnitId = group.Key.PackageUnitId,
                    PackageUnitName = group.Key.PackageUnitName,
                    PackageQuantity = group.Key.PackageQuantity,
                    PackageBaseQuantity = group.Key.PackageBaseQuantity,
                    PackageProcurementQuantity = group.Key.PackageProcurementQuantity,
                    ProcurementUnitId = group.Key.ProcurementUnitId,
                    ProcurementUnitName = group.Key.ProcurementUnitName,
                    PackagePriceSnapshot = group.Key.PurchaseMode == PurchaseMode.Packaged
                        ? group.Key.CurrentPackagePrice
                        : null,
                    UnitPricePerProcurementUnit = group.Key.PurchaseMode == PurchaseMode.Loose
                        ? group.Key.CurrentProcurementUnitPrice
                        : null,
                    Currency = group.Key.Currency,
                    Specification = group.Key.Specification,
                    LeadTimeDays = group.Key.LeadTimeDays,
                    MinimumOrderPackageCount = group.Key.MinimumOrderPackageCount,
                    PackageCount = count,
                DemandCoveredBaseQuantity = group.Sum(x => x.Allocation.DemandCoveredBaseQuantity),
                OrderedBaseQuantity = group.Sum(x => x.Allocation.OrderedBaseQuantity),
                RoundingSurplusBaseQuantity = group.Sum(x => x.Allocation.RoundingSurplusBaseQuantity),
                DemandCoveredProcurementQuantity = group.All(x => x.Allocation.DemandCoveredProcurementQuantity.HasValue)
                    ? group.Sum(x => x.Allocation.DemandCoveredProcurementQuantity!.Value)
                    : null,
                OrderedProcurementQuantity = orderedProcurement,
                RoundingSurplusProcurementQuantity = group.All(x => x.Allocation.RoundingSurplusProcurementQuantity.HasValue)
                    ? group.Sum(x => x.Allocation.RoundingSurplusProcurementQuantity!.Value)
                    : null,
                AllocatedBaseQuantity = group.Sum(x => x.Allocation.OrderedBaseQuantity),
                    LineTotal = ProcurementPurchaseMath.CalculateLineTotal(
                        group.Key.PurchaseMode,
                        count,
                        group.Key.PurchaseMode == PurchaseMode.Packaged ? group.Key.CurrentPackagePrice : null,
                        orderedProcurement,
                        group.Key.CurrentProcurementUnitPrice),
                    Allocations = group.Select(x => x.Allocation).OrderBy(x => x.StoreName).ToArray()
                };
            }).ToArray();
        var moqFailure = groups.FirstOrDefault(group =>
            group.PurchaseMode == PurchaseMode.Packaged
            && group.PackageCount < allocations.First(x => x.Offer.IngredientSupplierId == group.IngredientSupplierId).Offer.MinimumOrderPackageCount);
        if (moqFailure != null)
        {
            var moq = allocations.First(x => x.Offer.IngredientSupplierId == moqFailure.IngredientSupplierId).Offer.MinimumOrderPackageCount;
            return Failure<PurchaseAdviceConsolidationPreviewDto>(PurchaseAdviceErrorCodes.MoqViolation, $"{moqFailure.IngredientName} yêu cầu tối thiểu {moq} gói.");
        }

        var warnings = allocations
            .Where(x => DateTime.UtcNow.Date.AddDays(x.Offer.LeadTimeDays) > x.Allocation.NeededByDate.Date)
            .Select(x => $"{x.Allocation.AdviceNumber}: thời gian giao dự kiến vượt ngày cần hàng.")
            .Distinct().ToArray();
        if (transaction != null) await transaction.CommitAsync();
        return ServiceResult<PurchaseAdviceConsolidationPreviewDto>.Success(new PurchaseAdviceConsolidationPreviewDto
        {
            SupplierId = supplier.SupplierId,
            SupplierName = supplier.Name ?? $"Nhà cung cấp #{supplier.SupplierId}",
            Groups = groups,
            TotalAmount = groups.Sum(x => x.LineTotal),
            StoreCount = allocations.Select(x => x.Allocation.StoreId).Distinct().Count(),
            LineCount = allocations.Count,
            Warnings = warnings
        });
    }

    private async Task<List<PurchaseAdviceOfferDto>> LoadOfferDtosAsync(
        int[] ingredientIds,
        IReadOnlyDictionary<int, int> procurementUnitsByIngredient)
    {
        var offers = await _context.IngredientSuppliers.AsNoTracking()
            .Where(x => ingredientIds.Contains(x.IngredientId) && x.Active && x.Supplier.Active
                && ((x.PackageQuantity.HasValue && x.PackageQuantity > 0 && x.CurrentPrice > 0 && x.Unit.Active)
                    || (x.AllowsLoosePurchase && x.CurrentProcurementUnitPrice > 0 && x.LooseProcurementUnitId.HasValue)))
            .Include(x => x.Supplier).Include(x => x.Unit).Include(x => x.LooseProcurementUnit).Include(x => x.Ingredient)
            .OrderBy(x => x.Supplier.Name).ThenBy(x => x.Ingredient.Name)
            .ToListAsync();
        var readinessById = await _packageEligibility.EvaluateReadinessAsync(offers);
        offers = offers
            .Where(x => readinessById.TryGetValue(x.IngredientSupplierId, out var readiness)
                && readiness.IsReady)
            .ToList();
        var result = new List<PurchaseAdviceOfferDto>();
        foreach (var offer in offers)
        {
            decimal packageBaseQuantity = 0m;
            if (offer.PackageQuantity > 0m && offer.CurrentPrice > 0m)
            {
                var conversion = await ConvertForIngredientAsync(
                    offer.IngredientId,
                    offer.PackageQuantity.Value,
                    offer.UnitId,
                    offer.Ingredient.BaseUnitId);
                if (conversion.IsSuccess && conversion.Data > 0m)
                    packageBaseQuantity = conversion.Data;
            }
            if (packageBaseQuantity <= 0m && !offer.AllowsLoosePurchase)
                continue;
            decimal? packageProcurementQuantity = null;
            int? procurementUnitId = null;
            string? procurementUnitName = null;
            if (procurementUnitsByIngredient.TryGetValue(offer.IngredientId, out var targetProcurementUnitId))
            {
                if (offer.PackageQuantity > 0m)
                {
                    var procurementConversion = await ConvertForIngredientAsync(
                        offer.IngredientId,
                        offer.PackageQuantity.Value,
                        offer.UnitId,
                        targetProcurementUnitId);
                    if (procurementConversion.IsSuccess && procurementConversion.Data > 0m)
                        packageProcurementQuantity = procurementConversion.Data;
                }
                procurementUnitId = targetProcurementUnitId;
                procurementUnitName = await _context.Units.AsNoTracking()
                    .Where(x => x.UnitId == targetProcurementUnitId)
                    .Select(x => x.Name)
                    .SingleOrDefaultAsync();
            }
            result.Add(new PurchaseAdviceOfferDto
            {
                IngredientSupplierId = offer.IngredientSupplierId,
                SupplierId = offer.SupplierId,
                SupplierName = offer.Supplier.Name ?? $"Nhà cung cấp #{offer.SupplierId}",
                IngredientId = offer.IngredientId,
                PackageUnitId = offer.UnitId,
                PackageUnitName = offer.Unit.Name,
                PackageQuantity = offer.PackageQuantity,
                PackageBaseQuantity = packageBaseQuantity,
                PackageProcurementQuantity = packageProcurementQuantity,
                ProcurementUnitId = procurementUnitId,
                ProcurementUnitName = procurementUnitName,
                MinimumOrderPackageCount = offer.MinimumOrderPackageCount ?? 1,
                LeadTimeDays = offer.LeadTimeDays ?? 0,
                CurrentPackagePrice = offer.CurrentPrice,
                AllowsLoosePurchase = offer.AllowsLoosePurchase
                    && offer.LooseProcurementUnitId == procurementUnitId
                    && offer.CurrentProcurementUnitPrice > 0m,
                CurrentProcurementUnitPrice = offer.CurrentProcurementUnitPrice,
                LooseProcurementUnitId = offer.LooseProcurementUnitId,
                LooseProcurementUnitName = offer.LooseProcurementUnit?.Name,
                Specification = offer.Note
            });
        }
        return result;
    }

    private async Task<List<ReadableStore>> ResolveReadableStoresAsync(AdminActorContext actor)
    {
        if (_permissions == null || actor.AccountId <= 0) return new();
        var storeRows = await _context.Stores.AsNoTracking().Where(x => x.Active)
            .OrderBy(x => x.Name)
            .Select(x => new { x.StoreId, x.Name, x.ProvinceId, AreaName = x.Province != null ? x.Province.Name : null })
            .ToListAsync();
        var allowed = new List<ReadableStore>();
        foreach (var store in storeRows)
            if (await HasPermissionAsync(actor, store.StoreId, PermissionConstants.PurchaseAdviceConsolidate)
                && await _scopeAuthorization.CanAccessStoreAsync(actor.StaffId, store.StoreId))
                allowed.Add(new ReadableStore(store.StoreId, store.Name, store.ProvinceId, store.AreaName));
        return allowed;
    }

    private Task<ServiceResult<decimal>> ConvertForIngredientAsync(
        int ingredientId,
        decimal quantity,
        int fromUnitId,
        int toUnitId) =>
        _unitConversion != null
            ? _unitConversion.ConvertAsync(ingredientId, quantity, fromUnitId, toUnitId)
            : _physicalConversion.ConvertAsync(quantity, fromUnitId, toUnitId);

    private static decimal Remaining(decimal requested, decimal allocated, decimal closed) => Math.Max(0m, requested - allocated - closed);
    private async Task<bool> HasAnyPermissionAsync(AdminActorContext actor, string code)
    {
        if (_permissions == null || actor.AccountId <= 0) return false;
        var effective = await _permissions.GetEffectivePermissionCodesAsync(actor.AccountId);
        return effective.IsSuccess && effective.Data?.Contains(code) == true;
    }

    private async Task<bool> HasPermissionAsync(AdminActorContext actor, int storeId, string code)
    {
        if (_permissions == null || actor.AccountId <= 0) return false;
        var decision = await _permissions.HasPermissionAsync(actor.AccountId, code, storeId);
        return decision.IsSuccess && decision.Data?.Allowed == true;
    }
    private static bool VersionMatches(byte[] current, string? provided)
    {
        if (current.Length == 0 && string.IsNullOrWhiteSpace(provided)) return true;
        if (string.IsNullOrWhiteSpace(provided)) return false;
        try { return current.SequenceEqual(Convert.FromBase64String(provided)); }
        catch (FormatException) { return false; }
    }
    private static ServiceResult<T> Failure<T>(string code, string message) => ServiceResult<T>.Failure(message, errorCode: code);
    private sealed record ReadableStore(int Id, string Name, int? AreaId, string? AreaName);
}
