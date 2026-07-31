using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.DTOs.AI;
using CafeChain.Application.Interfaces.AI;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Results;
using CafeChain.Infrastructure.Interfaces.Admin.Procurement;
using CafeChain.Models.Inventories.Suppliers;

namespace CafeChain.Application.Services.Inventories;

/// <summary>
/// The single deterministic source of truth for reorder suggestions.
/// No consumer is allowed to recalculate these quantities independently.
/// </summary>
public sealed class ReorderSuggestionService : IReorderSuggestionService
{
    private readonly IReorderSuggestionRepository _repository;
    private readonly IPhysicalUnitConversionService _conversion;
    private readonly IReorderIncomingQuantityProvider _incomingProvider;
    private readonly IAIService _aiService;
    private readonly IReorderSuggestionAuthorizationService? _authorization;
    private readonly TimeProvider _clock;

    public ReorderSuggestionService(
        IReorderSuggestionRepository repository,
        IPhysicalUnitConversionService conversion,
        IReorderIncomingQuantityProvider incomingProvider,
        IAIService aiService,
        IReorderSuggestionAuthorizationService? authorization = null,
        TimeProvider? clock = null)
    {
        _repository = repository;
        _conversion = conversion;
        _incomingProvider = incomingProvider;
        _aiService = aiService;
        _authorization = authorization;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<ServiceResult<ReorderSuggestionListDto>> CalculateForStoreAsync(
        int storeId,
        int analysisWindowDays = 30,
        DateTime? analysisToUtc = null,
        IReadOnlyCollection<int>? ingredientIds = null,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateCalculationRequest(storeId, analysisWindowDays);
        if (validation != null)
            return ServiceResult<ReorderSuggestionListDto>.Failure(validation);

        var toUtc = NormalizeUtc(analysisToUtc ?? _clock.GetUtcNow().UtcDateTime);
        var fromUtc = toUtc.AddDays(-analysisWindowDays);
        var data = await _repository.GetCalculationDataAsync(
            storeId,
            ingredientIds,
            fromUtc,
            toUtc,
            cancellationToken);
        if (data == null)
            return ServiceResult<ReorderSuggestionListDto>.Failure("Không tìm thấy cửa hàng.");

        var externalIncoming = await _incomingProvider.GetIncomingBaseQuantitiesAsync(
            storeId,
            data.Inventories.Select(x => x.IngredientId).Distinct().ToArray());
        var result = await CalculateAsync(
            data,
            analysisWindowDays,
            fromUtc,
            toUtc,
            externalIncoming,
            cancellationToken);
        return ServiceResult<ReorderSuggestionListDto>.Success(result);
    }

    public async Task<ServiceResult<IReadOnlyList<ReorderSuggestionListDto>>> CalculateForStoresAsync(
        IReadOnlyCollection<int> storeIds,
        int analysisWindowDays = 30,
        DateTime? analysisToUtc = null,
        CancellationToken cancellationToken = default)
    {
        var ids = storeIds?.Where(x => x > 0).Distinct().OrderBy(x => x).ToArray()
                  ?? Array.Empty<int>();
        if (ids.Length == 0)
            return ServiceResult<IReadOnlyList<ReorderSuggestionListDto>>.Failure("Danh sách cửa hàng không hợp lệ.");
        if (analysisWindowDays is < 1 or > 365)
            return ServiceResult<IReadOnlyList<ReorderSuggestionListDto>>.Failure(
                "Khoảng phân tích phải từ 1 đến 365 ngày.");

        var toUtc = NormalizeUtc(analysisToUtc ?? _clock.GetUtcNow().UtcDateTime);
        var fromUtc = toUtc.AddDays(-analysisWindowDays);
        var rows = await _repository.GetCalculationDataForStoresAsync(
            ids,
            fromUtc,
            toUtc,
            cancellationToken);

        var results = new List<ReorderSuggestionListDto>(rows.Count);
        foreach (var row in rows.OrderBy(x => x.Store.StoreId))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var externalIncoming = await _incomingProvider.GetIncomingBaseQuantitiesAsync(
                row.Store.StoreId,
                row.Inventories.Select(x => x.IngredientId).Distinct().ToArray());
            results.Add(await CalculateAsync(
                row,
                analysisWindowDays,
                fromUtc,
                toUtc,
                externalIncoming,
                cancellationToken));
        }

        return ServiceResult<IReadOnlyList<ReorderSuggestionListDto>>.Success(results);
    }

