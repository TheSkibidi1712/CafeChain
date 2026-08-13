using CafeChain.Application.Options;
using CafeChain.Application.Services.AIImport;
using CafeChain.Application.DTOs.AI;
using CafeChain.Application.DTOs.AIImport;
using CafeChain.Application.Interfaces.AI;
using CafeChain.Models.AIImport;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Options;
using Moq;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using OpenXmlDocument = DocumentFormat.OpenXml.Wordprocessing.Document;
using PdfDocumentBuilder = QuestPDF.Fluent.Document;

namespace CafeChain.Tests;

public sealed class AIImportDocumentParserTests
{
    [Theory]
    [InlineData(AIImportSourceFormats.Docx)]
    [InlineData(AIImportSourceFormats.Pdf)]
    public async Task Supplier_document_records_expose_all_required_editor_fields(string sourceFormat)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var lines = new[]
        {
            "Tên nhà cung cấp: Nhà cung cấp kiểm thử",
            "Mã số thuế: 0312345679",
            "Số điện thoại: 0901000002",
            "Người liên hệ: Liên hệ kiểm thử",
            "Email liên hệ: supplier@cafechain.test"
        };
        var content = sourceFormat == AIImportSourceFormats.Pdf
            ? PdfDocumentBuilder.Create(container => container.Page(page =>
                page.Content().Text(string.Join('\n', lines)))).GeneratePdf()
            : CreateDocx(lines);
        IAIImportSourceParser parser = sourceFormat == AIImportSourceFormats.Pdf
            ? new AIImportPdfSourceParser(TestOptions())
            : new AIImportDocxSourceParser(TestOptions());
        var schemas = new AIImportSchemaRegistry();

        var result = await parser.ParseAsync(
            new AIImportSourceFile($"supplier.{sourceFormat.ToLowerInvariant()}", content),
            null,
            default);

