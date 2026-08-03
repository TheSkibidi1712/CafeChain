using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.POS;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Controllers.Api.v1;

/// <summary>Thin HTTP adapter for cashier-driven payment cancellation actions.</summary>
[Route("api/v1/pos/payments")]
public sealed class POSPaymentController : PosApiController
{
    private readonly IPOSPaymentCancellationService _service;

    public POSPaymentController(IPOSPaymentCancellationService service) => _service = service;

    [HttpPost("cancel-payment")]
    public async Task<IActionResult> CancelPayment(
        [FromBody] CancelPaymentRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.CancelPaymentAsync(
            request,
            CurrentStaffId,
            CurrentStoreId,
            cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("temporary-cash/cancel")]
    public async Task<IActionResult> CancelTemporaryCash(
        [FromBody] CancelTemporaryCashRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.CancelTemporaryCashAsync(
            request,
            CurrentStaffId,
            CurrentStoreId,
            cancellationToken);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult(POSPaymentOperationResultDto result)
    {
        var payload = new
        {
            success = result.Success,
            code = result.ErrorCode,
            errorCode = result.ErrorCode,
            message = result.Message,
            correlationId = HttpContext.TraceIdentifier
        };

        return result.HttpStatusCode switch
        {
            StatusCodes.Status200OK => Ok(payload),
            StatusCodes.Status404NotFound => NotFound(payload),
            StatusCodes.Status409Conflict => Conflict(payload),
            StatusCodes.Status500InternalServerError => StatusCode(
                StatusCodes.Status500InternalServerError,
                payload),
            _ => BadRequest(payload)
        };
    }
}
