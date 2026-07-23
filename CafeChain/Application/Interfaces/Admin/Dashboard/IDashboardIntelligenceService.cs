using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Dashboard;

namespace CafeChain.Application.Interfaces.Admin.Dashboard;

public interface IDashboardIntelligenceService
{
    Task<DashboardIntentParseResultDto> ParseAsync(AdminActorContext actor, DashboardPromptRequestDto request, CancellationToken cancellationToken = default);
    Task<DashboardAnalysisResultDto> ExecuteAsync(AdminActorContext actor, DashboardIntentDto intent, CancellationToken cancellationToken = default);
    Task<DashboardExplanationResultDto> ExplainAsync(AdminActorContext actor, Guid analysisId, CancellationToken cancellationToken = default);
}