        Assert.Empty(result.Errors);
        var group = Assert.Single(result.Groups);
        Assert.Equal(AIImportEntityType.Supplier, group.EntityType);
        var candidate = Assert.Single(group.Candidates);
        var normalized = schemas.Normalize(group.EntityType, candidate.MappedData);
        Assert.Empty(schemas.Validate(group.EntityType, normalized));
        Assert.Equal("0901000002", normalized["PrimaryPhone"]);
        Assert.Equal("Liên hệ kiểm thử", normalized["PrimaryContactName"]);
    }

    [Fact]
    public async Task Docx_key_value_record_is_exposed_with_evidence_and_locator()
    {
        var parser = new AIImportDocxSourceParser(TestOptions());
        var source = new AIImportSourceFile("danh-muc.docx", CreateDocx(
            "DANH MỤC",
            "Mã danh mục: CAT_COFFEE",
            "Tên danh mục: Cà phê",
            "Biểu tượng Unicode: ☕"));

        var result = await parser.ParseAsync(source, AIImportEntityType.Category, default);

        Assert.Empty(result.Errors);
        Assert.Equal(AIImportSourceFormats.Docx, result.SourceFormat);
        var group = Assert.Single(result.Groups);
        var candidate = Assert.Single(group.Candidates);
        Assert.Equal("CAT_COFFEE", candidate.RawData["Mã danh mục"]);
        Assert.Contains("CAT_COFFEE", candidate.EvidenceSnippet, StringComparison.Ordinal);
        Assert.Equal(2, candidate.SourceLocator.Paragraph);
        Assert.Equal(AIImportExtractionModes.DocxTextDeterministic, group.ExtractionMode);
    }

    [Fact]
    public async Task Pdf_with_text_is_extracted_but_pdf_without_text_requires_ocr()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var parser = new AIImportPdfSourceParser(TestOptions());
        var textPdf = PdfDocumentBuilder.Create(container => container.Page(page =>
        {
            page.Content().Text("Mã danh mục: CAT_TEA\nTên danh mục: Trà");
        })).GeneratePdf();
        var imageOnlyPdf = PdfDocumentBuilder.Create(container => container.Page(page =>
        {
            page.Content().Height(40).Background("#222222");
        })).GeneratePdf();

        var textResult = await parser.ParseAsync(new AIImportSourceFile("danh-muc.pdf", textPdf), AIImportEntityType.Category, default);
        var imageResult = await parser.ParseAsync(new AIImportSourceFile("scan.pdf", imageOnlyPdf), null, default);

        Assert.Empty(textResult.Errors);
        Assert.Equal(AIImportSourceFormats.Pdf, textResult.SourceFormat);
        var textCandidate = Assert.Single(textResult.Groups.SelectMany(x => x.Candidates));
        Assert.NotNull(textCandidate.SourceLocator.TextStart);
        Assert.True(textCandidate.SourceLocator.TextEnd > textCandidate.SourceLocator.TextStart);
        Assert.Contains(imageResult.Errors, error => error.Code == "PDF_CẦN_OCR");
    }

    [Fact]
    public async Task Pdf_with_significant_image_and_partial_text_requires_ocr()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
        var pdf = PdfDocumentBuilder.Create(container => container.Page(page =>
        {
            page.Content().Column(column =>
            {
                column.Item().Text("Mã danh mục: CAT_PARTIAL\nTên danh mục: Dữ liệu một phần");
                column.Item().Height(300).Image(png).FitArea();
            });
        })).GeneratePdf();
        var parser = new AIImportPdfSourceParser(TestOptions());

        var result = await parser.ParseAsync(new AIImportSourceFile("mixed.pdf", pdf), AIImportEntityType.Category, default);

        Assert.Contains(result.Errors, error => error.Code == "PDF_CẦN_OCR");
        Assert.Empty(result.Groups);
    }

    [Fact]
    public async Task Pdf_table_is_extracted_deterministically_from_word_coordinates()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var pdf = PdfDocumentBuilder.Create(container => container.Page(page =>
        {
            page.Content().Table(table =>
            {
                table.ColumnsDefinition(columns => { columns.RelativeColumn(); columns.RelativeColumn(); });
                table.Cell().Text("Mã danh mục");
                table.Cell().Text("Tên danh mục");
                table.Cell().Text("CAT_TABLE");
                table.Cell().Text("Cà phê bảng");
            });
        })).GeneratePdf();
        var parser = new AIImportPdfSourceParser(TestOptions());

        var result = await parser.ParseAsync(new AIImportSourceFile("table.pdf", pdf), AIImportEntityType.Category, default);

        Assert.Empty(result.Errors);
        var group = Assert.Single(result.Groups);
        Assert.Equal(AIImportExtractionModes.PdfTextDeterministic, group.ExtractionMode);
        Assert.Equal("CAT_TABLE", Assert.Single(group.Candidates).MappedData["CategoryCode"]);
    }

    [Fact]
    public async Task Docx_with_external_relationship_is_rejected_as_active_content()
    {
        var bytes = CreateDocx("Mã danh mục: CAT01", "Tên danh mục: Cà phê");
        using var stream = new MemoryStream();
        await stream.WriteAsync(bytes);
        stream.Position = 0;
        using (var document = WordprocessingDocument.Open(stream, true))
        {
            document.MainDocumentPart!.AddHyperlinkRelationship(new Uri("https://example.com"), true);
        }

        var parser = new AIImportDocxSourceParser(TestOptions());
        var result = await parser.ParseAsync(new AIImportSourceFile("unsafe.docx", stream.ToArray()), null, default);

        Assert.Contains(result.Errors, error => error.Code == "NỘI_DUNG_CHỦ_ĐỘNG_KHÔNG_ĐƯỢC_HỖ_TRỢ");
        Assert.Empty(result.Groups);
    }

    [Fact]
    public async Task Docx_with_field_command_is_rejected_as_active_content()
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var main = document.AddMainDocumentPart();
            main.Document = new OpenXmlDocument(new Body(
                new Paragraph(
                    new Run(new FieldChar { FieldCharType = FieldCharValues.Begin }),
                    new Run(new FieldCode(" INCLUDETEXT \\\"https://example.com/data.txt\\\" ")),
                    new Run(new FieldChar { FieldCharType = FieldCharValues.End })),
                new Paragraph(new Run(new Text("Mã danh mục: CAT_FIELD"))),
                new Paragraph(new Run(new Text("Tên danh mục: Không an toàn")))));
            main.Document.Save();
        }
        var parser = new AIImportDocxSourceParser(TestOptions());

        var result = await parser.ParseAsync(new AIImportSourceFile("field.docx", stream.ToArray()), null, default);

        Assert.Contains(result.Errors, error => error.Code == "NỘI_DUNG_CHỦ_ĐỘNG_KHÔNG_ĐƯỢC_HỖ_TRỢ");
        Assert.Empty(result.Groups);
    }

    [Fact]
    public async Task Docx_merged_table_is_reviewable_and_preserves_cell_locator()
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var main = document.AddMainDocumentPart();
            main.Document = new OpenXmlDocument(new Body(new Table(
                new TableRow(
                    Cell("Mã danh mục"),
                    Cell("Tên danh mục")),
                new TableRow(
                    Cell("CAT_MERGED", new VerticalMerge { Val = MergedCellValues.Restart }),
                    Cell("Danh mục gộp")))));
            main.Document.Save();
        }
        var options = TestOptions();
        var parser = new AIImportDocxSourceParser(options);

        var result = await parser.ParseAsync(new AIImportSourceFile("merged.docx", stream.ToArray()), AIImportEntityType.Category, default);

        var group = Assert.Single(result.Groups);
        var candidate = Assert.Single(group.Candidates);
        Assert.Contains(result.Warnings, error => error.Code == "DOCX_Ô_GỘP_CẦN_XEM_LẠI");
        Assert.True(group.Confidence < options.Value.ReviewConfidenceThreshold);
        var fieldLocator = System.Text.Json.JsonSerializer.Deserialize<AIImportSourceLocator>(candidate.SourceTrace["Mã danh mục"]!);
        Assert.Equal(1, fieldLocator!.TableColumn);
    }

    [Fact]
    public async Task Docx_with_unaccepted_tracked_changes_is_forced_to_review()
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var main = document.AddMainDocumentPart();
            main.Document = new OpenXmlDocument(new Body(
                new Paragraph(new InsertedRun(new Run(new Text("Mã danh mục: CAT_TRACKED")))),
                new Paragraph(new Run(new Text("Tên danh mục: Chưa chấp nhận revision")))));
            main.Document.Save();
        }
        var options = TestOptions();
        var parser = new AIImportDocxSourceParser(options);

        var result = await parser.ParseAsync(new AIImportSourceFile("tracked.docx", stream.ToArray()), AIImportEntityType.Category, default);

        var group = Assert.Single(result.Groups);
        Assert.Contains(result.Warnings, error => error.Code == "DOCX_TRACK_CHANGE_CẦN_XEM_LẠI");
        Assert.True(group.Confidence < options.Value.ReviewConfidenceThreshold);
        Assert.True(Assert.Single(group.Candidates).Confidence < options.Value.ReviewConfidenceThreshold);
    }

    [Fact]
    public async Task Docx_locator_tracks_sections_for_each_record_and_field()
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var main = document.AddMainDocumentPart();
            main.Document = new OpenXmlDocument(new Body(
                new Paragraph(new Run(new Text("Mã danh mục: CAT_SECTION_1"))),
                new Paragraph(
                    new ParagraphProperties(new SectionProperties()),
                    new Run(new Text("Tên danh mục: Section một"))),
                new Paragraph(new Run(new Text(string.Empty))),
                new Paragraph(new Run(new Text("Mã danh mục: CAT_SECTION_2"))),
                new Paragraph(new Run(new Text("Tên danh mục: Section hai")))));
            main.Document.Save();
        }
        var parser = new AIImportDocxSourceParser(TestOptions());

        var result = await parser.ParseAsync(new AIImportSourceFile("sections.docx", stream.ToArray()), AIImportEntityType.Category, default);

        Assert.Equal(2, result.Groups.Count);
        Assert.Equal(1, result.Groups[0].SourceLocator.Section);
        Assert.Equal(2, result.Groups[1].SourceLocator.Section);
        var second = Assert.Single(result.Groups[1].Candidates);
        var fieldLocator = System.Text.Json.JsonSerializer.Deserialize<AIImportSourceLocator>(second.SourceTrace["Mã danh mục"]!);
        Assert.Equal(2, fieldLocator!.Section);
    }

    [Fact]
    public async Task Pipeline_accepts_ai_candidate_only_when_evidence_is_grounded_in_source()
    {
        var sourceParser = new StubSourceParser(new AIImportSourceDocument
        {
            SourceFormat = AIImportSourceFormats.Docx,
            ExtractedText = "Mã danh mục: CAT_AI\nTên danh mục: Danh mục AI",
            Errors = [new AIImportErrorDto { Code = "DOCX_CẤU_TRÚC_KHÔNG_RÕ", Message = "Cần AI." }]
        });
        var ollama = new Mock<IOllamaClient>();
        ollama.Setup(client => client.ChatStructuredAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), "AIImport.DocumentExtraction", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaResultDTO
            {
                Success = true,
                Content = """{"records":[{"entity":"Category","confidence":0.95,"evidence":"Mã danh mục: CAT_AI\nTên danh mục: Danh mục AI","fields":{"CategoryCode":"CAT_AI","Name":"Danh mục AI"}}]}"""
            });
        var extractor = new AIImportDocumentAiExtractor(ollama.Object, new AIImportSchemaRegistry(), TestOptions());
        var pipeline = new AIImportDocumentPipeline([sourceParser], extractor);

        var result = await pipeline.AnalyzeAsync(new AIImportSourceFile("ai.docx", [1]), null, default);

        Assert.Empty(result.Errors);
        var group = Assert.Single(result.Groups);
        Assert.Equal(AIImportExtractionModes.DocxAiExtraction, group.ExtractionMode);
        Assert.Equal("CAT_AI", Assert.Single(group.Candidates).MappedData["CategoryCode"]);
    }

    [Fact]
    public async Task Pipeline_preserves_grounded_low_confidence_candidate_for_review()
    {
        var sourceParser = new StubSourceParser(new AIImportSourceDocument
        {
            SourceFormat = AIImportSourceFormats.Docx,
            ExtractedText = "Mã danh mục: CAT_REVIEW\nTên danh mục: Cần xem lại",
            Errors = [new AIImportErrorDto { Code = "DOCX_CẤU_TRÚC_KHÔNG_RÕ", Message = "Cần AI." }]
        });
        var ollama = new Mock<IOllamaClient>();
        ollama.Setup(client => client.ChatStructuredAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), "AIImport.DocumentExtraction", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaResultDTO
            {
                Success = true,
                Content = """{"records":[{"entity":"Category","confidence":0.55,"evidence":"Mã danh mục: CAT_REVIEW\nTên danh mục: Cần xem lại","fields":{"CategoryCode":"CAT_REVIEW","Name":"Cần xem lại"}}]}"""
            });
        var extractor = new AIImportDocumentAiExtractor(ollama.Object, new AIImportSchemaRegistry(), TestOptions());
        var pipeline = new AIImportDocumentPipeline([sourceParser], extractor);

        var result = await pipeline.AnalyzeAsync(new AIImportSourceFile("review.docx", [1]), null, default);

        Assert.Empty(result.Errors);
        var group = Assert.Single(result.Groups);
        Assert.Equal(0.55m, group.Confidence);
        Assert.Contains(result.Warnings, error => error.Code == "AI_CONFIDENCE_THẤP");
    }

    [Fact]
    public async Task Pipeline_enriches_unknown_groups_even_when_document_also_has_known_groups()
    {
        var known = new AIImportSourceGroup
        {
            SourceLabel = "Bảng danh mục",
            ExtractionMode = AIImportExtractionModes.DocxTableDeterministic,
            EntityType = AIImportEntityType.Category,
            Confidence = 0.95m
        };
        known.Candidates.Add(new AIImportSourceCandidate
        {
            RawData = new(StringComparer.OrdinalIgnoreCase) { ["CategoryCode"] = "CAT_KNOWN", ["Name"] = "Đã biết" },
            MappedData = new(StringComparer.OrdinalIgnoreCase) { ["CategoryCode"] = "CAT_KNOWN", ["Name"] = "Đã biết" }
        });
        var sourceParser = new StubSourceParser(new AIImportSourceDocument
        {
            SourceFormat = AIImportSourceFormats.Docx,
            ExtractedText = "Mã danh mục: CAT_AI_MIXED\nTên danh mục: AI bổ sung",
            Groups =
            [
                known,
                new AIImportSourceGroup
                {
                    SourceLabel = "Đoạn chưa rõ",
                    ExtractionMode = AIImportExtractionModes.DocxTextDeterministic,
                    EntityType = AIImportEntityType.Unknown,
                    Confidence = 0.2m
                }
            ]
        });
        var ollama = new Mock<IOllamaClient>();
        ollama.Setup(client => client.ChatStructuredAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), "AIImport.DocumentExtraction", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaResultDTO
            {
                Success = true,
                Content = """{"records":[{"entity":"Category","confidence":0.95,"evidence":"Mã danh mục: CAT_AI_MIXED\nTên danh mục: AI bổ sung","fields":{"CategoryCode":"CAT_AI_MIXED","Name":"AI bổ sung"}}]}"""
            });
        var pipeline = new AIImportDocumentPipeline(
            [sourceParser],
            new AIImportDocumentAiExtractor(ollama.Object, new AIImportSchemaRegistry(), TestOptions()));

        var result = await pipeline.AnalyzeAsync(new AIImportSourceFile("mixed.docx", [1]), null, default);

        Assert.Contains(result.Groups, group => group.ExtractionMode == AIImportExtractionModes.DocxAiExtraction);
    }

    [Fact]
    public async Task Ai_extraction_rejects_document_when_chunk_limit_would_drop_trailing_text()
    {
        var options = TestOptions();
        options.Value.MaxAIChunks = 2;
        options.Value.AIChunkMaxCharacters = 12_000;
        options.Value.AIChunkOverlapCharacters = 500;
        var ollama = new Mock<IOllamaClient>();
        ollama.Setup(client => client.ChatStructuredAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), "AIImport.DocumentExtraction", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaResultDTO { Success = true, Content = """{"records":[]}""" });
        var document = new AIImportSourceDocument
        {
            SourceFormat = AIImportSourceFormats.Docx,
            ExtractedText = new string('x', 23_501)
        };
        var extractor = new AIImportDocumentAiExtractor(ollama.Object, new AIImportSchemaRegistry(), options);

        await extractor.EnrichAsync(document, null, default);

        Assert.Contains(document.Errors, error => error.Code == "CHUNK_VƯỢT_GIỚI_HẠN");
        ollama.Verify(client => client.ChatStructuredAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static IOptions<AIImportOptions> TestOptions() => Options.Create(new AIImportOptions());

    private static byte[] CreateDocx(params string[] paragraphs)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var main = document.AddMainDocumentPart();
            main.Document = new OpenXmlDocument(new Body(paragraphs.Select(text =>
                new Paragraph(new Run(new Text(text))))));
            main.Document.Save();
        }

        return stream.ToArray();
    }

    private static TableCell Cell(string text, OpenXmlElement? property = null)
    {
        var cell = new TableCell();
        if (property != null) cell.Append(new TableCellProperties(property));
        cell.Append(new Paragraph(new Run(new Text(text))));
        return cell;
    }

    private sealed class StubSourceParser(AIImportSourceDocument result) : IAIImportSourceParser
    {
        public string SourceFormat => AIImportSourceFormats.Docx;
        public Task<AIImportSourceDocument> ParseAsync(AIImportSourceFile source, AIImportEntityType? entityHint, CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }
}
