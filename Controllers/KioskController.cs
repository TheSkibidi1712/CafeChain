using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Linq;
using CafeChain.Application.Interfaces.Attendance;

namespace CafeChain.Controllers
{
    [Authorize] // Bắt buộc đăng nhập mới vào được Kiosk Chấm Công
    public class KioskController : Controller
    {
        private readonly IAttendanceSecurityService _securityService;

        public KioskController(IAttendanceSecurityService securityService)
        {
            _securityService = securityService;
        }

        // GET: /Kiosk/Index
        public async Task<IActionResult> Index()
        {
            var storeIdStr = User.FindFirst("StoreId")?.Value;
            if (!string.IsNullOrEmpty(storeIdStr) && int.TryParse(storeIdStr, out int storeId))
            {
                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
                
                var forwardedHeader = Request.Headers["X-Forwarded-For"].FirstOrDefault();
                if (!string.IsNullOrEmpty(forwardedHeader))
                {
                    var ips = forwardedHeader.Split(',', System.StringSplitOptions.RemoveEmptyEntries);
                    if (ips.Length > 0) clientIp = ips[0].Trim();
                }

                var result = await _securityService.ValidateStoreIPAsync(storeId, clientIp);
                if (!result.IsSuccess)
                {
                    return Content($@"
                        <html><head><meta name='viewport' content='width=device-width, initial-scale=1'/></head>
                        <body style='text-align:center; padding: 20px; font-family:sans-serif;'>
                            <h2 style='color:#dc3545;'>Sai Truy Cập Mạng (Kiosk)!</h2>
                            <p>IP của thiết bị này là: <b>{clientIp}</b></p>
                            <p>{result.Message}</p>
                        </body></html>", "text/html"
                    );
                }
            }
            return View();
        }
    }
}
