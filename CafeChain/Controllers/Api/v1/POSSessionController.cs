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
        if (!result.IsSuccess || result.Data == null)
            return Unauthorized(new { success = false, errorCode = result.ErrorCode, message = result.Message });
        return Ok(new
        {
            success = true,
            token = result.Data.Token,
            expiresAtUtc = result.Data.ExpiresAtUtc,
            purpose = result.Data.Purpose
        });
    }
}
