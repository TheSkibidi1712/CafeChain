using CafeChain.Application.Interfaces.POS;
using CafeChain.Hubs;
using CafeChain.Models.Orders;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace CafeChain.Application.Services.POS
{
    /// <summary>
    /// ADR-0003: PrintDispatcher — build ESC/POS receipt rồi gửi qua SignalR đến Print Bridge Worker.
    /// 
    /// Architecture flow:
    ///   CommitOrder → PrintDispatcher.DispatchPrintJobAsync()
    ///     → IEscPosBuilder.BuildReceipt() → byte[]
    ///     → IHubContext<PrintBridgeHub>.SendAsync("PrintJob", payload) → group "PrintBridge_Store_{storeId}"
    ///     → Print Bridge Worker (.NET Worker) nhận event → TCP:9100 → Máy in
    /// 
    /// Fire-and-forget: Print failure KHÔNG block order commit.
    /// Tất cả lỗi được catch + log, không propagate lên caller.
    /// </summary>
    public class PrintDispatcher : IPrintDispatcher
    {
        private readonly IEscPosBuilder _escPosBuilder;
        private readonly IHubContext<PrintBridgeHub> _hubContext;
        private readonly ILogger<PrintDispatcher> _logger;

        public PrintDispatcher(
            IEscPosBuilder escPosBuilder,
            IHubContext<PrintBridgeHub> hubContext,
            ILogger<PrintDispatcher> logger)
        {
            _escPosBuilder = escPosBuilder;
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task<bool> DispatchPrintJobAsync(
            Order order,
            int storeId,
            string cashierName,
            decimal cashReceived,
            bool isCashPayment)
        {
            try
            {
                // 1. Build ESC/POS receipt bytes
                var storeName = order.Store?.Name ?? "CafeChain";
                var escPosPayload = _escPosBuilder.BuildReceipt(order, storeName, cashierName, cashReceived, isCashPayment);

                if (escPosPayload == null || escPosPayload.Length == 0)
                {
                    _logger.LogWarning(
                        "[PrintDispatcher] ESC/POS payload trống cho Order #{OrderId}. Skip print.",
                        order.OrderId);
                    return false;
                }

                // 2. Gửi qua SignalR đến Print Bridge Worker group
                //    Group name pattern: "PrintBridge_Store_{storeId}" (khớp với PrintBridgeHub.JoinPrintGroup)
                var groupName = $"PrintBridge_Store_{storeId}";

                await _hubContext.Clients.Group(groupName).SendAsync("PrintJob", new
                {
                    orderId = order.OrderId,
                    storeId,
                    payload = escPosPayload,           // byte[] — SignalR serialize thành Base64 JSON
                    isCashPayment,
                    printedAt = DateTime.UtcNow
                });

                _logger.LogInformation(
                    "[PrintDispatcher] Đã gửi print job cho Order #{OrderId} → group {Group} ({ByteCount} bytes)",
                    order.OrderId, groupName, escPosPayload.Length);

                return true;
            }
            catch (Exception ex)
            {
                // Fire-and-forget — log lỗi nhưng KHÔNG throw
                // Order đã commit thành công, lỗi in không được phá hủy giao dịch
                _logger.LogError(ex,
                    "[PrintDispatcher] Lỗi gửi print job cho Order #{OrderId}. Print sẽ cần retry thủ công.",
                    order.OrderId);
                return false;
            }
        }
    }
}
