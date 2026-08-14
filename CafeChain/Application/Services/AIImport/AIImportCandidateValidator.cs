using CafeChain.Application.DTOs.AIImport;
using CafeChain.Application.Options;
using CafeChain.Models.AIImport;
using Microsoft.Extensions.Options;

namespace CafeChain.Application.Services.AIImport;

public sealed record AIImportCandidateValidationResult(
    Dictionary<string, string?> NormalizedData,
    List<AIImportErrorDto> Issues,
    string Status);

public sealed class AIImportCandidateValidator(
    IAIImportSchemaRegistry schemas,
    IOptions<AIImportOptions> options)
{
    private readonly AIImportOptions _options = options.Value;

    public AIImportCandidateValidationResult Validate(
        AIImportEntityType entityType,
        IReadOnlyDictionary<string, string?> values,
        decimal confidence,
        IEnumerable<AIImportErrorDto> sourceIssues,
        bool manualReviewConfirmed,
        string currentStatus,
        string action,
        string? aiErrorCode = null)
    {
        var issues = sourceIssues.ToList();
        Dictionary<string, string?> normalized;
        if (entityType == AIImportEntityType.Unknown)
        {
            normalized = new Dictionary<string, string?>(values, StringComparer.OrdinalIgnoreCase);
            issues.Add(AIImportValidationContract.Issue(aiErrorCode ?? "KHÔNG_XÁC_ĐỊNH_SCHEMA",
                "Cần chọn entity và mapping trước khi Confirm.", AIImportIssueSeverities.Review,
                resolution: AIImportIssueResolutions.RemapGroup));
        }
        else
        {
            normalized = schemas.Normalize(entityType, values);
            issues.AddRange(schemas.Validate(entityType, normalized));
            if (confidence < _options.ReviewConfidenceThreshold)
                issues.Add(AIImportValidationContract.Issue("AI_CONFIDENCE_THẤP",
                    "Bản ghi có độ tin cậy thấp và phải được đối chiếu với nguồn.",
                    AIImportIssueSeverities.Review,
                    resolution: AIImportIssueResolutions.ManualReview,
                    metadata: new Dictionary<string, object?> { ["confidence"] = confidence }));
        }

        issues = issues.GroupBy(issue => new { issue.Code, issue.Field, issue.Severity })
            .Select(group => group.First()).ToList();
        return new AIImportCandidateValidationResult(
            normalized,
            issues,
            AIImportValidationContract.ResolveStatus(currentStatus, action, issues, manualReviewConfirmed));
    }
}
