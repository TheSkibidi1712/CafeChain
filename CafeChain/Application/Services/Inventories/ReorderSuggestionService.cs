using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.DTOs.AI;
using CafeChain.Application.Interfaces.AI;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Results;
using CafeChain.Infrastructure.Interfaces.Admin.Procurement;
using CafeChain.Models.Inventories.Suppliers;

namespace CafeChain.Application.Services.Inventories
{
    public sealed class ReorderSuggestionService : IReorderSuggestionService
    {
        private readonly IReorderSuggestionRepository _repository;
        private readonly IPhysicalUnitConversionService _conversion;
        private readonly IReorderIncomingQuantityProvider _incomingProvider;
        private readonly IScopeAuthorizationService _scopeAuthorization;
        private readonly IAIService _aiService;

        public ReorderSuggestionService(
            IReorderSuggestionRepository repository,
            IPhysicalUnitConversionService conversion,
            IReorderIncomingQuantityProvider incomingProvider,
            IScopeAuthorizationService scopeAuthorization,
            IAIService aiService)
        {
            _repository = repository;
            _conversion = conversion;
            _incomingProvider = incomingProvider;
            _scopeAuthorization = scopeAuthorization;
            _aiService = aiService;
        }

        public async Task<ServiceResult<ReorderSuggestionListDto>> GetForStoreAsync(
            int storeId,
            int actorStaffId,
            IReadOnlyCollection<string> actorRoles,
            int analysisWindowDays = 30)
        {
            if (storeId <= 0 || actorStaffId <= 0)
                return ServiceResult<ReorderSuggestionListDto>.Failure("Thiếu thông tin cửa hàng hoặc người thao tác.");
            if (analysisWindowDays is < 1 or > 365)
                return ServiceResult<ReorderSuggestionListDto>.Failure("Khoảng phân tích phải từ 1 đến 365 ngày.");
            if (!await CanViewStoreAsync(storeId, actorStaffId, actorRoles))
                return ServiceResult<ReorderSuggestionListDto>.Failure("Bạn không có quyền xem gợi ý nhập hàng của cửa hàng này.");

            var store = await _repository.GetStoreAsync(storeId);
            if (store == null)
                return ServiceResult<ReorderSuggestionListDto>.Failure("Không tìm thấy cửa hàng.");

            var inventories = await _repository.GetInventoriesAsync(storeId);

            var ingredientIds = inventories
                .Select(x => x.IngredientId!.Value)
                .Distinct()
                .ToArray();
            var fromUtc = DateTime.UtcNow.AddDays(-analysisWindowDays);
            var usage = await _repository.GetUsageAsync(storeId, fromUtc);

            var offers = await _repository.GetOffersAsync(storeId, ingredientIds);

            var incoming = await _incomingProvider.GetIncomingBaseQuantitiesAsync(storeId, ingredientIds);
            var activeRequests = await _repository.GetActiveRestockRequestsAsync(storeId);
            var activePurchaseAdvice = await _repository.GetActivePurchaseAdviceQuantitiesAsync(storeId);

            var result = new ReorderSuggestionListDto
            {
                StoreId = store.StoreId,
                StoreName = store.StoreName,
                AnalysisWindowDays = analysisWindowDays,
                CalculatedAtUtc = DateTime.UtcNow
            };

            foreach (var inventory in inventories)
            {
                var ingredientId = inventory.IngredientId!.Value;
                var item = new ReorderSuggestionItemDto
                {
                    StoreId = storeId,
                    StoreName = result.StoreName,
                    IngredientId = ingredientId,
                    IngredientCode = inventory.Ingredient.Code ?? string.Empty,
                    IngredientName = inventory.Ingredient.Name ?? $"Nguyên liệu #{ingredientId}",
                    BaseUnitCode = inventory.Ingredient.BaseUnit?.UnitCode ?? string.Empty,
                    AvailableQuantity = inventory.AvailableQty,
                    ReservedQuantity = inventory.ReservedQty,
                    UsableQuantity = inventory.AvailableQty - inventory.ReservedQty,
                    MinLevel = inventory.MinStockLevel,
                    IncomingApprovedPoQuantity = incoming.GetValueOrDefault(ingredientId),
                    PendingPurchaseAdviceQuantity = activePurchaseAdvice.GetValueOrDefault(ingredientId),
                    ActiveRestockRequestId = activeRequests.GetValueOrDefault(ingredientId) is var requestId && requestId > 0
                        ? requestId
                        : null
                };
                item.ProjectedQuantity = item.UsableQuantity + item.IncomingApprovedPoQuantity;

                if (!inventory.MinStockLevel.HasValue)
                {
                    item.RecommendationLevel = ReorderRecommendationLevels.DataIncomplete;
                    SetStatus(item, ReorderSuggestionStatuses.MissingThreshold, "Chưa cấu hình ngưỡng tồn kho tối thiểu.");
                    result.Items.Add(item);
                    continue;
                }

                if (!usage.TryGetValue(ingredientId, out var usageRow) || usageRow.Count == 0)
                {
                    item.RecommendationLevel = ReorderRecommendationLevels.DataIncomplete;
                    SetStatus(item, ReorderSuggestionStatuses.InsufficientHistory, "Chưa có dữ liệu xuất kho tiêu thụ hợp lệ trong kỳ phân tích.");
                    result.Items.Add(item);
                    continue;
                }

                item.AverageDailyUsage = usageRow.Quantity / analysisWindowDays;
                var selected = await SelectOfferAsync(
                    offers.Where(x => x.IngredientId == ingredientId).ToList(),
                    storeId,
                    inventory.Ingredient.BaseUnitId);
                if (!selected.IsSuccess)
                {
                    item.RecommendationLevel = ReorderRecommendationLevels.DataIncomplete;
                    SetStatus(item, selected.Status, selected.Reason);
                    result.Items.Add(item);
                    continue;
                }

                var offer = selected.Offer!;
                item.IngredientSupplierId = offer.IngredientSupplierId;
                item.SupplierId = offer.SupplierId;
                item.SupplierCode = offer.Supplier.Code;
                item.SupplierName = offer.Supplier.Name;
                item.PackagePrice = offer.CurrentPrice;
                item.PackageBaseQuantity = selected.PackageBaseQuantity;
                item.MinimumOrderPackageCount = offer.MinimumOrderPackageCount;
                item.LeadTimeDays = selected.LeadTimeDays;

                var reorderPoint = item.AverageDailyUsage.Value * selected.LeadTimeDays!.Value
                    + inventory.MinStockLevel.Value;
                item.ProjectedQuantity = item.UsableQuantity + item.IncomingApprovedPoQuantity;
                var shortageBeforeIncoming = reorderPoint - item.UsableQuantity;
                var suggested = Math.Max(0m, reorderPoint - item.ProjectedQuantity);
                item.ReorderPoint = reorderPoint;
                item.SuggestedBaseQuantity = suggested;

                if (item.ActiveRestockRequestId.HasValue || item.PendingPurchaseAdviceQuantity > 0)
                {
                    item.SuggestedBaseQuantity = 0;
                    item.SuggestedPackageCount = 0;
                    item.EstimatedAmount = 0;
                    item.RecommendationLevel = ReorderRecommendationLevels.ProcurementInProgress;
                    SetStatus(item, ReorderSuggestionStatuses.ProcurementInProgress,
                        "Đã có yêu cầu nhập hàng hoặc PA đang xử lý; hệ thống không tạo gợi ý trùng.");
                    result.Items.Add(item);
                    continue;
                }

                if (suggested <= 0)
                {
                    if (shortageBeforeIncoming > 0 && item.IncomingApprovedPoQuantity > 0)
                    {
                        item.RecommendationLevel = ReorderRecommendationLevels.IncomingCoversDemand;
                        SetStatus(item, ReorderSuggestionStatuses.IncomingCoversDemand, "Lượng hàng đang về đã bao phủ nhu cầu dự kiến.");
                    }
                    else
                    {
                        item.RecommendationLevel = ReorderRecommendationLevels.Normal;
                        SetStatus(item, ReorderSuggestionStatuses.NoReorderNeeded, "Tồn khả dụng đang đáp ứng điểm đặt hàng.");
                    }
                    item.SuggestedPackageCount = 0;
                    item.EstimatedAmount = 0;
                    result.Items.Add(item);
                    continue;
                }

                var packageCount = (int)Math.Ceiling(suggested / selected.PackageBaseQuantity!.Value);
                if (offer.MinimumOrderPackageCount.HasValue)
                    packageCount = Math.Max(packageCount, offer.MinimumOrderPackageCount.Value);
                item.SuggestedPackageCount = packageCount;
                item.EstimatedAmount = packageCount * offer.CurrentPrice;
                item.RecommendationLevel = item.UsableQuantity <= inventory.MinStockLevel.Value
                    ? ReorderRecommendationLevels.Urgent
                    : ReorderRecommendationLevels.NearReorder;
                SetStatus(item, ReorderSuggestionStatuses.Ready, "Đủ dữ liệu để tạo yêu cầu nhập nháp.");
                result.Items.Add(item);
            }

            return ServiceResult<ReorderSuggestionListDto>.Success(result);
        }

