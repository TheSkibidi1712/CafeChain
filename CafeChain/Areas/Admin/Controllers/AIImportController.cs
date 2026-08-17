using CafeChain.Application.Authorization;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.AIImport;
using CafeChain.Application.Interfaces.AIImport;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Areas.Admin.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AuthorizationPolicyConstants.AdminPanelAccess)]
public sealed class AIImportController : Controller
{
    private readonly IAIImportService _service;
    private readonly IAdminActorContextAccessor _actorContext;

    public AIImportController(IAIImportService service, IAdminActorContextAccessor actorContext)
    {
        _service = service;
        _actorContext = actorContext;
    }

    [HttpGet]
    [RequirePermission(PermissionConstants.AIImportView)]
    public IActionResult Index() => View();

    [HttpPost("/api/ai-import/analyze")]
    [ValidateAntiForgeryToken]
    [RequirePermission(PermissionConstants.AIImportUpload)]
    [AIImportRequestSizeLimit]
    public async Task<IActionResult> Analyze([FromForm] AIImportAnalyzeRequest request, CancellationToken cancellationToken)
    {
        var files = request.Files.Where(file => file != null).ToList();
        if (request.File != null && files.All(file => !ReferenceEquals(file, request.File))) files.Insert(0, request.File);
        return Result(await _service.AnalyzeAsync(files, request.EntityHint, request.UseOcr, _actorContext.Get(User), cancellationToken));
    }

    [HttpGet("/api/ai-import/ocr-capability")]
    [RequirePermission(PermissionConstants.AIImportView)]
    public async Task<IActionResult> OcrCapability(CancellationToken cancellationToken) =>
        Result(await _service.GetOcrCapabilityAsync(_actorContext.Get(User), cancellationToken));

    [HttpPost("/api/ai-import/{id:int}/reanalyze")]
    [ValidateAntiForgeryToken]
    [RequirePermission(PermissionConstants.AIImportAnalyze)]
    public async Task<IActionResult> Reanalyze(int id, [FromBody] AIImportReanalyzeRequest request, CancellationToken cancellationToken) =>
        Result(await _service.ReanalyzeAsync(id, request, _actorContext.Get(User), cancellationToken));

    [HttpGet("/api/ai-import/{id:int}")]
    [RequirePermission(PermissionConstants.AIImportView)]
    public async Task<IActionResult> Get(int id, int? groupId, string? status, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default) =>
        Result(await _service.GetSessionAsync(id, groupId, status, page, pageSize, _actorContext.Get(User), cancellationToken));

    [HttpGet("/api/ai-import/{id:int}/editor-options")]
    [RequirePermission(PermissionConstants.AIImportView)]
    public async Task<IActionResult> EditorOptions(int id, CancellationToken cancellationToken) =>
        Result(await _service.GetEditorOptionsAsync(id, _actorContext.Get(User), cancellationToken));

    [HttpPatch("/api/ai-import/{id:int}/groups/{groupId:int}")]
    [ValidateAntiForgeryToken]
    [RequirePermission(PermissionConstants.AIImportAnalyze)]
    public async Task<IActionResult> PatchGroup(int id, int groupId, [FromBody] AIImportGroupPatchRequest request, CancellationToken cancellationToken) =>
        Result(await _service.UpdateGroupAsync(id, groupId, request, _actorContext.Get(User), cancellationToken));

    [HttpPatch("/api/ai-import/{id:int}/items/{itemId:int}")]
    [ValidateAntiForgeryToken]
    [RequirePermission(PermissionConstants.AIImportAnalyze)]
    public async Task<IActionResult> PatchItem(int id, int itemId, [FromBody] AIImportItemPatchRequest request, CancellationToken cancellationToken) =>
        Result(await _service.UpdateItemAsync(id, itemId, request, _actorContext.Get(User), cancellationToken));

    [HttpPost("/api/ai-import/{id:int}/confirm")]
    [ValidateAntiForgeryToken]
    [RequirePermission(PermissionConstants.AIImportConfirm)]
    public async Task<IActionResult> Confirm(int id, [FromBody] AIImportConfirmRequest request, CancellationToken cancellationToken) =>
        Result(await _service.ConfirmAsync(id, Request.Headers["Idempotency-Key"].FirstOrDefault(), request, _actorContext.Get(User), cancellationToken));

    [HttpPost("/api/ai-import/{id:int}/cancel")]
    [ValidateAntiForgeryToken]
    [RequirePermission(PermissionConstants.AIImportCancel)]
    public async Task<IActionResult> Cancel(int id, [FromBody] AIImportCancelRequest request, CancellationToken cancellationToken) =>
        Result(await _service.CancelAsync(id, request, _actorContext.Get(User), cancellationToken));

    [HttpDelete("/api/ai-import/{id:int}/sources/{sourceDocumentId:int}")]
    [ValidateAntiForgeryToken]
    [RequirePermission(PermissionConstants.AIImportAnalyze)]
    public async Task<IActionResult> RemoveSource(int id, int sourceDocumentId, int expectedPreviewVersion, CancellationToken cancellationToken) =>
        Result(await _service.RemoveSourceAsync(id, sourceDocumentId, expectedPreviewVersion, _actorContext.Get(User), cancellationToken));

    [HttpGet("/api/ai-import/history")]
    [RequirePermission(PermissionConstants.AIImportHistory)]
    public async Task<IActionResult> History(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default) =>
        Result(await _service.GetHistoryAsync(page, pageSize, _actorContext.Get(User), cancellationToken));

    private IActionResult Result<T>(AIImportOperationResult<T> result) =>
        StatusCode(result.StatusCode, new
        {
            success = result.Success,
            code = result.ErrorCode,
            message = result.Message,
            data = result.Data,
            details = result.Details
        });
}
