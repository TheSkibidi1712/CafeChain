using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Inventory;
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

        public InventoryDeductionService(
            AppDbContext context,
            ILogger<InventoryDeductionService> logger,
            IUnitConversionService unitConversion,
            IEstimatedBomCostService estimatedBomCost,
            IPhysicalUnitConversionService physicalConversion,
            IStockAlertService? stockAlertService = null,
            IInventoryWriterModeService? writerModeService = null,
            IStoreInventoryWriteResolver? writeResolver = null)
        {
            _context = context;
            _logger = logger;
            _unitConversion = unitConversion;
            _estimatedBomCost = estimatedBomCost;
            _physicalConversion = physicalConversion;
            _stockAlertService = stockAlertService;
            _writerModeService = writerModeService;
            _writeResolver = writeResolver;
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
            return await DeductStockForOrderInternalAsync(soldItems, storeId, null);
        }

        public async Task<ServiceResult> DeductStockForCommittedOrderAsync(
            List<POSSoldItemDto> soldItems,
            int storeId,
            int referenceOrderId)
        {
            if (referenceOrderId <= 0)
                return ServiceResult.Failure("Thiếu mã đơn hàng đã commit để trừ kho.");

            return await DeductStockForOrderInternalAsync(soldItems, storeId, referenceOrderId);
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
                if (referenceOrderId.HasValue)
                {
                    var order = await LoadOrderForUpdateAsync(referenceOrderId.Value);
                    if (order == null
                        || order.StoreId != storeId
                        || order.OrderStatusId != SystemConstants.OrderStatuses.Completed
                        || order.PaymentStatusId != SystemConstants.PaymentStatuses.Paid)
                    {
                        await transaction.RollbackAsync();
                        return ServiceResult.Failure("Chỉ trừ kho cho đơn POS đã thanh toán và đã commit.");
                    }

                    // Idempotency AFTER order lock
                    var alreadyDeducted = await _context.InventoryTransactions
                        .AsNoTracking()
                        .AnyAsync(t =>
                            t.ReferenceOrderId == referenceOrderId.Value
                            && t.Type == InventoryTransactionTypeEnum.SALES_DEDUCTION);

                    if (alreadyDeducted)
                    {
                        await transaction.CommitAsync();
                        return ServiceResult.Success("Đơn hàng đã được trừ kho trước đó.");
                    }
                }

                InventoryWriterModeSnapshot? modeSnapshot = null;
                InventoryWriterMode mode = InventoryWriterMode.LegacyRecipe;

                if (_writerModeService != null)
                {
                    // Always acquire when writer service is present so mode is consistent for BTP + ingredient-only Blocked.
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

                // Collect all requirements without mutating.
                var requirements = new List<RequirementLine>();
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
                            requirements);
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
                            requirements);
                    }
                }

                // Mutate once per StoreInventoryId (sum all paths).
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

                // Ledger: group by (StoreInventoryId, SourceRecipeId) so exact ChildRecipe audit is durable
                // even when multiple children resolve to the same PreparedItem row.
                var ledgerGroups = requirements
                    .GroupBy(r => new { r.StoreInventoryId, r.SourceRecipeId })
                    .Select(g => new
                    {
                        g.Key.StoreInventoryId,
                        g.Key.SourceRecipeId,
                        RequiredQty = g.Sum(x => x.RequiredQty),
                        DisplayName = g.First().DisplayName
                    })
                    .OrderBy(x => x.StoreInventoryId)
                    .ThenBy(x => x.SourceRecipeId)
                    .ToList();

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
                    // Blind Selling: AvailableQty -= Required; ReservedQty unchanged.
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

                // Sequential Before/After per inventory for multi-source ledger slices.
                var runningQty = new Dictionary<int, decimal>(preMutationQty);
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

                    _context.InventoryTransactions.Add(new InventoryTransaction
                    {
                        StoreInventoryId = ledger.StoreInventoryId,
                        Type = InventoryTransactionTypeEnum.SALES_DEDUCTION,
                        StockStatus = afterQty < 0
                            ? InventoryStockStatus.NEGATIVE_CONFIRMED
                            : InventoryStockStatus.NORMAL,
                        Quantity = ledger.RequiredQty,
                        BeforeQty = beforeQty,
                        AfterQty = afterQty,
                        ReferenceOrderId = referenceOrderId,
                        SourceRecipeId = ledger.SourceRecipeId,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await EvaluateStockAlertsSafeAsync(storeId, referenceOrderId);

                if (inventoryWarnings.Any())
                {
                    var result = ServiceResult.Success(
                        $"Trừ kho thành công. Cảnh báo: {inventoryWarnings.Count} nguyên liệu tồn kho âm.");
                    result.Errors = inventoryWarnings;
                    return result;
                }

                return ServiceResult.Success("Trừ kho bán hàng thành công.");
            }
            catch (DbUpdateConcurrencyException ex)
            {
                try { await transaction.RollbackAsync(); } catch { /* ignore */ }
                // Clear only after concurrency — avoids detaching caller-tracked seed entities on soft failures.
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

        private async Task CollectRequirementsAsync(
            Recipe saleRecipe,
            int soldQuantity,
            int storeId,
            InventoryWriterMode mode,
            InventoryWriterModeSnapshot? modeSnapshot,
            List<RequirementLine> requirements)
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
                    var name = await _context.Ingredients.AsNoTracking()
                        .Where(i => i.IngredientId == detail.IngredientId.Value)
                        .Select(i => i.Name)
                        .FirstOrDefaultAsync() ?? $"Ingredient #{detail.IngredientId}";

                    requirements.Add(new RequirementLine
                    {
                        StoreInventoryId = inv.StoreInventoryId,
                        RequiredQty = converted.Data,
                        SourceRecipeId = saleRecipe.RecipeId,
                        DisplayName = name
                    });
                    continue;
                }

                if (!detail.ChildRecipeId.HasValue)
                {
                    throw new InvalidOperationException(
                        $"RecipeDetail #{detail.RecipeDetailId} phải có IngredientId hoặc ChildRecipeId.");
                }

                // ----- BTP path -----
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
                        requirements);
                }
                else
                {
                    // LegacyRecipe: RecipeId = exact ChildRecipeId, raw qty (no physical convert).
                    var inv = await GetOrCreateLegacyRecipeInventoryAsync(storeId, detail.ChildRecipeId.Value);
                    requirements.Add(new RequirementLine
                    {
                        StoreInventoryId = inv.StoreInventoryId,
                        RequiredQty = rawRequired,
                        SourceRecipeId = detail.ChildRecipeId.Value,
                        DisplayName = $"Recipe #{detail.ChildRecipeId}"
                    });
                }
            }
        }

        private async Task CollectPreparedBtpRequirementAsync(
            RecipeDetail detail,
            decimal rawRequired,
            int storeId,
            InventoryWriterModeSnapshot modeSnapshot,
            List<RequirementLine> requirements)
        {
            // Exact ChildRecipe by id — no Active filter / no latest substitute.
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

            // Output contract validation (mapping validity) — do NOT multiply by OutputQuantity/Yield.
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
                DisplayName = preparedItem.Name
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
        }

    }
}
