using CafeChain.Application.DTOs.AI;
using CafeChain.Application.Interfaces.AI;
using CafeChain.Application.Services.AI;
using CafeChain.Infrastructure.Configurations;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace CafeChain.Tests;

public sealed class AIImagePipelineTests
{
    [Fact]
    public void Drink_visual_specification_is_structured_and_uses_multiple_english_queries()
    {
        var builder = new VisualSpecificationBuilder();

        var spec = builder.BuildDrink(
            "Trà đào cam sả",
            "Trà trái cây với đào, cam và sả",
            "peach orange lemongrass iced tea");

        Assert.Equal("beverage", spec.SubjectType);
        Assert.Equal("square", spec.Orientation);
        Assert.InRange(spec.PexelsQueries.Count, 3, 6);
        Assert.Contains(spec.MainIngredients, x => x.Contains("peach", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("person", spec.ForbiddenKeywords);
        Assert.Contains("peach", spec.ComfyPositivePrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("watermark", spec.ComfyNegativePrompt, StringComparison.OrdinalIgnoreCase);
        Assert.All(spec.PexelsQueries, query => Assert.True(query.All(character => character <= 127)));
    }

    [Fact]
    public void Topping_visual_specification_does_not_search_for_a_full_beverage()
    {
        var spec = new VisualSpecificationBuilder().BuildTopping("Trân châu đen");

        Assert.Equal("food ingredient", spec.SubjectType);
        Assert.Contains("full beverage", spec.ForbiddenKeywords);
        Assert.Contains(spec.PexelsQueries, x => x.Contains("tapioca", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Metadata_scorer_rejects_low_resolution_wrong_orientation_and_forbidden_objects()
    {
        var options = Options.Create(DefaultOptions());
        var scorer = new PexelsMetadataScorer(options);
        var spec = new VisualSpecificationBuilder().BuildDrink("Trà đào cam sả", "Trà đào cam sả");

        var lowResolution = scorer.Score(Photo(320, 320, "glass of peach tea"), spec, spec.PexelsQueries[0]);
        var portrait = scorer.Score(Photo(700, 1400, "glass of peach tea"), spec, spec.PexelsQueries[0]);
        var forbidden = scorer.Score(Photo(1000, 1000, "person holding a glass of peach tea"), spec, spec.PexelsQueries[0]);

        Assert.True(lowResolution.Rejected);
        Assert.True(portrait.Rejected);
        Assert.True(forbidden.Rejected);
    }

    [Fact]
    public async Task Search_deduplicates_ranks_and_returns_at_most_three_user_candidates()
    {
        var options = DefaultOptions();
        options.MinimumCandidateScore = 0.35;
        var pexels = new Mock<IPexelsClient>();
        pexels.Setup(x => x.SearchAsync(It.IsAny<PexelsSearchRequestDTO>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PexelsSearchRequestDTO request, CancellationToken _) => new PexelsSearchResultDTO
            {
                Success = true,
                Photos =
                [
                    Photo(1200, 1200, "clear glass of peach orange iced tea", 10),
                    Photo(1000, 1000, "amber fruit tea in glass", 11),
                    Photo(900, 900, "iced tea product photography", 12),
                    Photo(800, 800, "fruit tea", 13),
                    Photo(1200, 1200, "clear glass of peach orange iced tea", 10)
                ]
            });
        var service = CreatePipeline(pexels.Object, Mock.Of<IComfyUIClient>(), options);
        var spec = new VisualSpecificationBuilder().BuildDrink("Trà đào cam sả", "Trà đào cam sả");

        var result = await service.SearchReferenceImagesAsync(new AIReferenceSearchRequestDTO
        {
            RequestId = Guid.NewGuid(), SuggestionId = Guid.NewGuid(), EntityType = "Drink", VisualSpecification = spec
        });

        Assert.True(result.Success);
        Assert.InRange(result.Candidates.Count, 1, 3);
        Assert.Equal(result.Candidates.Count, result.Candidates.Select(x => x.PhotoId).Distinct().Count());
        Assert.Equal(result.Candidates.OrderByDescending(x => x.Score).Select(x => x.PhotoId),
            result.Candidates.Select(x => x.PhotoId));
        pexels.Verify(x => x.SearchAsync(It.IsAny<PexelsSearchRequestDTO>(), It.IsAny<CancellationToken>()),
            Times.AtLeast(2));
    }

    [Fact]
    public async Task Generation_requires_reference_and_returns_all_technically_valid_outputs()
    {
        var options = DefaultOptions();
        options.MinimumCandidateScore = 0.20;
        var bytes = CreatePng(512, 512);
        var photo = Photo(1000, 1000, "clear glass of peach iced tea", 42);
        var pexels = new Mock<IPexelsClient>();
        pexels.Setup(x => x.DownloadPhotoAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PexelsImageResultDTO
            {
                Success = true, Bytes = bytes, ContentType = "image/png", FileName = "reference.png", Photo = photo
            });
        var comfy = new Mock<IComfyUIClient>();
        comfy.Setup(x => x.GenerateImageAsync(It.IsAny<ComfyUIImageRequestDTO>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComfyUIImageResultDTO
            {
                Success = true,
                PromptId = "prompt-1",
                Images = Enumerable.Range(1, 3).Select(index => new ComfyUIImageOutputDTO
                {
                    Bytes = bytes, ContentType = "image/png", FileName = $"output-{index}.png"
                }).ToList()
            });
        var service = CreatePipeline(pexels.Object, comfy.Object, options);
        var spec = new VisualSpecificationBuilder().BuildDrink("Trà đào", "Trà đào");

        var result = await service.GenerateFromReferenceAsync(new AIGenerateFromReferenceRequestDTO
        {
            RequestId = Guid.NewGuid(), SuggestionId = Guid.NewGuid(), EntityType = "Drink",
            PhotoId = 42, VisualSpecification = spec, FileNamePrefix = "TRA_DAO"
        });

        Assert.True(result.Success);
        Assert.Equal(3, result.GeneratedImages.Count);
        Assert.All(result.GeneratedImages, image => Assert.True(image.TechnicalValidationPassed));
        comfy.Verify(x => x.GenerateImageAsync(
            It.Is<ComfyUIImageRequestDTO>(request => request.ReferenceImageBytes.Length > 0
                && request.OutputCount == 3 && request.Denoise == 0.55),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Generation_does_not_call_comfy_without_a_valid_photo_id()
    {
        var comfy = new Mock<IComfyUIClient>();
        var service = CreatePipeline(Mock.Of<IPexelsClient>(), comfy.Object, DefaultOptions());

        var result = await service.GenerateFromReferenceAsync(new AIGenerateFromReferenceRequestDTO
        {
            RequestId = Guid.NewGuid(), SuggestionId = Guid.NewGuid(), EntityType = "Topping",
            PhotoId = 0, VisualSpecification = new VisualSpecificationBuilder().BuildTopping("Trân châu đen")
        });

        Assert.False(result.Success);
        comfy.Verify(x => x.GenerateImageAsync(It.IsAny<ComfyUIImageRequestDTO>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Search_exposes_text_fallback_only_for_no_match_or_pexels_unavailable()
    {
        var noMatchPexels = new Mock<IPexelsClient>();
        noMatchPexels.Setup(x => x.SearchAsync(It.IsAny<PexelsSearchRequestDTO>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PexelsSearchResultDTO { Success = true, Photos = [] });
        var service = CreatePipeline(noMatchPexels.Object, Mock.Of<IComfyUIClient>(), DefaultOptions());
        var result = await service.SearchReferenceImagesAsync(new AIReferenceSearchRequestDTO
        {
            RequestId = Guid.NewGuid(), SuggestionId = Guid.NewGuid(), EntityType = "Drink",
            VisualSpecification = new VisualSpecificationBuilder().BuildDrink("Peach tea", "Peach tea")
        });

        Assert.False(result.Success);
        Assert.Equal("NO_SUITABLE_PEXELS_IMAGE", result.FailureCode);
        Assert.True(result.TextFallbackAvailable);

        var invalid = await service.SearchReferenceImagesAsync(new AIReferenceSearchRequestDTO
        {
            RequestId = Guid.Empty, SuggestionId = Guid.NewGuid(), EntityType = "Drink",
            VisualSpecification = new VisualSpecificationBuilder().BuildDrink("Peach tea", "Peach tea")
        });
        Assert.Equal("INVALID_REQUEST", invalid.FailureCode);
        Assert.False(invalid.TextFallbackAvailable);
    }

    [Fact]
    public async Task Direct_pexels_image_is_normalized_and_keeps_attribution_in_response_only()
    {
        var options = DefaultOptions();
        options.MinimumCandidateScore = 0.20;
        var pexels = new Mock<IPexelsClient>();
        pexels.Setup(x => x.DownloadPhotoAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PexelsImageResultDTO
            {
                Success = true, Bytes = CreatePng(1800, 1800), ContentType = "image/png",
                Photo = new PexelsPhotoDTO
                {
                    Id = 42, Width = 1800, Height = 1800, Alt = "clear glass of peach iced tea",
                    Photographer = "Cafe Artist", Url = "https://www.pexels.com/photo/42",
                    PreviewUrl = "https://images.pexels.com/photos/42/preview.jpeg",
                    DownloadUrl = "https://images.pexels.com/photos/42/large.jpeg"
                }
            });
        var service = CreatePipeline(pexels.Object, Mock.Of<IComfyUIClient>(), options);

        var result = await service.UsePexelsImageAsync(new AIUsePexelsImageRequestDTO
        {
            RequestId = Guid.NewGuid(), SuggestionId = Guid.NewGuid(), EntityType = "Topping", PhotoId = 42,
            MatchedQuery = "peach topping product photo",
            VisualSpecification = new VisualSpecificationBuilder().BuildTopping("Peach topping")
        });

        Assert.True(result.Success);
        var output = Assert.Single(result.GeneratedImages);
        Assert.Equal("Pexels", output.Source);
        Assert.Equal(42, output.ExternalPhotoId);
        Assert.Contains("Cafe Artist", output.AttributionText);
        Assert.Equal("image/jpeg", output.ContentType);
        Assert.True(Convert.FromBase64String(output.Base64Data).Length < 3 * 1024 * 1024);
    }

    [Fact]
    public async Task Text_fallback_builds_detailed_prompt_and_never_sends_reference_bytes()
    {
        var options = DefaultOptions();
        options.AllowTextOnlyFallback = true;
        var bytes = CreatePng(512, 512);
        var comfy = new Mock<IComfyUIClient>();
        ComfyUIImageRequestDTO? captured = null;
        comfy.Setup(x => x.GenerateImageAsync(It.IsAny<ComfyUIImageRequestDTO>(), It.IsAny<CancellationToken>()))
            .Callback<ComfyUIImageRequestDTO, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(new ComfyUIImageResultDTO
            {
                Success = true, PromptId = "text-1",
                Images = Enumerable.Range(1, 3).Select(x => new ComfyUIImageOutputDTO
                { Bytes = bytes, ContentType = "image/png", FileName = $"text-{x}.png" }).ToList()
            });
        var service = CreatePipeline(Mock.Of<IPexelsClient>(), comfy.Object, options);
        var spec = new VisualSpecificationBuilder().BuildDrink("Peach tea", "Peach tea with orange");

        var result = await service.GenerateFromPromptAsync(new AIGenerateFromPromptRequestDTO
        {
            RequestId = Guid.NewGuid(), SuggestionId = Guid.NewGuid(), EntityType = "Drink",
            VisualSpecification = spec
        });

        Assert.True(result.Success);
        Assert.Equal(3, result.GeneratedImages.Count);
        Assert.NotNull(captured);
        Assert.Equal(ComfyUIGenerationMode.TextToImage, captured.GenerationMode);
        Assert.Empty(captured.ReferenceImageBytes);
        Assert.Equal(1.0, captured.Denoise);
        Assert.Contains("background:", captured.PositivePrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("lighting:", captured.PositivePrompt, StringComparison.OrdinalIgnoreCase);
    }

    private static AIImagePipelineService CreatePipeline(
        IPexelsClient pexels, IComfyUIClient comfy, AIImagePipelineOptions options) => new(
        pexels,
        comfy,
        new PexelsMetadataScorer(Options.Create(options)),
        new MemoryCache(new MemoryCacheOptions()),
        Options.Create(options),
        NullLogger<AIImagePipelineService>.Instance);

    private static AIImagePipelineOptions DefaultOptions() => new()
    {
        MinimumImageWidth = 640,
        MinimumImageHeight = 640,
        MinimumCandidateScore = 0.60,
        PreferredCandidateScore = 0.75,
        MaximumQueries = 6,
        MaximumSearchRounds = 3,
        MaximumCandidates = 3,
        PexelsResultsPerQuery = 15,
        GeneratedOutputCount = 3,
        GeneratedWidth = 1024,
        GeneratedHeight = 1024,
        DefaultDenoise = 0.55
    };

    private static PexelsPhotoDTO Photo(int width, int height, string alt, long id = 1) => new()
    {
        Id = id, Width = width, Height = height, Alt = alt, AverageColor = "#d89b52",
        Url = $"https://www.pexels.com/photo/{id}",
        PreviewUrl = $"https://images.pexels.com/photos/{id}/preview.jpeg",
        DownloadUrl = $"https://images.pexels.com/photos/{id}/large.jpeg"
    };

    private static byte[] CreatePng(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }
}
