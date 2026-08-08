using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces.AI;
using CafeChain.Application.Interfaces.Admin.Actor;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CafeChain.Application.Authorization;

namespace CafeChain.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AuthorizationPolicyConstants.AdminDashboardApp)]
public sealed class AdminIntelligenceController : Controller
{
    private readonly IForecastService _forecast;
    private readonly ISupplierIntelligenceService _supplier;
    private readonly IAdminActorContextAccessor _actor;
    private readonly IAIService _ai;
    public AdminIntelligenceController(IForecastService forecast, ISupplierIntelligenceService supplier, IAdminActorContextAccessor actor, IAIService ai)
    { _forecast = forecast; _supplier = supplier; _actor = actor; _ai = ai; }

    [HttpGet]
    public async Task<IActionResult> Forecast(string seriesType, int storeId, int? entityId, int horizonDays = 7)
    {
        try { return Json(new { success = true, data = await _forecast.GetLatestAsync(_actor.Get(User), seriesType, storeId, entityId, horizonDays, HttpContext.RequestAborted) }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { success = false, message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { success = false, message = ex.Message }); }
    }

    [HttpGet]
    [RequirePermission("PurchaseAdvice.View")]
    [RequirePermission("SupplierQuality.View")]
    public async Task<IActionResult> CompareSuppliers(int storeId, int ingredientId, decimal requiredBaseQuantity)
    {
        try { return Json(new { success = true, data = await _supplier.CompareAsync(_actor.Get(User), storeId, ingredientId, requiredBaseQuantity, HttpContext.RequestAborted) }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { success = false, message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { success = false, message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { success = false, errorCode = "SUPPLIER_INTELLIGENCE_NOT_ENABLED", message = ex.Message }); }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ExplainForecast(string seriesType, int storeId, int? entityId, int horizonDays = 7, int pointIndex = 0)
    {
        try
        {
            var result = await _forecast.GetLatestAsync(_actor.Get(User), seriesType, storeId, entityId, horizonDays, HttpContext.RequestAborted);
            if (result == null || pointIndex < 0 || pointIndex >= result.Points.Count) return NotFound(new { success = false, message = "Không tìm thấy forecast còn hiệu lực." });
            var point = result.Points[pointIndex];
            var context = new CafeChain.Application.DTOs.AI.ForecastExplanationContextDto { RunId = result.ForecastRunId, ModelType = result.ModelType, TrainingToExclusive = result.TrainingToExclusive, PointForecast = point.PointForecast, LowerBound = point.LowerBound, UpperBound = point.UpperBound, Wape = result.Wape ?? 0, QualityStatus = result.QualityStatus, Warnings = result.Warnings };
            return Json(new { success = true, data = await _ai.ExplainForecastAsync(context, HttpContext.RequestAborted) });
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost, ValidateAntiForgeryToken]
    [RequirePermission(PermissionConstants.DashboardAiUse)]
    public async Task<IActionResult> ExplainSupplier(int storeId, int ingredientId, decimal requiredBaseQuantity, int supplierId)
    {
        try
        {
            var result = await _supplier.CompareAsync(_actor.Get(User), storeId, ingredientId, requiredBaseQuantity, HttpContext.RequestAborted);
            var candidate = result.Candidates.FirstOrDefault(x => x.SupplierId == supplierId);
            if (candidate == null) return NotFound(new { success = false, message = "Không tìm thấy kết quả nhà cung cấp." });
            var context = new CafeChain.Application.DTOs.AI.SupplierExplanationContextDto { SupplierId = candidate.SupplierId, TotalScore = candidate.Score ?? 0, Confidence = candidate.Confidence, Warnings = candidate.Warnings, ComponentScores = new Dictionary<string, decimal> { ["price"] = candidate.ComponentScores.Price ?? 0, ["onTime"] = candidate.ComponentScores.OnTime ?? 0, ["fill"] = candidate.ComponentScores.Fill ?? 0, ["quality"] = candidate.ComponentScores.Quality ?? 0, ["leadTime"] = candidate.ComponentScores.LeadTime ?? 0 } };
            return Json(new { success = true, data = await _ai.ExplainSupplierScoreAsync(context, HttpContext.RequestAborted) });
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
}