    public async Task<ServiceResult<ReorderSuggestionListDto>> GetForStoreAsync(
        int storeId,
        AdminActorContext actor,
        int analysisWindowDays = 30,
        CancellationToken cancellationToken = default)
    {
        if (_authorization == null
            || !await _authorization.CanViewAsync(actor, storeId, cancellationToken))
        {
            return ServiceResult<ReorderSuggestionListDto>.Failure(
                "Bạn không có quyền xem gợi ý nhập hàng của cửa hàng này.");
        }

        return await CalculateForStoreAsync(
            storeId,
            analysisWindowDays,
            cancellationToken: cancellationToken);
    }

    public async Task<ServiceResult<InventoryReorderExplanationResultDto>> ExplainAsync(
        int storeId,
        int ingredientId,
        AdminActorContext actor,
        int analysisWindowDays = 30,
        CancellationToken cancellationToken = default)
    {
        var suggestions = await GetForStoreAsync(
            storeId,
            actor,
            analysisWindowDays,
            cancellationToken);
        return await ExplainCalculatedAsync(
            suggestions,
            ingredientId,
            cancellationToken);
    }

    private async Task<ReorderSuggestionListDto> CalculateAsync(
        ReorderCalculationData data,
        int analysisWindowDays,
        DateTime fromUtc,
        DateTime toUtc,
        IReadOnlyDictionary<int, decimal> externalIncoming,
        CancellationToken cancellationToken)
    {
        var result = new ReorderSuggestionListDto
        {
            StoreId = data.Store.StoreId,
            StoreName = data.Store.StoreName,
            AnalysisWindowDays = analysisWindowDays,
            AnalysisFromUtc = fromUtc,
            AnalysisToUtc = toUtc,
            CalculatedAtUtc = toUtc,
            CalculationVersion = ReorderSuggestionStatuses.CalculationVersion
        };

        foreach (var inventory in data.Inventories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.Items.Add(await CalculateItemAsync(
                data,
                inventory,
                analysisWindowDays,
                fromUtc,
                toUtc,
                externalIncoming.GetValueOrDefault(inventory.IngredientId)));
        }

        return result;
    }

