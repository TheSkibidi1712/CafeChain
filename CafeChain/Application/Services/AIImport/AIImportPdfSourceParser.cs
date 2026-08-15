using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CafeChain.Application.DTOs.AIImport;
using CafeChain.Application.Options;
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

    public AIImportPdfSourceParser(IOptions<AIImportOptions> options)
        : this(options, new AIImportSchemaRegistry())
    {
    }

    public AIImportPdfSourceParser(IOptions<AIImportOptions> options, IAIImportSchemaRegistry schemas)
    {
        _options = options.Value;
        _schemas = schemas;
    }

    public string SourceFormat => AIImportSourceFormats.Pdf;

    public Task<AIImportSourceDocument> ParseAsync(
        AIImportSourceFile source,
        AIImportEntityType? entityHint,
        CancellationToken cancellationToken)
    {
        var result = new AIImportSourceDocument { SourceFormat = SourceFormat };
        if (!ValidateBytes(source.Content, result)) return Task.FromResult(result);

        try
        {
            using var document = PdfDocument.Open(source.Content);
            if (document.NumberOfPages > _options.PdfMaxPages)
            {
                result.Errors.Add(Error("PDF_VƯỢT_GIỚI_HẠN", $"PDF vượt giới hạn {_options.PdfMaxPages} trang."));
                return Task.FromResult(result);
            }

            var allLines = new List<PdfLine>();
            var pagesWithoutText = new List<int>();
            var pagesWithSignificantImages = new List<int>();
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
                    return Task.FromResult(result);
                }
                var pageArea = page.Width * page.Height;
                if (pageArea > 0 && images.Any(image =>
                        (decimal)(image.Bounds.Area / pageArea) >= _options.PdfOcrImageAreaRatioThreshold))
                    pagesWithSignificantImages.Add(page.Number);
                var words = NearestNeighbourWordExtractor.Instance.GetWords(page.Letters).ToList();
                if (words.Count == 0) pagesWithoutText.Add(page.Number);
                var lines = BuildLines(page.Number, words);
                if (HasAmbiguousReadingOrder(lines, entityHint))
                {
                    result.Errors.Add(AIImportValidationContract.Issue(
                        "THỨ_TỰ_ĐỌC_PDF_KHÔNG_RÕ",
                        $"Không xác định được thứ tự đọc an toàn trên trang {page.Number}.",
                        AIImportIssueSeverities.Error,
                        locator: new AIImportPositionDto { SourceFormat = SourceFormat, Page = page.Number },
                        resolution: AIImportIssueResolutions.ReuploadOrSkip));
                    return Task.FromResult(result);
                }
                blockCount += lines.Count;
                extractedCharacters += lines.Sum(line => line.Text.Length);
                if (blockCount > _options.PdfMaxTextBlocks || extractedCharacters > _options.DocumentMaxExtractedCharacters)
                {
                    result.Errors.Add(Error("PDF_VƯỢT_GIỚI_HẠN", "PDF vượt giới hạn block hoặc ký tự trích xuất."));
                    return Task.FromResult(result);
                }
                allLines.AddRange(lines);
            }

            result.Metadata["imageCount"] = imageCount;
            if (pagesWithSignificantImages.Count > 0)
            {
                result.Errors.Add(new AIImportErrorDto
                {
                    Code = "PDF_CẦN_OCR",
                    Message = $"PDF có vùng ảnh đáng kể không thể trích xuất text an toàn (trang {string.Join(", ", pagesWithSignificantImages.Take(10))}); hệ thống hiện chưa hỗ trợ OCR.",
                    Position = new AIImportPositionDto { SourceFormat = SourceFormat, Page = pagesWithSignificantImages[0] }
                });
                return Task.FromResult(result);
            }
            if (pagesWithoutText.Count > 0 && imageCount > 0)
            {
                result.Errors.Add(new AIImportErrorDto
                {
                    Code = "PDF_CẦN_OCR",
                    Message = $"PDF có trang ảnh không có lớp text ({string.Join(", ", pagesWithoutText.Take(10))}); hệ thống hiện chưa hỗ trợ OCR.",
                    Position = new AIImportPositionDto { SourceFormat = SourceFormat, Page = pagesWithoutText[0] }
                });
                return Task.FromResult(result);
            }

            if (allLines.Count == 0 || allLines.Sum(line => line.Text.Count(char.IsLetterOrDigit)) < 2)
            {
                result.Errors.Add(Error("PDF_CẦN_OCR", "PDF không có lớp text đủ tin cậy; hệ thống hiện chưa hỗ trợ OCR."));
                return Task.FromResult(result);
            }

            var contentLines = AddTextOffsets(RemoveRepeatedDecorations(allLines));
            result.ExtractedText = string.Join(Environment.NewLine, contentLines.Select(line => $"[[PAGE:{line.Page} BLOCK:{line.Block}]] {line.Text}"));
            var tableLines = ExtractTables(contentLines, entityHint, result);
            ExtractKeyValueRecords(contentLines.Where(line => !tableLines.Contains((line.Page, line.Block))).ToList(), entityHint, result);
            result.Metadata["pageCount"] = document.NumberOfPages;
            result.Metadata["textBlockCount"] = blockCount;
            result.Metadata["extractedCharacters"] = extractedCharacters;
            result.Metadata["ocrUsed"] = false;

            if (result.Groups.SelectMany(group => group.Candidates).Any()) return Task.FromResult(result);
            result.Errors.Add(Error("BỐ_CỤC_PDF_KHÔNG_RÕ", "Không xác định được bản ghi nghiệp vụ từ bố cục PDF."));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var encrypted = Encoding.Latin1.GetString(source.Content).Contains("/Encrypt", StringComparison.Ordinal);
            result.Errors.Add(encrypted
                ? Error("PDF_CÓ_MẬT_KHẨU", "PDF được mã hóa hoặc có mật khẩu không được hỗ trợ.")
                : Error("PDF_BỊ_HỎNG", "PDF bị hỏng hoặc không thể đọc an toàn."));
        }

        return Task.FromResult(result);
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
        AIImportSourceDocument result)
    {
        var records = lines.GroupBy(line => line.Page);
        var ordinal = 0;
        foreach (var page in records)
        {
            var raw = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var evidence = new List<PdfLine>();
            foreach (var line in page)
            {
                var match = KeyValuePattern().Match(line.Text);
                if (!match.Success) continue;
                var key = match.Groups[1].Value.Trim();
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
                raw[key] = match.Groups[2].Value.Trim();
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
                ExtractionMode = AIImportExtractionModes.PdfTextDeterministic,
                HeaderOrdinal = first.Block,
                EntityType = detected.EntityType,
                Mapping = detected.Mapping,
                SourceHeaders = raw.Keys.ToList(),
                Confidence = detected.Confidence
            };
            group.Candidates.Add(new AIImportSourceCandidate
            {
                SortOrder = ++ordinal,
                RawData = raw,
                MappedData = detected.Mapping.ToDictionary(pair => pair.Key,
                    pair => string.IsNullOrWhiteSpace(pair.Value) ? null : raw.GetValueOrDefault(pair.Value), StringComparer.OrdinalIgnoreCase),
                SourceTrace = raw.Keys.ToDictionary(key => key, _ => JsonSerializer.Serialize(locator), StringComparer.OrdinalIgnoreCase),
                SourceLocator = locator,
                EvidenceSnippet = string.Join(Environment.NewLine, evidence.Select(line => line.Text)),
                Confidence = detected.Confidence
            });
            result.Groups.Add(group);
        }
    }

    private HashSet<(int Page, int Block)> ExtractTables(
        IReadOnlyList<PdfLine> lines,
        AIImportEntityType? entityHint,
        AIImportSourceDocument result)
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
                    ExtractionMode = AIImportExtractionModes.PdfTextDeterministic,
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
                    Confidence = detected.Confidence
                };

                for (var rowIndex = headerIndex + 1; rowIndex < ordered.Count; rowIndex++)
                {
                    var row = ordered[rowIndex];
                    if (row.Cells.Count != headers.Count) break;
                    var raw = headerColumns.ToDictionary(column => column.Key,
                        column => (string?)row.Cells[column.Index].Text, StringComparer.OrdinalIgnoreCase);
                    var locator = new AIImportSourceLocator
                    {
                        SourceFormat = SourceFormat,
                        Page = pageLines.Key,
                        Block = row.Block,
                        TextStart = row.TextStart,
                        TextEnd = row.TextEnd,
                        BoundingBox = row.Box
                    };
                    group.Candidates.Add(new AIImportSourceCandidate
                    {
                        SortOrder = row.Block,
                        RawData = raw,
                        MappedData = mapping.ToDictionary(pair => pair.Key,
                            pair => string.IsNullOrWhiteSpace(pair.Value) ? null : raw.GetValueOrDefault(pair.Value), StringComparer.OrdinalIgnoreCase),
                        SourceTrace = raw.Keys.ToDictionary(key => key, _ => JsonSerializer.Serialize(locator), StringComparer.OrdinalIgnoreCase),
                        SourceLocator = locator,
                        EvidenceSnippet = row.Text,
                        Confidence = detected.Confidence
                    });
                    consumed.Add((row.Page, row.Block));
                }

                if (group.Candidates.Count == 0) continue;
                consumed.Add((headerLine.Page, headerLine.Block));
                result.Groups.Add(group);
                headerIndex += group.Candidates.Count;
            }
        }
        return consumed;
    }

    private static List<PdfLine> BuildLines(int pageNumber, IReadOnlyList<Word> words)
    {
        var rows = new List<List<Word>>();
        foreach (var word in words.OrderByDescending(word => word.BoundingBox.Bottom).ThenBy(word => word.BoundingBox.Left))
        {
            var row = rows.FirstOrDefault(candidate => Math.Abs(candidate[0].BoundingBox.Bottom - word.BoundingBox.Bottom) <= 3d);
            if (row == null) rows.Add([word]); else row.Add(word);
        }

        return rows.OrderByDescending(row => row.Max(word => word.BoundingBox.Top))
            .Select((row, index) =>
            {
                var ordered = row.OrderBy(word => word.BoundingBox.Left).ToList();
                var left = ordered.Min(word => word.BoundingBox.Left);
                var bottom = ordered.Min(word => word.BoundingBox.Bottom);
                var right = ordered.Max(word => word.BoundingBox.Right);
                var top = ordered.Max(word => word.BoundingBox.Top);
                return new PdfLine(pageNumber, index + 1, JoinWords(ordered),
                    new AIImportBoundingBox { X = left, Y = bottom, Width = right - left, Height = top - bottom },
                    BuildCells(ordered));
            })
            .Where(line => !string.IsNullOrWhiteSpace(line.Text)).ToList();
    }

    private bool HasAmbiguousReadingOrder(IReadOnlyList<PdfLine> lines, AIImportEntityType? entityHint)
    {
        var splitLines = lines.Where(line => line.Cells.Count >= 2).ToList();
        if (splitLines.Count < 3) return false;
        var hasRecognizedHeader = splitLines.Any(line =>
            _schemas.Detect(line.Cells.Select(cell => cell.Text), $"Trang {line.Page}", entityHint).EntityType
            != AIImportEntityType.Unknown);
        if (hasRecognizedHeader) return false;
        var tracks = splitLines.Select(line => line.Cells.Count).Distinct().Count();
        return tracks > 1 || splitLines.Count >= Math.Max(3, lines.Count / 2);
    }

    private static List<PdfCell> BuildCells(IReadOnlyList<Word> words)
    {
        var groups = new List<List<Word>>();
        foreach (var word in words)
        {
            if (groups.Count == 0)
            {
                groups.Add([word]);
                continue;
            }
            var previous = groups[^1][^1];
            var averageLetterWidth = previous.Text.Length == 0 ? 4d : previous.BoundingBox.Width / previous.Text.Length;
            var gap = word.BoundingBox.Left - previous.BoundingBox.Right;
            if (gap > Math.Max(18d, averageLetterWidth * 4d)) groups.Add([word]);
            else groups[^1].Add(word);
        }
        return groups.Select(group => new PdfCell(JoinWords(group))).ToList();
    }

    private static string JoinWords(IEnumerable<Word> words) =>
        string.Join(" ", words.Select(word => word.Text.Trim()).Where(text => text.Length > 0));

    private static IReadOnlyList<PdfLine> RemoveRepeatedDecorations(IReadOnlyList<PdfLine> lines)
    {
        var repeated = lines.GroupBy(line => AIImportSchemaRegistry.Key(line.Text))
            .Where(group => group.Select(line => line.Page).Distinct().Count() >= 2
                            && group.All(line => line.Block <= 2 || line.Block >= lines.Count(candidate => candidate.Page == line.Page) - 1))
            .Select(group => group.Key).ToHashSet(StringComparer.Ordinal);
        return lines.Where(line => !repeated.Contains(AIImportSchemaRegistry.Key(line.Text))).ToList();
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

    private static AIImportErrorDto Error(string code, string message) => new() { Code = code, Message = message };
    private sealed record PdfLine(
        int Page,
        int Block,
        string Text,
        AIImportBoundingBox Box,
        List<PdfCell> Cells,
        int TextStart = 0,
        int TextEnd = 0);
    private sealed record PdfCell(string Text);

    [GeneratedRegex(@"^\s*([^:\t]{2,100})\s*[:\t]\s*(.+?)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyValuePattern();

    [GeneratedRegex(@"/Subtype\s*/Image", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ImagePattern();
}
