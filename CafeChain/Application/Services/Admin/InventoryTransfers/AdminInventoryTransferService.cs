using CafeChain.Application.DTOs.Admin.InventoryDocuments.Create;
using CafeChain.Application.DTOs.Admin.InventoryTransfers;
using CafeChain.Application.DTOs.Admin.RestockRequests;
using CafeChain.Application.DTOs.Systems;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces.Admin.InventoryDocuments;
using CafeChain.Application.Interfaces.Admin.InventoryTransfers;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Systems;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Infrastrusture.Interfaces.Admin.InventoryTransfers;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Costing;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Inventories.Transactions;
using CafeChain.Models.Inventories.Transfers;
using CafeChain.Models.Stores;
using CafeChain.ViewModels.Admin.InventoryTransfers;
using System.Globalization;

namespace CafeChain.Application.Services.Admin.InventoryTransfers
{
    public class AdminInventoryTransferService : IAdminInventoryTransferService
    {
        private const string CreateDraftAction = "InventoryTransfer.CreateDraft";
        private const string UpdateDraftAction = "InventoryTransfer.UpdateDraft";
        private const string DispatchAction = "InventoryTransfer.Dispatch";
        private const string ReceiveAction = "InventoryTransfer.Receive";
        private const string RequestReturnAction = "InventoryTransfer.RequestReturn";
        private const string ConfirmReturnAction = "InventoryTransfer.ConfirmReturn";
        private const string ResolveShortageAction = "InventoryTransfer.ResolveShortage";
        private const string FollowUpAction = "InventoryTransfer.CreateFollowUp";
        private const string CancelAction = "InventoryTransfer.Cancel";

        private readonly IAdminInventoryTransferRepository _repository;
        private readonly IRequestDeduplicationService _deduplicationService;
        private readonly IInventoryIssuePolicy _inventoryIssuePolicy;
        private readonly IInventoryCostLayerConsumptionService _costLayerConsumptionService;
        private readonly IRestockFulfillmentPostingService _fulfillmentPostingService;
        private readonly IRestockAllocationService _restockAllocationService;
        private readonly IStockAlertService _stockAlertService;
        private readonly IUserContext _userContext;
        private readonly IAdminActorContextAccessor _actorAccessor;
        private readonly IScopeAuthorizationService _scopeAuthorization;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AdminInventoryTransferService(
            IAdminInventoryTransferRepository repository,
            IRequestDeduplicationService deduplicationService,
            IInventoryIssuePolicy inventoryIssuePolicy,
            IInventoryCostLayerConsumptionService costLayerConsumptionService,
            IRestockFulfillmentPostingService fulfillmentPostingService,
            IStockAlertService stockAlertService,
            IUserContext userContext,
            IAdminActorContextAccessor actorAccessor,
            IScopeAuthorizationService scopeAuthorization,
            IHttpContextAccessor httpContextAccessor,
            IRestockAllocationService restockAllocationService)
        {
            _repository = repository;
            _deduplicationService = deduplicationService;
            _inventoryIssuePolicy = inventoryIssuePolicy;
            _costLayerConsumptionService = costLayerConsumptionService;
            _fulfillmentPostingService = fulfillmentPostingService;
            _stockAlertService = stockAlertService;
            _userContext = userContext;
            _actorAccessor = actorAccessor;
            _scopeAuthorization = scopeAuthorization;
            _httpContextAccessor = httpContextAccessor;
            _restockAllocationService = restockAllocationService;
        }

        public async Task<AdminInventoryTransferIndexVM> GetIndexAsync(
            AdminInventoryTransferIndexVM filter,
            IReadOnlyCollection<int>? allowedStoreIds = null)
        {
            allowedStoreIds = await GetAllowedStoreIdsAsync();
            var page = filter.Page <= 0 ? 1 : filter.Page;
            var pageSize = filter.PageSize <= 0 ? 20 : Math.Min(filter.PageSize, 100);
            var skip = (page - 1) * pageSize;
            var keyword = string.IsNullOrWhiteSpace(filter.Keyword)
                ? null
                : filter.Keyword.Trim();

            var totalItems = await _repository.CountTransfersAsync(
                keyword,
                filter.Status,
                filter.FromStoreId,
                filter.ToStoreId,
                allowedStoreIds);
            var transfers = await _repository.GetTransfersAsync(
                keyword,
                filter.Status,
                filter.FromStoreId,
                filter.ToStoreId,
                skip,
                pageSize,
                allowedStoreIds);

            var stores = await _repository.GetStoreDropdownAsync();
            if (allowedStoreIds != null)
            {
                var allowed = allowedStoreIds.ToHashSet();
                stores = stores.Where(x => allowed.Contains(x.StoreId)).ToList();
            }

            return new AdminInventoryTransferIndexVM
            {
                Keyword = keyword,
                Status = filter.Status,
                FromStoreId = filter.FromStoreId,
                ToStoreId = filter.ToStoreId,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                Stores = stores,
                Items = transfers
                    .Select(x => new AdminInventoryTransferIndexItemVM
                    {
                        InventoryTransferId = x.InventoryTransferId,
                        Code = x.Code,
                        Status = x.Status,
                        Purpose = x.Purpose,
                        FromStoreName = x.FromStore?.Name ?? string.Empty,
                        ToStoreName = x.ToStore?.Name ?? string.Empty,
                        CreatedByName = x.CreatedByStaff?.FullName ?? string.Empty,
                        DocumentDate = x.DocumentDate,
                        CreatedAt = x.CreatedAt,
                        ConfirmedAt = x.ConfirmedAt,
                        DetailCount = x.Details.Count
                    })
                    .ToList()
            };
        }

        public async Task<AdminInventoryTransferCreateVM> GetCreateDataAsync(
            IReadOnlyCollection<int>? allowedStoreIds = null)
        {
            allowedStoreIds = await GetAllowedStoreIdsAsync();
            var stores = await _repository.GetStoreDropdownAsync();
            if (allowedStoreIds != null)
            {
                var allowed = allowedStoreIds.ToHashSet();
                stores = stores.Where(x => allowed.Contains(x.StoreId)).ToList();
            }
            return new AdminInventoryTransferCreateVM
            {
                DocumentDate = DateTime.Today,
                CreatedByName = _userContext.StaffName,
                Stores = stores
            };
        }

        public async Task<AdminInventoryTransferDetailVM?> GetDetailAsync(int id)
        {
            if (id <= 0)
            {
                return null;
            }

            var transfer = await _repository.GetTransferByIdAsync(id);

            if (transfer == null)
            {
                return null;
            }

            if (!await CanReadTransferAsync(transfer.FromStoreId, transfer.ToStoreId))
                return null;

            var detailIds = transfer.Details.Select(x => x.InventoryTransferDetailId).ToArray();
            var postings = await _repository.GetTransferDiscrepancyPostingsAsync(detailIds) ?? [];
            var receiptLines = await _repository.GetTransferReceiptLinesAsync(transfer.InventoryTransferId) ?? [];
            var actor = GetActor();
            var destinationOperator = HasOperationalRole(actor)
                && await CanAccessStoreAsync(transfer.ToStoreId);
            var sourceOperator = HasOperationalRole(actor)
                && await CanAccessStoreAsync(transfer.FromStoreId);
            var canCoordinate = HasRole(actor, RoleConstants.BusinessOwner, RoleConstants.AccountantWarehouse);
            var canResolve = HasRole(actor, RoleConstants.BusinessOwner);

            var detailItems = transfer.Details
                .OrderBy(x => x.InventoryTransferDetailId)
                .Select(x =>
                {
                    var authority = InventoryTransferQuantityAuthority.Calculate(x, postings);
                    return new AdminInventoryTransferDetailItemVM
                    {
                        InventoryTransferDetailId = x.InventoryTransferDetailId,
                        IngredientId = x.IngredientId,
                        PreparedItemId = x.PreparedItemId,
                        RestockRequestId = x.RestockRequestId,
                        ItemType = x.IngredientId.HasValue ? "Nguyên liệu" : "Bán thành phẩm",
                        ItemName = x.Ingredient?.Name ?? x.PreparedItem?.Name ?? string.Empty,
                        UnitName = x.Unit?.Name ?? string.Empty,
                        UnitCode = x.Unit?.UnitCode ?? string.Empty,
                        BaseUnitCode = x.Ingredient?.BaseUnit?.UnitCode
                            ?? x.PreparedItem?.BaseUnit?.UnitCode
                            ?? string.Empty,
                        Quantity = x.Quantity,
                        BaseQuantity = x.BaseQuantity,
                        DispatchedBaseQuantity = authority.Dispatched,
                        DestinationAccepted = authority.DestinationAccepted,
                        DestinationRejected = authority.DestinationRejected,
                        ReturnedToSource = authority.ReturnedToSource,
                        WrittenOff = authority.WrittenOff,
                        ClosedShortage = authority.ClosedShortage,
                        InTransitOpen = authority.InTransitOpen,
                        PendingReturn = authority.PendingReturn,
                        ReturnableRejected = Math.Max(0,
                            authority.DestinationRejected
                            - authority.ReturnedToSource
                            - authority.WrittenOff
                            - authority.ClosedShortage
                            - authority.PendingReturn),
                        DiscrepancyStatus = authority.Status,
                        UnitPrice = x.UnitPrice,
                        SourceBeforeQty = x.SourceBeforeQty,
                        SourceAfterQty = x.SourceAfterQty,
                        DestinationBeforeQty = x.DestinationBeforeQty,
                        DestinationAfterQty = x.DestinationAfterQty,
                        Note = x.Note
                    };
                })
                .ToList();

            var itemByDetail = detailItems.ToDictionary(x => x.InventoryTransferDetailId);
            var timeline = receiptLines.Select(x => new AdminInventoryTransferTimelineItemVM
                {
                    OccurredAt = x.BranchReceipt.ConfirmedAt ?? x.CreatedAt,
                    EventType = x.RejectedBaseQuantity > 0 && x.ReceivedBaseQuantity > 0
                        ? "DESTINATION_RECEIPT_MIXED"
                        : x.RejectedBaseQuantity > 0
                            ? "DESTINATION_REJECTED"
                            : "DESTINATION_ACCEPTED",
                    ItemName = itemByDetail.GetValueOrDefault(x.SourceInventoryTransferDetailId ?? 0)?.ItemName ?? string.Empty,
                    Quantity = x.ReceivedBaseQuantity + x.RejectedBaseQuantity,
                    UnitCode = itemByDetail.GetValueOrDefault(x.SourceInventoryTransferDetailId ?? 0)?.BaseUnitCode ?? string.Empty,
                    ActorName = x.BranchReceipt.ConfirmedByStaff?.FullName ?? string.Empty,
                    Reason = x.RejectionReason ?? x.BranchReceipt.Notes,
                    RequestKey = x.BranchReceipt.ReceiptKey
                })
                .Concat(postings.Select(x => new AdminInventoryTransferTimelineItemVM
                {
                    OccurredAt = x.CreatedAt,
                    EventType = x.PostingType.ToString(),
                    ItemName = itemByDetail.GetValueOrDefault(x.InventoryTransferDetailId)?.ItemName ?? string.Empty,
                    Quantity = x.Quantity,
                    UnitCode = itemByDetail.GetValueOrDefault(x.InventoryTransferDetailId)?.BaseUnitCode ?? string.Empty,
                    ActorName = x.ActorStaff?.FullName ?? string.Empty,
                    Reason = x.Reason,
                    RequestKey = x.RequestKey
                }))
                .OrderByDescending(x => x.OccurredAt)
                .ToList();

            return new AdminInventoryTransferDetailVM
            {
                InventoryTransferId = transfer.InventoryTransferId,
                Code = transfer.Code,
                RequestKey = transfer.RequestKey,
                RowVersion = Convert.ToBase64String(transfer.RowVersion),
                Type = transfer.Type,
                Purpose = transfer.Purpose,
                Status = transfer.Status,
                DocumentDate = transfer.DocumentDate,
                CreatedAt = transfer.CreatedAt,
                ConfirmedAt = transfer.ConfirmedAt,
                CancelledAt = transfer.CancelledAt,
                FromStoreId = transfer.FromStoreId,
                ToStoreId = transfer.ToStoreId,
                FromStoreName = transfer.FromStore?.Name ?? string.Empty,
                ToStoreName = transfer.ToStore?.Name ?? string.Empty,
                CreatedByName = transfer.CreatedByStaff?.FullName ?? string.Empty,
                ConfirmedByName = transfer.ConfirmedByStaff?.FullName,
                CancelledByName = transfer.CancelledByStaff?.FullName,
                Note = transfer.Note,
                ParentInventoryTransferId = transfer.ParentInventoryTransferId,
                ParentTransferCode = transfer.ParentInventoryTransfer?.Code,
                CanReceive = transfer.Status == InventoryTransferStatus.DISPATCHED && destinationOperator,
                CanRequestReturn = transfer.Status == InventoryTransferStatus.DISPATCHED && destinationOperator,
                CanConfirmReturn = transfer.Status == InventoryTransferStatus.DISPATCHED && sourceOperator,
                CanResolveShortage = transfer.Status == InventoryTransferStatus.DISPATCHED && canResolve,
                CanCreateFollowUp = transfer.Status == InventoryTransferStatus.DISPATCHED && canCoordinate,
                Details = detailItems,
                Timeline = timeline
            };
        }