    private async Task<ReorderSuggestionItemDto> CalculateItemAsync(
        ReorderCalculationData data,
        ReorderInventoryRow inventory,
        int analysisWindowDays,
        DateTime fromUtc,
        DateTime toUtc,
        decimal providerIncoming)
    {
        var item = NewItem(data.Store, inventory, analysisWindowDays, fromUtc, toUtc);
        var reasons = new List<string>();
        var messages = new List<string>();

        if (!inventory.MinimumStock.HasValue)
            AddProblem(reasons, messages, ReorderSuggestionReasonCodes.MissingThreshold,
                "Chưa cấu hình ngưỡng tồn kho tối thiểu.");
        else if (inventory.MinimumStock < 0)
            AddProblem(reasons, messages, ReorderSuggestionReasonCodes.NegativeThreshold,
                "Ngưỡng tồn kho tối thiểu không hợp lệ.");

        if (!data.Usage.TryGetValue(inventory.IngredientId, out var usage)
            || usage.Count <= 0)
        {
            AddProblem(reasons, messages, ReorderSuggestionReasonCodes.InsufficientHistory,
                "Chưa có giao dịch tiêu thụ hợp lệ trong kỳ phân tích.");
        }
        else
        {
            item.AverageDailyConsumption = usage.Quantity / analysisWindowDays;
        }

        var offerSelection = await SelectOfferAsync(
            data.Offers
                .Where(x => x.Offer.IngredientId == inventory.IngredientId)
                .ToArray(),
            data.Store.StoreId,
            inventory.BaseUnitId);
        if (offerSelection.Selected != null)
            ApplyOffer(item, offerSelection.Selected);
        else
        {
            foreach (var problem in offerSelection.Problems)
                AddProblem(reasons, messages, problem.Code, problem.Message);
        }

        var incomingFromRows = CalculateIncoming(data, inventory.IngredientId);
        item.IncomingQuantity = Math.Max(incomingFromRows, Math.Max(0m, providerIncoming));
        item.ProjectedStock = item.AvailableStock + item.IncomingQuantity;
        item.ActiveRestockRequestId = data.RestockRequests
            .Where(x => x.IngredientId == inventory.IngredientId)
            .OrderBy(x => x.RestockRequestId)
            .Select(x => (int?)x.RestockRequestId)
            .FirstOrDefault();

        if (reasons.Count > 0)
        {
            CompleteItem(
                item,
                ReorderRecommendationLevels.DataIncomplete,
                reasons,
                string.Join(" ", messages));
            return item;
        }

        try
        {
            var minimum = item.MinimumStock!.Value;
            var average = item.AverageDailyConsumption!.Value;
            var lead = item.LeadTimeDays!.Value;
            item.ReorderPoint = checked(average * lead + minimum);
            item.ProjectedStock = checked(item.AvailableStock + item.IncomingQuantity);
            var shortageBeforeIncoming = Math.Max(0m, item.ReorderPoint.Value - item.AvailableStock);
            item.RawDemand = Math.Max(0m, item.ReorderPoint.Value - item.ProjectedStock.Value);

            var procurementCoverage = CalculateProcurementCoverage(
                data,
                inventory.IngredientId);
            item.ProcurementCoveredQuantity = Math.Min(
                item.RawDemand.Value,
                procurementCoverage);
            item.RemainingDemand = Math.Max(
                0m,
                item.RawDemand.Value - item.ProcurementCoveredQuantity);

            if (item.RawDemand <= 0m)
            {
                item.SuggestedPackageCount = 0m;
                item.FinalSuggestedQuantity = 0m;
                item.EstimatedCost = 0m;
                if (shortageBeforeIncoming > 0m && item.IncomingQuantity > 0m)
                {
                    CompleteItem(
                        item,
                        ReorderRecommendationLevels.IncomingCoversDemand,
                        [ReorderSuggestionReasonCodes.IncomingCoversDemand],
                        "Lượng hàng đang về đã bao phủ nhu cầu tại điểm đặt hàng.");
                }
                else
                {
                    CompleteItem(
                        item,
                        ReorderRecommendationLevels.Normal,
                        [ReorderSuggestionReasonCodes.NoReorderNeeded],
                        "Tồn khả dụng đang đáp ứng điểm đặt hàng.");
                }

                return item;
            }

            if (item.RemainingDemand > 0m)
            {
                var packageCount = decimal.Ceiling(
                    item.RemainingDemand.Value / item.PackageBaseQuantity!.Value);
                packageCount = Math.Max(
                    packageCount,
                    item.MinimumOrderPackageCount.GetValueOrDefault());
                item.SuggestedPackageCount = packageCount;
                item.FinalSuggestedQuantity = checked(
                    packageCount * item.PackageBaseQuantity.Value);
                item.EstimatedCost = checked(packageCount * item.PackagePrice!.Value);
            }
            else
            {
                item.SuggestedPackageCount = 0m;
                item.FinalSuggestedQuantity = 0m;
                item.EstimatedCost = 0m;
            }

            if (item.ProcurementCoveredQuantity > 0m)
            {
                var progressReasons = new List<string>
                {
                    ReorderSuggestionReasonCodes.RawDemand,
                    ReorderSuggestionReasonCodes.ProcurementInProgress
                };
                if (item.RemainingDemand > 0m)
                    progressReasons.Add(ReorderSuggestionReasonCodes.RemainingDemand);
                CompleteItem(
                    item,
                    ReorderRecommendationLevels.ProcurementInProgress,
                    progressReasons,
                    item.RemainingDemand > 0m
                        ? "Nhu cầu đang được xử lý một phần; vẫn còn lượng cần bổ sung."
                        : "Nhu cầu đã được pipeline mua hàng hiện tại bao phủ.");
            }
            else
            {
                CompleteItem(
                    item,
                    item.AvailableStock < minimum
                        ? ReorderRecommendationLevels.Urgent
                        : ReorderRecommendationLevels.NearReorder,
                    [
                        ReorderSuggestionReasonCodes.RawDemand,
                        ReorderSuggestionReasonCodes.RemainingDemand
                    ],
                    item.AvailableStock < minimum
                        ? "Tồn khả dụng dưới ngưỡng tối thiểu và còn nhu cầu cần nhập."
                        : "Tồn khả dụng đang tiến gần điểm đặt hàng và còn nhu cầu cần nhập.");
            }

            item.CanConfirm = item.RemainingDemand > 0m
                && item.FinalSuggestedQuantity > 0m
                && item.SuggestionStatus != ReorderRecommendationLevels.DataIncomplete;
            item.MeaningfulSuggestionVersion = ComputeMeaningfulVersion(item);
            return item;
        }
        catch (OverflowException)
        {
            CompleteItem(
                item,
                ReorderRecommendationLevels.DataIncomplete,
                [ReorderSuggestionReasonCodes.CalculationOverflow],
                "Dữ liệu vượt giới hạn tính toán an toàn.");
            return item;
        }
    }

