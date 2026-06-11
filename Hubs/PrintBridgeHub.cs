using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace CafeChain.Hubs
{
    /// <summary>
    /// ADR-0003: SignalR Hub cho Print Bridge — kết nối Cloud Backend với Print Bridge tại quán.
    /// 
    /// Architecture:
    ///   Cloud Backend ──SignalR──► Print Bridge (.NET Worker) ──TCP:9100──► Máy in LAN
    ///   Cloud Backend ──SignalR──► iPad POS (printer status indicator)
    /// 
    /// Groups:
    ///   "PrintBridge_Store_{storeId}" — Print Bridge Worker join group này để nhận lệnh in
    ///   "POS_Store_{storeId}"        — iPad POS join group này để nhận printer status
    /// 
    /// Authentication:
    ///   Print Bridge Worker phải gửi header "X-PrintBridge-Key" khớp với config "PrintBridge:ApiKey".
    ///   iPad POS (browser) không cần key — chỉ nhận status, không nhận print payload.
    /// 
    /// TUYỆT ĐỐI KHÔNG dùng Clients.All — chỉ gửi đến group theo StoreId.
    /// </summary>
    public class PrintBridgeHub : Hub
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<PrintBridgeHub> _logger;

        public PrintBridgeHub(IConfiguration configuration, ILogger<PrintBridgeHub> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Print Bridge Worker gọi method này ngay sau khi connect thành công
        /// để đăng ký nhận lệnh in cho quán mình.
        /// YÊU CẦU: Header "X-PrintBridge-Key" phải khớp với config server.
        /// </summary>
        public async Task JoinPrintGroup(int storeId)
        {
            // Xác thực API Key từ header — chỉ Worker mới cần join group PrintBridge
            var httpContext = Context.GetHttpContext();
            var clientKey = httpContext?.Request.Headers["X-PrintBridge-Key"].ToString();
            var serverKey = _configuration["PrintBridge:ApiKey"];

            if (string.IsNullOrEmpty(clientKey) || clientKey != serverKey)
            {
                _logger.LogWarning(
                    "[PrintBridgeHub] Từ chối JoinPrintGroup — API Key không hợp lệ. ConnectionId={ConnectionId}",
                    Context.ConnectionId);
                await Clients.Caller.SendAsync("AuthError", "API Key không hợp lệ. Kết nối bị từ chối.");
                Context.Abort();
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, $"PrintBridge_Store_{storeId}");
            // Cũng join group POS để broadcast status cho iPad cùng quán
            await Groups.AddToGroupAsync(Context.ConnectionId, $"POS_Store_{storeId}");

            _logger.LogInformation(
                "[PrintBridgeHub] Worker đã join PrintBridge_Store_{StoreId}. ConnectionId={ConnectionId}",
                storeId, Context.ConnectionId);
        }

        /// <summary>
        /// iPad POS gọi method này để nhận printer status real-time.
        /// KHÔNG yêu cầu API Key — iPad chỉ nhận status, không nhận print payload.
        /// </summary>
        public async Task JoinPosGroup(int storeId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"POS_Store_{storeId}");
        }

        /// <summary>
        /// Print Bridge Worker gửi heartbeat định kỳ (mỗi 30s).
        /// Backend broadcast trạng thái cho iPad POS cùng quán.
        /// </summary>
        /// <param name="storeId">Store ID của quán</param>
        /// <param name="isOnline">true = printer online, false = offline</param>
        public async Task ReportPrinterStatus(int storeId, bool isOnline)
        {
            await Clients.Group($"POS_Store_{storeId}").SendAsync("PrinterStatusChanged", new
            {
                storeId,
                isOnline,
                reportedAt = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Được gọi khi Print Bridge Worker disconnect (mất kết nối).
        /// Tự động broadcast printer offline cho iPad POS.
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // Note: Không biết storeId ở đây — iPad POS sẽ detect qua timeout
            // của heartbeat (không nhận PrinterStatusChanged trong 60s → coi là offline)
            _logger.LogInformation(
                "[PrintBridgeHub] Client disconnected. ConnectionId={ConnectionId}, Error={Error}",
                Context.ConnectionId, exception?.Message ?? "none");
            await base.OnDisconnectedAsync(exception);
        }
    }
}
