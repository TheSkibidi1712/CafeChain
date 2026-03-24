namespace CafeChain.Application.Interfaces.Accounts
{
    public interface IEmailService
    {
        Task SendAsync(string to, string subject, string body);
        string BuildOtpEmail(string code); // 🔥 thêm dòng này
    }
}
