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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SuggestInventoryInput([FromBody] InventoryInputSuggestionRequestDTO request)
    {
        if (!ModelState.IsValid)
        {
            var message = ModelState.Values.SelectMany(x => x.Errors)
                .Select(x => x.ErrorMessage).FirstOrDefault() ?? "Dữ liệu gợi ý không hợp lệ.";
            return BadRequest(new { success = false, message, data = (object?)null, usedOllama = false, usedFallback = false });
        }

        try
        {
            var result = await _aiService.SuggestInventoryInputAsync(request, HttpContext.RequestAborted);
            var response = new { success = result.Success, message = result.Message, data = result,
                usedOllama = result.UsedOllama, usedFallback = result.UsedFallback };
            return result.Success ? Ok(response) : BadRequest(response);
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            return new EmptyResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Inventory input AI suggestion failed.");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { success = false, message = "Không thể tạo gợi ý nhập kho.", data = (object?)null, usedOllama = false, usedFallback = false });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SuggestSupplier([FromBody] SupplierSuggestionRequestDTO request)
    {
        if (!ModelState.IsValid)
        {
            var message = ModelState.Values.SelectMany(x => x.Errors)
                .Select(x => x.ErrorMessage).FirstOrDefault() ?? "Dữ liệu phân tích không hợp lệ.";
            return BadRequest(new { success = false, message, data = (object?)null, usedOllama = false, usedFallback = false });
        }

        try
        {
            var result = await _aiService.SuggestSupplierAsync(request, HttpContext.RequestAborted);
            var response = new { success = result.Success, message = result.Message, data = result,
                usedOllama = result.UsedOllama, usedFallback = result.UsedFallback };
            return result.Success ? Ok(response) : BadRequest(response);
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            return new EmptyResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Supplier AI analysis failed.");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { success = false, message = "Không thể phân tích nhà cung cấp.", data = (object?)null, usedOllama = false, usedFallback = false });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Health()
    {
        var health = await _aiService.CheckHealthAsync(HttpContext.RequestAborted);
        return Ok(new { success = health.ServerAvailable && health.ModelAvailable, message = health.Message,
            data = health, usedOllama = false, usedFallback = false });
    }
}
