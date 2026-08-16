using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CafeChain.Application.DTOs.AIImport;
using CafeChain.Application.Options;
using CafeChain.Application.Services.AI;
using CafeChain.Models.AIImport;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace CafeChain.Application.Services.AIImport;

public sealed partial class AIImportPdfSourceParser : IAIImportSourceParser
{
    private readonly AIImportOptions _options;
    private readonly IAIImportSchemaRegistry _schemas;
    private readonly IAIImportOcrProvider? _ocrProvider;

    public AIImportPdfSourceParser(IOptions<AIImportOptions> options)
        : this(options, new AIImportSchemaRegistry(), null)
    {
    }

    public AIImportPdfSourceParser(IOptions<AIImportOptions> options, IAIImportSchemaRegistry schemas)
        : this(options, schemas, null)
    {
    }

    public AIImportPdfSourceParser(
        IOptions<AIImportOptions> options,
        IAIImportSchemaRegistry schemas,
        IAIImportOcrProvider? ocrProvider)
    {
        _options = options.Value;
        _schemas = schemas;
        _ocrProvider = ocrProvider;
    }

    public string SourceFormat => AIImportSourceFormats.Pdf;

    public async Task<AIImportSourceDocument> ParseAsync(
        AIImportSourceFile source,
        AIImportEntityType? entityHint,
        CancellationToken cancellationToken)
    {
        var result = new AIImportSourceDocument { SourceFormat = SourceFormat };
        // Security preflight must finish before the PDF classifier or OCR provider can run.
        if (!ValidateBytes(source.Content, result)) return result;

        try
        {
            using var document = PdfDocument.Open(source.Content);
            if (document.NumberOfPages > _options.PdfMaxPages)
            {
                result.Errors.Add(Error("PDF_VƯỢT_GIỚI_HẠN", $"PDF vượt giới hạn {_options.PdfMaxPages} trang."));
                return result;
            }

            var allLines = new List<PdfLine>();
            var pagesNeedingOcr = new List<int>();
            var pageClassifications = new Dictionary<int, string>();
            var pageDimensions = new Dictionary<int, (double Width, double Height)>();
            var ambiguousReadingOrderPages = new HashSet<int>();
            var extractedCharacters = 0;
            var blockCount = 0;
            var imageCount = 0;
            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var images = page.GetImages().ToList();
                imageCount += images.Count;
                if (imageCount > _options.PdfMaxImages)
                {
                    result.Errors.Add(Error("PDF_VƯỢT_GIỚI_HẠN", "PDF vượt giới hạn số lượng hình ảnh."));
                    return result;
                }
                var pageArea = page.Width * page.Height;
                var hasSignificantImage = pageArea > 0 && images.Any(image =>
                    (decimal)(image.Bounds.Area / pageArea) >= _options.PdfOcrImageAreaRatioThreshold);
                var words = NearestNeighbourWordExtractor.Instance.GetWords(page.Letters).ToList();
                var hasText = words.Sum(word => word.Text.Count(char.IsLetterOrDigit)) >= 2;
                var classification = hasText
                    ? hasSignificantImage ? AIImportPdfPageClassifications.Mixed : AIImportPdfPageClassifications.TextBased
                    : AIImportPdfPageClassifications.ImageBased;
                pageClassifications[page.Number] = classification;
                pageDimensions[page.Number] = ((double)page.Width, (double)page.Height);
                if (classification != AIImportPdfPageClassifications.TextBased) pagesNeedingOcr.Add(page.Number);

                var lines = BuildLines(page.Number, words, (double)page.Width, (double)page.Height, page.Rotation.Value);
                if (HasAmbiguousReadingOrder(lines, entityHint))
                {
                    ambiguousReadingOrderPages.Add(page.Number);
                    result.Warnings.Add(AIImportValidationContract.Issue(
                        "THỨ_TỰ_ĐỌC_PDF_KHÔNG_RÕ",
                        $"Không xác định được thứ tự đọc duy nhất trên trang {page.Number}; candidate liên quan phải được xem lại.",
                        AIImportIssueSeverities.Review,
                        locator: new AIImportPositionDto { SourceFormat = SourceFormat, Page = page.Number },
                        resolution: AIImportIssueResolutions.ManualReview));
                }
                blockCount += lines.Count;
                extractedCharacters += lines.Sum(line => line.Text.Length);
                if (blockCount > _options.PdfMaxTextBlocks || extractedCharacters > _options.DocumentMaxExtractedCharacters)
                {
                    result.Errors.Add(Error("PDF_VƯỢT_GIỚI_HẠN", "PDF vượt giới hạn block hoặc ký tự trích xuất."));
                    return result;
                }
                allLines.AddRange(lines);
            }

            result.Metadata["imageCount"] = imageCount;
            result.Metadata["pageClassifications"] = pageClassifications;
            if (pagesNeedingOcr.Count > 0 && !source.UseOcr)
            {
                result.Errors.Add(new AIImportErrorDto
                {
                    Code = "PDF_CẦN_OCR",
                    Message = $"PDF cần OCR ở trang {string.Join(", ", pagesNeedingOcr.Take(10))}; OCR hiện đang tắt.",
                    Position = new AIImportPositionDto { SourceFormat = SourceFormat, Page = pagesNeedingOcr[0] }
                });
                return result;
            }

            if (pagesNeedingOcr.Count > 0)
            {
                var resourceIssue = ValidateOcrResources(pagesNeedingOcr, pageDimensions, source.OcrRuntime);
                if (resourceIssue != null)
                {
                    result.Errors.Add(resourceIssue);
                    return result;
                }
                if (_ocrProvider == null)
                {
                    result.Errors.Add(Error("PDF_OCR_KHÔNG_KHẢ_DỤNG", "OCR đã bật nhưng provider chưa được đăng ký."));
                    return result;
                }

                var ocr = await _ocrProvider.RecognizeAsync(
                    new AIImportOcrRequest(source.Content, pagesNeedingOcr,
                        source.ContentType ?? "application/pdf",
                        source.OcrRuntime?.Languages ?? _options.OcrLanguages,
                        source.OcrRuntime?.RenderDpi ?? _options.OcrRenderDpi,
                        source.OcrRuntime?.MaxConcurrentPages ?? _options.OcrMaxConcurrentPages,
                        source.OcrRuntime?.PageTimeoutSeconds,
                        source.OcrRuntime?.TotalTimeoutSeconds),
                    cancellationToken);
                if (!ocr.Success)
                {
                    result.Errors.Add(Error(ocr.ErrorCode ?? "OCR_OUTPUT_KHÔNG_HỢP_LỆ",
                        ocr.ErrorMessage ?? "OCR provider trả về kết quả không hợp lệ."));
                    return result;
                }
                if (pagesNeedingOcr.Except(ocr.Pages.Select(page => page.PageNumber)).Any())
                {
                    result.Errors.Add(Error("OCR_OUTPUT_KHÔNG_HỢP_LỆ", "OCR provider không trả đủ các trang đã yêu cầu."));
                    return result;
                }

                foreach (var page in ocr.Pages.OrderBy(page => page.PageNumber))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    allLines.AddRange(BuildOcrLines(page));
                    result.OcrPages.Add(new AIImportOcrPageSnapshot
                    {
                        PageNumber = page.PageNumber,
                        Text = page.Text,
                        Words = page.Words,
                        Provider = ocr.Provider,
                        ProviderVersion = ocr.ProviderVersion,
                        ExtractionVersion = result.ExtractionVersion
                    });
                }
                result.OcrUsed = true;
                result.OcrPageCount = ocr.Pages.Count;
                result.OcrProvider = ocr.Provider;
                result.OcrProviderVersion = ocr.ProviderVersion;
                result.Metadata["ocrUsed"] = true;
                result.Metadata["ocrPageCount"] = result.OcrPageCount;
                result.Metadata["ocrProvider"] = ocr.Provider;
                result.Metadata["ocrProviderVersion"] = ocr.ProviderVersion;
                result.Metadata["ocrExtractionVersion"] = result.ExtractionVersion;
                var ocrConfidence = ocr.Pages.SelectMany(page => page.Words)
                    .Select(word => word.Confidence).ToList();
                result.Metadata["ocrConfidenceSummary"] = ocrConfidence.Count == 0 ? null : new
                {
                    minimum = ocrConfidence.Min(),
                    average = ocrConfidence.Average(),
                    maximum = ocrConfidence.Max()
                };
            }

