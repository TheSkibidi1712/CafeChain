using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Costing;
using CafeChain.Models.Inventories.Transactions;
using CafeChain.Models.Orders;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CafeChain.Application.Services.Inventories
{
    /// <summary>
    /// POS sales stock deduction. Issue #121: PreparedItem-mode BTP via canonical identity;
    /// LegacyRecipe keeps RecipeId BTP; Blind Selling (negative qty allowed).
    /// Issue #133: actual FIFO COGS allocations/gaps + order snapshot (payment-independent Option B).
    /// </summary>
    public class InventoryDeductionService : IInventoryDeductionService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<InventoryDeductionService> _logger;
        private readonly IUnitConversionService _unitConversion;
        private readonly IPhysicalUnitConversionService _physicalConversion;
        private readonly IEstimatedBomCostService _estimatedBomCost;
        private readonly IStockAlertService? _stockAlertService;
        private readonly IInventoryWriterModeService? _writerModeService;
        private readonly IStoreInventoryWriteResolver? _writeResolver;
        private readonly IInventoryCostLayerConsumptionService? _costLayerConsumption;

        public InventoryDeductionService(
            AppDbContext context,
            ILogger<InventoryDeductionService> logger,
            IUnitConversionService unitConversion,
            IEstimatedBomCostService estimatedBomCost,
            IPhysicalUnitConversionService physicalConversion,
            IStockAlertService? stockAlertService = null,
            IInventoryWriterModeService? writerModeService = null,
            IStoreInventoryWriteResolver? writeResolver = null,
            IInventoryCostLayerConsumptionService? costLayerConsumption = null)
        {
            _context = context;
            _logger = logger;
            _unitConversion = unitConversion;
            _estimatedBomCost = estimatedBomCost;
            _physicalConversion = physicalConversion;
            _stockAlertService = stockAlertService;
            _writerModeService = writerModeService;
            _writeResolver = writeResolver;
            _costLayerConsumption = costLayerConsumption;
        }

        public async Task<ServiceResult<decimal>> CalculateRecipeCogsAsync(int recipeId)
        {
            var estimate = await _estimatedBomCost.CalculateRecipeEstimatedCostAsync(recipeId);
            if (estimate.IsComplete && estimate.TotalCost.HasValue)
                return ServiceResult<decimal>.Success(estimate.TotalCost.Value);

            var message = estimate.Issues.Count > 0
                ? estimate.Issues[0].Message
                : $"Giá vốn ước tính chưa đủ dữ liệu cho Recipe #{recipeId}.";

            _logger.LogWarning(
                "EstimatedBomCost incomplete for Recipe #{RecipeId}: {Message} (issues={Count})",
                recipeId, message, estimate.Issues.Count);

            return ServiceResult<decimal>.Failure(message);
        }

        public async Task<ServiceResult> DeductStockForOrderAsync(List<POSSoldItemDto> soldItems, int storeId)
        {
            return await DeductStockWithConcurrencyRetryAsync(soldItems, storeId, null);
        }

        public async Task<ServiceResult> DeductStockForCommittedOrderAsync(
            List<POSSoldItemDto> soldItems,
            int storeId,
            int referenceOrderId)
        {
            if (referenceOrderId <= 0)
                return ServiceResult.Failure("Thiếu mã đơn hàng đã commit để trừ kho.");

            return await DeductStockWithConcurrencyRetryAsync(soldItems, storeId, referenceOrderId);
        }

        private async Task<ServiceResult> DeductStockWithConcurrencyRetryAsync(
            List<POSSoldItemDto> soldItems,
            int storeId,
            int? referenceOrderId)
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                var result = await DeductStockForOrderInternalAsync(soldItems, storeId, referenceOrderId);
                if (result.IsSuccess)
                    return result;

                var isConcurrency = (result.Message ?? string.Empty).Contains("tranh chấp", StringComparison.OrdinalIgnoreCase)
                    || (result.Message ?? string.Empty).Contains("concurrency", StringComparison.OrdinalIgnoreCase);
                if (!isConcurrency || attempt == 2)
                    return result;

                _context.ChangeTracker.Clear();
                _logger.LogWarning(
                    "[InventoryDeduction] Retry after concurrency StoreId={StoreId} OrderId={OrderId} Attempt={Attempt}",
                    storeId,
                    referenceOrderId,
                    attempt + 1);
            }

            return ServiceResult.Failure("Lỗi hệ thống: Có nhiều giao dịch đồng thời đang tranh chấp kho. Vui lòng thử lại.");
        }

        private async Task<ServiceResult> DeductStockForOrderInternalAsync(
            List<POSSoldItemDto> soldItems,
            int storeId,
            int? referenceOrderId)
        {
            if (soldItems == null || !soldItems.Any())
                return ServiceResult.Failure("Không có sản phẩm nào để xuất kho.");

            var inventoryWarnings = new List<string>();

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                Order? lockedOrder = null;
                if (referenceOrderId.HasValue)
                {
                    lockedOrder = await LoadOrderForUpdateAsync(referenceOrderId.Value);
                    if (lockedOrder == null
                        || lockedOrder.StoreId != storeId
                        || lockedOrder.OrderStatusId != SystemConstants.OrderStatuses.Completed
                        || lockedOrder.PaymentStatusId != SystemConstants.PaymentStatuses.Paid)
                    {
                        await transaction.RollbackAsync();
                        return ServiceResult.Failure("Chỉ trừ kho cho đơn POS đã thanh toán và đã commit.");
                    }

                    var alreadyDeducted = await _context.InventoryTransactions
                        .AsNoTracking()
                        .AnyAsync(t =>
                            t.ReferenceOrderId == referenceOrderId.Value
                            && t.Type == InventoryTransactionTypeEnum.SALES_DEDUCTION);

                    if (alreadyDeducted)
                    {
                        await transaction.CommitAsync();
                        return BuildReplayResult(lockedOrder);
                    }
                }

                InventoryWriterModeSnapshot? modeSnapshot = null;
                InventoryWriterMode mode = InventoryWriterMode.LegacyRecipe;

                if (_writerModeService != null)
                {
                    var hasBtp = await SoldItemsContainBtpAsync(soldItems);
                    if (hasBtp)
                    {
                        var snapshotResult = await _writerModeService.AcquireSnapshotAsync(storeId);
                        if (!snapshotResult.IsSuccess || snapshotResult.Data == null)
                        {
                            await transaction.RollbackAsync();
                            return ServiceResult.Failure(snapshotResult.Message, errorCode: snapshotResult.ErrorCode);
                        }

                        modeSnapshot = snapshotResult.Data;
                        mode = modeSnapshot.WriterMode;

                        if (mode == InventoryWriterMode.Blocked)
                        {
                            await transaction.RollbackAsync();
                            return ServiceResult.Failure(
                                "Kho BTP của cửa hàng đang bị khóa; không thể trừ BTP.",
                                errorCode: InventoryWriterFailureCodes.ModeBlocked);
                        }

                        if (mode == InventoryWriterMode.LegacyRecipe)
                        {
                            var guard = _writerModeService.EnsureLegacyBtpWriteAllowed(modeSnapshot, storeId);
                            if (!guard.IsSuccess)
                            {
                                await transaction.RollbackAsync();
                                return guard;
                            }
                        }
                        else if (mode == InventoryWriterMode.PreparedItem)
                        {
                            if (_writeResolver == null || _physicalConversion == null)
                            {
                                await transaction.RollbackAsync();
                                return ServiceResult.Failure(
                                    "PreparedItem POS writer chưa được cấu hình đầy đủ.",
                                    errorCode: "POS_PREPARED_WRITER_NOT_CONFIGURED");
                            }
                        }
                    }
                }

                // Build attributed requirements.
                // Prefer OrderDetails when present (#133); otherwise soldItems (legacy tests/mock).
                var requirements = new List<RequirementLine>();
                var usedOrderLines = false;
                if (referenceOrderId.HasValue)
                {
                    var detailCount = await _context.OrderDetails
                        .AsNoTracking()
                        .CountAsync(d => d.OrderId == referenceOrderId.Value);
                    if (detailCount > 0)
                    {
                        await CollectRequirementsFromOrderAsync(
                            referenceOrderId.Value,
                            storeId,
                            mode,
                            modeSnapshot,
                            requirements);
                        usedOrderLines = true;
                    }
                }

                if (!usedOrderLines)
                {
                    foreach (var item in soldItems)
                    {
                        var drinkRecipe = await GetActiveRecipeAsync(item.DrinkId, item.SizeId, null);
                        if (drinkRecipe == null)
                        {
                            _logger.LogWarning(
                                "Không tìm thấy công thức (BOM) hoạt động cho DrinkId={DrinkId}, SizeId={SizeId}",
                                item.DrinkId,
                                item.SizeId);
                        }
                        else
                        {
                            await CollectRequirementsAsync(
                                drinkRecipe,
                                item.Quantity,
                                storeId,
                                mode,
                                modeSnapshot,
                                requirements,
                                orderDetailId: null,
                                orderToppingId: null);
                        }

                        foreach (var topping in item.Toppings ?? new List<POSOrderToppingDto>())
                        {
                            var toppingRecipe = await GetActiveRecipeAsync(null, null, topping.ToppingId);
                            if (toppingRecipe == null)
                            {
                                _logger.LogWarning(
                                    "Không tìm thấy công thức (BOM) hoạt động cho ToppingId={ToppingId}",
                                    topping.ToppingId);
                                continue;
                            }

                            await CollectRequirementsAsync(
                                toppingRecipe,
                                item.Quantity,
                                storeId,
                                mode,
                                modeSnapshot,
                                requirements,
                                orderDetailId: null,
                                orderToppingId: null);
                        }
                    }
                }

                var mutationGroups = requirements
                    .GroupBy(r => r.StoreInventoryId)
                    .Select(g => new
                    {
                        StoreInventoryId = g.Key,
                        RequiredQty = g.Sum(x => x.RequiredQty),
                        DisplayName = g.First().DisplayName
                    })
                    .OrderBy(x => x.StoreInventoryId)
                    .ToList();

                var ledgerGroups = requirements
                    .GroupBy(r => new { r.StoreInventoryId, r.SourceRecipeId })
                    .Select(g => new
                    {
                        g.Key.StoreInventoryId,
                        g.Key.SourceRecipeId,
                        RequiredQty = g.Sum(x => x.RequiredQty),
                        Lines = g.ToList(),
                        DisplayName = g.First().DisplayName
                    })
                    .OrderBy(x => x.StoreInventoryId)
                    .ThenBy(x => x.SourceRecipeId)
                    .ToList();

                // ---- Cost plans (partial allowed for sales) BEFORE qty mutation ----
                var costByIdentity = new Dictionary<string, CostLayerConsumptionPlan>();
                if (_costLayerConsumption != null && referenceOrderId.HasValue)
                {
                    foreach (var identityGroup in requirements
                        .Where(r => r.IngredientId.HasValue || r.PreparedItemId.HasValue)
                        .GroupBy(r => IdentityKey(r.IngredientId, r.PreparedItemId))
                        .OrderBy(g => g.Key))
                    {
                        var sample = identityGroup.First();
                        var required = identityGroup.Sum(x => x.RequiredQty);
                        var planResult = await _costLayerConsumption.PlanConsumeAsync(
                            storeId,
                            sample.IngredientId,
                            sample.PreparedItemId,
                            required,
                            requireFullCoverage: false);

                        if (!planResult.IsSuccess || planResult.Data == null)
                        {
                            // Treat plan failure (invalid identity) as zero coverage gap for sales.
                            costByIdentity[identityGroup.Key] = new CostLayerConsumptionPlan
                            {
                                StoreId = storeId,
                                IngredientId = sample.IngredientId,
                                PreparedItemId = sample.PreparedItemId,
                                RequiredQuantity = required,
                                CoveredQuantity = 0,
                                AvailableLayerQuantity = 0,
                                TotalCost = 0,
                                WeightedUnitCost = 0,
                                IsFullyCovered = false,
                                Slices = Array.Empty<CostLayerAllocationSlice>()
                            };
                        }
                        else
                        {
                            costByIdentity[identityGroup.Key] = planResult.Data;
                        }
                    }
                }

                var lockedRows = new Dictionary<int, StoreInventory>();
                var preMutationQty = new Dictionary<int, decimal>();

                foreach (var line in mutationGroups)
                {
                    var inv = await LoadInventoryForUpdateAsync(line.StoreInventoryId);
                    if (inv == null)
                        throw new InvalidOperationException(
                            $"Không tìm thấy StoreInventory #{line.StoreInventoryId} sau khi resolve.");

                    var tracked = _context.StoreInventories.Local
                        .FirstOrDefault(x => x.StoreInventoryId == inv.StoreInventoryId)
                        ?? inv;
                    if (_context.Entry(tracked).State == EntityState.Detached)
                        _context.StoreInventories.Attach(tracked);

                    lockedRows[tracked.StoreInventoryId] = tracked;
                    preMutationQty[tracked.StoreInventoryId] = tracked.AvailableQty;

                    var beforeQty = tracked.AvailableQty;
                    tracked.AvailableQty -= line.RequiredQty;
                    tracked.LastUpdated = DateTime.UtcNow;

                    if (tracked.AvailableQty < 0)
                    {
                        inventoryWarnings.Add(
                            $"⚠️ {line.DisplayName}: tồn kho âm ({tracked.AvailableQty:N2}), " +
                            $"trước xuất: {beforeQty:N2}, xuất: {line.RequiredQty:N2}");

                        _logger.LogWarning(
                            "[InventoryDeduction] Kho âm — StoreId={StoreId}, Item={ItemName}, " +
                            "Before={Before:N2}, Deducted={Deducted:N2}, After={After:N2}",
                            storeId, line.DisplayName, beforeQty, line.RequiredQty, tracked.AvailableQty);
                    }
                }

                // Apply layer decrements (partial OK)
                if (_costLayerConsumption != null)
                {
                    foreach (var plan in costByIdentity.Values.OrderBy(p =>
                                 IdentityKey(p.IngredientId, p.PreparedItemId)))
                    {
                        if (plan.Slices.Count > 0)
                            _costLayerConsumption.ApplyPlan(plan);
                    }
                }

                // Distribute FIFO slices to attributed requirements (deterministic line order).
                var now = DateTime.UtcNow;
                var reqCostState = BuildRequirementCostDistribution(requirements, costByIdentity);

                var runningQty = new Dictionary<int, decimal>(preMutationQty);
                var pendingAllocations = new List<(InventoryTransaction Tx, RequirementLine Req, CostLayerAllocationSlice Slice)>();
                var ledgerCompleteness = new Dictionary<InventoryTransaction, bool>();

                foreach (var ledger in ledgerGroups)
                {
                    if (ledger.SourceRecipeId <= 0)
                    {
                        throw new InvalidOperationException(
                            "Thiếu SourceRecipeId durable audit cho movement SALES_DEDUCTION.");
                    }

                    var beforeQty = runningQty[ledger.StoreInventoryId];
                    var afterQty = beforeQty - ledger.RequiredQty;
                    runningQty[ledger.StoreInventoryId] = afterQty;

                    var lines = ledger.Lines;
                    var allComplete = lines.All(l => reqCostState[l].IsComplete);
                    decimal? unitCost = null;
                    decimal? totalCost = null;
                    if (allComplete && lines.Count > 0)
                    {
                        totalCost = lines.Sum(l => reqCostState[l].AllocatedCost);
                        unitCost = ledger.RequiredQty > 0 ? totalCost / ledger.RequiredQty : null;
                    }

                    var tx = new InventoryTransaction
                    {
                        StoreInventoryId = ledger.StoreInventoryId,
                        Type = InventoryTransactionTypeEnum.SALES_DEDUCTION,
                        StockStatus = afterQty < 0
                            ? InventoryStockStatus.NEGATIVE_CONFIRMED
                            : InventoryStockStatus.NORMAL,
                        Quantity = ledger.RequiredQty,
                        BeforeQty = beforeQty,
                        AfterQty = afterQty,
                        UnitCost = unitCost,
                        TotalCost = totalCost,
                        ReferenceOrderId = referenceOrderId,
                        SourceRecipeId = ledger.SourceRecipeId,
                        CreatedAt = now
                    };
                    _context.InventoryTransactions.Add(tx);
                    ledgerCompleteness[tx] = allComplete;

                    foreach (var req in lines)
                    {
                        foreach (var slice in reqCostState[req].Slices)
                        {
                            pendingAllocations.Add((tx, req, slice));
                        }
                    }
                }

                await _context.SaveChangesAsync(); // materialize transaction ids

                if (referenceOrderId.HasValue && lockedOrder != null)
                {
                    foreach (var (tx, req, slice) in pendingAllocations)
                    {
                        if (!req.OrderDetailId.HasValue)
                            continue;

                        _context.SalesCostAllocations.Add(new SalesCostAllocation
                        {
                            OrderId = referenceOrderId.Value,
                            OrderDetailId = req.OrderDetailId.Value,
                            OrderToppingId = req.OrderToppingId,
                            InventoryTransactionId = tx.InventoryTransactionId,
                            InventoryCostLayerId = slice.InventoryCostLayerId,
                            IngredientId = req.IngredientId,
                            PreparedItemId = req.PreparedItemId,
                            Quantity = slice.Quantity,
                            UnitCost = slice.UnitCost,
                            TotalCost = slice.TotalCost,
                            CreatedAtUtc = now
                        });
                    }

                    // Gaps only when inventory identity supports cost layers (Ingredient XOR PreparedItem).
                    foreach (var req in requirements
                        .Where(r => r.OrderDetailId.HasValue
                                    && (r.IngredientId.HasValue || r.PreparedItemId.HasValue))
                        .OrderBy(r => r.OrderDetailId)
                        .ThenBy(r => r.OrderToppingId ?? 0)
                        .ThenBy(r => IdentityKey(r.IngredientId, r.PreparedItemId)))
                    {
                        var state = reqCostState[req];
                        if (state.IsComplete)
                            continue;

                        _context.SalesCostGaps.Add(new SalesCostGap
                        {
                            OrderId = referenceOrderId.Value,
                            OrderDetailId = req.OrderDetailId!.Value,
                            OrderToppingId = req.OrderToppingId,
                            IngredientId = req.IngredientId,
                            PreparedItemId = req.PreparedItemId,
                            RequiredQuantity = req.RequiredQty,
                            AllocatedCostQuantity = state.AllocatedQty,
                            MissingCostQuantity = Math.Max(0m, req.RequiredQty - state.AllocatedQty),
                            BaseUnitId = req.BaseUnitId,
                            ReasonCode = SalesCogsCodes.Incomplete,
                            CreatedAtUtc = now
                        });
                    }

                    await SnapshotOrderCogsAsync(lockedOrder, requirements, reqCostState, now);
                    if (lockedOrder.CostStatus == SalesCostStatus.Incomplete)
                    {
                        inventoryWarnings.Add(
                            $"{SalesCogsCodes.Incomplete}: Đơn hàng đã thanh toán nhưng giá vốn chưa đầy đủ.");
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await EvaluateStockAlertsSafeAsync(storeId, referenceOrderId);

                if (inventoryWarnings.Any())
                {
                    var result = ServiceResult.Success(
                        inventoryWarnings.Any(w => w.StartsWith(SalesCogsCodes.Incomplete, StringComparison.Ordinal))
                            ? "Trừ kho thành công. Giá vốn chưa đầy đủ."
                            : $"Trừ kho thành công. Cảnh báo: {inventoryWarnings.Count} nguyên liệu tồn kho âm.");
                    result.Errors = inventoryWarnings;
                    return result;
                }

                return ServiceResult.Success("Trừ kho bán hàng thành công.");
            }
            catch (DbUpdateConcurrencyException ex)
            {
                try { await transaction.RollbackAsync(); } catch { /* ignore */ }
                _context.ChangeTracker.Clear();
                _logger.LogError(ex, "Lỗi tranh chấp dữ liệu khi trừ kho.");
                return ServiceResult.Failure(
                    "Lỗi hệ thống: Có nhiều giao dịch đồng thời đang tranh chấp kho. Vui lòng thử lại.");
            }
            catch (Exception ex)
            {
                try { await transaction.RollbackAsync(); } catch { /* ignore */ }
                _logger.LogError(ex, "Lỗi xuất kho bán hàng.");
                return ServiceResult.Failure($"Lỗi xuất kho: {ex.Message}");
            }
        }

        private ServiceResult BuildReplayResult(Order order)
        {
            if (order.CostStatus == SalesCostStatus.Incomplete)
            {
                var r = ServiceResult.Success("Đơn hàng đã được trừ kho trước đó.");
                r.Errors = new List<string>
                {
                    $"{SalesCogsCodes.Incomplete}: Đơn hàng đã thanh toán nhưng giá vốn chưa đầy đủ."
                };
                return r;
            }

            return ServiceResult.Success("Đơn hàng đã được trừ kho trước đó.");
        }

        private static string IdentityKey(int? ingredientId, int? preparedItemId)
            => ingredientId.HasValue ? $"I:{ingredientId.Value}" : $"P:{preparedItemId ?? 0}";

        private sealed class ReqCostState
        {
            public decimal AllocatedQty { get; set; }
            public decimal AllocatedCost { get; set; }
            public bool IsComplete { get; set; }
            public List<CostLayerAllocationSlice> Slices { get; } = new();
        }

        private static Dictionary<RequirementLine, ReqCostState> BuildRequirementCostDistribution(
            List<RequirementLine> requirements,
            Dictionary<string, CostLayerConsumptionPlan> costByIdentity)
        {
            var result = requirements.ToDictionary(r => r, _ => new ReqCostState());

            // Mutable pool of remaining slice qty per identity (FIFO already ordered)
            var pools = new Dictionary<string, List<(CostLayerAllocationSlice Slice, decimal Remaining)>>();
            foreach (var kv in costByIdentity)
            {
                pools[kv.Key] = kv.Value.Slices
                    .Select(s => (s, s.Quantity))
                    .ToList();
            }

            var orderedReqs = requirements
                .Where(r => r.IngredientId.HasValue || r.PreparedItemId.HasValue)
                .OrderBy(r => r.OrderDetailId ?? int.MaxValue)
                .ThenBy(r => r.OrderToppingId ?? int.MaxValue)
                .ThenBy(r => IdentityKey(r.IngredientId, r.PreparedItemId))
                .ThenBy(r => r.SourceRecipeId)
                .ThenBy(r => r.StoreInventoryId)
                .ToList();

            foreach (var req in orderedReqs)
            {
                var key = IdentityKey(req.IngredientId, req.PreparedItemId);
                if (!pools.TryGetValue(key, out var pool))
                {
                    result[req].IsComplete = false;
                    continue;
                }

                var need = req.RequiredQty;
                var state = result[req];
                for (var i = 0; i < pool.Count && need > 0; i++)
                {
                    var (slice, rem) = pool[i];
                    if (rem <= 0)
                        continue;

                    var take = Math.Min(need, rem);
                    if (take <= 0)
                        continue;

                    var portionCost = take * slice.UnitCost;
                    state.Slices.Add(new CostLayerAllocationSlice
                    {
                        Layer = slice.Layer,
                        InventoryCostLayerId = slice.InventoryCostLayerId,
                        Quantity = take,
                        UnitCost = slice.UnitCost,
                        TotalCost = portionCost
                    });
                    state.AllocatedQty += take;
                    state.AllocatedCost += portionCost;
                    pool[i] = (slice, rem - take);
                    need -= take;
                }

                state.IsComplete = need <= 0 && state.AllocatedQty == req.RequiredQty && req.RequiredQty > 0;
            }

            // Requirements without layer identity (legacy recipe-only rows): no COGS evidence
            foreach (var req in requirements.Where(r => !r.IngredientId.HasValue && !r.PreparedItemId.HasValue))
            {
                result[req].IsComplete = false;
            }

            return result;
        }

        private async Task SnapshotOrderCogsAsync(
            Order order,
            List<RequirementLine> requirements,
            Dictionary<RequirementLine, ReqCostState> costState,
            DateTime now)
        {
            var details = await _context.OrderDetails
                .Include(d => d.OrderToppings)
                .Where(d => d.OrderId == order.OrderId)
                .OrderBy(d => d.OrderDetailId)
                .ToListAsync();

            foreach (var detail in details)
            {
                // Drink BOM requirements (no topping id)
                var drinkReqs = requirements
                    .Where(r => r.OrderDetailId == detail.OrderDetailId && r.OrderToppingId == null)
                    .ToList();

                if (drinkReqs.Count == 0)
                {
                    // No BOM lines → treat complete with zero actual cost for drink body
                    detail.CostStatus = SalesCostStatus.Complete;
                    detail.TotalCogs = 0m;
                    detail.UnitCogs = 0m;
                }
                else if (drinkReqs.All(r => costState[r].IsComplete))
                {
                    var total = drinkReqs.Sum(r => costState[r].AllocatedCost);
                    detail.CostStatus = SalesCostStatus.Complete;
                    detail.TotalCogs = total;
                    detail.UnitCogs = detail.Quantity > 0 ? total / detail.Quantity : null;
                }
                else
                {
                    detail.CostStatus = SalesCostStatus.Incomplete;
                    detail.TotalCogs = null;
                    detail.UnitCogs = null;
                }

                foreach (var topping in detail.OrderToppings.OrderBy(t => t.OrderToppingId))
                {
                    var topReqs = requirements
                        .Where(r => r.OrderDetailId == detail.OrderDetailId
                                    && r.OrderToppingId == topping.OrderToppingId)
                        .ToList();

                    if (topReqs.Count == 0)
                    {
                        topping.CostStatus = SalesCostStatus.Complete;
                        topping.TotalCogs = 0m;
                    }
                    else if (topReqs.All(r => costState[r].IsComplete))
                    {
                        topping.CostStatus = SalesCostStatus.Complete;
                        topping.TotalCogs = topReqs.Sum(r => costState[r].AllocatedCost);
                    }
                    else
                    {
                        topping.CostStatus = SalesCostStatus.Incomplete;
                        topping.TotalCogs = null;
                    }
                }
            }

            var anyIncomplete = details.Any(d =>
                d.CostStatus == SalesCostStatus.Incomplete
                || d.OrderToppings.Any(t => t.CostStatus == SalesCostStatus.Incomplete));

            // Also incomplete if attributed requirements missing completeness
            if (requirements.Any(r => r.OrderDetailId.HasValue && !costState[r].IsComplete
                                      && (r.IngredientId.HasValue || r.PreparedItemId.HasValue
                                          || (!r.IngredientId.HasValue && !r.PreparedItemId.HasValue))))
            {
                // legacy recipe identity without layer → incomplete
                anyIncomplete = anyIncomplete
                    || requirements.Any(r => r.OrderDetailId.HasValue && !costState[r].IsComplete);
            }

            order.CostedAtUtc = now;
            if (anyIncomplete)
            {
                order.CostStatus = SalesCostStatus.Incomplete;
                order.TotalCogs = null;
                order.GrossProfit = null;
            }
            else
            {
                var totalCogs = details.Sum(d => (d.TotalCogs ?? 0m)
                    + d.OrderToppings.Sum(t => t.TotalCogs ?? 0m));
                order.CostStatus = SalesCostStatus.Complete;
                order.TotalCogs = totalCogs;
                // Revenue authority: actual collected total after voucher/points (Order.Total).
                order.GrossProfit = order.Total - totalCogs;
            }
        }

        private async Task CollectRequirementsFromOrderAsync(
            int orderId,
            int storeId,
            InventoryWriterMode mode,
            InventoryWriterModeSnapshot? modeSnapshot,
            List<RequirementLine> requirements)
        {
            var details = await _context.OrderDetails
                .Include(d => d.OrderToppings)
                .Where(d => d.OrderId == orderId)
                .OrderBy(d => d.OrderDetailId)
                .ToListAsync();

            foreach (var detail in details)
            {
                var drinkRecipe = await GetActiveRecipeAsync(detail.DrinkId, detail.SizeId, null);
                if (drinkRecipe == null)
                {
                    _logger.LogWarning(
                        "Không tìm thấy công thức (BOM) hoạt động cho OrderDetail #{OrderDetailId} DrinkId={DrinkId}",
                        detail.OrderDetailId,
                        detail.DrinkId);
                }
                else
                {
                    await CollectRequirementsAsync(
                        drinkRecipe,
                        detail.Quantity,
                        storeId,
                        mode,
                        modeSnapshot,
                        requirements,
                        detail.OrderDetailId,
                        orderToppingId: null);
                }

                foreach (var topping in detail.OrderToppings.OrderBy(t => t.OrderToppingId))
                {
                    var toppingRecipe = await GetActiveRecipeAsync(null, null, topping.ToppingId);
                    if (toppingRecipe == null)
                    {
                        _logger.LogWarning(
                            "Không tìm thấy công thức topping cho OrderTopping #{Id} ToppingId={ToppingId}",
                            topping.OrderToppingId,
                            topping.ToppingId);
                        continue;
                    }

                    await CollectRequirementsAsync(
                        toppingRecipe,
                        detail.Quantity,
                        storeId,
                        mode,
                        modeSnapshot,
                        requirements,
                        detail.OrderDetailId,
                        topping.OrderToppingId);
                }
            }
        }

        private async Task CollectRequirementsAsync(
            Recipe saleRecipe,
            int soldQuantity,
            int storeId,
            InventoryWriterMode mode,
            InventoryWriterModeSnapshot? modeSnapshot,
            List<RequirementLine> requirements,
            int? orderDetailId,
            int? orderToppingId)
        {
            var details = saleRecipe.RecipeDetails?.ToList() ?? new List<RecipeDetail>();
            foreach (var detail in details)
            {
                decimal rawRequired = detail.Quantity * soldQuantity;

                if (detail.IngredientId.HasValue)
                {
                    var converted = await _unitConversion.ConvertAsync(
                        detail.IngredientId.Value,
                        rawRequired,
                        detail.UnitId);
                    if (!converted.IsSuccess)
                    {
                        throw new InvalidOperationException(
                            converted.Message ??
                            $"Thiếu quy đổi đơn vị cho nguyên liệu #{detail.IngredientId}.");
                    }

                    var inv = await GetOrCreateIngredientInventoryAsync(storeId, detail.IngredientId.Value);
                    var ing = await _context.Ingredients.AsNoTracking()
                        .FirstOrDefaultAsync(i => i.IngredientId == detail.IngredientId.Value);
                    var name = ing?.Name ?? $"Ingredient #{detail.IngredientId}";

                    requirements.Add(new RequirementLine
                    {
                        StoreInventoryId = inv.StoreInventoryId,
                        RequiredQty = converted.Data,
                        SourceRecipeId = saleRecipe.RecipeId,
                        DisplayName = name,
                        OrderDetailId = orderDetailId,
                        OrderToppingId = orderToppingId,
                        IngredientId = detail.IngredientId.Value,
                        PreparedItemId = null,
                        BaseUnitId = ing?.BaseUnitId
                    });
                    continue;
                }

                if (!detail.ChildRecipeId.HasValue)
                {
                    throw new InvalidOperationException(
                        $"RecipeDetail #{detail.RecipeDetailId} phải có IngredientId hoặc ChildRecipeId.");
                }

                if (mode == InventoryWriterMode.Blocked)
                {
                    throw new InvalidOperationException(
                        "Kho BTP đang bị khóa; không thể trừ ChildRecipe/BTP.");
                }

                if (mode == InventoryWriterMode.PreparedItem)
                {
                    await CollectPreparedBtpRequirementAsync(
                        detail,
                        rawRequired,
                        storeId,
                        modeSnapshot!,
                        requirements,
                        orderDetailId,
                        orderToppingId);
                }
                else
                {
                    var inv = await GetOrCreateLegacyRecipeInventoryAsync(storeId, detail.ChildRecipeId.Value);
                    requirements.Add(new RequirementLine
                    {
                        StoreInventoryId = inv.StoreInventoryId,
                        RequiredQty = rawRequired,
                        SourceRecipeId = detail.ChildRecipeId.Value,
                        DisplayName = $"Recipe #{detail.ChildRecipeId}",
                        OrderDetailId = orderDetailId,
                        OrderToppingId = orderToppingId,
                        IngredientId = null,
                        PreparedItemId = null,
                        BaseUnitId = null
                    });
                }
            }
        }

        private async Task CollectPreparedBtpRequirementAsync(
            RecipeDetail detail,
            decimal rawRequired,
            int storeId,
            InventoryWriterModeSnapshot modeSnapshot,
            List<RequirementLine> requirements,
            int? orderDetailId,
            int? orderToppingId)
        {
            var child = await _context.Recipes
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.RecipeId == detail.ChildRecipeId!.Value);

            if (child == null)
            {
                throw new InvalidOperationException(
                    $"Không tìm thấy ChildRecipe #{detail.ChildRecipeId}.");
            }

            if (!child.PreparedItemId.HasValue)
            {
                throw new InvalidOperationException(
                    $"ChildRecipe #{child.RecipeId} chưa map PreparedItemId.");
            }

            if (child.OutputQuantity is null or <= 0 || !child.OutputUnitId.HasValue)
            {
                throw new InvalidOperationException(
                    $"ChildRecipe #{child.RecipeId} thiếu output contract hợp lệ (OutputQuantity/OutputUnitId).");
            }

            var preparedItem = await _context.PreparedItems
                .AsNoTracking()
                .Include(p => p.BaseUnit)
                .FirstOrDefaultAsync(p => p.PreparedItemId == child.PreparedItemId.Value);

            if (preparedItem == null || !preparedItem.Active)
            {
                throw new InvalidOperationException(
                    $"PreparedItem #{child.PreparedItemId} không hợp lệ hoặc không Active.");
            }

            if (preparedItem.BaseUnit == null || !preparedItem.BaseUnit.Active)
            {
                throw new InvalidOperationException(
                    $"BaseUnit của PreparedItem #{preparedItem.PreparedItemId} không hợp lệ.");
            }

            var converted = await _physicalConversion.ConvertAsync(
                rawRequired,
                detail.UnitId,
                preparedItem.BaseUnitId);
            if (!converted.IsSuccess)
            {
                throw new InvalidOperationException(
                    converted.Message ??
                    $"Không quy đổi được đơn vị BTP ChildRecipe #{child.RecipeId} → PreparedItem base unit.");
            }

            var resolve = await _writeResolver!.ResolveAsync(new StoreInventoryWriteRequest
            {
                ModeSnapshot = modeSnapshot,
                StoreId = storeId,
                IdentityType = InventoryWriteIdentityTypes.PreparedItem,
                PreparedItemId = preparedItem.PreparedItemId,
                NormalizedBaseUnitId = preparedItem.BaseUnitId,
                SourceRecipeId = child.RecipeId,
                AllowCreateIntent = false
            });

            if (resolve.Status != InventoryWriteResolutionStatuses.FoundCanonical || resolve.StoreInventory == null)
            {
                throw new InvalidOperationException(
                    $"Không resolve được tồn PreparedItem #{preparedItem.PreparedItemId}: {resolve.Status} — {resolve.Message}");
            }

            requirements.Add(new RequirementLine
            {
                StoreInventoryId = resolve.StoreInventory.StoreInventoryId,
                RequiredQty = converted.Data,
                SourceRecipeId = child.RecipeId,
                DisplayName = preparedItem.Name,
                OrderDetailId = orderDetailId,
                OrderToppingId = orderToppingId,
                IngredientId = null,
                PreparedItemId = preparedItem.PreparedItemId,
                BaseUnitId = preparedItem.BaseUnitId
            });
        }

        private async Task<Order?> LoadOrderForUpdateAsync(int orderId)
        {
            if (_context.Database.IsSqlServer())
            {
                return await _context.Orders
                    .FromSqlInterpolated(
                        $@"SELECT * FROM Orders WITH (UPDLOCK, ROWLOCK, HOLDLOCK)
                           WHERE OrderId = {orderId}")
                    .SingleOrDefaultAsync();
            }

            return await _context.Orders
                .SingleOrDefaultAsync(o => o.OrderId == orderId);
        }

        private async Task<StoreInventory?> LoadInventoryForUpdateAsync(int storeInventoryId)
        {
            if (_context.Database.IsSqlServer())
            {
                return await _context.StoreInventories
                    .FromSqlInterpolated(
                        $@"SELECT * FROM StoreInventories WITH (UPDLOCK, ROWLOCK, HOLDLOCK)
                           WHERE StoreInventoryId = {storeInventoryId}")
                    .SingleOrDefaultAsync();
            }

            return await _context.StoreInventories
                .SingleOrDefaultAsync(x => x.StoreInventoryId == storeInventoryId);
        }

        private async Task EvaluateStockAlertsSafeAsync(int storeId, int? referenceOrderId)
        {
            if (_stockAlertService == null)
                return;

            try
            {
                var alertResult = await _stockAlertService.EvaluateStoreAsync(storeId, StockAlertSources.PosSale);
                if (!alertResult.IsSuccess)
                {
                    _logger.LogWarning(
                        "[InventoryDeduction] Stock alert evaluation failed for StoreId={StoreId}: {Message}",
                        storeId,
                        alertResult.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "[InventoryDeduction] Stock alert evaluation threw for StoreId={StoreId}",
                    storeId);
            }
        }

        /// <summary>
        /// KNOWN LIMITATION (#121 / ADR-0009): parent sale recipe is Active at deduction time,
        /// not a persisted sale-time BOM snapshot. After selection, ChildRecipeId is exact.
        /// </summary>
        private async Task<Recipe?> GetActiveRecipeAsync(int? drinkId, int? sizeId, int? toppingId)
        {
            var query = _context.Recipes
                .Include(r => r.RecipeDetails)
                .Where(r => r.Active && r.Status == "Active");

            if (drinkId.HasValue)
            {
                var sizedRecipe = await query
                    .FirstOrDefaultAsync(r => r.DrinkId == drinkId.Value
                                           && r.SizeId == sizeId
                                           && r.ToppingId == null);

                if (sizedRecipe != null)
                    return sizedRecipe;

                return await query
                    .FirstOrDefaultAsync(r => r.DrinkId == drinkId.Value
                                           && r.SizeId == null
                                           && r.ToppingId == null);
            }

            if (toppingId.HasValue)
            {
                return await query
                    .FirstOrDefaultAsync(r => r.ToppingId == toppingId.Value
                                           && r.DrinkId == null);
            }

            return null;
        }

        private async Task<bool> SoldItemsContainBtpAsync(IEnumerable<POSSoldItemDto> soldItems)
        {
            foreach (var item in soldItems)
            {
                var drinkRecipe = await GetActiveRecipeAsync(item.DrinkId, item.SizeId, null);
                if (drinkRecipe?.RecipeDetails.Any(x => x.ChildRecipeId.HasValue) == true)
                    return true;

                foreach (var topping in item.Toppings ?? new List<POSOrderToppingDto>())
                {
                    var toppingRecipe = await GetActiveRecipeAsync(null, null, topping.ToppingId);
                    if (toppingRecipe?.RecipeDetails.Any(x => x.ChildRecipeId.HasValue) == true)
                        return true;
                }
            }

            return false;
        }

        private async Task<StoreInventory> GetOrCreateIngredientInventoryAsync(int storeId, int ingredientId)
        {
            var item = await _context.StoreInventories
                .FirstOrDefaultAsync(i => i.StoreId == storeId && i.IngredientId == ingredientId);

            if (item != null)
                return item;

            item = new StoreInventory
            {
                StoreId = storeId,
                IngredientId = ingredientId,
                RecipeId = null,
                PreparedItemId = null,
                AvailableQty = 0,
                ReservedQty = 0,
                LastUpdated = DateTime.UtcNow
            };
            _context.StoreInventories.Add(item);
            await _context.SaveChangesAsync();
            return item;
        }

        private async Task<StoreInventory> GetOrCreateLegacyRecipeInventoryAsync(int storeId, int recipeId)
        {
            var item = await _context.StoreInventories
                .FirstOrDefaultAsync(i =>
                    i.StoreId == storeId
                    && i.RecipeId == recipeId
                    && i.IngredientId == null);

            if (item != null)
                return item;

            item = new StoreInventory
            {
                StoreId = storeId,
                IngredientId = null,
                RecipeId = recipeId,
                AvailableQty = 0,
                ReservedQty = 0,
                LastUpdated = DateTime.UtcNow
            };
            _context.StoreInventories.Add(item);
            await _context.SaveChangesAsync();
            return item;
        }

        private sealed class RequirementLine
        {
            public int StoreInventoryId { get; init; }
            public decimal RequiredQty { get; init; }
            public int SourceRecipeId { get; init; }
            public string DisplayName { get; init; } = string.Empty;
            public int? OrderDetailId { get; init; }
            public int? OrderToppingId { get; init; }
            public int? IngredientId { get; init; }
            public int? PreparedItemId { get; init; }
            public int? BaseUnitId { get; init; }
        }
    }
}
