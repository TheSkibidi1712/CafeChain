using CafeChain.Application.Interfaces.AI;
using CafeChain.Infrastructure.Configurations;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace CafeChain.Application.Services.AI;

public sealed class AISuggestionHistoryStore : IAISuggestionHistoryStore
{
    private readonly IMemoryCache _cache;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AIOptions _options;

    public AISuggestionHistoryStore(
        IMemoryCache cache,
        IHttpContextAccessor httpContextAccessor,
        IOptions<AIOptions> options)
    {
        _cache = cache;
        _httpContextAccessor = httpContextAccessor;
        _options = options.Value;
    }

    public IReadOnlyList<string> Get(string entityType) =>
        _cache.TryGetValue<IReadOnlyList<string>>(BuildKey(entityType), out var values)
            ? values ?? []
            : [];

    public void Add(string entityType, IEnumerable<string> suggestions)
    {
        var combined = Get(entityType)
            .Concat(suggestions)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .TakeLast(Math.Clamp(_options.SuggestionHistoryLimit, 3, 100))
            .ToArray();
        _cache.Set(BuildKey(entityType), combined,
            TimeSpan.FromMinutes(Math.Clamp(_options.SuggestionHistoryMinutes, 1, 1440)));
    }

    private string BuildKey(string entityType)
    {
        var context = _httpContextAccessor.HttpContext;
        var actor = context?.User.FindFirstValue("StaffId")
            ?? context?.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? "anonymous";
        var session = context?.Session?.Id ?? "no-session";
        return $"ai:suggestions:{actor}:{session}:{entityType.Trim().ToLowerInvariant()}";
    }
}