    private async Task<OfferSelection> SelectOfferAsync(
        IReadOnlyCollection<ReorderOfferRow> offers,
        int storeId,
        int baseUnitId)
    {
        if (offers.Count == 0)
        {
            return OfferSelection.Fail(
                ReorderSuggestionReasonCodes.NoActiveSupplier,
                "Không có nguồn cung đang hoạt động cho cửa hàng.");
        }

        var primaryRows = offers.Where(x => x.Offer.IsPrimary).ToArray();
        if (primaryRows.Length > 1)
        {
            return OfferSelection.Fail(
                ReorderSuggestionReasonCodes.MultiplePrimarySuppliers,
                "Có nhiều hơn một nguồn cung chính; cần chuẩn hóa cấu hình.");
        }

        var evaluated = new List<EvaluatedOffer>();
        foreach (var row in offers.OrderBy(x => x.Offer.IngredientSupplierId))
            evaluated.Add(await EvaluateOfferAsync(row, storeId, baseUnitId));

        if (primaryRows.Length == 1)
        {
            var primary = evaluated.Single(x =>
                x.Row.Offer.IngredientSupplierId
                == primaryRows[0].Offer.IngredientSupplierId);
            if (primary.IsValid)
                return OfferSelection.Success(primary);
        }

        var fallback = evaluated
            .Where(x => x.IsValid)
            .OrderBy(x => x.PackagePrice!.Value / x.PackageBaseQuantity!.Value)
            .ThenBy(x => x.LeadTimeDays)
            .ThenBy(x => x.Row.Offer.IngredientSupplierId)
            .FirstOrDefault();
        if (fallback != null)
            return OfferSelection.Success(fallback);

        var firstProblem = evaluated
            .SelectMany(x => x.Problems)
            .FirstOrDefault();
        return firstProblem == null
            ? OfferSelection.Fail(
                ReorderSuggestionReasonCodes.NoActiveSupplier,
                "Không có nguồn cung hợp lệ.")
            : new OfferSelection(null, evaluated.SelectMany(x => x.Problems).Distinct().ToArray());
    }

