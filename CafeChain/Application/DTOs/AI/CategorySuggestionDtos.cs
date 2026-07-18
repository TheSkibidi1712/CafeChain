using System.ComponentModel.DataAnnotations;

namespace CafeChain.Application.DTOs.AI;

public sealed class CategorySuggestionRequestDTO
{
    [StringLength(100)]
    public string? CurrentName { get; set; }

    [StringLength(30)]
    public string? CurrentCategoryCode { get; set; }

    [StringLength(10)]
    public string? CurrentIcon { get; set; }
}

public sealed class CategorySuggestionResultDTO
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public List<CategorySuggestionOptionDTO> Options { get; set; } = [];
    public bool UsedOllama { get; set; }
    public bool UsedFallback { get; set; }
    public int RejectedDuplicateCount { get; set; }
    public List<string> Warnings { get; set; } = [];
}

public sealed class CategorySuggestionOptionDTO
{
    public string Name { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
}

public sealed class CategoryOllamaResponseDTO
{
    public List<CategoryOllamaOptionDTO> Suggestions { get; set; } = [];
}

public sealed class CategoryOllamaOptionDTO
{
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
}
