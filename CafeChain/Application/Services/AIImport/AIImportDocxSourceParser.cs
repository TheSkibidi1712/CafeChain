using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using CafeChain.Application.DTOs.AIImport;
using CafeChain.Application.Options;
using CafeChain.Models.AIImport;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Options;

namespace CafeChain.Application.Services.AIImport;

public sealed partial class AIImportDocxSourceParser : IAIImportSourceParser
{
    private readonly AIImportOptions _options;
    private readonly IAIImportSchemaRegistry _schemas;

    public AIImportDocxSourceParser(IOptions<AIImportOptions> options)
        : this(options, new AIImportSchemaRegistry())
    {
    }

    public AIImportDocxSourceParser(IOptions<AIImportOptions> options, IAIImportSchemaRegistry schemas)
    {
        _options = options.Value;
        _schemas = schemas;
    }

    public string SourceFormat => AIImportSourceFormats.Docx;

    public Task<AIImportSourceDocument> ParseAsync(
        AIImportSourceFile source,
        AIImportEntityType? entityHint,
        CancellationToken cancellationToken)
    {
        var result = new AIImportSourceDocument { SourceFormat = SourceFormat };
        if (!ValidatePackage(source.Content, result)) return Task.FromResult(result);

        try
        {
            using var stream = new MemoryStream(source.Content, writable: false);
            using var document = WordprocessingDocument.Open(stream, false);
            var body = document.MainDocumentPart?.Document?.Body;
            if (body == null)
            {
                result.Errors.Add(Error("DOCX_KHÔNG_CÓ_DỮ_LIỆU", "Tệp DOCX không có nội dung đọc được."));
                return Task.FromResult(result);
            }
            if (body.Descendants<FieldCode>().Any()
                || body.Descendants<SimpleField>().Any()
                || body.Descendants<AltChunk>().Any())
            {
                result.Errors.Add(Error(
                    "NỘI_DUNG_CHỦ_ĐỘNG_KHÔNG_ĐƯỢC_HỖ_TRỢ",
                    "DOCX chứa field command hoặc nội dung liên kết động không được hỗ trợ."));
                return Task.FromResult(result);
            }
            var hasTrackedChanges = body.Descendants<InsertedRun>().Any()
                                    || body.Descendants<DeletedRun>().Any();
            if (hasTrackedChanges)
            {
                result.Warnings.Add(new AIImportErrorDto
                {
                    Code = "DOCX_TRACK_CHANGE_CẦN_XEM_LẠI",
                    Message = "DOCX có Track Changes chưa được chấp nhận; mọi bản ghi trích xuất phải được xem lại."
                });
            }

            var paragraphs = body.Descendants<Paragraph>().ToList();
            var tables = body.Descendants<Table>().ToList();
            if (paragraphs.Count > _options.DocxMaxParagraphs || tables.Count > _options.DocxMaxTables)
            {
                result.Errors.Add(Error("DOCX_VƯỢT_GIỚI_HẠN", "Cấu trúc DOCX vượt giới hạn đã cấu hình."));
                return Task.FromResult(result);
            }

            var extractedCharacters = paragraphs.Sum(paragraph => paragraph.InnerText.Length);
            if (extractedCharacters > _options.DocumentMaxExtractedCharacters)
            {
                result.Errors.Add(Error("DOCX_VƯỢT_GIỚI_HẠN", "Nội dung DOCX vượt giới hạn ký tự trích xuất."));
                return Task.FromResult(result);
            }

            result.ExtractedText = string.Join(Environment.NewLine,
                body.Descendants<Paragraph>().Select(paragraph => paragraph.InnerText.Trim()).Where(text => text.Length > 0));

            ExtractTables(tables, entityHint, result, hasTrackedChanges, cancellationToken);
            ExtractKeyValueRecords(body, entityHint, result, hasTrackedChanges, cancellationToken);
            result.Metadata["paragraphCount"] = paragraphs.Count;
            result.Metadata["tableCount"] = tables.Count;
            result.Metadata["extractedCharacters"] = extractedCharacters;

            if (result.Groups.SelectMany(group => group.Candidates).Any()) return Task.FromResult(result);
            result.Errors.Add(Error("DOCX_CẤU_TRÚC_KHÔNG_RÕ", "Không xác định được bản ghi nghiệp vụ trong tệp DOCX."));
        }
        catch (OpenXmlPackageException)
        {
            result.Errors.Add(Error("DOCX_BỊ_HỎNG", "Tệp DOCX bị hỏng hoặc không phải OpenXML hợp lệ."));
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or XmlException)
        {
            result.Errors.Add(Error("DOCX_BỊ_HỎNG", "Tệp DOCX bị hỏng hoặc không thể đọc an toàn."));
        }

        return Task.FromResult(result);
    }