    private async Task<EvaluatedOffer> EvaluateOfferAsync(
        ReorderOfferRow row,
        int storeId,
        int baseUnitId)
    {
        var problems = new List<OfferProblem>();
        // Price history is the audited source when present. Legacy offers
        // created before price-history rollout retain their current snapshot
        // as a compatibility source until the data migration is complete.
        var packagePrice = row.HasCurrentPriceHistory
            ? row.HistoricalPrice
            : row.Offer.CurrentPrice;
        var packageQuantity = row.HasCurrentPriceHistory
            ? row.HistoricalPackageQuantity
            : row.Offer.PackageQuantity;
        var packageUnitId = row.HasCurrentPriceHistory
            ? row.HistoricalPackageUnitId
            : row.Offer.UnitId;
        if (!packagePrice.HasValue || packagePrice <= 0m)
            problems.Add(new(ReorderSuggestionReasonCodes.MissingCost,
                "Giá gói mua hiện hành không hợp lệ."));
        if (!packageQuantity.HasValue || packageQuantity <= 0m || !packageUnitId.HasValue)
            problems.Add(new(ReorderSuggestionReasonCodes.InvalidPackage,
                "Quy cách gói mua hiện hành không hợp lệ."));
        if (row.Offer.MinimumOrderPackageCount is < 0)
            problems.Add(new(ReorderSuggestionReasonCodes.InvalidMoq,
                "MOQ theo số gói không hợp lệ."));

        var leadTime = row.Offer.Supplier.SupplierStores
            .Where(x => x.StoreId == storeId && x.Active)
            .Select(x => x.LeadTimeOverrideDays)
            .FirstOrDefault() ?? row.Offer.LeadTimeDays;
        if (!leadTime.HasValue)
            problems.Add(new(ReorderSuggestionReasonCodes.MissingLeadTime,
                "Nguồn cung chưa có thời gian giao hàng."));
        else if (leadTime < 0)
            problems.Add(new(ReorderSuggestionReasonCodes.NegativeLeadTime,
                "Thời gian giao hàng không hợp lệ."));

        decimal? packageBase = null;
        if (packageQuantity > 0m && packageUnitId.HasValue)
        {
            var conversion = await _conversion.ConvertAsync(
                packageQuantity.Value,
                packageUnitId.Value,
                baseUnitId);
            if (!conversion.IsSuccess || conversion.Data <= 0m)
            {
                problems.Add(new(ReorderSuggestionReasonCodes.InvalidConversion,
                    "Không quy đổi được gói mua sang đơn vị tồn kho cơ sở."));
            }
            else
            {
                packageBase = conversion.Data;
            }
        }

        return new EvaluatedOffer(
            row,
            problems.Count == 0,
            packageBase,
            packagePrice,
            packageUnitId,
            leadTime,
            problems);
    }

    private static decimal CalculateIncoming(
        ReorderCalculationData data,
        int ingredientId) =>
        data.PurchaseOrderLines
            .Where(x => x.IngredientId == ingredientId
                && PurchaseOrderStatuses.IncomingValues.Contains(
                    x.Status,
                    StringComparer.OrdinalIgnoreCase))
            .Sum(x => Math.Max(
                0m,
                x.OrderedBaseQuantity
                - x.AcceptedBaseQuantity
                - x.ClosedRemainingQuantity));