        public async Task<List<InventoryTransferItemDTO>> GetTransferItemsAsync(int fromStoreId)
        {
            if (fromStoreId <= 0)
            {
                return [];
            }

            if (!await CanAccessStoreAsync(fromStoreId))
                return [];

            var inventories = await _repository.GetStoreInventoriesAsync(fromStoreId);
            var ingredientInventories = inventories
                .Where(x => x.IngredientId.HasValue)
                .Where(x => x.AvailableQty > 0)
                .Where(x => x.Ingredient != null && x.Ingredient.Active)
                .GroupBy(x => x.IngredientId!.Value)
                .Select(x => x.First())
                .OrderBy(x => x.Ingredient!.Name)
                .ToList();

            var result = new List<InventoryTransferItemDTO>();

            foreach (var inventory in ingredientInventories)
            {
                var ingredient = inventory.Ingredient!;

                var unitOptions = BuildUnitOptions(ingredient);
                var defaultUnit = unitOptions.FirstOrDefault(x => x.IsBaseUnit)
                    ?? unitOptions.FirstOrDefault();
                var baseUnitCost = await EstimateBaseUnitCostAsync(fromStoreId, ingredient.IngredientId);
                var conversionFactor = defaultUnit?.ConversionFactorToBase ?? 0;

                result.Add(
                    new InventoryTransferItemDTO
                    {
                        ItemType = "INGREDIENT",
                        IngredientId = ingredient.IngredientId,
                        ItemName = ingredient.Name,
                        BaseUnitId = ingredient.BaseUnitId,
                        BaseUnitName = ingredient.BaseUnit?.Name ?? string.Empty,
                        BaseUnitCode = ingredient.BaseUnit?.UnitCode ?? string.Empty,
                        AvailableBaseQuantity = inventory.AvailableQty,
                        SuggestedBaseUnitCost = baseUnitCost,
                        SuggestedUnitPrice = conversionFactor > 0 ? baseUnitCost * conversionFactor : 0,
                        UnitOptions = unitOptions
                    });
            }

            var preparedInventories = inventories
                .Where(x => x.PreparedItemId.HasValue)
                .Where(x => x.AvailableQty > 0)
                .Where(x => x.PreparedItem != null && x.PreparedItem.Active)
                .GroupBy(x => x.PreparedItemId!.Value)
                .Select(x => x.First())
                .OrderBy(x => x.PreparedItem!.Name)
                .ToList();

            foreach (var inventory in preparedInventories)
            {
                var preparedItem = inventory.PreparedItem!;
                var baseUnitCost = await EstimatePreparedItemBaseUnitCostAsync(
                    fromStoreId,
                    preparedItem.PreparedItemId);
                result.Add(new InventoryTransferItemDTO
                {
                    ItemType = "PREPARED_ITEM",
                    PreparedItemId = preparedItem.PreparedItemId,
                    ItemName = preparedItem.Name,
                    BaseUnitId = preparedItem.BaseUnitId,
                    BaseUnitName = preparedItem.BaseUnit?.Name ?? string.Empty,
                    BaseUnitCode = preparedItem.BaseUnit?.UnitCode ?? string.Empty,
                    AvailableBaseQuantity = inventory.AvailableQty,
                    SuggestedBaseUnitCost = baseUnitCost,
                    SuggestedUnitPrice = baseUnitCost,
                    UnitOptions =
                    [
                        new InventoryUnitOptionDTO
                        {
                            UnitId = preparedItem.BaseUnitId,
                            UnitName = preparedItem.BaseUnit?.Name ?? string.Empty,
                            UnitCode = preparedItem.BaseUnit?.UnitCode ?? string.Empty,
                            ConversionFactorToBase = 1,
                            IsBaseUnit = true
                        }
                    ]
                });
            }

            return result
                .OrderBy(x => x.ItemType)
                .ThenBy(x => x.ItemName)
                .ToList();
        }

        public async Task<InventoryTransferMutationResultDTO> CreateDraftAsync(InventoryTransferMutationDTO dto)
        {
            RequestDeduplicationBeginResult? dedup = null;

            await _repository.BeginTransactionAsync();

            try
            {
                var staffId = GetCurrentStaffId();
                dedup = await _deduplicationService.BeginAsync(
                    dto.RequestKey,
                    CreateDraftAction,
                    staffId,
                    dto);

                if (!dedup.CanProcess)
                {
                    await _repository.RollbackTransactionAsync();
                    return await ResolveDuplicateResultAsync(dedup);
                }

                await ValidateAndNormalizeAsync(dto);

                var transfer = new InventoryTransfer
                {
                    Code = await _repository.GenerateTransferCodeAsync(),
                    RequestKey = dto.RequestKey?.Trim(),
                    FromStoreId = dto.FromStoreId,
                    ToStoreId = dto.ToStoreId,
                    Type = InventoryTransferType.STORE_TO_STORE,
                    Purpose = dto.Purpose,
                    Status = InventoryTransferStatus.DRAFT,
                    DocumentDate = dto.DocumentDate.Date,
                    CreatedByStaffId = staffId,
                    CreatedAt = DateTime.UtcNow,
                    Note = NormalizeNote(dto.Note),
                    Details = BuildTransferDetails(dto)
                };

                await _repository.AddTransferAsync(transfer);
                await _repository.SaveChangesAsync();

                var response = BuildResult(transfer);
                await _deduplicationService.MarkSuccessAsync(
                    dedup.Entry!,
                    transfer.InventoryTransferId,
                    response);

                await _repository.CommitTransactionAsync();

                return response;
            }
            catch (Exception ex)
            {
                await MarkFailedIfPossibleAsync(dedup, ex.Message);
                await _repository.RollbackTransactionAsync();
                throw;
            }
        }

        public async Task<InventoryTransferMutationResultDTO> UpdateDraftAsync(
            int id,
            InventoryTransferMutationDTO dto)
        {
            if (dto.TransferId.HasValue && dto.TransferId.Value != id)
                throw new InvalidOperationException("INVALID_TRANSFER_ID");

            RequestDeduplicationBeginResult? dedup = null;

            await _repository.BeginTransactionAsync();

            try
            {
                var staffId = GetCurrentStaffId();
                dedup = await _deduplicationService.BeginAsync(
                    dto.RequestKey,
                    UpdateDraftAction,
                    staffId,
                    new { id, dto },
                    id);

                if (!dedup.CanProcess)
                {
                    await _repository.RollbackTransactionAsync();
                    return await ResolveDuplicateResultAsync(dedup);
                }

                var transfer = await _repository.GetTransferForUpdateAsync(id)
                    ?? throw new InvalidOperationException("Không tìm thấy phiếu chuyển kho.");

                await EnsureTransferScopeAsync(transfer.FromStoreId, transfer.ToStoreId);
                EnsureTransferRowVersion(transfer, dto.RowVersion);

                if (transfer.Status != InventoryTransferStatus.DRAFT)
                {
                    throw new InvalidOperationException("Chỉ được sửa phiếu chuyển kho ở trạng thái nháp.");
                }

                await ValidateAndNormalizeAsync(dto);

                var oldDetails = transfer.Details.ToList();
                transfer.Details.Clear();
                _repository.RemoveTransferDetails(oldDetails);

                transfer.FromStoreId = dto.FromStoreId;
                transfer.ToStoreId = dto.ToStoreId;
                transfer.Purpose = dto.Purpose;
                transfer.DocumentDate = dto.DocumentDate.Date;
                transfer.Note = NormalizeNote(dto.Note);

                foreach (var detail in BuildTransferDetails(dto))
                {
                    transfer.Details.Add(detail);
                }

                _repository.UpdateTransfer(transfer);
                await _repository.SaveChangesAsync();

                var response = BuildResult(transfer);
                await _deduplicationService.MarkSuccessAsync(
                    dedup.Entry!,
                    transfer.InventoryTransferId,
                    response);

                await _repository.CommitTransactionAsync();

                return response;
            }
            catch (Exception ex)
            {
                await MarkFailedIfPossibleAsync(dedup, ex.Message);
                await _repository.RollbackTransactionAsync();
                throw;
            }
        }

        public Task<InventoryTransferMutationResultDTO> ConfirmAsync(int id, string? requestKey) =>
            DispatchAsync(id, requestKey);

