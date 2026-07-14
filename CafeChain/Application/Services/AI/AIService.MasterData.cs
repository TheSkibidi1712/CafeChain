using System.Text.Json;
using System.Text.RegularExpressions;
using CafeChain.Application.DTOs.AI;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Drink;

namespace CafeChain.Application.Services.AI;

public sealed partial class AIService
{
    private const int MaximumMasterOptions = 3;
    private const decimal DefaultToppingPrice = 7000m;

    private static readonly (string Name, string CategoryCode, string ProductTypeCode, string Description)[] DrinkFallbacks =
    [
        ("Cà phê sữa đá", "COFFEE", "HANDCRAFTED", "Cà phê sữa đá đậm vị, cân bằng giữa cà phê và sữa."),
        ("Bạc xỉu", "COFFEE", "HANDCRAFTED", "Bạc xỉu thơm béo, phù hợp dùng nóng hoặc lạnh."),
        ("Cà phê đen đá", "COFFEE", "HANDCRAFTED", "Cà phê đen đá nguyên bản với hương vị mạnh mẽ."),
        ("Trà sữa trân châu", "TRASUA", "HANDCRAFTED", "Trà sữa thơm dịu, vị béo nhẹ và phù hợp dùng lạnh."),
        ("Trà sữa đường đen", "TRASUA", "HANDCRAFTED", "Trà sữa đường đen thơm caramel, vị ngọt hài hòa."),
        ("Trà đào cam sả", "NUOCNGOT", "HANDCRAFTED", "Trà đào cam sả thanh mát, phù hợp dùng lạnh."),
        ("Nước cam chai 300ml", "NUOCNGOT", "RETAIL", "Nước cam đóng chai dung tích 300ml, tiện lợi để mang theo."),
        ("Trà chanh chai 300ml", "NUOCNGOT", "RETAIL", "Trà chanh đóng chai vị thanh nhẹ, dùng ngon khi uống lạnh."),
        ("Cà phê sữa chai 250ml", "COFFEE", "RETAIL", "Cà phê sữa đóng chai dung tích 250ml, tiện lợi và đậm vị.")
    ];

    private static readonly (string Name, string Description, SizeTypeEnum Type)[] SizeFallbacks =
    [
        ("M", "Kích thước trung bình dành cho đồ uống pha chế.", SizeTypeEnum.Cup),
        ("L", "Kích thước lớn dành cho đồ uống pha chế.", SizeTypeEnum.Cup),
        ("XL", "Kích thước rất lớn dành cho đồ uống pha chế.", SizeTypeEnum.Cup),
        ("250ml", "Dung tích 250ml dành cho sản phẩm đóng chai.", SizeTypeEnum.Volume),
        ("300ml", "Dung tích 300ml dành cho sản phẩm đóng chai.", SizeTypeEnum.Volume),
        ("350ml", "Dung tích 350ml dành cho sản phẩm đóng chai.", SizeTypeEnum.Volume),
        ("500ml", "Dung tích 500ml dành cho sản phẩm bán sẵn.", SizeTypeEnum.Volume),
        ("700ml", "Dung tích 700ml dành cho sản phẩm bán sẵn cỡ lớn.", SizeTypeEnum.Volume)
    ];

    private static readonly string[] ToppingFallbacks =
    [
        "Trân châu đen", "Trân châu hoàng kim", "Thạch phô mai", "Pudding trứng",
        "Thạch cà phê", "Thạch dừa", "Kem cheese", "Nha đam", "Hạt thủy tinh"
    ];

