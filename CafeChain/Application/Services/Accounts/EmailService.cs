using System.Net;
using System.Net.Mail;
using CafeChain.Application.Interfaces.Accounts;
using Microsoft.Extensions.Logging;

namespace CafeChain.Application.Services.Accounts
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task SendAsync(string to, string subject, string body)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(to))
                    throw new InvalidOperationException("Thiếu địa chỉ email người nhận.");

                // Local/dev: DeliveryMode=Log skips SMTP so OTP/close-shift can be tested without Gmail.
                var deliveryMode = (_config["Email:DeliveryMode"] ?? "Smtp").Trim();
                if (string.Equals(deliveryMode, "Log", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning(
                        "EMAIL_LOG_ONLY | To={To} | Subject={Subject} | BodyPreview={Preview}",
                        to.Trim(),
                        subject,
                        TruncateForLog(body, 500));
                    await Task.CompletedTask;
                    return;
                }

                var smtpHost = _config["Email:SmtpHost"]?.Trim();
                var smtpPortRaw = _config["Email:SmtpPort"]?.Trim();
                var email = _config["Email:Address"]?.Trim();
                var password = _config["Email:Password"];

                // Fail fast with actionable messages — never include secrets/OTP in exceptions.
                if (string.IsNullOrWhiteSpace(smtpHost))
                    throw new InvalidOperationException("Thiếu cấu hình Email:SmtpHost.");
                if (string.IsNullOrWhiteSpace(smtpPortRaw) || !int.TryParse(smtpPortRaw, out var smtpPort))
                    throw new InvalidOperationException("Cấu hình Email:SmtpPort không hợp lệ.");
                if (string.IsNullOrWhiteSpace(email))
                    throw new InvalidOperationException("Thiếu cấu hình Email:Address (tài khoản SMTP gửi đi).");
                if (string.IsNullOrWhiteSpace(password))
                    throw new InvalidOperationException("Thiếu cấu hình Email:Password (Gmail cần App Password).");

                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    Credentials = new NetworkCredential(email, password),
                    EnableSsl = true,
                    UseDefaultCredentials = false,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Timeout = 10000
                };

                var mail = new MailMessage()
                {
                    From = new MailAddress(email, "CafeChain Support"),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };

                mail.To.Add(to);

                await client.SendMailAsync(mail);
            }
            catch (SmtpException ex)
            {
                // Gmail 5.7.0 / auth failures — config issue, not OTP domain bug.
                var detail = ex.Message ?? string.Empty;
                if (detail.Contains("5.7.0", StringComparison.OrdinalIgnoreCase)
                    || detail.Contains("Authentication Required", StringComparison.OrdinalIgnoreCase)
                    || detail.Contains("not authenticated", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Xác thực SMTP thất bại (Authentication Required). " +
                        "Kiểm tra Email:Address và Email:Password — với Gmail cần bật 2FA và dùng App Password 16 ký tự.",
                        ex);
                }

                throw new InvalidOperationException("Lỗi SMTP khi gửi email: " + SummarizeSmtp(ex), ex);
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Lỗi gửi email: " + (ex.Message ?? "không xác định"), ex);
            }
        }

        private static string TruncateForLog(string? text, int max)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var cleaned = text.Replace('\r', ' ').Replace('\n', ' ');
            return cleaned.Length <= max ? cleaned : cleaned.Substring(0, max) + "...";
        }

        private static string SummarizeSmtp(SmtpException ex)
        {
            var msg = ex.Message ?? string.Empty;
            // Keep short, no credentials.
            if (msg.Length > 160) msg = msg.Substring(0, 160) + "...";
            return msg.Replace('\r', ' ').Replace('\n', ' ');
        }

        // =========================
        // BUILD OTP EMAIL TEMPLATE
        // =========================
        public string BuildOtpEmail(string code)
        {
            return $@"
<div style='font-family:Segoe UI,Arial,sans-serif;background:#f5f5f5;padding:30px'>
    
    <div style='max-width:500px;margin:auto;background:#ffffff;border-radius:12px;
                padding:30px;text-align:center;box-shadow:0 5px 15px rgba(0,0,0,0.1)'>

        <h2 style='color:#ff4d00;margin-bottom:10px'>CafeChain</h2>
        
        <p style='color:#555;font-size:14px'>
            Bạn vừa yêu cầu đặt lại mật khẩu.
        </p>

        <p style='margin-top:20px;font-size:16px'>
            Mã OTP của bạn là:
        </p>

        <div style='font-size:32px;font-weight:bold;
                    letter-spacing:8px;
                    background:#fff3ed;
                    color:#ff4d00;
                    padding:15px 20px;
                    border-radius:10px;
                    display:inline-block;
                    margin:15px 0'>
            {code}
        </div>

        <p style='font-size:13px;color:#888'>
            Mã có hiệu lực trong <b>5 phút</b>.
        </p>

        <hr style='margin:25px 0;border:none;border-top:1px solid #eee' />

        <p style='font-size:12px;color:#aaa'>
            Nếu bạn không yêu cầu, hãy bỏ qua email này.
        </p>

    </div>

</div>";
        }

        // =====================================================
        // BUILD OPERATIONAL OTP EMAIL TEMPLATE (Issue #89)
        // =====================================================
        public string BuildOperationalOtpEmail(
            string otpCode,
            string storeName,
            string targetLabel,
            string requestedByName,
            string actionLabel,
            string reason,
            DateTime requestedAt,
            int ttlMinutes)
        {
            var timeStr = requestedAt.ToString("dd/MM/yyyy HH:mm:ss") + " (UTC)";

            return $@"
<div style='font-family:Segoe UI,Arial,sans-serif;background:#f5f5f5;padding:30px'>

    <div style='max-width:600px;margin:auto;background:#ffffff;border-radius:12px;
                padding:30px;box-shadow:0 5px 15px rgba(0,0,0,0.1)'>

        <h2 style='color:#ff4d00;margin-bottom:5px;text-align:center'>CafeChain</h2>
        <p style='text-align:center;color:#888;font-size:13px;margin-top:0'>Xác nhận thao tác vận hành</p>

        <hr style='border:none;border-top:1px solid #eee;margin:20px 0' />

        <p style='color:#333;font-size:14px'>
            Hệ thống ghi nhận một thao tác cần ca trưởng xác nhận:
        </p>

        <table style='width:100%;border-collapse:collapse;font-size:14px;color:#444'>
            <tr><td style='padding:6px 0;font-weight:600;width:40%'>Chi nhánh:</td><td>{storeName}</td></tr>
            <tr><td style='padding:6px 0;font-weight:600'>Ca / Đối tượng:</td><td>{targetLabel}</td></tr>
            <tr><td style='padding:6px 0;font-weight:600'>Người yêu cầu:</td><td>{requestedByName}</td></tr>
            <tr><td style='padding:6px 0;font-weight:600'>Loại thao tác:</td><td>{actionLabel}</td></tr>
            <tr><td style='padding:6px 0;font-weight:600'>Lý do:</td><td>{reason}</td></tr>
            <tr><td style='padding:6px 0;font-weight:600'>Thời gian yêu cầu:</td><td>{timeStr}</td></tr>
        </table>

        <div style='text-align:center;margin:25px 0'>
            <p style='font-size:15px;color:#333;margin-bottom:8px'>Mã OTP xác nhận:</p>
            <div style='font-size:36px;font-weight:bold;
                        letter-spacing:10px;
                        background:#fff3ed;
                        color:#ff4d00;
                        padding:15px 25px;
                        border-radius:10px;
                        display:inline-block'>
                {otpCode}
            </div>
        </div>

        <p style='font-size:13px;color:#888;text-align:center'>
            Mã có hiệu lực trong <b>{ttlMinutes} phút</b>.
        </p>

        <hr style='border:none;border-top:1px solid #eee;margin:20px 0' />

        <div style='background:#fff8f0;border-left:4px solid #ff9800;padding:12px 15px;border-radius:4px;margin-top:10px'>
            <p style='font-size:13px;color:#b35c00;margin:0'>
                <strong>⚠ Cảnh báo:</strong> Nếu bạn không biết hoặc không đồng ý thao tác này,
                <strong>không cung cấp mã OTP</strong> và báo ngay cho quản lý chi nhánh.
            </p>
        </div>

    </div>

</div>";
        }

        // =====================================================
        // BUILD STOCK SHORTAGE REPORT EMAIL (Issue #98)
        // =====================================================
        public string BuildStockShortageReportEmail(
            string storeName,
            string itemName,
            string itemTypeLabel,
            decimal availableQty,
            string note,
            string reporterName,
            DateTime reportedAtUtc)
        {
            var timeStr = reportedAtUtc.ToString("dd/MM/yyyy HH:mm:ss") + " (UTC)";
            var qtyStr = availableQty.ToString("N3");

            return $@"
<div style='font-family:Segoe UI,Arial,sans-serif;background:#f5f5f5;padding:30px'>
  <div style='max-width:600px;margin:auto;background:#ffffff;border-radius:12px;padding:30px;box-shadow:0 5px 15px rgba(0,0,0,0.1)'>
    <h2 style='color:#ff4d00;margin-bottom:5px;text-align:center'>CafeChain</h2>
    <p style='text-align:center;color:#888;font-size:13px;margin-top:0'>Báo thiếu hàng — Kho chi nhánh</p>
    <hr style='border:none;border-top:1px solid #eee;margin:20px 0' />
    <p style='color:#333;font-size:14px'>Nhân viên POS vừa gửi yêu cầu kiểm tra tồn kho:</p>
    <table style='width:100%;border-collapse:collapse;font-size:14px;color:#444'>
      <tr><td style='padding:6px 0;font-weight:600;width:40%'>Chi nhánh:</td><td>{WebUtility.HtmlEncode(storeName)}</td></tr>
      <tr><td style='padding:6px 0;font-weight:600'>Mặt hàng:</td><td>{WebUtility.HtmlEncode(itemName)}</td></tr>
      <tr><td style='padding:6px 0;font-weight:600'>Loại:</td><td>{WebUtility.HtmlEncode(itemTypeLabel)}</td></tr>
      <tr><td style='padding:6px 0;font-weight:600'>Tồn hiện tại:</td><td>{qtyStr}</td></tr>
      <tr><td style='padding:6px 0;font-weight:600'>Người báo:</td><td>{WebUtility.HtmlEncode(reporterName)}</td></tr>
      <tr><td style='padding:6px 0;font-weight:600'>Thời gian:</td><td>{timeStr}</td></tr>
      <tr><td style='padding:6px 0;font-weight:600'>Ghi chú:</td><td>{WebUtility.HtmlEncode(note)}</td></tr>
    </table>
    <p style='font-size:13px;color:#888;margin-top:20px'>Vui lòng kiểm tra kho chi nhánh và xử lý theo quy trình.</p>
  </div>
</div>";
        }
    }
}