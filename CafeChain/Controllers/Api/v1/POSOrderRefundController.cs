using System.Security.Claims;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.POS;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Controllers.Api.v1
{
    /// <summary>Issue #134 — full-order cash refund API.</summary>
    [Route("api/v1/pos/refunds")]
    public sealed class POSOrderRefundController : PosApiController
    {
        private readonly IOrderRefundService _refundService;

        public POSOrderRefundController(IOrderRefundService refundService)
        {
            _refundService = refundService;
        }

        [HttpPost("request")]
        public async Task<IActionResult> RequestFullRefund([FromBody] RequestFullOrderRefundDto dto)
        {
            if (dto == null)
                return BadRequest(new { success = false, message = "Body rỗng." });

            var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value)
                .Concat(User.FindAll("role").Select(c => c.Value))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var result = await _refundService.RequestFullRefundAsync(
                dto, CurrentStaffId, CurrentStoreId, roles);

            if (!result.IsSuccess)
            {
                return Ok(new
                {
                    success = false,
                    message = result.Message,
                    errorCode = result.ErrorCode
                });
            }

            return Ok(new { success = true, message = result.Message, data = result.Data });
        }

        [HttpPost("confirm-cash")]
        public async Task<IActionResult> ConfirmCashRefund([FromBody] ConfirmCashRefundDto dto)
        {
            if (dto == null)
                return BadRequest(new { success = false, message = "Body rỗng." });

            var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value)
                .Concat(User.FindAll("role").Select(c => c.Value))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var result = await _refundService.ConfirmCashRefundAsync(
                dto, CurrentStaffId, CurrentStoreId, roles);

            if (!result.IsSuccess)
            {
                return Ok(new
                {
                    success = false,
                    message = result.Message,
                    errorCode = result.ErrorCode
                });
            }

            return Ok(new { success = true, message = result.Message, data = result.Data });
        }
    }
}