    public async Task<DrinkSuggestionResultDTO> SuggestDrinkAsync(
        DrinkSuggestionRequestDTO request,
        CancellationToken cancellationToken = default)
    {
        var drinks = (await _drinkRepository.GetAllDrinksAsync()).ToList();
        var categories = (await _drinkRepository.GetDrinkCategoriesAsync()).Where(x => x.Active).ToList();
        var productTypes = (await _drinkRepository.GetProductTypesAsync()).Where(x => x.Active).ToList();
        if (categories.Count == 0 || productTypes.Count == 0)
            return DrinkFailure("Không có Category hoặc ProductType đang hoạt động để tạo gợi ý.");

        var candidates = new List<(DrinkOllamaSuggestionDTO Value, bool FromOllama)>();
        var currentName = CleanSuggestionText(request.CurrentName, 200);
        if (currentName != null)
        {
            var currentCategory = categories.FirstOrDefault(x => x.CategoryId == request.CurrentCategoryId)
                ?? ResolveCategoryForName(categories, currentName);
            var currentProductType = productTypes.FirstOrDefault(x => x.ProductTypeId == request.CurrentProductTypeId)
                ?? ResolveProductTypeForName(productTypes, currentName);
            if (currentCategory != null && currentProductType != null)
            {
                candidates.Add((new DrinkOllamaSuggestionDTO
                {
                    Name = currentName,
                    CategoryCode = currentCategory.CategoryCode,
                    ProductTypeCode = currentProductType.Code,
                    Description = CleanSuggestionText(request.CurrentDescription, 1000),
                    ImagePrompt = $"{currentName}, {currentCategory.Name}, {currentProductType.Name}, premium cafe product"
                }, false));
            }
        }

        var ollamaOptions = await RequestDrinkOptionsAsync(request, categories, productTypes, cancellationToken);
        candidates.AddRange(ollamaOptions.Select(x => (x, true)));
        candidates.AddRange(DrinkFallbacks.Select(x => (new DrinkOllamaSuggestionDTO
        {
            Name = x.Name,
            CategoryCode = x.CategoryCode,
            ProductTypeCode = x.ProductTypeCode,
            Description = x.Description,
            ImagePrompt = $"{x.Name}, premium Vietnamese cafe beverage"
        }, false)));

        var existingNames = new HashSet<string>(drinks.Select(x => AISuggestionUniquenessPolicy.NormalizeTextKey(x.Name)));
        var reservedCodes = new HashSet<string>(drinks.Select(x => AISuggestionUniquenessPolicy.NormalizeCodeKey(x.DrinkCode)));
        var options = new List<DrinkSuggestionOptionDTO>();
        var usedOllama = false;
        var usedFallback = false;
        var rejected = 0;

        foreach (var candidate in candidates)
        {
            if (options.Count == MaximumMasterOptions) break;
            var name = CleanSuggestionText(candidate.Value.Name, 200);
            var category = FindCategory(categories, candidate.Value.CategoryCode);
            var productType = FindProductType(productTypes, candidate.Value.ProductTypeCode);
            if (name == null || category == null || productType == null || !existingNames.Add(AISuggestionUniquenessPolicy.NormalizeTextKey(name)))
            {
                rejected++;
                continue;
            }

            var code = await CreateReservedCodeAsync(name, 50, reservedCodes,
                value => _drinkRepository.IsDrinkCodeExistsAsync(value));
            if (code.Length == 0) { rejected++; continue; }
            var description = CleanSuggestionText(candidate.Value.Description, 1000)
                ?? $"{name} thuộc danh mục {category.Name}, phù hợp với dòng sản phẩm {productType.Name}.";
            var imagePrompt = CleanSuggestionText(candidate.Value.ImagePrompt, 500)
                ?? $"{name}, {category.Name}, {productType.Name}, premium cafe product";

            options.Add(new DrinkSuggestionOptionDTO
            {
                Title = name,
                CanApply = true,
                Fields = new DrinkSuggestionFieldsDTO
                {
                    Name = name,
                    DrinkCode = code,
                    Description = description,
                    CategoryId = category.CategoryId,
                    CategoryName = category.Name,
                    ProductTypeId = productType.ProductTypeId,
                    ProductTypeName = productType.Name,
                    ImagePrompt = imagePrompt
                },
                VisualSpecification = _visualSpecificationBuilder.BuildDrink(name, description, imagePrompt)
            });
            usedOllama |= candidate.FromOllama;
            usedFallback |= !candidate.FromOllama;
        }

        if (options.Count == 0) return DrinkFailure("Không còn gợi ý đồ uống khác biệt với dữ liệu hiện có.");
        var result = new DrinkSuggestionResultDTO
        {
            Success = true,
            RequestId = Guid.NewGuid(),
            Message = $"Đã tạo {options.Count} gợi ý đồ uống.",
            Options = options,
            UsedOllama = usedOllama,
            UsedFallback = usedFallback
        };
        if (rejected > 0) result.Warnings.Add($"Đã loại {rejected} lựa chọn trùng hoặc không hợp lệ.");
        return result;
    }

