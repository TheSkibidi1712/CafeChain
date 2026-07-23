using System.Collections.Concurrent;
using System.Text.Json;
using CafeChain.Application.Interfaces.AI;
using CafeChain.Infrastructure.Configurations;
using Microsoft.Extensions.Options;

namespace CafeChain.Application.Services.AI;

public sealed class AISkillCatalog : IAISkillCatalog
{
    private static readonly IReadOnlyDictionary<string, string> NamedSkillSchemas =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["inventory-reorder-explanation"] = "inventory-reorder-explanation.schema.json",
            ["dashboard-intent-parser"] = "dashboard-intent.schema.json",
            ["dashboard-insight-explanation"] = "dashboard-insight-explanation.schema.json",
            ["forecast-result-explanation"] = "forecast-result-explanation.schema.json",
            ["supplier-score-explanation"] = "supplier-score-explanation.schema.json",
            ["shift-proposal-explanation"] = "shift-proposal-explanation.schema.json",
            ["anomaly-explanation"] = "anomaly-explanation.schema.json"
        };
    private sealed record CacheEntry(DateTime LastWriteUtc, string Content);
    private sealed record SkillResource(string RelativePath, bool IsSkill, string? ExpectedName = null);

    private readonly IWebHostEnvironment _environment;
    private readonly AIOptions _options;
    private readonly ILogger<AISkillCatalog> _logger;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    public AISkillCatalog(
        IWebHostEnvironment environment,
        IOptions<AIOptions> options,
        ILogger<AISkillCatalog> logger)
    {
        _environment = environment;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AISkillContext> GetContextAsync(
        string entityType,
        bool includeImageSkills,
        CancellationToken cancellationToken = default)
    {
        var entity = NormalizeEntity(entityType);
        var warnings = new List<string>();
        var candidates = new List<(string Path, string Content)>();

        foreach (var resource in Route(entity, includeImageSkills))
        {
            var raw = await ReadSafeAsync(resource.RelativePath, warnings, cancellationToken);
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var content = resource.IsSkill
                ? StripAndValidateFrontmatter(raw, resource.ExpectedName!, resource.RelativePath, warnings)
                : raw.Trim();
            if (!string.IsNullOrWhiteSpace(content))
                candidates.Add((resource.RelativePath.Replace('\\', '/'), content));
        }

        var maxCharacters = Math.Clamp(_options.MaximumSkillContextCharacters, 2000, 50000);
        var sections = new List<string>();
        var loaded = new List<string>();
        var length = 0;
        foreach (var candidate in candidates)
        {
            var section = $"## SOURCE: {candidate.Path}\n{candidate.Content.Trim()}";
            var separatorLength = sections.Count == 0 ? 0 : 2;
            if (length + separatorLength + section.Length > maxCharacters)
            {
                warnings.Add($"Bỏ qua {candidate.Path} vì Skill context đã đạt giới hạn {maxCharacters} ký tự.");
                continue;
            }
            sections.Add(section);
            loaded.Add(candidate.Path);
            length += separatorLength + section.Length;
        }

        var suggestionSchemaPath = Path.Combine(_options.SchemaRootPath, "ai-suggestion.schema.json");
        var suggestionSchema = await ReadJsonSchemaAsync(suggestionSchemaPath, warnings, cancellationToken);
        var imageSchemaPath = Path.Combine(_options.SchemaRootPath, "image-concept.schema.json");
        _ = await ReadJsonSchemaAsync(imageSchemaPath, warnings, cancellationToken);

        if (sections.Count == 0)
            warnings.Add($"Chưa có nội dung Skill cho {entity}; sử dụng prompt fallback tích hợp.");

        return new AISkillContext(
            entity,
            loaded,
            string.Join("\n\n", sections),
            suggestionSchema,
            warnings);
    }

    public async Task<AINamedSkillContext> GetNamedSkillAsync(
        string skillName,
        CancellationToken cancellationToken = default)
    {
        var normalized = skillName?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!NamedSkillSchemas.TryGetValue(normalized, out var schemaFile))
            throw new ArgumentOutOfRangeException(nameof(skillName), "AI skill is not whitelisted.");

        var warnings = new List<string>();
        var skillPath = Path.Combine(_options.SkillRootPath, normalized, "SKILL.md");
        var rawSkill = await ReadSafeAsync(skillPath, warnings, cancellationToken);
        var content = string.IsNullOrWhiteSpace(rawSkill)
            ? string.Empty
            : StripAndValidateFrontmatter(rawSkill, normalized, skillPath, warnings);
        var schemaPath = Path.Combine(_options.SchemaRootPath, schemaFile);
        var schema = await ReadJsonSchemaAsync(schemaPath, warnings, cancellationToken) ?? string.Empty;

        if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(schema))
            throw new InvalidOperationException($"AI skill '{normalized}' does not have a valid skill and schema.");

        return new AINamedSkillContext(
            normalized,
            content,
            schema,
            [skillPath.Replace('\\', '/'), schemaPath.Replace('\\', '/')],
            warnings);
    }

    private IReadOnlyList<SkillResource> Route(string entity, bool includeImageSkills)
    {
        var resources = new List<SkillResource>
        {
            // Entity rules are first so context budgeting can never discard the domain contract.
            Reference("cafe-business-rules", "references", $"{entity.ToLowerInvariant()}-rules.md"),
            Skill("skill-router"),
            Skill("cafe-business-rules"),
            Skill("suggestion-generation"),
            Reference("suggestion-generation", "references", "diversity-profiles.md"),
            Skill("duplicate-detection")
        };

        if (includeImageSkills)
        {
            resources.AddRange(
            [
                Skill("image-prompt-builder"),
                Reference("image-prompt-builder", "references", "style-profiles.md"),
                Reference("image-prompt-builder", "references", "composition-profiles.md"),
                Reference("image-prompt-builder", "references", "negative-prompts.md")
            ]);
        }

        // Pexels and ComfyUI instructions are deterministic pipeline documentation. They are
        // intentionally not injected into master-data suggestion prompts.
        return resources;
    }

    private SkillResource Skill(string name) => new(
        Path.Combine(_options.SkillRootPath, name, "SKILL.md"), true, name);

    private SkillResource Reference(params string[] parts) => new(
        Path.Combine([_options.SkillRootPath, .. parts]), false);

    private static string StripAndValidateFrontmatter(
        string raw,
        string expectedName,
        string relativePath,
        ICollection<string> warnings)
    {
        var normalized = raw.Replace("\r\n", "\n");
        if (!normalized.StartsWith("---\n", StringComparison.Ordinal))
        {
            warnings.Add($"{relativePath} thiếu YAML frontmatter; vẫn dùng phần Markdown hiện có.");
            return normalized.Trim();
        }

        var end = normalized.IndexOf("\n---\n", 4, StringComparison.Ordinal);
        if (end < 0)
        {
            warnings.Add($"{relativePath} có YAML frontmatter không đóng; bỏ qua file.");
            return string.Empty;
        }

        var header = normalized[4..end];
        var fields = header.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split(':', 2, StringSplitOptions.TrimEntries))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.OrdinalIgnoreCase);
        if (!fields.TryGetValue("name", out var name) || !fields.ContainsKey("description"))
            warnings.Add($"{relativePath} phải có đúng name và description trong frontmatter.");
        else if (!string.Equals(name.Trim('"', '\''), expectedName, StringComparison.Ordinal))
            warnings.Add($"{relativePath} có name '{name}' không khớp folder '{expectedName}'.");
        if (fields.Keys.Any(key => key is not ("name" or "description")))
            warnings.Add($"{relativePath} có frontmatter field ngoài name/description.");

        return normalized[(end + 5)..].Trim();
    }

    private async Task<string?> ReadJsonSchemaAsync(
        string configuredPath,
        ICollection<string> warnings,
        CancellationToken cancellationToken)
    {
        var schema = await ReadSafeAsync(configuredPath, warnings, cancellationToken);
        if (string.IsNullOrWhiteSpace(schema)) return null;
        try
        {
            using var document = JsonDocument.Parse(schema);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new JsonException("Schema root must be an object.");
            return schema;
        }
        catch (JsonException)
        {
            warnings.Add($"{Path.GetFileName(configuredPath)} không hợp lệ; sử dụng DTO validator C#.");
            return null;
        }
    }

    private async Task<string?> ReadSafeAsync(
        string configuredPath,
        ICollection<string> warnings,
        CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(_environment.ContentRootPath)
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(root, configuredPath));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add($"Bỏ qua đường dẫn Skill ngoài ContentRoot: {configuredPath}.");
            return null;
        }
        if (!File.Exists(path))
        {
            warnings.Add($"Không tìm thấy tài nguyên AI: {configuredPath}.");
            return null;
        }

        var lastWrite = File.GetLastWriteTimeUtc(path);
        if (_cache.TryGetValue(path, out var cached) && cached.LastWriteUtc == lastWrite)
            return cached.Content;

        var content = await File.ReadAllTextAsync(path, cancellationToken);
        _cache[path] = new CacheEntry(lastWrite, content);
        if (string.IsNullOrWhiteSpace(content))
            _logger.LogDebug("AI skill resource is empty. Path={Path}", configuredPath);
        return content;
    }

    private static string NormalizeEntity(string entityType) => entityType.Trim().ToLowerInvariant() switch
    {
        "drink" => "Drink",
        "size" => "Size",
        "topping" => "Topping",
        "ingredient" => "Ingredient",
        _ => throw new ArgumentOutOfRangeException(nameof(entityType), "Entity AI không được hỗ trợ.")
    };
}
