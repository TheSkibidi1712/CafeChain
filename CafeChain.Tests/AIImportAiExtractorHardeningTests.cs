using CafeChain.Application.DTOs.AI;
using CafeChain.Application.Interfaces.AI;
using CafeChain.Application.Options;
using CafeChain.Application.Services.AIImport;
using CafeChain.Models.AIImport;
using Microsoft.Extensions.Options;
using Moq;

namespace CafeChain.Tests;

public sealed class AIImportAiExtractorHardeningTests
{
    [Fact]
    public async Task Malformed_json_is_retried_once_with_same_operation_and_versions_are_recorded()
    {
        var ollama = new Mock<IOllamaClient>();
        ollama.SetupSequence(client => client.ChatStructuredAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), "AIImport.DocumentExtraction", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaResultDTO { Success = true, Content = "{ malformed" })
            .ReturnsAsync(new OllamaResultDTO
            {
                Success = true,
                Content = """{"records":[{"entity":"Category","confidence":0.95,"evidence":"Mã danh mục: CAT_RETRY\nTên danh mục: Retry","fields":{"CategoryCode":"CAT_RETRY","Name":"Retry"}}]}"""
            });
        var document = Source("Mã danh mục: CAT_RETRY\nTên danh mục: Retry");

        await Extractor(ollama).EnrichAsync(document, null, default);

        Assert.Single(document.Groups);
        Assert.Equal("ai-import-document-v2", document.Metadata["promptVersion"]);
        Assert.Equal("ai-import-record-schema-v2", document.Metadata["schemaVersion"]);
        ollama.Verify(client => client.ChatStructuredAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), "AIImport.DocumentExtraction", It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Transient_transport_failure_is_retried_once_without_relaxing_schema()
    {
        var ollama = new Mock<IOllamaClient>();
        ollama.SetupSequence(client => client.ChatStructuredAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), "AIImport.DocumentExtraction", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("transient"))
            .ReturnsAsync(new OllamaResultDTO
            {
                Success = true,
                Content = """{"records":[{"entity":"Category","confidence":0.95,"evidence":"Mã danh mục: CAT_NET\nTên danh mục: Network","fields":{"CategoryCode":"CAT_NET","Name":"Network"}}]}"""
            });
        var document = Source("Mã danh mục: CAT_NET\nTên danh mục: Network");

        await Extractor(ollama).EnrichAsync(document, null, default);

        Assert.Single(document.Groups);
        ollama.Verify(client => client.ChatStructuredAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), "AIImport.DocumentExtraction", It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Same_business_key_with_different_payload_across_semantic_chunks_is_a_conflict()
    {
        var first = "Mã danh mục: CAT_CONFLICT\nTên danh mục: Tên A";
        var second = "Mã danh mục: CAT_CONFLICT\nTên danh mục: Tên B";
        var ollama = new Mock<IOllamaClient>();
        ollama.SetupSequence(client => client.ChatStructuredAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), "AIImport.DocumentExtraction", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaResultDTO { Success = true, Content = Response(first, "Tên A") })
            .ReturnsAsync(new OllamaResultDTO { Success = true, Content = Response(second, "Tên B") });
        var document = Source(first + "\n" + second);
        document.Blocks.Add(new AIImportSemanticBlock { Ordinal = 1, Text = first + new string(' ', 960) });
        document.Blocks.Add(new AIImportSemanticBlock { Ordinal = 2, Text = second + new string(' ', 960) });

        await Extractor(ollama).EnrichAsync(document, null, default);

