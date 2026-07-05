using CafeChain.Application.DTOs.Admin.InventoryDocuments.Create;
using CafeChain.Application.DTOs.Admin.InventoryTransfers;
using CafeChain.Application.DTOs.Systems;
using CafeChain.Application.Interfaces.Admin.InventoryDocuments;
using CafeChain.Application.Interfaces.Admin.InventoryTransfers;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Systems;
using CafeChain.Infrastrusture.Interfaces.Admin.InventoryTransfers;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Costing;
using CafeChain.Models.Inventories.Ingredients;
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
        private const string ConfirmAction = "InventoryTransfer.Confirm";
        private const string CancelAction = "InventoryTransfer.Cancel";

        private readonly IAdminInventoryTransferRepository _repository;
        private readonly IRequestDeduplicationService _deduplicationService;
        private readonly INegativeInventoryService _negativeInventoryService;
        private readonly IUserContext _userContext;

        public AdminInventoryTransferService(
            IAdminInventoryTransferRepository repository,
            IRequestDeduplicationService deduplicationService,
            INegativeInventoryService negativeInventoryService,
            IUserContext userContext)
        {
            _repository = repository;
            _deduplicationService = deduplicationService;
            _negativeInventoryService = negativeInventoryService;
            _userContext = userContext;
        }

        public async Task<AdminInventoryTransferCreateVM> GetCreateDataAsync()
        {
            return new AdminInventoryTransferCreateVM
            {
                DocumentDate = DateTime.Today,
                Stores = await _repository.GetStoreDropdownAsync()
            };
        }

        public async Task<List<SupplierIngredientDTO>> GetTransferIngredientsAsync(int fromStoreId)
        {
            if (fromStoreId <= 0)
            {
                return [];
            }

            var ingredients = await _repository.GetActiveIngredientsAsync();
            var inventories = await _repository.GetStoreInventoriesAsync(fromStoreId);
            var inventoryByIngredient = inventories
                .Where(x => x.IngredientId.HasValue)
                .GroupBy(x => x.IngredientId!.Value)
                .ToDictionary(x => x.Key, x => x.First());

            var result = new List<SupplierIngredientDTO>();

            foreach (var ingredient in ingredients)
            {
                inventoryByIngredient.TryGetValue(ingredient.IngredientId, out var inventory);

                var unitOptions = BuildUnitOptions(ingredient);
                var defaultUnit = unitOptions.FirstOrDefault(x => x.IsBaseUnit)
                    ?? unitOptions.FirstOrDefault();
                var baseUnitCost = await EstimateBaseUnitCostAsync(fromStoreId, ingredient.IngredientId);
                var conversionFactor = defaultUnit?.ConversionFactorToBase ?? 0;

                result.Add(
                    new SupplierIngredientDTO
                    {
                        IngredientId = ingredient.IngredientId,
                        IngredientName = ingredient.Name,
                        UnitId = defaultUnit?.UnitId ?? ingredient.BaseUnitId,
                        UnitName = defaultUnit?.UnitName ?? ingredient.BaseUnit?.Name ?? string.Empty,
                        UnitCode = defaultUnit?.UnitCode ?? ingredient.BaseUnit?.UnitCode ?? string.Empty,
                        CurrentPrice = conversionFactor > 0 ? baseUnitCost * conversionFactor : 0,
                        BaseUnitId = ingredient.BaseUnitId,
                        BaseUnitName = ingredient.BaseUnit?.Name ?? string.Empty,
                        BaseUnitCode = ingredient.BaseUnit?.UnitCode ?? string.Empty,
                        ConversionFactorToBase = conversionFactor,
                        CanConvertToBase = conversionFactor > 0,
                        AvailableBaseQuantity = inventory?.AvailableQty ?? 0,
                        SuggestedBaseUnitCost = baseUnitCost,
                        SuggestedUnitPrice = conversionFactor > 0 ? baseUnitCost * conversionFactor : 0,
                        PriceSource = baseUnitCost > 0 ? "FIFO" : "No cost layer",
                        IsQuantityLocked = false,
                        IsPriceLocked = false,
                        UnitOptions = unitOptions
                    });
            }

            return result;
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

                var transfer = await _repository.GetTransferByIdAsync(id)
                    ?? throw new InvalidOperationException("Transfer not found.");

                if (transfer.Status != InventoryTransferStatus.DRAFT)
                {
                    throw new InvalidOperationException("Only draft transfer can be updated.");
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

        public async Task<InventoryTransferMutationResultDTO> ConfirmAsync(int id, string? requestKey)
        {
            RequestDeduplicationBeginResult? dedup = null;

            await _repository.BeginTransactionAsync();

            try
            {
                var staffId = GetCurrentStaffId();
                dedup = await _deduplicationService.BeginAsync(
                    requestKey,
                    ConfirmAction,
                    staffId,
                    new { id, requestKey },
                    id);

                if (!dedup.CanProcess)
                {
                    await _repository.RollbackTransactionAsync();
                    return await ResolveDuplicateResultAsync(dedup);
                }

                var transfer = await _repository.GetTransferByIdAsync(id)
                    ?? throw new InvalidOperationException("Transfer not found.");

                if (transfer.Status == InventoryTransferStatus.COMPLETED)
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
                    throw new InvalidOperationException("Cancelled transfer cannot be confirmed.");
                }

                if (!transfer.Details.Any())
                {
                    throw new InvalidOperationException("Transfer must have at least one detail.");
                }

                await ProcessConfirmAsync(transfer);

                transfer.Status = InventoryTransferStatus.COMPLETED;
                transfer.ConfirmedAt = DateTime.UtcNow;
                transfer.ConfirmedByStaffId = staffId;

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

                    throw new InvalidOperationException(dedup.ErrorMessage);
                }

                var transfer = await _repository.GetTransferByIdAsync(id);

                if (transfer == null)
                {
                    return false;
                }

                if (transfer.Status == InventoryTransferStatus.COMPLETED)
                {
                    throw new InvalidOperationException("Completed transfer cannot be cancelled.");
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
            var inventoryByIngredient = inventories
                .Where(x => x.IngredientId.HasValue)
                .GroupBy(x => x.IngredientId!.Value)
                .ToDictionary(x => x.Key, x => x.First());

            var warnings = new List<InventoryStockWarningDTO>();

            foreach (var detail in dto.Details)
            {
                var ingredient = await _repository.GetIngredientAsync(detail.IngredientId)
                    ?? throw new InvalidOperationException("Ingredient not found.");

                inventoryByIngredient.TryGetValue(detail.IngredientId, out var inventory);
                inventory ??= new StoreInventory
                {
                    StoreId = dto.FromStoreId,
                    IngredientId = detail.IngredientId,
                    Ingredient = ingredient,
                    AvailableQty = 0,
                    ReservedQty = 0,
                    LastUpdated = DateTime.UtcNow
                };

                var validation = await _negativeInventoryService.ValidateIssueAsync(
                    inventory,
                    detail.BaseQuantity,
                    ingredient.Name);

                if (!validation.IsAllowed || validation.IsNegative)
                {
                    warnings.Add(
                        new InventoryStockWarningDTO
                        {
                            StoreId = dto.FromStoreId,
                            IngredientId = detail.IngredientId,
                            IngredientName = ingredient.Name,
                            AvailableQuantity = validation.BeforeQty,
                            ReservedQuantity = inventory.ReservedQty,
                            UsableQuantity = validation.BeforeQty - inventory.ReservedQty,
                            ThresholdQuantity = validation.ThresholdQuantity,
                            UnitCode = ingredient.BaseUnit?.UnitCode ?? string.Empty,
                            Message = validation.Message
                        });
                }
            }

            return warnings;
        }

        private async Task ProcessConfirmAsync(InventoryTransfer transfer)
        {
            foreach (var detail in transfer.Details.OrderBy(x => x.IngredientId))
            {
                var ingredient = detail.Ingredient
                    ?? await _repository.GetIngredientAsync(detail.IngredientId)
                    ?? throw new InvalidOperationException("Ingredient not found.");

                if (detail.BaseQuantity <= 0)
                {
                    throw new InvalidOperationException("Base quantity must be greater than 0.");
                }

                var sourceInventory = await GetOrCreateInventoryForUpdateAsync(
                    transfer.FromStoreId,
                    detail.IngredientId,
                    ingredient);
                var destinationInventory = await GetOrCreateInventoryForUpdateAsync(
                    transfer.ToStoreId,
                    detail.IngredientId,
                    ingredient);

                var stockValidation = await _negativeInventoryService.ValidateIssueAsync(
                    sourceInventory,
                    detail.BaseQuantity,
                    ingredient.Name);

                if (!stockValidation.IsAllowed)
                {
                    throw new InvalidOperationException(stockValidation.Message);
                }

                var sourceBefore = sourceInventory.AvailableQty;
                var destinationBefore = destinationInventory.AvailableQty;
                var cost = await ConsumeSourceCostAsync(
                    transfer.FromStoreId,
                    detail.IngredientId,
                    detail.BaseQuantity,
                    detail);

                sourceInventory.AvailableQty -= detail.BaseQuantity;
                destinationInventory.AvailableQty += detail.BaseQuantity;

                detail.SourceBeforeQty = sourceBefore;
                detail.SourceAfterQty = sourceInventory.AvailableQty;
                detail.DestinationBeforeQty = destinationBefore;
                detail.DestinationAfterQty = destinationInventory.AvailableQty;

                _repository.UpdateStoreInventory(sourceInventory);
                _repository.UpdateStoreInventory(destinationInventory);

                await _repository.AddInventoryTransactionAsync(
                    BuildTransferTransaction(
                        transfer.InventoryTransferId,
                        sourceInventory,
                        InventoryTransactionTypeEnum.OUT_TRANSFER,
                        stockValidation.StockStatus,
                        detail.BaseQuantity,
                        sourceBefore,
                        sourceInventory.AvailableQty,
                        cost.UnitCost,
                        cost.TotalCost));

                await _repository.AddInventoryTransactionAsync(
                    BuildTransferTransaction(
                        transfer.InventoryTransferId,
                        destinationInventory,
                        InventoryTransactionTypeEnum.IN_TRANSFER,
                        InventoryStockStatus.NORMAL,
                        detail.BaseQuantity,
                        destinationBefore,
                        destinationInventory.AvailableQty,
                        cost.UnitCost,
                        cost.TotalCost));

                await _repository.AddCostLayerAsync(
                    new InventoryCostLayer
                    {
                        StoreId = transfer.ToStoreId,
                        IngredientId = detail.IngredientId,
                        Quantity = detail.BaseQuantity,
                        RemainingQuantity = detail.BaseQuantity,
                        UnitCost = cost.UnitCost,
                        CreatedAt = DateTime.UtcNow
                    });
            }
        }

        private async Task<StoreInventory> GetOrCreateInventoryForUpdateAsync(
            int storeId,
            int ingredientId,
            Ingredient ingredient)
        {
            var inventory = await _repository.GetStoreInventoryForUpdateAsync(storeId, ingredientId);

            if (inventory != null)
            {
                return inventory;
            }

            inventory = new StoreInventory
            {
                StoreId = storeId,
                IngredientId = ingredientId,
                Ingredient = ingredient,
                AvailableQty = 0,
                ReservedQty = 0,
                LastUpdated = DateTime.UtcNow
            };

            await _repository.AddStoreInventoryAsync(inventory);

            return inventory;
        }

        private static InventoryTransaction BuildTransferTransaction(
            int transferId,
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
                StoreInventory = inventory,
                InventoryTransferId = transferId,
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

        private async Task<(decimal UnitCost, decimal TotalCost)> ConsumeSourceCostAsync(
            int storeId,
            int ingredientId,
            decimal baseQuantity,
            InventoryTransferDetail detail)
        {
            var requiredQty = baseQuantity;
            var totalCost = 0m;
            decimal? lastCost = null;
            var layers = await _repository.GetAvailableCostLayersAsync(storeId, ingredientId);

            foreach (var layer in layers)
            {
                if (requiredQty <= 0)
                {
                    break;
                }

                var consumeQty = Math.Min(requiredQty, layer.RemainingQuantity);

                layer.RemainingQuantity -= consumeQty;
                _repository.UpdateCostLayer(layer);

                totalCost += consumeQty * layer.UnitCost;
                lastCost = layer.UnitCost;
                requiredQty -= consumeQty;
            }

            if (requiredQty > 0)
            {
                var fallbackCost = lastCost ?? CalculateFallbackBaseUnitCost(detail);
                totalCost += requiredQty * fallbackCost;
            }

            var unitCost = baseQuantity > 0
                ? totalCost / baseQuantity
                : 0;

            return (unitCost, totalCost);
        }

        private async Task ValidateAndNormalizeAsync(InventoryTransferMutationDTO dto)
        {
            if (dto.FromStoreId <= 0 || dto.ToStoreId <= 0)
            {
                throw new InvalidOperationException("Source and destination stores are required.");
            }

            if (dto.FromStoreId == dto.ToStoreId)
            {
                throw new InvalidOperationException("Source store and destination store must be different.");
            }

            var stores = await _repository.GetStoresByIdsAsync([dto.FromStoreId, dto.ToStoreId]);

            if (stores.Select(x => x.StoreId).Distinct().Count() != 2)
            {
                throw new InvalidOperationException("Source or destination store does not exist.");
            }

            if (!Enum.IsDefined(typeof(InventoryTransferPurpose), dto.Purpose)
                || dto.Purpose == 0)
            {
                dto.Purpose = InventoryTransferPurpose.REPLENISHMENT;
            }

            if (dto.Details == null || !dto.Details.Any())
            {
                throw new InvalidOperationException("Transfer must have at least one ingredient.");
            }

            var duplicatedIngredient = dto.Details
                .Where(x => x.IngredientId > 0)
                .GroupBy(x => x.IngredientId)
                .FirstOrDefault(x => x.Count() > 1);

            if (duplicatedIngredient != null)
            {
                throw new InvalidOperationException("Each ingredient can appear only once in one transfer.");
            }

            foreach (var detail in dto.Details)
            {
                if (detail.IngredientId <= 0)
                {
                    throw new InvalidOperationException("Ingredient is required.");
                }

                if (detail.UnitId <= 0)
                {
                    throw new InvalidOperationException("Unit is required.");
                }

                if (detail.Quantity <= 0)
                {
                    throw new InvalidOperationException("Quantity must be greater than 0.");
                }

                if (detail.UnitPrice.HasValue && detail.UnitPrice.Value < 0)
                {
                    throw new InvalidOperationException("Unit price cannot be negative.");
                }

                var ingredient = await _repository.GetIngredientAsync(detail.IngredientId)
                    ?? throw new InvalidOperationException("Ingredient does not exist.");

                if (!ingredient.Active)
                {
                    throw new InvalidOperationException($"Ingredient {ingredient.Name} is inactive.");
                }

                var conversionFactor = CalculateConversionFactorToBase(
                    ingredient,
                    detail.UnitId,
                    throwIfMissing: true) ?? 0;

                detail.BaseQuantity = Math.Round(
                    detail.Quantity * conversionFactor,
                    3,
                    MidpointRounding.AwayFromZero);

                if (detail.BaseQuantity <= 0)
                {
                    throw new InvalidOperationException("Base quantity must be greater than 0.");
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
                    UnitId = x.UnitId,
                    Quantity = x.Quantity,
                    BaseQuantity = x.BaseQuantity,
                    UnitPrice = x.UnitPrice,
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

            throw new InvalidOperationException(dedup.ErrorMessage ?? "RequestKey already exists.");
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

        private static InventoryTransferMutationResultDTO BuildResult(InventoryTransfer transfer)
        {
            return new InventoryTransferMutationResultDTO
            {
                InventoryTransferId = transfer.InventoryTransferId,
                Code = transfer.Code,
                Status = transfer.Status
            };
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

        private static List<InventoryIngredientUnitOptionDTO> BuildUnitOptions(Ingredient ingredient)
        {
            var options = new List<InventoryIngredientUnitOptionDTO>();

            if (ingredient.BaseUnit != null)
            {
                options.Add(
                    new InventoryIngredientUnitOptionDTO
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
                    new InventoryIngredientUnitOptionDTO
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
                        $"Missing unit conversion for ingredient {ingredient.Name}.");
                }

                return null;
            }

            return conversion.ToQuantity / conversion.FromQuantity;
        }

        private static decimal CalculateFallbackBaseUnitCost(InventoryTransferDetail detail)
        {
            if (!detail.UnitPrice.HasValue || detail.UnitPrice.Value <= 0)
            {
                return 0;
            }

            if (detail.Quantity <= 0 || detail.BaseQuantity <= 0)
            {
                return detail.UnitPrice.Value;
            }

            var conversionFactor = detail.BaseQuantity / detail.Quantity;

            return conversionFactor > 0
                ? detail.UnitPrice.Value / conversionFactor
                : detail.UnitPrice.Value;
        }

        private int GetCurrentStaffId()
        {
            if (_userContext.StaffId <= 0)
            {
                throw new InvalidOperationException("Current staff is required.");
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
