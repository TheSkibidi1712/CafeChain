using CafeChain.Application.Interfaces.Admin.InventoryDocuments;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Application.DTOs.Admin.InventoryDocuments.Create;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Costing;
using CafeChain.Models.Inventories.Documents;
using CafeChain.Models.Inventories.Transactions;
using CafeChain.Models.Stores;
using CafeChain.Infrastrusture.Interfaces.Admin.InventoryDocuments;

namespace CafeChain.Application.Services.Admin.InventoryDocuments
{
    public class AdminInventoryDocumentProcessService : IAdminInventoryDocumentProcessService
    {
        private readonly IAdminInventoryDocumentRepository _repository;

        private readonly IInventoryIssuePolicy _inventoryIssuePolicy;
        private readonly IInventoryCostLayerConsumptionService _costLayerConsumptionService;

        public AdminInventoryDocumentProcessService(
            IAdminInventoryDocumentRepository repository,
            IInventoryIssuePolicy inventoryIssuePolicy,
            IInventoryCostLayerConsumptionService costLayerConsumptionService)
        {
            _repository = repository;
            _inventoryIssuePolicy = inventoryIssuePolicy;
            _costLayerConsumptionService = costLayerConsumptionService;
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
                ValidateProcessDetail(detail);

                var lineTotal = detail.TotalAmount ?? detail.Quantity * (detail.UnitPrice ?? 0);

                var baseUnitCost = detail.BaseQuantity > 0 ? lineTotal / detail.BaseQuantity : 0;

                detail.CostPrice = baseUnitCost;
                detail.CostAmount = lineTotal;
                detail.TotalAmount = lineTotal;
                _repository.UpdateDocumentDetail(detail);

                var inventory =
                    await _repository.GetOrCreateStoreInventoryForIngredientAsync(
                        document.StoreId,
                        detail.IngredientId);

                var beforeQty = inventory.AvailableQty;

                inventory.AvailableQty += detail.BaseQuantity;

                _repository.UpdateStoreInventory(inventory);

                await AddInboundLayerAndSettleAsync(
                    document,
                    detail,
                    inventory,
                    beforeQty,
                    detail.BaseQuantity,
                    baseUnitCost);

                var transaction =
                    new InventoryTransaction
                    {
                        InventoryDocumentId = document.InventoryDocumentId,

                        InventoryDocumentDetailId = detail.InventoryDocumentDetailId,

                        Type = document.Purpose == InventoryDocumentPurpose.IMPORT_ADJUSTMENT
                            ? InventoryTransactionTypeEnum.ADJUSTMENT_IN
                            : InventoryTransactionTypeEnum.IMPORT,

                        StockStatus = InventoryStockStatus.NORMAL,

                        Quantity = detail.BaseQuantity,

                        BeforeQty = beforeQty,

                        AfterQty = inventory.AvailableQty,

                        UnitCost = baseUnitCost,

                        TotalCost = lineTotal,

                        CreatedAt = DateTime.UtcNow
                    };

                transaction.StoreInventoryId = inventory.StoreInventoryId;

                await _repository.AddInventoryTransactionAsync(transaction);

                AddLowStockWarning(result, document, detail, inventory);
            }

        }

        // =====================================================
        // EXPORT
        // =====================================================

