using System.Globalization;
using System.Text.Json;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.Options;
using CafeChain.Data;
using CafeChain.Models.Inventories.Auditing;
using CafeChain.Models.Systems;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CafeChain.Application.Services.AIImport;

public static class AIImportOcrSettingKeys
{
    public const string Languages = "ai_import_ocr_languages";
    public const string LegacyLocale = "ai_import_ocr_locale";
    public const string ReviewConfidence = "ai_import_ocr_review_confidence";
    public const string RenderDpi = "ai_import_ocr_render_dpi";
    public const string MaxPages = "ai_import_ocr_max_pages";
    public const string MaxPixelsPerPage = "ai_import_ocr_max_pixels_per_page";
    public const string MaxTotalPixels = "ai_import_ocr_max_total_pixels";
    public const string PageTimeoutSeconds = "ai_import_ocr_page_timeout_seconds";
    public const string TotalTimeoutSeconds = "ai_import_ocr_total_timeout_seconds";
    public const string MaxConcurrentPages = "ai_import_ocr_max_concurrent_pages";
    public const string ConfigVersion = "ai_import_ocr_config_version";
    public const string LastHealthStatus = "ai_import_ocr_last_health_status";
    public const string LastHealthMessage = "ai_import_ocr_last_health_message";
    public const string LastHealthCheckedAtUtc = "ai_import_ocr_last_health_checked_at_utc";
    public const string LastHealthFingerprint = "ai_import_ocr_last_health_fingerprint";
    public const string LastProviderVersion = "ai_import_ocr_last_provider_version";
    public const string LastExecutableAvailable = "ai_import_ocr_last_executable_available";
    public const string LastModelDataReady = "ai_import_ocr_last_model_data_ready";

    public static readonly string[] All =
    [
        Languages, LegacyLocale, ReviewConfidence, RenderDpi, MaxPages, MaxPixelsPerPage, MaxTotalPixels,
        PageTimeoutSeconds, TotalTimeoutSeconds, MaxConcurrentPages, ConfigVersion,
        LastHealthStatus, LastHealthMessage, LastHealthCheckedAtUtc, LastHealthFingerprint,
        LastProviderVersion, LastExecutableAvailable, LastModelDataReady
    ];
}

public sealed record AIImportOcrRuntimeState(
    bool InfrastructureConfigured,
    bool ProviderReady,
    string Provider,
    string? ProviderVersion,
    string Languages,
    bool ExecutableAvailable,
    bool ModelDataReady,
    decimal ReviewConfidenceThreshold,
    int RenderDpi,
    int MaxPages,
    long MaxRenderedPixelsPerPage,
    long MaxTotalRenderedPixels,
    int PageTimeoutSeconds,
    int TotalTimeoutSeconds,
    int MaxConcurrentPages,
    string ConfigVersion,
    string HealthStatus,
    string? HealthMessage,
    DateTime? LastHealthCheckedAtUtc)
{
    public bool EffectiveEnabled => InfrastructureConfigured && ProviderReady;
}

public sealed record AIImportOcrRuntimeUpdate(
    string Languages,
    decimal ReviewConfidenceThreshold,
    int RenderDpi,
    int MaxPages,
    long MaxRenderedPixelsPerPage,
    long MaxTotalRenderedPixels,
    int PageTimeoutSeconds,
    int TotalTimeoutSeconds,
    int MaxConcurrentPages);

public interface IAIImportOcrRuntimeSettings
{
    Task<AIImportOcrRuntimeState> GetAsync(CancellationToken cancellationToken);
    Task<AIImportOcrRuntimeState> UpdateAsync(
        AIImportOcrRuntimeUpdate update,
        AdminActorContext actor,
        CancellationToken cancellationToken);
    Task<AIImportOcrRuntimeState> CheckHealthAsync(
        AdminActorContext actor,
        CancellationToken cancellationToken);
}