    public async Task<SizeSuggestionResultDTO> SuggestSizeAsync(
        SizeSuggestionRequestDTO request,
        CancellationToken cancellationToken = default)
    {
        var sizes = (await _sizeRepository.GetAllAsync()).ToList();
        var candidates = new List<(SizeOllamaSuggestionDTO Value, bool FromOllama)>();
        var currentName = CleanSuggestionText(request.CurrentName, 50);
        if (currentName != null)
        {
            var currentType = request.CurrentSizeType ?? InferSizeType(currentName);
            if (currentType.HasValue)
                candidates.Add((new SizeOllamaSuggestionDTO
                {
                    Name = currentName,
                    Description = CleanSuggestionText(request.CurrentDescription, 300),
                    SizeType = currentType.Value.ToString()
                }, false));
        }

        var ollamaOptions = await RequestSizeOptionsAsync(request, cancellationToken);
        candidates.AddRange(ollamaOptions.Select(x => (x, true)));
        candidates.AddRange(SizeFallbacks.Select(x => (new SizeOllamaSuggestionDTO
        {
            Name = x.Name,
            Description = x.Description,
            SizeType = x.Type.ToString()
        }, false)));

        var names = new HashSet<string>(sizes.Select(x => AISuggestionUniquenessPolicy.NormalizeTextKey(x.Name)));
        var codes = new HashSet<string>(sizes.Select(x => AISuggestionUniquenessPolicy.NormalizeCodeKey(x.SizeCode)));
        var options = new List<SizeSuggestionOptionDTO>();
        var usedOllama = false;
        var usedFallback = false;
        var rejected = 0;
        foreach (var candidate in candidates)
        {
            if (options.Count == MaximumMasterOptions) break;
            var name = CleanSuggestionText(candidate.Value.Name, 50);
            var sizeType = ParseSizeType(candidate.Value.SizeType) ?? (name == null ? null : InferSizeType(name));
            if (name == null || !sizeType.HasValue || !names.Add(AISuggestionUniquenessPolicy.NormalizeTextKey(name)))
            {
                rejected++;
                continue;
            }
            var code = await CreateReservedCodeAsync(BuildSizeCode(name), 20, codes, _sizeRepository.ExistsBySizeCodeAsync);
            if (code.Length == 0) { rejected++; continue; }
            options.Add(new SizeSuggestionOptionDTO
            {
                Title = $"Size {name}",
                CanApply = true,
                Fields = new SizeSuggestionFieldsDTO
                {
                    Name = name,
                    SizeCode = code,
                    Description = CleanSuggestionText(candidate.Value.Description, 300)
                        ?? $"Kích thước {name} dành cho sản phẩm CafeChain.",
                    SizeType = sizeType.Value,
                    Active = true
                }
            });
            usedOllama |= candidate.FromOllama;
            usedFallback |= !candidate.FromOllama;
        }
        if (options.Count == 0) return SizeFailure("Không còn gợi ý size khác biệt với dữ liệu hiện có.");
        var result = new SizeSuggestionResultDTO
        {
            Success = true,
            Message = $"Đã tạo {options.Count} gợi ý size.",
            Options = options,
            UsedOllama = usedOllama,
            UsedFallback = usedFallback
        };
        if (rejected > 0) result.Warnings.Add($"Đã loại {rejected} lựa chọn trùng hoặc không hợp lệ.");
        return result;
    }