        public async Task<InventoryTransferMutationResultDTO> DispatchAsync(int id, string? requestKey)
        {
            RequestDeduplicationBeginResult? dedup = null;

            await _repository.BeginTransactionAsync();

            try
            {
                var staffId = GetCurrentStaffId();
                dedup = await _deduplicationService.BeginAsync(
                    requestKey,
                    DispatchAction,
                    staffId,
                    new { id, requestKey },
                    id);

                if (!dedup.CanProcess)
                {
                    await _repository.RollbackTransactionAsync();
                    return await ResolveDuplicateResultAsync(dedup);
                }

                var transfer = await _repository.GetTransferForUpdateAsync(id)
                    ?? throw new InvalidOperationException("Không tìm thấy phiếu chuyển kho.");

                await EnsureTransferScopeAsync(transfer.FromStoreId, transfer.ToStoreId);

                if (transfer.Status is InventoryTransferStatus.DISPATCHED or InventoryTransferStatus.COMPLETED)
                {
                    var completedResult = BuildResult(transfer);
                    await _deduplicationService.MarkSuccessAsync(
                        dedup.Entry!,
                        transfer.InventoryTransferId,
                        completedResult);
                    await _repository.CommitTransactionAsync();
                    return completedResult;
                }

                if (transfer.Status == InventoryTransferStatus.CANCELLED)
                {
                    throw new InvalidOperationException("Phiếu đã hủy, không thể xác nhận.");
                }

                if (!transfer.Details.Any())
                {
                    throw new InvalidOperationException("Phiếu chuyển kho phải có ít nhất một nguyên liệu.");
                }

                await LockTransferInventoriesAsync(transfer);
                var (warnings, affectedInventoryIds) = await ProcessDispatchAsync(transfer, staffId);

                transfer.Status = InventoryTransferStatus.DISPATCHED;
                transfer.DispatchedAt = DateTime.UtcNow;
                transfer.ConfirmedAt = DateTime.UtcNow;
                transfer.ConfirmedByStaffId = staffId;

                _repository.UpdateTransfer(transfer);
                await _repository.SaveChangesAsync();

                var response = BuildResult(transfer, warnings);
                await _deduplicationService.MarkSuccessAsync(
                    dedup.Entry!,
                    transfer.InventoryTransferId,
                    response);

                await _repository.CommitTransactionAsync();

                foreach (var inventoryId in affectedInventoryIds.Distinct())
                {
                    try
                    {
                        await _stockAlertService.EvaluateStoreInventoryItemAsync(
                            inventoryId,
                            "INVENTORY_TRANSFER_DISPATCHED");
                    }
                    catch
                    {
                        // Transfer is already committed; scheduled reconciliation can retry alerts.
                    }
                }

                return response;
            }
            catch (Exception ex)
            {
                await MarkFailedIfPossibleAsync(dedup, ex.Message);
                await _repository.RollbackTransactionAsync();
                throw;
            }
        }

        public async Task<InventoryTransferMutationResultDTO> ReceiveAsync(
            int id,
            InventoryTransferReceiveDTO dto)
        {
            RequestDeduplicationBeginResult? dedup = null;
            await _repository.BeginTransactionAsync();

            try
            {
                var staffId = GetCurrentStaffId();
                dedup = await _deduplicationService.BeginAsync(
                    dto.RequestKey,
                    ReceiveAction,
                    staffId,
                    new { id, dto.RequestKey, dto.ReceivedAt, dto.Lines },
                    id);
                if (!dedup.CanProcess)
                {
                    await _repository.RollbackTransactionAsync();
                    return await ResolveDuplicateResultAsync(dedup);
                }

                var transfer = await _repository.GetTransferForUpdateAsync(id)
                    ?? throw new InvalidOperationException("Không tìm thấy phiếu chuyển kho.");
                await EnsureStoreScopeAsync(transfer.ToStoreId);
                EnsureOperationalRole();

                if (transfer.Status == InventoryTransferStatus.COMPLETED)
                {
                    var replay = BuildResult(transfer);
                    await _deduplicationService.MarkSuccessAsync(dedup.Entry!, id, replay);
                    await _repository.CommitTransactionAsync();
                    return replay;
                }
                EnsureTransferRowVersion(transfer, dto.RowVersion);
                if (transfer.Status != InventoryTransferStatus.DISPATCHED)
                    throw new InvalidOperationException("Chỉ phiếu DISPATCHED mới được nhận kho.");
                if (dto.Lines.Count == 0)
                    throw new InvalidOperationException("Phiếu nhận phải có ít nhất một dòng.");
                if (dto.Lines.Any(x => x.InventoryTransferDetailId <= 0
                    || x.ReceivedBaseQuantity < 0
                    || x.RejectedBaseQuantity < 0
                    || x.ReceivedBaseQuantity + x.RejectedBaseQuantity <= 0))
                    throw new InvalidOperationException("Tổng số lượng nhận hợp lệ và từ chối phải lớn hơn 0.");
                if (dto.Lines.Any(x => x.RejectedBaseQuantity > 0
                    && (string.IsNullOrWhiteSpace(x.RejectionIssueType)
                        || string.IsNullOrWhiteSpace(x.RejectionReason))))
                    throw new InvalidOperationException("Lý do và loại chênh lệch là bắt buộc khi từ chối hàng.");
                if (dto.Lines.GroupBy(x => x.InventoryTransferDetailId).Any(x => x.Count() > 1))
                    throw new InvalidOperationException("Dòng nhận chuyển kho bị trùng.");

                var detailById = transfer.Details.ToDictionary(x => x.InventoryTransferDetailId);
                var detailIds = dto.Lines.Select(x => x.InventoryTransferDetailId).ToArray();
                var existingPostings = await _repository.GetTransferDiscrepancyPostingsAsync(detailIds) ?? [];
                foreach (var line in dto.Lines)
                {
                    if (!detailById.TryGetValue(line.InventoryTransferDetailId, out var detail))
                        throw new InvalidOperationException("Dòng nhận không thuộc phiếu chuyển kho.");
                    var authority = InventoryTransferQuantityAuthority.Calculate(detail, existingPostings);
                    if (line.ReceivedBaseQuantity + line.RejectedBaseQuantity > authority.InTransitOpen)
                        throw new InvalidOperationException("Số lượng nhận/từ chối vượt lượng còn đang xử lý.");
                }

                await LockTransferInventoriesAsync(transfer);
                var allocations = await _repository.GetTransferCostAllocationsAsync(detailIds);
                var actorAccountId = await _repository.GetAccountIdForStaffAsync(staffId)
                    ?? throw new InvalidOperationException("Không xác định được tài khoản người nhận.");
                var newPostings = new List<InventoryTransferDiscrepancyPosting>();
                var receipt = new BranchReceipt
                {
                    ReceiptCode = $"TR-{transfer.InventoryTransferId}-{DateTime.UtcNow:yyyyMMddHHmmssfff}",
                    StoreId = transfer.ToStoreId,
                    SourceInventoryTransferId = transfer.InventoryTransferId,
                    Status = "CONFIRMED",
                    ReceiptKey = dto.RequestKey!.Trim(),
                    ReferenceNumber = transfer.Code,
                    ReceivedAt = dto.ReceivedAt,
                    ReceivedByStaffId = staffId,
                    ConfirmedAt = DateTime.UtcNow,
                    ConfirmedByStaffId = staffId,
                    Notes = dto.Note,
                    CreatedAt = DateTime.UtcNow,
                    CreatedByStaffId = staffId
                };

                foreach (var receiveLine in dto.Lines.OrderBy(x => x.InventoryTransferDetailId))
                {
                    var detail = detailById[receiveLine.InventoryTransferDetailId];
                    var ingredient = detail.Ingredient;
                    var preparedItem = detail.PreparedItem;
                    StoreInventory? inventory = null;
                    if (receiveLine.ReceivedBaseQuantity > 0)
                    {
                        inventory = ingredient != null
                            ? await _repository.GetOrCreateStoreInventoryForUpdateAsync(transfer.ToStoreId, ingredient.IngredientId)
                            : await _repository.GetOrCreatePreparedItemInventoryForUpdateAsync(
                                transfer.ToStoreId,
                                preparedItem!.PreparedItemId,
                                actorAccountId,
                                $"INVENTORY_TRANSFER_RECEIPT:{transfer.InventoryTransferId}");
                        detail.DestinationBeforeQty ??= inventory.AvailableQty;
                    }
                    var remainingAccepted = receiveLine.ReceivedBaseQuantity;
                    var remainingRejected = receiveLine.RejectedBaseQuantity;
                    var detailAllocations = allocations
                        .Where(x => x.InventoryTransferDetailId == detail.InventoryTransferDetailId)
                        .OrderBy(x => x.InventoryTransferCostAllocationId)
                        .ToList();

                    foreach (var allocation in detailAllocations)
                    {
                        if (remainingAccepted <= 0 && remainingRejected <= 0)
                            break;
                        var allPostings = existingPostings.Concat(newPostings);
                        var available = allocation.Quantity
                            - InventoryTransferQuantityAuthority.AllocationClassifiedQuantity(allocation, allPostings);
                        if (available <= 0)
                            continue;

                        var accepted = Math.Min(remainingAccepted, available);
                        remainingAccepted -= accepted;
                        available -= accepted;
                        var rejected = Math.Min(remainingRejected, available);
                        remainingRejected -= rejected;

                        var receiptLine = new BranchReceiptLine
                        {
                            RestockRequestId = detail.RestockRequestId,
                            SourceInventoryTransferDetailId = detail.InventoryTransferDetailId,
                            SourceTransferCostAllocationId = allocation.InventoryTransferCostAllocationId,
                            IngredientId = detail.IngredientId,
                            PreparedItemId = detail.PreparedItemId,
                            InputQuantity = accepted + rejected,
                            InputUnitId = ingredient?.BaseUnitId ?? preparedItem!.BaseUnitId,
                            ReceivedBaseQuantity = accepted,
                            RejectedBaseQuantity = rejected,
                            RejectionIssueType = rejected > 0 ? receiveLine.RejectionIssueType!.Trim() : null,
                            RejectionReason = rejected > 0 ? receiveLine.RejectionReason!.Trim() : null,
                            BaseUnitId = ingredient?.BaseUnitId ?? preparedItem!.BaseUnitId,
                            BaseUnitCostSnapshot = allocation.UnitCost,
                            LineTotalCost = accepted * allocation.UnitCost,
                            CreatedAt = DateTime.UtcNow
                        };
                        receipt.Lines.Add(receiptLine);

                        if (accepted > 0)
                        {
                            var before = inventory!.AvailableQty;
                            inventory.AvailableQty += accepted;
                            allocation.ReceivedQuantity += accepted;
                            await _repository.AddInventoryTransactionAsync(new InventoryTransaction
                            {
                                StoreInventoryId = inventory.StoreInventoryId,
                                InventoryTransferId = transfer.InventoryTransferId,
                                InventoryTransferDetailId = detail.InventoryTransferDetailId,
                                BranchReceiptLine = receiptLine,
                                Type = InventoryTransactionTypeEnum.IN_TRANSFER,
                                StockStatus = inventory.AvailableQty < 0
                                    ? InventoryStockStatus.NEGATIVE_CONFIRMED
                                    : InventoryStockStatus.NORMAL,
                                Quantity = accepted,
                                BeforeQty = before,
                                AfterQty = inventory.AvailableQty,
                                UnitCost = allocation.UnitCost,
                                TotalCost = accepted * allocation.UnitCost,
                                CreatedAt = DateTime.UtcNow
                            });
                            var inboundLayer = new InventoryCostLayer
                            {
                                StoreId = transfer.ToStoreId,
                                IngredientId = detail.IngredientId,
                                PreparedItemId = detail.PreparedItemId,
                                Quantity = accepted,
                                RemainingQuantity = accepted,
                                UnitCost = allocation.UnitCost,
                                SourceBranchReceiptLine = receiptLine,
                                SourceTransferCostAllocation = allocation,
                                CreatedAt = DateTime.UtcNow
                            };
                            await SettleDestinationGapAsync(inventory, before, inboundLayer);
                            await _repository.AddCostLayerAsync(inboundLayer);
                        }

                        if (rejected > 0)
                        {
                            newPostings.Add(new InventoryTransferDiscrepancyPosting
                            {
                                InventoryTransferDetailId = detail.InventoryTransferDetailId,
                                InventoryTransferCostAllocationId = allocation.InventoryTransferCostAllocationId,
                                PostingType = InventoryTransferDiscrepancyPostingType.DESTINATION_REJECTED,
                                Quantity = rejected,
                                UnitCost = allocation.UnitCost,
                                TotalCost = rejected * allocation.UnitCost,
                                RequestKey = dto.RequestKey!.Trim(),
                                Reason = $"{receiveLine.RejectionIssueType!.Trim()}: {receiveLine.RejectionReason!.Trim()}",
                                ActorStaffId = staffId,
                                CreatedAt = DateTime.UtcNow
                            });
                        }
                    }

                    if (remainingAccepted > 0 || remainingRejected > 0)
                        throw new InvalidOperationException("COST_TRACE_INCOMPLETE: thiếu cost allocation cho số lượng nhận/từ chối.");

                    detail.ReceivedBaseQuantity += receiveLine.ReceivedBaseQuantity;
                    if (inventory != null)
                    {
                        detail.DestinationAfterQty = inventory.AvailableQty;
                        _repository.UpdateStoreInventory(inventory);
                    }

                }

                await _repository.AddTransferDiscrepancyPostingsAsync(newPostings);
                await _repository.AddBranchReceiptAsync(receipt);
                var finalPostings = existingPostings.Concat(newPostings).ToList();
                if (transfer.Details.All(x =>
                    InventoryTransferQuantityAuthority.Calculate(x, finalPostings).InTransitOpen <= 0))
                    transfer.Status = InventoryTransferStatus.COMPLETED;
                _repository.UpdateTransfer(transfer);
                await _repository.SaveChangesAsync();

                // A transfer can be received in multiple physical receipts. Persist the
                // receipt first so each fulfillment posting is keyed by its immutable
                // BranchReceiptLine instead of collapsing all receipts onto the transfer detail.
                foreach (var line in receipt.Lines
                    .Where(x => x.ReceivedBaseQuantity > 0 && x.RestockRequestId.HasValue)
                    .OrderBy(x => x.BranchReceiptLineId))
                {
                    var posting = await _fulfillmentPostingService.RegisterAsync(
                        new RegisterRestockFulfillmentPostingCommand
                        {
                            RestockRequestId = line.RestockRequestId!.Value,
                            DestinationStoreId = transfer.ToStoreId,
                            SourceDocumentType = RestockFulfillmentDocumentTypes.InventoryTransfer,
                            SourceDocumentId = transfer.InventoryTransferId,
                            SourceDocumentLineId = line.BranchReceiptLineId,
                            IngredientId = line.IngredientId,
                            PreparedItemId = line.PreparedItemId,
                            Quantity = line.ReceivedBaseQuantity,
                            BaseUnitId = line.BaseUnitId,
                            ActorStaffId = staffId,
                            Reason = $"InventoryTransfer #{transfer.InventoryTransferId} RECEIVED"
                        });
                    if (!posting.IsSuccess)
                        throw new InvalidOperationException(posting.Message);
                }

                await _repository.SaveChangesAsync();

                var response = BuildResult(transfer);
                await _deduplicationService.MarkSuccessAsync(dedup.Entry!, id, response);
                await _repository.CommitTransactionAsync();
                return response;
            }
            catch (Exception ex)
            {
                await _repository.RollbackTransactionAsync();
                await MarkFailedIfPossibleAsync(dedup, ex.Message);
                throw;
            }
        }