    private bool ValidatePackage(byte[] content, AIImportSourceDocument result)
    {
        if (content.Length < 4)
        {
            result.Errors.Add(Error("DOCX_BỊ_HỎNG", "Tệp DOCX không hợp lệ."));
            return false;
        }
        if (content.AsSpan(0, 4).SequenceEqual(new byte[] { 0xD0, 0xCF, 0x11, 0xE0 }))
        {
            result.Errors.Add(Error("DOCX_CÓ_MẬT_KHẨU", "DOCX được mã hóa hoặc có mật khẩu không được hỗ trợ."));
            return false;
        }
        if (!content.AsSpan(0, 2).SequenceEqual(new byte[] { 0x50, 0x4B }))
        {
            result.Errors.Add(Error("DOCX_BỊ_HỎNG", "Phần mở rộng DOCX không khớp nội dung OpenXML."));
            return false;
        }

        try
        {
            using var stream = new MemoryStream(content, writable: false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            long expanded = 0;
            foreach (var entry in archive.Entries)
            {
                expanded += entry.Length;
                var ratio = entry.CompressedLength == 0 ? entry.Length : entry.Length / (decimal)entry.CompressedLength;
                if (expanded > _options.MaxExpandedBytes || ratio > _options.MaxCompressionRatio)
                {
                    result.Errors.Add(Error("DOCX_VƯỢT_GIỚI_HẠN", "DOCX có kích thước giải nén hoặc tỷ lệ nén không an toàn."));
                    return false;
                }

                var name = entry.FullName.Replace('\\', '/');
                if (name.Contains("vbaProject", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("/embeddings/", StringComparison.OrdinalIgnoreCase)
                    || name.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
                {
                    result.Errors.Add(Error("NỘI_DUNG_CHỦ_ĐỘNG_KHÔNG_ĐƯỢC_HỖ_TRỢ", "DOCX chứa macro, OLE hoặc tệp nhúng không được hỗ trợ."));
                    return false;
                }

                if (!name.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)) continue;
                using var reader = new StreamReader(entry.Open(), Encoding.UTF8, true, leaveOpen: false);
                var relationships = reader.ReadToEnd();
                if (relationships.Contains("TargetMode=\"External\"", StringComparison.OrdinalIgnoreCase))
                {
                    result.Errors.Add(Error("NỘI_DUNG_CHỦ_ĐỘNG_KHÔNG_ĐƯỢC_HỖ_TRỢ", "DOCX chứa liên kết hoặc tài nguyên ngoài không được hỗ trợ."));
                    return false;
                }
            }

            if (archive.GetEntry("[Content_Types].xml") == null || archive.GetEntry("word/document.xml") == null)
            {
                result.Errors.Add(Error("DOCX_BỊ_HỎNG", "Gói DOCX thiếu thành phần OpenXML bắt buộc."));
                return false;
            }
        }
        catch (InvalidDataException)
        {
            result.Errors.Add(Error("DOCX_BỊ_HỎNG", "Tệp DOCX bị hỏng hoặc không thể giải nén."));
            return false;
        }
        return true;
    }

    private void ExtractTables(
        IReadOnlyList<Table> tables,
        AIImportEntityType? entityHint,
        AIImportSourceDocument result,
        bool hasTrackedChanges,
        CancellationToken cancellationToken)
    {
        var totalRows = 0;
        var totalCells = 0;
        for (var tableIndex = 0; tableIndex < tables.Count; tableIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sectionNumber = SectionNumber(tables[tableIndex]);
            var rows = tables[tableIndex].Elements<TableRow>().ToList();
            var hasMergedCells = tables[tableIndex].Descendants<GridSpan>()
                                     .Any(span => span.Val?.Value is > 1)
                                 || tables[tableIndex].Descendants<VerticalMerge>().Any();
            totalRows += rows.Count;
            totalCells += rows.Sum(row => row.Elements<TableCell>().Count());
            if (totalRows > _options.DocxMaxTableRows || totalCells > _options.DocxMaxCells)
            {
                result.Errors.Add(Error("DOCX_VƯỢT_GIỚI_HẠN", "Bảng DOCX vượt giới hạn dòng hoặc ô."));
                return;
            }
            if (rows.Count < 2) continue;

            var headers = rows[0].Elements<TableCell>().Select(CellText).ToList();
            if (headers.All(string.IsNullOrWhiteSpace)) continue;
            var detected = _schemas.Detect(headers, $"Bảng {tableIndex + 1}", entityHint);
            var headerColumns = AIImportSourceColumnBuilder.Build(headers);
            var mapping = AIImportSourceColumnBuilder.RebindMapping(detected.Mapping, headerColumns);
            var confidence = hasMergedCells || hasTrackedChanges
                ? Math.Min(detected.Confidence, Math.Max(0m, _options.ReviewConfidenceThreshold - 0.01m))
                : detected.Confidence;
            if (hasMergedCells)
            {
                result.Warnings.Add(new AIImportErrorDto
                {
                    Code = "DOCX_Ô_GỘP_CẦN_XEM_LẠI",
                    Message = "Bảng DOCX có ô gộp; cần kiểm tra lại ánh xạ và dữ liệu trước khi nhập.",
                    Position = new AIImportPositionDto { SourceFormat = SourceFormat, Section = sectionNumber, Table = tableIndex + 1 }
                });
            }
            var group = new AIImportSourceGroup
            {
                SourceLabel = $"Bảng {tableIndex + 1}",
                SourceLocator = new AIImportSourceLocator { SourceFormat = SourceFormat, Section = sectionNumber, Table = tableIndex + 1, TableRow = 1 },
                ExtractionMode = AIImportExtractionModes.DocxTableDeterministic,
                HeaderOrdinal = 1,
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
                            SourceFormat = SourceFormat, Section = sectionNumber, Table = tableIndex + 1,
                            TableRow = 1, TableColumn = column.Index + 1
                        }
                    }), mapping),
                Confidence = confidence
            };
            if (hasMergedCells)
                group.Issues.Add(ReviewIssue("DOCX_Ô_GỘP_CẦN_XEM_LẠI",
                    "Bảng DOCX có ô gộp; cần đối chiếu từng trường với nguồn.", group.SourceLocator));
            if (hasTrackedChanges)
                group.Issues.Add(ReviewIssue("DOCX_TRACK_CHANGE_CẦN_XEM_LẠI",
                    "DOCX có Track Changes chưa được chấp nhận; cần đối chiếu từng trường với nguồn.", group.SourceLocator));
            for (var rowIndex = 1; rowIndex < rows.Count; rowIndex++)
            {
                var values = rows[rowIndex].Elements<TableCell>().Select(CellText).ToList();
                var raw = headerColumns.ToDictionary(column => column.Key,
                    column => values.ElementAtOrDefault(column.Index), StringComparer.OrdinalIgnoreCase);
                if (raw.Values.All(string.IsNullOrWhiteSpace)) continue;
                var locator = new AIImportSourceLocator { SourceFormat = SourceFormat, Section = sectionNumber, Table = tableIndex + 1, TableRow = rowIndex + 1 };
                var trace = headerColumns.Select(column => new
                    {
                        Header = column.Key,
                        Locator = new AIImportSourceLocator
                        {
                            SourceFormat = SourceFormat,
                            Section = sectionNumber,
                            Table = tableIndex + 1,
                            TableRow = rowIndex + 1,
                            TableColumn = column.Index + 1
                        }
                    })
                    .Where(value => value.Header.Length > 0)
                    .ToDictionary(value => value.Header, value => (string?)JsonSerializer.Serialize(value.Locator), StringComparer.OrdinalIgnoreCase);
                var candidate = Candidate(rowIndex + 1, raw, mapping, locator,
                    AIImportExtractionModes.DocxTableDeterministic, confidence, sourceTrace: trace);
                candidate.Issues.AddRange(group.Issues);
                AddColumnIssues(candidate, group.SourceColumns, raw);
                group.Candidates.Add(candidate);
            }
            if (group.Candidates.Count > 0) result.Groups.Add(group);
        }
    }

    private void ExtractKeyValueRecords(
        Body body,
        AIImportEntityType? entityHint,
        AIImportSourceDocument result,
        bool hasTrackedChanges,
        CancellationToken cancellationToken)
    {
        var record = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var evidence = new List<string>();
        var sourceTrace = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var firstParagraph = 0;
        var firstSection = 0;
        var sourceLabel = "Nội dung DOCX";
        var ordinal = 0;

        void Flush()
        {
            if (record.Count < 2) { record.Clear(); evidence.Clear(); sourceTrace.Clear(); firstParagraph = 0; firstSection = 0; return; }
            var detected = _schemas.Detect(record.Keys, sourceLabel, entityHint);
            var confidence = hasTrackedChanges
                ? Math.Min(detected.Confidence, Math.Max(0m, _options.ReviewConfidenceThreshold - 0.01m))
                : detected.Confidence;
            var locator = new AIImportSourceLocator { SourceFormat = SourceFormat, Section = firstSection, Paragraph = firstParagraph };
            var group = new AIImportSourceGroup
            {
                SourceLabel = sourceLabel,
                SourceLocator = locator,
                ExtractionMode = AIImportExtractionModes.DocxTextDeterministic,
                HeaderOrdinal = firstParagraph,
                EntityType = detected.EntityType,
                Mapping = detected.Mapping,
                SourceHeaders = record.Keys.ToList(),
                Confidence = confidence
            };
            if (hasTrackedChanges)
                group.Issues.Add(ReviewIssue("DOCX_TRACK_CHANGE_CẦN_XEM_LẠI",
                    "DOCX có Track Changes chưa được chấp nhận; cần đối chiếu từng trường với nguồn.", locator));
            var candidate = Candidate(++ordinal, new Dictionary<string, string?>(record, StringComparer.OrdinalIgnoreCase), detected.Mapping, locator,
                AIImportExtractionModes.DocxTextDeterministic, confidence, string.Join(Environment.NewLine, evidence),
                new Dictionary<string, string?>(sourceTrace, StringComparer.OrdinalIgnoreCase));
            candidate.Issues.AddRange(group.Issues);
            group.Candidates.Add(candidate);
            result.Groups.Add(group);
            record.Clear(); evidence.Clear(); sourceTrace.Clear(); firstParagraph = 0; firstSection = 0;
        }

        var paragraphIndex = 0;
        foreach (var element in body.ChildElements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (element is Table) { Flush(); continue; }
            if (element is not Paragraph paragraph) continue;
            paragraphIndex++;
            var sectionNumber = SectionNumber(paragraph);
            var text = paragraph.InnerText.Trim();
            if (string.IsNullOrWhiteSpace(text)) { Flush(); continue; }
            var match = KeyValuePattern().Match(text);
            if (!match.Success)
            {
                if (record.Count == 1)
                    result.Errors.Add(Error("KHÔNG_XÁC_ĐỊNH_RANH_GIỚI_BẢN_GHI",
                        $"Không xác định được ranh giới bản ghi gần paragraph {paragraphIndex}."));
                Flush();
                sourceLabel = text.Length <= 150 ? text : "Nội dung DOCX";
                continue;
            }
            var key = match.Groups[1].Value.Trim();
            if (record.ContainsKey(key)) Flush();
            if (firstParagraph == 0)
            {
                firstParagraph = paragraphIndex;
                firstSection = sectionNumber;
            }
            record[key] = match.Groups[2].Value.Trim();
            sourceTrace[key] = JsonSerializer.Serialize(new AIImportSourceLocator
            {
                SourceFormat = SourceFormat,
                Section = sectionNumber,
                Paragraph = paragraphIndex
            });
            evidence.Add(text);
        }
        Flush();
    }

    private static AIImportSourceCandidate Candidate(
        int sortOrder,
        Dictionary<string, string?> raw,
        IReadOnlyDictionary<string, string?> mapping,
        AIImportSourceLocator locator,
        string mode,
        decimal confidence,
        string? evidence = null,
        Dictionary<string, string?>? sourceTrace = null)
    {
        var trace = sourceTrace ?? raw.Keys.ToDictionary(key => key, _ => (string?)JsonSerializer.Serialize(locator), StringComparer.OrdinalIgnoreCase);
        return new AIImportSourceCandidate
        {
            SortOrder = sortOrder,
            RawData = raw,
            MappedData = mapping.ToDictionary(pair => pair.Key,
                pair => string.IsNullOrWhiteSpace(pair.Value) ? null : raw.GetValueOrDefault(pair.Value), StringComparer.OrdinalIgnoreCase),
            SourceTrace = trace,
            SourceLocator = locator,
            EvidenceSnippet = evidence ?? string.Join(" | ", raw.Select(pair => $"{pair.Key}: {pair.Value}")),
            Confidence = confidence
        };
    }

    private static AIImportErrorDto ReviewIssue(string code, string message, AIImportSourceLocator locator) =>
        AIImportValidationContract.Issue(code, message, AIImportIssueSeverities.Review,
            locator: new AIImportPositionDto
            {
                SourceFormat = locator.SourceFormat, Section = locator.Section, Paragraph = locator.Paragraph,
                Table = locator.Table, TableRow = locator.TableRow, TableColumn = locator.TableColumn
            }, resolution: AIImportIssueResolutions.ManualReview);

    private static void AddColumnIssues(
        AIImportSourceCandidate candidate,
        IEnumerable<AIImportSourceColumn> columns,
        IReadOnlyDictionary<string, string?> raw)
    {
        foreach (var column in columns.Where(column => !string.IsNullOrWhiteSpace(raw.GetValueOrDefault(column.Key))))
        {
            if (column.Classification == AIImportColumnClassifications.Forbidden)
                candidate.Issues.Add(AIImportValidationContract.Issue("CỘT_CẤM",
                    $"Cột '{column.Label}' không được phép dùng trong AI Smart Import.", AIImportIssueSeverities.Error,
                    resolution: AIImportIssueResolutions.ReuploadOrSkip));
            else if (column.Classification == AIImportColumnClassifications.Unknown)
                candidate.Issues.Add(AIImportValidationContract.Issue("CỘT_KHÔNG_XÁC_ĐỊNH",
                    $"Cột '{column.Label}' không thuộc ImportSchema và sẽ bị bỏ qua.", AIImportIssueSeverities.Warning,
                    resolution: AIImportIssueResolutions.Acknowledge));
        }
    }

    private static string CellText(TableCell cell) => string.Join(" ", cell.Descendants<Text>().Select(text => text.Text)).Trim();

    private static int SectionNumber(OpenXmlElement element)
    {
        var body = element.Ancestors<Body>().FirstOrDefault();
        if (body == null) return 1;
        var directChild = element;
        while (directChild.Parent != null && directChild.Parent != body) directChild = directChild.Parent;

        var section = 1;
        foreach (var child in body.ChildElements)
        {
            if (ReferenceEquals(child, directChild)) break;
            if (child is Paragraph paragraph
                && paragraph.ParagraphProperties?.GetFirstChild<SectionProperties>() != null)
                section++;
        }
        return section;
    }

    private static AIImportErrorDto Error(string code, string message) => new() { Code = code, Message = message };

    [GeneratedRegex(@"^\s*([^:\t]{2,100})\s*[:\t]\s*(.+?)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyValuePattern();
}