        private async Task ProcessExportAsync(InventoryDocument document, InventoryProcessResultDTO result)
        {
            foreach (var detail in document.Details)
            {
                ValidateProcessDetail(detail);

                var inventory = await GetExistingInventoryForUpdateAsync(document.StoreId, detail.IngredientId);

                var operation = document.Purpose == InventoryDocumentPurpose.ADJUSTMENT_OUT
                    ? InventoryIssueOperation.AdjustmentOut
                    : InventoryIssueOperation.ManualExternalExport;
                var stockValidation = await EvaluateIssueAsync(document, detail, inventory, operation);

                if (!stockValidation.IsAllowed)
                {
                    throw new InvalidOperationException(stockValidation.ReasonCode);
                }

                var fifo = await AllocateFifoAsync(
                    detail,
                    document.StoreId,
                    requireFullCoverage: !stockValidation.IsNegative);

                detail.CostPrice = fifo.IsFullyCovered ? fifo.CostPrice : null;
                detail.CostAmount = fifo.IsFullyCovered ? fifo.CostAmount : null;

                _repository.UpdateDocumentDetail(detail);

                var beforeQty = inventory.AvailableQty;

                inventory.AvailableQty -= detail.BaseQuantity;

                _repository.UpdateStoreInventory(inventory);

                AddLowStockWarning(result, document, detail, inventory);

                var transaction = new InventoryTransaction
                    {
                        StoreInventoryId = inventory.StoreInventoryId,

                        InventoryDocumentId = document.InventoryDocumentId,

                        InventoryDocumentDetailId = detail.InventoryDocumentDetailId,

                        Type = document.Purpose == InventoryDocumentPurpose.ADJUSTMENT_OUT
                            ? InventoryTransactionTypeEnum.ADJUSTMENT_OUT
                            : InventoryTransactionTypeEnum.EXPORT,

                        StockStatus = ResolveStockStatus(stockValidation),

                        Quantity = detail.BaseQuantity,

                        BeforeQty = beforeQty,

                        AfterQty = inventory.AvailableQty,

                        UnitCost = fifo.IsFullyCovered ? fifo.CostPrice : null,

                        TotalCost = fifo.IsFullyCovered ? fifo.CostAmount : null,

                        CreatedAt = DateTime.UtcNow
                    };
                await _repository.AddInventoryTransactionAsync(transaction);

                if (!fifo.IsFullyCovered && fifo.MissingQuantity > 0)
                {
                    await _repository.AddInventoryNegativeCostGapAsync(new InventoryNegativeCostGap
                    {
                        SourceType = InventoryNegativeCostGapSources.ManualDocument,
                        StoreInventoryId = inventory.StoreInventoryId,
                        IngredientId = detail.IngredientId,
                        InventoryDocumentDetailId = detail.InventoryDocumentDetailId,
                        InventoryTransaction = transaction,
                        OriginalQuantity = fifo.MissingQuantity,
                        OutstandingQuantity = fifo.MissingQuantity,
                        OccurredAt = DateTime.UtcNow,
                        Status = InventoryNegativeCostGapStatuses.Open
                    });
                }
            }

        }

        // =====================================================
        // WASTE
        // =====================================================

        private async Task ProcessWasteAsync(InventoryDocument document, InventoryProcessResultDTO result)
        {
            foreach (var detail in document.Details)
            {
                ValidateProcessDetail(detail);

                var inventory = await GetExistingInventoryForUpdateAsync(document.StoreId, detail.IngredientId);

                var stockValidation = await EvaluateIssueAsync(document, detail, inventory, InventoryIssueOperation.Waste);

                if (!stockValidation.IsAllowed)
                {
                    throw new InvalidOperationException(stockValidation.ReasonCode);
                }

                var fifo = await AllocateFifoAsync(detail, document.StoreId);

                detail.UnitPrice = 0;
                detail.TotalAmount = 0;
                detail.CostPrice = 0;
                detail.CostAmount = 0;

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

                        InventoryDocumentDetailId = detail.InventoryDocumentDetailId,

                        Type = InventoryTransactionTypeEnum.WASTE,

                        StockStatus = ResolveStockStatus(stockValidation),

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
                ValidateProcessDetail(detail);

                var inventory = await GetOrCreateInventoryAsync(document.StoreId, detail.IngredientId);
                
                var stockValidation = await EvaluateIssueAsync(document, detail, inventory, InventoryIssueOperation.PosBlindSale);

                if (!stockValidation.IsAllowed)
                {
                    throw new InvalidOperationException(stockValidation.ReasonCode);
                }

                var fifo = await AllocateFifoAsync(detail, document.StoreId, requireFullCoverage: false);

                detail.CostPrice = fifo.IsFullyCovered ? fifo.CostPrice : null;
                detail.CostAmount = fifo.IsFullyCovered ? fifo.CostAmount : null;

                _repository.UpdateDocumentDetail(detail);

                var beforeQty = inventory.AvailableQty;

                inventory.AvailableQty -= detail.BaseQuantity;

                _repository.UpdateStoreInventory(inventory);

                AddLowStockWarning(result, document, detail, inventory);

                var transaction = new InventoryTransaction
                    {
                        StoreInventoryId = inventory.StoreInventoryId,

                        InventoryDocumentId = document.InventoryDocumentId,

                        InventoryDocumentDetailId = detail.InventoryDocumentDetailId,

                        Type = InventoryTransactionTypeEnum.SALES_DEDUCTION,

                        StockStatus = ResolveStockStatus(stockValidation),

                        Quantity = detail.BaseQuantity,

                        BeforeQty = beforeQty,

                        AfterQty = inventory.AvailableQty,

                        UnitCost = fifo.IsFullyCovered ? fifo.CostPrice : null,

                        TotalCost = fifo.IsFullyCovered ? fifo.CostAmount : null,

                        CreatedAt = DateTime.UtcNow
                    };
                await _repository.AddInventoryTransactionAsync(transaction);

                if (!fifo.IsFullyCovered && fifo.MissingQuantity > 0)
                {
                    await _repository.AddInventoryNegativeCostGapAsync(new InventoryNegativeCostGap
                    {
                        SourceType = InventoryNegativeCostGapSources.PosSale,
                        StoreInventoryId = inventory.StoreInventoryId,
                        IngredientId = detail.IngredientId,
                        InventoryDocumentDetailId = detail.InventoryDocumentDetailId,
                        InventoryTransaction = transaction,
                        OriginalQuantity = fifo.MissingQuantity,
                        OutstandingQuantity = fifo.MissingQuantity,
                        OccurredAt = DateTime.UtcNow,
                        Status = InventoryNegativeCostGapStatuses.Open
                    });
                }
            }
        }