        public async Task<ServiceResult<InventoryReorderExplanationResultDto>> ExplainAsync(
            int storeId,
            int ingredientId,
            int actorStaffId,
            IReadOnlyCollection<string> actorRoles,
            int analysisWindowDays = 30,
            CancellationToken cancellationToken = default)
        {
            var suggestions = await GetForStoreAsync(storeId, actorStaffId, actorRoles, analysisWindowDays);
            if (!suggestions.IsSuccess || suggestions.Data == null)
                return ServiceResult<InventoryReorderExplanationResultDto>.Failure(suggestions.Message ?? "Không tải được dữ liệu rule.");
            var item = suggestions.Data.Items.FirstOrDefault(x => x.IngredientId == ingredientId);
            if (item == null)
                return ServiceResult<InventoryReorderExplanationResultDto>.Failure("Không tìm thấy nguyên liệu trong cửa hàng.");

            var context = new InventoryReorderExplanationContextDto
            {
                IngredientId = item.IngredientId,
                IngredientName = item.IngredientName,
                RecommendationLevel = string.IsNullOrWhiteSpace(item.RecommendationLevel)
                    ? ReorderRecommendationLevels.DataIncomplete
                    : item.RecommendationLevel,
                UsableStock = item.UsableQuantity,
                MinimumStock = item.MinLevel ?? 0,
                PendingIncoming = item.IncomingApprovedPoQuantity,
                SuggestedQuantity = item.SuggestedBaseQuantity ?? 0,
                Unit = item.BaseUnitCode,
                DeterministicReason = item.Reason
            };
            var explanation = await _aiService.ExplainInventoryReorderAsync(context, cancellationToken);
            return ServiceResult<InventoryReorderExplanationResultDto>.Success(explanation);
        }

