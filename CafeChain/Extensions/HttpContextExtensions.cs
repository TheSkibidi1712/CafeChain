using Microsoft.AspNetCore.Http;
using System.Linq;

namespace CafeChain.Extensions
{
    public static class HttpContextExtensions 
    {
        public static string GetClientIP(this HttpContext context)
        {
            // 1. Phá băng Cloudflare / Proxy: Đọc Header X-Forwarded-For trước
            var forwardedHeader = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedHeader))
            {
                // Proxy có thể gộp nhiều IP bằng dấu phẩy, IP đầu tiên sẽ là Client IP thật
                var ips = forwardedHeader.Split(',', System.StringSplitOptions.RemoveEmptyEntries);
                if (ips.Length > 0) return ips[0].Trim();
            }

            // 2. Fallback về IP vật lý mà Kestrel Connection nhận được
            var remoteIp = context.Connection.RemoteIpAddress;
            if (remoteIp != null)
            {
                // Chuyển local IPv6 ::1 thành 127.0.0.1 để đồng nhất
                if (remoteIp.IsIPv4MappedToIPv6) return remoteIp.MapToIPv4().ToString();
                return remoteIp.ToString();
            }

            return "Unknown";
        }
    }
}
