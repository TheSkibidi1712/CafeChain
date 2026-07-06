using CafeChain.Application.Interfaces.Admin.InventoryDocuments;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Application.DTOs.Admin.InventoryDocuments.Create;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Costing;
using CafeChain.Models.Inventories.Debts;
using CafeChain.Models.Inventories.Documents;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Inventories.Transactions;
using CafeChain.Models.Stores;
using CafeChain.Infrastrusture.Interfaces.Admin.InventoryDocuments;

namespace CafeChain.Application.Services.Admin.InventoryDocuments
{
    public class AdminInventoryDocumentProcessService : IAdminInventoryDocumentProcessService
    {
        private readonly IAdminInventoryDocumentRepository _repository;

        private readonly INegativeInventoryService _negativeInventoryService;

        public AdminInventoryDocumentProcessService(IAdminInventoryDocumentRepository repository, INegativeInventoryService negativeInventoryService)
        {
            _repository = repository;
            _negativeInventoryService = negativeInventoryService;
        }

        public async Task<InventoryProcessResultDTO> ExecuteProcessAsync(InventoryDocument document)
        {
            var result = new InventoryProcessResultDTO();

            switch (document.Type)
            {
                case InventoryDocumentType.IMPORT:
                case InventoryDocumentType.ADJUSTMENT_IN:
                    await ProcessImportAsync(document, result);
                    break;

                case InventoryDocumentType.EXPORT:
                    await ProcessExportAsync(document, result);
                    break;

                case InventoryDocumentType.WASTE:
                    await ProcessWasteAsync(document, result);
                    break;

                case InventoryDocumentType.STOCK_TAKE:
                    await ProcessStockTakeAsync(document, result);
                    break;

                case InventoryDocumentType.PRODUCTION_IN:
                    await ProcessProductionInAsync(document, result);
                    break;

                case InventoryDocumentType.PRODUCTION_OUT:
                    await ProcessProductionOutAsync(document, result);
                    break;

                case InventoryDocumentType.SALES_DEDUCTION:
                    await ProcessSalesDeductionAsync(document, result);
                    break;

                default: throw new InvalidOperationException($"Không hỗ trợ loại chứng từ {document.Type}");
            }

            return result;
        }

        // =====================================================
        // IMPORT
        // =====================================================

        private async Task ProcessImportAsync(InventoryDocument document, InventoryProcessResultDTO result)
        {
            foreach (var detail in document.Details)
            {
                var lineTotal = detail.TotalAmount ?? detail.Quantity * (detail.UnitPrice ?? 0);

                var baseUnitCost = detail.BaseQuantity > 0 ? lineTotal / detail.BaseQuantity : 0;

                detail.CostPrice = baseUnitCost;
                detail.CostAmount = lineTotal;
                detail.TotalAmount = lineTotal;
                _repository.UpdateDocumentDetail(detail);

                var existingInventory = await _repository.GetStoreInventoryAsync(document.StoreId, detail.IngredientId);

                StoreInventory inventory;

                var isNewInventory = false;

                if (existingInventory == null)
                {
                    isNewInventory = true;

                    inventory =
                        new StoreInventory
                        {
                            StoreId = document.StoreId,

                            IngredientId = detail.IngredientId,

                            AvailableQty = detail.BaseQuantity,

                            ReservedQty = 0,

                            LastUpdated = DateTime.UtcNow
                        };

                    await _repository.AddStoreInventoryAsync(inventory);
                }
                else
                {
                    inventory = existingInventory;

                    inventory.AvailableQty += detail.BaseQuantity;

                    _repository.UpdateStoreInventory(inventory);
                }

                await UpsertStoreInventorySnapshotAsync(document.StoreId, detail.IngredientId, inventory.AvailableQty, baseUnitCost);

                await _repository
                    .AddCostLayerAsync(
                        new InventoryCostLayer
                        {
                            StoreId = document.StoreId,

                            IngredientId = detail.IngredientId,

                            Quantity = detail.BaseQuantity,

                            RemainingQuantity = detail.BaseQuantity,

                            UnitCost = baseUnitCost,

                            CreatedAt = DateTime.UtcNow
                        });

                var transaction =
                    new InventoryTransaction
                    {
                        InventoryDocumentId = document.InventoryDocumentId,

                        Type = document.Purpose == InventoryDocumentPurpose.IMPORT_ADJUSTMENT
                            ? InventoryTransactionTypeEnum.ADJUSTMENT_IN
                            : InventoryTransactionTypeEnum.IMPORT,

                        StockStatus = InventoryStockStatus.NORMAL,

                        Quantity = detail.BaseQuantity,

                        BeforeQty = inventory.AvailableQty - detail.BaseQuantity,

                        AfterQty = inventory.AvailableQty,

                        UnitCost = baseUnitCost,

                        TotalCost = lineTotal,

                        CreatedAt = DateTime.UtcNow
                    };

                if (isNewInventory)
                {
                    transaction.StoreInventory = inventory;
                }
                else
                {
                    transaction.StoreInventoryId = inventory.StoreInventoryId;
                }

                await _repository.AddInventoryTransactionAsync(transaction);

                AddLowStockWarning(result, document, detail, inventory);
            }

            if (document.Purpose == InventoryDocumentPurpose.IMPORT_PURCHASE)
            {
                await _repository.AddDebtAsync(
                    new InventoryDebt
                    {
                        InventoryDocumentId = document.InventoryDocumentId,

                        InventoryDocument = document,

                        PartnerType = InventoryPartnerType.SUPPLIER,

                        PartnerId = document.SupplierId,

                        PartnerName = document.Supplier?.Name ?? document.PartnerName ?? string.Empty,

                        Amount = document.FinalAmount ?? 0,

                        PaidAmount = 0,

                        CreatedAt = DateTime.UtcNow
                    });
            }
        }

