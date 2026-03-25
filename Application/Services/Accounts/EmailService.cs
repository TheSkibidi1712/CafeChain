using System.Net;
using System.Net.Mail;
using CafeChain.Application.Interfaces.Accounts;

namespace CafeChain.Application.Services.Accounts
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendAsync(string to, string subject, string body)
        {
            try
            {
                var smtpHost = _config["Email:SmtpHost"];
                var smtpPort = int.Parse(_config["Email:SmtpPort"]);
                var email = _config["Email:Address"];
                var password = _config["Email:Password"];

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
                    From = new MailAddress(email, "CafeChain Support"), // 🔥 From name
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };

                mail.To.Add(to);

                await client.SendMailAsync(mail);
            }
            catch (Exception ex)
            {
                throw new Exception("Lỗi gửi email", ex);
            }
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
    }
}