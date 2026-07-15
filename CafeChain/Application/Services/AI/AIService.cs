using CafeChain.Application.DTOs.AI;
using CafeChain.Application.Interfaces.AI;
using CafeChain.Infrastructure.Configurations;
using CafeChain.Infrastrusture.Interfaces.Admin.Categories;
using CafeChain.Infrastrusture.Interfaces.Admin.Drinks;
using CafeChain.Infrastrusture.Interfaces.Admin.Sizes;
using CafeChain.Infrastrusture.Interfaces.Admin.Toppings;
using CafeChain.Models.Enums.Drink;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CafeChain.Application.Services.AI;

public sealed partial class AIService : IAIService
{
    private readonly IAdminCategoryRepository _categoryRepository;
    private readonly IAdminDrinkRepository _drinkRepository;
    private readonly IAdminSizeRepository _sizeRepository;
    private readonly IAdminToppingRepository _toppingRepository;
    private readonly IOllamaClient _ollama;
    private readonly IVisualSpecificationBuilder _visualSpecificationBuilder;
    private readonly AIOptions _options;
    private readonly ILogger<AIService> _logger;

    public AIService(
        IAdminCategoryRepository categoryRepository,
        IAdminDrinkRepository drinkRepository,
        IAdminSizeRepository sizeRepository,
        IAdminToppingRepository toppingRepository,
        IOllamaClient ollama,
        IVisualSpecificationBuilder visualSpecificationBuilder,
        IOptions<AIOptions> options,
        ILogger<AIService> logger)
    {
        _categoryRepository = categoryRepository;
        _drinkRepository = drinkRepository;
        _sizeRepository = sizeRepository;
        _toppingRepository = toppingRepository;
        _ollama = ollama;
        _visualSpecificationBuilder = visualSpecificationBuilder;
        _options = options.Value;
        _logger = logger;
    }

