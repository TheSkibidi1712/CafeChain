using System.Security.Claims;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.POS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Controllers.Api.v1
{
    /// <summary>Issue #134 — full-order cash refund API.</summary>
    [Route("api/v1/pos/refunds")]
    [Authorize(Policy = AuthorizationPolicyConstants.PosApp)]
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

            var result = await _refundService.RequestFullRefundAsync(
                dto, GetCurrentActor());

            if (!result.IsSuccess)
            {
                if (result.ErrorCode == OrderRefundFailureCodes.RoleUnauthorized)
                    return Forbid();

                if (result.ErrorCode is OrderRefundFailureCodes.StoreUnauthorized
                    or OrderRefundFailureCodes.OrderNotFound)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Không tìm thấy đơn hàng."
                    });
                }

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

            var result = await _refundService.ConfirmCashRefundAsync(
                dto, GetCurrentActor());

            if (!result.IsSuccess)
            {
                if (result.ErrorCode == OrderRefundFailureCodes.RoleUnauthorized)
                    return Forbid();

                if (result.ErrorCode is OrderRefundFailureCodes.StoreUnauthorized
                    or OrderRefundFailureCodes.RefundNotFound
                    or OrderRefundFailureCodes.OrderNotFound)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Không tìm thấy yêu cầu hoàn đơn."
                    });
                }

                return Ok(new
                {
                    success = false,
                    message = result.Message,
                    errorCode = result.ErrorCode
                });
            }

            return Ok(new { success = true, message = result.Message, data = result.Data });
        }

        private AdminActorContext GetCurrentActor()
        {
            var roles = User.FindAll(ClaimTypes.Role).Select(claim => claim.Value)
                .Concat(User.FindAll("role").Select(claim => claim.Value))
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new AdminActorContext
            {
                StaffId = CurrentStaffId,
                StoreId = CurrentStoreId,
                RoleNames = roles
            };
        }
    }
}