        private async Task UpsertStoreInventorySnapshotAsync(int storeId, int ingredientId, decimal quantity, decimal avgCost)
        {
            var snapshot = await _repository.GetStoreInventorySnapshotAsync(storeId, ingredientId);

            if (snapshot == null)
            {
                await _repository.AddStoreInventorySnapshotAsync(
                    new StoreInventorySnapshot
                    {
                        StoreId = storeId,

                        IngredientId = ingredientId,

                        Quantity = quantity,

                        AvgCost = avgCost,

                        UpdatedAt = DateTime.UtcNow
                    });

                return;
            }

            snapshot.Quantity = quantity;
            snapshot.AvgCost = avgCost;
            snapshot.UpdatedAt = DateTime.UtcNow;

            _repository.UpdateStoreInventorySnapshot(snapshot);
        }

        // =====================================================
        // EXPORT
        // =====================================================

        private async Task ProcessExportAsync(InventoryDocument document, InventoryProcessResultDTO result)
        {
            foreach (var detail in document.Details)
            {
                var inventory = await GetOrCreateInventoryAsync(document.StoreId, detail.IngredientId);

                var stockValidation = await _negativeInventoryService.ValidateIssueAsync(inventory, detail.BaseQuantity, detail.Ingredient.Name);

                if (!stockValidation.IsAllowed)
                {
                    throw new InvalidOperationException(stockValidation.Message);
                }

                var fifo = await AllocateFifoAsync(detail, document.StoreId);

                detail.CostPrice = fifo.CostPrice;
                detail.CostAmount = fifo.CostAmount;

                _repository.UpdateDocumentDetail(detail);

                var beforeQty = inventory.AvailableQty;

                inventory.AvailableQty -= detail.BaseQuantity;

                _repository.UpdateStoreInventory(inventory);

                AddLowStockWarning(result, document, detail, inventory);

                await _repository.AddInventoryTransactionAsync(
                    new InventoryTransaction
                    {
                        StoreInventoryId = inventory.StoreInventoryId,

                        InventoryDocumentId = document.InventoryDocumentId,

                        Type = document.Purpose == InventoryDocumentPurpose.ADJUSTMENT_OUT
                            ? InventoryTransactionTypeEnum.ADJUSTMENT_OUT
                            : InventoryTransactionTypeEnum.EXPORT,

                        StockStatus = stockValidation.StockStatus,

                        Quantity = detail.BaseQuantity,

                        BeforeQty = beforeQty,

                        AfterQty = inventory.AvailableQty,

                        UnitCost = fifo.CostPrice,

                        TotalCost = fifo.CostAmount,

                        CreatedAt = DateTime.UtcNow
                    });
            }

            if (document.Purpose == InventoryDocumentPurpose.DEBT)
            {
                await _repository.AddDebtAsync(
                    new InventoryDebt
                    {
                        InventoryDocumentId = document.InventoryDocumentId,

                        PartnerType = document.PartnerType,

                        PartnerId = document.PartnerId,

                        PartnerName = document.PartnerName ?? "",

                        Amount = document.FinalAmount ?? 0,

                        PaidAmount = 0,

                        CreatedAt = DateTime.UtcNow
                    });
            }
        }

