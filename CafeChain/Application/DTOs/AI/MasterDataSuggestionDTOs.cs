using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using CafeChain.Models.Enums.Drink;

namespace CafeChain.Application.DTOs.AI;

public enum AISuggestionGenerationMode
{
    New = 0,
    Develop = 1,
    Variant = 2
}

public sealed class AISuggestionHistoryItemDTO
{
    [StringLength(200)] public string Name { get; set; } = string.Empty;
    [StringLength(500)] public string? Description { get; set; }
    [StringLength(500)] public string? ImageConcept { get; set; }
}

internal sealed class AISuggestionReferenceDTO
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class DrinkSuggestionRequestDTO
{
    public AISuggestionGenerationMode GenerationMode { get; set; } = AISuggestionGenerationMode.New;
    [MaxLength(30)] public List<AISuggestionHistoryItemDTO> PreviousSuggestions { get; set; } = [];
    [StringLength(200)] public string? Idea { get; set; }
    [StringLength(50)] public string? CurrentDrinkCode { get; set; }
    [StringLength(200)] public string? CurrentName { get; set; }
    [StringLength(1000)] public string? CurrentDescription { get; set; }
    public int? CurrentCategoryId { get; set; }
    public int? CurrentProductTypeId { get; set; }

    // Compatibility for the private legacy implementation; never serialized.
    [JsonIgnore] internal string? Name => CurrentName;
    [JsonIgnore] internal int CategoryId => CurrentCategoryId ?? 0;
    [JsonIgnore] internal int ProductTypeId => CurrentProductTypeId ?? 0;
}

