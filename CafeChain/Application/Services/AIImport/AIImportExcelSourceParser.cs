using CafeChain.Application.DTOs.AIImport;
using CafeChain.Models.AIImport;

namespace CafeChain.Application.Services.AIImport;

public sealed class AIImportExcelSourceParser(
    IAIImportExcelParser parser,
    IAIImportRegionAnalyzer analyzer,
    IAIImportSchemaRegistry schemas) : IAIImportSourceParser
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
            var headerValues = Enumerable.Range(region.MinColumn, region.MaxColumn - region.MinColumn + 1)
                .Select(column => region.Cells.GetValueOrDefault((analysis.HeaderRow, column))).ToList();
            var headerColumns = AIImportSourceColumnBuilder.Build(headerValues, region.MinColumn);
            var categoryDetection = schemas.Detect(headerColumns.Select(column => column.Label), region.SheetName, AIImportEntityType.Category);
            var drinkDetection = schemas.Detect(headerColumns.Select(column => column.Label), region.SheetName, AIImportEntityType.Drink);
            var categoryMapping = AIImportSourceColumnBuilder.RebindMapping(categoryDetection.Mapping, headerColumns);
            var drinkMapping = AIImportSourceColumnBuilder.RebindMapping(drinkDetection.Mapping, headerColumns);
            var splitCategoryDrink = HasAllRequired(AIImportEntityType.Category, categoryMapping)
                                     && HasAllRequired(AIImportEntityType.Drink, drinkMapping);
            var entityType = splitCategoryDrink ? AIImportEntityType.Drink : analysis.EntityType;
            var mapping = splitCategoryDrink
                ? drinkMapping
                : AIImportSourceColumnBuilder.RebindMapping(analysis.Mapping, headerColumns);
            var detectedMapping = splitCategoryDrink ? drinkDetection.Mapping : analysis.Mapping;
            var sourceColumns = headerColumns.Select(column => new AIImportSourceColumn
            {
                Key = column.Key,
                Label = column.Label,
                SourceLocator = new AIImportSourceLocator
                {
                    SourceFormat = SourceFormat,
                    Sheet = region.SheetName,
                    Region = region.Address,
                    Row = analysis.HeaderRow,
                    Column = AIImportRegionData.ColumnName(region.MinColumn + column.Index)
                }
            }).ToList();
            if (entityType != AIImportEntityType.Unknown)
                sourceColumns = schemas.ClassifyColumns(entityType, sourceColumns, mapping);
            if (splitCategoryDrink)
                MarkOtherProjectionIgnored(sourceColumns, categoryMapping, "Category");
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
                SourceRegionId = $"XLSX:{region.SheetName}:{region.Address}",
                BoundingRange = region.Address,
                HeaderRange = $"{AIImportRegionData.ColumnName(region.MinColumn)}{analysis.HeaderRow}:{AIImportRegionData.ColumnName(region.MaxColumn)}{analysis.HeaderRow}",
                DataRange = analysis.HeaderRow < region.MaxRow
                    ? $"{AIImportRegionData.ColumnName(region.MinColumn)}{analysis.HeaderRow + 1}:{AIImportRegionData.ColumnName(region.MaxColumn)}{region.MaxRow}"
                    : null,
                HeaderOrdinal = analysis.HeaderRow,
                EntityType = entityType,
                Mapping = mapping,
                SourceHeaders = headerColumns.Select(column => column.Key).ToList(),
                SourceColumns = sourceColumns,
                Confidence = analysis.Confidence,
                LayoutConfidence = region.Issues.Count == 0 ? 1m : 0.75m
            };

            var duplicateLabels = headerColumns.GroupBy(column => column.Label, StringComparer.OrdinalIgnoreCase)
                .Where(columns => columns.Count() > 1).ToList();
            foreach (var duplicate in duplicateLabels)
            {
                var candidateSourceKeys = duplicate.Select(column => column.Key).ToArray();
                var targetFields = detectedMapping
                    .Where(pair => string.Equals(pair.Value, duplicate.Key, StringComparison.OrdinalIgnoreCase))
                    .Select(pair => pair.Key)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                foreach (var targetField in targetFields)
                    group.Issues.Add(AIImportValidationContract.Issue(
                        "XUNG_ĐỘT_ÁNH_XẠ",
                        $"Header '{duplicate.Key}' xuất hiện nhiều lần; cần chọn cột nguồn cụ thể cho '{targetField}'.",
                        AIImportIssueSeverities.Review,
                        field: targetField,
                        locator: Position(group.SourceLocator),
                        resolution: AIImportIssueResolutions.RemapGroup,
                        metadata: new Dictionary<string, object?>
                        {
                            ["sourceHeader"] = duplicate.Key,
                            ["targetField"] = targetField,
                            ["candidateSourceKeys"] = candidateSourceKeys
                        }));
            }
            group.Issues.AddRange(region.Issues);

            foreach (var rowNumber in Enumerable.Range(
                         analysis.HeaderRow + 1,
                         Math.Max(0, region.MaxRow - analysis.HeaderRow)))
            {
                var (raw, trace) = ReadNamedRow(region, rowNumber, headerColumns);
                if (raw.Values.All(string.IsNullOrWhiteSpace) || IsFooterRow(raw)
                    || IsRepeatedHeader(raw, headerColumns)) continue;
                var mapped = ApplyMapping(raw, mapping);
                var candidate = new AIImportSourceCandidate
                {
                    SortOrder = rowNumber,
                    RawData = raw,
                    MappedData = mapped,
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
                    LayoutConfidence = group.LayoutConfidence,
                    AiConfidence = analysis.UsedAI ? analysis.Confidence : null,
                    AIErrorCode = analysis.AIErrorCode
                };
                candidate.Issues.AddRange(group.Issues);
                foreach (var column in sourceColumns.Where(column => !string.IsNullOrWhiteSpace(raw.GetValueOrDefault(column.Key))))
                {
                    if (column.Classification == AIImportColumnClassifications.Forbidden)
                        candidate.Issues.Add(AIImportValidationContract.Issue(
                            "CỘT_CẤM", $"Cột '{column.Label}' không được phép dùng trong AI Smart Import.",
                            AIImportIssueSeverities.Error, locator: Position(column.SourceLocator),
                            resolution: AIImportIssueResolutions.ReuploadOrSkip,
                            metadata: new Dictionary<string, object?> { ["sourceColumn"] = column.Key }));
                    else if (column.Classification == AIImportColumnClassifications.Unknown)
                        candidate.Issues.Add(AIImportValidationContract.Issue(
                            "CỘT_KHÔNG_XÁC_ĐỊNH", $"Cột '{column.Label}' không thuộc ImportSchema và sẽ bị bỏ qua.",
                            AIImportIssueSeverities.Warning, locator: Position(column.SourceLocator),
                            resolution: AIImportIssueResolutions.Acknowledge,
                            metadata: new Dictionary<string, object?> { ["sourceColumn"] = column.Key }));
                }
                group.Candidates.Add(candidate);
            }
            if (splitCategoryDrink)
            {
                var categoryGroup = BuildCategoryGroup(group, headerColumns, categoryMapping, categoryDetection.Confidence);
                group.Candidates.RemoveAll(candidate => !HasRequiredPayload(AIImportEntityType.Drink, candidate.MappedData));
                if (group.Candidates.Count > 0) result.Groups.Add(group);
                if (categoryGroup.Candidates.Count > 0) result.Groups.Add(categoryGroup);
            }
            else result.Groups.Add(group);
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
        int sourceRow,
        IReadOnlyList<(int Index, string Key, string Label)> columns)
    {
        var raw = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var trace = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in columns)
        {
            var sourceColumn = region.MinColumn + column.Index;
            raw[column.Key] = region.Cells.GetValueOrDefault((sourceRow, sourceColumn));
            trace[column.Key] = $"{region.SheetName}!{AIImportRegionData.ColumnName(sourceColumn)}{sourceRow}";
        }
        return (raw, trace);
    }

    private static AIImportPositionDto Position(AIImportSourceLocator locator) => new()
    {
        SourceFormat = locator.SourceFormat,
        Sheet = locator.Sheet,
        Region = locator.Region,
        Row = locator.Row,
        Column = locator.Column
    };

    private bool HasAllRequired(AIImportEntityType entity, IReadOnlyDictionary<string, string?> mapping) =>
        schemas.Get(entity).RequiredFields.All(field => mapping.TryGetValue(field, out var source)
                                                        && !string.IsNullOrWhiteSpace(source));

    private AIImportSourceGroup BuildCategoryGroup(
        AIImportSourceGroup drinkGroup,
        IReadOnlyList<(int Index, string Key, string Label)> headerColumns,
        Dictionary<string, string?> categoryMapping,
        decimal confidence)
    {
        var columns = schemas.ClassifyColumns(AIImportEntityType.Category,
            drinkGroup.SourceColumns.Select(column => new AIImportSourceColumn
            {
                Key = column.Key,
                Label = column.Label,
                SourceLocator = column.SourceLocator
            }), categoryMapping);
        MarkOtherProjectionIgnored(columns, drinkGroup.Mapping, "Drink");
        var categoryGroup = new AIImportSourceGroup
        {
            SourceLabel = $"{drinkGroup.SourceLabel} · Danh mục",
            SourceLocator = drinkGroup.SourceLocator,
            ExtractionMode = drinkGroup.ExtractionMode,
            SourceRegionId = $"{drinkGroup.SourceRegionId}:CATEGORY",
            BoundingRange = drinkGroup.BoundingRange,
            HeaderRange = drinkGroup.HeaderRange,
            DataRange = drinkGroup.DataRange,
            HeaderOrdinal = drinkGroup.HeaderOrdinal,
            EntityType = AIImportEntityType.Category,
            Mapping = categoryMapping,
            SourceHeaders = headerColumns.Select(column => column.Key).ToList(),
            SourceColumns = columns,
            Confidence = confidence,
            LayoutConfidence = drinkGroup.LayoutConfidence
        };
        foreach (var source in drinkGroup.Candidates)
        {
            var mapped = ApplyMapping(source.RawData, categoryMapping);
            if (!HasRequiredPayload(AIImportEntityType.Category, mapped)) continue;
            var candidate = new AIImportSourceCandidate
            {
                SortOrder = source.SortOrder,
                RawData = source.RawData,
                MappedData = mapped,
                SourceTrace = source.SourceTrace,
                SourceLocator = source.SourceLocator,
                EvidenceSnippet = source.EvidenceSnippet,
                Confidence = confidence,
                LayoutConfidence = source.LayoutConfidence
            };
            foreach (var issue in source.Issues.Where(issue => issue.Code is "VÙNG_DỮ_LIỆU_CHỒNG_LẤN" or "XUNG_ĐỘT_ÁNH_XẠ"))
                candidate.Issues.Add(issue);
            categoryGroup.Candidates.Add(candidate);
        }
        return categoryGroup;
    }

    private static void MarkOtherProjectionIgnored(
        IEnumerable<AIImportSourceColumn> columns,
        IReadOnlyDictionary<string, string?> otherMapping,
        string projection)
    {
        var projected = otherMapping.Values.Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var column in columns.Where(column => column.Classification == AIImportColumnClassifications.Unknown
                                                       && projected.Contains(column.Key)))
        {
            column.Classification = AIImportColumnClassifications.Ignored;
            column.Reason = $"Cột thuộc projection {projection} trong cùng region.";
        }
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

    private bool HasRequiredPayload(AIImportEntityType entity, IReadOnlyDictionary<string, string?> mapped) =>
        schemas.Get(entity).RequiredFields.All(field => mapped.TryGetValue(field, out var value)
                                                        && !string.IsNullOrWhiteSpace(value));

    private static bool IsRepeatedHeader(
        IReadOnlyDictionary<string, string?> raw,
        IReadOnlyList<(int Index, string Key, string Label)> columns)
    {
        var populated = columns.Where(column => !string.IsNullOrWhiteSpace(raw.GetValueOrDefault(column.Key))).ToList();
        return populated.Count >= 2 && populated.All(column =>
            string.Equals(AIImportSchemaRegistry.Key(raw.GetValueOrDefault(column.Key)!),
                AIImportSchemaRegistry.Key(column.Label), StringComparison.Ordinal));
    }
}