public sealed class AIImportOcrRuntimeSettings(
    AppDbContext db,
    IOptions<AIImportOptions> options,
    IAIImportOcrProvider provider,
    IWebHostEnvironment environment) : IAIImportOcrRuntimeSettings
{
    private readonly AIImportOptions _defaults = options.Value;

    public async Task<AIImportOcrRuntimeState> GetAsync(CancellationToken cancellationToken)
    {
        var values = await db.SystemSettings.AsNoTracking()
            .Where(setting => AIImportOcrSettingKeys.All.Contains(setting.SettingKey))
            .ToDictionaryAsync(setting => setting.SettingKey, setting => setting.SettingValue, cancellationToken);
        return Build(values);
    }

    public async Task<AIImportOcrRuntimeState> UpdateAsync(
        AIImportOcrRuntimeUpdate update,
        AdminActorContext actor,
        CancellationToken cancellationToken)
    {
        Validate(update);
        var version = $"ocr-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}";
        var languages = TesseractLocalOcrProvider.NormalizeLanguages(update.Languages);
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [AIImportOcrSettingKeys.Languages] = languages,
            [AIImportOcrSettingKeys.ReviewConfidence] = update.ReviewConfidenceThreshold.ToString(CultureInfo.InvariantCulture),
            [AIImportOcrSettingKeys.RenderDpi] = update.RenderDpi.ToString(CultureInfo.InvariantCulture),
            [AIImportOcrSettingKeys.MaxPages] = update.MaxPages.ToString(CultureInfo.InvariantCulture),
            [AIImportOcrSettingKeys.MaxPixelsPerPage] = update.MaxRenderedPixelsPerPage.ToString(CultureInfo.InvariantCulture),
            [AIImportOcrSettingKeys.MaxTotalPixels] = update.MaxTotalRenderedPixels.ToString(CultureInfo.InvariantCulture),
            [AIImportOcrSettingKeys.PageTimeoutSeconds] = update.PageTimeoutSeconds.ToString(CultureInfo.InvariantCulture),
            [AIImportOcrSettingKeys.TotalTimeoutSeconds] = update.TotalTimeoutSeconds.ToString(CultureInfo.InvariantCulture),
            [AIImportOcrSettingKeys.MaxConcurrentPages] = update.MaxConcurrentPages.ToString(CultureInfo.InvariantCulture),
            [AIImportOcrSettingKeys.ConfigVersion] = version,
            [AIImportOcrSettingKeys.LastHealthStatus] = "STALE",
            [AIImportOcrSettingKeys.LastHealthMessage] = "Cấu hình đã thay đổi; cần kiểm tra lại Tesseract local.",
            [AIImportOcrSettingKeys.LastHealthFingerprint] = string.Empty
        };
        await UpsertAsync(values, cancellationToken);
        db.AuditLogs.Add(new AuditLog
        {
            TableName = "SystemSettings",
            RecordId = 0,
            Action = "UPDATE_AI_IMPORT_OCR",
            NewData = JsonSerializer.Serialize(new
            {
                Languages = languages, update.ReviewConfidenceThreshold, update.RenderDpi,
                update.MaxPages, update.MaxRenderedPixelsPerPage, update.MaxTotalRenderedPixels,
                update.PageTimeoutSeconds, update.TotalTimeoutSeconds, update.MaxConcurrentPages, Version = version
            }),
            UserId = actor.AccountId,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        return await CheckHealthAndPersistAsync(actor, cancellationToken);
    }

    public async Task<AIImportOcrRuntimeState> CheckHealthAsync(
        AdminActorContext actor,
        CancellationToken cancellationToken) =>
        await CheckHealthAndPersistAsync(actor, cancellationToken);

    private async Task<AIImportOcrRuntimeState> CheckHealthAndPersistAsync(
        AdminActorContext actor,
        CancellationToken cancellationToken)
    {
        var values = await db.SystemSettings.AsNoTracking()
            .Where(setting => AIImportOcrSettingKeys.All.Contains(setting.SettingKey))
            .ToDictionaryAsync(setting => setting.SettingKey, setting => setting.SettingValue, cancellationToken);
        var result = await provider.CheckHealthAsync(
            new AIImportOcrHealthRequest(ResolveLanguages(values)), cancellationToken);
        var now = DateTime.UtcNow;
        await UpsertAsync(new Dictionary<string, string>
        {
            [AIImportOcrSettingKeys.LastHealthStatus] = result.Ready ? "READY" : result.Status,
            [AIImportOcrSettingKeys.LastHealthMessage] = Limit(result.Message, 500),
            [AIImportOcrSettingKeys.LastHealthCheckedAtUtc] = now.ToString("O", CultureInfo.InvariantCulture),
            [AIImportOcrSettingKeys.LastHealthFingerprint] = result.ConfigurationFingerprint ?? string.Empty,
            [AIImportOcrSettingKeys.LastProviderVersion] = result.ProviderVersion ?? string.Empty,
            [AIImportOcrSettingKeys.LastExecutableAvailable] = result.ExecutableAvailable.ToString().ToLowerInvariant(),
            [AIImportOcrSettingKeys.LastModelDataReady] = result.ModelDataReady.ToString().ToLowerInvariant()
        }, cancellationToken);
        db.AuditLogs.Add(new AuditLog
        {
            TableName = "SystemSettings",
            RecordId = 0,
            Action = "CHECK_AI_IMPORT_OCR",
            NewData = JsonSerializer.Serialize(new { result.Ready, result.Status, CheckedAtUtc = now }),
            UserId = actor.AccountId,
            CreatedAt = now
        });
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(cancellationToken);
    }

    private AIImportOcrRuntimeState Build(IReadOnlyDictionary<string, string> values)
    {
        var languages = ResolveLanguages(values);
        var tessdata = Path.GetFullPath(Path.IsPathRooted(_defaults.OcrTessdataPath)
            ? _defaults.OcrTessdataPath
            : Path.Combine(environment.ContentRootPath, _defaults.OcrTessdataPath));
        var fingerprint = TesseractLocalOcrProvider.ConfigurationFingerprint(
            _defaults.OcrExecutablePath.Trim(), tessdata, languages);
        var storedFingerprint = values.GetValueOrDefault(AIImportOcrSettingKeys.LastHealthFingerprint);
        var fingerprintMatches = string.Equals(fingerprint, storedFingerprint, StringComparison.Ordinal);
        var executableAvailable = fingerprintMatches
                                  && Bool(values, AIImportOcrSettingKeys.LastExecutableAvailable, false);
        var modelDataReady = TesseractLocalOcrProvider.RequiredModelsExist(tessdata, languages)
                             && (!fingerprintMatches || Bool(values, AIImportOcrSettingKeys.LastModelDataReady, true));
        var configured = executableAvailable && modelDataReady;
        var healthStatus = fingerprintMatches
            ? Value(values, AIImportOcrSettingKeys.LastHealthStatus, "NOT_CHECKED")
            : "STALE";
        var providerReady = configured && string.Equals(healthStatus, "READY", StringComparison.Ordinal);

        return new AIImportOcrRuntimeState(
            configured, providerReady,
            _defaults.OcrProvider, fingerprintMatches ? values.GetValueOrDefault(AIImportOcrSettingKeys.LastProviderVersion) : null,
            languages, executableAvailable, modelDataReady,
            Decimal(values, AIImportOcrSettingKeys.ReviewConfidence, _defaults.OcrReviewConfidenceThreshold),
            Int(values, AIImportOcrSettingKeys.RenderDpi, _defaults.OcrRenderDpi),
            Int(values, AIImportOcrSettingKeys.MaxPages, _defaults.OcrMaxPages),
            Long(values, AIImportOcrSettingKeys.MaxPixelsPerPage, _defaults.OcrMaxRenderedPixelsPerPage),
            Long(values, AIImportOcrSettingKeys.MaxTotalPixels, _defaults.OcrMaxTotalRenderedPixels),
            Int(values, AIImportOcrSettingKeys.PageTimeoutSeconds, _defaults.OcrPageTimeoutSeconds),
            Int(values, AIImportOcrSettingKeys.TotalTimeoutSeconds, _defaults.OcrTotalTimeoutSeconds),
            Int(values, AIImportOcrSettingKeys.MaxConcurrentPages, _defaults.OcrMaxConcurrentPages),
            Value(values, AIImportOcrSettingKeys.ConfigVersion, "ocr-tesseract-default-v1"), healthStatus,
            fingerprintMatches ? values.GetValueOrDefault(AIImportOcrSettingKeys.LastHealthMessage)
                : "Chưa kiểm tra cấu hình Tesseract local hiện tại.",
            fingerprintMatches && DateTime.TryParse(values.GetValueOrDefault(AIImportOcrSettingKeys.LastHealthCheckedAtUtc),
                CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var checkedAt) ? checkedAt : null);
    }

    private string ResolveLanguages(IReadOnlyDictionary<string, string> values)
    {
        var configured = values.GetValueOrDefault(AIImportOcrSettingKeys.Languages);
        if (string.IsNullOrWhiteSpace(configured))
            configured = values.GetValueOrDefault(AIImportOcrSettingKeys.LegacyLocale);
        return TesseractLocalOcrProvider.NormalizeLanguages(
            string.IsNullOrWhiteSpace(configured) ? _defaults.OcrLanguages : configured);
    }

    private async Task UpsertAsync(IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken)
    {
        var keys = values.Keys.ToList();
        var existing = await db.SystemSettings.Where(setting => keys.Contains(setting.SettingKey)).ToListAsync(cancellationToken);
        foreach (var (key, value) in values)
        {
            var setting = existing.SingleOrDefault(item => item.SettingKey == key);
            if (setting == null)
                db.SystemSettings.Add(new SystemSetting
                {
                    SettingKey = key,
                    SettingValue = value,
                    Description = "AI Smart Import local OCR runtime setting."
                });
            else setting.SettingValue = value;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void Validate(AIImportOcrRuntimeUpdate value)
    {
        var languages = value.Languages?.Trim().ToLowerInvariant();
        if (languages is not ("vie+eng" or "vie" or "eng")
            || value.ReviewConfidenceThreshold is < 0 or > 1
            || value.RenderDpi is < 72 or > 600
            || value.MaxPages is < 1 or > 500
            || value.MaxRenderedPixelsPerPage < 1 || value.MaxTotalRenderedPixels < value.MaxRenderedPixelsPerPage
            || value.PageTimeoutSeconds is < 1 or > 600 || value.TotalTimeoutSeconds < value.PageTimeoutSeconds
            || value.MaxConcurrentPages is < 1 or > 16)
            throw new ArgumentException("Cấu hình OCR không hợp lệ.");
    }

    private static string Value(IReadOnlyDictionary<string, string> values, string key, string fallback) =>
        string.IsNullOrWhiteSpace(values.GetValueOrDefault(key)) ? fallback : values[key];
    private static bool Bool(IReadOnlyDictionary<string, string> values, string key, bool fallback) =>
        bool.TryParse(values.GetValueOrDefault(key), out var value) ? value : fallback;
    private static int Int(IReadOnlyDictionary<string, string> values, string key, int fallback) =>
        int.TryParse(values.GetValueOrDefault(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    private static long Long(IReadOnlyDictionary<string, string> values, string key, long fallback) =>
        long.TryParse(values.GetValueOrDefault(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    private static decimal Decimal(IReadOnlyDictionary<string, string> values, string key, decimal fallback) =>
        decimal.TryParse(values.GetValueOrDefault(key), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    private static string Limit(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Length <= max ? value : value[..max];
}
