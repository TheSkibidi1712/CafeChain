using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.POS;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Controllers.Api.v1
{
    /// <summary>
    /// OTP Approval API — Issue #89: OTP Foundation Backend.
    /// 
    /// Cung cấp 3 endpoints:
    ///   POST /api/v1/otp/request  — tạo OTP challenge mới
    ///   POST /api/v1/otp/verify   — xác nhận OTP
    ///   POST /api/v1/otp/resend   — gửi lại OTP
    /// 
    /// StaffId/StoreId lấy từ JWT Claims (PosApiController base).
    /// Không nhận approverStaffId/email từ frontend.
    /// </summary>
    [Route("api/v1/otp")]
    public class OtpApprovalController : PosApiController
    {
        private readonly IOtpApprovalService _otpService;
        private readonly ILogger<OtpApprovalController> _logger;

        public OtpApprovalController(
            IOtpApprovalService otpService,
            ILogger<OtpApprovalController> logger)
        {
            _otpService = otpService;
            _logger = logger;
        }

        /// <summary>
        /// POST /api/v1/otp/request
        /// Tạo OTP challenge mới, gửi email đến Ca trưởng/Cửa hàng trưởng.
        /// </summary>
        [HttpPost("request")]
        public async Task<IActionResult> RequestOtp([FromBody] OtpRequestDto request)
        {
            var result = await _otpService.RequestOtpAsync(request, CurrentStaffId, CurrentStoreId);

            if (!result.IsSuccess)
                return BadRequest(new
                {
                    success = false,
                    message = result.Message,
                    errorCode = result.ErrorCode,
                    data = result.Data
                });

            return Ok(new
            {
                success = true,
                message = result.Message,
                data = result.Data
            });
        }

        /// <summary>
        /// POST /api/v1/otp/verify
        /// Xác nhận OTP — chuyển challenge sang Approved nếu hợp lệ.
        /// </summary>
        [HttpPost("verify")]
        public async Task<IActionResult> VerifyOtp([FromBody] OtpVerifyDto request)
        {
            var result = await _otpService.VerifyOtpAsync(request);

            if (!result.IsSuccess)
                return BadRequest(new
                {
                    success = false,
                    message = result.Message,
                    errorCode = result.ErrorCode,
                    data = result.Data
                });

            return Ok(new
            {
                success = true,
                message = result.Message,
                data = result.Data
            });
        }

        /// <summary>
        /// POST /api/v1/otp/resend
        /// Gửi lại OTP — invalidates OTP cũ, tạo OTP mới.
        /// </summary>
        [HttpPost("resend")]
        public async Task<IActionResult> ResendOtp([FromBody] OtpResendDto request)
        {
            var result = await _otpService.ResendOtpAsync(request);

            if (!result.IsSuccess)
                return BadRequest(new
                {
                    success = false,
                    message = result.Message,
                    errorCode = result.ErrorCode,
                    data = result.Data
                });

            return Ok(new
            {
                success = true,
                message = result.Message,
                data = result.Data
            });
        }
    }
}
