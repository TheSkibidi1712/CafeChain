using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace CafeChain.PrintBridge.Services
{
    /// <summary>
    /// Forward raw ESC/POS byte[] sang máy in nhiệt qua TCP:9100.
    /// 
    /// Flow:
    ///   byte[] payload → TcpClient → NetworkStream.Write → Máy in
    /// 
    /// Retry logic: MaxRetries lần, delay 1s giữa mỗi lần.
    /// Mỗi lần forward mở connection mới rồi đóng — không giữ persistent connection
    /// vì máy in nhiệt giá rẻ thường chỉ hỗ trợ 1 connection đồng thời.
    /// </summary>
    public class TcpPrinterForwarder
    {
        private readonly PrintBridgeOptions _options;
        private readonly ILogger<TcpPrinterForwarder> _logger;

        public TcpPrinterForwarder(IOptions<PrintBridgeOptions> options, ILogger<TcpPrinterForwarder> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        /// <summary>
        /// Gửi ESC/POS payload đến máy in qua TCP.
        /// Retry tối đa MaxRetries lần nếu thất bại.
        /// </summary>
        /// <param name="payload">Raw ESC/POS bytes từ PrintJob event</param>
        /// <param name="orderId">Order ID (chỉ dùng cho logging)</param>
        /// <returns>true nếu gửi thành công, false nếu thất bại sau tất cả retries</returns>
        public async Task<bool> ForwardAsync(byte[] payload, int orderId)
        {
            for (int attempt = 1; attempt <= _options.MaxRetries; attempt++)
            {
                try
                {
                    using var client = new TcpClient();

                    // Connect với timeout
                    var connectTask = client.ConnectAsync(_options.PrinterIp, _options.PrinterPort);
                    if (await Task.WhenAny(connectTask, Task.Delay(_options.TcpTimeoutMs)) != connectTask)
                    {
                        throw new TimeoutException(
                            $"TCP connect timeout sau {_options.TcpTimeoutMs}ms → {_options.PrinterIp}:{_options.PrinterPort}");
                    }
                    await connectTask; // propagate exception nếu có

                    // Gửi raw bytes
                    using var stream = client.GetStream();
                    await stream.WriteAsync(payload, 0, payload.Length);
                    await stream.FlushAsync();

                    _logger.LogInformation(
                        "[TCP] ✅ Order #{OrderId} — Đã gửi {ByteCount} bytes → {PrinterIp}:{PrinterPort} (attempt {Attempt})",
                        orderId, payload.Length, _options.PrinterIp, _options.PrinterPort, attempt);

                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "[TCP] ⚠️ Order #{OrderId} — Attempt {Attempt}/{MaxRetries} thất bại → {PrinterIp}:{PrinterPort}",
                        orderId, attempt, _options.MaxRetries, _options.PrinterIp, _options.PrinterPort);

                    if (attempt < _options.MaxRetries)
                    {
                        // Delay trước khi retry — exponential backoff đơn giản
                        await Task.Delay(1000 * attempt);
                    }
                }
            }

            _logger.LogError(
                "[TCP] ❌ Order #{OrderId} — Thất bại sau {MaxRetries} lần thử. Payload {ByteCount} bytes bị mất.",
                orderId, _options.MaxRetries, payload.Length);

            return false;
        }
    }
}
