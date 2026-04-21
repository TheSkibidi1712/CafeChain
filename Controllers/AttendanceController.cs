using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using CafeChain.Application.Interfaces.Attendance;
using Microsoft.AspNetCore.Http;

namespace CafeChain.Controllers
{
    [Route("api/[controller]")]
    // [ApiController] // Bỏ ApiController để hỗ trợ trả về View
    public class AttendanceController : Controller
    {
        private readonly IAttendanceSecurityService _securityService;
        private readonly IAttendanceActionService _actionService;

        public AttendanceController(IAttendanceSecurityService securityService, IAttendanceActionService actionService)
        {
            _securityService = securityService;
            _actionService = actionService;
        }

        [HttpPost("CheckNetwork")]
        public async Task<IActionResult> CheckNetwork([FromQuery] int storeId)
        {
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            var result = await _securityService.ValidateStoreIPAsync(storeId, clientIp);
            
            if (!result.IsSuccess)
                return BadRequest(new { success = false, message = result.Message });

            return Ok(new { success = true, message = result.Message });
        }

        [HttpPost("FirstLoginChangePassword")]
        public async Task<IActionResult> FirstLoginChangePassword(int accountId, [FromForm] string oldPassword, [FromForm] string newPassword)
        {
            var result = await _securityService.ProcessFirstLoginPasswordChangeAsync(accountId, oldPassword, newPassword);
            
            if (!result.IsSuccess)
                return BadRequest(new { success = false, message = result.Message });

            return Ok(new { success = true, message = result.Message });
        }

        [HttpPost("SubmitTimeAction")]
        public async Task<IActionResult> SubmitTimeAction(int accountId, [FromForm] string actionType, [FromForm] string faceDescriptor, [FromForm] bool forceSave = false)
        {
            var result = await _actionService.SubmitTimeActionAsync(accountId, actionType, faceDescriptor, forceSave);

            if (!result.IsSuccess)
                return BadRequest(new { success = false, errorCode = result.ErrorCode, message = result.Message });

            return Ok(new { success = true, errorCode = result.ErrorCode, message = result.Message });
        }

        [HttpPost("RegisterFace")]
        public async Task<IActionResult> RegisterFace([FromBody] RegisterFaceRequest request)
        {
            var result = await _securityService.RegisterFaceAsync(request.AccountId, request.FaceDescriptor);

            if (!result.IsSuccess)
                return BadRequest(new { success = false, message = result.Message });

            return Ok(new { success = true, message = result.Message });
        }

        /// <summary>
        /// API tổng hợp: Trả về thông tin nhân viên, trạng thái Face ID, và lịch ca hôm nay
        /// Frontend Kiosk gọi API này khi page load
        /// </summary>
        [HttpGet("GetKioskData")]
        public async Task<IActionResult> GetKioskData([FromQuery] int accountId)
        {
            var result = await _actionService.GetKioskDataAsync(accountId);

            if (!result.IsSuccess)
                return BadRequest(new { success = false, message = result.Message });

            return Ok(new { success = true, data = result.Data });
        }

        [HttpGet("/Attendance/MyBYOD")]
        public async Task<IActionResult> MyBYOD()
        {
            string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            
            // Thử lấy IP từ X-Forwarded-For nếu qua proxy
            var forwardedHeader = Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedHeader))
            {
                var ips = forwardedHeader.Split(',', System.StringSplitOptions.RemoveEmptyEntries);
                if (ips.Length > 0) clientIp = ips[0].Trim();
            }

            var accountIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(accountIdStr)) return Unauthorized();
            
            int accountId = int.Parse(accountIdStr);

            // Truy cập _context cần được Inject. Vì AttendanceController đang gọi thông qua ActionService, ta thêm DbContext vào đây.
            // Để nhanh, lấy thông tin staff từ action service.
            // Tuy nhiên, vì đoạn code yêu cầu DbContext trực tiếp, ta sẽ inject HttpContext.RequestServices.
            var context = HttpContext.RequestServices.GetService(typeof(CafeChain.Data.AppDbContext)) as CafeChain.Data.AppDbContext;
            
            var staff = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                context.Staffs, s => s.AccountId == accountId);
                
            if (staff == null) return NotFound("Lỗi profile nhân sự");

            // Sửa dụng chung ValidateStoreIPAsync để hỗ trợ Wildcard subnet
            var ipResult = await _securityService.ValidateStoreIPAsync(staff.StoreId, clientIp);
            if (!ipResult.IsSuccess)
            {
                return Content($@"
                    <html><head><meta name='viewport' content='width=device-width, initial-scale=1'/></head>
                    <body style='text-align:center; padding: 20px; font-family:sans-serif;'>
                        <h2 style='color:#dc3545;'>Sai Truy Cập Mạng!</h2>
                        <p>IP của máy bạn hiện tại là: <b>{clientIp}</b></p>
                        <p>{ipResult.Message}</p>
                    </body></html>", "text/html"
                );
            }

            ViewBag.StaffName = staff.FullName;
            ViewBag.StoreName = staff.Store?.Name ?? "CafeChain";
            ViewBag.AccountId = accountId;
            return View("~/Views/Attendance/MyBYOD.cshtml");
        }
    }

    public class RegisterFaceRequest
    {
        public int AccountId { get; set; }
        public string FaceDescriptor { get; set; } = string.Empty;
    }
}
