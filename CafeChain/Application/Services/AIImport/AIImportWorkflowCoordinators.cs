using CafeChain.Models.AIImport;

namespace CafeChain.Application.Services.AIImport;

public sealed class AIImportAnalysisCoordinator
{
    public string ClaimReanalysis(ImportSession session)
    {
        var previous = session.Status;
        session.Status = AIImportSessionStatuses.Analyzing;
        return previous;
    }
}

public sealed class AIImportPreviewMutationCoordinator
{
    public AIImportValidationScope GroupScope(int groupId, AIImportEntityType previousEntityType) =>
        AIImportValidationScope.ForGroup(groupId, previousEntityType);

    public AIImportValidationScope ItemScope(int itemId, AIImportEntityType entityType, string previousBusinessKey) =>
        AIImportValidationScope.ForItem(itemId, entityType, previousBusinessKey);

    public void AdvancePreview(ImportSession session) => session.PreviewVersion++;
}

public sealed record AIImportExecutionEntry(ImportGroup Group, ImportItem Item);

public sealed class AIImportConfirmCoordinator(AIImportEntityRegistry entityRegistry)
{
    public IReadOnlyList<ImportItem> FindBlockers(ImportSession session) => session.Groups
        .SelectMany(group => group.Items)
        .Where(item => item.Action != AIImportActions.Skip
                       && (item.Status is AIImportItemStatuses.Error or AIImportItemStatuses.ReviewRequired
                           || (item.Status == AIImportItemStatuses.Warning && !item.WarningsAcknowledged)))
        .ToList();

    public IReadOnlyList<AIImportExecutionEntry> BuildExecutionPlan(ImportSession session) => session.Groups
        .OrderBy(group => entityRegistry.Get(group.EntityType).DependencyOrder)
        .ThenBy(group => group.ImportGroupId)
        .SelectMany(group => group.Items.OrderBy(item => item.SourceRow)
            .Select(item => new AIImportExecutionEntry(group, item)))
        .ToList();

    public IReadOnlyList<string> RequiredCreatePermissions(ImportSession session) => session.Groups
        .SelectMany(group => group.Items.Where(item => item.Action == AIImportActions.Create)
            .Select(_ => entityRegistry.Get(group.EntityType).CreatePermission))
        .Distinct(StringComparer.Ordinal)
        .ToList();
}

public sealed class AIImportSessionQuery
{
    public (int Page, int PageSize) NormalizePage(int page, int pageSize, int defaultPageSize, int maxPageSize) =>
        (Math.Max(1, page), pageSize <= 0 ? defaultPageSize : Math.Min(pageSize, maxPageSize));

    public IOrderedQueryable<ImportItem> OrderPreviewItems(IQueryable<ImportItem> query) => query
        .OrderBy(x => x.ImportGroupId)
        .ThenBy(x => x.Status == AIImportItemStatuses.Error ? 0
            : x.Status == AIImportItemStatuses.ReviewRequired ? 1
            : x.Status == AIImportItemStatuses.Warning && !x.WarningsAcknowledged ? 2
            : x.Status == AIImportItemStatuses.Valid || x.Status == AIImportItemStatuses.Warning ? 3
            : x.Status == AIImportItemStatuses.Skipped ? 4 : 5)
        .ThenBy(x => x.SourceRow)
        .ThenBy(x => x.ImportItemId);
}