        // =====================================================
        // WASTE
        // =====================================================

        private async Task ProcessWasteAsync(InventoryDocument document, InventoryProcessResultDTO result)
        {
            foreach (var detail in document.Details)
            {
                var inventory = await GetOrCreateInventoryAsync(document.StoreId, detail.IngredientId);

                var stockValidation = await _negativeInventoryService.ValidateIssueAsync(inventory, detail.BaseQuantity, detail.Ingredient.Name);

                if (!stockValidation.IsAllowed)
                {
                    throw new InvalidOperationException(stockValidation.Message);
                }

                var fifo = await AllocateFifoAsync(detail, document.StoreId);

                detail.CostPrice = fifo.CostPrice;
                detail.CostAmount = fifo.CostAmount;

                _repository.UpdateDocumentDetail(detail);

                var beforeQty = inventory.AvailableQty;

                inventory.AvailableQty -= detail.BaseQuantity;

                _repository.UpdateStoreInventory(inventory);

                AddLowStockWarning(result, document, detail, inventory);

                await _repository.AddInventoryTransactionAsync(
                    new InventoryTransaction
                    {
                        StoreInventoryId = inventory.StoreInventoryId,

                        InventoryDocumentId = document.InventoryDocumentId,

                        Type = InventoryTransactionTypeEnum.WASTE,

                        StockStatus = stockValidation.StockStatus,

                        Quantity = detail.BaseQuantity,

                        BeforeQty = beforeQty,

                        AfterQty = inventory.AvailableQty,

                        UnitCost = fifo.CostPrice,

                        TotalCost = fifo.CostAmount,

                        CreatedAt = DateTime.UtcNow
                    });
            }
        }

        // =====================================================
        // SALES DEDUCTION
        // =====================================================
        private async Task ProcessSalesDeductionAsync(InventoryDocument document, InventoryProcessResultDTO result)
        {
            foreach (var detail in document.Details)
            {
                var inventory = await GetOrCreateInventoryAsync(document.StoreId, detail.IngredientId);
                
                var stockValidation = await _negativeInventoryService.ValidateIssueAsync( inventory, detail.BaseQuantity, detail.Ingredient.Name);

                if (!stockValidation.IsAllowed)
                {
                    throw new InvalidOperationException(stockValidation.Message);
                }

                var fifo = await AllocateFifoAsync(detail, document.StoreId);

                detail.CostPrice = fifo.CostPrice;
                detail.CostAmount = fifo.CostAmount;

                _repository.UpdateDocumentDetail(detail);

                var beforeQty = inventory.AvailableQty;

                inventory.AvailableQty -= detail.BaseQuantity;

                _repository.UpdateStoreInventory(inventory);

                AddLowStockWarning(result, document, detail, inventory);

                await _repository.AddInventoryTransactionAsync(
                    new InventoryTransaction
                    {
                        StoreInventoryId = inventory.StoreInventoryId,

                        InventoryDocumentId = document.InventoryDocumentId,

                        Type = InventoryTransactionTypeEnum.SALES_DEDUCTION,

                        StockStatus = stockValidation.StockStatus,

                        Quantity = detail.BaseQuantity,

                        BeforeQty = beforeQty,

                        AfterQty = inventory.AvailableQty,

                        UnitCost = fifo.CostPrice,

                        TotalCost = fifo.CostAmount,

                        CreatedAt = DateTime.UtcNow
                    });
            }
        }

        // ====================================================
        // PRODUCTION OUT
        // ====================================================
        private async Task ProcessProductionOutAsync(InventoryDocument document, InventoryProcessResultDTO result)
        {
            foreach (var detail in document.Details)
            {
                var inventory = await GetOrCreateInventoryAsync(document.StoreId, detail.IngredientId);
                
                var stockValidation = await _negativeInventoryService.ValidateIssueAsync(inventory, detail.BaseQuantity, detail.Ingredient.Name);

                if (!stockValidation.IsAllowed)
                {
                    throw new InvalidOperationException(stockValidation.Message);
                }

                var fifo = await AllocateFifoAsync(detail, document.StoreId);

                detail.CostPrice = fifo.CostPrice;
                detail.CostAmount = fifo.CostAmount;

                _repository.UpdateDocumentDetail(detail);

                var beforeQty = inventory.AvailableQty;

                inventory.AvailableQty -= detail.BaseQuantity;

                _repository.UpdateStoreInventory(inventory);

                AddLowStockWarning(result, document, detail, inventory);

                await _repository.AddInventoryTransactionAsync(
                    new InventoryTransaction
                    {
                        StoreInventoryId = inventory.StoreInventoryId,

                        InventoryDocumentId = document.InventoryDocumentId,

                        Type = InventoryTransactionTypeEnum.PRODUCTION_OUT,

                        StockStatus = stockValidation.StockStatus,

                        Quantity = detail.BaseQuantity,

                        BeforeQty = beforeQty,

                        AfterQty = inventory.AvailableQty,

                        UnitCost = fifo.CostPrice,

                        TotalCost = fifo.CostAmount,

                        CreatedAt = DateTime.UtcNow
                    });
            }
        }

