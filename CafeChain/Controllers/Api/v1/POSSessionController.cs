using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.POS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Controllers.Api.v1;

[AllowAnonymous]
[ApiController]
[Route("api/v1/pos/session")]
public sealed class POSSessionController : ControllerBase
{
    private readonly IPosSessionExchangeService _exchangeService;
    public POSSessionController(IPosSessionExchangeService exchangeService) => _exchangeService = exchangeService;

    [HttpPost("exchange")]
    public async Task<IActionResult> Exchange([FromBody] PosSessionExchangeRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _exchangeService.ExchangeAsync(request.ExchangeCode, cancellationToken);
        if (result == null)
            return Unauthorized(new { success = false, errorCode = "POS_SESSION_EXCHANGE_INVALID",
                message = "Mã mở POS không hợp lệ, đã hết hạn hoặc đã được sử dụng." });
        return Ok(new { success = true, token = result.Token, expiresAtUtc = result.ExpiresAtUtc });
    }
}