        public Task<InventoryTransferMutationResultDTO> RequestReturnAsync(
            int id,
            InventoryTransferResolutionDTO dto) =>
            PostDiscrepancyAsync(id, dto, RequestReturnAction, InventoryTransferDiscrepancyPostingType.RETURN_REQUESTED);

        public async Task<InventoryTransferMutationResultDTO> ConfirmReturnAsync(
            int id,
            InventoryTransferResolutionDTO dto)
        {
            RequestDeduplicationBeginResult? dedup = null;
            await _repository.BeginTransactionAsync();
            try
            {
                ValidateResolutionDto(dto);
                var staffId = GetCurrentStaffId();
                dedup = await _deduplicationService.BeginAsync(
                    dto.RequestKey,
                    ConfirmReturnAction,
                    staffId,
                    new { id, dto.RowVersion, dto.RequestKey, dto.Reason, dto.Lines },
                    id);
                if (!dedup.CanProcess)
                {
                    await _repository.RollbackTransactionAsync();
                    return await ResolveDuplicateResultAsync(dedup);
                }

                var transfer = await _repository.GetTransferForUpdateAsync(id)
                    ?? throw new InvalidOperationException("Không tìm thấy phiếu chuyển kho.");
                await EnsureStoreScopeAsync(transfer.FromStoreId);
                EnsureOperationalRole();
                EnsureTransferRowVersion(transfer, dto.RowVersion);
                EnsureDispatchedTransfer(transfer);

                var detailById = transfer.Details.ToDictionary(x => x.InventoryTransferDetailId);
                ValidateResolutionLines(dto.Lines, detailById);
                await LockTransferInventoriesAsync(transfer);
                var allocations = await _repository.GetTransferCostAllocationsAsync(detailById.Keys);
                var postings = await _repository.GetTransferDiscrepancyPostingsAsync(detailById.Keys) ?? [];
                var actorAccountId = await _repository.GetAccountIdForStaffAsync(staffId)
                    ?? throw new InvalidOperationException("Không xác định được tài khoản người xác nhận hoàn trả.");
                var created = new List<InventoryTransferDiscrepancyPosting>();

                foreach (var input in dto.Lines.OrderBy(x => x.InventoryTransferDetailId))
                {
                    var detail = detailById[input.InventoryTransferDetailId];
                    var inventory = detail.IngredientId.HasValue
                        ? await _repository.GetOrCreateStoreInventoryForUpdateAsync(
                            transfer.FromStoreId,
                            detail.IngredientId.Value)
                        : await _repository.GetOrCreatePreparedItemInventoryForUpdateAsync(
                            transfer.FromStoreId,
                            detail.PreparedItemId!.Value,
                            actorAccountId,
                            $"TRANSFER_RETURN:{transfer.InventoryTransferId}");
                    var remaining = input.BaseQuantity;
                    var detailAllocations = allocations
                        .Where(x => x.InventoryTransferDetailId == detail.InventoryTransferDetailId)
                        .OrderBy(x => x.InventoryTransferCostAllocationId)
                        .ToList();

                    foreach (var allocation in detailAllocations)
                    {
                        if (remaining <= 0)
                            break;
                        var requests = postings
                            .Concat(created)
                            .Where(x => x.InventoryTransferCostAllocationId == allocation.InventoryTransferCostAllocationId
                                && x.PostingType == InventoryTransferDiscrepancyPostingType.RETURN_REQUESTED)
                            .OrderBy(x => x.CreatedAt)
                            .ThenBy(x => x.InventoryTransferDiscrepancyPostingId)
                            .ToList();
                        foreach (var request in requests)
                        {
                            if (remaining <= 0)
                                break;
                            var alreadyReturned = postings.Concat(created)
                                .Where(x => x.PostingType == InventoryTransferDiscrepancyPostingType.RETURNED_TO_SOURCE
                                    && x.RelatedPostingId == request.InventoryTransferDiscrepancyPostingId)
                                .Sum(x => x.Quantity);
                            var pending = request.Quantity - alreadyReturned;
                            var quantity = Math.Min(remaining, pending);
                            if (quantity <= 0)
                                continue;

                            var before = inventory.AvailableQty;
                            inventory.AvailableQty += quantity;
                            remaining -= quantity;
                            var returnPosting = NewPosting(
                                detail,
                                allocation,
                                InventoryTransferDiscrepancyPostingType.RETURNED_TO_SOURCE,
                                quantity,
                                dto.RequestKey!,
                                dto.Reason!,
                                staffId,
                                request.InventoryTransferDiscrepancyPostingId);
                            created.Add(returnPosting);

                            await _repository.AddInventoryTransactionAsync(new InventoryTransaction
                            {
                                StoreInventoryId = inventory.StoreInventoryId,
                                InventoryTransferId = transfer.InventoryTransferId,
                                InventoryTransferDetailId = detail.InventoryTransferDetailId,
                                Type = InventoryTransactionTypeEnum.TRANSFER_RETURN_IN,
                                StockStatus = inventory.AvailableQty < 0
                                    ? InventoryStockStatus.NEGATIVE_CONFIRMED
                                    : InventoryStockStatus.NORMAL,
                                Quantity = quantity,
                                BeforeQty = before,
                                AfterQty = inventory.AvailableQty,
                                UnitCost = allocation.UnitCost,
                                TotalCost = quantity * allocation.UnitCost,
                                CreatedAt = DateTime.UtcNow
                            });
                            var layer = new InventoryCostLayer
                            {
                                StoreId = transfer.FromStoreId,
                                IngredientId = detail.IngredientId,
                                PreparedItemId = detail.PreparedItemId,
                                Quantity = quantity,
                                RemainingQuantity = quantity,
                                UnitCost = allocation.UnitCost,
                                SourceTransferCostAllocation = allocation,
                                SourceTransferDiscrepancyPosting = returnPosting,
                                CreatedAt = DateTime.UtcNow
                            };
                            await SettleDestinationGapAsync(inventory, before, layer);
                            await _repository.AddCostLayerAsync(layer);
                        }
                    }

                    if (remaining > 0)
                        throw new InvalidOperationException("Số lượng xác nhận hoàn trả vượt lượng đã yêu cầu trả.");
                    _repository.UpdateStoreInventory(inventory);
                }

                await _repository.AddTransferDiscrepancyPostingsAsync(created);
                CompleteTransferWhenResolved(transfer, postings.Concat(created));
                _repository.UpdateTransfer(transfer);
                await _repository.SaveChangesAsync();
                var result = BuildResult(transfer);
                await _deduplicationService.MarkSuccessAsync(dedup.Entry!, id, result);
                await _repository.CommitTransactionAsync();
                return result;
            }
            catch (Exception ex)
            {
                await _repository.RollbackTransactionAsync();
                await MarkFailedIfPossibleAsync(dedup, ex.Message);
                throw;
            }
        }

        public async Task<InventoryTransferMutationResultDTO> ResolveShortageAsync(
            int id,
            InventoryTransferResolutionDTO dto)
        {
            if (dto.ResolutionType is not InventoryTransferDiscrepancyPostingType.WRITTEN_OFF
                and not InventoryTransferDiscrepancyPostingType.CLOSED_SHORTAGE)
                throw new InvalidOperationException("ResolutionType phải là WRITTEN_OFF hoặc CLOSED_SHORTAGE.");
            EnsureOwnerRole();
            return await PostDiscrepancyAsync(id, dto, ResolveShortageAction, dto.ResolutionType.Value);
        }