    /// <summary>
    /// Returns only demand that is covered by procurement stages which are
    /// not already represented by IncomingQuantity. A root demand is counted
    /// once: bare RestockRequest, then the unallocated PA residual, then a
    /// draft PO residual. Approved/sent/partially received PO quantities are
    /// deliberately excluded because ProjectedStock already includes them.
    /// </summary>
    private static decimal CalculateProcurementCoverage(
        ReorderCalculationData data,
        int ingredientId)
    {
        var roots = data.RestockRequests
            .Where(x => x.IngredientId == ingredientId)
            .ToDictionary(x => x.RestockRequestId);
        if (roots.Count == 0)
            return 0m;

        var adviceRoot = data.PurchaseAdviceLines
            .GroupBy(x => x.PurchaseAdviceLineId)
            .ToDictionary(
                x => x.Key,
                x => x.First().RestockRequestId);
        var activeAdviceByRoot = data.PurchaseAdviceLines
            .Where(x => x.IngredientId == ingredientId
                && roots.ContainsKey(x.RestockRequestId)
                && x.IsActiveReservation
                && !string.Equals(
                    x.Status,
                    PurchaseAdviceStatuses.Rejected,
                    StringComparison.OrdinalIgnoreCase)
                && !string.Equals(
                    x.Status,
                    PurchaseAdviceStatuses.Cancelled,
                    StringComparison.OrdinalIgnoreCase)
                && !string.Equals(
                    x.Status,
                    PurchaseAdviceStatuses.Completed,
                    StringComparison.OrdinalIgnoreCase))
            .GroupBy(x => x.RestockRequestId)
            .ToDictionary(x => x.Key, x => x.ToArray());
        var allocationsByLine = data.Allocations
            .GroupBy(x => x.PurchaseOrderLineId)
            .ToDictionary(x => x.Key, x => x.ToArray());
        var rootsWithTrackedPo = new HashSet<int>();
        var draftPoCoverageByRoot = new Dictionary<int, decimal>();

        foreach (var line in data.PurchaseOrderLines.Where(x =>
                     x.IngredientId == ingredientId
                     && !string.Equals(
                         x.Status,
                         PurchaseOrderStatuses.Cancelled,
                         StringComparison.OrdinalIgnoreCase)
                     && !string.Equals(
                         x.Status,
                         PurchaseOrderStatuses.Completed,
                         StringComparison.OrdinalIgnoreCase)))
        {
            if (allocationsByLine.TryGetValue(line.PurchaseOrderLineId, out var allocations)
                && allocations.Length > 0)
            {
                foreach (var allocation in allocations.Where(x =>
                             roots.ContainsKey(x.RestockRequestId)))
                {
                    rootsWithTrackedPo.Add(allocation.RestockRequestId);
                    if (string.Equals(
                            line.Status,
                            PurchaseOrderStatuses.Draft,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        AddCoverage(
                            draftPoCoverageByRoot,
                            allocation.RestockRequestId,
                            Math.Max(
                                0m,
                                allocation.AllocatedBaseQuantity
                                - allocation.AcceptedBaseQuantity
                                - allocation.ClosedBaseQuantity));
                    }
                }

                continue;
            }

            var rootId = line.RestockRequestId;
            if (!rootId.HasValue && line.PurchaseAdviceLineId.HasValue)
                rootId = adviceRoot.GetValueOrDefault(line.PurchaseAdviceLineId.Value);
            if (!rootId.HasValue || !roots.ContainsKey(rootId.Value))
                continue;

            rootsWithTrackedPo.Add(rootId.Value);
            if (string.Equals(
                    line.Status,
                    PurchaseOrderStatuses.Draft,
                    StringComparison.OrdinalIgnoreCase))
            {
                AddCoverage(
                    draftPoCoverageByRoot,
                    rootId.Value,
                    Math.Max(
                        0m,
                        line.OrderedBaseQuantity
                        - line.AcceptedBaseQuantity
                        - line.ClosedRemainingQuantity));
            }
        }

        decimal covered = 0m;
        foreach (var root in roots.Values)
        {
            var rootResidual = Math.Max(
                0m,
                root.RequestedQuantity
                - root.FulfilledQuantity
                - root.ClosedRemainingQuantity);
            if (rootResidual <= 0m)
                continue;

            var hasActiveAdvice = activeAdviceByRoot.TryGetValue(
                root.RestockRequestId,
                out var activeAdvice);
            var adviceResidual = hasActiveAdvice
                ? activeAdvice!.Sum(x => Math.Max(
                    0m,
                    x.RequestedPurchaseBaseQuantity
                    - x.AllocatedToPoBaseQuantity
                    - x.ClosedBaseQuantity))
                : 0m;
            var hasTrackedPo = rootsWithTrackedPo.Contains(root.RestockRequestId);
            var draftPoResidual = draftPoCoverageByRoot.GetValueOrDefault(
                root.RestockRequestId);

            // Once the root has progressed downstream, the root quantity is
            // no longer added. Only the highest non-incoming stage covers it.
            var rootCoverage = hasActiveAdvice || hasTrackedPo
                ? adviceResidual + draftPoResidual
                : rootResidual;
            covered += Math.Min(rootResidual, Math.Max(0m, rootCoverage));
        }

        return covered;
    }

    private static void AddCoverage(
        IDictionary<int, decimal> coverageByRoot,
        int restockRequestId,
        decimal quantity)
    {
        if (quantity <= 0m)
            return;
        coverageByRoot.TryGetValue(restockRequestId, out var current);
        coverageByRoot[restockRequestId] =
            current + quantity;
    }

    private static ReorderSuggestionItemDto NewItem(
        ReorderStoreRow store,
        ReorderInventoryRow inventory,
        int analysisWindowDays,
        DateTime fromUtc,
        DateTime toUtc) =>
        new()
        {
            StoreId = store.StoreId,
            StoreName = store.StoreName,
            IngredientId = inventory.IngredientId,
            IngredientCode = inventory.IngredientCode,
            IngredientName = string.IsNullOrWhiteSpace(inventory.IngredientName)
                ? $"Nguyên liệu #{inventory.IngredientId}"
                : inventory.IngredientName,
            BaseUnitId = inventory.BaseUnitId,
            BaseUnitCode = inventory.BaseUnitCode,
            OnHandQuantity = inventory.OnHandQuantity,
            ReservedQuantity = inventory.ReservedQuantity,
            AvailableStock = inventory.OnHandQuantity - inventory.ReservedQuantity,
            MinimumStock = inventory.MinimumStock,
            AnalysisWindowDays = analysisWindowDays,
            AnalysisFromUtc = fromUtc,
            AnalysisToUtc = toUtc,
            CalculatedAtUtc = toUtc,
            CalculationVersion = ReorderSuggestionStatuses.CalculationVersion
        };

    private static void ApplyOffer(
        ReorderSuggestionItemDto item,
        EvaluatedOffer selection)
    {
        var offer = selection.Row.Offer;
        item.IngredientSupplierId = offer.IngredientSupplierId;
        item.SupplierId = offer.SupplierId;
        item.SupplierCode = offer.Supplier.Code;
        item.SupplierName = offer.Supplier.Name;
        item.PackageUnitId = selection.PackageUnitId;
        item.PackageBaseQuantity = selection.PackageBaseQuantity;
        item.PackagePrice = selection.PackagePrice;
        item.PriceEffectiveAtUtc = selection.Row.PriceEffectiveAtUtc
            ?? selection.Row.Offer.UpdatedAt;
        item.MinimumOrderPackageCount = offer.MinimumOrderPackageCount;
        item.LeadTimeDays = selection.LeadTimeDays;
    }

    private static void CompleteItem(
        ReorderSuggestionItemDto item,
        string status,
        IReadOnlyCollection<string> reasonCodes,
        string reason)
    {
        item.SuggestionStatus = status;
        item.ReasonCodes = reasonCodes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
        item.ReasonCode = item.ReasonCodes.FirstOrDefault() ?? string.Empty;
        item.Reason = reason;
        item.CanConfirm = false;
        item.MeaningfulSuggestionVersion = ComputeMeaningfulVersion(item);
    }

    private static string ComputeMeaningfulVersion(ReorderSuggestionItemDto item)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            item.CalculationVersion,
            item.StoreId,
            item.IngredientId,
            item.OnHandQuantity,
            item.ReservedQuantity,
            item.AvailableStock,
            item.MinimumStock,
            item.AverageDailyConsumption,
            item.LeadTimeDays,
            item.ReorderPoint,
            item.IncomingQuantity,
            item.RawDemand,
            item.ProcurementCoveredQuantity,
            item.RemainingDemand,
            item.IngredientSupplierId,
            item.PackageBaseQuantity,
            item.PackagePrice,
            item.MinimumOrderPackageCount,
            item.SuggestedPackageCount,
            item.FinalSuggestedQuantity,
            item.SuggestionStatus,
            item.ActiveRestockRequestId,
            item.ReasonCodes
        });
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))[..32];
    }

    private async Task<ServiceResult<InventoryReorderExplanationResultDto>> ExplainCalculatedAsync(
        ServiceResult<ReorderSuggestionListDto> suggestions,
        int ingredientId,
        CancellationToken cancellationToken)
    {
        if (!suggestions.IsSuccess || suggestions.Data == null)
        {
            return ServiceResult<InventoryReorderExplanationResultDto>.Failure(
                suggestions.Message ?? "Không tải được dữ liệu rule.");
        }

        var item = suggestions.Data.Items.FirstOrDefault(x =>
            x.IngredientId == ingredientId);
        if (item == null)
        {
            return ServiceResult<InventoryReorderExplanationResultDto>.Failure(
                "Không tìm thấy nguyên liệu trong cửa hàng.");
        }

        return await ExplainCalculatedAsync(item, cancellationToken);
    }

    public async Task<ServiceResult<InventoryReorderExplanationResultDto>> ExplainCalculatedAsync(
        ReorderSuggestionItemDto item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        var context = new InventoryReorderExplanationContextDto
        {
            StoreId = item.StoreId,
            StoreName = item.StoreName,
            IngredientId = item.IngredientId,
            IngredientCode = item.IngredientCode,
            IngredientName = item.IngredientName,
            BaseUnitCode = item.BaseUnitCode,
            AnalysisFromUtc = item.AnalysisFromUtc,
            AnalysisToUtc = item.AnalysisToUtc,
            CalculatedAtUtc = item.CalculatedAtUtc,
            CalculationVersion = item.CalculationVersion,
            OnHandQuantity = item.OnHandQuantity,
            ReservedQuantity = item.ReservedQuantity,
            AvailableStock = item.AvailableStock,
            MinimumStock = item.MinimumStock,
            AverageDailyConsumption = item.AverageDailyConsumption,
            LeadTimeDays = item.LeadTimeDays,
            ReorderPoint = item.ReorderPoint,
            IncomingQuantity = item.IncomingQuantity,
            ProjectedStock = item.ProjectedStock,
            RawDemand = item.RawDemand,
            ProcurementCoveredQuantity = item.ProcurementCoveredQuantity,
            RemainingDemand = item.RemainingDemand,
            PackageBaseQuantity = item.PackageBaseQuantity,
            SuggestedPackageCount = item.SuggestedPackageCount,
            FinalSuggestedQuantity = item.FinalSuggestedQuantity,
            MinimumOrderPackageCount = item.MinimumOrderPackageCount,
            PackagePrice = item.PackagePrice,
            PriceEffectiveAt = item.PriceEffectiveAtUtc,
            PriceEffectiveAtUtc = item.PriceEffectiveAtUtc,
            EstimatedCost = item.EstimatedCost,
            IngredientSupplierId = item.IngredientSupplierId,
            SupplierId = item.SupplierId,
            SupplierCode = item.SupplierCode ?? string.Empty,
            SupplierName = item.SupplierName ?? string.Empty,
            SuggestionStatus = item.SuggestionStatus,
            ReasonCodes = item.ReasonCodes,
            DeterministicReason = item.Reason,
            CanConfirm = item.CanConfirm,
            ActiveRestockRequestId = item.ActiveRestockRequestId
        };
        var explanation = await _aiService.ExplainInventoryReorderAsync(
            context,
            cancellationToken);
        return ServiceResult<InventoryReorderExplanationResultDto>.Success(explanation);
    }

    private static string? ValidateCalculationRequest(
        int storeId,
        int analysisWindowDays)
    {
        if (storeId <= 0)
            return "Cửa hàng không hợp lệ.";
        return analysisWindowDays is < 1 or > 365
            ? "Khoảng phân tích phải từ 1 đến 365 ngày."
            : null;
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

    private static void AddProblem(
        ICollection<string> codes,
        ICollection<string> messages,
        string code,
        string message)
    {
        if (!codes.Contains(code))
            codes.Add(code);
        if (!messages.Contains(message))
            messages.Add(message);
    }

    private sealed record OfferProblem(string Code, string Message);

    private sealed record EvaluatedOffer(
        ReorderOfferRow Row,
        bool IsValid,
        decimal? PackageBaseQuantity,
        decimal? PackagePrice,
        int? PackageUnitId,
        int? LeadTimeDays,
        IReadOnlyCollection<OfferProblem> Problems);

    private sealed record OfferSelection(
        EvaluatedOffer? Selected,
        IReadOnlyCollection<OfferProblem> Problems)
    {
        public static OfferSelection Success(EvaluatedOffer value) =>
            new(value, Array.Empty<OfferProblem>());

        public static OfferSelection Fail(string code, string message) =>
            new(null, [new OfferProblem(code, message)]);
    }
}