        private async Task<OfferSelection> SelectOfferAsync(
            IReadOnlyCollection<IngredientSupplier> offers,
            int storeId,
            int baseUnitId)
        {
            if (offers.Count == 0)
                return OfferSelection.Fail(ReorderSuggestionStatuses.NoActiveSupplier, "Không có nguồn cung đang hoạt động cho cửa hàng.");

            var converted = new List<OfferSelection>();
            foreach (var offer in offers)
            {
                if (!offer.PackageQuantity.HasValue || offer.PackageQuantity <= 0)
                {
                    converted.Add(OfferSelection.Fail(ReorderSuggestionStatuses.InvalidConversion, "Nguồn cung chưa có quy cách gói hợp lệ.", offer));
                    continue;
                }

                var conversion = await _conversion.ConvertAsync(offer.PackageQuantity.Value, offer.UnitId, baseUnitId);
                if (!conversion.IsSuccess || conversion.Data <= 0)
                {
                    converted.Add(OfferSelection.Fail(ReorderSuggestionStatuses.InvalidConversion, "Không quy đổi được gói mua sang đơn vị tồn kho cơ sở.", offer));
                    continue;
                }

                if (offer.CurrentPrice <= 0)
                {
                    converted.Add(OfferSelection.Fail(ReorderSuggestionStatuses.MissingCost, "Nguồn cung chưa có giá gói mua hợp lệ.", offer));
                    continue;
                }

                var leadTime = offer.Supplier.SupplierStores
                    .Where(x => x.StoreId == storeId && x.Active)
                    .Select(x => x.LeadTimeOverrideDays)
                    .FirstOrDefault() ?? offer.LeadTimeDays;
                if (!leadTime.HasValue)
                {
                    converted.Add(OfferSelection.Fail(ReorderSuggestionStatuses.MissingLeadTime, "Nguồn cung chưa có thời gian giao hàng.", offer, conversion.Data));
                    continue;
                }

                converted.Add(OfferSelection.Success(offer, conversion.Data, leadTime.Value));
            }

            var primary = converted
                .Where(x => x.Offer?.IsPrimary == true)
                .OrderBy(x => x.Offer!.IngredientSupplierId)
                .FirstOrDefault();
            if (primary != null)
                return primary;

            var fallback = converted
                .Where(x => x.IsSuccess)
                .OrderBy(x => x.Offer!.CurrentPrice / x.PackageBaseQuantity!.Value)
                .ThenBy(x => x.LeadTimeDays)
                .ThenBy(x => x.Offer!.IngredientSupplierId)
                .FirstOrDefault();
            return fallback ?? converted.OrderBy(x => x.Offer?.IngredientSupplierId).First();
        }