            if (allLines.Count == 0 || allLines.Sum(line => line.Text.Count(char.IsLetterOrDigit)) < 2)
            {
                result.Errors.Add(Error("PDF_CẦN_OCR", "PDF không có lớp text hoặc OCR đủ tin cậy."));
                return result;
            }

            var contentLines = AddTextOffsets(RemoveRepeatedDecorations(allLines));
            result.ExtractedText = string.Join(Environment.NewLine, contentLines.Select(line => $"[[PAGE:{line.Page} BLOCK:{line.Block}]] {line.Text}"));
            result.Blocks.AddRange(contentLines.Select((line, index) => new AIImportSemanticBlock
            {
                Ordinal = index + 1,
                Kind = line.Cells.Count > 1 ? "TABLE_ROW" : "PAGE_BLOCK",
                Text = line.Text,
                Locator = Locator(line)
            }));
            var reviewThreshold = source.OcrRuntime?.ReviewConfidenceThreshold ?? _options.OcrReviewConfidenceThreshold;
            var tableLines = ExtractTables(contentLines, entityHint, result, reviewThreshold);
            ExtractKeyValueRecords(contentLines.Where(line => !tableLines.Contains((line.Page, line.Block))).ToList(), entityHint, result, reviewThreshold);
            MergeCompatibleMultiPageTables(result);
            AttachReadingOrderReview(result, ambiguousReadingOrderPages);
            result.Metadata["pageCount"] = document.NumberOfPages;
            result.Metadata["textBlockCount"] = blockCount;
            result.Metadata["extractedCharacters"] = extractedCharacters;
            result.Metadata.TryAdd("ocrUsed", false);
            result.Metadata["extractionVersion"] = result.ExtractionVersion;

