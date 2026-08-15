using CafeChain.Application.DTOs.AIImport;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Models.AIImport;

namespace CafeChain.Application.Interfaces.AIImport;

public interface IAIImportService
{
    Task<AIImportOperationResult<AIImportSessionDto>> AnalyzeAsync(
        IFormFile? file,
        AIImportEntityType? entityHint,
        AdminActorContext actor,
        CancellationToken cancellationToken);

    Task<AIImportOperationResult<AIImportSessionDto>> ReanalyzeAsync(
        int sessionId,
        AIImportReanalyzeRequest request,
        AdminActorContext actor,
        CancellationToken cancellationToken);

    Task<AIImportOperationResult<AIImportSessionDto>> GetSessionAsync(
        int sessionId,
        int? groupId,
        string? status,
        int page,
        int pageSize,
        AdminActorContext actor,
        CancellationToken cancellationToken);

    Task<AIImportOperationResult<AIImportEditorOptionsDto>> GetEditorOptionsAsync(
        int sessionId,
        AdminActorContext actor,
        CancellationToken cancellationToken);

    Task<AIImportOperationResult<AIImportSessionDto>> UpdateGroupAsync(
        int sessionId,
        int groupId,
        AIImportGroupPatchRequest request,
        AdminActorContext actor,
        CancellationToken cancellationToken);

    Task<AIImportOperationResult<AIImportSessionDto>> UpdateItemAsync(
        int sessionId,
        int itemId,
        AIImportItemPatchRequest request,
        AdminActorContext actor,
        CancellationToken cancellationToken);

    Task<AIImportOperationResult<AIImportConfirmResultDto>> ConfirmAsync(
        int sessionId,
        string? idempotencyKey,
        AIImportConfirmRequest request,
        AdminActorContext actor,
        CancellationToken cancellationToken);

    Task<AIImportOperationResult<AIImportSessionDto>> CancelAsync(
        int sessionId,
        AIImportCancelRequest request,
        AdminActorContext actor,
        CancellationToken cancellationToken);

    Task<AIImportOperationResult<AIImportHistoryDto>> GetHistoryAsync(
        int page,
        int pageSize,
        AdminActorContext actor,
        CancellationToken cancellationToken);
}
