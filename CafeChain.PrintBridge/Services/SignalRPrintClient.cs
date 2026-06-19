using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CafeChain.PrintBridge.Services
{
    /// <summary>
    /// SignalR client kết nối PrintBridgeHub trên Cloud Backend.
    /// 
    /// Lifecycle:
    ///   1. Build HubConnection với X-PrintBridge-Key header
    ///   2. Register handler cho event "PrintJob"
    ///   3. Connect → JoinPrintGroup(storeId)
    ///   4. Heartbeat do Worker.cs điều khiển từ bên ngoài (không dùng Timer nội bộ)
    ///   5. Auto-reconnect khi mất kết nối
    /// 
    /// PrintJob event payload (JSON):
    ///   {
    ///     "orderId": 123,
    ///     "storeId": 1,
    ///     "payload": "base64-encoded-bytes",
    ///     "isCashPayment": true,
    ///     "printerTarget": "Cashier",
    ///     "printedAt": "2026-06-10T..."
    ///   }
    /// </summary>
    public class SignalRPrintClient
    {
        private readonly PrintBridgeOptions _options;
        private readonly TcpPrinterForwarder _tcpForwarder;
        private readonly ILogger<SignalRPrintClient> _logger;
        private HubConnection? _connection;

        public SignalRPrintClient(
            IOptions<PrintBridgeOptions> options,
            TcpPrinterForwarder tcpForwarder,
            ILogger<SignalRPrintClient> logger)
        {
            _options = options.Value;
            _tcpForwarder = tcpForwarder;
            _logger = logger;
        }

        /// <summary>
        /// Trạng thái kết nối hiện tại — Worker dùng để kiểm tra trước khi gửi heartbeat.
        /// </summary>
        public bool IsConnected => _connection?.State == HubConnectionState.Connected;

        /// <summary>
        /// Khởi tạo kết nối SignalR, đăng ký event handler, connect, join group.
        /// Gọi 1 lần duy nhất khi Worker start.
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _connection = new HubConnectionBuilder()
                .WithUrl(_options.HubUrl, options =>
                {
                    // Đính kèm API Key lên header mỗi request
                    options.Headers.Add("X-PrintBridge-Key", _options.ApiKey);
                })
                .WithAutomaticReconnect(new[] {
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(10),
                    TimeSpan.FromSeconds(30)
                })
                .Build();

            // ── Event Handlers ──

            _connection.On<JsonElement>("PrintJob", (jobData) =>
            {
                // Fire-and-forget an toàn — toàn bộ logic nằm trong try-catch,
                // không có đường nào throw ra ngoài gây crash
                _ = HandlePrintJobSafeAsync(jobData);
            });

            _connection.On<string>("AuthError", (message) =>
            {
                _logger.LogError("[SignalR] ❌ Auth Error từ Hub: {Message}", message);
            });

            _connection.Reconnecting += (error) =>
            {
                _logger.LogWarning("[SignalR] ⚠️ Mất kết nối, đang reconnect... Error: {Error}",
                    error?.Message ?? "none");
                return Task.CompletedTask;
            };

            _connection.Reconnected += async (connectionId) =>
            {
                _logger.LogInformation("[SignalR] ✅ Reconnected! ConnectionId={ConnectionId}", connectionId);
                // Re-join group sau khi reconnect (SignalR không tự nhớ group membership)
                try
                {
                    await _connection.InvokeAsync("JoinPrintGroup", _options.StoreId, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[SignalR] ❌ Re-join group thất bại sau reconnect");
                }
            };

            _connection.Closed += (error) =>
            {
                _logger.LogWarning("[SignalR] 🔴 Connection closed. Error: {Error}",
                    error?.Message ?? "none");
                return Task.CompletedTask;
            };

            // ── Connect ──
            await ConnectWithRetryAsync(cancellationToken);
        }

        /// <summary>
        /// Gửi heartbeat "online" lên Hub.
        /// Worker gọi method này từ vòng lặp chính mỗi 30s.
        /// </summary>
        public async Task SendHeartbeatAsync(CancellationToken cancellationToken)
        {
            if (_connection?.State != HubConnectionState.Connected)
                return;

            await _connection.InvokeAsync("ReportPrinterStatus", _options.StoreId, true, cancellationToken);
            _logger.LogDebug("[Heartbeat] 💚 ReportPrinterStatus(Store_{StoreId}, online)", _options.StoreId);
        }

        /// <summary>
        /// Dừng và ngắt kết nối SignalR.
        /// </summary>
        public async Task StopAsync()
        {
            if (_connection != null)
            {
                // Báo offline trước khi disconnect
                try
                {
                    if (_connection.State == HubConnectionState.Connected)
                    {
                        await _connection.InvokeAsync("ReportPrinterStatus", _options.StoreId, false);
                    }
                }
                catch { /* Best-effort */ }

                await _connection.DisposeAsync();
                _connection = null;
            }
        }

        // ═══════════════════════════════════════════════════════════
        // PRIVATE: Connect Logic
        // ═══════════════════════════════════════════════════════════

        private async Task ConnectWithRetryAsync(CancellationToken cancellationToken)
        {
            // Retry connect loop — WithAutomaticReconnect chỉ hoạt động SAU khi đã connect lần đầu
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("[SignalR] Đang kết nối tới {HubUrl}...", _options.HubUrl);
                    await _connection!.StartAsync(cancellationToken);

                    _logger.LogInformation("[SignalR] ✅ Kết nối thành công! Joining group Store_{StoreId}...",
                        _options.StoreId);
                    await _connection.InvokeAsync("JoinPrintGroup", _options.StoreId, cancellationToken);

                    _logger.LogInformation(
                        "[SignalR] ✅ Đã join group. Target={PrinterTarget}, Printer={PrinterIp}:{PrinterPort}",
                        _options.PrinterTarget, _options.PrinterIp, _options.PrinterPort);

                    break; // Kết nối thành công, thoát retry loop
                }
                catch (OperationCanceledException)
                {
                    throw; // Shutdown — propagate lên Worker
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[SignalR] ❌ Kết nối thất bại. Retry sau 5s...");
                    await Task.Delay(5000, cancellationToken);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════
        // PRIVATE: Handle PrintJob Event — FAULT-TOLERANT
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Wrapper an toàn — mọi exception đều được catch + log.
        /// Không bao giờ throw ra ngoài → không crash app.
        /// </summary>
        private async Task HandlePrintJobSafeAsync(JsonElement jobData)
        {
            try
            {
                // Parse fields từ JSON payload
                int orderId = jobData.GetProperty("orderId").GetInt32();
                string printerTarget = jobData.GetProperty("printerTarget").GetString() ?? "Cashier";
                byte[] payload = jobData.GetProperty("payload").GetBytesFromBase64();

                _logger.LogInformation(
                    "[PrintJob] 📄 Nhận lệnh in Order #{OrderId} → Target={Target}, Size={ByteCount} bytes",
                    orderId, printerTarget, payload.Length);

                // ── FILTER: So khớp PrinterTarget ──
                if (!string.Equals(printerTarget, _options.PrinterTarget, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug(
                        "[PrintJob] ⏭️ Skip Order #{OrderId} — Target={Target} ≠ MyTarget={MyTarget}",
                        orderId, printerTarget, _options.PrinterTarget);
                    return;
                }

                // ── FORWARD: Gửi bytes sang máy in qua TCP ──
                bool success = await _tcpForwarder.ForwardAsync(payload, orderId);

                if (success)
                {
                    _logger.LogInformation("[PrintJob] ✅ Order #{OrderId} đã in thành công!", orderId);
                }
                else
                {
                    _logger.LogError("[PrintJob] ❌ Order #{OrderId} in thất bại sau tất cả retries.", orderId);
                }
            }
            catch (Exception ex)
            {
                // Catch-all: JSON parse lỗi, Base64 decode lỗi, bất kỳ exception nào
                // → log rồi tiếp tục, KHÔNG crash Worker
                _logger.LogError(ex, "[PrintJob] ❌ Lỗi xử lý PrintJob event. Worker vẫn tiếp tục chạy.");
            }
        }
    }
}
