using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Threading.Tasks;
using CafeChain.Application.Interfaces.Attendance;

namespace CafeChain.Controllers
{
    [Route("api/[controller]")]
    [Authorize]
    public class AttendanceController : Controller
    {
        private readonly IAttendanceSecurityService _securityService;
        private readonly IAttendanceActionService _actionService;

        public AttendanceController(IAttendanceSecurityService securityService, IAttendanceActionService actionService)
        {
            _securityService = securityService;
            _actionService = actionService;
        }

        /// <summary>
        /// Helper: Extract accountId from authenticated Claims (Anti-IDOR)
        /// </summary>
        private bool TryGetAccountId(out int accountId)
        {
            accountId = 0;
            var str = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return !string.IsNullOrEmpty(str) && int.TryParse(str, out accountId);
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

        // Anti-IDOR: accountId is NO LONGER accepted from client
        [HttpPost("FirstLoginChangePassword")]
        public async Task<IActionResult> FirstLoginChangePassword([FromForm] string oldPassword, [FromForm] string newPassword)
        {
            if (!TryGetAccountId(out int accountId))
                return Unauthorized(new { success = false, message = "Chưa đăng nhập." });

            var result = await _securityService.ProcessFirstLoginPasswordChangeAsync(accountId, oldPassword, newPassword);
            
            if (!result.IsSuccess)
                return BadRequest(new { success = false, message = result.Message });

            return Ok(new { success = true, message = result.Message });
        }

        // Anti-IDOR: accountId extracted from Claims, NOT from form body
        [HttpPost("SubmitTimeAction")]
        public async Task<IActionResult> SubmitTimeAction([FromForm] string actionType, [FromForm] string faceDescriptor, [FromForm] bool forceSave = false)
        {
            if (!TryGetAccountId(out int accountId))
                return Unauthorized(new { success = false, message = "Chưa đăng nhập." });

            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
            var result = await _actionService.SubmitTimeActionAsync(accountId, actionType, faceDescriptor, forceSave, clientIp);

            if (!result.IsSuccess)
            {
                // Return 409 for duplicate check-in conflict
                if (result.ErrorCode == "CONFLICT_ALREADY_ACTIVE")
                    return Conflict(new { success = false, errorCode = result.ErrorCode, message = result.Message });

                return BadRequest(new { success = false, errorCode = result.ErrorCode, message = result.Message });
            }

            return Ok(new { success = true, errorCode = result.ErrorCode, message = result.Message });
        }

        // Anti-IDOR: accountId extracted from Claims
        [HttpPost("RegisterFace")]
        public async Task<IActionResult> RegisterFace([FromBody] RegisterFaceRequest request)
        {
            if (!TryGetAccountId(out int accountId))
                return Unauthorized(new { success = false, message = "Chưa đăng nhập." });

            var result = await _securityService.RegisterFaceAsync(accountId, request.FaceDescriptor);

            if (!result.IsSuccess)
                return BadRequest(new { success = false, message = result.Message });

            return Ok(new { success = true, message = result.Message });
        }

        [HttpPost("UpdatePin")]
        public async Task<IActionResult> UpdatePin([FromForm] string pin)
        {
            if (!TryGetAccountId(out int accountId))
                return Unauthorized(new { success = false, message = "Chưa đăng nhập." });

            var result = await _securityService.UpdatePinAsync(accountId, pin);

            if (!result.IsSuccess)
                return BadRequest(new { success = false, message = result.Message });

            return Ok(new { success = true, message = result.Message });
        }

        /// <summary>
        /// API tổng hợp: Trả về thông tin nhân viên, trạng thái Face ID, và lịch ca hôm nay
        /// Frontend StaffHub gọi API này khi page load
        /// </summary>
        [HttpGet("GetStaffHubData")]
        public async Task<IActionResult> GetStaffHubData()
        {
            if (!TryGetAccountId(out int accountId))
                return Unauthorized(new { success = false, message = "Chưa đăng nhập." });

            var result = await _actionService.GetKioskDataAsync(accountId);

            if (!result.IsSuccess)
                return BadRequest(new { success = false, message = result.Message });

            return Ok(new { success = true, data = result.Data });
        }

        // Keep old endpoint for backward compatibility (redirects to new)
        [HttpGet("GetKioskData")]
        public async Task<IActionResult> GetKioskData()
        {
            return await GetStaffHubData();
        }

        [HttpGet("/Attendance/MyBYOD")]
        public async Task<IActionResult> MyBYOD()
        {
            if (!TryGetAccountId(out int accountId))
                return RedirectToAction("Login", "Account");

            var result = await _actionService.GetKioskDataAsync(accountId);
            if (!result.IsSuccess)
                return NotFound(result.Message);

            dynamic data = result.Data;
            ViewBag.StaffName = data.staffName;
            ViewBag.StoreName = data.storeName;
            ViewBag.AccountId = accountId;

            return View("~/Views/Attendance/MyBYOD.cshtml");
        }
    }

    public class RegisterFaceRequest
    {
        public string FaceDescriptor { get; set; } = string.Empty;
    }
}
