using CafeChain.Application.DTOs.AI;
using CafeChain.Application.Interfaces.AI;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Infrastructure.Configurations;
using CafeChain.Infrastrusture.Interfaces.Admin.InventoryDocuments;
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
    private static readonly HashSet<string> RiskLevels = ["Low", "Medium", "High"];
    private static readonly HashSet<string> Actions = ["ReviewAndApply", "ReviewOnly"];
    private readonly IAdminInventoryDocumentRepository _repository;
    private readonly IAdminCategoryRepository _categoryRepository;
    private readonly IAdminDrinkRepository _drinkRepository;
    private readonly IAdminSizeRepository _sizeRepository;
    private readonly IAdminToppingRepository _toppingRepository;
    private readonly IUnitConversionService _conversion;
    private readonly IOllamaClient _ollama;
    private readonly IComfyUIClient _comfyUI;
    private readonly IPexelsClient _pexels;
    private readonly AIOptions _options;
    private readonly AIImageOptions _imageOptions;
    private readonly ILogger<AIService> _logger;

    public AIService(
        IAdminInventoryDocumentRepository repository,
        IAdminCategoryRepository categoryRepository,
        IAdminDrinkRepository drinkRepository,
        IAdminSizeRepository sizeRepository,
        IAdminToppingRepository toppingRepository,
        IUnitConversionService conversion,
        IOllamaClient ollama,
        IComfyUIClient comfyUI,
        IPexelsClient pexels,
        IOptions<AIOptions> options,
        IOptions<AIImageOptions> imageOptions,
        ILogger<AIService> logger)
    {
        _repository = repository;
        _categoryRepository = categoryRepository;
        _drinkRepository = drinkRepository;
        _sizeRepository = sizeRepository;
        _toppingRepository = toppingRepository;
        _conversion = conversion;
        _ollama = ollama;
        _comfyUI = comfyUI;
        _pexels = pexels;
        _options = options.Value;
        _imageOptions = imageOptions.Value;
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

    public async Task<InventoryInputSuggestionResultDTO> SuggestInventoryInputAsync(
        InventoryInputSuggestionRequestDTO request,
        CancellationToken cancellationToken = default)
    {
        if (request.Type != Models.Enums.Inventory.InventoryDocumentType.IMPORT
            || request.Purpose != Models.Enums.Inventory.InventoryDocumentPurpose.IMPORT_PURCHASE
            || request.StoreId <= 0 || request.DocumentDate == default)
            return InventoryInputFailure("Cửa hàng hoặc loại phiếu không hợp lệ.");

        var inventories = await _repository.GetStoreInventoriesAsync(request.StoreId, cancellationToken);
        var lowStock = inventories
            .Where(x => x.IngredientId.HasValue)
            .GroupBy(x => x.IngredientId!.Value)
            .Select(x => new
            {
                IngredientId = x.Key,
                Ingredient = x.First().Ingredient,
                Available = x.Sum(y => y.AvailableQty),
                Reserved = x.Sum(y => y.ReservedQty),
                MinStock = x.Where(y => y.MinStockLevel.HasValue).Select(y => y.MinStockLevel).FirstOrDefault()
            })
            .Where(x => x.MinStock is > 0 && x.Available - x.Reserved < x.MinStock)
            .ToList();

        if (lowStock.Count == 0)
            return InventoryInputFailure("Không có nguyên liệu nào đang dưới ngưỡng tồn kho tối thiểu.");

        var supplierResult = await SuggestSupplierAsync(new SupplierSuggestionRequestDTO
        {
            Type = request.Type,
            Purpose = request.Purpose,
            StoreId = request.StoreId,
            DocumentDate = request.DocumentDate,
            Details = lowStock.Select(x => new SupplierSuggestionItemRequestDTO
            {
                IngredientId = x.IngredientId,
                UnitId = x.Ingredient.BaseUnitId,
                Quantity = x.MinStock!.Value * 2m - (x.Available - x.Reserved)
            }).ToList()
        }, cancellationToken);

        var result = new InventoryInputSuggestionResultDTO
        {
            Success = supplierResult.Comparisons.Count > 0,
            Message = supplierResult.Success ? "Đã tạo phương án nhập kho tối ưu." : supplierResult.Message,
            StoreId = request.StoreId,
            SupplierId = supplierResult.RecommendedSupplierId,
            SupplierName = supplierResult.RecommendedSupplierName ?? string.Empty,
            TotalAmount = supplierResult.RecommendedTotalCost ?? 0,
            Summary = supplierResult.Summary,
            Reason = supplierResult.Reason,
            Warnings = supplierResult.Warnings,
            Comparisons = supplierResult.Comparisons,
            CanApply = supplierResult.Success && supplierResult.RecommendedSupplierId.HasValue,
            UsedOllama = supplierResult.UsedOllama,
            UsedFallback = supplierResult.UsedFallback
        };

        var stockById = lowStock.ToDictionary(x => x.IngredientId);
        result.Items = supplierResult.ApplyItems.Select(x =>
        {
            var stock = stockById[x.IngredientId];
            return new InventoryInputSuggestionItemDTO
            {
                IngredientId = x.IngredientId,
                IngredientName = x.IngredientName,
                AvailableQuantity = stock.Available,
                ReservedQuantity = stock.Reserved,
                UsableQuantity = stock.Available - stock.Reserved,
                MinimumStockLevel = stock.MinStock!.Value,
                TargetStockLevel = stock.MinStock.Value * 2m,
                SuggestedBaseQuantity = x.BaseQuantity,
                UnitId = x.UnitId,
                UnitName = x.UnitName,
                Quantity = x.Quantity,
                UnitPrice = x.UnitPrice,
                MinimumOrderQuantity = x.MinimumOrderQuantity,
                LineTotal = x.LineTotal
            };
        }).ToList();
        return result;
    }

    private static InventoryInputSuggestionResultDTO InventoryInputFailure(string message) => new()
    {
        Message = message,
        Summary = message,
        Reason = "Dữ liệu đầu vào không hợp lệ.",
        UsedFallback = true,
        RequiresUserConfirmation = true
    };

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

    public async Task<SupplierSuggestionResultDTO> SuggestSupplierAsync(
        SupplierSuggestionRequestDTO request,
        CancellationToken cancellationToken = default)
    {
        var validationMessage = ValidateRequest(request);
        if (validationMessage != null)
            return Failure(validationMessage);

        var requestedBase = new Dictionary<int, decimal>();
        foreach (var item in request.Details)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var converted = await _conversion.ConvertAsync(item.IngredientId, item.Quantity, item.UnitId);
            if (!converted.IsSuccess || converted.Data <= 0)
                return Failure(converted.Message ?? $"Không thể quy đổi nguyên liệu #{item.IngredientId} về đơn vị cơ sở.");
            requestedBase[item.IngredientId] = converted.Data;
        }

        var offers = await _repository.GetSupplierOffersAsync(
            requestedBase.Keys, request.DocumentDate, cancellationToken);
        if (offers.Count == 0)
            return Failure("Không có báo giá nhà cung cấp đang hoạt động cho các nguyên liệu đã chọn.");

        var comparisons = new List<SupplierComparisonDTO>();
        var applyBySupplier = new Dictionary<int, List<SupplierSuggestionApplyItemDTO>>();
        foreach (var supplierGroup in offers.GroupBy(x => new { x.SupplierId, x.SupplierName }))
        {
            var comparison = new SupplierComparisonDTO
            {
                SupplierId = supplierGroup.Key.SupplierId,
                SupplierName = supplierGroup.Key.SupplierName
            };
            var applyItems = new List<SupplierSuggestionApplyItemDTO>();

            foreach (var requested in requestedBase)
            {
                var candidates = supplierGroup.Where(x => x.IngredientId == requested.Key).ToList();
                var calculated = new List<(SupplierSuggestionApplyItemDTO Item, SupplierOfferDTO Offer)>();
                foreach (var offer in candidates)
                {
                    var item = await CalculateOfferAsync(offer, requested.Value);
                    if (item != null)
                        calculated.Add((item, offer));
                }

                var best = calculated
                    .OrderBy(x => x.Item.LineTotal)
                    .ThenBy(x => x.Offer.LeadTimeDays ?? int.MaxValue)
                    .ThenByDescending(x => x.Offer.IsPrimary)
                    .ThenBy(x => x.Offer.IngredientSupplierId)
                    .FirstOrDefault();

                if (best.Item == null)
                {
                    comparison.Warnings.Add($"Không có quy cách/giá hợp lệ cho nguyên liệu #{requested.Key}.");
                    comparison.MissingIngredients.Add(
                        offers.FirstOrDefault(x => x.IngredientId == requested.Key)?.IngredientName
                        ?? $"Nguyên liệu #{requested.Key}");
                    continue;
                }
                applyItems.Add(best.Item);
            }

            comparison.CoversAllIngredients = applyItems.Count == requestedBase.Count;
            comparison.CoveredIngredientCount = applyItems.Count;
            comparison.TotalIngredientCount = requestedBase.Count;
            comparison.TotalCost = comparison.CoversAllIngredients ? applyItems.Sum(x => x.LineTotal) : null;
            comparison.LeadTimeDays = supplierGroup.Where(x => x.LeadTimeDays.HasValue).Select(x => x.LeadTimeDays).DefaultIfEmpty().Max();
            comparison.RiskLevel = DetermineRisk(comparison);
            comparisons.Add(comparison);
            if (comparison.CoversAllIngredients)
                applyBySupplier[comparison.SupplierId] = applyItems;
        }

        var ranked = comparisons
            .Where(x => x.CoversAllIngredients && x.TotalCost.HasValue)
            .OrderBy(x => x.TotalCost)
            .ThenBy(x => x.LeadTimeDays ?? int.MaxValue)
            .ThenByDescending(x => offers.Where(o => o.SupplierId == x.SupplierId).All(o => o.IsPrimary))
            .ThenBy(x => x.SupplierId)
            .ToList();

        var recommended = ranked.FirstOrDefault();
        if (recommended == null)
        {
            var result = Failure("Không có nhà cung cấp nào cung cấp hợp lệ toàn bộ danh sách nguyên liệu.");
            result.CurrentSupplierId = request.CurrentSupplierId;
            result.Comparisons = comparisons.OrderBy(x => x.SupplierName).ToList();
            result.Warnings.Add("Hệ thống không tự tách danh sách thành nhiều phiếu nhập.");
            return result;
        }

        var current = comparisons.FirstOrDefault(x => x.SupplierId == request.CurrentSupplierId);
        if (current?.LeadTimeDays.HasValue == true
            && recommended.LeadTimeDays.HasValue
            && recommended.LeadTimeDays.Value > current.LeadTimeDays.Value)
        {
            recommended.RiskLevel = "Medium";
            recommended.Warnings.Add("Nhà cung cấp đề xuất có thời gian giao lâu hơn nhà cung cấp hiện tại.");
        }
        var currentCost = current?.TotalCost;
        var savings = currentCost.HasValue ? Math.Max(0, currentCost.Value - recommended.TotalCost!.Value) : 0;
        var resultDto = new SupplierSuggestionResultDTO
        {
            Success = true,
            Message = "Đã phân tích nhà cung cấp bằng dữ liệu hệ thống.",
            CurrentSupplierId = request.CurrentSupplierId,
            RecommendedSupplierId = recommended.SupplierId,
            RecommendedSupplierName = recommended.SupplierName,
            CurrentTotalCost = currentCost,
            RecommendedTotalCost = recommended.TotalCost,
            SavingsAmount = savings,
            SavingsPercentage = currentCost > 0 ? Math.Round(savings / currentCost.Value * 100m, 2) : 0,
            RiskLevel = recommended.RiskLevel,
            Comparisons = comparisons.OrderByDescending(x => x.CoversAllIngredients).ThenBy(x => x.TotalCost).ToList(),
            ApplyItems = applyBySupplier[recommended.SupplierId],
            RecommendedAction = "ReviewAndApply",
            Warnings = BuildWarnings(recommended),
            RequiresUserConfirmation = true
        };
        ApplyFallbackExplanation(resultDto);
        await TryApplyOllamaExplanationAsync(resultDto, cancellationToken);
        return resultDto;
    }

    private async Task<SupplierSuggestionApplyItemDTO?> CalculateOfferAsync(SupplierOfferDTO offer, decimal requestedBaseQuantity)
    {
        if (offer.PackagePrice <= 0 || !offer.PackageQuantity.HasValue || offer.PackageQuantity <= 0)
            return null;
        var packageBase = await _conversion.ConvertAsync(
            offer.IngredientId, offer.PackageQuantity.Value, offer.PackageUnitId, offer.BaseUnitId);
        if (!packageBase.IsSuccess || packageBase.Data <= 0)
            return null;

        var packageCount = Math.Ceiling(requestedBaseQuantity / packageBase.Data);
        if (offer.MinimumOrderQuantity is > 0)
            packageCount = Math.Max(packageCount, offer.MinimumOrderQuantity.Value);
        var quantityInPackageUnit = packageCount * offer.PackageQuantity.Value;
        var unitPrice = offer.PackagePrice / offer.PackageQuantity.Value;
        return new()
        {
            IngredientId = offer.IngredientId,
            IngredientName = offer.IngredientName,
            UnitId = offer.PackageUnitId,
            UnitName = offer.PackageUnitName,
            Quantity = quantityInPackageUnit,
            UnitPrice = unitPrice,
            BaseQuantity = packageCount * packageBase.Data,
            MinimumOrderQuantity = offer.MinimumOrderQuantity ?? 0,
            LineTotal = packageCount * offer.PackagePrice
        };
    }

    private async Task TryApplyOllamaExplanationAsync(SupplierSuggestionResultDTO result, CancellationToken cancellationToken)
    {
        if (!_options.Enabled || !string.Equals(_options.Provider, "Ollama", StringComparison.OrdinalIgnoreCase))
            return;

        var payload = JsonSerializer.Serialize(new
        {
            result.RecommendedSupplierId,
            result.RecommendedSupplierName,
            result.CurrentSupplierId,
            result.CurrentTotalCost,
            result.RecommendedTotalCost,
            result.SavingsAmount,
            result.SavingsPercentage,
            result.RiskLevel,
            result.Warnings,
            Comparisons = result.Comparisons.Select(x => new
            {
                x.SupplierId, x.SupplierName, x.CoversAllIngredients, x.TotalCost, x.LeadTimeDays, x.RiskLevel
            })
        });
        var ollama = await _ollama.ChatAsync(BuildSystemPrompt(), payload, cancellationToken);
        if (!ollama.Success || string.IsNullOrWhiteSpace(ollama.Content))
        {
            _logger.LogInformation("Supplier AI explanation used fallback. Reason={Reason}", ollama.ErrorMessage);
            return;
        }

        try
        {
            var content = StripMarkdownFence(ollama.Content);
            var explanation = JsonSerializer.Deserialize<SupplierExplanationDTO>(content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (explanation == null || !RiskLevels.Contains(explanation.RiskLevel)
                || !Actions.Contains(explanation.RecommendedAction)
                || explanation.RiskLevel != result.RiskLevel
                || explanation.RecommendedAction != result.RecommendedAction
                || string.IsNullOrWhiteSpace(explanation.Summary)
                || string.IsNullOrWhiteSpace(explanation.Reason))
                return;

            result.Summary = explanation.Summary.Trim();
            result.Reason = explanation.Reason.Trim();
            result.UsedOllama = true;
            result.UsedFallback = false;
        }
        catch (JsonException)
        {
            _logger.LogInformation("Supplier AI explanation returned invalid structured JSON; fallback retained.");
        }
    }

    private static string BuildSystemPrompt() => """
        Bạn là trợ lý phân tích vận hành cho CafeChain. Chỉ sử dụng JSON được cung cấp.
        Mọi số tiền, số lượng, mã định danh và xếp hạng đã được C# tính chính xác; không tính lại, thay đổi hoặc bịa dữ liệu.
        Chỉ giải thích ngắn gọn bằng tiếng Việt. Trả đúng JSON gồm summary, reason, riskLevel, warnings, recommendedAction.
        riskLevel phải giữ nguyên Low/Medium/High trong dữ liệu; recommendedAction phải giữ nguyên ReviewAndApply/ReviewOnly.
        Không đề nghị tự động lưu, tạo, xác nhận hoặc submit phiếu.
        """;

    private static void ApplyFallbackExplanation(SupplierSuggestionResultDTO result)
    {
        result.Summary = $"{result.RecommendedSupplierName} có tổng chi phí hợp lệ thấp nhất và cung cấp đủ danh sách nguyên liệu.";
        result.Reason = "Kết quả được hệ thống C# tính từ giá gói, quy cách, MOQ và quy đổi đơn vị. Phần giải thích bằng Ollama hiện không khả dụng.";
        result.UsedOllama = false;
        result.UsedFallback = true;
    }

    private static string DetermineRisk(SupplierComparisonDTO comparison) =>
        !comparison.CoversAllIngredients || comparison.Warnings.Count > 0 ? "High"
        : !comparison.LeadTimeDays.HasValue ? "Medium" : "Low";

    private static List<string> BuildWarnings(SupplierComparisonDTO recommended)
    {
        var warnings = new List<string>(recommended.Warnings)
        {
            "Chưa có dữ liệu cấu trúc về chiết khấu, phí vận chuyển và chi phí khác; các khoản này không được giả định bằng 0.",
            "Chưa có dữ liệu tỷ lệ giao đúng hạn/giao thiếu để chấm độ tin cậy nhà cung cấp."
        };
        if (!recommended.LeadTimeDays.HasValue)
            warnings.Add("Nhà cung cấp đề xuất chưa có dữ liệu thời gian giao hàng.");
        return warnings;
    }

    private static string? ValidateRequest(SupplierSuggestionRequestDTO request)
    {
        if (request.Type != Models.Enums.Inventory.InventoryDocumentType.IMPORT
            || request.Purpose != Models.Enums.Inventory.InventoryDocumentPurpose.IMPORT_PURCHASE)
            return "Chỉ phân tích nhà cung cấp cho phiếu nhập mua hàng.";
        if (request.StoreId <= 0 || request.DocumentDate == default)
            return "Cửa hàng hoặc ngày nhập không hợp lệ.";
        if (request.Details.Count == 0 || request.Details.Any(x => x.IngredientId <= 0 || x.UnitId <= 0 || x.Quantity <= 0))
            return "Danh sách nguyên liệu không hợp lệ.";
        if (request.Details.Select(x => x.IngredientId).Distinct().Count() != request.Details.Count)
            return "Không được gửi trùng nguyên liệu.";
        return null;
    }

    private static SupplierSuggestionResultDTO Failure(string message) => new()
    {
        Success = false,
        Message = message,
        RiskLevel = "High",
        RecommendedAction = "ReviewOnly",
        Summary = message,
        Reason = "Không đủ dữ liệu hợp lệ để đưa ra đề xuất.",
        UsedFallback = true,
        RequiresUserConfirmation = true
    };

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
