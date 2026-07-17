using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using CafeChain.Application.DTOs.AI;
using CafeChain.Application.Interfaces.AI;
using CafeChain.Infrastructure.Configurations;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace CafeChain.Application.Services.AI;

public sealed class AIImagePipelineService : IAIImagePipelineService
{
    private readonly IPexelsClient _pexels;
    private readonly IComfyUIClient _comfyUI;
    private readonly IPexelsMetadataScorer _scorer;
    private readonly IMemoryCache _cache;
    private readonly AIImagePipelineOptions _options;
    private readonly ILogger<AIImagePipelineService> _logger;

    public AIImagePipelineService(
        IPexelsClient pexels,
        IComfyUIClient comfyUI,
        IPexelsMetadataScorer scorer,
        IMemoryCache cache,
        IOptions<AIImagePipelineOptions> options,
        ILogger<AIImagePipelineService> logger)
    {
        _pexels = pexels;
        _comfyUI = comfyUI;
        _scorer = scorer;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AIReferenceSearchResultDTO> SearchReferenceImagesAsync(
        AIReferenceSearchRequestDTO request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var validation = Validate(request.EntityType, request.RequestId, request.SuggestionId, request.VisualSpecification);
        if (validation != null) return SearchFailure(request, validation, false, "INVALID_REQUEST", false);

        var queries = request.VisualSpecification.PexelsQueries
            .Select(x => x.Trim()).Where(x => x.Length is >= 2 and <= 200)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(_options.MaximumQueries, 3, 6)).ToList();
        if (queries.Count < 3) return SearchFailure(request, "Visual Specification phải có ít nhất 3 Pexels query hợp lệ.", false, "INVALID_REQUEST", false);

        var cacheKey = BuildCacheKey(request.EntityType, request.VisualSpecification);
        if (_cache.TryGetValue<List<PexelsImageCandidateDTO>>(cacheKey, out var cached) && cached != null)
        {
            var usable = cached.Where(x => !request.ExcludedPhotoIds.Contains(x.PhotoId))
                .Take(Math.Clamp(_options.MaximumCandidates, 1, 5)).ToList();
            if (usable.Count > 0)
                return SearchSuccess(request, usable, "Đã lấy ứng viên Pexels đã xếp hạng từ cache.");
        }

        var candidates = new Dictionary<long, PexelsImageCandidateDTO>();
        var errors = new List<string>();
        var retryable = false;
        var successfulSearches = 0;
        var rounds = Math.Clamp(_options.MaximumSearchRounds, 1, 3);
        var chunkSize = (int)Math.Ceiling(queries.Count / (double)rounds);
        for (var round = 0; round < rounds; round++)
        {
            foreach (var query in queries.Skip(round * chunkSize).Take(chunkSize))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var search = await _pexels.SearchAsync(new PexelsSearchRequestDTO
                {
                    Query = query,
                    Orientation = request.VisualSpecification.Orientation,
                    PerPage = Math.Clamp(_options.PexelsResultsPerQuery, 10, 20),
                    ExcludedPhotoIds = request.ExcludedPhotoIds
                }, cancellationToken);
                if (!search.Success)
                {
                    errors.Add(search.ErrorMessage ?? "Pexels search failed.");
                    retryable |= search.Retryable;
                    continue;
                }

                successfulSearches++;

                foreach (var photo in search.Photos)
                {
                    var score = _scorer.Score(photo, request.VisualSpecification, query);
                    if (score.Rejected || score.Score < Math.Clamp(_options.MinimumCandidateScore, 0, 1)) continue;
                    var mapped = Map(photo, score, query);
                    if (!candidates.TryGetValue(photo.Id, out var existing) || mapped.Score > existing.Score)
                        candidates[photo.Id] = mapped;
                }
            }

            if (candidates.Values.Any(x => x.Score >= _options.PreferredCandidateScore)) break;
        }

