using CafeChain.Application.Interfaces.Operations;
using CafeChain.Application.Services.Operations;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Authorization;
using CafeChain.Application.Constants;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Controllers.Api.v1
{
    /// <summary>
    /// Issue #101 — POS StaffNotification read/mark APIs (JWT CurrentStaffId).
    /// </summary>
    [Route("api/v1/pos")]
    public class POSNotificationsController : PosApiController
    {
        private readonly IStaffNotificationQueryService _service;
        private readonly ITerminalRegistrationNotificationService? _terminalRegistration;

        public POSNotificationsController(
            IStaffNotificationQueryService service,
            ITerminalRegistrationNotificationService? terminalRegistration = null)
        {
            _service = service;
            _terminalRegistration = terminalRegistration;
        }

        /// <summary>GET /api/v1/pos/notifications/unread-count</summary>
        [HttpGet("notifications/unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var result = await _service.GetUnreadCountAsync(CurrentStaffId);
            if (!result.IsSuccess)
                return BadRequest(new { success = false, message = result.Message });

            return Ok(new { success = true, data = result.Data });
        }

        /// <summary>GET /api/v1/pos/notifications?page=1&amp;pageSize=20</summary>
        [HttpGet("notifications")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> GetList(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _service.GetListAsync(
                CurrentStaffId,
                page,
                pageSize,
                StaffNotificationQueryService.ChannelPos);

            if (!result.IsSuccess)
                return BadRequest(new { success = false, message = result.Message });

            return Ok(new { success = true, data = result.Data });
        }

        /// <summary>POST /api/v1/pos/notifications/{id}/read</summary>
        [HttpPost("notifications/{id:int}/read")]
        public async Task<IActionResult> MarkRead(int id)
        {
            var result = await _service.MarkReadAsync(CurrentStaffId, id);
            if (!result.IsSuccess)
            {
                return NotFound(new { success = false, message = result.Message });
            }

            return Ok(new { success = true, data = result.Data });
        }

        /// <summary>POST /api/v1/pos/notifications/read-all</summary>
        [HttpPost("notifications/read-all")]
        public async Task<IActionResult> MarkAllRead()
        {
            var result = await _service.MarkAllReadAsync(CurrentStaffId);
            if (!result.IsSuccess)
                return BadRequest(new { success = false, message = result.Message });

            return Ok(new { success = true, data = result.Data });
        }

        [HttpGet("notifications/{id:int}/operational-otp")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> RevealOperationalOtp(int id)
        {
            if (_terminalRegistration == null) return StatusCode(503);
            var result = await _terminalRegistration.RevealOperationalOtpAsync(CurrentStaffId, id);
            return result.IsSuccess
                ? Ok(new { success = true, data = result.Data })
                : BadRequest(new { success = false, errorCode = result.ErrorCode, message = result.Message });
        }

        [HttpGet("notifications/{id:int}/terminal-otp")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public Task<IActionResult> RevealTerminalOtp(int id) => RevealOperationalOtp(id);

        [HttpPost("notifications/{id:int}/terminal-confirm")]
        [RequirePermission(PermissionConstants.PosWorkShiftOverrideTerminal)]
        public async Task<IActionResult> ConfirmTerminal(
            int id,
            [FromBody] ConfirmTerminalNotificationRequestDto request)
        {
            if (_terminalRegistration == null) return StatusCode(503);
            var result = await _terminalRegistration.ConfirmAsync(CurrentStaffId, id, request);
            return result.IsSuccess
                ? Ok(new { success = true, message = result.Message })
                : BadRequest(new { success = false, errorCode = result.ErrorCode, message = result.Message });
        }
    }
}