        // =====================================================
        // PRODUCTION IN
        // =====================================================
        private async Task ProcessProductionInAsync(InventoryDocument document, InventoryProcessResultDTO result)
        {
            foreach (var detail in document.Details)
            {
                var inventory = await GetOrCreateInventoryAsync(document.StoreId, detail.IngredientId);

                var beforeQty = inventory.AvailableQty;

                inventory.AvailableQty += detail.BaseQuantity;

                _repository.UpdateStoreInventory(inventory);

                AddLowStockWarning(result, document, detail, inventory);

                await _repository.AddCostLayerAsync(
                    new InventoryCostLayer
                    {
                        StoreId = document.StoreId,

                        IngredientId = detail.IngredientId,

                        Quantity = detail.BaseQuantity,

                        RemainingQuantity = detail.BaseQuantity,

                        UnitCost = detail.CostPrice ?? 0,

                        CreatedAt = DateTime.UtcNow
                    });

                await _repository.AddInventoryTransactionAsync(
                    new InventoryTransaction
                    {
                        StoreInventoryId = inventory.StoreInventoryId,

                        InventoryDocumentId = document.InventoryDocumentId,

                        Type = InventoryTransactionTypeEnum.PRODUCTION_IN,

                        StockStatus = InventoryStockStatus.NORMAL,

                        Quantity = detail.BaseQuantity,

                        BeforeQty = beforeQty,

                        AfterQty = inventory.AvailableQty,

                        UnitCost = detail.CostPrice,

                        TotalCost = detail.CostAmount,

                        CreatedAt = DateTime.UtcNow
                    });
            }
        }


        // =====================================================
        // STOCK TAKE
        // =====================================================
        private async Task ProcessStockTakeAsync(InventoryDocument document, InventoryProcessResultDTO result)
        {
            foreach (var detail in document.Details)
            {
                var inventory = await GetOrCreateInventoryAsync(document.StoreId, detail.IngredientId);

                var systemQty = inventory.AvailableQty;

                var actualQty = detail.BaseQuantity;

                var variance = actualQty - systemQty;

                if (variance == 0)
                {
                    continue;
                }

                inventory.AvailableQty += variance;

                _repository.UpdateStoreInventory(inventory);

                AddLowStockWarning(result, document, detail, inventory);

                await _repository
                    .AddInventoryTransactionAsync(
                        new InventoryTransaction
                        {
                            StoreInventoryId = inventory.StoreInventoryId,

                            InventoryDocumentId = document.InventoryDocumentId,

                            Type = InventoryTransactionTypeEnum.STOCK_TAKE,

                            StockStatus = InventoryStockStatus.NORMAL,

                            Quantity = Math.Abs(variance),

                            BeforeQty = systemQty,

                            AfterQty = inventory.AvailableQty,

                            CreatedAt = DateTime.UtcNow
                        });
            }
        }

        // =====================================================
        // INVENTORY
        // =====================================================
        private async Task<StoreInventory> GetOrCreateInventoryAsync(int storeId, int ingredientId)
        {
            var inventory = await _repository.GetStoreInventoryAsync(storeId, ingredientId);

            if (inventory != null)
            {
                return inventory;
            }

            inventory =
                new StoreInventory
                {
                    StoreId = storeId,

                    IngredientId = ingredientId,

                    AvailableQty = 0,

                    ReservedQty = 0,

                    LastUpdated = DateTime.UtcNow
                };

            await _repository.AddStoreInventoryAsync(inventory);

            return inventory;
        }