        var ranked = candidates.Values.OrderByDescending(x => x.Score).ThenByDescending(x => x.Width * x.Height)
            .Take(Math.Clamp(_options.MaximumCandidates, 1, 5)).ToList();
        if (ranked.Count == 0)
        {
            var message = errors.Count > 0
                ? $"Không tìm thấy ảnh Pexels đủ phù hợp. {errors.Distinct().First()}"
                : "Không tìm thấy ảnh Pexels đủ phù hợp với gợi ý.";
            LogSearch(request, 0, queries.Count, stopwatch.ElapsedMilliseconds, false);
            var unavailable = successfulSearches == 0 && errors.Count > 0;
            return SearchFailure(request, message, retryable || unavailable,
                unavailable ? "PEXELS_UNAVAILABLE" : "NO_SUITABLE_PEXELS_IMAGE", true);
        }

        _cache.Set(cacheKey, ranked, TimeSpan.FromMinutes(Math.Clamp(_options.CacheMinutes, 1, 1440)));
        LogSearch(request, ranked.Count, queries.Count, stopwatch.ElapsedMilliseconds, true);
        return SearchSuccess(request, ranked, "Đã xếp hạng ứng viên Pexels. Vui lòng chọn ảnh tham chiếu.");
    }

    public async Task<AIGenerateFromReferenceResultDTO> GenerateFromReferenceAsync(
        AIGenerateFromReferenceRequestDTO request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var validation = Validate(request.EntityType, request.RequestId, request.SuggestionId, request.VisualSpecification);
        if (validation != null || request.PhotoId <= 0)
            return GenerateFailure(request, validation ?? "Pexels Photo ID không hợp lệ.", false);

        var reference = await _pexels.DownloadPhotoAsync(request.PhotoId, cancellationToken);
        if (!reference.Success || reference.Bytes == null || reference.Photo == null)
            return GenerateFailure(request, reference.ErrorMessage ?? "Không tải được ảnh tham chiếu Pexels.", reference.Retryable);
        var referenceScore = _scorer.Score(reference.Photo, request.VisualSpecification,
            ResolveMatchedQuery(request.MatchedQuery, request.VisualSpecification));
        if (referenceScore.Rejected)
            return GenerateFailure(request, "Ảnh Pexels đã chọn không vượt qua kiểm tra metadata.", false);

        var profile = GetProfile(request.EntityType);
        var generation = await _comfyUI.GenerateImageAsync(new ComfyUIImageRequestDTO
        {
            GenerationMode = ComfyUIGenerationMode.ReferenceImage,
            ReferenceImageBytes = reference.Bytes,
            ReferenceContentType = reference.ContentType ?? "image/jpeg",
            PositivePrompt = request.VisualSpecification.ComfyPositivePrompt,
            NegativePrompt = request.VisualSpecification.ComfyNegativePrompt,
            FileNamePrefix = request.FileNamePrefix,
            OutputCount = Math.Clamp(_options.GeneratedOutputCount, 2, 4),
            Width = Math.Clamp(_options.GeneratedWidth, 256, 2048),
            Height = Math.Clamp(_options.GeneratedHeight, 256, 2048),
            Denoise = profile.Denoise ?? _options.DefaultDenoise
        }, cancellationToken);
        if (!generation.Success)
            return GenerateFailure(request, generation.ErrorMessage ?? "Không thể tạo ảnh bằng ComfyUI.", true);

        var validImages = generation.Images.Select(x => ValidateOutput(x, request.VisualSpecification.Orientation))
            .Where(x => x.TechnicalValidationPassed).ToList();
        if (validImages.Count == 0)
            return GenerateFailure(request, "ComfyUI không tạo được output vượt qua kiểm tra kỹ thuật.", true);

        _logger.LogInformation(
            "AI image generation completed. RequestId={RequestId} SuggestionId={SuggestionId} EntityType={EntityType} PhotoId={PhotoId} PromptId={PromptId} OutputCount={OutputCount} ElapsedMs={ElapsedMs}",
            request.RequestId, request.SuggestionId, request.EntityType, request.PhotoId,
            generation.PromptId, validImages.Count, stopwatch.ElapsedMilliseconds);
        return new AIGenerateFromReferenceResultDTO
        {
            Success = true,
            RequestId = request.RequestId,
            SuggestionId = request.SuggestionId,
            Stage = "Completed",
            Message = "Đã tạo các ảnh hợp lệ về kỹ thuật. Vui lòng chọn ảnh phù hợp nhất.",
            PexelsReference = Map(reference.Photo, referenceScore,
                ResolveMatchedQuery(request.MatchedQuery, request.VisualSpecification)),
            GeneratedImages = validImages,
            Warnings = ["Hệ thống chưa dùng vision model; người dùng phải xác nhận nội dung ảnh cuối."]
        };
    }

    public async Task<AIGenerateFromReferenceResultDTO> UsePexelsImageAsync(
        AIUsePexelsImageRequestDTO request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var validation = Validate(request.EntityType, request.RequestId, request.SuggestionId, request.VisualSpecification);
        if (validation != null || request.PhotoId <= 0)
            return GenerateFailure(request.RequestId, request.SuggestionId,
                validation ?? "Pexels Photo ID không hợp lệ.", false, "PexelsDirectValidation");

        var downloaded = await _pexels.DownloadPhotoAsync(request.PhotoId, cancellationToken);
        if (!downloaded.Success || downloaded.Bytes == null || downloaded.Photo == null)
            return GenerateFailure(request.RequestId, request.SuggestionId,
                downloaded.ErrorMessage ?? "Không tải được ảnh Pexels.", downloaded.Retryable, "PexelsDirectDownload");

        var matchedQuery = ResolveMatchedQuery(request.MatchedQuery, request.VisualSpecification);
        var score = _scorer.Score(downloaded.Photo, request.VisualSpecification, matchedQuery);
        if (score.Rejected || score.Score < Math.Clamp(_options.MinimumCandidateScore, 0, 1))
            return GenerateFailure(request.RequestId, request.SuggestionId,
                "Ảnh Pexels đã chọn không còn vượt qua kiểm tra metadata.", false, "PexelsDirectValidation");

        var normalized = await NormalizePexelsImageAsync(downloaded.Bytes, request.EntityType, cancellationToken);
        if (normalized == null)
            return GenerateFailure(request.RequestId, request.SuggestionId,
                "Ảnh Pexels không thể chuẩn hóa theo giới hạn kỹ thuật của biểu mẫu.", false, "PexelsDirectValidation");
        if (!MatchesOrientation(normalized.Value.Width, normalized.Value.Height, request.VisualSpecification.Orientation))
            return GenerateFailure(request.RequestId, request.SuggestionId,
                "Ảnh Pexels không đúng orientation yêu cầu.", false, "PexelsDirectValidation");

        var attribution = string.IsNullOrWhiteSpace(downloaded.Photo.Photographer)
            ? "Photo provided by Pexels"
            : $"Photo by {downloaded.Photo.Photographer} on Pexels";
        var image = new AIGeneratedImageDTO
        {
            Base64Data = Convert.ToBase64String(normalized.Value.Bytes),
            ContentType = "image/jpeg",
            FileName = SanitizeFileName(request.FileNamePrefix, request.PhotoId),
            Width = normalized.Value.Width,
            Height = normalized.Value.Height,
            TechnicalValidationPassed = true,
            Source = "Pexels",
            ExternalPhotoId = request.PhotoId,
            AttributionText = attribution,
            Warnings = ["Ảnh được dùng trực tiếp từ Pexels; attribution chỉ tồn tại trong phiên biểu mẫu này."]
        };

        _logger.LogInformation(
            "Pexels direct image prepared. RequestId={RequestId} SuggestionId={SuggestionId} EntityType={EntityType} PhotoId={PhotoId} Score={Score} Bytes={Bytes} ElapsedMs={ElapsedMs}",
            request.RequestId, request.SuggestionId, request.EntityType, request.PhotoId, score.Score,
            normalized.Value.Bytes.Length, stopwatch.ElapsedMilliseconds);
        return new AIGenerateFromReferenceResultDTO
        {
            Success = true,
            RequestId = request.RequestId,
            SuggestionId = request.SuggestionId,
            Stage = "Completed",
            Message = "Ảnh Pexels đã được kiểm tra và chuẩn hóa. Bạn có thể áp dụng ảnh này vào form.",
            PexelsReference = Map(downloaded.Photo, score, matchedQuery),
            GeneratedImages = [image]
        };
    }

    public async Task<AIGenerateFromReferenceResultDTO> GenerateFromPromptAsync(
        AIGenerateFromPromptRequestDTO request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var validation = Validate(request.EntityType, request.RequestId, request.SuggestionId, request.VisualSpecification);
        if (validation != null)
            return GenerateFailure(request.RequestId, request.SuggestionId, validation, false, "ComfyUITextGeneration");
        if (!_options.AllowTextOnlyFallback)
            return GenerateFailure(request.RequestId, request.SuggestionId,
                "Fallback ComfyUI không dùng ảnh tham chiếu đang bị tắt.", false, "ComfyUITextGeneration");

        var generation = await _comfyUI.GenerateImageAsync(new ComfyUIImageRequestDTO
        {
            GenerationMode = ComfyUIGenerationMode.TextToImage,
            PositivePrompt = BuildDetailedPrompt(request.VisualSpecification),
            NegativePrompt = request.VisualSpecification.ComfyNegativePrompt,
            FileNamePrefix = request.FileNamePrefix,
            OutputCount = Math.Clamp(_options.GeneratedOutputCount, 2, 4),
            Width = Math.Clamp(_options.GeneratedWidth, 256, 2048),
            Height = Math.Clamp(_options.GeneratedHeight, 256, 2048),
            Denoise = 1.0
        }, cancellationToken);
        if (!generation.Success)
            return GenerateFailure(request.RequestId, request.SuggestionId,
                generation.ErrorMessage ?? "Không thể tạo ảnh bằng ComfyUI.", true, "ComfyUITextGeneration");

        var validImages = generation.Images.Select(x => ValidateOutput(x, request.VisualSpecification.Orientation))
            .Where(x => x.TechnicalValidationPassed).ToList();
        if (validImages.Count == 0)
            return GenerateFailure(request.RequestId, request.SuggestionId,
                "ComfyUI không tạo được output vượt qua kiểm tra kỹ thuật.", true, "ComfyUITextGeneration");

        _logger.LogInformation(
            "ComfyUI text generation completed. RequestId={RequestId} SuggestionId={SuggestionId} EntityType={EntityType} PromptId={PromptId} OutputCount={OutputCount} ElapsedMs={ElapsedMs}",
            request.RequestId, request.SuggestionId, request.EntityType, generation.PromptId,
            validImages.Count, stopwatch.ElapsedMilliseconds);
        return new AIGenerateFromReferenceResultDTO
        {
            Success = true,
            RequestId = request.RequestId,
            SuggestionId = request.SuggestionId,
            Stage = "Completed",
            Message = "Đã tạo ảnh từ Visual Specification. Vui lòng chọn một ảnh trước khi áp dụng.",
            GeneratedImages = validImages,
            Warnings = ["Ảnh không dùng Pexels reference và chưa được vision model kiểm tra ngữ nghĩa."]
        };
    }

    private AIGeneratedImageDTO ValidateOutput(ComfyUIImageOutputDTO output, string orientation)
    {
        var dto = new AIGeneratedImageDTO
        {
            ContentType = output.ContentType,
            FileName = output.FileName,
            Base64Data = Convert.ToBase64String(output.Bytes)
        };
        try
        {
            var info = Image.Identify(output.Bytes);
            dto.Width = info.Width;
            dto.Height = info.Height;
            if (!MatchesOrientation(info.Width, info.Height, orientation))
            {
                dto.Warnings.Add("Output không đúng hướng ảnh yêu cầu.");
                return dto;
            }
            if (info.Width < 256 || info.Height < 256)
            {
                dto.Warnings.Add("Output có kích thước quá thấp.");
                return dto;
            }
            dto.TechnicalValidationPassed = true;
            dto.Warnings.Add("Chưa kiểm tra ngữ nghĩa bằng vision model.");
            return dto;
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException)
        {
            dto.Warnings.Add("Output không phải file ảnh hợp lệ.");
            return dto;
        }
    }

    private static string BuildDetailedPrompt(VisualSpecificationDTO spec)
    {
        var parts = new List<string>
        {
            spec.ComfyPositivePrompt,
            $"primary subject: {spec.PrimarySubject}",
            $"product type: {spec.SubjectType}",
            spec.MainIngredients.Count > 0 ? $"visible ingredients: {string.Join(", ", spec.MainIngredients)}" : string.Empty,
            spec.SecondaryObjects.Count > 0 ? $"supporting objects: {string.Join(", ", spec.SecondaryObjects)}" : string.Empty,
            spec.DominantColors.Count > 0 ? $"dominant colors: {string.Join(", ", spec.DominantColors)}" : string.Empty,
            $"background: {spec.Background}",
            $"composition: {spec.Composition}",
            $"camera angle: {spec.CameraAngle}",
            $"lighting: {spec.Lighting}",
            $"style: {spec.ImageStyle}",
            $"style profile: {spec.StyleProfile}",
            $"mood: {spec.Mood}",
            $"container: {spec.Container}",
            $"surface: {spec.Surface}",
            spec.Garnishes.Count > 0 ? $"garnishes: {string.Join(", ", spec.Garnishes)}" : string.Empty,
            spec.Props.Count > 0 ? $"props: {string.Join(", ", spec.Props)}" : string.Empty,
            $"lens: {spec.Lens}",
            $"depth of field: {spec.DepthOfField}",
            $"reference purpose: {spec.ReferencePurpose}",
            $"orientation: {spec.Orientation}",
            "commercial cafe menu product photography, realistic texture, clean food styling, sharp focus"
        };
        var prompt = string.Join(", ", parts.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()));
        return prompt[..Math.Min(prompt.Length, 4000)];
    }

    private static string ResolveMatchedQuery(string? matchedQuery, VisualSpecificationDTO specification) =>
        string.IsNullOrWhiteSpace(matchedQuery)
            ? specification.PexelsQueries.FirstOrDefault() ?? specification.PrimarySubject
            : matchedQuery.Trim();

    private static async Task<(byte[] Bytes, int Width, int Height)?> NormalizePexelsImageAsync(
        byte[] bytes, string entityType, CancellationToken cancellationToken)
    {
        try
        {
            using var image = Image.Load(bytes);
            const int maximumDimension = 1400;
            if (image.Width > maximumDimension || image.Height > maximumDimension)
            {
                var scale = Math.Min(maximumDimension / (double)image.Width, maximumDimension / (double)image.Height);
                image.Mutate(x => x.Resize(Math.Max(1, (int)(image.Width * scale)), Math.Max(1, (int)(image.Height * scale))));
            }

            var maxBytes = entityType == "Topping" ? 3 * 1024 * 1024 : 5 * 1024 * 1024;
            foreach (var quality in new[] { 88, 80, 72, 64 })
            {
                await using var stream = new MemoryStream();
                await image.SaveAsJpegAsync(stream, new JpegEncoder { Quality = quality }, cancellationToken);
                if (stream.Length <= maxBytes)
                    return (stream.ToArray(), image.Width, image.Height);
                image.Mutate(x => x.Resize(Math.Max(256, (int)(image.Width * 0.85)), Math.Max(256, (int)(image.Height * 0.85))));
            }
            return null;
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException)
        {
            return null;
        }
    }

    private static string SanitizeFileName(string prefix, long photoId)
    {
        var clean = new string((prefix ?? string.Empty).Where(x => char.IsLetterOrDigit(x) || x is '-' or '_').ToArray());
        if (string.IsNullOrWhiteSpace(clean)) clean = "cafechain_pexels";
        return $"{clean[..Math.Min(clean.Length, 70)]}_{photoId}.jpg";
    }

    private AIImageEntityProfileOptions GetProfile(string entityType) =>
        _options.Entities.TryGetValue(entityType, out var profile) ? profile : new();

    private static string? Validate(string entityType, Guid requestId, Guid suggestionId, VisualSpecificationDTO spec)
    {
        if (requestId == Guid.Empty || suggestionId == Guid.Empty) return "RequestId hoặc SuggestionId không hợp lệ.";
        if (entityType is not ("Drink" or "Topping")) return "EntityType không được hỗ trợ.";
        if (string.IsNullOrWhiteSpace(spec.PrimarySubject) || string.IsNullOrWhiteSpace(spec.SubjectType))
            return "Visual Specification thiếu đối tượng chính.";
        if (string.IsNullOrWhiteSpace(spec.ComfyPositivePrompt) || string.IsNullOrWhiteSpace(spec.ComfyNegativePrompt))
            return "Visual Specification thiếu prompt ComfyUI.";
        return null;
    }

    private static PexelsImageCandidateDTO Map(PexelsPhotoDTO photo, PexelsCandidateScoreResult score, string query) => new()
    {
        PhotoId = photo.Id,
        PreviewUrl = photo.PreviewUrl ?? string.Empty,
        SourceUrl = photo.Url ?? string.Empty,
        Photographer = photo.Photographer,
        PhotographerUrl = photo.PhotographerUrl,
        Alt = photo.Alt,
        AverageColor = photo.AverageColor,
        Width = photo.Width,
        Height = photo.Height,
        Score = score.Score,
        MatchedQuery = query,
        Warnings = score.Warnings.ToList()
    };

    private static bool MatchesOrientation(int width, int height, string orientation)
    {
        var ratio = width / (double)height;
        return orientation.ToLowerInvariant() switch
        {
            "square" => ratio is >= 0.80 and <= 1.25,
            "portrait" => ratio < 0.90,
            "landscape" => ratio > 1.10,
            _ => true
        };
    }

    private static string BuildCacheKey(string entityType, VisualSpecificationDTO specification)
    {
        var raw = string.Join('|', entityType, specification.SubjectType, specification.PrimarySubject,
            string.Join(',', specification.MainIngredients.OrderBy(x => x)), specification.Orientation).ToLowerInvariant();
        return "ai-image:pexels:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }

    private void LogSearch(AIReferenceSearchRequestDTO request, int candidates, int queries, long elapsed, bool success) =>
        _logger.LogInformation(
            "Pexels pipeline completed. RequestId={RequestId} SuggestionId={SuggestionId} EntityType={EntityType} QueryCount={QueryCount} CandidateCount={CandidateCount} Success={Success} ElapsedMs={ElapsedMs}",
            request.RequestId, request.SuggestionId, request.EntityType, queries, candidates, success, elapsed);

    private static AIReferenceSearchResultDTO SearchSuccess(
        AIReferenceSearchRequestDTO request, List<PexelsImageCandidateDTO> candidates, string message) => new()
    {
        Success = true, RequestId = request.RequestId, SuggestionId = request.SuggestionId,
        Stage = "PexelsReferenceReady", Message = message, Candidates = candidates,
        Warnings = ["Điểm Pexels chỉ dựa trên metadata; cần người dùng xác nhận ảnh."]
    };

    private static AIReferenceSearchResultDTO SearchFailure(
        AIReferenceSearchRequestDTO request, string message, bool retryable, string failureCode, bool textFallbackAvailable) => new()
    {
        RequestId = request.RequestId, SuggestionId = request.SuggestionId,
        Stage = "PexelsValidation", Message = message, Retryable = retryable,
        FailureCode = failureCode, TextFallbackAvailable = textFallbackAvailable
    };

    private static AIGenerateFromReferenceResultDTO GenerateFailure(
        AIGenerateFromReferenceRequestDTO request, string message, bool retryable) => new()
    {
        RequestId = request.RequestId, SuggestionId = request.SuggestionId,
        Stage = "ComfyUIGeneration", Message = message, Retryable = retryable
    };

    private static AIGenerateFromReferenceResultDTO GenerateFailure(
        Guid requestId, Guid suggestionId, string message, bool retryable, string stage) => new()
    {
        RequestId = requestId, SuggestionId = suggestionId,
        Stage = stage, Message = message, Retryable = retryable
    };
}
