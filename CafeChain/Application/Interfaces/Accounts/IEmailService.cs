namespace CafeChain.Application.Interfaces.Accounts
{
    public interface IEmailService
    {
        Task SendAsync(string to, string subject, string body);
        string BuildOtpEmail(string code); // 🔥 thêm dòng này

        /// <summary>
        /// Build HTML email cho OTP xác nhận thao tác vận hành (lệch két, hủy đơn, v.v.).
        /// Khác với BuildOtpEmail dùng cho reset password.
        /// </summary>
        string BuildOperationalOtpEmail(
            string otpCode,
            string storeName,
            string targetLabel,
            string requestedByName,
            string actionLabel,
            string reason,
            DateTime requestedAt,
            int ttlMinutes);
    }
}
