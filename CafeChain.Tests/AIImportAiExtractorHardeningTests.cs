using CafeChain.Application.DTOs.AI;
using CafeChain.Application.Interfaces.AI;
using CafeChain.Application.Options;
using CafeChain.Application.Services.AIImport;
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