        public async Task<InventoryTransferMutationResultDTO> CreateFollowUpAsync(
            int id,
            InventoryTransferFollowUpDTO dto)
        {
            if (string.IsNullOrWhiteSpace(dto.RequestKey))
                throw new InvalidOperationException("RequestKey là bắt buộc.");
            if (dto.Lines.Count == 0 || dto.Lines.Any(x => x.InventoryTransferDetailId <= 0 || x.BaseQuantity <= 0))
                throw new InvalidOperationException("Điều chuyển gửi bù phải có số lượng hợp lệ.");
            EnsureCoordinatorRole();

            RequestDeduplicationBeginResult? dedup = null;
            await _repository.BeginTransactionAsync();
            try
            {
                var staffId = GetCurrentStaffId();
                dedup = await _deduplicationService.BeginAsync(
                    dto.RequestKey,
                    FollowUpAction,
                    staffId,
                    new { id, dto.RowVersion, dto.RequestKey, dto.Note, dto.Lines });
                if (!dedup.CanProcess)
                {
                    await _repository.RollbackTransactionAsync();
                    return await ResolveDuplicateResultAsync(dedup);
                }

                var parent = await _repository.GetTransferForUpdateAsync(id)
                    ?? throw new InvalidOperationException("Không tìm thấy phiếu chuyển kho gốc.");
                await EnsureTransferScopeAsync(parent.FromStoreId, parent.ToStoreId);
                EnsureTransferRowVersion(parent, dto.RowVersion);
                EnsureDispatchedTransfer(parent);
                var detailById = parent.Details.ToDictionary(x => x.InventoryTransferDetailId);
                ValidateResolutionLines(dto.Lines, detailById);
                var postings = await _repository.GetTransferDiscrepancyPostingsAsync(detailById.Keys) ?? [];

                foreach (var input in dto.Lines)
                {
                    var authority = InventoryTransferQuantityAuthority.Calculate(detailById[input.InventoryTransferDetailId], postings);
                    if (input.BaseQuantity > authority.InTransitOpen)
                        throw new InvalidOperationException("Số lượng gửi bù vượt lượng còn đang xử lý.");
                }

                var followUp = new InventoryTransfer
                {
                    Code = await _repository.GenerateTransferCodeAsync(),
                    RequestKey = dto.RequestKey.Trim(),
                    ParentInventoryTransferId = parent.InventoryTransferId,
                    FromStoreId = parent.FromStoreId,
                    ToStoreId = parent.ToStoreId,
                    Type = InventoryTransferType.STORE_TO_STORE,
                    Purpose = parent.Purpose,
                    Status = InventoryTransferStatus.DRAFT,
                    DocumentDate = DateTime.UtcNow.Date,
                    CreatedByStaffId = staffId,
                    CreatedAt = DateTime.UtcNow,
                    Note = NormalizeNote(dto.Note) ?? $"Gửi bù cho {parent.Code}"
                };
                foreach (var input in dto.Lines)
                {
                    var source = detailById[input.InventoryTransferDetailId];
                    followUp.Details.Add(new InventoryTransferDetail
                    {
                        ParentInventoryTransferDetailId = source.InventoryTransferDetailId,
                        IngredientId = source.IngredientId,
                        PreparedItemId = source.PreparedItemId,
                        RestockRequestId = source.RestockRequestId,
                        UnitId = source.Ingredient?.BaseUnitId ?? source.PreparedItem!.BaseUnitId,
                        Quantity = input.BaseQuantity,
                        BaseQuantity = input.BaseQuantity,
                        Note = $"Gửi bù từ {parent.Code}"
                    });
                }

                await _repository.AddTransferAsync(followUp);
                await _repository.SaveChangesAsync();
                var result = BuildResult(followUp);
                await _deduplicationService.MarkSuccessAsync(
                    dedup.Entry!, followUp.InventoryTransferId, result);
                await _repository.CommitTransactionAsync();
                return result;
            }
            catch (Exception ex)
            {
                await _repository.RollbackTransactionAsync();
                await MarkFailedIfPossibleAsync(dedup, ex.Message);
                throw;
            }
        }

        public async Task<List<InventoryTransferDiscrepancyDryRunRowDTO>> GetDiscrepancyDryRunAsync()
        {
            var transfers = await _repository.GetLegacyDispatchedTransfersAsync();
            var result = new List<InventoryTransferDiscrepancyDryRunRowDTO>();
            foreach (var transfer in transfers)
            {
                var detailIds = transfer.Details.Select(x => x.InventoryTransferDetailId).ToArray();
                var allocations = await _repository.GetTransferCostAllocationsAsync(detailIds);
                var postings = await _repository.GetTransferDiscrepancyPostingsAsync(detailIds) ?? [];
                foreach (var detail in transfer.Details)
                {
                    var authority = InventoryTransferQuantityAuthority.Calculate(detail, postings);
                    if (authority.InTransitOpen <= 0)
                        continue;
                    var costRows = allocations.Where(x => x.InventoryTransferDetailId == detail.InventoryTransferDetailId).ToList();
                    var traceComplete = costRows.Count > 0
                        && costRows.All(x => x.UnitCost > 0)
                        && costRows.Sum(x => x.Quantity) >= detail.DispatchedBaseQuantity;
                    result.Add(new InventoryTransferDiscrepancyDryRunRowDTO
                    {
                        InventoryTransferId = transfer.InventoryTransferId,
                        TransferCode = transfer.Code,
                        InventoryTransferDetailId = detail.InventoryTransferDetailId,
                        ItemName = detail.Ingredient?.Name ?? detail.PreparedItem?.Name ?? string.Empty,
                        DispatchedBaseQuantity = authority.Dispatched,
                        DestinationAccepted = authority.DestinationAccepted,
                        DestinationRejected = authority.DestinationRejected,
                        ReturnedToSource = authority.ReturnedToSource,
                        WrittenOff = authority.WrittenOff,
                        ClosedShortage = authority.ClosedShortage,
                        InTransitOpen = authority.InTransitOpen,
                        SuggestedStatus = authority.Status,
                        TraceConfidence = traceComplete ? "EXACT_TRANSFER_COST" : "MANUAL_REVIEW_REQUIRED"
                    });
                }
            }
            return result;
        }

        private async Task<InventoryTransferMutationResultDTO> PostDiscrepancyAsync(
            int id,
            InventoryTransferResolutionDTO dto,
            string action,
            InventoryTransferDiscrepancyPostingType postingType)
        {
            ValidateResolutionDto(dto);
            RequestDeduplicationBeginResult? dedup = null;
            await _repository.BeginTransactionAsync();
            try
            {
                var staffId = GetCurrentStaffId();
                dedup = await _deduplicationService.BeginAsync(
                    dto.RequestKey,
                    action,
                    staffId,
                    new { id, dto.RowVersion, dto.RequestKey, dto.Reason, dto.Lines, postingType },
                    id);
                if (!dedup.CanProcess)
                {
                    await _repository.RollbackTransactionAsync();
                    return await ResolveDuplicateResultAsync(dedup);
                }

                var transfer = await _repository.GetTransferForUpdateAsync(id)
                    ?? throw new InvalidOperationException("Không tìm thấy phiếu chuyển kho.");
                if (postingType == InventoryTransferDiscrepancyPostingType.RETURN_REQUESTED)
                {
                    await EnsureStoreScopeAsync(transfer.ToStoreId);
                    EnsureOperationalRole();
                }
                else
                {
                    await EnsureTransferScopeAsync(transfer.FromStoreId, transfer.ToStoreId);
                    EnsureOwnerRole();
                }
                EnsureTransferRowVersion(transfer, dto.RowVersion);
                EnsureDispatchedTransfer(transfer);
                var detailById = transfer.Details.ToDictionary(x => x.InventoryTransferDetailId);
                ValidateResolutionLines(dto.Lines, detailById);
                var allocations = await _repository.GetTransferCostAllocationsAsync(detailById.Keys);
                var postings = await _repository.GetTransferDiscrepancyPostingsAsync(detailById.Keys) ?? [];
                var created = new List<InventoryTransferDiscrepancyPosting>();

                foreach (var input in dto.Lines.OrderBy(x => x.InventoryTransferDetailId))
                {
                    var detail = detailById[input.InventoryTransferDetailId];
                    var authority = InventoryTransferQuantityAuthority.Calculate(detail, postings.Concat(created));
                    if (postingType != InventoryTransferDiscrepancyPostingType.RETURN_REQUESTED
                        && authority.PendingReturn > 0)
                        throw new InvalidOperationException("Phần thiếu đang trong quy trình hoàn trả, không thể đóng hoặc ghi nhận mất.");
                    if (input.BaseQuantity > authority.InTransitOpen)
                        throw new InvalidOperationException("Số lượng xử lý vượt lượng còn đang xử lý.");

                    var remaining = input.BaseQuantity;
                    foreach (var allocation in allocations
                        .Where(x => x.InventoryTransferDetailId == detail.InventoryTransferDetailId)
                        .OrderBy(x => x.InventoryTransferCostAllocationId))
                    {
                        if (remaining <= 0)
                            break;
                        var combined = postings.Concat(created).ToList();
                        decimal available;
                        if (postingType == InventoryTransferDiscrepancyPostingType.RETURN_REQUESTED)
                        {
                            available = InventoryTransferQuantityAuthority.AllocationReturnableQuantity(allocation, combined);
                        }
                        else
                        {
                            var resolved = combined.Where(x =>
                                    x.InventoryTransferCostAllocationId == allocation.InventoryTransferCostAllocationId
                                    && x.PostingType is InventoryTransferDiscrepancyPostingType.RETURNED_TO_SOURCE
                                        or InventoryTransferDiscrepancyPostingType.WRITTEN_OFF
                                        or InventoryTransferDiscrepancyPostingType.CLOSED_SHORTAGE)
                                .Sum(x => x.Quantity);
                            available = allocation.Quantity - allocation.ReceivedQuantity - resolved;
                        }
                        var quantity = Math.Min(remaining, Math.Max(0, available));
                        if (quantity <= 0)
                            continue;
                        created.Add(NewPosting(
                            detail,
                            allocation,
                            postingType,
                            quantity,
                            dto.RequestKey!,
                            dto.Reason!,
                            staffId));
                        remaining -= quantity;
                    }

                    if (remaining > 0)
                    {
                        var code = postingType == InventoryTransferDiscrepancyPostingType.RETURN_REQUESTED
                            ? "Số lượng yêu cầu trả vượt số đã từ chối chưa xử lý."
                            : "COST_TRACE_INCOMPLETE: không đủ cost allocation để xử lý phần thiếu.";
                        throw new InvalidOperationException(code);
                    }
                }

                await _repository.AddTransferDiscrepancyPostingsAsync(created);
                if (postingType != InventoryTransferDiscrepancyPostingType.RETURN_REQUESTED)
                    CompleteTransferWhenResolved(transfer, postings.Concat(created));
                _repository.UpdateTransfer(transfer);
                await _repository.SaveChangesAsync();
                var result = BuildResult(transfer);
                await _deduplicationService.MarkSuccessAsync(dedup.Entry!, id, result);
                await _repository.CommitTransactionAsync();
                return result;
            }
            catch (Exception ex)
            {
                await _repository.RollbackTransactionAsync();
                await MarkFailedIfPossibleAsync(dedup, ex.Message);
                throw;
            }
        }