    public Task<OllamaHealthDTO> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return Task.FromResult(new OllamaHealthDTO { Message = "AIService đang bị tắt trong cấu hình." });
        return _ollama.CheckHealthAsync(cancellationToken);
    }

    private async Task<DrinkSuggestionResultDTO> SuggestDrinkLegacyAsync(
        DrinkSuggestionRequestDTO request,
        CancellationToken cancellationToken = default)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length is < 2 or > 200)
            return DrinkSuggestionFailure("Tên đồ uống phải từ 2 đến 200 ký tự.");
        var existingDrinks = (await _drinkRepository.GetAllDrinksAsync()).ToList();
        var nameKey = AISuggestionUniquenessPolicy.NormalizeTextKey(name);
        if (existingDrinks.Any(x => AISuggestionUniquenessPolicy.NormalizeTextKey(x.Name) == nameKey))
            return DrinkSuggestionFailure("Tên đồ uống đã tồn tại.");

        var category = (await _drinkRepository.GetDrinkCategoriesAsync())
            .FirstOrDefault(x => x.CategoryId == request.CategoryId && x.Active);
        var productType = (await _drinkRepository.GetProductTypesAsync())
            .FirstOrDefault(x => x.ProductTypeId == request.ProductTypeId && x.Active);
        if (category == null || productType == null)
            return DrinkSuggestionFailure("Danh mục hoặc loại sản phẩm không tồn tại/không hoạt động.");

        var code = await CreateUniqueCodeAsync(name, 50, existingDrinks.Select(x => x.DrinkCode),
            value => _drinkRepository.IsDrinkCodeExistsAsync(value));
        if (code.Length == 0)
            return DrinkSuggestionFailure("Không thể tạo mã đồ uống hợp lệ.");

        var handcrafted = string.Equals(productType.Code, "HANDCRAFTED", StringComparison.OrdinalIgnoreCase)
            || productType.ProductTypeId == (int)ProductTypeEnum.Handcrafted;
        var preferredCodes = handcrafted
            ? new[] { "S", "M", "L", "XL" }
            : new[] { "150ML", "200ML", "250ML", "300ML" };
        var expectedType = handcrafted ? SizeTypeEnum.Cup : SizeTypeEnum.Volume;
        var sizes = (await _sizeRepository.GetAllAsync())
            .Where(x => x.Active && x.SizeType == expectedType)
            .OrderBy(x => Array.IndexOf(preferredCodes, x.SizeCode.ToUpperInvariant()) is var rank && rank >= 0 ? rank : int.MaxValue)
            .ThenBy(x => x.SizeId)
            .Take(4)
            .Select(x => new AISuggestionReferenceDTO { Id = x.SizeId, Code = x.SizeCode, Name = x.Name })
            .ToList();

        var fallbackDescription = $"{name} thuộc danh mục {category.Name}, phù hợp với dòng sản phẩm {productType.Name}.";
        var result = new DrinkSuggestionResultDTO
        {
            Success = true,
            Message = "Đã tạo gợi ý. Vui lòng kiểm tra trước khi lưu.",
            DrinkCode = code,
            Description = fallbackDescription,
            Sizes = sizes,
            UsedFallback = true
        };

        var allowedToppings = handcrafted
            ? (await _toppingRepository.GetActiveAsync()).ToList()
            : [];
        if (!_options.Enabled || !string.Equals(_options.Provider, "Ollama", StringComparison.OrdinalIgnoreCase))
        {
            result.Warnings.Add("Ollama đang tắt; hệ thống giữ mô tả mẫu và không tự chọn topping.");
            return result;
        }

        var payload = JsonSerializer.Serialize(new
        {
            name,
            category = category.Name,
            productType = productType.Name,
            allowedToppings = allowedToppings.Select(x => new { x.ToppingCode, x.Name })
        });
        var prompt = """
            Bạn hỗ trợ viết nội dung cho CafeChain. Chỉ dùng dữ liệu trong JSON.
            Trả đúng JSON: {"description":"...","toppingCodes":["..."]}.
            Mô tả tiếng Việt ngắn gọn. toppingCodes tối đa 4 mã và chỉ được lấy từ allowedToppings.
            Không tạo ID, giá, mã đồ uống hoặc dữ liệu ngoài danh sách. Không đề nghị tự động lưu.
            """;
        var ollama = await _ollama.ChatAsync(prompt, payload, cancellationToken);
        if (!ollama.Success || string.IsNullOrWhiteSpace(ollama.Content))
        {
            result.Warnings.Add("Ollama không khả dụng; hệ thống dùng mô tả mẫu.");
            return result;
        }

        try
        {
            var ai = JsonSerializer.Deserialize<DrinkOllamaSuggestionDTO>(
                StripMarkdownFence(ollama.Content),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (ai == null || string.IsNullOrWhiteSpace(ai.Description) || ai.Description.Trim().Length > 1000)
                throw new JsonException("Invalid drink suggestion response.");

            var byCode = allowedToppings.ToDictionary(
                x => AISuggestionUniquenessPolicy.NormalizeCodeKey(x.ToppingCode),
                x => x);
            result.Toppings = ai.ToppingCodes
                .Select(AISuggestionUniquenessPolicy.NormalizeCodeKey)
                .Distinct()
                .Where(byCode.ContainsKey)
                .Take(4)
                .Select(x => byCode[x])
                .Select(x => new AISuggestionReferenceDTO { Id = x.ToppingId, Code = x.ToppingCode, Name = x.Name })
                .ToList();
            result.Description = ai.Description.Trim();
            result.UsedOllama = true;
            result.UsedFallback = false;
        }
        catch (JsonException)
        {
            result.Warnings.Add("Phản hồi Ollama không hợp lệ; hệ thống dùng mô tả mẫu.");
        }

        return result;
    }

    private async Task<SizeSuggestionResultDTO> SuggestSizeLegacyAsync(
        SizeSuggestionRequestDTO request,
        CancellationToken cancellationToken = default)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length is < 1 or > 50 || !Enum.IsDefined(request.SizeType))
            return new SizeSuggestionResultDTO { Message = "Tên hoặc loại size không hợp lệ." };
        var existingSizes = (await _sizeRepository.GetAllAsync()).ToList();
        var nameKey = AISuggestionUniquenessPolicy.NormalizeTextKey(name);
        if (existingSizes.Any(x => AISuggestionUniquenessPolicy.NormalizeTextKey(x.Name) == nameKey))
            return new SizeSuggestionResultDTO { Message = "Tên size đã tồn tại." };

        var baseCode = BuildSizeCode(name);
        var code = await CreateUniqueCodeAsync(baseCode, 20, existingSizes.Select(x => x.SizeCode), _sizeRepository.ExistsBySizeCodeAsync);
        return new SizeSuggestionResultDTO
        {
            Success = code.Length > 0,
            Message = code.Length > 0 ? "Đã tạo mã size. Vui lòng kiểm tra trước khi lưu." : "Không thể tạo mã size hợp lệ.",
            SizeCode = code
        };
    }

    private async Task<ToppingSuggestionResultDTO> SuggestToppingLegacyAsync(
        ToppingSuggestionRequestDTO request,
        CancellationToken cancellationToken = default)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length is < 2 or > 100 || request.Price is <= 0)
            return new ToppingSuggestionResultDTO { Message = "Tên hoặc giá topping không hợp lệ." };
        var existingToppings = (await _toppingRepository.GetAllAsync()).ToList();
        var nameKey = AISuggestionUniquenessPolicy.NormalizeTextKey(name);
        if (existingToppings.Any(x => AISuggestionUniquenessPolicy.NormalizeTextKey(x.Name) == nameKey))
            return new ToppingSuggestionResultDTO { Message = "Tên topping đã tồn tại." };

        var code = await CreateUniqueCodeAsync(name, 50, existingToppings.Select(x => x.ToppingCode),
            value => _toppingRepository.ExistsByToppingCodeAsync(value));
        return new ToppingSuggestionResultDTO
        {
            Success = code.Length > 0,
            Message = code.Length > 0 ? "Đã tạo mã topping. Vui lòng kiểm tra trước khi lưu." : "Không thể tạo mã topping hợp lệ.",
            ToppingCode = code
        };
    }

    public async Task<CategorySuggestionResultDTO> SuggestCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var existing = (await _categoryRepository.GetAllCategoriesAsync(cancellationToken)).ToList();
        cancellationToken.ThrowIfCancellationRequested();
        var candidates = new List<CategoryOllamaOptionDTO>();
        var usedOllama = false;

        if (_options.Enabled && string.Equals(_options.Provider, "Ollama", StringComparison.OrdinalIgnoreCase))
        {
            var payload = JsonSerializer.Serialize(new
            {
                ExistingCategories = existing.Select(x => new { x.Name, x.CategoryCode })
            });
            var ollama = await _ollama.ChatAsync(BuildCategoryPrompt(), payload, cancellationToken);
            if (ollama.Success && !string.IsNullOrWhiteSpace(ollama.Content))
            {
                try
                {
                    var response = JsonSerializer.Deserialize<CategoryOllamaResponseDTO>(
                        StripMarkdownFence(ollama.Content),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (response != null)
                    {
                        candidates.AddRange(response.Suggestions);
                        usedOllama = true;
                    }
                }
                catch (JsonException)
                {
                    _logger.LogInformation("Category AI suggestion returned invalid JSON; fallback will be used.");
                }
            }
        }

        var ollamaCandidateCount = candidates.Count;
        candidates.AddRange(CategoryFallbackCandidates());
        var usedNames = new HashSet<string>(existing.Select(x => AISuggestionUniquenessPolicy.NormalizeTextKey(x.Name)));
        var usedCodes = new HashSet<string>(existing.Select(x => AISuggestionUniquenessPolicy.NormalizeCodeKey(x.CategoryCode)));
        var options = new List<CategorySuggestionOptionDTO>();
        var acceptedOllamaCount = 0;
        var rejectedCount = 0;
        for (var candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
        {
            var candidate = candidates[candidateIndex];
            var name = candidate.Name?.Trim() ?? string.Empty;
            var icon = candidate.Icon?.Trim() ?? string.Empty;
            if (name.Length is < 2 or > 100 || icon.Length is < 1 or > 10
                || !usedNames.Add(AISuggestionUniquenessPolicy.NormalizeTextKey(name)))
            {
                rejectedCount++;
                continue;
            }
            var code = CreateUniqueCategoryCode(name, usedCodes);
            if (string.IsNullOrWhiteSpace(code))
            {
                rejectedCount++;
                continue;
            }
            options.Add(new() { Name = name, CategoryCode = code, Icon = icon });
            if (candidateIndex < ollamaCandidateCount) acceptedOllamaCount++;
            if (options.Count == 3) break;
        }

        options = AISuggestionUniquenessPolicy.FilterDistinctSuggestions(
            options, x => x.Name, x => x.CategoryCode,
            existing.Select(x => x.Name), existing.Select(x => x.CategoryCode), out var finalRejected);
        rejectedCount += finalRejected;

        return new CategorySuggestionResultDTO
        {
            Success = options.Count >= 2,
            Message = options.Count >= 2 ? "Đã tạo các lựa chọn danh mục." : "Không đủ lựa chọn danh mục hợp lệ.",
            Options = options,
            UsedOllama = usedOllama && acceptedOllamaCount > 0,
            UsedFallback = acceptedOllamaCount < options.Count,
            RejectedDuplicateCount = rejectedCount,
            Warnings = rejectedCount > 0 ? [$"Đã loại {rejectedCount} gợi ý trùng hoặc không hợp lệ."] : []
        };
    }

    private static string BuildCategoryPrompt() => """
        Bạn là trợ lý nội dung cho CafeChain. Dựa trên danh sách danh mục hiện có, đề xuất đúng 3 danh mục đồ uống mới bằng tiếng Việt.
        Không trùng tên hiện có. Mỗi lựa chọn chỉ gồm name và một emoji phù hợp trong icon.
        Không tạo mã, ID hoặc dữ liệu khác. Trả đúng JSON: {"suggestions":[{"name":"...","icon":"..."}]}.
        """;

    private static IEnumerable<CategoryOllamaOptionDTO> CategoryFallbackCandidates() =>
    [
        new() { Name = "Sinh tố", Icon = "🥑" },
        new() { Name = "Nước ép", Icon = "🍊" },
        new() { Name = "Đá xay", Icon = "🧊" },
        new() { Name = "Trà trái cây", Icon = "🍹" },
        new() { Name = "Đồ uống theo mùa", Icon = "✨" }
    ];

    private static string CreateUniqueCategoryCode(string name, HashSet<string> usedCodes)
    {
        var normalized = name.Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(character)) builder.Append(char.ToUpperInvariant(character));
        }
        var baseCode = AISuggestionUniquenessPolicy.NormalizeCodeKey(
            Regex.Replace(builder.ToString(), "[^A-Z0-9]", string.Empty));
        if (baseCode.Length > 30) baseCode = baseCode[..30];
        if (baseCode.Length < 2) return string.Empty;
        var code = baseCode;
        for (var suffix = 2; !usedCodes.Add(code); suffix++)
        {
            var suffixText = suffix.ToString(CultureInfo.InvariantCulture);
            code = baseCode[..Math.Min(baseCode.Length, 30 - suffixText.Length)] + suffixText;
        }
        return code;
    }

    private static DrinkSuggestionResultDTO DrinkSuggestionFailure(string message) => new()
    {
        Success = false,
        Message = message,
        UsedFallback = true
    };

    private static string BuildSizeCode(string name)
    {
        var key = AISuggestionUniquenessPolicy.NormalizeTextKey(name);
        return key switch
        {
            "SMALL" or "S" => "S",
            "MEDIUM" or "M" => "M",
            "LARGE" or "L" => "L",
            "EXTRALARGE" or "XL" => "XL",
            _ => Regex.IsMatch(key, @"^\d+ML$") ? key : name
        };
    }

    private static async Task<string> CreateUniqueCodeAsync(
        string value,
        int maxLength,
        IEnumerable<string> existingCodes,
        Func<string, Task<bool>> existsAsync)
    {
        var code = BuildCode(value, maxLength);
        if (code.Length == 0) return string.Empty;
        var normalizedExisting = existingCodes
            .Select(AISuggestionUniquenessPolicy.NormalizeCodeKey)
            .ToHashSet(StringComparer.Ordinal);
        if (!normalizedExisting.Contains(AISuggestionUniquenessPolicy.NormalizeCodeKey(code)) && !await existsAsync(code)) return code;

        for (var suffix = 2; suffix <= 999; suffix++)
        {
            var suffixText = $"_{suffix}";
            var prefixLength = maxLength - suffixText.Length;
            if (prefixLength <= 0) return string.Empty;
            var candidate = code[..Math.Min(code.Length, prefixLength)].TrimEnd('_') + suffixText;
            if (!normalizedExisting.Contains(AISuggestionUniquenessPolicy.NormalizeCodeKey(candidate)) && !await existsAsync(candidate)) return candidate;
        }
        return string.Empty;
    }

    private static string BuildCode(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var normalized = value.Trim().Replace('đ', 'd').Replace('Đ', 'D')
            .Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var pendingSeparator = false;
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsLetterOrDigit(character))
            {
                if (pendingSeparator && builder.Length > 0) builder.Append('_');
                builder.Append(char.ToUpperInvariant(character));
                pendingSeparator = false;
            }
            else
            {
                pendingSeparator = true;
            }
        }
        var result = builder.ToString().Trim('_');
        if (result.Length > maxLength) result = result[..maxLength].TrimEnd('_');
        return result;
    }

    private static string StripMarkdownFence(string content)
    {
        var trimmed = content.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal)) return trimmed;
        var firstLine = trimmed.IndexOf('\n');
        var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        return firstLine >= 0 && lastFence > firstLine ? trimmed[(firstLine + 1)..lastFence].Trim() : trimmed;
    }
}
