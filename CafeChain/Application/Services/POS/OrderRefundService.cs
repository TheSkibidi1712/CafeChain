using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Costing;
using CafeChain.Models.Inventories.Refunds;
using CafeChain.Models.Inventories.Transactions;
using CafeChain.Models.Orders;
using CafeChain.Models.Payments;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CafeChain.Application.Services.POS
{
    /// <summary>
    /// Full-order cash refund (#134): restore qty from SALES_DEDUCTION evidence;
    /// compensating cost layers from SalesCostAllocation; Order stays Completed.
    /// </summary>
    public sealed class OrderRefundService : IOrderRefundService
    {
        public const int CashPaymentMethodId = 1;
        public const int PayOsPaymentMethodId = 2;

        private readonly AppDbContext _context;
        private readonly ILogger<OrderRefundService> _logger;

        public OrderRefundService(AppDbContext context, ILogger<OrderRefundService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ServiceResult<OrderRefundResultDto>> RequestFullRefundAsync(
            RequestFullOrderRefundDto dto,
            int staffId,
            int staffHomeStoreId,
            IReadOnlyList<string> roleNames)
        {
            if (dto == null || dto.OrderId <= 0)
                return Fail(OrderRefundFailureCodes.InvalidRequest, "Yêu cầu hoàn đơn không hợp lệ.");
            if (staffId <= 0 || staffHomeStoreId <= 0)
                return Fail(OrderRefundFailureCodes.StoreUnauthorized, "Thiếu thông tin nhân viên/cửa hàng.");
            if (!CanRequest(roleNames))
                return Fail(OrderRefundFailureCodes.RoleUnauthorized, "Bạn không có quyền yêu cầu hoàn đơn.");

            if (!dto.RefundKey.HasValue || dto.RefundKey.Value == Guid.Empty)
                return Fail(OrderRefundFailureCodes.InvalidRequest, "RefundKey (GUID) là bắt buộc.");

            var reason = (dto.Reason ?? string.Empty).Trim();
            if (reason.Length < 3)
                return Fail(OrderRefundFailureCodes.InvalidRequest, "Lý do hoàn đơn là bắt buộc.");
            if (reason.Length > 500)
                return Fail(OrderRefundFailureCodes.InvalidRequest, "Lý do tối đa 500 ký tự.");

            var order = await _context.Orders
                .AsNoTracking()
                .Include(o => o.Payments)
                .Include(o => o.OrderVouchers)
                .FirstOrDefaultAsync(o => o.OrderId == dto.OrderId);

            if (order == null)
                return Fail(OrderRefundFailureCodes.OrderNotFound, "Không tìm thấy đơn hàng.");

            if (!CanAccessStore(order.StoreId, staffHomeStoreId, roleNames))
                return Fail(OrderRefundFailureCodes.StoreUnauthorized, "Không có quyền hoàn đơn cửa hàng này.");

            var gate = ValidateRefundableOrder(order, expectedAmount: null);
            if (!gate.IsSuccess)
                return Fail(gate.ErrorCode!, gate.Message);

            var existingKey = await _context.OrderRefunds.AsNoTracking()
                .FirstOrDefaultAsync(r => r.StoreId == order.StoreId && r.RefundKey == dto.RefundKey.Value);
            if (existingKey != null)
            {
                if (existingKey.OrderId != order.OrderId
                    || !string.Equals(existingKey.Reason, reason, StringComparison.Ordinal)
                    || existingKey.RefundAmount != order.Total)
                {
                    return Fail(OrderRefundFailureCodes.RefundKeyReused, "RefundKey đã dùng với payload khác.");
                }

                return ServiceResult<OrderRefundResultDto>.Success(
                    ToDto(existingKey, wasReplay: true),
                    "Yêu cầu hoàn đơn đã tồn tại (replay).");
            }

            var active = await _context.OrderRefunds.AsNoTracking()
                .AnyAsync(r => r.OrderId == order.OrderId
                               && (r.Status == OrderRefundStatus.Requested
                                   || r.Status == OrderRefundStatus.Processing
                                   || r.Status == OrderRefundStatus.Completed));
            if (active)
                return Fail(OrderRefundFailureCodes.RefundActive, "Đơn đã có yêu cầu/hoàn tiền full.");

            var refund = new OrderRefund
            {
                OrderId = order.OrderId,
                StoreId = order.StoreId,
                RefundKey = dto.RefundKey.Value,
                Status = OrderRefundStatus.Requested,
                PaymentMethodId = CashPaymentMethodId,
                Reason = reason,
                RefundAmount = order.Total,
                CostStatus = SalesCostStatus.Pending,
                InventoryReversalStatus = RefundInventoryReversalStatus.Pending,
                RequestedAtUtc = DateTime.UtcNow,
                RequestedByStaffId = staffId
            };

            _context.OrderRefunds.Add(refund);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                _context.ChangeTracker.Clear();
                var raced = await _context.OrderRefunds.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.StoreId == order.StoreId && r.RefundKey == dto.RefundKey.Value);
                if (raced != null)
                    return ServiceResult<OrderRefundResultDto>.Success(ToDto(raced, true), "Yêu cầu hoàn đơn đã tồn tại (replay).");
                return Fail(OrderRefundFailureCodes.ConcurrencyConflict, "Xung đột khi tạo yêu cầu hoàn đơn.");
            }

            return ServiceResult<OrderRefundResultDto>.Success(
                ToDto(refund, false),
                "Đã tạo yêu cầu hoàn toàn bộ đơn (chưa trừ kho/thanh toán).");
        }

        public async Task<ServiceResult<OrderRefundResultDto>> ConfirmCashRefundAsync(
            ConfirmCashRefundDto dto,
            int staffId,
            int staffHomeStoreId,
            IReadOnlyList<string> roleNames)
        {
            if (dto == null || dto.OrderRefundId <= 0)
                return Fail(OrderRefundFailureCodes.InvalidRequest, "OrderRefundId không hợp lệ.");
            if (!dto.CashReturnedToCustomer)
                return Fail(OrderRefundFailureCodes.CashConfirmRequired, "Phải xác nhận đã hoàn tiền mặt cho khách.");
            if (staffId <= 0 || staffHomeStoreId <= 0)
                return Fail(OrderRefundFailureCodes.StoreUnauthorized, "Thiếu thông tin nhân viên/cửa hàng.");
            if (!CanConfirm(roleNames))
                return Fail(OrderRefundFailureCodes.RoleUnauthorized, "Bạn không có quyền xác nhận hoàn tiền mặt.");

            for (var attempt = 0; attempt < 3; attempt++)
            {
                var result = await ConfirmOnceAsync(dto, staffId, staffHomeStoreId, roleNames);
                if (result.IsSuccess
                    || result.ErrorCode != OrderRefundFailureCodes.ConcurrencyConflict
                    || attempt == 2)
                    return result;

                _context.ChangeTracker.Clear();
                _logger.LogWarning(
                    "[OrderRefund] Retry concurrency OrderRefundId={Id} Attempt={Attempt}",
                    dto.OrderRefundId,
                    attempt + 1);
            }

            return Fail(OrderRefundFailureCodes.ConcurrencyConflict, "Xung đột đồng thời. Vui lòng thử lại.");
        }

        private async Task<ServiceResult<OrderRefundResultDto>> ConfirmOnceAsync(
            ConfirmCashRefundDto dto,
            int staffId,
            int staffHomeStoreId,
            IReadOnlyList<string> roleNames)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var refund = await LoadRefundForUpdateAsync(dto.OrderRefundId);
                if (refund == null)
                {
                    await transaction.RollbackAsync();
                    return Fail(OrderRefundFailureCodes.RefundNotFound, "Không tìm thấy yêu cầu hoàn đơn.");
                }

                if (refund.Status == OrderRefundStatus.Completed)
                {
                    await transaction.CommitAsync();
                    return ServiceResult<OrderRefundResultDto>.Success(
                        ToDto(refund, wasReplay: true),
                        "Hoàn đơn đã được thực hiện trước đó (replay).");
                }

                if (refund.Status is not (OrderRefundStatus.Requested or OrderRefundStatus.Processing))
                {
                    await transaction.RollbackAsync();
                    return Fail(OrderRefundFailureCodes.InvalidRequest, "Trạng thái refund không cho phép confirm.");
                }

                if (!CanAccessStore(refund.StoreId, staffHomeStoreId, roleNames))
                {
                    await transaction.RollbackAsync();
                    return Fail(OrderRefundFailureCodes.StoreUnauthorized, "Không có quyền hoàn đơn cửa hàng này.");
                }

                var order = await LoadOrderForUpdateAsync(refund.OrderId);
                if (order == null)
                {
                    await transaction.RollbackAsync();
                    return Fail(OrderRefundFailureCodes.OrderNotFound, "Không tìm thấy đơn hàng.");
                }

                var payments = await _context.Payments
                    .Where(p => p.OrderId == order.OrderId)
                    .ToListAsync();

                var hasVoucher = await _context.Set<CafeChain.Models.Vouchers.OrderVoucher>()
                    .AsNoTracking()
                    .AnyAsync(v => v.OrderId == order.OrderId);
                if (hasVoucher || order.VoucherDiscount > 0 || order.PointsUsed > 0 || order.PointDiscount > 0)
                {
                    await transaction.RollbackAsync();
                    return Fail(
                        OrderRefundFailureCodes.LoyaltyReversalNotSupported,
                        "Đơn có voucher/điểm — chưa hỗ trợ reverse trong #134.");
                }

                // Temporary attach payments for method validation
                order.Payments = payments;
                var gate = ValidateRefundableOrder(order, refund.RefundAmount);
                if (!gate.IsSuccess)
                {
                    await transaction.RollbackAsync();
                    return Fail(gate.ErrorCode!, gate.Message);
                }

                refund.Status = OrderRefundStatus.Processing;
                refund.ProcessingAtUtc = DateTime.UtcNow;

                var deductions = await _context.InventoryTransactions
                    .Include(t => t.StoreInventory)
                    .Where(t => t.ReferenceOrderId == order.OrderId
                                && t.Type == InventoryTransactionTypeEnum.SALES_DEDUCTION)
                    .OrderBy(t => t.StoreInventoryId)
                    .ThenBy(t => t.InventoryTransactionId)
                    .ToListAsync();

                var allocations = await _context.SalesCostAllocations
                    .Where(a => a.OrderId == order.OrderId)
                    .OrderBy(a => a.SalesCostAllocationId)
                    .ToListAsync();

                var gaps = await _context.SalesCostGaps
                    .Where(g => g.OrderId == order.OrderId)
                    .OrderBy(g => g.SalesCostGapId)
                    .ToListAsync();

                var now = DateTime.UtcNow;

                if (deductions.Count == 0)
                {
                    refund.InventoryReversalStatus = RefundInventoryReversalStatus.NoOriginalDeduction;
                    refund.CostStatus = SalesCostStatus.Complete;
                    refund.ReversedCogs = 0m;
                }
                else
                {
                    // Aggregate restore qty by StoreInventoryId from original sale movements.
                    var restorePlan = deductions
                        .GroupBy(t => t.StoreInventoryId)
                        .Select(g => new
                        {
                            StoreInventoryId = g.Key,
                            Qty = g.Sum(x => x.Quantity),
                            Sample = g.First()
                        })
                        .OrderBy(x => x.StoreInventoryId)
                        .ToList();

                    var lockedInv = new Dictionary<int, StoreInventory>();
                    foreach (var line in restorePlan)
                    {
                        var inv = await LoadInventoryForUpdateAsync(line.StoreInventoryId);
                        if (inv == null)
                            throw new InvalidOperationException($"Missing StoreInventory #{line.StoreInventoryId}.");

                        var tracked = _context.StoreInventories.Local
                            .FirstOrDefault(x => x.StoreInventoryId == inv.StoreInventoryId) ?? inv;
                        if (_context.Entry(tracked).State == EntityState.Detached)
                            _context.StoreInventories.Attach(tracked);

                        lockedInv[tracked.StoreInventoryId] = tracked;
                        var before = tracked.AvailableQty;
                        tracked.AvailableQty += line.Qty;
                        tracked.LastUpdated = now;

                        // Cost for return movement: weighted from allocations on this inventory if all covered
                        var invAllocs = allocations
                            .Where(a => deductions.Any(d =>
                                d.InventoryTransactionId == a.InventoryTransactionId
                                && d.StoreInventoryId == line.StoreInventoryId))
                            .ToList();
                        decimal? unit = null;
                        decimal? total = null;
                        var deductedQty = line.Qty;
                        var allocQty = invAllocs.Sum(a => a.Quantity);
                        if (invAllocs.Count > 0 && allocQty == deductedQty)
                        {
                            total = invAllocs.Sum(a => a.TotalCost);
                            unit = deductedQty > 0 ? total / deductedQty : null;
                        }

                        _context.InventoryTransactions.Add(new InventoryTransaction
                        {
                            StoreInventoryId = tracked.StoreInventoryId,
                            Type = InventoryTransactionTypeEnum.SALES_RETURN,
                            StockStatus = InventoryStockStatus.NORMAL,
                            Quantity = line.Qty,
                            BeforeQty = before,
                            AfterQty = tracked.AvailableQty,
                            UnitCost = unit,
                            TotalCost = total,
                            ReferenceOrderId = order.OrderId,
                            OrderRefundId = refund.OrderRefundId,
                            SourceRecipeId = line.Sample.SourceRecipeId,
                            CreatedAt = now
                        });
                    }

                    await _context.SaveChangesAsync(); // need return tx ids for reversals

                    // Map StoreInventoryId -> SALES_RETURN tx for this refund
                    var returnTxByInv = await _context.InventoryTransactions
                        .Where(t => t.OrderRefundId == refund.OrderRefundId
                                    && t.Type == InventoryTransactionTypeEnum.SALES_RETURN)
                        .ToDictionaryAsync(t => t.StoreInventoryId, t => t);

                    foreach (var alloc in allocations.OrderBy(a => a.SalesCostAllocationId))
                    {
                        var saleTx = deductions.FirstOrDefault(d => d.InventoryTransactionId == alloc.InventoryTransactionId);
                        if (saleTx == null)
                            continue;

                        if (!returnTxByInv.TryGetValue(saleTx.StoreInventoryId, out var returnTx))
                            throw new InvalidOperationException("Missing SALES_RETURN for inventory row.");

                        var returnLayer = new InventoryCostLayer
                        {
                            StoreId = refund.StoreId,
                            IngredientId = alloc.IngredientId,
                            PreparedItemId = alloc.PreparedItemId,
                            Quantity = alloc.Quantity,
                            RemainingQuantity = alloc.Quantity,
                            UnitCost = alloc.UnitCost,
                            CreatedAt = now,
                            SourceOrderRefundId = refund.OrderRefundId
                        };
                        _context.InventoryCostLayers.Add(returnLayer);
                        await _context.SaveChangesAsync(); // need layer id

                        _context.RefundCostReversals.Add(new RefundCostReversal
                        {
                            OrderRefundId = refund.OrderRefundId,
                            SalesCostAllocationId = alloc.SalesCostAllocationId,
                            OriginalInventoryCostLayerId = alloc.InventoryCostLayerId,
                            ReturnInventoryCostLayerId = returnLayer.InventoryCostLayerId,
                            InventoryTransactionId = returnTx.InventoryTransactionId,
                            IngredientId = alloc.IngredientId,
                            PreparedItemId = alloc.PreparedItemId,
                            Quantity = alloc.Quantity,
                            UnitCost = alloc.UnitCost,
                            TotalCost = alloc.TotalCost,
                            CreatedAtUtc = now
                        });
                    }

                    foreach (var gap in gaps)
                    {
                        if (!gap.IngredientId.HasValue && !gap.PreparedItemId.HasValue)
                            continue;

                        _context.RefundCostGaps.Add(new RefundCostGap
                        {
                            OrderRefundId = refund.OrderRefundId,
                            SalesCostGapId = gap.SalesCostGapId,
                            IngredientId = gap.IngredientId,
                            PreparedItemId = gap.PreparedItemId,
                            Quantity = gap.MissingCostQuantity,
                            BaseUnitId = gap.BaseUnitId,
                            ReasonCode = gap.ReasonCode,
                            CreatedAtUtc = now
                        });
                    }

                    refund.InventoryReversalStatus = RefundInventoryReversalStatus.Completed;
                    if (gaps.Count > 0 || order.CostStatus == SalesCostStatus.Incomplete)
                    {
                        refund.CostStatus = SalesCostStatus.Incomplete;
                        // Known reversed only — not presented as full actual COGS
                        refund.ReversedCogs = allocations.Sum(a => a.TotalCost);
                    }
                    else
                    {
                        refund.CostStatus = SalesCostStatus.Complete;
                        refund.ReversedCogs = allocations.Sum(a => a.TotalCost);
                    }
                }

                // Mark all paid payments refunded
                foreach (var p in payments.Where(p => p.PaymentStatusId == SystemConstants.PaymentStatuses.Paid))
                    p.PaymentStatusId = SystemConstants.PaymentStatuses.Refunded;

                order.PaymentStatusId = SystemConstants.PaymentStatuses.Refunded;
                // Keep OrderStatus = Completed (D4)

                refund.Status = OrderRefundStatus.Completed;
                refund.CompletedAtUtc = now;
                refund.CompletedByStaffId = staffId;
                if (!string.IsNullOrWhiteSpace(dto.Reason))
                    refund.Reason = dto.Reason.Trim().Length > 500 ? dto.Reason.Trim()[..500] : dto.Reason.Trim();

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation(
                    "[OrderRefund] Completed OrderRefundId={RefundId} OrderId={OrderId} Amount={Amount}",
                    refund.OrderRefundId,
                    order.OrderId,
                    refund.RefundAmount);

                return ServiceResult<OrderRefundResultDto>.Success(
                    ToDto(refund, false),
                    "Đã hoàn toàn bộ đơn (tiền mặt) và đảo tồn/COGS khi có bằng chứng sale.");
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                try { await transaction.RollbackAsync(); } catch { /* ignore */ }
                _context.ChangeTracker.Clear();
                return Fail(OrderRefundFailureCodes.ConcurrencyConflict, "Xung đột đồng thời khi hoàn đơn.");
            }
            catch (Exception ex)
            {
                try { await transaction.RollbackAsync(); } catch { /* ignore */ }
                _context.ChangeTracker.Clear();
                _logger.LogError(ex, "[OrderRefund] Confirm failed OrderRefundId={Id}", dto.OrderRefundId);
                return Fail(OrderRefundFailureCodes.ExecutionFailed, "Không thể xác nhận hoàn đơn: " + ex.Message);
            }
        }

        private ServiceResult ValidateRefundableOrder(Order order, decimal? expectedAmount)
        {
            if (order.OrderStatusId != SystemConstants.OrderStatuses.Completed)
            {
                return ServiceResult.Failure(
                    "Chỉ hoàn đơn đã Completed.",
                    errorCode: OrderRefundFailureCodes.InvalidOrderStatus);
            }

            if (order.PaymentStatusId == SystemConstants.PaymentStatuses.Refunded)
            {
                return ServiceResult.Failure(
                    "Đơn đã được hoàn tiền.",
                    errorCode: OrderRefundFailureCodes.AlreadyRefunded);
            }

            if (order.PaymentStatusId != SystemConstants.PaymentStatuses.Paid)
            {
                return ServiceResult.Failure(
                    "Chỉ hoàn đơn đã Paid.",
                    errorCode: OrderRefundFailureCodes.InvalidPaymentStatus);
            }

            var payments = order.Payments?.ToList() ?? new List<Payment>();
            if (payments.Count == 0)
            {
                return ServiceResult.Failure(
                    "Đơn không có payment.",
                    errorCode: OrderRefundFailureCodes.InvalidPaymentStatus);
            }

            if (payments.Any(p => p.PaymentMethodId == PayOsPaymentMethodId)
                || payments.Any(p => p.PaymentMethodId != CashPaymentMethodId))
            {
                return ServiceResult.Failure(
                    "Hoàn PayOS/external chưa hỗ trợ trong #134.",
                    errorCode: OrderRefundFailureCodes.PaymentProviderNotSupported);
            }

            if (order.PointsUsed > 0
                || order.PointDiscount > 0
                || order.VoucherDiscount > 0
                || (order.OrderVouchers != null && order.OrderVouchers.Count > 0))
            {
                return ServiceResult.Failure(
                    "Đơn có voucher/điểm — chưa hỗ trợ reverse trong #134.",
                    errorCode: OrderRefundFailureCodes.LoyaltyReversalNotSupported);
            }

            if (expectedAmount.HasValue && expectedAmount.Value != order.Total)
            {
                return ServiceResult.Failure(
                    "Chỉ hỗ trợ hoàn full amount.",
                    errorCode: OrderRefundFailureCodes.PartialAmountRejected);
            }

            return ServiceResult.Success();
        }

        private static bool CanRequest(IReadOnlyList<string> roles)
        {
            return roles.Contains(RoleConstants.StoreManager)
                   || roles.Contains(RoleConstants.ShiftSupervisor)
                   || roles.Contains(RoleConstants.BusinessOwner)
                   || roles.Contains(RoleConstants.SystemAdmin);
        }

        private static bool CanConfirm(IReadOnlyList<string> roles)
        {
            // ShiftSupervisor may request; confirm requires SM/BO/SA (PIN policy not auto-granted)
            return roles.Contains(RoleConstants.StoreManager)
                   || roles.Contains(RoleConstants.BusinessOwner)
                   || roles.Contains(RoleConstants.SystemAdmin);
        }

        private static bool CanAccessStore(int orderStoreId, int staffHomeStoreId, IReadOnlyList<string> roles)
        {
            if (roles.Contains(RoleConstants.BusinessOwner) || roles.Contains(RoleConstants.SystemAdmin))
                return true;
            return orderStoreId == staffHomeStoreId;
        }

        private async Task<OrderRefund?> LoadRefundForUpdateAsync(int orderRefundId)
        {
            if (_context.Database.IsSqlServer())
            {
                return await _context.OrderRefunds
                    .FromSqlInterpolated(
                        $@"SELECT * FROM OrderRefunds WITH (UPDLOCK, ROWLOCK, HOLDLOCK)
                           WHERE OrderRefundId = {orderRefundId}")
                    .SingleOrDefaultAsync();
            }

            return await _context.OrderRefunds.SingleOrDefaultAsync(r => r.OrderRefundId == orderRefundId);
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

            return await _context.Orders.SingleOrDefaultAsync(o => o.OrderId == orderId);
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

            return await _context.StoreInventories.SingleOrDefaultAsync(x => x.StoreInventoryId == storeInventoryId);
        }

        private static OrderRefundResultDto ToDto(OrderRefund r, bool wasReplay) => new()
        {
            OrderRefundId = r.OrderRefundId,
            OrderId = r.OrderId,
            StoreId = r.StoreId,
            RefundKey = r.RefundKey,
            Status = r.Status.ToString(),
            RefundAmount = r.RefundAmount,
            CostStatus = r.CostStatus.ToString(),
            ReversedCogs = r.ReversedCogs,
            InventoryReversalStatus = r.InventoryReversalStatus.ToString(),
            WasReplay = wasReplay,
            CompletedAtUtc = r.CompletedAtUtc,
            MessageKey = wasReplay ? "OrderRefund.Replay" : "OrderRefund.Ok"
        };

        private static bool IsUniqueViolation(DbUpdateException ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            return msg.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
                   || msg.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
                   || msg.Contains("2601")
                   || msg.Contains("2627");
        }

        private static ServiceResult<OrderRefundResultDto> Fail(string code, string message)
            => ServiceResult<OrderRefundResultDto>.Failure(message, errorCode: code);
    }
}
