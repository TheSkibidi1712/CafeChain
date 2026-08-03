using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Results;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;

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
            ApplySecurityMetadata(request);
            var result = await _otpService.RequestOtpAsync(request, CurrentStaffId, CurrentStoreId);

            if (!result.IsSuccess)
                return OtpError(result);

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
            ApplySecurityMetadata(request);
            var result = await _otpService.VerifyOtpAsync(request, CurrentStaffId, CurrentStoreId);

            if (!result.IsSuccess)
                return OtpError(result);

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
            ApplySecurityMetadata(request);
            var result = await _otpService.ResendOtpAsync(request, CurrentStaffId, CurrentStoreId);

            if (!result.IsSuccess)
                return OtpError(result);

            return Ok(new
            {
                success = true,
                message = result.Message,
                data = result.Data
            });
        }

        private IActionResult OtpError(ServiceResult<OtpChallengeResponseDto> result)
        {
            var body = new
            {
                success = false,
                message = result.Message,
                errorCode = result.ErrorCode,
                data = result.Data,
                correlationId = HttpContext.TraceIdentifier
            };

            return result.ErrorCode switch
            {
                OtpConstants.ErrorCodes.RateLimited => StatusCode(StatusCodes.Status429TooManyRequests, body),
                OtpConstants.ErrorCodes.ApproverNoLongerEligible => StatusCode(StatusCodes.Status403Forbidden, body),
                _ => BadRequest(body)
            };
        }

        private void ApplySecurityMetadata(OtpRequestDto request)
        {
            var metadata = BuildSecurityMetadata(request.TerminalId);
            request.ClientIpHash = metadata.clientIpHash;
            request.DeviceFingerprintHash = metadata.deviceFingerprintHash;
        }

        private void ApplySecurityMetadata(OtpVerifyDto request)
        {
            var metadata = BuildSecurityMetadata(null);
            request.ClientIpHash = metadata.clientIpHash;
            request.DeviceFingerprintHash = metadata.deviceFingerprintHash;
        }

        private void ApplySecurityMetadata(OtpResendDto request)
        {
            var metadata = BuildSecurityMetadata(null);
            request.ClientIpHash = metadata.clientIpHash;
            request.DeviceFingerprintHash = metadata.deviceFingerprintHash;
        }

        private (string? clientIpHash, string? deviceFingerprintHash) BuildSecurityMetadata(string? terminalId)
        {
            var address = HttpContext.Connection.RemoteIpAddress;
            if (address?.IsIPv4MappedToIPv6 == true)
                address = address.MapToIPv4();

            var deviceSource = Request.Headers["X-Device-Id"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(deviceSource))
                deviceSource = terminalId;
            if (string.IsNullOrWhiteSpace(deviceSource))
                deviceSource = $"staff:{CurrentStaffId}:{Request.Headers.UserAgent}";

            return (HashMetadata(address?.ToString()), HashMetadata(deviceSource));
        }

        private static string? HashMetadata(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var normalized = value.Trim();
            if (normalized.Length > 512)
                normalized = normalized[..512];
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
        }
    }
}
