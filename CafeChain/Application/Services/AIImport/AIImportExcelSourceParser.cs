using CafeChain.Application.DTOs.AIImport;
using CafeChain.Models.AIImport;

namespace CafeChain.Application.Services.AIImport;

public sealed class AIImportExcelSourceParser(
    IAIImportExcelParser parser,
    IAIImportRegionAnalyzer analyzer) : IAIImportSourceParser
{
    public string SourceFormat => AIImportSourceFormats.Xlsx;

    public async Task<AIImportSourceDocument> ParseAsync(
        AIImportSourceFile source,
        AIImportEntityType? entityHint,
        CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream(source.Content, writable: false);
        var workbook = await parser.ParseAsync(stream, cancellationToken);
        var result = new AIImportSourceDocument
        {
            SourceFormat = SourceFormat,
            Metadata = new Dictionary<string, object?>
            {
                ["regionCount"] = workbook.Regions.Count
            },
            Warnings = workbook.Warnings,
            Errors = workbook.Errors
        };
        if (result.Errors.Count > 0) return result;

        foreach (var region in workbook.Regions)
        {
            var analysis = await analyzer.AnalyzeAsync(region, entityHint, cancellationToken);
            result.UsedAI |= analysis.UsedAI;
            if (analysis.UsedAI) result.AiChunkCount++;
            var headers = region.ReadRow(analysis.HeaderRow).Values
                .Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim()).ToList();
            var group = new AIImportSourceGroup
            {
                SourceLabel = region.SheetName,
                SourceLocator = new AIImportSourceLocator
                {
                    SourceFormat = SourceFormat,
                    Sheet = region.SheetName,
                    Region = region.Address,
                    Row = analysis.HeaderRow
                },
                ExtractionMode = analysis.UsedAI
                    ? AIImportExtractionModes.XlsxAiMapping
                    : AIImportExtractionModes.XlsxDeterministic,
                HeaderOrdinal = analysis.HeaderRow,
                EntityType = analysis.EntityType,
                Mapping = analysis.Mapping,
                SourceHeaders = headers,
                Confidence = analysis.Confidence
            };

            foreach (var rowNumber in Enumerable.Range(
                         analysis.HeaderRow + 1,
                         Math.Max(0, region.MaxRow - analysis.HeaderRow)))
            {
                var (raw, trace) = ReadNamedRow(region, analysis.HeaderRow, rowNumber);
                if (raw.Values.All(string.IsNullOrWhiteSpace) || IsFooterRow(raw)) continue;
                group.Candidates.Add(new AIImportSourceCandidate
                {
                    SortOrder = rowNumber,
                    RawData = raw,
                    MappedData = ApplyMapping(raw, analysis.Mapping),
                    SourceTrace = trace,
                    SourceLocator = new AIImportSourceLocator
                    {
                        SourceFormat = SourceFormat,
                        Sheet = region.SheetName,
                        Region = region.Address,
                        Row = rowNumber
                    },
                    EvidenceSnippet = string.Join(" | ", raw.Select(pair => $"{pair.Key}: {pair.Value}")),
                    Confidence = analysis.Confidence,
                    AiConfidence = analysis.UsedAI ? analysis.Confidence : null,
                    AIErrorCode = analysis.AIErrorCode
                });
            }
            result.Groups.Add(group);
        }

        return result;
    }

    private static Dictionary<string, string?> ApplyMapping(
        IReadOnlyDictionary<string, string?> raw,
        IReadOnlyDictionary<string, string?> mapping) => mapping.ToDictionary(
        pair => pair.Key,
        pair => string.IsNullOrWhiteSpace(pair.Value) ? null : raw.GetValueOrDefault(pair.Value),
        StringComparer.OrdinalIgnoreCase);

    private static (Dictionary<string, string?> Raw, Dictionary<string, string?> Trace) ReadNamedRow(
        AIImportRegionData region,
        int headerRow,
        int sourceRow)
    {
        var headers = region.ReadRow(headerRow);
        var values = region.ReadRow(sourceRow);
        var raw = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var trace = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (column, headerValue) in headers)
        {
            var header = string.IsNullOrWhiteSpace(headerValue) ? null : headerValue.Trim();
            if (header == null || raw.ContainsKey(header)) continue;
            raw[header] = values.GetValueOrDefault(column);
            trace[header] = $"{region.SheetName}!{column}{sourceRow}";
        }
        return (raw, trace);
    }

    private static bool IsFooterRow(IReadOnlyDictionary<string, string?> raw)
    {
        var values = raw.Values.Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(AIImportSchemaRegistry.Key).ToList();
        if (values.Count == 0) return true;
        var first = values[0];
        return values.Count <= 3 && (first.StartsWith("tong", StringComparison.Ordinal)
                                     || first.StartsWith("total", StringComparison.Ordinal)
                                     || first.StartsWith("ghichu", StringComparison.Ordinal)
                                     || first.StartsWith("note", StringComparison.Ordinal));
    }
}