    public async Task<ToppingSuggestionResultDTO> SuggestToppingAsync(
        ToppingSuggestionRequestDTO request,
        CancellationToken cancellationToken = default)
    {
        var toppings = (await _toppingRepository.GetAllAsync()).ToList();
        var price = request.CurrentPrice is >= 1000
            ? request.CurrentPrice.Value
            : CalculateMedianToppingPrice(toppings) ?? DefaultToppingPrice;
        var candidates = new List<(ToppingOllamaSuggestionDTO Value, bool FromOllama)>();
        var currentName = CleanSuggestionText(request.CurrentName, 100);
        if (currentName != null)
            candidates.Add((new ToppingOllamaSuggestionDTO
            {
                Name = currentName,
                ImagePrompt = $"{currentName}, premium cafe topping, appetizing close-up"
            }, false));

        var ollamaOptions = await RequestToppingOptionsAsync(request, cancellationToken);
        candidates.AddRange(ollamaOptions.Select(x => (x, true)));
        candidates.AddRange(ToppingFallbacks.Select(x => (new ToppingOllamaSuggestionDTO
        {
            Name = x,
            ImagePrompt = $"{x}, premium cafe topping, appetizing close-up"
        }, false)));

        var names = new HashSet<string>(toppings.Select(x => AISuggestionUniquenessPolicy.NormalizeTextKey(x.Name)));
        var codes = new HashSet<string>(toppings.Select(x => AISuggestionUniquenessPolicy.NormalizeCodeKey(x.ToppingCode)));
        var options = new List<ToppingSuggestionOptionDTO>();
        var usedOllama = false;
        var usedFallback = false;
        var rejected = 0;
        foreach (var candidate in candidates)
        {
            if (options.Count == MaximumMasterOptions) break;
            var name = CleanSuggestionText(candidate.Value.Name, 100);
            if (name == null || !names.Add(AISuggestionUniquenessPolicy.NormalizeTextKey(name)))
            {
                rejected++;
                continue;
            }
            var code = await CreateReservedCodeAsync(name, 50, codes,
                value => _toppingRepository.ExistsByToppingCodeAsync(value));
            if (code.Length == 0) { rejected++; continue; }
            options.Add(new ToppingSuggestionOptionDTO
            {
                Title = name,
                CanApply = true,
                Fields = new ToppingSuggestionFieldsDTO
                {
                    Name = name,
                    ToppingCode = code,
                    Price = price,
                    Active = true,
                    ImagePrompt = CleanSuggestionText(candidate.Value.ImagePrompt, 500)
                        ?? $"{name}, premium cafe topping, appetizing close-up"
                },
                VisualSpecification = _visualSpecificationBuilder.BuildTopping(name, candidate.Value.ImagePrompt)
            });
            usedOllama |= candidate.FromOllama;
            usedFallback |= !candidate.FromOllama;
        }
        if (options.Count == 0) return ToppingFailure("Không còn gợi ý topping khác biệt với dữ liệu hiện có.");
        var result = new ToppingSuggestionResultDTO
        {
            Success = true,
            RequestId = Guid.NewGuid(),
            Message = $"Đã tạo {options.Count} gợi ý topping.",
            Options = options,
            UsedOllama = usedOllama,
            UsedFallback = usedFallback
        };
        if (rejected > 0) result.Warnings.Add($"Đã loại {rejected} lựa chọn trùng hoặc không hợp lệ.");
        if (!toppings.Any(x => x.Active && x.Price > 0) && request.CurrentPrice is not >= 1000)
            result.Warnings.Add("Chưa có lịch sử giá; C# dùng giá fallback 7.000đ.");
        return result;
    }