            if (result.Groups.SelectMany(group => group.Candidates).Any()) return result;
            result.Errors.Add(Error("BỐ_CỤC_PDF_KHÔNG_RÕ", "Không xác định được bản ghi nghiệp vụ từ bố cục PDF."));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var encrypted = Encoding.Latin1.GetString(source.Content).Contains("/Encrypt", StringComparison.Ordinal);
            result.Errors.Add(encrypted
                ? Error("PDF_CÓ_MẬT_KHẨU", "PDF được mã hóa hoặc có mật khẩu không được hỗ trợ.")
                : Error("PDF_BỊ_HỎNG", "PDF bị hỏng hoặc không thể đọc an toàn."));
        }

        return result;
    }

    private bool ValidateBytes(byte[] content, AIImportSourceDocument result)
    {
        if (content.Length < 5 || !content.AsSpan(0, Math.Min(content.Length, 8)).StartsWith("%PDF-"u8))
        {
            result.Errors.Add(Error("PDF_BỊ_HỎNG", "Phần mở rộng PDF không khớp nội dung tệp."));
            return false;
        }
        var source = Encoding.Latin1.GetString(content);
        if (source.Contains("/Encrypt", StringComparison.Ordinal))
        {
            result.Errors.Add(Error("PDF_CÓ_MẬT_KHẨU", "PDF được mã hóa hoặc có mật khẩu không được hỗ trợ."));
            return false;
        }
        var unsafeMarkers = new[] { "/EmbeddedFile", "/Filespec", "/JavaScript", "/JS ", "/Launch", "/URI" };
        if (unsafeMarkers.Any(marker => source.Contains(marker, StringComparison.OrdinalIgnoreCase)))
        {
            result.Errors.Add(Error("NỘI_DUNG_CHỦ_ĐỘNG_KHÔNG_ĐƯỢC_HỖ_TRỢ", "PDF chứa tệp nhúng, liên kết hoặc hành động chủ động không được hỗ trợ."));
            return false;
        }
        var imageCount = ImagePattern().Count(source);
        if (imageCount > _options.PdfMaxImages)
        {
            result.Errors.Add(Error("PDF_VƯỢT_GIỚI_HẠN", "PDF vượt giới hạn số lượng hình ảnh."));
            return false;
        }
        result.Metadata["imageCount"] = imageCount;
        return true;
    }

    private void ExtractKeyValueRecords(
        IReadOnlyList<PdfLine> lines,
        AIImportEntityType? entityHint,
        AIImportSourceDocument result,
        decimal reviewThreshold)
    {
        var records = lines.GroupBy(line => line.Page)
            .SelectMany(page => BuildKeyValueTracks(page, entityHint)
                .Select(track => new KeyValueTrack(page.Key, track)));
        var ordinal = 0;
        foreach (var page in records)
        {
            var raw = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var evidence = new List<PdfLine>();
            foreach (var line in page)
            {
                if (!TryParseKeyValue(line, entityHint, out var key, out var value)) continue;
                if (raw.ContainsKey(key))
                {
                    result.Errors.Add(AIImportValidationContract.Issue(
                        "KHÔNG_XÁC_ĐỊNH_RANH_GIỚI_BẢN_GHI",
                        $"Nhãn '{key}' lặp lại trên trang {page.Key}; không thể ghép bản ghi an toàn.",
                        AIImportIssueSeverities.Error,
                        locator: new AIImportPositionDto { SourceFormat = SourceFormat, Page = page.Key, Block = line.Block },
                        resolution: AIImportIssueResolutions.ReuploadOrSkip));
                    raw.Clear();
                    break;
                }
                raw[key] = value;
                evidence.Add(line);
            }
            if (raw.Count < 2) continue;

            var detected = _schemas.Detect(raw.Keys, $"Trang {page.Key}", entityHint);
            var first = evidence[0];
            var locator = new AIImportSourceLocator
            {
                SourceFormat = SourceFormat,
                Page = page.Key,
                Block = first.Block,
                TextStart = first.TextStart,
                TextEnd = evidence[^1].TextEnd,
                BoundingBox = first.Box
            };
            var group = new AIImportSourceGroup
            {
                SourceLabel = $"Trang {page.Key}",
                SourceLocator = locator,
                ExtractionMode = ExtractionMode(evidence),
                SourceRegionId = $"PDF:P{page.Key}:KV:{first.Block}",
                HeaderOrdinal = first.Block,
                EntityType = detected.EntityType,
                Mapping = detected.Mapping,
                SourceHeaders = raw.Keys.ToList(),
                Confidence = detected.Confidence,
                LayoutConfidence = 0.90m
            };
            var mapped = detected.Mapping.ToDictionary(pair => pair.Key,
                pair => string.IsNullOrWhiteSpace(pair.Value) ? null : raw.GetValueOrDefault(pair.Value), StringComparer.OrdinalIgnoreCase);
            var candidate = new AIImportSourceCandidate
            {
                SortOrder = ++ordinal,
                RawData = raw,
                MappedData = mapped,
                SourceTrace = raw.Keys.ToDictionary(key => key, _ => JsonSerializer.Serialize(locator), StringComparer.OrdinalIgnoreCase),
                SourceLocator = locator,
                EvidenceSnippet = string.Join(Environment.NewLine, evidence.Select(line => line.RawText ?? line.Text)),
                Confidence = detected.Confidence,
                LayoutConfidence = 0.90m,
                OcrConfidence = AverageOcrConfidence(evidence),
                FieldEvidence = BuildFieldEvidence(detected.Mapping, raw, evidence, detected.EntityType)
            };
            AddOcrReviewIssues(candidate, detected.EntityType, reviewThreshold);
            group.Candidates.Add(candidate);
            result.Groups.Add(group);
        }
    }

    private IReadOnlyList<List<PdfLine>> BuildKeyValueTracks(
        IEnumerable<PdfLine> pageLines,
        AIImportEntityType? entityHint)
    {
        var tracks = new List<List<PdfLine>>();
        foreach (var line in pageLines
                     .Where(line => (line.Cells.Count == 1 || line.SourceKind == AIImportSourceKinds.Ocr)
                                    && TryParseKeyValue(line, entityHint, out _, out _))
                     .OrderBy(line => line.Box.X).ThenBy(line => line.Box.Y))
        {
            var pageWidth = line.Box.PageWidth.GetValueOrDefault(612d);
            var tolerance = Math.Max(72d, pageWidth * 0.20d);
            var track = tracks.FirstOrDefault(candidate =>
                candidate.All(item => item.SourceKind == line.SourceKind)
                && Math.Abs(candidate.Average(item => item.Box.X) - line.Box.X) <= tolerance);
            if (track == null) tracks.Add([line]); else track.Add(line);
        }
        return tracks;
    }

    private bool TryParseKeyValue(
        PdfLine line,
        AIImportEntityType? entityHint,
        out string key,
        out string value)
    {
        var match = KeyValuePattern().Match(line.Text);
        if (match.Success)
        {
            key = match.Groups[1].Value.Trim();
            value = match.Groups[2].Value.Trim();
            return true;
        }

        key = string.Empty;
        value = string.Empty;
        if (line.SourceKind != AIImportSourceKinds.Ocr || entityHint is null
            || entityHint == AIImportEntityType.Unknown) return false;

        var words = line.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length < 2) return false;
        var schema = _schemas.Get(entityHint.Value);
        var best = (Score: 0d, Split: 0, Field: (AIImportFieldDefinition?)null);
        for (var split = 1; split < words.Length; split++)
        {
            var prefix = string.Join(' ', words.Take(split));
            foreach (var field in schema.Fields)
            {
                var score = field.Aliases.Append(AIImportSchemaRegistry.Key(field.Name))
                    .Max(alias => AISuggestionUniquenessPolicy.NameSimilarity(prefix, alias));
                if (score > best.Score || (Math.Abs(score - best.Score) < 0.0001d && split > best.Split))
                    best = (score, split, field);
            }
        }

        if (best.Field == null || best.Score < 0.84d) return false;
        var parsedValue = string.Join(' ', words.Skip(best.Split)).Trim(' ', ':', '：', '-', '|');
        if (string.IsNullOrWhiteSpace(parsedValue)) return false;
        key = best.Field.Name;
        value = parsedValue;
        return true;
    }

    private HashSet<(int Page, int Block)> ExtractTables(
        IReadOnlyList<PdfLine> lines,
        AIImportEntityType? entityHint,
        AIImportSourceDocument result,
        decimal reviewThreshold)
    {
        var consumed = new HashSet<(int Page, int Block)>();
        foreach (var pageLines in lines.GroupBy(line => line.Page))
        {
            var ordered = pageLines.OrderBy(line => line.Block).ToList();
            for (var headerIndex = 0; headerIndex < ordered.Count - 1; headerIndex++)
            {
                var headerLine = ordered[headerIndex];
                if (headerLine.Cells.Count < 2 || consumed.Contains((headerLine.Page, headerLine.Block))) continue;
                var headers = headerLine.Cells.Select(cell => cell.Text).ToList();
                var detected = _schemas.Detect(headers, $"Trang {pageLines.Key}", entityHint);
                if (detected.EntityType == AIImportEntityType.Unknown
                    || !_schemas.Get(detected.EntityType).RequiredFields.All(field =>
                        detected.Mapping.TryGetValue(field, out var source) && !string.IsNullOrWhiteSpace(source)))
                    continue;

                var headerColumns = AIImportSourceColumnBuilder.Build(headers);
                var mapping = AIImportSourceColumnBuilder.RebindMapping(detected.Mapping, headerColumns);
                var group = new AIImportSourceGroup
                {
                    SourceLabel = $"Trang {pageLines.Key} · bảng {headerIndex + 1}",
                    SourceLocator = new AIImportSourceLocator
                    {
                        SourceFormat = SourceFormat,
                        Page = pageLines.Key,
                        Block = headerLine.Block,
                        TextStart = headerLine.TextStart,
                        TextEnd = headerLine.TextEnd,
                        BoundingBox = headerLine.Box
                    },
                    ExtractionMode = ExtractionMode([headerLine]),
                    SourceRegionId = $"PDF:P{pageLines.Key}:TABLE:{headerLine.Block}",
                    HeaderOrdinal = headerLine.Block,
                    EntityType = detected.EntityType,
                    Mapping = mapping,
                    SourceHeaders = headerColumns.Select(column => column.Key).ToList(),
                    SourceColumns = _schemas.ClassifyColumns(detected.EntityType,
                        headerColumns.Select(column => new AIImportSourceColumn
                        {
                            Key = column.Key,
                            Label = column.Label,
                            SourceLocator = new AIImportSourceLocator
                            {
                                SourceFormat = SourceFormat, Page = pageLines.Key, Block = headerLine.Block,
                                TextStart = headerLine.TextStart, TextEnd = headerLine.TextEnd,
                                BoundingBox = headerLine.Box
                            }
                        }), mapping),
                    Confidence = detected.Confidence,
                    LayoutConfidence = 0.95m
                };

                for (var rowIndex = headerIndex + 1; rowIndex < ordered.Count; rowIndex++)
                {
                    var row = ordered[rowIndex];
                    if (row.Cells.Count < 2) break;
                    var alignedCells = AlignTableCells(headerLine.Cells, row.Cells);
                    var raw = headerColumns.ToDictionary(column => column.Key,
                        column => alignedCells[column.Index]?.Text, StringComparer.OrdinalIgnoreCase);
                    var locator = new AIImportSourceLocator
                    {
                        SourceFormat = SourceFormat,
                        Page = pageLines.Key,
                        Block = row.Block,
                        TextStart = row.TextStart,
                        TextEnd = row.TextEnd,
                        BoundingBox = row.Box
                    };
                    var missingRequired = _schemas.Get(detected.EntityType).RequiredFields.Any(field =>
                        !mapping.TryGetValue(field, out var sourceKey)
                        || string.IsNullOrWhiteSpace(sourceKey)
                        || string.IsNullOrWhiteSpace(raw.GetValueOrDefault(sourceKey)));
                    if (missingRequired)
                    {
                        result.Warnings.Add(AIImportValidationContract.Issue("BẢNG_PDF_KHÔNG_RÕ",
                            "Hàng bảng bị thiếu ô bắt buộc hoặc bị cắt qua trang; không tự động ghép dữ liệu.",
                            AIImportIssueSeverities.Review,
                            locator: Position(locator), resolution: AIImportIssueResolutions.ManualReview));
                        break;
                    }
                    var candidate = new AIImportSourceCandidate
                    {
                        SortOrder = row.Block,
                        RawData = raw,
                        MappedData = mapping.ToDictionary(pair => pair.Key,
                            pair => string.IsNullOrWhiteSpace(pair.Value) ? null : raw.GetValueOrDefault(pair.Value), StringComparer.OrdinalIgnoreCase),
                        SourceTrace = raw.Keys.ToDictionary(key => key, _ => JsonSerializer.Serialize(locator), StringComparer.OrdinalIgnoreCase),
                        SourceLocator = locator,
                        EvidenceSnippet = row.RawText ?? row.Text,
                        Confidence = detected.Confidence,
                        LayoutConfidence = 0.95m,
                        OcrConfidence = row.OcrConfidence,
                        FieldEvidence = BuildTableFieldEvidence(mapping, headerColumns, raw, row, alignedCells)
                    };
                    AddColumnIssues(candidate, group.SourceColumns, raw);
                    AddOcrReviewIssues(candidate, detected.EntityType, reviewThreshold);
                    group.Candidates.Add(candidate);
                    consumed.Add((row.Page, row.Block));
                }

                if (group.Candidates.Count == 0) continue;
                if (group.Candidates.Any(candidate => candidate.OcrConfidence.HasValue)
                    && headerLine.SourceKind == AIImportSourceKinds.TextLayer)
                    group.ExtractionMode = AIImportExtractionModes.PdfMixedTextOcr;
                consumed.Add((headerLine.Page, headerLine.Block));
                result.Groups.Add(group);
                headerIndex += group.Candidates.Count;
            }
        }
        return consumed;
    }

    private static IReadOnlyList<PdfCell?> AlignTableCells(
        IReadOnlyList<PdfCell> headerCells,
        IReadOnlyList<PdfCell> rowCells)
    {
        var aligned = new PdfCell?[headerCells.Count];
        foreach (var cell in rowCells)
        {
            var target = Enumerable.Range(0, headerCells.Count)
                .OrderBy(index => Math.Abs(headerCells[index].X - cell.X))
                .First();
            aligned[target] = aligned[target] == null
                ? cell
                : aligned[target] with
                {
                    Text = NormalizeText($"{aligned[target]!.Text} {cell.Text}"),
                    RawText = $"{aligned[target]!.RawText ?? aligned[target]!.Text} {cell.RawText ?? cell.Text}"
                };
        }
        return aligned;
    }

    private static void MergeCompatibleMultiPageTables(AIImportSourceDocument result)
    {
        var tables = result.Groups.Where(group => group.SourceLocator.Page.HasValue
                                                   && group.SourceHeaders.Count > 1
                                                   && group.ExtractionMode != AIImportExtractionModes.PdfTextAiExtraction)
            .OrderBy(group => group.SourceLocator.Page).ThenBy(group => group.HeaderOrdinal).ToList();
        for (var index = 0; index < tables.Count - 1; index++)
        {
            var current = tables[index];
            var next = tables[index + 1];
            if (next.SourceLocator.Page != current.SourceLocator.Page + 1
                || next.EntityType != current.EntityType
                || !current.SourceHeaders.Select(AIImportSchemaRegistry.Key)
                    .SequenceEqual(next.SourceHeaders.Select(AIImportSchemaRegistry.Key), StringComparer.Ordinal)) continue;

            var currentBox = current.SourceLocator.BoundingBox;
            var nextBox = next.SourceLocator.BoundingBox;
            var geometryCompatible = currentBox != null && nextBox != null
                                     && Math.Abs(currentBox.X - nextBox.X) <= 12d
                                     && Math.Abs(currentBox.Width - nextBox.Width) <= Math.Max(20d, currentBox.Width * 0.15d);
            if (!geometryCompatible)
            {
                var issue = AIImportValidationContract.Issue("BẢNG_PDF_KHÔNG_RÕ",
                    "Bảng qua trang có cùng schema nhưng geometry không tương thích; không tự động ghép.",
                    AIImportIssueSeverities.Review,
                    locator: Position(next.SourceLocator), resolution: AIImportIssueResolutions.ManualReview);
                current.Issues.Add(issue);
                next.Issues.Add(issue);
                continue;
            }

            current.Candidates.AddRange(next.Candidates);
            result.Groups.Remove(next);
            tables.RemoveAt(index + 1);
            index--;
        }
    }

    private static void AttachReadingOrderReview(
        AIImportSourceDocument result,
        IReadOnlySet<int> ambiguousPages)
    {
        if (ambiguousPages.Count == 0) return;
        foreach (var group in result.Groups.Where(group => group.SourceLocator.Page is { } page
                                                           && ambiguousPages.Contains(page)))
        {
            var issue = AIImportValidationContract.Issue("THỨ_TỰ_ĐỌC_PDF_KHÔNG_RÕ",
                "Thứ tự đọc của trang PDF không duy nhất; cần đối chiếu candidate với evidence.",
                AIImportIssueSeverities.Review,
                locator: Position(group.SourceLocator), resolution: AIImportIssueResolutions.ManualReview);
            group.Issues.Add(issue);
            foreach (var candidate in group.Candidates) candidate.Issues.Add(issue);
        }
    }

    private static List<PdfLine> BuildLines(
        int pageNumber,
        IReadOnlyList<Word> words,
        double pageWidth,
        double pageHeight,
        int rotation)
    {
        var tokens = words
            .Where(word => !string.IsNullOrWhiteSpace(word.Text))
            .Select(word =>
            {
                var normalizedRotation = NormalizeRotation(rotation);
                var rowCoordinate = normalizedRotation is 90 or 270
                    ? word.BoundingBox.Left
                    : word.BoundingBox.Bottom;
                var orderCoordinate = normalizedRotation switch
                {
                    90 => -word.BoundingBox.Bottom,
                    180 => -word.BoundingBox.Left,
                    270 => word.BoundingBox.Bottom,
                    _ => word.BoundingBox.Left
                };
                return new PdfToken(word.Text.Trim(), NormalizePdfBox(
                    word.BoundingBox.Left, word.BoundingBox.Bottom,
                    word.BoundingBox.Width, word.BoundingBox.Height,
                    pageWidth, pageHeight, rotation), rowCoordinate, orderCoordinate);
            })
            .ToList();
        var rows = new List<List<PdfToken>>();
        foreach (var token in tokens.OrderBy(token => token.RowCoordinate).ThenBy(token => token.OrderCoordinate))
        {
            const double tolerance = 4d;
            var row = rows.FirstOrDefault(candidate =>
                Math.Abs(candidate.Average(item => item.RowCoordinate) - token.RowCoordinate) <= tolerance);
            if (row == null) rows.Add([token]); else row.Add(token);
        }

        return rows.OrderBy(row => row.Min(token => token.Box.Y))
            .Select((row, index) =>
            {
                var ordered = row.OrderBy(token => token.OrderCoordinate).ToList();
                var rawText = string.Join(" ", ordered.Select(token => token.Text));
                return new PdfLine(pageNumber, index + 1, NormalizeText(rawText),
                    UnionBox(ordered.Select(token => token.Box)),
                    BuildCells(ordered), AIImportSourceKinds.TextLayer, RawText: rawText);
            })
            .Where(line => !string.IsNullOrWhiteSpace(line.Text)).ToList();
    }

    private static AIImportBoundingBox UnionBox(IEnumerable<AIImportBoundingBox> source)
    {
        var boxes = source.ToList();
        var first = boxes[0];
        var left = boxes.Min(box => box.X);
        var top = boxes.Min(box => box.Y);
        var right = boxes.Max(box => box.X + box.Width);
        var bottom = boxes.Max(box => box.Y + box.Height);
        return new AIImportBoundingBox
        {
            X = left,
            Y = top,
            Width = right - left,
            Height = bottom - top,
            PageWidth = first.PageWidth,
            PageHeight = first.PageHeight,
            Rotation = first.Rotation,
            Unit = first.Unit,
            Polygon = [left, top, right, top, right, bottom, left, bottom]
        };
    }

    private static List<PdfLine> BuildOcrLines(AIImportOcrPage page)
    {
        var rows = HasReliableOcrTextOffsets(page)
            ? BuildOcrRowsFromTextOffsets(page)
            : BuildOcrRowsFromGeometry(page.Words);

        return rows.OrderBy(row => row.Min(word => word.BoundingBox.Y))
            .Select((row, index) =>
            {
                var ordered = row.OrderBy(word => word.BoundingBox.X).ToList();
                var left = ordered.Min(word => word.BoundingBox.X);
                var top = ordered.Min(word => word.BoundingBox.Y);
                var right = ordered.Max(word => word.BoundingBox.X + word.BoundingBox.Width);
                var bottom = ordered.Max(word => word.BoundingBox.Y + word.BoundingBox.Height);
                var polygon = new List<double> { left, top, right, top, right, bottom, left, bottom };
                var box = new AIImportBoundingBox
                {
                    X = left,
                    Y = top,
                    Width = right - left,
                    Height = bottom - top,
                    PageWidth = page.Width,
                    PageHeight = page.Height,
                    Rotation = NormalizeRotation(page.Rotation),
                    Unit = page.Unit.ToUpperInvariant(),
                    Polygon = polygon
                };
                var rawText = string.Join(" ", ordered.Select(word => word.Text));
                return new PdfLine(page.PageNumber, 100_000 + index + 1,
                    NormalizeText(rawText), box,
                    BuildOcrCells(ordered), AIImportSourceKinds.Ocr,
                    ordered.Average(word => word.Confidence), rawText);
            })
            .Where(line => !string.IsNullOrWhiteSpace(line.Text)).ToList();
    }

    private static bool HasReliableOcrTextOffsets(AIImportOcrPage page)
    {
        if (string.IsNullOrEmpty(page.Text) || page.Words.Count == 0) return false;
        var ordered = page.Words.OrderBy(word => word.Offset).ToList();
        return ordered[0].Offset >= 0
               && ordered.Select(word => word.Offset).Distinct().Count() == ordered.Count
               && ordered.All(word => word.Length > 0
                                      && word.Offset + word.Length <= page.Text.Length);
    }

    private static List<List<AIImportOcrWord>> BuildOcrRowsFromTextOffsets(AIImportOcrPage page)
    {
        var rows = new List<List<AIImportOcrWord>>();
        AIImportOcrWord? previous = null;
        foreach (var word in page.Words.OrderBy(word => word.Offset))
        {
            var startsNewLine = previous != null
                                && page.Text.AsSpan(previous.Offset + previous.Length,
                                        word.Offset - previous.Offset - previous.Length)
                                    .Contains('\n');
            if (rows.Count == 0 || startsNewLine) rows.Add([word]); else rows[^1].Add(word);
            previous = word;
        }
        return rows;
    }

    private static List<List<AIImportOcrWord>> BuildOcrRowsFromGeometry(
        IReadOnlyList<AIImportOcrWord> words)
    {
        var rows = new List<List<AIImportOcrWord>>();
        foreach (var word in words.OrderBy(word => word.BoundingBox.Y).ThenBy(word => word.BoundingBox.X))
        {
            var tolerance = Math.Max(3d, word.BoundingBox.Height * 0.6d);
            var row = rows.FirstOrDefault(candidate =>
                Math.Abs(candidate[0].BoundingBox.Y - word.BoundingBox.Y) <= tolerance);
            if (row == null) rows.Add([word]); else row.Add(word);
        }
        return rows;
    }

    private bool HasAmbiguousReadingOrder(IReadOnlyList<PdfLine> lines, AIImportEntityType? entityHint)
    {
        var splitLines = lines.Where(line => line.Cells.Count >= 2).ToList();
        if (splitLines.Count < 2) return false;
        var hasRecognizedHeader = splitLines.Any(line =>
            _schemas.Detect(line.Cells.Select(cell => cell.Text), $"Trang {line.Page}", entityHint).EntityType
            != AIImportEntityType.Unknown);
        if (hasRecognizedHeader) return false;
        var tracks = splitLines.Select(line => line.Cells.Count).Distinct().Count();
        return tracks > 1 || splitLines.Count >= 2;
    }

    private static List<PdfCell> BuildCells(IReadOnlyList<PdfToken> words)
    {
        var groups = new List<List<PdfToken>>();
        foreach (var word in words)
        {
            if (groups.Count == 0)
            {
                groups.Add([word]);
                continue;
            }
            var previous = groups[^1][^1];
            var averageLetterWidth = previous.Text.Length == 0 ? 4d : previous.Box.Width / previous.Text.Length;
            var gap = word.Box.X - (previous.Box.X + previous.Box.Width);
            if (gap > Math.Max(18d, averageLetterWidth * 4d)) groups.Add([word]);
            else groups[^1].Add(word);
        }
        return groups.Select(group => new PdfCell(
            NormalizeText(string.Join(" ", group.Select(word => word.Text))),
            X: group.Min(word => word.Box.X),
            Width: group.Max(word => word.Box.X + word.Box.Width) - group.Min(word => word.Box.X))).ToList();
    }

    private static List<PdfCell> BuildOcrCells(IReadOnlyList<AIImportOcrWord> words)
    {
        var groups = new List<List<AIImportOcrWord>>();
        foreach (var word in words)
        {
            if (groups.Count == 0)
            {
                groups.Add([word]);
                continue;
            }
            var previous = groups[^1][^1];
            var averageLetterWidth = previous.Text.Length == 0
                ? 4d : previous.BoundingBox.Width / previous.Text.Length;
            var gap = word.BoundingBox.X - (previous.BoundingBox.X + previous.BoundingBox.Width);
            if (gap > Math.Max(18d, averageLetterWidth * 4d)) groups.Add([word]);
            else groups[^1].Add(word);
        }
        return groups.Select(group =>
        {
            var raw = string.Join(" ", group.Select(word => word.Text));
            return new PdfCell(NormalizeText(raw), group.Average(word => word.Confidence), raw,
                group.Min(word => word.BoundingBox.X),
                group.Max(word => word.BoundingBox.X + word.BoundingBox.Width) - group.Min(word => word.BoundingBox.X));
        }).ToList();
    }

    private static IReadOnlyList<PdfLine> RemoveRepeatedDecorations(IReadOnlyList<PdfLine> lines)
    {
        var repeated = lines.GroupBy(line => AIImportSchemaRegistry.Key(line.Text))
            .Where(group => group.Select(line => line.Page).Distinct().Count() >= 2
                            && group.All(line => line.Cells.Count == 1)
                            && group.All(line => IsPageDecoration(line, lines)))
            .Select(group => group.Key).ToHashSet(StringComparer.Ordinal);
        return lines.Where(line => !repeated.Contains(AIImportSchemaRegistry.Key(line.Text))).ToList();
    }

    private static bool IsPageDecoration(PdfLine line, IReadOnlyList<PdfLine> lines)
    {
        if (line.Box.PageHeight is > 0)
        {
            var relativeTop = line.Box.Y / line.Box.PageHeight.Value;
            var relativeBottom = (line.Box.Y + line.Box.Height) / line.Box.PageHeight.Value;
            return relativeTop <= 0.12d || relativeBottom >= 0.88d;
        }
        var pageLineCount = lines.Count(candidate => candidate.Page == line.Page);
        return line.Block <= 2 || line.Block >= pageLineCount - 1;
    }

    private static IReadOnlyList<PdfLine> AddTextOffsets(IReadOnlyList<PdfLine> lines)
    {
        var located = new List<PdfLine>(lines.Count);
        var offset = 0;
        foreach (var line in lines)
        {
            var prefix = $"[[PAGE:{line.Page} BLOCK:{line.Block}]] ";
            var textStart = offset + prefix.Length;
            var textEnd = textStart + line.Text.Length;
            located.Add(line with { TextStart = textStart, TextEnd = textEnd });
            offset = textEnd + Environment.NewLine.Length;
        }
        return located;
    }

    private AIImportErrorDto? ValidateOcrResources(
        IReadOnlyList<int> pages,
        IReadOnlyDictionary<int, (double Width, double Height)> dimensions,
        AIImportOcrRuntimeState? runtime)
    {
        var maxPages = runtime?.MaxPages ?? _options.OcrMaxPages;
        var renderDpi = runtime?.RenderDpi ?? _options.OcrRenderDpi;
        var maxPixelsPerPage = runtime?.MaxRenderedPixelsPerPage ?? _options.OcrMaxRenderedPixelsPerPage;
        var maxTotalPixels = runtime?.MaxTotalRenderedPixels ?? _options.OcrMaxTotalRenderedPixels;
        if (pages.Count > maxPages)
            return Error("PDF_OCR_VƯỢT_GIỚI_HẠN", $"PDF cần OCR {pages.Count} trang, vượt giới hạn {maxPages} trang.");

        long totalPixels = 0;
        foreach (var page in pages)
        {
            if (!dimensions.TryGetValue(page, out var size))
                return Error("OCR_OUTPUT_KHÔNG_HỢP_LỆ", $"Không xác định được kích thước trang {page}.");
            var widthPixels = Math.Ceiling(size.Width / 72d * renderDpi);
            var heightPixels = Math.Ceiling(size.Height / 72d * renderDpi);
            var pixels = checked((long)Math.Min(long.MaxValue, widthPixels * heightPixels));
            if (pixels > maxPixelsPerPage)
                return Error("PDF_OCR_VƯỢT_GIỚI_HẠN", $"Trang {page} vượt giới hạn pixel OCR.");
            totalPixels = checked(totalPixels + pixels);
            if (totalPixels > maxTotalPixels)
                return Error("PDF_OCR_VƯỢT_GIỚI_HẠN", "Tổng số pixel ước tính vượt giới hạn OCR của tài liệu.");
        }
        return null;
    }

    private static AIImportSourceLocator Locator(PdfLine line) => new()
    {
        SourceFormat = AIImportSourceFormats.Pdf,
        Page = line.Page,
        Block = line.Block,
        TextStart = line.TextStart,
        TextEnd = line.TextEnd,
        BoundingBox = line.Box
    };

    private static AIImportBoundingBox NormalizePdfBox(
        double left,
        double bottom,
        double width,
        double height,
        double pageWidth,
        double pageHeight,
        int rotation)
    {
        var normalizedRotation = NormalizeRotation(rotation);
        var x = left;
        var y = pageHeight - (bottom + height);
        var normalizedWidth = width;
        var normalizedHeight = height;
        switch (normalizedRotation)
        {
            case 90:
                (x, y, normalizedWidth, normalizedHeight) =
                    (pageHeight - (y + height), x, height, width);
                break;
            case 180:
                (x, y) = (pageWidth - (x + width), pageHeight - (y + height));
                break;
            case 270:
                (x, y, normalizedWidth, normalizedHeight) =
                    (y, pageWidth - (x + width), height, width);
                break;
        }
        var displayedWidth = normalizedRotation is 90 or 270 ? pageHeight : pageWidth;
        var displayedHeight = normalizedRotation is 90 or 270 ? pageWidth : pageHeight;
        return new AIImportBoundingBox
        {
            X = x,
            Y = y,
            Width = normalizedWidth,
            Height = normalizedHeight,
            PageWidth = displayedWidth,
            PageHeight = displayedHeight,
            Rotation = normalizedRotation,
            Unit = "POINT",
            Polygon = [x, y, x + normalizedWidth, y, x + normalizedWidth, y + normalizedHeight, x, y + normalizedHeight]
        };
    }

    private static int NormalizeRotation(int rotation)
    {
        var normalized = ((rotation % 360) + 360) % 360;
        return normalized switch
        {
            < 45 or >= 315 => 0,
            < 135 => 90,
            < 225 => 180,
            _ => 270
        };
    }

    private static string NormalizeText(string value) => value
        .Replace('\u00A0', ' ')
        .Replace("\u200B", string.Empty, StringComparison.Ordinal)
        .Replace("\u200C", string.Empty, StringComparison.Ordinal)
        .Replace("\u200D", string.Empty, StringComparison.Ordinal)
        .Replace("\uFEFF", string.Empty, StringComparison.Ordinal)
        .Replace("ﬁ", "fi", StringComparison.Ordinal)
        .Replace("ﬂ", "fl", StringComparison.Ordinal)
        .Normalize(NormalizationForm.FormKC)
        .Trim();

    private static string ExtractionMode(IReadOnlyCollection<PdfLine> evidence)
    {
        var hasOcr = evidence.Any(line => line.SourceKind == AIImportSourceKinds.Ocr);
        var hasText = evidence.Any(line => line.SourceKind == AIImportSourceKinds.TextLayer);
        return hasOcr && hasText
            ? AIImportExtractionModes.PdfMixedTextOcr
            : hasOcr ? AIImportExtractionModes.PdfOcrDeterministic : AIImportExtractionModes.PdfTextDeterministic;
    }

    private static decimal? AverageOcrConfidence(IReadOnlyCollection<PdfLine> lines)
    {
        var values = lines.Where(line => line.OcrConfidence.HasValue)
            .Select(line => line.OcrConfidence!.Value).ToList();
        return values.Count == 0 ? null : values.Average();
    }

    private Dictionary<string, AIImportFieldEvidence> BuildFieldEvidence(
        IReadOnlyDictionary<string, string?> mapping,
        IReadOnlyDictionary<string, string?> raw,
        IReadOnlyList<PdfLine> lines,
        AIImportEntityType entityType)
    {
        var evidence = new Dictionary<string, AIImportFieldEvidence>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in mapping.Where(pair => !string.IsNullOrWhiteSpace(pair.Value)))
        {
            var sourceHeader = pair.Value!;
            var line = lines.FirstOrDefault(candidate =>
                TryParseKeyValue(candidate, entityType, out var parsedKey, out _)
                && string.Equals(AIImportSchemaRegistry.Key(parsedKey),
                    AIImportSchemaRegistry.Key(sourceHeader), StringComparison.Ordinal));
            if (line == null) continue;
            evidence[pair.Key] = FieldEvidence(line, raw.GetValueOrDefault(sourceHeader));
        }
        return evidence;
    }

    private static Dictionary<string, AIImportFieldEvidence> BuildTableFieldEvidence(
        IReadOnlyDictionary<string, string?> mapping,
        IReadOnlyList<(int Index, string Key, string Label)> columns,
        IReadOnlyDictionary<string, string?> raw,
        PdfLine line,
        IReadOnlyList<PdfCell?> alignedCells)
    {
        var evidence = new Dictionary<string, AIImportFieldEvidence>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in mapping.Where(pair => !string.IsNullOrWhiteSpace(pair.Value)))
        {
            var column = columns.FirstOrDefault(column =>
                string.Equals(column.Key, pair.Value, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(column.Key)) continue;
            var cell = column.Index >= 0 && column.Index < alignedCells.Count ? alignedCells[column.Index] : null;
            evidence[pair.Key] = FieldEvidence(line, raw.GetValueOrDefault(column.Key),
                cell?.OcrConfidence, cell?.RawText);
        }
        return evidence;
    }

    private static AIImportFieldEvidence FieldEvidence(
        PdfLine line,
        string? normalizedValue,
        decimal? ocrConfidence = null,
        string? rawText = null) => new()
    {
        SourceKind = line.SourceKind,
        Locator = Locator(line),
        RawText = rawText ?? line.RawText ?? line.Text,
        NormalizedValue = normalizedValue,
        OcrConfidence = ocrConfidence ?? line.OcrConfidence
    };

    private void AddOcrReviewIssues(AIImportSourceCandidate candidate, AIImportEntityType entityType, decimal reviewThreshold)
    {
        if (entityType == AIImportEntityType.Unknown) return;
        var criticalFields = _schemas.Get(entityType).RequiredFields;
        foreach (var field in criticalFields)
        {
            if (!candidate.FieldEvidence.TryGetValue(field, out var evidence)
                || evidence.SourceKind != AIImportSourceKinds.Ocr
                || evidence.OcrConfidence is null
                || evidence.OcrConfidence >= reviewThreshold) continue;
            candidate.Issues.Add(AIImportValidationContract.Issue(
                "OCR_CONFIDENCE_THẤP",
                $"Trường bắt buộc '{field}' có độ tin cậy OCR {evidence.OcrConfidence:P0}; cần xác nhận thủ công.",
                AIImportIssueSeverities.Review,
                field,
                Position(evidence.Locator),
                AIImportIssueResolutions.ManualReview));
        }
    }

    private static void AddColumnIssues(
        AIImportSourceCandidate candidate,
        IEnumerable<AIImportSourceColumn> columns,
        IReadOnlyDictionary<string, string?> raw)
    {
        foreach (var column in columns.Where(column => !string.IsNullOrWhiteSpace(raw.GetValueOrDefault(column.Key))))
        {
            if (column.Classification == AIImportColumnClassifications.Forbidden)
                candidate.Issues.Add(AIImportValidationContract.Issue("CỘT_CẤM",
                    $"Cột '{column.Label}' không được phép dùng trong AI Smart Import.",
                    AIImportIssueSeverities.Error, resolution: AIImportIssueResolutions.ReuploadOrSkip));
            else if (column.Classification == AIImportColumnClassifications.Unknown)
                candidate.Issues.Add(AIImportValidationContract.Issue("CỘT_KHÔNG_XÁC_ĐỊNH",
                    $"Cột '{column.Label}' không thuộc ImportSchema và sẽ bị bỏ qua.",
                    AIImportIssueSeverities.Warning, resolution: AIImportIssueResolutions.Acknowledge));
        }
    }

    private static AIImportPositionDto Position(AIImportSourceLocator locator) => new()
    {
        SourceFormat = locator.SourceFormat,
        Page = locator.Page,
        Block = locator.Block,
        TextStart = locator.TextStart,
        TextEnd = locator.TextEnd,
        BoundingBox = locator.BoundingBox == null ? null : new AIImportBoundingBoxDto
        {
            X = locator.BoundingBox.X,
            Y = locator.BoundingBox.Y,
            Width = locator.BoundingBox.Width,
            Height = locator.BoundingBox.Height,
            PageWidth = locator.BoundingBox.PageWidth,
            PageHeight = locator.BoundingBox.PageHeight,
            Rotation = locator.BoundingBox.Rotation,
            Unit = locator.BoundingBox.Unit,
            Polygon = locator.BoundingBox.Polygon
        }
    };

    private static AIImportErrorDto Error(string code, string message) => new() { Code = code, Message = message };
    private sealed record PdfLine(
        int Page,
        int Block,
        string Text,
        AIImportBoundingBox Box,
        List<PdfCell> Cells,
        string SourceKind,
        decimal? OcrConfidence = null,
        string? RawText = null,
        int TextStart = 0,
        int TextEnd = 0);
    private sealed record PdfCell(
        string Text,
        decimal? OcrConfidence = null,
        string? RawText = null,
        double X = 0,
        double Width = 0);
    private sealed record PdfToken(
        string Text,
        AIImportBoundingBox Box,
        double RowCoordinate,
        double OrderCoordinate);
    private sealed record KeyValueTrack(int Key, List<PdfLine> Lines)
    {
        public List<PdfLine>.Enumerator GetEnumerator() => Lines.GetEnumerator();
    }

    [GeneratedRegex(@"^\s*([^:\t]{2,100})\s*[:\t]\s*(.+?)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyValuePattern();

    [GeneratedRegex(@"/Subtype\s*/Image", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ImagePattern();
}