        public async Task<bool> CancelAsync(int id, string? requestKey)
        {
            RequestDeduplicationBeginResult? dedup = null;

            await _repository.BeginTransactionAsync();

            try
            {
                var staffId = GetCurrentStaffId();
                dedup = await _deduplicationService.BeginAsync(
                    requestKey,
                    CancelAction,
                    staffId,
                    new { id, requestKey },
                    id);

                if (!dedup.CanProcess)
                {
                    await _repository.RollbackTransactionAsync();

                    if (dedup.Status == "SUCCESS")
                    {
                        return true;
                    }

                    throw BuildDeduplicationException(dedup);
                }

                var transfer = await _repository.GetTransferByIdAsync(id);

                if (transfer == null)
                {
                    return false;
                }

                await EnsureTransferScopeAsync(transfer.FromStoreId, transfer.ToStoreId);

                if (transfer.Status is InventoryTransferStatus.COMPLETED or InventoryTransferStatus.DISPATCHED)
                {
                    throw new InvalidOperationException("Chỉ phiếu DRAFT mới được hủy; phiếu đã dispatch phải dùng workflow hoàn trả.");
                }

                if (transfer.Status == InventoryTransferStatus.CANCELLED)
                {
                    await _deduplicationService.MarkSuccessAsync(
                        dedup.Entry!,
                        id,
                        new { id, status = transfer.Status.ToString() });
                    await _repository.CommitTransactionAsync();
                    return true;
                }

                transfer.Status = InventoryTransferStatus.CANCELLED;
                transfer.CancelledAt = DateTime.UtcNow;
                transfer.CancelledByStaffId = staffId;

                _repository.UpdateTransfer(transfer);
                await _repository.SaveChangesAsync();

                await _deduplicationService.MarkSuccessAsync(
                    dedup.Entry!,
                    transfer.InventoryTransferId,
                    new { id = transfer.InventoryTransferId, status = transfer.Status.ToString() });

                await _repository.CommitTransactionAsync();

                return true;
            }
            catch (Exception ex)
            {
                await MarkFailedIfPossibleAsync(dedup, ex.Message);
                await _repository.RollbackTransactionAsync();
                throw;
            }
        }

        public async Task<List<InventoryStockWarningDTO>> ValidateStockAsync(InventoryTransferMutationDTO dto)
        {
            if (dto.FromStoreId <= 0 || dto.Details == null || !dto.Details.Any())
            {
                return [];
            }

            await ValidateAndNormalizeAsync(dto);

            var inventories = await _repository.GetStoreInventoriesAsync(dto.FromStoreId);
            var warnings = new List<InventoryStockWarningDTO>();

            foreach (var detail in dto.Details)
            {
                var ingredient = detail.IngredientId.HasValue
                    ? await _repository.GetIngredientAsync(detail.IngredientId.Value)
                    : null;
                var preparedItem = detail.PreparedItemId.HasValue
                    ? await _repository.GetPreparedItemAsync(detail.PreparedItemId.Value)
                    : null;
                if (ingredient == null && preparedItem == null)
                    throw new InvalidOperationException("Mặt hàng chuyển kho không tồn tại.");

                var inventory = inventories.FirstOrDefault(x =>
                    detail.IngredientId.HasValue
                        ? x.IngredientId == detail.IngredientId
                        : x.PreparedItemId == detail.PreparedItemId);
                inventory ??= new StoreInventory
                {
                    StoreId = dto.FromStoreId,
                    IngredientId = detail.IngredientId,
                    PreparedItemId = detail.PreparedItemId,
                    Ingredient = ingredient,
                    PreparedItem = preparedItem,
                    AvailableQty = 0,
                    ReservedQty = 0,
                    LastUpdated = DateTime.UtcNow
                };

                var validation = await _inventoryIssuePolicy.EvaluateAsync(
                    new InventoryIssueRequest(
                        InventoryIssueOperation.TransferDispatch,
                        dto.FromStoreId,
                        detail.IngredientId,
                        detail.PreparedItemId,
                        inventory.AvailableQty,
                        detail.BaseQuantity,
                        inventory.MaxNegativeQty,
                        null,
                        null,
                        null,
                        null));

                if (!validation.IsAllowed || validation.IsNegative)
                {
                    warnings.Add(
                        new InventoryStockWarningDTO
                        {
                            StoreId = dto.FromStoreId,
                            IngredientId = detail.IngredientId ?? 0,
                            PreparedItemId = detail.PreparedItemId,
                            ItemType = detail.IngredientId.HasValue ? "INGREDIENT" : "PREPARED_ITEM",
                            IngredientName = ingredient?.Name ?? preparedItem!.Name,
                            AvailableQuantity = validation.BeforeQty,
                            ReservedQuantity = inventory.ReservedQty,
                            UsableQuantity = validation.BeforeQty,
                            ThresholdQuantity = validation.EffectiveMaxNegativeQty,
                            UnitCode = ingredient?.BaseUnit?.UnitCode
                                ?? preparedItem?.BaseUnit?.UnitCode
                                ?? string.Empty,
                            Message = validation.ReasonCode
                        });
                }

                var fifo = await _costLayerConsumptionService.PlanConsumeAsync(
                    dto.FromStoreId,
                    detail.IngredientId,
                    detail.PreparedItemId,
                    detail.BaseQuantity,
                    requireFullCoverage: true);
                if (!fifo.IsSuccess || fifo.Data == null || !fifo.Data.IsFullyCovered)
                {
                    warnings.Add(new InventoryStockWarningDTO
                    {
                        StoreId = dto.FromStoreId,
                        IngredientId = detail.IngredientId ?? 0,
                        PreparedItemId = detail.PreparedItemId,
                        ItemType = detail.IngredientId.HasValue ? "INGREDIENT" : "PREPARED_ITEM",
                        IngredientName = ingredient?.Name ?? preparedItem!.Name,
                        AvailableQuantity = inventory.AvailableQty,
                        ReservedQuantity = inventory.ReservedQty,
                        UsableQuantity = inventory.AvailableQty,
                        ThresholdQuantity = detail.BaseQuantity,
                        UnitCode = ingredient?.BaseUnit?.UnitCode
                            ?? preparedItem?.BaseUnit?.UnitCode
                            ?? string.Empty,
                        Message = fifo.ErrorCode ?? "FIFO_FULL_COVERAGE_REQUIRED"
                    });
                }
            }

            return warnings;
        }

        private async Task<(List<InventoryStockWarningDTO> Warnings, List<int> InventoryIds)> ProcessDispatchAsync(
            InventoryTransfer transfer,
            int actorStaffId)
        {
            var warnings = new List<InventoryStockWarningDTO>();
            var inventoryIds = new List<int>();
            var actorAccountId = await _repository.GetAccountIdForStaffAsync(actorStaffId)
                ?? throw new InvalidOperationException("Không xác định được tài khoản người xác nhận.");

            foreach (var detail in transfer.Details
                .OrderBy(x => x.IngredientId.HasValue ? 0 : 1)
                .ThenBy(x => x.IngredientId ?? x.PreparedItemId))
            {
                if (detail.BaseQuantity <= 0)
                    throw new InvalidOperationException("Số lượng quy đổi base phải lớn hơn 0.");

                var ingredient = detail.IngredientId.HasValue
                    ? detail.Ingredient ?? await _repository.GetIngredientAsync(detail.IngredientId.Value)
                    : null;
                var preparedItem = detail.PreparedItemId.HasValue
                    ? detail.PreparedItem ?? await _repository.GetPreparedItemAsync(detail.PreparedItemId.Value)
                    : null;
                if (ingredient == null && preparedItem == null)
                    throw new InvalidOperationException("Dòng chuyển thiếu identity hợp lệ.");

                var itemName = ingredient?.Name ?? preparedItem!.Name;
                var baseUnitCode = ingredient?.BaseUnit?.UnitCode
                    ?? preparedItem?.BaseUnit?.UnitCode
                    ?? string.Empty;
                var sourceInventory = ingredient != null
                    ? await _repository.GetOrCreateStoreInventoryForUpdateAsync(
                        transfer.FromStoreId,
                        ingredient.IngredientId)
                    : await _repository.GetOrCreatePreparedItemInventoryForUpdateAsync(
                        transfer.FromStoreId,
                        preparedItem!.PreparedItemId,
                        actorAccountId,
                        $"INVENTORY_TRANSFER_SOURCE:{transfer.InventoryTransferId}");
                var stockValidation = await _inventoryIssuePolicy.EvaluateAsync(
                    new InventoryIssueRequest(
                        InventoryIssueOperation.TransferDispatch,
                        transfer.FromStoreId,
                        detail.IngredientId,
                        detail.PreparedItemId,
                        sourceInventory.AvailableQty,
                        detail.BaseQuantity,
                        sourceInventory.MaxNegativeQty,
                        null,
                        null,
                        null,
                        null));

                if (!stockValidation.IsAllowed)
                {
                    throw new InvalidOperationException(stockValidation.ReasonCode);
                }

                if (stockValidation.IsNegative)
                {
                    warnings.Add(
                        new InventoryStockWarningDTO
                        {
                            StoreId = transfer.FromStoreId,
                            IngredientId = detail.IngredientId ?? 0,
                            PreparedItemId = detail.PreparedItemId,
                            ItemType = detail.IngredientId.HasValue ? "INGREDIENT" : "PREPARED_ITEM",
                            IngredientName = itemName,
                            AvailableQuantity = stockValidation.BeforeQty,
                            ReservedQuantity = sourceInventory.ReservedQty,
                            UsableQuantity = stockValidation.BeforeQty,
                            ThresholdQuantity = stockValidation.EffectiveMaxNegativeQty,
                            UnitCode = baseUnitCode,
                            Message = stockValidation.ReasonCode
                        });
                }

                var sourceBefore = sourceInventory.AvailableQty;
                var planResult = await _costLayerConsumptionService.PlanConsumeAsync(
                    transfer.FromStoreId,
                    detail.IngredientId,
                    detail.PreparedItemId,
                    detail.BaseQuantity,
                    requireFullCoverage: true);
                if (!planResult.IsSuccess || planResult.Data == null)
                    throw new InvalidOperationException(planResult.ErrorCode ?? "FIFO_FULL_COVERAGE_REQUIRED");
                var cost = planResult.Data;
                _costLayerConsumptionService.ApplyPlan(cost);

                await _repository.AddTransferCostAllocationsAsync(cost.Slices.Select(slice =>
                    new InventoryTransferCostAllocation
                    {
                        InventoryTransferDetailId = detail.InventoryTransferDetailId,
                        SourceInventoryCostLayerId = slice.InventoryCostLayerId,
                        Quantity = slice.Quantity,
                        ReceivedQuantity = 0,
                        UnitCost = slice.UnitCost,
                        TotalCost = slice.TotalCost,
                        CreatedAt = DateTime.UtcNow
                    }));

                sourceInventory.AvailableQty -= detail.BaseQuantity;

                detail.SourceBeforeQty = sourceBefore;
                detail.SourceAfterQty = sourceInventory.AvailableQty;
                detail.DispatchedBaseQuantity = detail.BaseQuantity;

                _repository.UpdateStoreInventory(sourceInventory);
                inventoryIds.Add(sourceInventory.StoreInventoryId);

                await _repository.AddInventoryTransactionAsync(
                    BuildTransferTransaction(
                        transfer.InventoryTransferId,
                        detail.InventoryTransferDetailId,
                        sourceInventory,
                        InventoryTransactionTypeEnum.OUT_TRANSFER,
                        InventoryStockStatus.NORMAL,
                        detail.BaseQuantity,
                        sourceBefore,
                        sourceInventory.AvailableQty,
                        cost.WeightedUnitCost,
                        cost.TotalCost));
            }

            return (warnings, inventoryIds);
        }