        private async Task<bool> CanViewStoreAsync(
            int storeId,
            int actorStaffId,
            IReadOnlyCollection<string> actorRoles)
        {
            var roles = actorRoles.ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (roles.Contains(RoleConstants.BusinessOwner) || roles.Contains(RoleConstants.AccountantWarehouse))
                return true;
            if (roles.Contains(RoleConstants.AreaManager))
                return await _scopeAuthorization.CanAccessStoreAsync(actorStaffId, storeId);
            if (!roles.Contains(RoleConstants.StoreManager))
                return false;
            return await _repository.IsActiveStaffAtStoreAsync(actorStaffId, storeId);
        }

        private static void SetStatus(ReorderSuggestionItemDto item, string status, string reason)
        {
            item.Status = status;
            item.Reason = reason;
        }

        private sealed class OfferSelection
        {
            public bool IsSuccess { get; private init; }
            public string Status { get; private init; } = ReorderSuggestionStatuses.Unknown;
            public string Reason { get; private init; } = string.Empty;
            public IngredientSupplier? Offer { get; private init; }
            public decimal? PackageBaseQuantity { get; private init; }
            public int? LeadTimeDays { get; private init; }

            public static OfferSelection Success(IngredientSupplier offer, decimal packageBaseQuantity, int leadTimeDays) => new()
            {
                IsSuccess = true,
                Offer = offer,
                PackageBaseQuantity = packageBaseQuantity,
                LeadTimeDays = leadTimeDays
            };

            public static OfferSelection Fail(
                string status,
                string reason,
                IngredientSupplier? offer = null,
                decimal? packageBaseQuantity = null) => new()
            {
                Status = status,
                Reason = reason,
                Offer = offer,
                PackageBaseQuantity = packageBaseQuantity
            };
        }
    }
}
