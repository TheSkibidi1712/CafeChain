using CafeChain.Application.Interfaces.Operations;
using CafeChain.Application.Services.Operations;
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

        public POSNotificationsController(IStaffNotificationQueryService service)
        {
            _service = service;
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
    }
}
