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

            return new AdminInventoryTransferDetailVM
            {
                InventoryTransferId = transfer.InventoryTransferId,
                Code = transfer.Code,
                RequestKey = transfer.RequestKey,
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
                Details = transfer.Details
                    .OrderBy(x => x.InventoryTransferDetailId)
                    .Select(x => new AdminInventoryTransferDetailItemVM
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
                        UnitPrice = x.UnitPrice,
                        SourceBeforeQty = x.SourceBeforeQty,
                        SourceAfterQty = x.SourceAfterQty,
                        DestinationBeforeQty = x.DestinationBeforeQty,
                        DestinationAfterQty = x.DestinationAfterQty,
                        Note = x.Note
                    })
                    .ToList()
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
                if (dto.Lines.Any(x => x.InventoryTransferDetailId <= 0 || x.ReceivedBaseQuantity <= 0))
                    throw new InvalidOperationException("Số lượng nhận phải lớn hơn 0.");
                if (dto.Lines.GroupBy(x => x.InventoryTransferDetailId).Any(x => x.Count() > 1))
                    throw new InvalidOperationException("Dòng nhận chuyển kho bị trùng.");

                var detailById = transfer.Details.ToDictionary(x => x.InventoryTransferDetailId);
                foreach (var line in dto.Lines)
                {
                    if (!detailById.TryGetValue(line.InventoryTransferDetailId, out var detail))
                        throw new InvalidOperationException("Dòng nhận không thuộc phiếu chuyển kho.");
                    if (detail.ReceivedBaseQuantity + line.ReceivedBaseQuantity > detail.DispatchedBaseQuantity)
                        throw new InvalidOperationException("Số lượng nhận vượt số lượng đã dispatch.");
                }

                await LockTransferInventoriesAsync(transfer);
                var allocations = await _repository.GetTransferCostAllocationsAsync(dto.Lines.Select(x => x.InventoryTransferDetailId));
                var actorAccountId = await _repository.GetAccountIdForStaffAsync(staffId)
                    ?? throw new InvalidOperationException("Không xác định được tài khoản người nhận.");
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
                    var inventory = ingredient != null
                        ? await _repository.GetOrCreateStoreInventoryForUpdateAsync(transfer.ToStoreId, ingredient.IngredientId)
                        : await _repository.GetOrCreatePreparedItemInventoryForUpdateAsync(
                            transfer.ToStoreId,
                            preparedItem!.PreparedItemId,
                            actorAccountId,
                            $"INVENTORY_TRANSFER_RECEIPT:{transfer.InventoryTransferId}");
                    var remainingToReceive = receiveLine.ReceivedBaseQuantity;
                    var detailAllocations = allocations
                        .Where(x => x.InventoryTransferDetailId == detail.InventoryTransferDetailId)
                        .OrderBy(x => x.InventoryTransferCostAllocationId)
                        .ToList();

                    foreach (var allocation in detailAllocations)
                    {
                        if (remainingToReceive <= 0)
                            break;
                        var inTransit = allocation.Quantity - allocation.ReceivedQuantity;
                        var quantity = Math.Min(remainingToReceive, inTransit);
                        if (quantity <= 0)
                            continue;

                        var before = inventory.AvailableQty;
                        inventory.AvailableQty += quantity;
                        allocation.ReceivedQuantity += quantity;
                        remainingToReceive -= quantity;

                        var receiptLine = new BranchReceiptLine
                        {
                            RestockRequestId = detail.RestockRequestId,
                            SourceInventoryTransferDetailId = detail.InventoryTransferDetailId,
                            SourceTransferCostAllocationId = allocation.InventoryTransferCostAllocationId,
                            IngredientId = detail.IngredientId,
                            PreparedItemId = detail.PreparedItemId,
                            InputQuantity = quantity,
                            InputUnitId = ingredient?.BaseUnitId ?? preparedItem!.BaseUnitId,
                            ReceivedBaseQuantity = quantity,
                            BaseUnitId = ingredient?.BaseUnitId ?? preparedItem!.BaseUnitId,
                            BaseUnitCostSnapshot = allocation.UnitCost,
                            LineTotalCost = quantity * allocation.UnitCost,
                            CreatedAt = DateTime.UtcNow
                        };
                        receipt.Lines.Add(receiptLine);

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
                            Quantity = quantity,
                            BeforeQty = before,
                            AfterQty = inventory.AvailableQty,
                            UnitCost = allocation.UnitCost,
                            TotalCost = quantity * allocation.UnitCost,
                            CreatedAt = DateTime.UtcNow
                        });
                        var inboundLayer = new InventoryCostLayer
                        {
                            StoreId = transfer.ToStoreId,
                            IngredientId = detail.IngredientId,
                            PreparedItemId = detail.PreparedItemId,
                            Quantity = quantity,
                            RemainingQuantity = quantity,
                            UnitCost = allocation.UnitCost,
                            SourceBranchReceiptLine = receiptLine,
                            SourceTransferCostAllocation = allocation,
                            CreatedAt = DateTime.UtcNow
                        };
                        await SettleDestinationGapAsync(inventory, before, inboundLayer);
                        await _repository.AddCostLayerAsync(inboundLayer);
                    }

                    if (remainingToReceive > 0)
                        throw new InvalidOperationException("Thiếu cost allocation in-transit cho số lượng nhận.");

                    detail.ReceivedBaseQuantity += receiveLine.ReceivedBaseQuantity;
                    detail.DestinationAfterQty = inventory.AvailableQty;
                    _repository.UpdateStoreInventory(inventory);

                    if (detail.RestockRequestId.HasValue)
                    {
                        var posting = await _fulfillmentPostingService.RegisterAsync(
                            new RegisterRestockFulfillmentPostingCommand
                            {
                                RestockRequestId = detail.RestockRequestId.Value,
                                DestinationStoreId = transfer.ToStoreId,
                                SourceDocumentType = RestockFulfillmentDocumentTypes.InventoryTransfer,
                                SourceDocumentId = transfer.InventoryTransferId,
                                SourceDocumentLineId = detail.InventoryTransferDetailId,
                                IngredientId = detail.IngredientId,
                                PreparedItemId = detail.PreparedItemId,
                                Quantity = receiveLine.ReceivedBaseQuantity,
                                BaseUnitId = ingredient?.BaseUnitId ?? preparedItem!.BaseUnitId,
                                ActorStaffId = staffId,
                                Reason = $"InventoryTransfer #{transfer.InventoryTransferId} RECEIVED"
                            });
                        if (!posting.IsSuccess)
                            throw new InvalidOperationException(posting.Message);
                    }
                }

                await _repository.AddBranchReceiptAsync(receipt);
                if (transfer.Details.All(x => x.ReceivedBaseQuantity == x.DispatchedBaseQuantity))
                    transfer.Status = InventoryTransferStatus.COMPLETED;
                _repository.UpdateTransfer(transfer);
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
