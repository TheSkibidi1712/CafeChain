using System.ComponentModel.DataAnnotations;

namespace CafeChain.Application.DTOs.AI;

public sealed class VisualSpecificationDTO
{
    public string PrimarySubject { get; set; } = string.Empty;
    public string SubjectType { get; set; } = string.Empty;
    public List<string> MainIngredients { get; set; } = [];
    public List<string> SecondaryObjects { get; set; } = [];
    public List<string> DominantColors { get; set; } = [];
    public string Background { get; set; } = string.Empty;
    public string Composition { get; set; } = string.Empty;
    public string CameraAngle { get; set; } = string.Empty;
    public string Lighting { get; set; } = string.Empty;
    public string ImageStyle { get; set; } = string.Empty;
    public string StyleProfile { get; set; } = string.Empty;
    public string Mood { get; set; } = string.Empty;
    public string Container { get; set; } = string.Empty;
    public string Surface { get; set; } = string.Empty;
    public List<string> Garnishes { get; set; } = [];
    public List<string> Props { get; set; } = [];
    public string Lens { get; set; } = string.Empty;
    public string DepthOfField { get; set; } = string.Empty;
    public string ReferencePurpose { get; set; } = string.Empty;
    public string Orientation { get; set; } = "square";
    public List<string> RequiredKeywords { get; set; } = [];
    public List<string> ForbiddenKeywords { get; set; } = [];
    public List<string> PexelsQueries { get; set; } = [];
    public string ComfyPositivePrompt { get; set; } = string.Empty;
    public string ComfyNegativePrompt { get; set; } = string.Empty;
}

public sealed class AIReferenceSearchRequestDTO
{
    public Guid RequestId { get; set; }
    public Guid SuggestionId { get; set; }
    [Required, StringLength(30)] public string EntityType { get; set; } = string.Empty;
    [Required] public VisualSpecificationDTO VisualSpecification { get; set; } = new();
    [MaxLength(50)] public List<long> ExcludedPhotoIds { get; set; } = [];
}

public sealed class AIReferenceSearchResultDTO
{
    public bool Success { get; set; }
    public Guid RequestId { get; set; }
    public Guid SuggestionId { get; set; }
    public string Stage { get; set; } = "PexelsValidation";
    public string Message { get; set; } = string.Empty;
    public bool Retryable { get; set; }
    public string? FailureCode { get; set; }
    public bool TextFallbackAvailable { get; set; }
    public List<PexelsImageCandidateDTO> Candidates { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}

public sealed class PexelsImageCandidateDTO
{
    public long PhotoId { get; set; }
    public string PreviewUrl { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string? Photographer { get; set; }
    public string? PhotographerUrl { get; set; }
    public string? Alt { get; set; }
    public string? AverageColor { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public double Score { get; set; }
    public string MatchedQuery { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = [];
}

public sealed class AIGenerateFromReferenceRequestDTO
{
    public Guid RequestId { get; set; }
    public Guid SuggestionId { get; set; }
    [Required, StringLength(30)] public string EntityType { get; set; } = string.Empty;
    [Range(1, long.MaxValue)] public long PhotoId { get; set; }
    [StringLength(200)] public string? MatchedQuery { get; set; }
    [Required] public VisualSpecificationDTO VisualSpecification { get; set; } = new();
    [StringLength(100)] public string FileNamePrefix { get; set; } = "cafechain_ai";
}

public sealed class AIUsePexelsImageRequestDTO
{
    public Guid RequestId { get; set; }
    public Guid SuggestionId { get; set; }
    [Required, StringLength(30)] public string EntityType { get; set; } = string.Empty;
    [Range(1, long.MaxValue)] public long PhotoId { get; set; }
    [StringLength(200)] public string? MatchedQuery { get; set; }
    [Required] public VisualSpecificationDTO VisualSpecification { get; set; } = new();
    [StringLength(100)] public string FileNamePrefix { get; set; } = "cafechain_pexels";
}

public sealed class AIGenerateFromPromptRequestDTO
{
    public Guid RequestId { get; set; }
    public Guid SuggestionId { get; set; }
    [Required, StringLength(30)] public string EntityType { get; set; } = string.Empty;
    [Required] public VisualSpecificationDTO VisualSpecification { get; set; } = new();
    [StringLength(100)] public string FileNamePrefix { get; set; } = "cafechain_ai";
}

public sealed class AIGeneratedImageDTO
{
    public Guid ImageId { get; set; } = Guid.NewGuid();
    public string Base64Data { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public bool TechnicalValidationPassed { get; set; }
    public string Source { get; set; } = "ComfyUI";
    public long? ExternalPhotoId { get; set; }
    public string? AttributionText { get; set; }
    public List<string> Warnings { get; set; } = [];
}

public sealed class AIGenerateFromReferenceResultDTO
{
    public bool Success { get; set; }
    public Guid RequestId { get; set; }
    public Guid SuggestionId { get; set; }
    public string Stage { get; set; } = "GeneratedImageValidation";
    public string Message { get; set; } = string.Empty;
    public bool Retryable { get; set; }
    public PexelsImageCandidateDTO? PexelsReference { get; set; }
    public List<AIGeneratedImageDTO> GeneratedImages { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}