public sealed class DrinkSuggestionResultDTO
{
    public bool Success { get; set; }
    public Guid RequestId { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<DrinkSuggestionOptionDTO> Options { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public bool UsedOllama { get; set; }
    public bool UsedFallback { get; set; }

    [JsonIgnore] internal bool CanApply { get; set; }
    [JsonIgnore] internal string Name { get; set; } = string.Empty;
    [JsonIgnore] internal string DrinkCode { get; set; } = string.Empty;
    [JsonIgnore] internal string Description { get; set; } = string.Empty;
    [JsonIgnore] internal List<AISuggestionReferenceDTO> Sizes { get; set; } = [];
    [JsonIgnore] internal List<AISuggestionReferenceDTO> Toppings { get; set; } = [];
}

public sealed class DrinkSuggestionOptionDTO
{
    public Guid SuggestionId { get; set; } = Guid.NewGuid();
    public string EntityType { get; set; } = "Drink";
    public string Title { get; set; } = string.Empty;
    public bool CanApply { get; set; }
    public DrinkSuggestionFieldsDTO Fields { get; set; } = new();
    public VisualSpecificationDTO VisualSpecification { get; set; } = new();
    public string Persona { get; set; } = string.Empty;
    public int CreativityScore { get; set; }
    public int RelevanceScore { get; set; }
    public string Reason { get; set; } = string.Empty;
    public List<string> DuplicateSignals { get; set; } = [];
}

public sealed class DrinkSuggestionFieldsDTO
{
    public string DrinkCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int ProductTypeId { get; set; }
    public string ProductTypeName { get; set; } = string.Empty;
    public string ImagePrompt { get; set; } = string.Empty;
}

public sealed class SizeSuggestionRequestDTO
{
    public AISuggestionGenerationMode GenerationMode { get; set; } = AISuggestionGenerationMode.New;
    [MaxLength(30)] public List<AISuggestionHistoryItemDTO> PreviousSuggestions { get; set; } = [];
    [StringLength(200)] public string? Idea { get; set; }
    [StringLength(20)] public string? CurrentSizeCode { get; set; }
    [StringLength(50)] public string? CurrentName { get; set; }
    [StringLength(300)] public string? CurrentDescription { get; set; }
    public SizeTypeEnum? CurrentSizeType { get; set; }

    [JsonIgnore] internal string? Name => CurrentName;
    [JsonIgnore] internal SizeTypeEnum SizeType => CurrentSizeType ?? default;
}

public sealed class SizeSuggestionResultDTO
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<SizeSuggestionOptionDTO> Options { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public bool UsedOllama { get; set; }
    public bool UsedFallback { get; set; }

    [JsonIgnore] internal bool CanApply { get; set; }
    [JsonIgnore] internal string SizeCode { get; set; } = string.Empty;
}

public sealed class SizeSuggestionOptionDTO
{
    public string Title { get; set; } = string.Empty;
    public bool CanApply { get; set; }
    public SizeSuggestionFieldsDTO Fields { get; set; } = new();
    public string Persona { get; set; } = string.Empty;
    public int CreativityScore { get; set; }
    public int RelevanceScore { get; set; }
    public string Reason { get; set; } = string.Empty;
    public List<string> DuplicateSignals { get; set; } = [];
}

public sealed class SizeSuggestionFieldsDTO
{
    public string SizeCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public SizeTypeEnum SizeType { get; set; }
    public bool Active { get; set; } = true;
}

public sealed class ToppingSuggestionRequestDTO
{
    public AISuggestionGenerationMode GenerationMode { get; set; } = AISuggestionGenerationMode.New;
    [MaxLength(30)] public List<AISuggestionHistoryItemDTO> PreviousSuggestions { get; set; } = [];
    [StringLength(200)] public string? Idea { get; set; }
    [StringLength(50)] public string? CurrentToppingCode { get; set; }
    [StringLength(100)] public string? CurrentName { get; set; }
    [Range(typeof(decimal), "1000", "999999999999")] public decimal? CurrentPrice { get; set; }

    [JsonIgnore] internal string? Name => CurrentName;
    [JsonIgnore] internal decimal? Price => CurrentPrice;
}

public sealed class ToppingSuggestionResultDTO
{
    public bool Success { get; set; }
    public Guid RequestId { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<ToppingSuggestionOptionDTO> Options { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public bool UsedOllama { get; set; }
    public bool UsedFallback { get; set; }

    [JsonIgnore] internal bool CanApply { get; set; }
    [JsonIgnore] internal string ToppingCode { get; set; } = string.Empty;
}

public sealed class ToppingSuggestionOptionDTO
{
    public Guid SuggestionId { get; set; } = Guid.NewGuid();
    public string EntityType { get; set; } = "Topping";
    public string Title { get; set; } = string.Empty;
    public bool CanApply { get; set; }
    public ToppingSuggestionFieldsDTO Fields { get; set; } = new();
    public VisualSpecificationDTO VisualSpecification { get; set; } = new();
    public string Persona { get; set; } = string.Empty;
    public int CreativityScore { get; set; }
    public int RelevanceScore { get; set; }
    public string Reason { get; set; } = string.Empty;
    public List<string> DuplicateSignals { get; set; } = [];
}

public sealed class ToppingSuggestionFieldsDTO
{
    public string ToppingCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool Active { get; set; } = true;
    public string ImagePrompt { get; set; } = string.Empty;
}

internal sealed class DrinkOllamaOptionsDTO { public List<DrinkOllamaSuggestionDTO> Options { get; set; } = []; }
internal sealed class DrinkOllamaSuggestionDTO
{
    public string? Name { get; set; }
    public string? CategoryCode { get; set; }
    public string? ProductTypeCode { get; set; }
    public string? Description { get; set; }
    public string? ImagePrompt { get; set; }
    public List<string> ToppingCodes { get; set; } = [];
}

internal sealed class SizeOllamaOptionsDTO { public List<SizeOllamaSuggestionDTO> Options { get; set; } = []; }
internal sealed class SizeOllamaSuggestionDTO
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? SizeType { get; set; }
}

internal sealed class ToppingOllamaOptionsDTO { public List<ToppingOllamaSuggestionDTO> Options { get; set; } = []; }
internal sealed class ToppingOllamaSuggestionDTO
{
    public string? Name { get; set; }
    public string? ImagePrompt { get; set; }
}