        private Task LockTransferInventoriesAsync(InventoryTransfer transfer) =>
            _repository.LockInventoriesAsync(
                transfer.Details.SelectMany(detail =>
                    new (int StoreId, int? IngredientId, int? PreparedItemId)[]
                    {
                        (transfer.FromStoreId, detail.IngredientId, detail.PreparedItemId),
                        (transfer.ToStoreId, detail.IngredientId, detail.PreparedItemId)
                    }));

        private async Task SettleDestinationGapAsync(
            StoreInventory inventory,
            decimal beforeQty,
            InventoryCostLayer inboundLayer)
        {
            var deficit = Math.Abs(Math.Min(beforeQty, 0));
            var settledQuantity = Math.Min(inboundLayer.Quantity, deficit);
            inboundLayer.RemainingQuantity = inboundLayer.Quantity - settledQuantity;
            if (settledQuantity <= 0)
                return;
            if (inboundLayer.UnitCost <= 0)
                throw new InvalidOperationException("INBOUND_NEGATIVE_SETTLEMENT_COST_REQUIRED");

            var gaps = await _repository.GetOpenCostGapsForUpdateAsync(inventory.StoreInventoryId);
            if (gaps.Sum(x => x.OutstandingQuantity) != deficit)
                throw new InvalidOperationException("NEGATIVE_COST_GAP_COVERAGE_MISMATCH");

            var remaining = settledQuantity;
            var settlements = new List<InventoryCostGapSettlement>();
            foreach (var gap in gaps.OrderBy(x => x.OccurredAt).ThenBy(x => x.InventoryNegativeCostGapId))
            {
                if (remaining <= 0)
                    break;
                var quantity = Math.Min(remaining, gap.OutstandingQuantity);
                if (quantity <= 0)
                    continue;
                gap.OutstandingQuantity -= quantity;
                gap.Status = gap.OutstandingQuantity == 0
                    ? InventoryNegativeCostGapStatuses.Settled
                    : InventoryNegativeCostGapStatuses.PartiallySettled;
                settlements.Add(new InventoryCostGapSettlement
                {
                    InventoryNegativeCostGap = gap,
                    InboundInventoryCostLayer = inboundLayer,
                    Quantity = quantity,
                    UnitCost = inboundLayer.UnitCost,
                    TotalCost = quantity * inboundLayer.UnitCost,
                    CreatedAt = DateTime.UtcNow
                });
                remaining -= quantity;
            }

            if (remaining != 0)
                throw new InvalidOperationException("NEGATIVE_COST_GAP_SETTLEMENT_INCOMPLETE");
            await _repository.AddCostGapSettlementsAsync(settlements);
        }

        private static InventoryTransaction BuildTransferTransaction(
            int transferId,
            int transferDetailId,
            StoreInventory inventory,
            InventoryTransactionTypeEnum type,
            InventoryStockStatus stockStatus,
            decimal quantity,
            decimal beforeQty,
            decimal afterQty,
            decimal unitCost,
            decimal totalCost)
        {
            return new InventoryTransaction
            {
                StoreInventoryId = inventory.StoreInventoryId,
                InventoryTransferId = transferId,
                InventoryTransferDetailId = transferDetailId,
                Type = type,
                StockStatus = stockStatus,
                Quantity = quantity,
                BeforeQty = beforeQty,
                AfterQty = afterQty,
                UnitCost = unitCost,
                TotalCost = totalCost,
                CreatedAt = DateTime.UtcNow
            };
        }

        private async Task ValidateAndNormalizeAsync(InventoryTransferMutationDTO dto)
        {
            if (dto.FromStoreId <= 0 || dto.ToStoreId <= 0)
            {
                throw new InvalidOperationException("Vui lòng chọn kho đi và kho đến.");
            }

            if (dto.FromStoreId == dto.ToStoreId)
            {
                throw new InvalidOperationException("Kho đi và kho đến phải khác nhau.");
            }

            await EnsureTransferScopeAsync(dto.FromStoreId, dto.ToStoreId);

            var stores = await _repository.GetStoresByIdsAsync([dto.FromStoreId, dto.ToStoreId]);

            if (stores.Select(x => x.StoreId).Distinct().Count() != 2)
            {
                throw new InvalidOperationException("Kho đi hoặc kho đến không tồn tại.");
            }

            if (!Enum.IsDefined(typeof(InventoryTransferPurpose), dto.Purpose)
                || dto.Purpose == 0)
            {
                dto.Purpose = InventoryTransferPurpose.REPLENISHMENT;
            }

            if (dto.Details == null || !dto.Details.Any())
            {
                throw new InvalidOperationException("Phiếu chuyển kho phải có ít nhất một nguyên liệu.");
            }

            var duplicatedIdentity = dto.Details
                .Where(x => x.IngredientId.HasValue || x.PreparedItemId.HasValue)
                .GroupBy(x => x.IngredientId.HasValue
                    ? $"I:{x.IngredientId.Value}"
                    : $"P:{x.PreparedItemId!.Value}")
                .FirstOrDefault(x => x.Count() > 1);

            if (duplicatedIdentity != null)
            {
                throw new InvalidOperationException("Mỗi mặt hàng chỉ được xuất hiện một lần trong một phiếu.");
            }

            foreach (var detail in dto.Details)
            {
                if (detail.IngredientId.HasValue == detail.PreparedItemId.HasValue)
                {
                    throw new InvalidOperationException("Dòng chuyển phải có đúng một identity Nguyên liệu/Bán thành phẩm.");
                }

                if (detail.UnitId <= 0)
                {
                    throw new InvalidOperationException("Vui lòng chọn đơn vị tính.");
                }

                if (detail.Quantity <= 0)
                {
                    throw new InvalidOperationException("Số lượng chuyển phải lớn hơn 0.");
                }

                if (detail.IngredientId.HasValue)
                {
                    var ingredient = await _repository.GetIngredientAsync(detail.IngredientId.Value)
                        ?? throw new InvalidOperationException("Nguyên liệu không tồn tại.");
                    if (!ingredient.Active)
                        throw new InvalidOperationException($"Nguyên liệu {ingredient.Name} đã ngừng hoạt động.");

                    var conversionFactor = CalculateConversionFactorToBase(
                        ingredient,
                        detail.UnitId,
                        throwIfMissing: true) ?? 0;
                    detail.BaseQuantity = Math.Round(
                        detail.Quantity * conversionFactor,
                        3,
                        MidpointRounding.AwayFromZero);
                }
                else
                {
                    var preparedItem = await _repository.GetPreparedItemAsync(detail.PreparedItemId!.Value)
                        ?? throw new InvalidOperationException("Bán thành phẩm không tồn tại.");
                    if (!preparedItem.Active)
                        throw new InvalidOperationException($"Bán thành phẩm {preparedItem.Name} đã ngừng hoạt động.");
                    if (detail.UnitId != preparedItem.BaseUnitId)
                        throw new InvalidOperationException(
                            $"Bán thành phẩm {preparedItem.Name} chỉ chuyển theo đơn vị cơ sở.");
                    detail.BaseQuantity = Math.Round(
                        detail.Quantity,
                        3,
                        MidpointRounding.AwayFromZero);
                }

                if (detail.BaseQuantity <= 0)
                {
                    throw new InvalidOperationException("Số lượng quy đổi base phải lớn hơn 0.");
                }

                if (detail.RestockRequestId.HasValue)
                {
                    var actor = _actorAccessor.Get(_httpContextAccessor.HttpContext?.User!);
                    var allocation = await _restockAllocationService.ValidateAllocationAsync(
                        new RestockAllocationValidationRequest
                        {
                            RestockRequestId = detail.RestockRequestId.Value,
                            DestinationStoreId = dto.ToStoreId,
                            IngredientId = detail.IngredientId,
                            PreparedItemId = detail.PreparedItemId,
                            AllocationQuantity = detail.BaseQuantity,
                            ExcludeInventoryTransferId = dto.TransferId,
                            AllowOverallocationOverride = dto.AllowRestockOverallocation,
                            OverrideReason = dto.RestockOverallocationReason,
                            ActorStaffId = actor.StaffId,
                            ActorRoles = actor.RoleNames,
                            RequestKey = dto.RequestKey
                        });
                    if (!allocation.IsSuccess)
                        throw new InvalidOperationException(allocation.Message);
                }
            }
        }

        private static List<InventoryTransferDetail> BuildTransferDetails(
            InventoryTransferMutationDTO dto)
        {
            return dto.Details
                .Select(x => new InventoryTransferDetail
                {
                    IngredientId = x.IngredientId,
                    PreparedItemId = x.PreparedItemId,
                    RestockRequestId = x.RestockRequestId,
                    RestockRequestFulfillmentId = x.RestockRequestFulfillmentId,
                    UnitId = x.UnitId,
                    Quantity = x.Quantity,
                    BaseQuantity = x.BaseQuantity,
                    UnitPrice = null,
                    Note = NormalizeNote(x.Note)
                })
                .ToList();
        }

        private async Task<InventoryTransferMutationResultDTO> ResolveDuplicateResultAsync(
            RequestDeduplicationBeginResult dedup)
        {
            if (!string.IsNullOrWhiteSpace(dedup.ErrorCode))
            {
                throw BuildDeduplicationException(dedup);
            }

            if (dedup.Status == "SUCCESS" && dedup.ReferenceId.HasValue)
            {
                var transfer = await _repository.GetTransferByIdAsync(dedup.ReferenceId.Value);

                if (transfer != null)
                {
                    return BuildResult(transfer);
                }
            }

            throw BuildDeduplicationException(dedup);
        }

        private async Task MarkFailedIfPossibleAsync(
            RequestDeduplicationBeginResult? dedup,
            string message)
        {
            if (dedup?.Entry == null)
            {
                return;
            }

            try
            {
                await _deduplicationService.MarkFailedAsync(
                    dedup.Entry,
                    new { success = false, message });
            }
            catch
            {
                // Best effort only. The business transaction is still rolled back below.
            }
        }

