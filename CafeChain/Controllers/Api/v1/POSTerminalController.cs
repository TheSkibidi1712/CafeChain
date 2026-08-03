using CafeChain.Application.Authorization;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.POS;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Controllers.Api.v1;

[Route("api/v1/pos/terminals")]
public sealed class POSTerminalController : PosApiController
{
    private readonly IWorkShiftService _workShiftService;

    public POSTerminalController(IWorkShiftService workShiftService) => _workShiftService = workShiftService;

    [HttpPost("register")]
    // The requesting cashier only needs Open; the consumed OTP is revalidated against
    // OverrideTerminal permission and store scope for the approver inside the service.
    [RequirePermission(PermissionConstants.PosWorkShiftOpen)]
    public async Task<IActionResult> Register([FromBody] PosTerminalRegisterDto request)
    {
        var result = await _workShiftService.RegisterTerminalAsync(CurrentStaffId, CurrentStoreId, request);
        return result.IsSuccess
            ? Ok(new { success = true, message = result.Message })
            : BadRequest(new
            {
                success = false,
                result.ErrorCode,
                result.Message,
                correlationId = HttpContext.TraceIdentifier
            });
    }
}
