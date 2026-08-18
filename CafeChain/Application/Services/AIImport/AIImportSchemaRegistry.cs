using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CafeChain.Application.DTOs.AIImport;
using CafeChain.Application.Validation;
using CafeChain.Models.AIImport;
using System.ComponentModel.DataAnnotations;

namespace CafeChain.Application.Services.AIImport;

public sealed record AIImportFieldDefinition(
    string Name,
    bool Required,
    int MaxLength,
    IReadOnlySet<string> Aliases);

public sealed class AIImportSchemaDefinition
{
    public required AIImportEntityType EntityType { get; init; }
    public required IReadOnlyList<AIImportFieldDefinition> Fields { get; init; }
    public IReadOnlySet<string> RequiredFields => Fields.Where(x => x.Required).Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
}

public interface IAIImportSchemaRegistry
{
    IReadOnlyCollection<AIImportEntityType> SupportedEntities { get; }
    AIImportSchemaDefinition Get(AIImportEntityType entityType);
    (AIImportEntityType EntityType, Dictionary<string, string?> Mapping, decimal Confidence) Detect(
        IEnumerable<string?> headers,
        string sheetName,
        AIImportEntityType? hint = null);
    Dictionary<string, string?> Normalize(AIImportEntityType entityType, IReadOnlyDictionary<string, string?> values);
    List<AIImportErrorDto> Validate(AIImportEntityType entityType, IReadOnlyDictionary<string, string?> values);
    bool IsAllowedMapping(AIImportEntityType entityType, IReadOnlyDictionary<string, string?> mapping);
    List<AIImportSourceColumn> ClassifyColumns(
        AIImportEntityType entityType,
        IEnumerable<AIImportSourceColumn> columns,
        IReadOnlyDictionary<string, string?> mapping,
        IReadOnlyCollection<string>? ignoredSourceColumns = null);
}

public sealed partial class AIImportSchemaRegistry : IAIImportSchemaRegistry
{
    private static readonly IReadOnlyDictionary<AIImportEntityType, AIImportSchemaDefinition> Schemas = BuildSchemas();
    public IReadOnlyCollection<AIImportEntityType> SupportedEntities => Schemas.Keys.ToArray();

    public AIImportSchemaDefinition Get(AIImportEntityType entityType) =>
        Schemas.TryGetValue(entityType, out var schema)
            ? schema
            : throw new ArgumentOutOfRangeException(nameof(entityType), "Loại dữ liệu không thuộc phạm vi nhập dữ liệu thông minh.");

    public (AIImportEntityType EntityType, Dictionary<string, string?> Mapping, decimal Confidence) Detect(
        IEnumerable<string?> headers,
        string sheetName,
        AIImportEntityType? hint = null)
    {
        var source = headers.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()).ToList();
        var candidates = Schemas.Values.Select(schema =>
        {
            var mapping = Map(schema, source);
            var requiredMatched = schema.RequiredFields.Count == 0 ? 0m :
                mapping.Count(x => schema.RequiredFields.Contains(x.Key) && !string.IsNullOrWhiteSpace(x.Value)) / (decimal)schema.RequiredFields.Count;
            var allMatched = mapping.Count(x => !string.IsNullOrWhiteSpace(x.Value)) / (decimal)schema.Fields.Count;
            var sheetSignal = EntityAliases(schema.EntityType).Any(x => Key(sheetName).Contains(x, StringComparison.Ordinal)) ? .12m : 0m;
            var hintSignal = hint == schema.EntityType ? .18m : 0m;
            return new { schema.EntityType, Mapping = mapping, Score = Math.Min(1m, requiredMatched * .7m + allMatched * .2m + sheetSignal + hintSignal) };
        }).OrderByDescending(x => x.Score).ToList();