    private async Task<List<DrinkOllamaSuggestionDTO>> RequestDrinkOptionsAsync(
        DrinkSuggestionRequestDTO request,
        IReadOnlyCollection<DrinkCategory> categories,
        IReadOnlyCollection<ProductType> productTypes,
        CancellationToken cancellationToken)
    {
        if (!CanUseOllama()) return [];
        var payload = JsonSerializer.Serialize(new
        {
            idea = CleanSuggestionText(request.Idea, 200),
            current = new { request.CurrentName, request.CurrentDescription, request.CurrentCategoryId, request.CurrentProductTypeId },
            allowedCategories = categories.Select(x => new { code = x.CategoryCode, x.Name }),
            allowedProductTypes = productTypes.Select(x => new { x.Code, x.Name })
        });
        var response = await _ollama.ChatAsync(
            """
            Bạn là trợ lý tạo nội dung đồ uống CafeChain. Hãy tạo 3 lựa chọn khác nhau, kể cả khi idea và current trống.
            Chỉ chọn categoryCode và productTypeCode trong allow-list.
            Trả đúng JSON: {"options":[{"name":"...","categoryCode":"...","productTypeCode":"...","description":"...","imagePrompt":"..."}]}.
            Tên/mô tả viết tiếng Việt; imagePrompt viết tiếng Anh. Không tạo ID, mã, giá hoặc yêu cầu tự lưu.
            """, payload, cancellationToken);
        return ParseSuggestion<DrinkOllamaOptionsDTO>(response)?.Options.Take(6).ToList() ?? [];
    }

    private async Task<List<SizeOllamaSuggestionDTO>> RequestSizeOptionsAsync(
        SizeSuggestionRequestDTO request,
        CancellationToken cancellationToken)
    {
        if (!CanUseOllama()) return [];
        var payload = JsonSerializer.Serialize(new
        {
            idea = CleanSuggestionText(request.Idea, 200),
            current = new { request.CurrentName, request.CurrentDescription, sizeType = request.CurrentSizeType?.ToString() }
        });
        var response = await _ollama.ChatAsync(
            """
            Tạo 3 lựa chọn size khác nhau cho CafeChain, kể cả khi dữ liệu trống.
            Trả đúng JSON: {"options":[{"name":"...","description":"...","sizeType":"Cup|Volume"}]}.
            Dung tích ml/lít dùng Volume; S/M/L/XL và tên kích cỡ ly dùng Cup. Không tạo mã hoặc ID.
            """, payload, cancellationToken);
        return ParseSuggestion<SizeOllamaOptionsDTO>(response)?.Options.Take(6).ToList() ?? [];
    }

    private async Task<List<ToppingOllamaSuggestionDTO>> RequestToppingOptionsAsync(
        ToppingSuggestionRequestDTO request,
        CancellationToken cancellationToken)
    {
        if (!CanUseOllama()) return [];
        var payload = JsonSerializer.Serialize(new
        {
            idea = CleanSuggestionText(request.Idea, 200),
            currentName = CleanSuggestionText(request.CurrentName, 100)
        });
        var response = await _ollama.ChatAsync(
            """
            Tạo 3 lựa chọn topping khác nhau cho CafeChain, kể cả khi dữ liệu trống.
            Trả đúng JSON: {"options":[{"name":"...","imagePrompt":"..."}]}.
            Tên viết tiếng Việt; imagePrompt viết tiếng Anh. Không tạo giá, mã hoặc ID.
            """, payload, cancellationToken);
        return ParseSuggestion<ToppingOllamaOptionsDTO>(response)?.Options.Take(6).ToList() ?? [];
    }

    private bool CanUseOllama() => _options.Enabled
        && string.Equals(_options.Provider, "Ollama", StringComparison.OrdinalIgnoreCase);