        private static InventoryTransferMutationResultDTO BuildResult(
            InventoryTransfer transfer,
            List<InventoryStockWarningDTO>? warnings = null)
        {
            return new InventoryTransferMutationResultDTO
            {
                InventoryTransferId = transfer.InventoryTransferId,
                Code = transfer.Code,
                Status = transfer.Status,
                RowVersion = Convert.ToBase64String(transfer.RowVersion),
                Warnings = warnings ?? []
            };
        }

        private static InventoryTransferDiscrepancyPosting NewPosting(
            InventoryTransferDetail detail,
            InventoryTransferCostAllocation allocation,
            InventoryTransferDiscrepancyPostingType postingType,
            decimal quantity,
            string requestKey,
            string reason,
            int actorStaffId,
            long? relatedPostingId = null) => new()
        {
            InventoryTransferDetailId = detail.InventoryTransferDetailId,
            InventoryTransferCostAllocationId = allocation.InventoryTransferCostAllocationId,
            RelatedPostingId = relatedPostingId,
            PostingType = postingType,
            Quantity = quantity,
            UnitCost = allocation.UnitCost,
            TotalCost = quantity * allocation.UnitCost,
            RequestKey = requestKey.Trim(),
            Reason = reason.Trim(),
            ActorStaffId = actorStaffId,
            CreatedAt = DateTime.UtcNow
        };

        private static void CompleteTransferWhenResolved(
            InventoryTransfer transfer,
            IEnumerable<InventoryTransferDiscrepancyPosting> postings)
        {
            var rows = postings.ToList();
            if (transfer.Details.All(x =>
                InventoryTransferQuantityAuthority.Calculate(x, rows).InTransitOpen <= 0))
                transfer.Status = InventoryTransferStatus.COMPLETED;
        }

        private static void ValidateResolutionDto(InventoryTransferResolutionDTO dto)
        {
            if (string.IsNullOrWhiteSpace(dto.RequestKey))
                throw new InvalidOperationException("RequestKey là bắt buộc.");
            if (string.IsNullOrWhiteSpace(dto.Reason))
                throw new InvalidOperationException("Lý do xử lý chênh lệch là bắt buộc.");
            if (dto.Lines.Count == 0)
                throw new InvalidOperationException("Phải chọn ít nhất một dòng chênh lệch.");
        }

        private static void ValidateResolutionLines(
            IEnumerable<InventoryTransferResolutionLineDTO> lines,
            IReadOnlyDictionary<int, InventoryTransferDetail> detailById)
        {
            var rows = lines.ToList();
            if (rows.Any(x => x.InventoryTransferDetailId <= 0 || x.BaseQuantity <= 0))
                throw new InvalidOperationException("Số lượng xử lý phải lớn hơn 0.");
            if (rows.GroupBy(x => x.InventoryTransferDetailId).Any(x => x.Count() > 1))
                throw new InvalidOperationException("Dòng xử lý chênh lệch bị trùng.");
            if (rows.Any(x => !detailById.ContainsKey(x.InventoryTransferDetailId)))
                throw new InvalidOperationException("Dòng xử lý không thuộc phiếu chuyển kho.");
        }

        private static void EnsureDispatchedTransfer(InventoryTransfer transfer)
        {
            if (transfer.Status != InventoryTransferStatus.DISPATCHED)
                throw new InvalidOperationException("Chỉ phiếu DISPATCHED mới được xử lý chênh lệch.");
        }

        private static bool HasRole(
            CafeChain.Application.DTOs.Admin.Actor.AdminActorContext actor,
            params string[] roles) =>
            actor.RoleNames.Contains(RoleConstants.SystemAdmin, StringComparer.OrdinalIgnoreCase)
            || actor.RoleNames.Any(role => roles.Contains(role, StringComparer.OrdinalIgnoreCase));

        private static bool HasOperationalRole(
            CafeChain.Application.DTOs.Admin.Actor.AdminActorContext actor) =>
            HasRole(actor,
                RoleConstants.BusinessOwner,
                RoleConstants.AccountantWarehouse,
                RoleConstants.StoreManager,
                RoleConstants.ShiftSupervisor);

        private void EnsureOperationalRole()
        {
            if (!HasOperationalRole(GetActor()))
                throw new UnauthorizedAccessException("INVENTORY_TRANSFER_ROLE_DENIED");
        }

        private void EnsureOwnerRole()
        {
            if (!HasRole(GetActor(), RoleConstants.BusinessOwner))
                throw new UnauthorizedAccessException("INVENTORY_TRANSFER_OWNER_REQUIRED");
        }

        private void EnsureCoordinatorRole()
        {
            if (!HasRole(GetActor(), RoleConstants.BusinessOwner, RoleConstants.AccountantWarehouse))
                throw new UnauthorizedAccessException("INVENTORY_TRANSFER_COORDINATOR_REQUIRED");
        }

        private void EnsureTransferRowVersion(InventoryTransfer transfer, string? suppliedBase64)
        {
            if (string.IsNullOrWhiteSpace(suppliedBase64))
                throw new InvalidOperationException(BranchReceiptErrorCodes.ValidationRowVersionRequired);

            byte[] supplied;
            try
            {
                supplied = Convert.FromBase64String(suppliedBase64);
            }
            catch (FormatException)
            {
                throw new InvalidOperationException(BranchReceiptErrorCodes.ValidationRowVersionRequired);
            }

            if (supplied.Length == 0 || !transfer.RowVersion.SequenceEqual(supplied))
                throw new InvalidOperationException(BranchReceiptErrorCodes.ResourceChanged);

        }

        private async Task<decimal> EstimateBaseUnitCostAsync(int storeId, int ingredientId)
        {
            var layers = await _repository.GetAvailableCostLayersAsync(storeId, ingredientId);
            var quantity = layers.Sum(x => x.RemainingQuantity);

            if (quantity <= 0)
            {
                return 0;
            }

            return layers.Sum(x => x.RemainingQuantity * x.UnitCost) / quantity;
        }

        private async Task<decimal> EstimatePreparedItemBaseUnitCostAsync(
            int storeId,
            int preparedItemId)
        {
            var layers = await _repository.GetAvailablePreparedItemCostLayersAsync(
                storeId,
                preparedItemId);
            var quantity = layers.Sum(x => x.RemainingQuantity);
            return quantity <= 0
                ? 0
                : layers.Sum(x => x.RemainingQuantity * x.UnitCost) / quantity;
        }

        private static List<InventoryUnitOptionDTO> BuildUnitOptions(Ingredient ingredient)
        {
            var options = new List<InventoryUnitOptionDTO>();

            if (ingredient.BaseUnit != null)
            {
                options.Add(
                    new InventoryUnitOptionDTO
                    {
                        UnitId = ingredient.BaseUnitId,
                        UnitName = ingredient.BaseUnit.Name,
                        UnitCode = ingredient.BaseUnit.UnitCode,
                        ConversionFactorToBase = 1,
                        IsBaseUnit = true
                    });
            }

            foreach (var conversion in ingredient.UnitConversions
                .Where(x =>
                    x.Active
                    && x.ToUnitId == ingredient.BaseUnitId
                    && x.FromQuantity > 0
                    && x.ToQuantity > 0))
            {
                if (options.Any(x => x.UnitId == conversion.FromUnitId))
                {
                    continue;
                }

                options.Add(
                    new InventoryUnitOptionDTO
                    {
                        UnitId = conversion.FromUnitId,
                        UnitName = conversion.FromUnit?.Name
                            ?? conversion.FromUnitId.ToString(CultureInfo.InvariantCulture),
                        UnitCode = conversion.FromUnit?.UnitCode
                            ?? conversion.FromUnitId.ToString(CultureInfo.InvariantCulture),
                        ConversionFactorToBase = conversion.ToQuantity / conversion.FromQuantity,
                        IsBaseUnit = false
                    });
            }

            return options;
        }

        private static decimal? CalculateConversionFactorToBase(
            Ingredient ingredient,
            int unitId,
            bool throwIfMissing)
        {
            if (unitId == ingredient.BaseUnitId)
            {
                return 1;
            }

            var conversion = ingredient.UnitConversions
                .FirstOrDefault(x =>
                    x.Active
                    && x.FromUnitId == unitId
                    && x.ToUnitId == ingredient.BaseUnitId
                    && x.FromQuantity > 0);

            if (conversion == null)
            {
                if (throwIfMissing)
                {
                    throw new InvalidOperationException(
                        $"Chưa cấu hình quy đổi đơn vị cho nguyên liệu {ingredient.Name}.");
                }

                return null;
            }

            return conversion.ToQuantity / conversion.FromQuantity;
        }

        private static InvalidOperationException BuildDeduplicationException(
            RequestDeduplicationBeginResult dedup)
        {
            var message = dedup.ErrorMessage ?? "RequestKey đã được xử lý.";
            return new InvalidOperationException(string.IsNullOrWhiteSpace(dedup.ErrorCode)
                ? message
                : $"{dedup.ErrorCode}: {message}");
        }

        private async Task<IReadOnlyCollection<int>> GetAllowedStoreIdsAsync()
        {
            var actor = GetActor();
            if (actor.StaffId <= 0)
                return Array.Empty<int>();

            return (await _scopeAuthorization.GetAllowedStoresAsync(actor.StaffId))
                .Select(x => x.StoreId)
                .Distinct()
                .ToArray();
        }

        private async Task<bool> CanAccessStoreAsync(int storeId)
        {
            var actor = GetActor();
            return actor.StaffId > 0
                && await _scopeAuthorization.CanAccessStoreAsync(actor.StaffId, storeId);
        }

        private async Task<bool> CanAccessTransferAsync(int fromStoreId, int toStoreId)
        {
            return await CanAccessStoreAsync(fromStoreId)
                && await CanAccessStoreAsync(toStoreId);
        }

        private async Task<bool> CanReadTransferAsync(int fromStoreId, int toStoreId)
        {
            return await CanAccessStoreAsync(fromStoreId)
                || await CanAccessStoreAsync(toStoreId);
        }

        private async Task EnsureStoreScopeAsync(int storeId)
        {
            if (!await CanAccessStoreAsync(storeId))
                throw new UnauthorizedAccessException("INVENTORY_TRANSFER_SCOPE_DENIED");
        }

        private async Task EnsureTransferScopeAsync(int fromStoreId, int toStoreId)
        {
            if (!await CanAccessTransferAsync(fromStoreId, toStoreId))
                throw new UnauthorizedAccessException("INVENTORY_TRANSFER_SCOPE_DENIED");
        }

        private CafeChain.Application.DTOs.Admin.Actor.AdminActorContext GetActor()
        {
            var principal = _httpContextAccessor.HttpContext?.User
                ?? new System.Security.Claims.ClaimsPrincipal();
            return _actorAccessor.Get(principal);
        }

        private int GetCurrentStaffId()
        {
            if (_userContext.StaffId <= 0)
            {
                throw new InvalidOperationException("Không xác định được nhân viên hiện tại.");
            }

            return _userContext.StaffId;
        }

        private static string? NormalizeNote(string? note)
        {
            return string.IsNullOrWhiteSpace(note)
                ? null
                : note.Trim();
        }
    }
}
