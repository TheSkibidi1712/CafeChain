using CafeChain.Application.DTOs.AI;
using CafeChain.Application.Interfaces.AI;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers;

[Area("Admin")]
[Route("Admin/AI/[action]")]
public sealed class AdminAIController : AdminBaseController
{
    private readonly IAIService _aiService;
    private readonly ILogger<AdminAIController> _logger;

    public AdminAIController(IAIService aiService, ILogger<AdminAIController> logger)
    {
        _aiService = aiService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Health()
    {
        var health = await _aiService.CheckHealthAsync(HttpContext.RequestAborted);
        return Ok(new { success = health.ServerAvailable && health.ModelAvailable, message = health.Message,
            data = health, usedOllama = false, usedFallback = false });
    }
}
