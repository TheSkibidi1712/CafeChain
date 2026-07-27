using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.RestockRequests;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CafeChain.Application.Services.Inventories
{
    /// <summary>
    /// Issue #100 — StoreManager creates RestockRequest from CONFIRMED StockAlert;
    /// notifies AccountantWarehouse. No inventory mutation / InventoryDocument.
    /// </summary>
    public class RestockRequestService : IRestockRequestService
    {
        private const int MaxNoteLength = 500;

        private readonly AppDbContext _context;
        private readonly IScopeAuthorizationService _scopeAuthorization;
        private readonly ILogger<RestockRequestService> _logger;
        private readonly IUnitConversionService? _unitConversion;

        public RestockRequestService(
            AppDbContext context,
            IScopeAuthorizationService scopeAuthorization,
            ILogger<RestockRequestService> logger,
            IUnitConversionService? unitConversion = null)
        {
            _context = context;
            _scopeAuthorization = scopeAuthorization;
            _logger = logger;
            _unitConversion = unitConversion;
        }

        public async Task<ServiceResult<CreateRestockRequestResultDto>> CreateFromConfirmedAlertAsync(
            int alertId,
            int managerStaffId,
            int managerStoreId,
            decimal requestedQuantity,
            string? note,
            string? priority)
        {
            if (requestedQuantity <= 0)
            {
                return ServiceResult<CreateRestockRequestResultDto>.Failure(
                    "Số lượng yêu cầu phải lớn hơn 0.");
            }

            var noteText = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            if (noteText != null && noteText.Length > MaxNoteLength)
            {
                return ServiceResult<CreateRestockRequestResultDto>.Failure(
                    "Ghi chú tối đa 500 ký tự.");
            }

            if (managerStaffId <= 0 || managerStoreId <= 0)
            {
                return ServiceResult<CreateRestockRequestResultDto>.Failure(
                    "Thiếu thông tin quản lý cửa hàng.");
            }

            if (!await IsAuthorizedRequesterAsync(managerStaffId, managerStoreId))
            {
                return ServiceResult<CreateRestockRequestResultDto>.Failure(
                    "Bạn không có quyền tạo yêu cầu nhập hàng tại cửa hàng này.");
            }

            var alert = await _context.StockAlerts
                .Include(a => a.Ingredient)
                .Include(a => a.Recipe)
                .Include(a => a.PreparedItem)
                .Include(a => a.Store)
                .FirstOrDefaultAsync(a => a.StockAlertId == alertId);

            if (alert == null)
            {
                return ServiceResult<CreateRestockRequestResultDto>.Failure(
                    "Không tìm thấy cảnh báo.");
            }

            if (alert.StoreId != managerStoreId)
            {
                return ServiceResult<CreateRestockRequestResultDto>.Failure(
                    "Cảnh báo không thuộc cửa hàng của bạn.");
            }

            if (alert.Status != StockAlertStatuses.Confirmed)
            {
                return ServiceResult<CreateRestockRequestResultDto>.Failure(
                    $"Chỉ tạo yêu cầu nhập hàng từ cảnh báo đã xác nhận. Trạng thái hiện tại: {StockAlertStatusLabel(alert.Status)}.");
            }

            var preparedItemId = alert.PreparedItemId ?? alert.Recipe?.PreparedItemId;
            if (!alert.IngredientId.HasValue && !preparedItemId.HasValue)
            {
                return ServiceResult<CreateRestockRequestResultDto>.Failure(
                    "Cảnh báo chưa có định danh nguyên liệu hoặc bán thành phẩm hợp lệ.");
            }

            var existingOpen = await _context.RestockRequests
                .AsNoTracking()
                .Where(r => RestockRequestStatuses.ActiveValues.Contains(r.Status))
                .Where(r =>
                    r.StockAlertId == alertId ||
                    (r.StoreId == alert.StoreId &&
                     (alert.IngredientId.HasValue
                         ? r.IngredientId == alert.IngredientId
                         : r.PreparedItemId == preparedItemId)))
                .OrderBy(r => r.RestockRequestId)
                .FirstOrDefaultAsync();

            if (existingOpen != null)
            {
                return ServiceResult<CreateRestockRequestResultDto>.Success(
                    new CreateRestockRequestResultDto
                    {
                        RestockRequestId = existingOpen.RestockRequestId,
                        AlreadyExisted = true
                    },
                    "Yêu cầu nhập hàng đang mở đã tồn tại; hệ thống trả lại bản ghi hiện có.");
            }

            var resolvedPriority = ResolvePriority(priority, alert.AlertType);
            decimal? suggested = null;
            if (alert.ThresholdSnapshot.HasValue)
            {
                suggested = Math.Max(0m, alert.ThresholdSnapshot.Value - alert.CurrentQtySnapshot);
            }
            if (!suggested.HasValue || suggested.Value <= 0)
            {
                return ServiceResult<CreateRestockRequestResultDto>.Failure(
                    "Cảnh báo không còn nhu cầu nhập có thể kiểm chứng. Báo thiếu ngoài ngưỡng phải có mục tiêu tồn hoặc dự báo đã được xác nhận.");
            }
            if (requestedQuantity > suggested.Value)
            {
                return ServiceResult<CreateRestockRequestResultDto>.Failure(
                    $"Số lượng yêu cầu không được vượt quá nhu cầu còn có thể mua ({suggested.Value:N3} theo đơn vị tồn kho chuẩn).");
            }

            var latestSalesReportTransition = alert.Source == StockAlertSources.SalesReport
                ? await _context.StockAlertTransitions
                    .AsNoTracking()
                    .Where(x =>
                        x.StockAlertId == alert.StockAlertId
                        && x.SourceType == StockAlertSources.SalesReport)
                    .OrderByDescending(x => x.CreatedAtUtc)
                    .ThenByDescending(x => x.StockAlertTransitionId)
                    .FirstOrDefaultAsync()
                : null;
            var now = DateTime.UtcNow;
            var isManualDemand = alert.AlertType == StockAlertTypes.ManualReview
                || (alert.Source == StockAlertSources.SalesReport
                    && latestSalesReportTransition?.MinLevelSnapshot == null);
            var request = new RestockRequest
            {
                StockAlertId = alert.StockAlertId,
                StoreId = alert.StoreId,
                IngredientId = alert.IngredientId,
                RecipeId = alert.IngredientId.HasValue ? null : alert.RecipeId,
                PreparedItemId = alert.IngredientId.HasValue ? null : preparedItemId,
                RequestedQuantity = requestedQuantity,
                SuggestedQuantity = suggested,
                SuggestionAvailableSnapshot = alert.CurrentQtySnapshot,
                SuggestionMinLevelSnapshot = isManualDemand
                    ? null
                    : alert.ThresholdSnapshot,
                SuggestionReason = isManualDemand
                    ? "Nhu cầu bổ sung ngoài ngưỡng đã được quản lý xác nhận; số lượng tính từ mục tiêu quyết định trừ khả dụng."
                    : "Bù đến ngưỡng tối thiểu: max(0, ngưỡng tối thiểu - khả dụng).",
                Status = RestockRequestStatuses.Draft,
                Priority = resolvedPriority,
                CreatedByStaffId = managerStaffId,
                CreatedAt = now,
                UpdatedAt = now,
                Note = noteText
            };

            request.RowVersion = Array.Empty<byte>();
            try
            {
                _context.RestockRequests.Add(request);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsActiveRequestUniqueConflict(ex))
            {
                // Concurrent create for same StockAlert SUBMITTED unique — treat as duplicate.
                _context.ChangeTracker.Clear();
                var existing = await _context.RestockRequests
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r =>
                        RestockRequestStatuses.ActiveValues.Contains(r.Status)
                        && (r.StockAlertId == alertId
                            || (r.StoreId == alert.StoreId
                                && (alert.IngredientId.HasValue
                                    ? r.IngredientId == alert.IngredientId
                                    : r.PreparedItemId == preparedItemId))));
                if (existing != null)
                {
                    return ServiceResult<CreateRestockRequestResultDto>.Success(
                        new CreateRestockRequestResultDto
                        {
                            RestockRequestId = existing.RestockRequestId,
                            AlreadyExisted = true
                        },
                        "Yêu cầu nhập hàng đang mở đã tồn tại; hệ thống trả lại bản ghi hiện có.");
                }

                throw;
            }

            _logger.LogInformation(
                "[RestockRequest] DRAFT Id={Id} AlertId={AlertId} StoreId={StoreId} ByStaffId={StaffId}",
                request.RestockRequestId, alert.StockAlertId, alert.StoreId, managerStaffId);

            var dto = new CreateRestockRequestResultDto
            {
                RestockRequestId = request.RestockRequestId,
                NotifiedAccountantWarehouse = false,
                RecipientCount = 0
            };
            return ServiceResult<CreateRestockRequestResultDto>.Success(
                dto,
                "Đã tạo yêu cầu nhập hàng nháp từ cảnh báo đã xác nhận.");
        }

        public async Task<ServiceResult<CreateRestockRequestResultDto>> CreateFromConfirmedAlertProcurementAsync(
            int alertId,
            int managerStaffId,
            int managerStoreId,
            decimal requestedProcurementQuantity,
            int procurementUnitId,
            string? note,
            string? priority)
        {
            if (requestedProcurementQuantity <= 0 || procurementUnitId <= 0)
                return ServiceResult<CreateRestockRequestResultDto>.Failure(
                    "Số lượng và đơn vị mua hàng phải hợp lệ.");

            var alert = await _context.StockAlerts
                .AsNoTracking()
                .Include(x => x.Ingredient)
                    .ThenInclude(x => x!.BaseUnit)
                .Include(x => x.PreparedItem)
                    .ThenInclude(x => x!.BaseUnit)
                .Include(x => x.Recipe)
                    .ThenInclude(x => x!.PreparedItem)
                        .ThenInclude(x => x!.BaseUnit)
                .SingleOrDefaultAsync(x => x.StockAlertId == alertId);
            if (alert == null)
                return ServiceResult<CreateRestockRequestResultDto>.Failure(
                    "Không tìm thấy cảnh báo.");

            var baseUnit = alert.Ingredient?.BaseUnit
                ?? alert.PreparedItem?.BaseUnit
                ?? alert.Recipe?.PreparedItem?.BaseUnit;
            var procurementUnit = await _context.Units
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.UnitId == procurementUnitId && x.Active);
            if (baseUnit == null || procurementUnit == null)
                return ServiceResult<CreateRestockRequestResultDto>.Failure(
                    "Cảnh báo chưa có đơn vị tồn kho hoặc đơn vị mua hàng hợp lệ.");

            var expectedProcurementCode = baseUnit.Type switch
            {
                UnitType.KhoiLuong => ProcurementUnitCodes.Kilogram,
                UnitType.TheTich => ProcurementUnitCodes.Liter,
                UnitType.Dem => ProcurementUnitCodes.Piece,
                _ => string.Empty
            };
            if (!string.Equals(
                    procurementUnit.UnitCode,
                    expectedProcurementCode,
                    StringComparison.OrdinalIgnoreCase))
                return ServiceResult<CreateRestockRequestResultDto>.Failure(
                    "Đơn vị mua hàng không tương thích với đơn vị tồn kho của mặt hàng.");

            decimal procurementToBaseFactor;
            if (procurementUnit.UnitId == baseUnit.UnitId)
            {
                procurementToBaseFactor = 1m;
            }
            else if (procurementUnit.Type == UnitType.Dem
                && baseUnit.Type == UnitType.Dem)
            {
                procurementToBaseFactor = 1m;
            }
            else if (!PhysicalUnitConversionRegistry.TryGetPairFactor(
                procurementUnit.UnitCode,
                baseUnit.UnitCode,
                procurementUnit.Type,
                baseUnit.Type,
                out procurementToBaseFactor))
            {
                return ServiceResult<CreateRestockRequestResultDto>.Failure(
                    "Chưa cấu hình quy đổi từ đơn vị mua hàng sang đơn vị tồn kho.");
            }

            var requestedBaseQuantity =
                requestedProcurementQuantity * procurementToBaseFactor;
            var suggestedBaseQuantity = alert.ThresholdSnapshot.HasValue
                ? Math.Max(0m, alert.ThresholdSnapshot.Value - alert.CurrentQtySnapshot)
                : 0m;
            var suggestedProcurementQuantity =
                suggestedBaseQuantity / procurementToBaseFactor;
            if (suggestedProcurementQuantity <= 0)
                return ServiceResult<CreateRestockRequestResultDto>.Failure(
                    "Cảnh báo không còn nhu cầu nhập có thể kiểm chứng.");
            if (requestedProcurementQuantity > suggestedProcurementQuantity)
                return ServiceResult<CreateRestockRequestResultDto>.Failure(
                    $"Số lượng yêu cầu không được vượt quá {suggestedProcurementQuantity:N3} {procurementUnit.Name}.");

            await using var transaction = await _context.Database.BeginTransactionAsync();
            var result = await CreateFromConfirmedAlertAsync(
                alertId,
                managerStaffId,
                managerStoreId,
                requestedBaseQuantity,
                note,
                priority);
            if (!result.IsSuccess || result.Data!.AlreadyExisted)
            {
                await transaction.RollbackAsync();
                return result;
            }

            var request = await _context.RestockRequests
                .SingleAsync(x => x.RestockRequestId == result.Data.RestockRequestId);
            request.CreatedForStoreId = alert.StoreId;
            request.SourceType = RestockRequestSourceTypes.StockAlert;
            request.SourceReferenceId = alert.StockAlertId.ToString();
            request.RequestedProcurementQuantity = requestedProcurementQuantity;
            request.ProcurementUnitId = procurementUnit.UnitId;
            request.TargetStockProcurementQuantity = alert.ThresholdSnapshot.HasValue
                ? alert.ThresholdSnapshot.Value / procurementToBaseFactor
                : null;
            request.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return result;
        }

        public async Task<ServiceResult<RestockRequestListResultDto>> ListForStoreAsync(
            int storeId,
            string? statusFilter,
            int page,
            int pageSize)
        {
            if (storeId <= 0)
                return ServiceResult<RestockRequestListResultDto>.Failure("StoreId không hợp lệ.");

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            var query = _context.RestockRequests
                .AsNoTracking()
                .Include(r => r.Ingredient)
                    .ThenInclude(i => i!.BaseUnit)
                .Include(r => r.Recipe)
                .Include(r => r.PreparedItem)
                    .ThenInclude(i => i!.BaseUnit)
                .Include(r => r.ProcurementUnit)
                .Include(r => r.CreatedByStaff)
                .Where(r => r.StoreId == storeId);

            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                var st = statusFilter.Trim().ToUpperInvariant();
                query = query.Where(r => r.Status == st);
            }

            var total = await query.CountAsync();
            var rows = await query
                .OrderByDescending(r => r.CreatedAt)
                .ThenByDescending(r => r.RestockRequestId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return ServiceResult<RestockRequestListResultDto>.Success(new RestockRequestListResultDto
            {
                StoreId = storeId,
                StatusFilter = statusFilter,
                Page = page,
                PageSize = pageSize,
                Total = total,
                Items = rows.Select(MapListItem).ToList()
            });
        }

        public async Task<ServiceResult<RestockRequestDetailDto>> GetDetailAsync(
            int requestId,
            int viewerStoreId)
        {
            if (viewerStoreId <= 0)
                return ServiceResult<RestockRequestDetailDto>.Failure("StoreId không hợp lệ.");

            var r = await _context.RestockRequests
                .AsNoTracking()
                .Include(x => x.Ingredient)
                    .ThenInclude(i => i!.BaseUnit)
                .Include(x => x.Recipe)
                .Include(x => x.PreparedItem)
                    .ThenInclude(i => i!.BaseUnit)
                .Include(x => x.ProcurementUnit)
                .Include(x => x.CreatedByStaff)
                .Include(x => x.Store)
                .Include(x => x.StockAlert)
                .Include(x => x.SourcingAllocations)
                .FirstOrDefaultAsync(x => x.RestockRequestId == requestId);

            if (r == null)
                return ServiceResult<RestockRequestDetailDto>.Failure("Không tìm thấy yêu cầu nhập hàng.");

            if (r.StoreId != viewerStoreId)
                return ServiceResult<RestockRequestDetailDto>.Failure("Yêu cầu không thuộc cửa hàng của bạn.");

            return ServiceResult<RestockRequestDetailDto>.Success(MapDetail(r));
        }

        public async Task<ServiceResult<RestockRequestListItemDto?>> GetOpenByAlertAsync(
            int stockAlertId,
            int storeId)
        {
            if (storeId <= 0 || stockAlertId <= 0)
                return ServiceResult<RestockRequestListItemDto?>.Failure("Tham số không hợp lệ.");

            var r = await _context.RestockRequests
                .AsNoTracking()
                .Include(x => x.Ingredient)
                    .ThenInclude(i => i!.BaseUnit)
                .Include(x => x.Recipe)
                .Include(x => x.PreparedItem)
                    .ThenInclude(i => i!.BaseUnit)
                .Include(x => x.CreatedByStaff)
                .Where(x =>
                    x.StockAlertId == stockAlertId &&
                    x.StoreId == storeId &&
                    RestockRequestStatuses.ActiveValues.Contains(x.Status))
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync();

            return ServiceResult<RestockRequestListItemDto?>.Success(
                r == null ? null : MapListItem(r));
        }

        public Task<ServiceResult<CreateRestockRequestResultDto>> CreateManualAsync(
            CreateProcurementDemandRequest request,
            int actorStaffId) =>
            CreateProcurementDemandAsync(request, actorStaffId, RestockRequestSourceTypes.ManualByStore);

        public Task<ServiceResult<CreateRestockRequestResultDto>> CreateCentralPlannerAsync(
            CreateProcurementDemandRequest request,
            int actorStaffId) =>
            CreateProcurementDemandAsync(request, actorStaffId, RestockRequestSourceTypes.CentralPlanner);

        public async Task<ServiceResult<SourcingAllocationDto>> SetSourcingDecisionAsync(
            SourcingDecisionRequest request,
            int actorStaffId)
        {
            if (request.RestockRequestId <= 0 || request.ProcurementQuantity <= 0)
                return ServiceResult<SourcingAllocationDto>.Failure("Yêu cầu và số lượng phân bổ không hợp lệ.");

            var decision = request.DecisionType.Trim().ToUpperInvariant();
            if (!RestockSourcingDecisionTypes.All.Contains(decision, StringComparer.Ordinal))
                return ServiceResult<SourcingAllocationDto>.Failure("Quyết định nguồn cung không hợp lệ.");
            if (decision == RestockSourcingDecisionTypes.Reject
                && string.IsNullOrWhiteSpace(request.Reason))
                return ServiceResult<SourcingAllocationDto>.Failure("Từ chối nhu cầu phải có lý do.");

            var demand = await _context.RestockRequests
                .Include(x => x.SourcingAllocations)
                .SingleOrDefaultAsync(x => x.RestockRequestId == request.RestockRequestId);
            if (demand == null)
                return ServiceResult<SourcingAllocationDto>.Failure("Không tìm thấy yêu cầu bổ sung.");
            if (!await IsAuthorizedRequesterAsync(actorStaffId, demand.StoreId, allowWarehouseRole: true))
                return ServiceResult<SourcingAllocationDto>.Failure("Bạn không có quyền quyết định nguồn cung cho cửa hàng này.");
            if (request.ProcurementUnitId != demand.ProcurementUnitId)
                return ServiceResult<SourcingAllocationDto>.Failure("Đơn vị mua hàng phải trùng với đơn vị của nhu cầu.");

            var allocated = demand.SourcingAllocations
                .Where(x => x.Status is RestockSourcingAllocationStatuses.Active
                    or RestockSourcingAllocationStatuses.PendingPurchaseAdvice)
                .Sum(x => x.ProcurementQuantity);
            var requested = demand.RequestedProcurementQuantity ?? demand.RequestedQuantity;
            if (request.ProcurementQuantity > requested - allocated)
                return ServiceResult<SourcingAllocationDto>.Failure(
                    $"Số lượng phân bổ vượt nhu cầu còn lại ({Math.Max(0m, requested - allocated):N3}).");

            var now = DateTime.UtcNow;
            var allocation = new RestockSourcingAllocation
            {
                RestockRequestId = demand.RestockRequestId,
                DecisionType = decision,
                ProcurementQuantity = request.ProcurementQuantity,
                ProcurementUnitId = request.ProcurementUnitId,
                Status = decision == RestockSourcingDecisionTypes.Purchase
                    ? RestockSourcingAllocationStatuses.PendingPurchaseAdvice
                    : RestockSourcingAllocationStatuses.Active,
                SourceDocumentType = decision,
                SourceDocumentId = request.SourceDocumentId,
                SourceDocumentLineId = request.SourceDocumentLineId,
                Reason = Clean(request.Reason, 500),
                CreatedByStaffId = actorStaffId,
                CreatedAtUtc = now
            };
            demand.SourcingDecision = decision;
            demand.SourcingStatus = allocated + request.ProcurementQuantity >= requested
                ? RestockSourcingStatuses.FullyAllocated
                : RestockSourcingStatuses.PartiallyAllocated;
            _context.RestockSourcingAllocations.Add(allocation);
            await _context.SaveChangesAsync();

            return ServiceResult<SourcingAllocationDto>.Success(MapSourcingAllocation(allocation),
                decision == RestockSourcingDecisionTypes.Purchase
                    ? "Đã ghi nhận nhu cầu mua; đề nghị mua sẽ được tạo từ allocation này."
                    : "Đã ghi nhận quyết định nguồn cung.");
        }

        private async Task<ServiceResult<CreateRestockRequestResultDto>> CreateProcurementDemandAsync(
            CreateProcurementDemandRequest request,
            int actorStaffId,
            string sourceType)
        {
            if (request.StoreId <= 0 || request.IngredientId <= 0 || request.ProcurementUnitId <= 0
                || request.RequestedProcurementQuantity <= 0)
                return ServiceResult<CreateRestockRequestResultDto>.Failure("Thông tin nhu cầu bổ sung không hợp lệ.");
            if (!await IsAuthorizedRequesterAsync(
                    actorStaffId,
                    request.StoreId,
                    allowWarehouseRole: sourceType == RestockRequestSourceTypes.CentralPlanner,
                    requireWarehouseScope: sourceType == RestockRequestSourceTypes.CentralPlanner))
                return ServiceResult<CreateRestockRequestResultDto>.Failure("Bạn không có quyền tạo nhu cầu cho cửa hàng này.");

            var ingredient = await _context.Ingredients
                .Include(x => x.BaseUnit)
                .SingleOrDefaultAsync(x => x.IngredientId == request.IngredientId && x.Active);
            var unit = await _context.Units
                .SingleOrDefaultAsync(x => x.UnitId == request.ProcurementUnitId && x.Active);
            if (ingredient == null || unit == null)
                return ServiceResult<CreateRestockRequestResultDto>.Failure("Nguyên liệu hoặc đơn vị mua hàng không hợp lệ.");
            if (!AllowedProcurementUnitCodes.Contains(unit.UnitCode, StringComparer.OrdinalIgnoreCase))
                return ServiceResult<CreateRestockRequestResultDto>.Failure("Đơn vị mua hàng phải là kg, L hoặc cái.");

            var sourceReference = Clean(request.SourceReferenceId, 100);
            if (sourceReference != null)
            {
                var duplicate = await _context.RestockRequests
                    .AsNoTracking()
                    .Where(x => x.StoreId == request.StoreId
                        && x.SourceType == sourceType
                        && x.SourceReferenceId == sourceReference
                        && RestockRequestStatuses.ActiveValues.Contains(x.Status))
                    .Select(x => x.RestockRequestId)
                    .FirstOrDefaultAsync();
                if (duplicate > 0)
                    return ServiceResult<CreateRestockRequestResultDto>.Success(
                        new CreateRestockRequestResultDto { RestockRequestId = duplicate, AlreadyExisted = true },
                        "Nhu cầu cùng nguồn đã tồn tại; hệ thống trả lại bản ghi hiện có.");
            }

            var now = DateTime.UtcNow;
            var requested = request.RequestedProcurementQuantity;
            decimal requestedBaseQuantity;
            if (unit.UnitId == ingredient.BaseUnitId)
            {
                requestedBaseQuantity = requested;
            }
            else
            {
                if (_unitConversion == null)
                    return ServiceResult<CreateRestockRequestResultDto>.Failure(
                        "Chưa cấu hình dịch vụ quy đổi đơn vị mua hàng sang đơn vị tồn kho.");

                var conversion = await _unitConversion.ConvertAsync(
                    ingredient.IngredientId,
                    requested,
                    unit.UnitId,
                    ingredient.BaseUnitId);
                if (!conversion.IsSuccess || conversion.Data <= 0)
                    return ServiceResult<CreateRestockRequestResultDto>.Failure(
                        $"Không thể quy đổi {requested:N3} {unit.Name} sang {ingredient.BaseUnit.Name}: {conversion.Message}");

                requestedBaseQuantity = conversion.Data;
            }

            var demand = new RestockRequest
            {
                StoreId = request.StoreId,
                CreatedForStoreId = request.StoreId,
                SourceType = sourceType,
                SourceReferenceId = sourceReference,
                NeedByDate = request.NeedByDate?.ToUniversalTime(),
                RequestedProcurementQuantity = requested,
                ProcurementUnitId = unit.UnitId,
                TargetStockProcurementQuantity = request.TargetStockProcurementQuantity,
                ForecastEvidence = Clean(request.ForecastEvidence, 1000),
                // Legacy base-quantity fields remain populated for existing allocation/workflow code.
                // Procurement quantity/UOM above is the authoritative purchasing contract.
                RequestedQuantity = requestedBaseQuantity,
                SuggestedQuantity = requestedBaseQuantity,
                IngredientId = ingredient.IngredientId,
                Status = RestockRequestStatuses.Draft,
                Priority = ResolvePriority(request.Priority, StockAlertTypes.ManualReview),
                CreatedByStaffId = actorStaffId,
                CreatedAt = now,
                UpdatedAt = now,
                Note = Clean(request.Note, MaxNoteLength)
            };
            _context.RestockRequests.Add(demand);
            await _context.SaveChangesAsync();
            return ServiceResult<CreateRestockRequestResultDto>.Success(
                new CreateRestockRequestResultDto { RestockRequestId = demand.RestockRequestId },
                sourceType == RestockRequestSourceTypes.CentralPlanner
                    ? "Đã tạo nhu cầu bổ sung từ kế hoạch trung tâm."
                    : "Đã tạo nhu cầu bổ sung thủ công cho cửa hàng.");
        }

        private async Task<List<int>> ResolveAccountantWarehouseAsync(int storeId)
        {
            return await _context.Staffs
                .AsNoTracking()
                .Where(s =>
                    s.Active &&
                    s.Account != null &&
                    s.Account.Active &&
                    s.Account.AccountRoles.Any(ar =>
                        ar.Role != null &&
                        ar.Role.Active &&
                        ar.Role.Name == RoleConstants.AccountantWarehouse))
                .Select(s => s.StaffId)
                .Distinct()
                .ToListAsync();
        }

        private static bool IsActiveRequestUniqueConflict(DbUpdateException ex)
        {
            var message = ex.InnerException?.Message ?? ex.Message;
            return message.Contains("UX_RestockRequest_Active_", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("UX_RestockRequest_Open_", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<bool> IsAuthorizedRequesterAsync(
            int staffId,
            int storeId,
            bool allowWarehouseRole = false,
            bool requireWarehouseScope = false)
        {
            var staff = await _context.Staffs
                .AsNoTracking()
                .Include(s => s.Account)
                    .ThenInclude(a => a.AccountRoles)
                        .ThenInclude(ar => ar.Role)
                .FirstOrDefaultAsync(s => s.StaffId == staffId && s.Active);
            if (staff == null)
                return false;
            var roles = staff.Account.AccountRoles
                .Where(ar => ar.Role != null && ar.Role.Active)
                .Select(ar => ar.Role.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (roles.Contains(RoleConstants.BusinessOwner))
                return true;
            if (roles.Contains(RoleConstants.AreaManager))
                return await _scopeAuthorization.CanAccessStoreAsync(staffId, storeId);
            if (allowWarehouseRole && roles.Contains(RoleConstants.AccountantWarehouse))
                return requireWarehouseScope
                    ? await _scopeAuthorization.CanAccessStoreAsync(staffId, storeId)
                    : staff.StoreId == storeId
                        || await _scopeAuthorization.CanAccessStoreAsync(staffId, storeId);
            return roles.Contains(RoleConstants.StoreManager) && staff.StoreId == storeId;
        }

        private static readonly string[] AllowedProcurementUnitCodes =
            { ProcurementUnitCodes.Kilogram, ProcurementUnitCodes.Liter, ProcurementUnitCodes.Piece };

        private static string? Clean(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var clean = value.Trim();
            return clean.Length <= maxLength ? clean : clean[..maxLength];
        }

        private static string ResolvePriority(string? priority, string alertType)
        {
            if (!string.IsNullOrWhiteSpace(priority))
            {
                var p = priority.Trim().ToUpperInvariant();
                if (p is RestockRequestPriorities.Normal
                    or RestockRequestPriorities.High
                    or RestockRequestPriorities.Urgent)
                    return p;
            }

            if (string.Equals(alertType, StockAlertTypes.OutOfStock, StringComparison.OrdinalIgnoreCase))
                return RestockRequestPriorities.Urgent;
            if (string.Equals(alertType, StockAlertTypes.LowStock, StringComparison.OrdinalIgnoreCase))
                return RestockRequestPriorities.High;
            return RestockRequestPriorities.Normal;
        }

        private static string StockAlertStatusLabel(string? status) => status switch
        {
            StockAlertStatuses.Open => "Đang mở",
            StockAlertStatuses.Confirmed => "Đã xác nhận",
            StockAlertStatuses.Resolved => "Đã phục hồi",
            StockAlertStatuses.Rejected => "Đã từ chối",
            StockAlertStatuses.Closed => "Đã đóng",
            _ => "Không xác định"
        };

        private static RestockRequestListItemDto MapListItem(RestockRequest r) => new()
        {
            RestockRequestId = r.RestockRequestId,
            StockAlertId = r.StockAlertId,
            StoreId = r.StoreId,
            ItemName = ResolveItemName(r),
            ItemTypeLabel = r.IngredientId.HasValue ? "Nguyên liệu" : "Bán thành phẩm",
            BaseUnitName = ResolveBaseUnitName(r),
            RequestedQuantity = r.RequestedQuantity,
            SuggestedQuantity = r.SuggestedQuantity,
            Status = r.Status,
            Priority = r.Priority,
            Note = r.Note,
            CreatedByName = r.CreatedByStaff?.FullName,
            CreatedAt = r.CreatedAt,
            SourceType = r.SourceType,
            SourceReferenceId = r.SourceReferenceId,
            CreatedForStoreId = r.CreatedForStoreId,
            SourcingStatus = r.SourcingStatus,
            SourcingDecision = r.SourcingDecision,
            RequestedProcurementQuantity = r.RequestedProcurementQuantity,
            ProcurementUnitId = r.ProcurementUnitId,
            ProcurementUnitName = r.ProcurementUnit?.Name
        };

        private static RestockRequestDetailDto MapDetail(RestockRequest r)
        {
            var dto = new RestockRequestDetailDto
            {
                RestockRequestId = r.RestockRequestId,
                StockAlertId = r.StockAlertId,
                StoreId = r.StoreId,
                ItemName = ResolveItemName(r),
                ItemTypeLabel = r.IngredientId.HasValue
                    ? "Nguyên liệu"
                    : "Bán thành phẩm",
                BaseUnitName = ResolveBaseUnitName(r),
                RequestedQuantity = r.RequestedQuantity,
                SuggestedQuantity = r.SuggestedQuantity,
                Status = r.Status,
                Priority = r.Priority,
                Note = r.Note,
                CreatedByName = r.CreatedByStaff?.FullName,
                CreatedAt = r.CreatedAt,
                IngredientId = r.IngredientId,
                RecipeId = r.RecipeId,
                PreparedItemId = r.PreparedItemId,
                CreatedByStaffId = r.CreatedByStaffId,
                UpdatedAt = r.UpdatedAt,
                StoreName = r.Store?.Name,
                AlertType = r.StockAlert?.AlertType,
                AlertStatus = r.StockAlert?.Status,
                AlertCurrentQtySnapshot = r.StockAlert?.CurrentQtySnapshot,
                AlertThresholdSnapshot = r.StockAlert?.ThresholdSnapshot,
                SuggestionAnalysisWindowDays = r.SuggestionAnalysisWindowDays,
                SuggestionAvailableSnapshot = r.SuggestionAvailableSnapshot,
                SuggestionMinLevelSnapshot = r.SuggestionMinLevelSnapshot,
                SuggestionAverageDailyUsageSnapshot = r.SuggestionAverageDailyUsageSnapshot,
                SuggestionLeadTimeDaysSnapshot = r.SuggestionLeadTimeDaysSnapshot,
                SuggestionIncomingQuantitySnapshot = r.SuggestionIncomingQuantitySnapshot,
                SuggestionReason = r.SuggestionReason,
                SourceType = r.SourceType,
                SourceReferenceId = r.SourceReferenceId,
                CreatedForStoreId = r.CreatedForStoreId,
                SourcingStatus = r.SourcingStatus,
                SourcingDecision = r.SourcingDecision,
                RequestedProcurementQuantity = r.RequestedProcurementQuantity,
                ProcurementUnitId = r.ProcurementUnitId,
                ProcurementUnitName = r.ProcurementUnit?.Name,
                SourcingAllocations = r.SourcingAllocations?.Select(MapSourcingAllocation).ToList() ?? new()
            };
            return dto;
        }

        private static SourcingAllocationDto MapSourcingAllocation(RestockSourcingAllocation allocation) => new()
        {
            RestockSourcingAllocationId = allocation.RestockSourcingAllocationId,
            RestockRequestId = allocation.RestockRequestId,
            DecisionType = allocation.DecisionType,
            ProcurementQuantity = allocation.ProcurementQuantity,
            ProcurementUnitId = allocation.ProcurementUnitId,
            Status = allocation.Status,
            PurchaseAdviceLineId = allocation.PurchaseAdviceLineId,
            PurchaseOrderLineId = allocation.PurchaseOrderLineId,
            Reason = allocation.Reason,
            CreatedAtUtc = allocation.CreatedAtUtc
        };

        private static string ResolveBaseUnitName(RestockRequest r) =>
            r.Ingredient?.BaseUnit?.Name
            ?? r.PreparedItem?.BaseUnit?.Name
            ?? "Đơn vị gốc";

        private static string ResolveItemName(RestockRequest r)
        {
            if (r.IngredientId.HasValue)
                return r.Ingredient?.Name ?? $"Nguyên liệu #{r.IngredientId}";
            if (r.PreparedItemId.HasValue)
            {
                if (!string.IsNullOrWhiteSpace(r.PreparedItem?.Name)) return r.PreparedItem.Name;
                if (!string.IsNullOrWhiteSpace(r.PreparedItem?.Code)) return r.PreparedItem.Code;
                return $"Bán thành phẩm #{r.PreparedItemId}";
            }
            if (r.RecipeId.HasValue)
            {
                if (!string.IsNullOrWhiteSpace(r.Recipe?.Name)) return r.Recipe.Name;
                if (!string.IsNullOrWhiteSpace(r.Recipe?.RecipeCode)) return r.Recipe.RecipeCode;
                return $"Bán thành phẩm #{r.RecipeId}";
            }
            return "Mặt hàng không xác định";
        }

        private static string ResolveItemName(StockAlert a)
        {
            if (a.IngredientId.HasValue)
                return a.Ingredient?.Name ?? $"Nguyên liệu #{a.IngredientId}";
            if (a.PreparedItemId.HasValue)
            {
                if (!string.IsNullOrWhiteSpace(a.PreparedItem?.Name)) return a.PreparedItem.Name;
                if (!string.IsNullOrWhiteSpace(a.PreparedItem?.Code)) return a.PreparedItem.Code;
                return $"Bán thành phẩm #{a.PreparedItemId}";
            }
            if (a.RecipeId.HasValue)
            {
                if (!string.IsNullOrWhiteSpace(a.Recipe?.Name)) return a.Recipe.Name;
                if (!string.IsNullOrWhiteSpace(a.Recipe?.RecipeCode)) return a.Recipe.RecipeCode;
                return $"Bán thành phẩm #{a.RecipeId}";
            }
            return "Mặt hàng không xác định";
        }
    }
}
