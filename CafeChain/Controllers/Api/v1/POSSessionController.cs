using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.POS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using CafeChain.Models.Operations;
using CafeChain.Application.Authorization;
using CafeChain.Application.Constants;

namespace CafeChain.Controllers.Api.v1;

[ApiController]
[Route("api/v1/pos/session")]
public sealed class POSSessionController : ControllerBase
{
    private readonly IPosSessionExchangeService _exchangeService;
    private readonly IPosAccessSessionService _sessions;
    public POSSessionController(IPosSessionExchangeService exchangeService, IPosAccessSessionService sessions)
    {
        _exchangeService = exchangeService;
        _sessions = sessions;
    }

    [HttpPost("exchange")]
    [AllowAnonymous]
    public async Task<IActionResult> Exchange([FromBody] PosSessionExchangeRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _exchangeService.ExchangeAsync(request.ExchangeCode, cancellationToken);
        if (!result.IsSuccess || result.Data == null)
            return Unauthorized(new { success = false, errorCode = result.ErrorCode, message = result.Message });
        return Ok(new
        {
            success = true,
            token = result.Data.Token,
            expiresAtUtc = result.Data.ExpiresAtUtc,
            sessionId = result.Data.SessionId,
            purpose = result.Data.Purpose
        });
    }

    [HttpGet("current")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [RequirePermission(PermissionConstants.AppPos)]
    public async Task<IActionResult> Current(CancellationToken cancellationToken)
    {
        if (!TryGetSessionId(out var sessionId)) return Unauthorized();
        var jwtId = User.FindFirstValue("jti") ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti);
        if (string.IsNullOrWhiteSpace(jwtId)) return Unauthorized();
        var result = await _sessions.ValidateAsync(sessionId, jwtId, cancellationToken);
        return result.IsSuccess ? Ok(new { success = true, data = result.Data })
            : Unauthorized(new { success = false, errorCode = result.ErrorCode, message = result.Message });
    }

    [HttpPost("logout")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (!TryGetSessionId(out var sessionId)) return Unauthorized();
        var staffId = int.TryParse(User.FindFirstValue("StaffId"), out var parsed) ? parsed : 0;
        var result = await _sessions.EndAsync(
            sessionId,
            PosAccessSessionStatuses.LoggedOut,
            staffId > 0 ? staffId : null,
            "Người dùng đã đăng xuất POS.",
            cancellationToken);
        return result.IsSuccess ? Ok(new { success = true, message = result.Message })
            : BadRequest(new { success = false, errorCode = result.ErrorCode, message = result.Message });
    }

    private bool TryGetSessionId(out Guid sessionId) =>
        Guid.TryParse(User.FindFirstValue("PosSessionId"), out sessionId) && sessionId != Guid.Empty;
}
