using CafeChain.Application.Interfaces.POS;
using CafeChain.Hubs;
using CafeChain.Models.Orders;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
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
        private readonly IConfiguration _configuration;
        private readonly ILogger<PrintDispatcher> _logger;

        public PrintDispatcher(
            IEscPosBuilder escPosBuilder,
            IHubContext<PrintBridgeHub> hubContext,
            IConfiguration configuration,
            ILogger<PrintDispatcher> logger)
        {
            _escPosBuilder = escPosBuilder;
            _hubContext = hubContext;
            _configuration = configuration;
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
                var storeName = order.Store?.Name ?? "CafeChain";

                // 1. Build + dispatch receipt cho thu ngân.
                var receiptPayload = _escPosBuilder.BuildReceipt(order, storeName, cashierName, cashReceived, isCashPayment);
                var receiptSent = await SendPrintJobAsync(
                    order,
                    storeId,
                    receiptPayload,
                    printerTarget: "Cashier",
                    jobType: "Receipt",
                    isCashPayment: isCashPayment);

                // 2. Build + dispatch drink label cho khu vực pha chế.
                // Default target = Cashier để demo/test dùng ngay với 1 PrintBridge.
                // Khi có máy in tem/bar riêng, cấu hình PrintBridge:CupLabelPrinterTarget = "Bar".
                if (IsCupLabelPrintingEnabled())
                {
                    var cupLabelPayload = _escPosBuilder.BuildCupLabels(order, storeName, cashierName);
                    var cupLabelTarget = GetCupLabelPrinterTarget();

                    await SendPrintJobAsync(
                        order,
                        storeId,
                        cupLabelPayload,
                        printerTarget: cupLabelTarget,
                        jobType: "DrinkLabel",
                        isCashPayment: false);
                }

                return receiptSent;
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

        private async Task<bool> SendPrintJobAsync(
            Order order,
            int storeId,
            byte[] payload,
            string printerTarget,
            string jobType,
            bool isCashPayment)
        {
            if (payload == null || payload.Length == 0)
            {
                _logger.LogWarning(
                    "[PrintDispatcher] {JobType} payload trống cho Order #{OrderId}. Skip print.",
                    jobType,
                    order.OrderId);
                return false;
            }

            // Group name pattern: "PrintBridge_Store_{storeId}" (khớp với PrintBridgeHub.JoinPrintGroup)
            var groupName = $"PrintBridge_Store_{storeId}";

            await _hubContext.Clients.Group(groupName).SendAsync("PrintJob", new
            {
                orderId = order.OrderId,
                storeId,
                payload,                     // byte[] — SignalR serialize thành Base64 JSON
                isCashPayment,
                printerTarget,
                jobType,
                printedAt = DateTime.UtcNow
            });

            _logger.LogInformation(
                "[PrintDispatcher] Đã gửi {JobType} print job cho Order #{OrderId} → {Target} / {Group} ({ByteCount} bytes)",
                jobType,
                order.OrderId,
                printerTarget,
                groupName,
                payload.Length);

            return true;
        }

        private bool IsCupLabelPrintingEnabled()
        {
            var raw = _configuration["PrintBridge:EnableCupLabels"];
            return !bool.TryParse(raw, out var enabled) || enabled;
        }

        private string GetCupLabelPrinterTarget()
        {
            var configuredTarget = _configuration["PrintBridge:CupLabelPrinterTarget"];
            return string.IsNullOrWhiteSpace(configuredTarget) ? "Cashier" : configuredTarget.Trim();
        }
    }
}