    private static T? ParseSuggestion<T>(OllamaResultDTO result) where T : class
    {
        if (!result.Success || string.IsNullOrWhiteSpace(result.Content)) return null;
        try
        {
            return JsonSerializer.Deserialize<T>(StripMarkdownFence(result.Content),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException) { return null; }
    }

    private async Task<string> CreateReservedCodeAsync(
        string source,
        int maximumLength,
        HashSet<string> reservedCodes,
        Func<string, Task<bool>> existsInDatabase)
    {
        var code = await CreateUniqueCodeAsync(source, maximumLength, reservedCodes, existsInDatabase);
        if (code.Length > 0) reservedCodes.Add(AISuggestionUniquenessPolicy.NormalizeCodeKey(code));
        return code;
    }

    private static string? CleanSuggestionText(string? value, int maximumLength)
    {
        var clean = value?.Trim();
        return string.IsNullOrWhiteSpace(clean) || clean.Length > maximumLength ? null : clean;
    }

    private static DrinkCategory? FindCategory(IEnumerable<DrinkCategory> values, string? code)
    {
        var key = AISuggestionUniquenessPolicy.NormalizeCodeKey(code);
        return key.Length == 0 ? null : values.FirstOrDefault(x => AISuggestionUniquenessPolicy.NormalizeCodeKey(x.CategoryCode) == key);
    }

    private static ProductType? FindProductType(IEnumerable<ProductType> values, string? code)
    {
        var key = AISuggestionUniquenessPolicy.NormalizeCodeKey(code);
        return key.Length == 0 ? null : values.FirstOrDefault(x => AISuggestionUniquenessPolicy.NormalizeCodeKey(x.Code) == key);
    }

    private static DrinkCategory? ResolveCategoryForName(IEnumerable<DrinkCategory> values, string name)
    {
        var key = AISuggestionUniquenessPolicy.NormalizeTextKey(name);
        var desiredCode = key.Contains("CAPHE") || key.Contains("BACXIU") ? "COFFEE"
            : key.Contains("TRASUA") ? "TRASUA"
            : "NUOCNGOT";
        return FindCategory(values, desiredCode);
    }

    private static ProductType? ResolveProductTypeForName(IEnumerable<ProductType> values, string name)
    {
        var key = AISuggestionUniquenessPolicy.NormalizeCodeKey(name);
        var code = key.Contains("CHAI") || Regex.IsMatch(key, @"\d+ML") ? "RETAIL" : "HANDCRAFTED";
        return FindProductType(values, code);
    }

    private static SizeTypeEnum? ParseSizeType(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "CUP" => SizeTypeEnum.Cup,
        "VOLUME" => SizeTypeEnum.Volume,
        _ => null
    };

    private static SizeTypeEnum? InferSizeType(string name)
    {
        var key = AISuggestionUniquenessPolicy.NormalizeCodeKey(name);
        if (Regex.IsMatch(key, @"^\d+(ML|L)$")) return SizeTypeEnum.Volume;
        if (new[] { "S", "M", "L", "XL", "SMALL", "MEDIUM", "LARGE", "EXTRALARGE" }.Contains(key))
            return SizeTypeEnum.Cup;
        return null;
    }

    private static decimal? CalculateMedianToppingPrice(IEnumerable<Topping> toppings)
    {
        var prices = toppings.Where(x => x.Active && x.Price > 0).Select(x => x.Price).OrderBy(x => x).ToList();
        if (prices.Count == 0) return null;
        var median = prices.Count % 2 == 1
            ? prices[prices.Count / 2]
            : (prices[prices.Count / 2 - 1] + prices[prices.Count / 2]) / 2m;
        return Math.Max(1000m, Math.Round(median / 1000m, MidpointRounding.AwayFromZero) * 1000m);
    }

    private static DrinkSuggestionResultDTO DrinkFailure(string message) => new() { Message = message };
    private static SizeSuggestionResultDTO SizeFailure(string message) => new() { Message = message };
    private static ToppingSuggestionResultDTO ToppingFailure(string message) => new() { Message = message };
}
