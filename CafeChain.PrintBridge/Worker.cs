using CafeChain.PrintBridge.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CafeChain.PrintBridge
{
    /// <summary>
    /// Print Bridge Worker — BackgroundService chạy tại quán.
    /// 
    /// Lifecycle:
    ///   1. ExecuteAsync → log config → start SignalRPrintClient
    ///   2. While loop mỗi 30s: kiểm tra kết nối + gửi heartbeat
    ///   3. PrintJob event (xử lý bởi SignalRPrintClient) → filter → TCP forward
    ///   4. StopAsync → report offline → disconnect
    /// 
    /// Fault-Tolerance:
    ///   - Heartbeat loop bọc trong try-catch → lỗi mạng chỉ ghi log, không crash
    ///   - PrintJob handler bọc trong try-catch → lỗi parse/TCP chỉ ghi log, không crash
    ///   - Không dùng System.Threading.Timer → tránh async void
    /// 
    /// Chạy:
    ///   dotnet run --project CafeChain.PrintBridge
    /// </summary>
    public class Worker : BackgroundService
    {
        private readonly SignalRPrintClient _signalRClient;
        private readonly PrintBridgeOptions _options;
        private readonly ILogger<Worker> _logger;

        public Worker(
            SignalRPrintClient signalRClient,
            IOptions<PrintBridgeOptions> options,
            ILogger<Worker> logger)
        {
            _signalRClient = signalRClient;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("═══════════════════════════════════════════════════");
            _logger.LogInformation("  🖨️  CafeChain Print Bridge Worker v1.0");
            _logger.LogInformation("═══════════════════════════════════════════════════");
            _logger.LogInformation("  Hub URL     : {HubUrl}", _options.HubUrl);
            _logger.LogInformation("  Store ID    : {StoreId}", _options.StoreId);
            _logger.LogInformation("  Target      : {PrinterTarget}", _options.PrinterTarget);
            _logger.LogInformation("  Printer     : {PrinterIp}:{PrinterPort}", _options.PrinterIp, _options.PrinterPort);
            _logger.LogInformation("  Heartbeat   : {Interval}s", _options.HeartbeatIntervalSeconds);
            _logger.LogInformation("  Max Retries : {MaxRetries}", _options.MaxRetries);
            _logger.LogInformation("═══════════════════════════════════════════════════");

            // 1. Khởi tạo kết nối SignalR — connect + join group + register event handlers
            try
            {
                await _signalRClient.StartAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return; // Shutdown bình thường
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Không thể khởi tạo kết nối SignalR. Worker dừng.");
                return;
            }

            _logger.LogInformation("🟢 Worker đang chạy. Nhấn Ctrl+C để dừng.");

            // 2. Heartbeat loop — chạy cho đến khi nhận shutdown signal
            var heartbeatInterval = TimeSpan.FromSeconds(_options.HeartbeatIntervalSeconds);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Chờ N giây trước khi gửi heartbeat tiếp theo
                    await Task.Delay(heartbeatInterval, stoppingToken);

                    // Kiểm tra trạng thái kết nối trước khi gửi
                    if (_signalRClient.IsConnected)
                    {
                        await _signalRClient.SendHeartbeatAsync(stoppingToken);
                    }
                    else
                    {
                        _logger.LogWarning("[Heartbeat] ⚠️ SignalR chưa connected — bỏ qua heartbeat lần này.");
                    }
                }
                catch (OperationCanceledException)
                {
                    break; // Shutdown bình thường — thoát vòng lặp
                }
                catch (Exception ex)
                {
                    // Lỗi mạng, lỗi SignalR, bất kỳ lỗi nào → chỉ ghi log, KHÔNG crash
                    _logger.LogWarning(ex, "[Heartbeat] ⚠️ Gửi heartbeat thất bại. Worker vẫn tiếp tục.");
                }
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("🔴 Đang ngắt kết nối Print Bridge...");
            await _signalRClient.StopAsync();
            await base.StopAsync(cancellationToken);
            _logger.LogInformation("✅ Print Bridge Worker đã dừng hoàn toàn.");
        }
    }
}