        Assert.Equal(2, document.Groups.Count);
        Assert.All(document.Groups, group => Assert.Contains(group.Issues,
            issue => issue.Code == "XUNG_ĐỘT_TRÍCH_XUẤT"));
    }

    [Fact]
    public async Task Value_outside_evidence_is_rejected_with_semantic_failure_taxonomy()
    {
        var ollama = new Mock<IOllamaClient>();
        ollama.Setup(client => client.ChatStructuredAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), "AIImport.DocumentExtraction", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaResultDTO
            {
                Success = true,
                Content = """{"records":[{"entity":"Category","confidence":0.95,"evidence":"Mã danh mục: CAT_SAFE","fields":{"CategoryCode":"CAT_INVENTED","Name":"Invented"}}]}"""
            });
        var document = Source("Mã danh mục: CAT_SAFE");

        await Extractor(ollama).EnrichAsync(document, null, default);

        Assert.Empty(document.Groups);
        Assert.Equal("AI_SEMANTIC_EVIDENCE_ERROR", document.Metadata["aiFailureType"]);
    }

    [Fact]
    public async Task Evidence_whitespace_is_mapped_back_to_the_original_contiguous_source_span()
    {
        const string source = "Mã danh mục: CAT_SPACE\nTên danh mục: Khoảng trắng";
        var ollama = new Mock<IOllamaClient>();
        ollama.Setup(client => client.ChatStructuredAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), "AIImport.DocumentExtraction", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaResultDTO
            {
                Success = true,
                Content = """{"records":[{"entity":"Category","confidence":0.95,"evidence":"Mã danh mục: CAT_SPACE Tên danh mục: Khoảng trắng","fields":{"CategoryCode":"CAT_SPACE","Name":"Khoảng trắng"}}]}"""
            });
        var document = Source(source);

        await Extractor(ollama).EnrichAsync(document, AIImportEntityType.Category, default);

        var candidate = Assert.Single(Assert.Single(document.Groups).Candidates);
        Assert.Equal(source, candidate.EvidenceSnippet);
        Assert.Equal(0, candidate.SourceLocator.TextStart);
        Assert.Equal(source.Length, candidate.SourceLocator.TextEnd);
    }

    [Fact]
    public async Task Synthesized_evidence_is_recovered_only_from_unique_verbatim_field_values()
    {
        const string source = "Hãy tạo đồ uống mã DR_SAFE có tên Tonic, thuộc danh mục CAT_SAFE và loại DRINK.";
        var ollama = new Mock<IOllamaClient>();
        ollama.Setup(client => client.ChatStructuredAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), "AIImport.DocumentExtraction", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaResultDTO
            {
                Success = true,
                Content = """{"records":[{"entity":"Drink","confidence":0.95,"evidence":"DR_SAFE | Tonic | CAT_SAFE | DRINK","fields":{"DrinkCode":"DR_SAFE","Name":"Tonic","Category":"CAT_SAFE","ProductType":"DRINK"}}]}"""
            });
        var document = Source(source);

        await Extractor(ollama).EnrichAsync(document, AIImportEntityType.Drink, default);

        var candidate = Assert.Single(Assert.Single(document.Groups).Candidates);
        Assert.All(candidate.MappedData.Values.Where(value => value != null),
            value => Assert.Contains(value!, candidate.EvidenceSnippet, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(document.Warnings, warning => warning.Code == "AI_EVIDENCE_ĐƯỢC_CHUẨN_HÓA");
    }

    [Fact]
    public async Task Percentage_confidence_is_bounded_and_normalized_to_zero_one()
    {
        const string source = "Mã danh mục: CAT_PERCENT\nTên danh mục: Phần trăm";
        var ollama = new Mock<IOllamaClient>();
        ollama.Setup(client => client.ChatStructuredAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), "AIImport.DocumentExtraction", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaResultDTO
            {
                Success = true,
                Content = """{"records":[{"entity":"Category","confidence":95,"evidence":"Mã danh mục: CAT_PERCENT\nTên danh mục: Phần trăm","fields":{"CategoryCode":"CAT_PERCENT","Name":"Phần trăm"}}]}"""
            });
        var document = Source(source);

        await Extractor(ollama).EnrichAsync(document, AIImportEntityType.Category, default);

        var candidate = Assert.Single(Assert.Single(document.Groups).Candidates);
        Assert.Equal(0.95m, candidate.AiConfidence);
        Assert.Contains(document.Warnings, warning => warning.Code == "AI_CONFIDENCE_ĐƯỢC_CHUẨN_HÓA");
    }

    [Fact]
    public async Task Enrichment_preserves_deterministic_groups_with_multiple_candidates()
    {
        const string evidence = "Mã danh mục: CAT_AI\nTên danh mục: AI bổ sung";
        var ollama = new Mock<IOllamaClient>();
        ollama.Setup(client => client.ChatStructuredAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), "AIImport.DocumentExtraction", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaResultDTO
            {
                Success = true,
                Content = """{"records":[{"entity":"Category","confidence":0.95,"evidence":"Mã danh mục: CAT_AI\nTên danh mục: AI bổ sung","fields":{"CategoryCode":"CAT_AI","Name":"AI bổ sung"}}]}"""
            });
        var document = Source(evidence);
        var deterministic = new AIImportSourceGroup
        {
            EntityType = AIImportEntityType.Category,
            ExtractionMode = AIImportExtractionModes.PdfOcrDeterministic
        };
        deterministic.Candidates.Add(new AIImportSourceCandidate
        {
            MappedData = new(StringComparer.OrdinalIgnoreCase) { ["CategoryCode"] = "CAT_1", ["Name"] = "Một" }
        });
        deterministic.Candidates.Add(new AIImportSourceCandidate
        {
            MappedData = new(StringComparer.OrdinalIgnoreCase) { ["CategoryCode"] = "CAT_2", ["Name"] = "Hai" }
        });
        document.Groups.Add(deterministic);

        await Extractor(ollama).EnrichAsync(document, AIImportEntityType.Category, default);

        Assert.Contains(deterministic, document.Groups);
        Assert.Equal(2, deterministic.Candidates.Count);
        Assert.Contains(document.Groups, group => group.ExtractionMode == AIImportExtractionModes.DocxAiExtraction);
    }

    [Fact]
    public async Task Empty_ai_fallback_does_not_turn_existing_deterministic_candidate_into_document_error()
    {
        var ollama = new Mock<IOllamaClient>();
        ollama.Setup(client => client.ChatStructuredAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), "AIImport.DocumentExtraction", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaResultDTO { Success = true, Content = """{"records":[]}""" });
        var document = Source("OCR text cần người dùng xem lại");
        var group = new AIImportSourceGroup { EntityType = AIImportEntityType.Unknown };
        group.Candidates.Add(new AIImportSourceCandidate
        {
            RawData = new(StringComparer.OrdinalIgnoreCase) { ["Nhãn OCR"] = "Giá trị OCR" }
        });
        document.Groups.Add(group);

        await Extractor(ollama).EnrichAsync(document, AIImportEntityType.Category, default);

        Assert.DoesNotContain(document.Errors, error => error.Code == "AI_TRÍCH_XUẤT_KHÔNG_CÓ_BẰNG_CHỨNG");
        Assert.Contains(document.Warnings, warning => warning.Code == "AI_TRÍCH_XUẤT_KHÔNG_CÓ_BẰNG_CHỨNG");
        Assert.Contains(group, document.Groups);
    }

    private static AIImportDocumentAiExtractor Extractor(Mock<IOllamaClient> ollama) => new(
        ollama.Object,
        new AIImportSchemaRegistry(),
        Options.Create(new AIImportOptions { AIChunkMaxCharacters = 1_000, AIChunkOverlapCharacters = 0 }));

    private static AIImportSourceDocument Source(string text) => new()
    {
        SourceFormat = AIImportSourceFormats.Docx,
        ExtractedText = text
    };

    private static string Response(string evidence, string name) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            records = new[]
            {
                new
                {
                    entity = "Category",
                    confidence = 0.95m,
                    evidence,
                    fields = new Dictionary<string, string?>
                    {
                        ["CategoryCode"] = "CAT_CONFLICT",
                        ["Name"] = name
                    }
                }
            }
        });
}