        // ====================================================
        // PRODUCTION OUT
        // ====================================================
        private async Task ProcessProductionOutAsync(InventoryDocument document, InventoryProcessResultDTO result)
        {
            foreach (var detail in document.Details)
            {
                ValidateProcessDetail(detail);

                var inventory = await GetOrCreateInventoryAsync(document.StoreId, detail.IngredientId);
                
                var stockValidation = await EvaluateIssueAsync(document, detail, inventory, InventoryIssueOperation.ProductionOut);

                if (!stockValidation.IsAllowed)
                {
                    throw new InvalidOperationException(stockValidation.ReasonCode);
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

                        InventoryDocumentDetailId = detail.InventoryDocumentDetailId,

                        Type = InventoryTransactionTypeEnum.PRODUCTION_OUT,

                        StockStatus = ResolveStockStatus(stockValidation),

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
                ValidateProcessDetail(detail);

                var inventory = await GetOrCreateInventoryAsync(document.StoreId, detail.IngredientId);

                var beforeQty = inventory.AvailableQty;

                inventory.AvailableQty += detail.BaseQuantity;

                _repository.UpdateStoreInventory(inventory);

                AddLowStockWarning(result, document, detail, inventory);

                await AddInboundLayerAndSettleAsync(
                    document,
                    detail,
                    inventory,
                    beforeQty,
                    detail.BaseQuantity,
                    detail.CostPrice ?? 0);

                await _repository.AddInventoryTransactionAsync(
                    new InventoryTransaction
                    {
                        StoreInventoryId = inventory.StoreInventoryId,

                        InventoryDocumentId = document.InventoryDocumentId,

                        InventoryDocumentDetailId = detail.InventoryDocumentDetailId,

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
                ValidateStockTakeDetail(detail);

                var inventory = await GetExistingInventoryForUpdateAsync(document.StoreId, detail.IngredientId);

                var systemQty = inventory.AvailableQty;

                var actualQty = detail.BaseQuantity;

                var variance = actualQty - systemQty;

                detail.UnitPrice = 0;
                detail.TotalAmount = 0;
                detail.CostPrice = 0;
                detail.CostAmount = 0;

                if (variance == 0)
                {
                    _repository.UpdateDocumentDetail(detail);
                    continue;
                }

                var transactionQuantity =
                    Math.Abs(variance);

                var transactionType =
                    variance > 0
                        ? InventoryTransactionTypeEnum.ADJUSTMENT_IN
                        : InventoryTransactionTypeEnum.ADJUSTMENT_OUT;

                decimal unitCost;
                decimal totalCost;

                if (variance > 0)
                {
                    unitCost = await ResolveLatestStockTakeUnitCostAsync(document.StoreId, detail.IngredientId);
                    totalCost = transactionQuantity * unitCost;

                    await AddInboundLayerAndSettleAsync(
                        document,
                        detail,
                        inventory,
                        systemQty,
                        transactionQuantity,
                        unitCost);
                }
                else
                {
                    var fifo =
                        await AllocateFifoQuantityAsync(
                            detail,
                            document.StoreId,
                            transactionQuantity);

                    unitCost = fifo.CostPrice;
                    totalCost = fifo.CostAmount;
                }

                detail.UnitPrice = 0;
                detail.TotalAmount = 0;
                detail.CostPrice = 0;
                detail.CostAmount = 0;

                _repository.UpdateDocumentDetail(detail);

                inventory.AvailableQty += variance;

                _repository.UpdateStoreInventory(inventory);

                AddLowStockWarning(result, document, detail, inventory);

                await _repository
                    .AddInventoryTransactionAsync(
                        new InventoryTransaction
                        {
                            StoreInventoryId = inventory.StoreInventoryId,

                            InventoryDocumentId = document.InventoryDocumentId,

                            InventoryDocumentDetailId = detail.InventoryDocumentDetailId,

                            Type = transactionType,

                            StockStatus = InventoryStockStatus.ADJUSTED,

                            Quantity = transactionQuantity,

                            BeforeQty = systemQty,

                            AfterQty = inventory.AvailableQty,

                            UnitCost = unitCost,

                            TotalCost = totalCost,

                            CreatedAt = DateTime.UtcNow
                        });
            }
        }

        // =====================================================
        // INVENTORY
        // =====================================================
        private async Task<StoreInventory> GetOrCreateInventoryAsync(int storeId, int ingredientId)
        {
            return await _repository.GetOrCreateStoreInventoryForIngredientAsync(
                storeId,
                ingredientId);
        }

        private async Task<StoreInventory> GetExistingInventoryForUpdateAsync(int storeId, int ingredientId)
        {
            return await _repository.GetStoreInventoryForUpdateAsync(storeId, ingredientId)
                ?? throw new InvalidOperationException("INGREDIENT_NOT_IN_STORE_INVENTORY");
        }

        private static void ValidateProcessDetail(InventoryDocumentDetail detail)
        {
            if (detail.IngredientId <= 0)
            {
                throw new InvalidOperationException("Nguyên liệu không hợp lệ.");
            }

            if (detail.UnitId <= 0)
            {
                throw new InvalidOperationException("Đơn vị tính không hợp lệ.");
            }

            if (detail.Quantity <= 0)
            {
                throw new InvalidOperationException("Số lượng phải lớn hơn 0.");
            }

            if (detail.BaseQuantity <= 0)
            {
                throw new InvalidOperationException("Số lượng quy đổi base phải lớn hơn 0.");
            }
        }

        private static void ValidateStockTakeDetail(InventoryDocumentDetail detail)
        {
            if (detail.IngredientId <= 0)
            {
                throw new InvalidOperationException("Nguyên liệu không hợp lệ.");
            }

            if (detail.UnitId <= 0)
            {
                throw new InvalidOperationException("Đơn vị tính không hợp lệ.");
            }

            if (detail.Quantity < 0 || detail.BaseQuantity < 0)
            {
                throw new InvalidOperationException("Số lượng kiểm kê thực tế không được âm.");
            }
        }

        private static void AddLowStockWarning(InventoryProcessResultDTO result, InventoryDocument document, InventoryDocumentDetail detail, StoreInventory inventory)
        {
            var threshold = GetDefaultLowStockThreshold(detail);

            if (threshold <= 0)
            {
                return;
            }

            // AvailableQty is already free/unreserved under the inventory invariant.
            var usableQuantity = inventory.AvailableQty;

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

        private async Task AddInboundLayerAndSettleAsync(
            InventoryDocument document,
            InventoryDocumentDetail detail,
            StoreInventory inventory,
            decimal beforeQty,
            decimal receivedQuantity,
            decimal unitCost)
        {
            var deficit = Math.Abs(Math.Min(beforeQty, 0));
            var settledQuantity = Math.Min(receivedQuantity, deficit);
            if (settledQuantity > 0 && unitCost <= 0)
                throw new InvalidOperationException("INBOUND_NEGATIVE_SETTLEMENT_COST_REQUIRED");

            var layer = new InventoryCostLayer
            {
                StoreId = document.StoreId,
                IngredientId = detail.IngredientId,
                Quantity = receivedQuantity,
                RemainingQuantity = receivedQuantity - settledQuantity,
                UnitCost = unitCost,
                SourceInventoryDocumentDetailId = detail.InventoryDocumentDetailId,
                CreatedAt = DateTime.UtcNow
            };
            await _repository.AddCostLayerAsync(layer);

            if (settledQuantity <= 0)
                return;

            var gaps = await _repository.GetOpenCostGapsForUpdateAsync(inventory.StoreInventoryId);
            var outstanding = gaps.Sum(x => x.OutstandingQuantity);
            if (outstanding != deficit)
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
                    InboundInventoryCostLayer = layer,
                    Quantity = quantity,
                    UnitCost = unitCost,
                    TotalCost = quantity * unitCost,
                    CreatedAt = DateTime.UtcNow
                });
                remaining -= quantity;
            }

            if (remaining != 0)
                throw new InvalidOperationException("NEGATIVE_COST_GAP_SETTLEMENT_INCOMPLETE");
            await _repository.AddCostGapSettlementsAsync(settlements);
        }

        // =====================================================
        // FIFO
        // =====================================================

        private async Task<decimal> ResolveLatestStockTakeUnitCostAsync(int storeId, int ingredientId)
        {
            var latestLayer =
                await _repository.GetLatestCostLayerAsync(storeId, ingredientId);

            return latestLayer?.UnitCost > 0
                ? latestLayer.UnitCost
                : 0;
        }

        private Task<FifoAllocationResult> AllocateFifoAsync(
            InventoryDocumentDetail detail,
            int storeId,
            bool requireFullCoverage = true)
        {
            return AllocateFifoQuantityAsync(detail, storeId, detail.BaseQuantity, requireFullCoverage);
        }

        private async Task<FifoAllocationResult> AllocateFifoQuantityAsync(
            InventoryDocumentDetail detail,
            int storeId,
            decimal issueQuantity,
            bool requireFullCoverage = true)
        {
            if (issueQuantity <= 0)
                throw new InvalidOperationException("COST_LAYER_INVALID_QUANTITY");

            var planResult = await _costLayerConsumptionService.PlanConsumeAsync(
                storeId,
                detail.IngredientId,
                null,
                issueQuantity,
                requireFullCoverage);
            if (!planResult.IsSuccess || planResult.Data == null)
                throw new InvalidOperationException(planResult.ErrorCode ?? "FIFO_FULL_COVERAGE_REQUIRED");

            var plan = planResult.Data;
            _costLayerConsumptionService.ApplyPlan(plan);

            if (plan.Slices.Count > 0)
            {
                await _repository.AddCostAllocationsAsync(plan.Slices.Select(slice =>
                    new InventoryCostAllocation
                    {
                        InventoryDocumentDetailId = detail.InventoryDocumentDetailId,
                        InventoryCostLayerId = slice.InventoryCostLayerId,
                        Quantity = slice.Quantity,
                        UnitCost = slice.UnitCost
                    }));
            }

            return new FifoAllocationResult(
                plan.WeightedUnitCost,
                plan.TotalCost,
                plan.IsFullyCovered,
                plan.RequiredQuantity - plan.CoveredQuantity);
        }

        private sealed record FifoAllocationResult(
            decimal CostPrice,
            decimal CostAmount,
            bool IsFullyCovered,
            decimal MissingQuantity);

        private async Task<InventoryIssueDecision> EvaluateIssueAsync(
            InventoryDocument document,
            InventoryDocumentDetail detail,
            StoreInventory inventory,
            InventoryIssueOperation operation)
        {
            InventoryApprovalEvidence? evidence = null;
            var approval = await _repository.GetNegativeApprovalForUpdateAsync(document.InventoryDocumentId);
            var line = approval?.Lines.FirstOrDefault(x => x.InventoryDocumentDetailId == detail.InventoryDocumentDetailId);
            if (approval?.Status == CafeChain.Models.Inventories.Approvals.InventoryNegativeApprovalStatuses.Approved
                && line != null)
            {
                evidence = new InventoryApprovalEvidence(
                    approval.InventoryNegativeApprovalId,
                    approval.StoreId,
                    line.IngredientId,
                    line.PreparedItemId,
                    line.BeforeQty,
                    line.ProjectedAfterQty,
                    line.EffectiveMaxNegativeQty,
                    approval.PolicyVersion,
                    approval.RequesterStaffId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    approval.ApproverStaffId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                    true,
                    approval.ScopeAuthorized,
                    line.IssueQty,
                    approval.Reason,
                    line.InventoryRowVersion);
            }

            return await _inventoryIssuePolicy.EvaluateAsync(
                new InventoryIssueRequest(
                    operation,
                    document.StoreId,
                    detail.IngredientId,
                    null,
                    inventory.AvailableQty,
                    detail.BaseQuantity,
                    inventory.MaxNegativeQty,
                    document.Purpose.ToString(),
                    document.NegativeReason,
                    approval?.PolicyVersion,
                    evidence,
                    inventory.RowVersion,
                    document.AllowNegativeStock));
        }

        private static InventoryStockStatus ResolveStockStatus(InventoryIssueDecision decision) =>
            decision.IsNegative
                ? InventoryStockStatus.NEGATIVE_CONFIRMED
                : InventoryStockStatus.NORMAL;

    }
}