        var best = candidates[0];
        return best.Score < .45m
            ? (AIImportEntityType.Unknown, new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase), best.Score)
            : (best.EntityType, best.Mapping, best.Score);
    }

    public Dictionary<string, string?> Normalize(AIImportEntityType entityType, IReadOnlyDictionary<string, string?> values)
    {
        var result = Get(entityType).Fields.ToDictionary(
            x => x.Name,
            x => Clean(values.GetValueOrDefault(x.Name)),
            StringComparer.OrdinalIgnoreCase);
        foreach (var field in new[] { "CategoryCode", "DrinkCode", "SizeCode", "Code" })
            if (result.ContainsKey(field)) result[field] = result[field]?.ToUpperInvariant();
        if (result.ContainsKey("TaxCode")) result["TaxCode"] = Regex.Replace(result["TaxCode"] ?? string.Empty, "[^0-9-]", string.Empty);
        if (result.ContainsKey("Active") && string.IsNullOrWhiteSpace(result["Active"])) result["Active"] = "true";
        if (result.ContainsKey("SizeType") && !string.IsNullOrWhiteSpace(result["SizeType"]))
        {
            result["SizeType"] = Key(result["SizeType"]) switch
            {
                "volume" or "dungtich" or "theothetich" => "Volume",
                "cup" or "ly" or "coc" => "Cup",
                _ => result["SizeType"]
            };
        }
        return result;
    }

    public List<AIImportErrorDto> Validate(AIImportEntityType entityType, IReadOnlyDictionary<string, string?> values)
    {
        var errors = new List<AIImportErrorDto>();
        foreach (var field in Get(entityType).Fields)
        {
            var value = Clean(values.GetValueOrDefault(field.Name));
            if (field.Required && string.IsNullOrWhiteSpace(value))
                errors.Add(NewError("TRƯỜNG_BẮT_BUỘC", $"{FieldLabel(field.Name)} là bắt buộc.", field.Name));
            if (!string.Equals(field.Name, "Icon", StringComparison.OrdinalIgnoreCase)
                && field.MaxLength > 0 && value?.Length > field.MaxLength)
                errors.Add(NewError("VƯỢT_GIỚI_HẠN", $"{FieldLabel(field.Name)} tối đa {field.MaxLength} ký tự.", field.Name));
        }

        string? Value(string field) => Clean(values.GetValueOrDefault(field));
        if (entityType == AIImportEntityType.Category)
        {
            if (Value("CategoryCode") is { Length: > 0 and < 2 }) errors.Add(NewError("KHÔNG_HỢP_LỆ", "Mã danh mục phải từ 2 ký tự.", "CategoryCode"));
            if (Value("Name") is { Length: > 0 and < 2 }) errors.Add(NewError("KHÔNG_HỢP_LỆ", "Tên danh mục phải từ 2 ký tự.", "Name"));
            if (!CategoryIconPolicy.TryNormalize(Value("Icon"), out _, out var iconError))
                errors.Add(NewError("ICON_KHÔNG_HỢP_LỆ", iconError?.Replace("Icon", "Biểu tượng", StringComparison.Ordinal) ?? "Biểu tượng không hợp lệ.", "Icon"));
            if (Value("Active") is { Length: > 0 } active && !bool.TryParse(active, out _))
                errors.Add(NewError("KHÔNG_HỢP_LỆ", "Trạng thái hoạt động chỉ nhận Có hoặc Không.", "Active"));
        }
        if (entityType == AIImportEntityType.Size && Value("SizeType") is { Length: > 0 } sizeType
            && !string.Equals(sizeType, "Cup", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(sizeType, "Volume", StringComparison.OrdinalIgnoreCase))
            errors.Add(NewError("KHÔNG_HỢP_LỆ", "Loại kích cỡ chỉ nhận Theo ly hoặc Theo dung tích.", "SizeType"));
        if (entityType == AIImportEntityType.Supplier && Value("TaxCode") is { Length: > 0 } taxCode
            && !TaxCodeRegex().IsMatch(taxCode))
            errors.Add(NewError("MÃ_SỐ_THUẾ_KHÔNG_HỢP_LỆ", "Mã số thuế phải có 10 chữ số hoặc dạng 10-3 chữ số.", "TaxCode"));
        if (entityType == AIImportEntityType.Supplier && Value("PrimaryContactEmail") is { Length: > 0 } email
            && !new EmailAddressAttribute().IsValid(email))
            errors.Add(NewError("EMAIL_KHÔNG_HỢP_LỆ", "Email đầu mối không đúng định dạng.", "PrimaryContactEmail"));
        return errors;
    }

    public bool IsAllowedMapping(AIImportEntityType entityType, IReadOnlyDictionary<string, string?> mapping)
    {
        if (!Schemas.TryGetValue(entityType, out var schema)) return false;
        var allowed = schema.Fields.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return mapping.Keys.All(allowed.Contains)
               && mapping.Values.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).Count()
               == mapping.Values.Count(x => !string.IsNullOrWhiteSpace(x));
    }

    public List<AIImportSourceColumn> ClassifyColumns(
        AIImportEntityType entityType,
        IEnumerable<AIImportSourceColumn> columns,
        IReadOnlyDictionary<string, string?> mapping,
        IReadOnlyCollection<string>? ignoredSourceColumns = null)
    {
        var mapped = mapping.Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(pair => pair.Value!, pair => pair.Key, StringComparer.OrdinalIgnoreCase);
        var explicitlyIgnored = (ignoredSourceColumns ?? [])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return columns.Select(column =>
        {
            if (mapped.TryGetValue(column.Key, out var target))
            {
                column.Classification = AIImportColumnClassifications.Mapped;
                column.TargetField = target;
                column.Reason = null;
            }
            else if (IsForbiddenHeader(column.Label))
            {
                column.Classification = AIImportColumnClassifications.Forbidden;
                column.TargetField = null;
                column.Reason = "Cột định danh, phạm vi, quyền hoặc lệnh không được phép nhập.";
            }
            else if (explicitlyIgnored.Contains(column.Key))
            {
                column.Classification = AIImportColumnClassifications.Ignored;
                column.TargetField = null;
                column.Reason = "Người dùng đã xác nhận bỏ qua cột nguồn này.";
            }
            else if (IsKnownIgnoredHeader(column.Label))
            {
                column.Classification = AIImportColumnClassifications.Ignored;
                column.TargetField = null;
                column.Reason = "Cột thông tin phụ đã được hệ thống nhận diện nhưng không thuộc danh sách trường được phép nhập.";
            }
            else
            {
                column.Classification = AIImportColumnClassifications.Unknown;
                column.TargetField = null;
                column.Reason = "Hệ thống chưa xác định ý nghĩa nghiệp vụ của cột.";
            }
            return column;
        }).ToList();
    }

    private static Dictionary<string, string?> Map(AIImportSchemaDefinition schema, IReadOnlyCollection<string> headers)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in schema.Fields)
        {
            var matches = headers.Where(header => field.Aliases.Contains(Key(RemoveColumnSuffix(header)))
                                                  || Key(RemoveColumnSuffix(header)) == Key(field.Name))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            result[field.Name] = matches.Count == 1 ? matches[0] : null;
        }
        return result;
    }

    private static string FieldLabel(string name) => name switch
    {
        "CategoryCode" => "Mã danh mục",
        "DrinkCode" => "Mã đồ uống",
        "SizeCode" => "Mã kích cỡ",
        "Code" => "Mã nguyên liệu",
        "Name" => "Tên",
        "Icon" => "Biểu tượng",
        "Active" => "Trạng thái hoạt động",
        "Description" => "Mô tả",
        "Category" => "Danh mục",
        "ProductType" => "Loại sản phẩm",
        "SizeType" => "Loại kích cỡ",
        "BaseUnit" => "Đơn vị cơ sở",
        "TaxCode" => "Mã số thuế",
        "Address" => "Địa chỉ",
        "Note" => "Ghi chú",
        "PrimaryPhone" => "Số điện thoại chính",
        "PrimaryContactName" => "Tên người liên hệ",
        "PrimaryContactPhone" => "Số điện thoại người liên hệ",
        "PrimaryContactEmail" => "Email người liên hệ",
        "PrimaryContactPosition" => "Chức vụ người liên hệ",
        _ => "Trường dữ liệu"
    };

    private static IReadOnlyDictionary<AIImportEntityType, AIImportSchemaDefinition> BuildSchemas()
    {
        static AIImportFieldDefinition F(string name, bool required, int max, params string[] aliases) =>
            new(name, required, max, aliases.Append(name).Select(Key).ToHashSet(StringComparer.Ordinal));
        return new Dictionary<AIImportEntityType, AIImportSchemaDefinition>
        {
            [AIImportEntityType.Category] = new() { EntityType = AIImportEntityType.Category, Fields =
            [ F("CategoryCode", true, 30, "mã danh mục", "ma dm", "category code"), F("Name", true, 100, "tên danh mục", "danh mục", "category name"), F("Icon", false, 10, "biểu tượng", "emoji"), F("Active", false, 5, "hoạt động", "trạng thái") ] },
            [AIImportEntityType.Drink] = new() { EntityType = AIImportEntityType.Drink, Fields =
            [ F("DrinkCode", true, 50, "mã đồ uống", "mã nước", "product code"), F("Name", true, 200, "tên đồ uống", "tên nước", "drink name"), F("Description", false, 1000, "mô tả"), F("Category", true, 100, "danh mục", "category"), F("ProductType", true, 100, "loại sản phẩm", "product type", "loại đồ uống") ] },
            [AIImportEntityType.Size] = new() { EntityType = AIImportEntityType.Size, Fields =
            [ F("SizeCode", true, 20, "mã size", "mã kích cỡ"), F("Name", true, 50, "tên size", "kích cỡ"), F("Description", false, 300, "mô tả"), F("SizeType", true, 20, "loại size", "kiểu size") ] },
            [AIImportEntityType.Ingredient] = new() { EntityType = AIImportEntityType.Ingredient, Fields =
            [ F("Code", true, 50, "mã nguyên liệu", "ingredient code"), F("Name", true, 200, "tên nguyên liệu", "ingredient name"), F("BaseUnit", true, 100, "đơn vị cơ sở", "đơn vị", "unit") ] },
            [AIImportEntityType.Supplier] = new() { EntityType = AIImportEntityType.Supplier, Fields =
            [ F("Name", true, 200, "tên nhà cung cấp", "nhà cung cấp", "supplier name"), F("TaxCode", false, 14, "mã số thuế", "mst"), F("Address", false, 500, "địa chỉ"), F("Note", false, 1000, "ghi chú"), F("PrimaryPhone", true, 20, "hotline", "điện thoại chính", "số điện thoại"), F("PrimaryContactName", true, 150, "người liên hệ", "liên hệ chính"), F("PrimaryContactPhone", false, 20, "sđt liên hệ", "điện thoại liên hệ"), F("PrimaryContactEmail", false, 150, "email liên hệ", "email"), F("PrimaryContactPosition", false, 100, "chức vụ", "vị trí") ] }
        };
    }

    private static IReadOnlySet<string> EntityAliases(AIImportEntityType entityType) => entityType switch
    {
        AIImportEntityType.Category => new HashSet<string> { "danhmuc", "category" },
        AIImportEntityType.Drink => new HashSet<string> { "douong", "nuocuong", "drink" },
        AIImportEntityType.Size => new HashSet<string> { "size", "kichco" },
        AIImportEntityType.Ingredient => new HashSet<string> { "nguyenlieu", "ingredient" },
        AIImportEntityType.Supplier => new HashSet<string> { "nhacungcap", "supplier", "ncc" },
        _ => new HashSet<string>()
    };

    internal static string Key(string? value)
    {
        var normalized = (value ?? string.Empty).Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var character in normalized)
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(character))
                builder.Append(char.ToLowerInvariant(character == 'đ' ? 'd' : character == 'Đ' ? 'd' : character));
        return builder.ToString();
    }

    internal static bool IsForbiddenHeader(string? value)
    {
        var key = Key(RemoveColumnSuffix(value));
        return key is "storeid" or "branchid" or "categoryid" or "drinkid" or "sizeid" or "ingredientid"
            or "supplierid" or "producttypeid" or "unitid" or "createdby" or "updatedby" or "accountid"
            or "staffid" or "roleid" or "sql" or "command" or "query";
    }

    internal static bool IsKnownIgnoredHeader(string? value)
    {
        var key = Key(RemoveColumnSuffix(value));
        return key is "tcid" or "expectedpreview" or "expectedcode" or "testpurpose" or "seeddependency";
    }

    internal static string RemoveColumnSuffix(string? value) =>
        Regex.Replace(value ?? string.Empty, @"\s+\[[A-Z]+\]$", string.Empty, RegexOptions.CultureInvariant).Trim();

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static AIImportErrorDto NewError(string code, string message, string field) =>
        AIImportValidationContract.Issue(code, message, AIImportIssueSeverities.Error, field,
            resolution: AIImportIssueResolutions.EditField);
    [GeneratedRegex("^[0-9]{10}(-[0-9]{3})?$", RegexOptions.CultureInvariant)]
    private static partial Regex TaxCodeRegex();
}