        private static void AddLowStockWarning(InventoryProcessResultDTO result, InventoryDocument document, InventoryDocumentDetail detail, StoreInventory inventory)
        {
            var threshold = GetDefaultLowStockThreshold(detail);

            if (threshold <= 0)
            {
                return;
            }

            var usableQuantity = inventory.AvailableQty - inventory.ReservedQty;

            if (usableQuantity > threshold)
            {
                return;
            }

            if (result.Warnings.Any(x => x.StoreId == document.StoreId && x.IngredientId == detail.IngredientId))
            {
                return;
            }

            var unitCode = detail.Ingredient?.BaseUnit?.UnitCode ?? string.Empty;

            var ingredientName = detail.Ingredient?.Name ?? $"#{detail.IngredientId}";

            result.Warnings.Add(
                new InventoryStockWarningDTO
                {
                    StoreId = document.StoreId,
                    StoreName = document.Store?.Name ?? string.Empty,
                    IngredientId = detail.IngredientId,
                    IngredientName = ingredientName,
                    AvailableQuantity = inventory.AvailableQty,
                    ReservedQuantity = inventory.ReservedQty,
                    UsableQuantity = usableQuantity,
                    ThresholdQuantity = threshold,
                    UnitCode = unitCode,
                    Message =
                        $"{ingredientName} sắp hết: còn {FormatQuantity(usableQuantity)} {unitCode}, ngưỡng {FormatQuantity(threshold)} {unitCode}."
                });
        }

        private static decimal GetDefaultLowStockThreshold(InventoryDocumentDetail detail)
        {
            return detail.Ingredient?.BaseUnit?.Type switch
            {
                UnitType.KhoiLuong => 1000,
                UnitType.TheTich => 1000,
                UnitType.Dem => 5,
                _ => 0
            };
        }

        private static string FormatQuantity(decimal quantity)
        {
            return quantity.ToString("#,0.###");
        }

        // =====================================================
        // FIFO
        // =====================================================

        private async Task<(decimal CostPrice, decimal CostAmount)> AllocateFifoAsync(InventoryDocumentDetail detail, int storeId)
        {
            if (detail.BaseQuantity <= 0)
            {
                return (0, 0);
            }

            decimal requiredQty = detail.BaseQuantity;

            decimal totalCost = 0;

            var allocations = new List<InventoryCostAllocation>();

            var layers = await _repository.GetAvailableCostLayersAsync(storeId, detail.IngredientId);

            foreach (var layer in layers)
            {
                if (requiredQty <= 0)
                {
                    break;
                }

                var consumeQty = Math.Min(requiredQty, layer.RemainingQuantity);

                if (consumeQty <= 0)
                {
                    continue;
                }

                allocations.Add(
                    new InventoryCostAllocation
                    {
                        InventoryDocumentDetailId = detail.InventoryDocumentDetailId,

                        InventoryCostLayerId = layer.InventoryCostLayerId,

                        Quantity = consumeQty,

                        UnitCost = layer.UnitCost
                    });

                layer.RemainingQuantity -= consumeQty;

                _repository.UpdateCostLayer(layer);

                totalCost += consumeQty * layer.UnitCost;

                requiredQty -= consumeQty;
            }

            if (requiredQty > 0)
            {
                var fallbackCost = await ResolveFallbackIssueCostAsync(detail, storeId, allocations);

                totalCost += requiredQty * fallbackCost;
            }

            if (allocations.Any())
            {
                await _repository.AddCostAllocationsAsync(allocations);
            }

            var avgCost = totalCost / detail.BaseQuantity;

            return (avgCost, totalCost);
        }

        private async Task<decimal> ResolveFallbackIssueCostAsync(InventoryDocumentDetail detail, int storeId, IReadOnlyCollection<InventoryCostAllocation> allocations)
        {
            var lastAllocatedCost = allocations
                    .Where(x => x.UnitCost > 0)
                    .Select(x => (decimal?)x.UnitCost)
                    .LastOrDefault();

            if (lastAllocatedCost.HasValue)
            {
                return lastAllocatedCost.Value;
            }

            var latestLayer = await _repository.GetLatestCostLayerAsync(storeId, detail.IngredientId);

            if (latestLayer?.UnitCost > 0)
            {
                return latestLayer.UnitCost;
            }

            if (detail.CostPrice.HasValue && detail.CostPrice.Value > 0)
            {
                return detail.CostPrice.Value;
            }

            if (detail.UnitPrice.HasValue
                && detail.UnitPrice.Value > 0
                && detail.Quantity > 0
                && detail.BaseQuantity > 0)
            {
                return detail.UnitPrice.Value * detail.Quantity / detail.BaseQuantity;
            }

            return 0;
        }

    }
}
