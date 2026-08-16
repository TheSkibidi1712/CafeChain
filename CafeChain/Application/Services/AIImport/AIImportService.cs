using System.Data;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.AIImport;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Categories;
using CafeChain.Application.DTOs.Admin.Drinks;
using CafeChain.Application.DTOs.Admin.Ingredients;
using CafeChain.Application.DTOs.Admin.Sizes;
using CafeChain.Application.DTOs.Admin.Suppliers;
using CafeChain.Application.Exceptions;
using CafeChain.Application.Interfaces.AIImport;
using CafeChain.Application.Interfaces.Admin.Categories;
using CafeChain.Application.Interfaces.Admin.Drinks;
using CafeChain.Application.Interfaces.Admin.Ingredients;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.Admin.Sizes;
using CafeChain.Application.Interfaces.Admin.Suppliers;
using CafeChain.Application.Interfaces.Systems;
using CafeChain.Application.Options;
using CafeChain.Data;
using CafeChain.Models.AIImport;
using CafeChain.Models.Enums.Drink;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using CafeChain.Infrastructure.Configurations;

namespace CafeChain.Application.Services.AIImport;

public sealed class AIImportService : IAIImportService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AppDbContext _db;
    private readonly IAIImportDocumentPipeline _pipeline;
    private readonly IAIImportRegionAnalyzer _analyzer;
    private readonly IAIImportSchemaRegistry _schemas;
    private readonly IRequestDeduplicationService _deduplication;
    private readonly IAdminPermissionService _permissions;
    private readonly IAdminSupplierService _suppliers;
    private readonly AIImportOptions _options;
    private readonly string _ollamaModel;
    private readonly ILogger<AIImportService> _logger;
    private readonly AIImportEntityCreator _entityCreator;
    private readonly AIImportPreviewValidator _previewValidator;
    private readonly AIImportEntityRegistry _entityRegistry;
    private readonly AIImportAnalysisCoordinator _analysisCoordinator;
    private readonly AIImportPreviewMutationCoordinator _mutationCoordinator;
    private readonly AIImportConfirmCoordinator _confirmCoordinator;
    private readonly AIImportSessionQuery _sessionQuery;
    private readonly IAIImportOcrRuntimeSettings? _ocrRuntimeSettings;

    public AIImportService(
        AppDbContext db,
        IAIImportDocumentPipeline pipeline,
        IAIImportRegionAnalyzer analyzer,
        IAIImportSchemaRegistry schemas,
        IRequestDeduplicationService deduplication,
        IAdminPermissionService permissions,
        IAdminSupplierService suppliers,
        IOptions<AIImportOptions> options,
        IOptions<OllamaOptions> ollamaOptions,
        ILogger<AIImportService> logger,
        AIImportEntityCreator entityCreator,
        AIImportPreviewValidator previewValidator,
        AIImportEntityRegistry entityRegistry,
        AIImportAnalysisCoordinator analysisCoordinator,
        AIImportPreviewMutationCoordinator mutationCoordinator,
        AIImportConfirmCoordinator confirmCoordinator,
        AIImportSessionQuery sessionQuery,
        IAIImportOcrRuntimeSettings? ocrRuntimeSettings = null)
    {
        _db = db;
        _pipeline = pipeline;
        _analyzer = analyzer;
        _schemas = schemas;
        _deduplication = deduplication;
        _permissions = permissions;
        _suppliers = suppliers;
        _options = options.Value;
        _ollamaModel = ollamaOptions.Value.Model;
        _logger = logger;
        _entityCreator = entityCreator;
        _previewValidator = previewValidator;
        _entityRegistry = entityRegistry;
        _analysisCoordinator = analysisCoordinator;
        _mutationCoordinator = mutationCoordinator;
        _confirmCoordinator = confirmCoordinator;
        _sessionQuery = sessionQuery;
        _ocrRuntimeSettings = ocrRuntimeSettings;
    }

    public async Task<AIImportOperationResult<AIImportSessionDto>> AnalyzeAsync(
        IReadOnlyList<IFormFile> files,
        AIImportEntityType? entityHint,
        bool useOcr,
        AdminActorContext actor,
        CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, PermissionConstants.AIImportUpload, PermissionConstants.AIImportAnalyze);
        if (access != null) return AIImportOperationResult<AIImportSessionDto>.Fail(403, "KHÔNG_CÓ_QUYỀN", access);
        if (files.Count == 0 || files.All(file => file.Length <= 0))
            return AIImportOperationResult<AIImportSessionDto>.Fail(400, "FILE_BẮT_BUỘC", "Vui lòng chọn ít nhất một tệp .xlsx, .docx hoặc .pdf.");
        if (files.Count > _options.MaxFilesPerSession)
            return AIImportOperationResult<AIImportSessionDto>.Fail(413, "VƯỢT_GIỚI_HẠN_SỐ_TỆP", $"Mỗi phiên chỉ nhận tối đa {_options.MaxFilesPerSession} tệp.");
        if (files.Sum(file => file.Length) > _options.MaxTotalUploadBytesPerSession)
            return AIImportOperationResult<AIImportSessionDto>.Fail(413, "VƯỢT_GIỚI_HẠN_PHIÊN_NHẬP", "Tổng dung lượng các tệp vượt giới hạn của một phiên.");
        foreach (var file in files)
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension == ".doc")
                return AIImportOperationResult<AIImportSessionDto>.Fail(400, "ĐỊNH_DẠNG_DOC_CŨ_KHÔNG_HỖ_TRỢ", $"Tệp {Path.GetFileName(file.FileName)} dùng định dạng .doc cũ; vui lòng chuyển sang .docx.");
            if (extension == ".docm" || !_options.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                return AIImportOperationResult<AIImportSessionDto>.Fail(400, "ĐỊNH_DẠNG_KHÔNG_HỖ_TRỢ", $"Tệp {Path.GetFileName(file.FileName)} không thuộc định dạng được hỗ trợ.");
            if (file.Length <= 0 || file.Length > _options.MaxFileBytes)
                return AIImportOperationResult<AIImportSessionDto>.Fail(413, "FILE_QUÁ_LỚN", $"Tệp {Path.GetFileName(file.FileName)} rỗng hoặc vượt giới hạn {_options.MaxFileBytes / 1024 / 1024} MB.");
        }
        var ocrState = _ocrRuntimeSettings == null
            ? LegacyOcrState()
            : await _ocrRuntimeSettings.GetAsync(cancellationToken);
        if (useOcr && !ocrState.EffectiveEnabled)
        {
            var code = ocrState.InfrastructureConfigured ? "PDF_OCR_KHÔNG_KHẢ_DỤNG" : "OCR_CHƯA_ĐƯỢC_CẤU_HÌNH";
            return AIImportOperationResult<AIImportSessionDto>.Fail(409, code,
                "Tesseract local chưa sẵn sàng. Hãy kiểm tra tại Cài đặt hệ thống.");
        }
        var preparedFiles = new List<PreparedUpload>();
        foreach (var file in files)
        {
            await using var stream = new MemoryStream();
            await file.CopyToAsync(stream, cancellationToken);
            preparedFiles.Add(new PreparedUpload(file, stream.ToArray()));
        }
        var analysisTimer = Stopwatch.StartNew();
        var sourceFormat = preparedFiles.Count == 1
            ? AIImportSourceFormats.FromFileName(preparedFiles[0].File.FileName) ?? string.Empty
            : "MULTI";
        var session = new ImportSession
        {
            FileName = preparedFiles.Count == 1 ? Path.GetFileName(preparedFiles[0].File.FileName) : $"{preparedFiles.Count} tài liệu nguồn",
            FileHash = Hash(string.Join('|', preparedFiles.Select(item => Convert.ToHexString(SHA256.HashData(item.Content))))),
            FileSize = preparedFiles.Sum(item => item.File.Length),
            SourceFormat = sourceFormat,
            RequestedOcr = useOcr,
            EffectiveOcr = useOcr && ocrState.EffectiveEnabled,
            OcrConfigVersion = ocrState.ConfigVersion,
            UploadedByStaffId = actor.StaffId,
            UploadedByAccountId = actor.AccountId,
            StoreId = 0,
            Status = AIImportSessionStatuses.Uploaded,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(_options.SessionLifetimeHours)
        };
        for (var index = 0; index < preparedFiles.Count; index++)
        {
            var prepared = preparedFiles[index];
            session.SourceDocuments.Add(new ImportSourceDocument
            {
                OriginalFileName = Path.GetFileName(prepared.File.FileName),
                FileHash = Convert.ToHexString(SHA256.HashData(prepared.Content)),
                FileSize = prepared.File.Length,
                SourceFormat = AIImportSourceFormats.FromFileName(prepared.File.FileName) ?? string.Empty,
                SortOrder = index,
                Status = AIImportSourceDocumentStatuses.Processing,
                CreatedAtUtc = DateTime.UtcNow
            });
        }
        _db.ImportSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);
        await TransitionAsync(session, AIImportSessionStatuses.Uploaded, AIImportSessionStatuses.Analyzing, actor, "ANALYZE_STARTED", cancellationToken);

        try
        {
            var sessionWarnings = new List<AIImportErrorDto>();
            var totalCharacters = 0;
            var totalAiChunks = 0;
            var totalOcrPages = 0;
            for (var index = 0; index < preparedFiles.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var prepared = preparedFiles[index];
                var sourceDocument = session.SourceDocuments.Single(source => source.SortOrder == index);
                AIImportSourceDocument document;
                try
                {
                    document = await _pipeline.AnalyzeAsync(new AIImportSourceFile(
                        prepared.File.FileName, prepared.Content, prepared.File.ContentType,
                        session.EffectiveOcr, ocrState), entityHint, cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    _logger.LogWarning(exception,
                        "AI Smart Import source analysis failed. SessionId={SessionId} SourceDocumentId={SourceDocumentId} Format={Format}",
                        session.ImportSessionId, sourceDocument.ImportSourceDocumentId, sourceDocument.SourceFormat);
                    sourceDocument.Status = AIImportSourceDocumentStatuses.Failed;
                    sourceDocument.ErrorCode = "PHÂN_TÍCH_TỆP_THẤT_BẠI";
                    sourceDocument.ErrorMessage = "Không thể phân tích tài liệu nguồn này.";
                    sessionWarnings.Add(WithSource(new AIImportErrorDto
                    {
                        Code = sourceDocument.ErrorCode,
                        Message = sourceDocument.ErrorMessage,
                        Severity = AIImportIssueSeverities.Error
                    }, sourceDocument));
                    continue;
                }
                NormalizeDocumentIssues(document);
                sourceDocument.SourceMetadataJson = Serialize(document.Metadata);
                sourceDocument.SourceSnapshotJson = Serialize(_analysisCoordinator.CaptureDocumentState(session, document));
                totalCharacters += document.ExtractedText.Length;
                totalAiChunks += document.AiChunkCount;
                totalOcrPages += document.OcrPageCount;
                if (document.Errors.Count > 0)
                {
                    sourceDocument.Status = AIImportSourceDocumentStatuses.Failed;
                    sourceDocument.ErrorCode = document.Errors[0].Code;
                    sourceDocument.ErrorMessage = document.Errors[0].Message;
                    sessionWarnings.AddRange(document.Errors.Select(error => WithSource(error, sourceDocument)));
                    continue;
                }
                sourceDocument.Status = AIImportSourceDocumentStatuses.Ready;
                sessionWarnings.AddRange(document.Warnings.Select(warning => WithSource(warning, sourceDocument)));
                if (document.UsedAI) session.ModelName = _ollamaModel;
                foreach (var sourceGroup in document.Groups)
                    session.Groups.Add(CreatePersistentGroup(sourceGroup, sourceDocument));
            }
            if (session.Groups.SelectMany(group => group.Items).Count() > _options.MaxTotalCandidatesPerSession
                || totalCharacters > _options.MaxTotalExtractedCharactersPerSession
                || totalAiChunks > _options.MaxTotalAIChunksPerSession
                || totalOcrPages > _options.MaxTotalOcrPagesPerSession)
                return await FailBatchLimitAsync(session, actor, cancellationToken);
            session.SourceMetadataJson = Serialize(new { sourceCount = session.SourceDocuments.Count, totalCharacters, totalAiChunks, totalOcrPages, requestedOcr = session.RequestedOcr, effectiveOcr = session.EffectiveOcr, ocrConfigVersion = session.OcrConfigVersion });
            session.AnalysisWarningsJson = Serialize(sessionWarnings);
            await TransitionAsync(session, AIImportSessionStatuses.Analyzing, AIImportSessionStatuses.Validating, actor, "PARSING_COMPLETED", cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            await ValidateSessionAsync(session, cancellationToken);
            session.PreviewVersion = 1;
            session.Status = AIImportSessionStatuses.ReadyToPreview;
            RefreshCounts(session);
            AddAudit(session, actor, "ANALYZE_COMPLETED", AIImportSessionStatuses.Validating, session.Status);
            await _db.SaveChangesAsync(cancellationToken);
            return AIImportOperationResult<AIImportSessionDto>.Ok(await BuildSessionDtoAsync(session.ImportSessionId, null, null, 1, _options.DefaultPageSize, actor, cancellationToken), $"Đã phân tích {preparedFiles.Count} tài liệu nguồn.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "AI Smart Import analyze failed. SessionId={SessionId}", session.ImportSessionId);
            await FailAsync(session, "PHÂN_TÍCH_THẤT_BẠI", "Không thể hoàn tất phân tích tài liệu.", actor, cancellationToken);
            return AIImportOperationResult<AIImportSessionDto>.Fail(500, "PHÂN_TÍCH_THẤT_BẠI", "Không thể hoàn tất phân tích tài liệu.");
        }
    }

    public async Task<AIImportOperationResult<AIImportSessionDto>> ReanalyzeAsync(
        int sessionId,
        AIImportReanalyzeRequest request,
        AdminActorContext actor,
        CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, PermissionConstants.AIImportAnalyze);
        if (access != null) return AIImportOperationResult<AIImportSessionDto>.Fail(403, "KHÔNG_CÓ_QUYỀN", access);
        var session = await OwnedSessionAsync(sessionId, actor, true, cancellationToken);
        if (session == null) return NotFound<AIImportSessionDto>();
        if (session.PreviewVersion != request.ExpectedPreviewVersion)
            return Conflict<AIImportSessionDto>("PREVIEW_ĐÃ_THAY_ĐỔI", "Bản xem trước đã thay đổi; vui lòng tải lại.");
        if (session.Status is not AIImportSessionStatuses.ReadyToPreview and not AIImportSessionStatuses.Failed)
            return Conflict<AIImportSessionDto>("PHIÊN_ĐÃ_XỬ_LÝ", "Phiên không thể phân tích lại.");
        var requiresDocumentSnapshot = session.SourceFormat is AIImportSourceFormats.Docx or AIImportSourceFormats.Pdf
                                       && session.Groups.Any(group => group.EntityType == AIImportEntityType.Unknown
                                                                      || group.Status == AIImportItemStatuses.ReviewRequired);
        var sourceSnapshot = requiresDocumentSnapshot
            ? Deserialize<AIImportSourceSnapshot>(session.SourceSnapshotJson ?? string.Empty)
            : null;
        if (requiresDocumentSnapshot && sourceSnapshot == null)
            return AIImportOperationResult<AIImportSessionDto>.Fail(409, "CẦN_TẢI_LẠI_TỆP_NGUỒN",
                "Snapshot nguồn đã bị xóa; cần tải lại tệp để OCR/phân tích lại.");

        var statusBeforeClaim = _analysisCoordinator.ClaimReanalysis(session);
        AddAudit(session, actor, "REANALYZE_CLAIMED", statusBeforeClaim, session.Status);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict<AIImportSessionDto>("PREVIEW_ĐÃ_THAY_ĐỔI", "Bản xem trước đã thay đổi; vui lòng tải lại.");
        }

        try
        {
        if (session.SourceFormat is AIImportSourceFormats.Docx or AIImportSourceFormats.Pdf
            && session.Groups.Any(group => group.EntityType == AIImportEntityType.Unknown || group.Status == AIImportItemStatuses.ReviewRequired)
            && sourceSnapshot is { } snapshot)
        {
            var reanalyzed = await _pipeline.ReanalyzeAsync(snapshot, null, cancellationToken);
            NormalizeDocumentIssues(reanalyzed);
            session.AnalysisWarningsJson = Serialize(reanalyzed.Warnings);
            if (reanalyzed.Errors.Count == 0 && reanalyzed.Groups.Count > 0)
            {
                var staleGroups = session.Groups
                    .Where(group => group.EntityType == AIImportEntityType.Unknown || group.Status == AIImportItemStatuses.ReviewRequired)
                    .ToList();
                _db.ImportGroups.RemoveRange(staleGroups);
                foreach (var staleGroup in staleGroups) session.Groups.Remove(staleGroup);
                foreach (var sourceGroup in reanalyzed.Groups) session.Groups.Add(CreatePersistentGroup(sourceGroup));
                session.ModelName = _ollamaModel;
                var metadata = Deserialize<Dictionary<string, object?>>(session.SourceMetadataJson) ?? new();
                metadata["aiChunkCount"] = reanalyzed.AiChunkCount;
                session.SourceMetadataJson = Serialize(metadata);
            }
        }

        foreach (var group in session.Groups)
        {
            var mapping = Deserialize<Dictionary<string, string?>>(group.MappingJson) ?? new(StringComparer.OrdinalIgnoreCase);
            if (session.SourceFormat == AIImportSourceFormats.Xlsx
                && (group.EntityType == AIImportEntityType.Unknown || group.Status == AIImportItemStatuses.ReviewRequired))
            {
                var region = RestoreRegion(group);
                var analysis = await _analyzer.AnalyzeAsync(region, null, cancellationToken);
                group.EntityType = analysis.EntityType;
                group.MappingJson = Serialize(analysis.Mapping);
                group.Confidence = analysis.Confidence;
                group.DependencyOrder = _entityRegistry.Find(analysis.EntityType)?.DependencyOrder ?? 30;
                group.Status = analysis.EntityType == AIImportEntityType.Unknown
                    ? AIImportItemStatuses.ReviewRequired
                    : AIImportItemStatuses.Valid;
                mapping = analysis.Mapping;
                if (analysis.UsedAI) session.ModelName = _ollamaModel;
            }
            foreach (var item in group.Items)
            {
                ResetManualReview(item);
                var raw = Deserialize<Dictionary<string, string?>>(item.RawDataJson) ?? new();
                var mapped = ApplyMapping(raw, mapping);
                ApplyValidation(item, group.EntityType, mapped, group.Confidence, null);
            }
        }
        await ValidateSessionAsync(session, cancellationToken);
        session.AnalysisVersion++;
        session.PreviewVersion++;
        session.Status = AIImportSessionStatuses.ReadyToPreview;
        session.FailureCode = null;
        session.FailureMessage = null;
        RefreshCounts(session);
        AddAudit(session, actor, "REANALYZE", AIImportSessionStatuses.Analyzing, session.Status);
        await _db.SaveChangesAsync(cancellationToken);
        return AIImportOperationResult<AIImportSessionDto>.Ok(await BuildSessionDtoAsync(sessionId, null, null, 1, _options.DefaultPageSize, actor, cancellationToken));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "AI Smart Import reanalyze failed after claim. SessionId={SessionId}", sessionId);
            await FailAsync(session, "PHÂN_TÍCH_LẠI_THẤT_BẠI",
                "Không thể hoàn tất phân tích lại; phiên đã chuyển sang trạng thái lỗi.", actor, cancellationToken);
            return AIImportOperationResult<AIImportSessionDto>.Fail(500, "PHÂN_TÍCH_LẠI_THẤT_BẠI",
                "Không thể hoàn tất phân tích lại; phiên đã chuyển sang trạng thái lỗi.");
        }
    }

    public async Task<AIImportOperationResult<AIImportSessionDto>> GetSessionAsync(
        int sessionId, int? groupId, string? status, int page, int pageSize, AdminActorContext actor, CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, PermissionConstants.AIImportView);
        if (access != null) return AIImportOperationResult<AIImportSessionDto>.Fail(403, "KHÔNG_CÓ_QUYỀN", access);
        var exists = await OwnedSessionAsync(sessionId, actor, false, cancellationToken);
        if (exists == null) return NotFound<AIImportSessionDto>();
        await ExpireIfNeededAsync(exists, actor, cancellationToken);
        return AIImportOperationResult<AIImportSessionDto>.Ok(await BuildSessionDtoAsync(sessionId, groupId, status, page, pageSize, actor, cancellationToken));
    }

    public async Task<AIImportOperationResult<AIImportEditorOptionsDto>> GetEditorOptionsAsync(
        int sessionId, AdminActorContext actor, CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, PermissionConstants.AIImportView);
        if (access != null) return AIImportOperationResult<AIImportEditorOptionsDto>.Fail(403, "KHÔNG_CÓ_QUYỀN", access);

        var session = await _db.ImportSessions.AsNoTracking()
            .Include(x => x.Groups).ThenInclude(x => x.Items)
            .SingleOrDefaultAsync(x => x.ImportSessionId == sessionId && x.UploadedByAccountId == actor.AccountId, cancellationToken);
        if (session == null) return NotFound<AIImportEditorOptionsDto>();

        var categories = await _db.DrinkCategories.AsNoTracking().Where(x => x.Active)
            .OrderBy(x => x.Name).Select(x => new AIImportEditorOptionDto
            {
                Value = x.CategoryCode,
                Label = x.CategoryCode + " · " + x.Name
            }).ToListAsync(cancellationToken);
        var pendingCategories = session.Groups.Where(x => x.EntityType == AIImportEntityType.Category)
            .SelectMany(x => x.Items)
            .Where(x => x.Action == AIImportActions.Create && x.Status is AIImportItemStatuses.Valid or AIImportItemStatuses.Warning)
            .Select(x => Deserialize<Dictionary<string, string?>>(x.NormalizedDataJson) ?? new())
            .Where(x => !string.IsNullOrWhiteSpace(x.GetValueOrDefault("CategoryCode")))
            .Select(x => new AIImportEditorOptionDto
            {
                Value = x.GetValueOrDefault("CategoryCode")!,
                Label = $"{x.GetValueOrDefault("CategoryCode")} · {x.GetValueOrDefault("Name")} (trong phiên)",
                FromCurrentSession = true
            });
        categories.AddRange(pendingCategories);

        var result = new AIImportEditorOptionsDto
        {
            SessionId = sessionId,
            PreviewVersion = session.PreviewVersion,
            Categories = categories.GroupBy(x => x.Value, StringComparer.OrdinalIgnoreCase).Select(x => x.First()).ToList(),
            ProductTypes = await _db.ProductTypes.AsNoTracking().Where(x => x.Active).OrderBy(x => x.Name)
                .Select(x => new AIImportEditorOptionDto { Value = x.Code, Label = x.Code + " · " + x.Name }).ToListAsync(cancellationToken),
            Units = await _db.Units.AsNoTracking().Where(x => x.Active).OrderBy(x => x.Name)
                .Select(x => new AIImportEditorOptionDto { Value = x.UnitCode, Label = x.UnitCode + " · " + x.Name }).ToListAsync(cancellationToken)
        };
        return AIImportOperationResult<AIImportEditorOptionsDto>.Ok(result);
    }

    public async Task<AIImportOperationResult<AIImportSessionDto>> UpdateGroupAsync(
        int sessionId, int groupId, AIImportGroupPatchRequest request, AdminActorContext actor, CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, PermissionConstants.AIImportAnalyze);
        if (access != null) return AIImportOperationResult<AIImportSessionDto>.Fail(403, "KHÔNG_CÓ_QUYỀN", access);
        if (!_schemas.SupportedEntities.Contains(request.EntityType) || !_schemas.IsAllowedMapping(request.EntityType, request.Mapping))
            return AIImportOperationResult<AIImportSessionDto>.Fail(400, "MAPPING_KHÔNG_HỢP_LỆ", "Ánh xạ chứa loại dữ liệu hoặc trường không hợp lệ, hoặc một cột nguồn được dùng nhiều lần.");
        var session = await OwnedSessionAsync(sessionId, actor, true, cancellationToken);
        if (session == null) return NotFound<AIImportSessionDto>();
        var editable = CheckEditable<AIImportSessionDto>(session, request.ExpectedPreviewVersion);
        if (editable != null) return editable;
        var group = session.Groups.SingleOrDefault(x => x.ImportGroupId == groupId);
        if (group == null) return NotFound<AIImportSessionDto>();
        var previousEntityType = group.EntityType;
        group.EntityType = request.EntityType;
        group.MappingJson = Serialize(request.Mapping);
        var sourceColumns = _schemas.ClassifyColumns(request.EntityType,
            Deserialize<List<AIImportSourceColumn>>(group.SourceColumnsJson) ?? [], request.Mapping);
        group.SourceColumnsJson = Serialize(sourceColumns);
        var groupIssues = (Deserialize<List<AIImportErrorDto>>(group.IssuesJson) ?? [])
            .Where(issue => !IsResolvedMappingConflict(issue, request.Mapping)).ToList();
        group.IssuesJson = Serialize(groupIssues);
        group.DependencyOrder = _entityRegistry.Get(request.EntityType).DependencyOrder;
        group.Status = AIImportItemStatuses.Valid;
        foreach (var item in group.Items)
        {
            ResetManualReview(item);
            var raw = Deserialize<Dictionary<string, string?>>(item.RawDataJson) ?? new();
            var parserIssues = (Deserialize<List<AIImportErrorDto>>(item.SourceIssuesJson) ?? [])
                .Where(issue => issue.Code is not ("XUNG_ĐỘT_ÁNH_XẠ" or "CỘT_CẤM" or "CỘT_KHÔNG_XÁC_ĐỊNH"))
                .Concat(groupIssues).Concat(BuildColumnIssues(sourceColumns, raw)).ToList();
            item.SourceIssuesJson = Serialize(parserIssues);
            ApplyValidation(item, group.EntityType, ApplyMapping(raw, request.Mapping), group.Confidence, null,
                sourceIssues: parserIssues);
        }
        await ValidateSessionAsync(session, cancellationToken,
            _mutationCoordinator.GroupScope(groupId, previousEntityType));
        _mutationCoordinator.AdvancePreview(session);
        RefreshCounts(session);
        AddAudit(session, actor, "GROUP_UPDATED", session.Status, session.Status, group.EntityType);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict<AIImportSessionDto>("PREVIEW_ĐÃ_THAY_ĐỔI", "Bản xem trước đã được một yêu cầu khác cập nhật; vui lòng tải lại.");
        }
        return AIImportOperationResult<AIImportSessionDto>.Ok(await BuildSessionDtoAsync(sessionId, groupId, null, 1, _options.DefaultPageSize, actor, cancellationToken));
    }

    public async Task<AIImportOperationResult<AIImportSessionDto>> UpdateItemAsync(
        int sessionId, int itemId, AIImportItemPatchRequest request, AdminActorContext actor, CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, PermissionConstants.AIImportAnalyze);
        if (access != null) return AIImportOperationResult<AIImportSessionDto>.Fail(403, "KHÔNG_CÓ_QUYỀN", access);
        if (request.Action is not (AIImportActions.Create or AIImportActions.Skip))
            return AIImportOperationResult<AIImportSessionDto>.Fail(400, "ACTION_KHÔNG_HỢP_LỆ", "Chỉ hỗ trợ tạo mới hoặc bỏ qua bản ghi.");
        var session = await OwnedSessionAsync(sessionId, actor, true, cancellationToken);
        if (session == null) return NotFound<AIImportSessionDto>();
        var editable = CheckEditable<AIImportSessionDto>(session, request.ExpectedPreviewVersion);
        if (editable != null) return editable;
        var item = session.Groups.SelectMany(x => x.Items).SingleOrDefault(x => x.ImportItemId == itemId);
        if (item == null) return NotFound<AIImportSessionDto>();
        var previousBusinessKey = AIImportBusinessKeys.Create(item.Group.EntityType,
            Deserialize<Dictionary<string, string?>>(item.NormalizedDataJson) ?? new());
        AdminSupplierDuplicateWarningDTO? supplierWarning = null;
        if (request.Action == AIImportActions.Create
            && item.Group.EntityType == AIImportEntityType.Supplier
            && !string.IsNullOrWhiteSpace(request.DuplicateOverrideReason))
        {
            var normalized = _schemas.Normalize(AIImportEntityType.Supplier, request.Values);
            supplierWarning = await _suppliers.PrepareDuplicateWarningAsync(SupplierDto(normalized), actor.StaffId);
        }
        item.Action = request.Action;
        item.DuplicateOverrideReason = Clean(request.DuplicateOverrideReason);
        item.SupplierDuplicateWarningId = null;
        if (request.Action == AIImportActions.Skip)
        {
            ResetManualReview(item);
            item.Status = AIImportItemStatuses.Skipped;
            item.ErrorsJson = "[]";
            item.WarningsJson = "[]";
        }
        else
        {
            ApplyValidation(item, item.Group.EntityType, request.Values, item.Confidence, null,
                request.ManualReviewConfirmed,
                Deserialize<List<AIImportErrorDto>>(item.SourceIssuesJson) ?? []);
            if (request.ManualReviewConfirmed)
            {
                item.ManualReviewConfirmed = true;
                item.ManualReviewConfirmedAtUtc = DateTime.UtcNow;
                item.ManualReviewConfirmedByAccountId = actor.AccountId;
                item.ManualReviewPayloadHash = AIImportValidationContract.PayloadHash(item.NormalizedDataJson);
            }
            else ResetManualReview(item);
            item.WarningsAcknowledged = request.WarningsAcknowledged;
            if (supplierWarning != null)
            {
                item.SupplierDuplicateWarningId = supplierWarning.WarningId;
                item.WarningsAcknowledged = true;
                var warnings = Deserialize<List<AIImportErrorDto>>(item.WarningsJson) ?? new();
                warnings.Add(AIImportValidationContract.Issue("NHÀ_CUNG_CẤP_GẦN_TRÙNG",
                    $"Tìm thấy {supplierWarning.Matches.Count} nhà cung cấp tương tự; lý do override đã được ghi nhận.",
                    AIImportIssueSeverities.Warning, resolution: AIImportIssueResolutions.Acknowledge));
                warnings.AddRange(supplierWarning.Matches.Select(match => new AIImportErrorDto
                {
                    Code = "NHÀ_CUNG_CẤP_TƯƠNG_TỰ",
                    Message = $"{match.Code} · {match.Name} · {string.Join(", ", match.MatchedSignals)}",
                    Severity = AIImportIssueSeverities.Warning
                }));
                item.WarningsJson = Serialize(warnings);
                item.Status = AIImportValidationContract.ResolveStatus(item.Status, item.Action,
                    AllIssues(item), item.ManualReviewConfirmed);
            }
        }
        await ValidateSessionAsync(session, cancellationToken,
            _mutationCoordinator.ItemScope(itemId, item.Group.EntityType, previousBusinessKey));
        if (request.Action == AIImportActions.Create && request.ManualReviewConfirmed)
            item.ManualReviewPayloadHash = AIImportValidationContract.PayloadHash(item.NormalizedDataJson);
        _mutationCoordinator.AdvancePreview(session);
        RefreshCounts(session);
        AddAudit(session, actor, "ITEM_UPDATED", session.Status, session.Status, item.Group.EntityType);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict<AIImportSessionDto>("PREVIEW_ĐÃ_THAY_ĐỔI", "Bản xem trước đã được một yêu cầu khác cập nhật; vui lòng tải lại.");
        }
        return AIImportOperationResult<AIImportSessionDto>.Ok(await BuildSessionDtoAsync(sessionId, item.ImportGroupId, null, 1, _options.DefaultPageSize, actor, cancellationToken));
    }

    public async Task<AIImportOperationResult<AIImportConfirmResultDto>> ConfirmAsync(
        int sessionId, string? idempotencyKey, AIImportConfirmRequest request, AdminActorContext actor, CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, PermissionConstants.AIImportConfirm);
        if (access != null) return AIImportOperationResult<AIImportConfirmResultDto>.Fail(403, "KHÔNG_CÓ_QUYỀN", access);
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return AIImportOperationResult<AIImportConfirmResultDto>.Fail(400, "IDEMPOTENCY_KEY_BẮT_BUỘC", "Thiếu khóa chống gửi lặp cho yêu cầu xác nhận.");

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var session = await OwnedSessionAsync(sessionId, actor, true, cancellationToken);
            if (session == null) return NotFound<AIImportConfirmResultDto>();

            if (session.Status == AIImportSessionStatuses.Completed)
            {
                var completedSnapshot = ConfirmSnapshot(session);
                var completedBegin = await _deduplication.BeginScopedAsync(
                    idempotencyKey, "AIImport.Confirm", actor.StaffId,
                    new { sessionId, request.ExpectedPreviewVersion, snapshot = completedSnapshot },
                    sessionId, 0, actor.AccountId);
                await transaction.RollbackAsync(cancellationToken);
                if (!completedBegin.CanProcess
                    && string.Equals(completedBegin.Status, "SUCCESS", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(completedBegin.ResponseBody))
                {
                    var replay = Deserialize<AIImportConfirmResultDto>(completedBegin.ResponseBody);
                    if (replay != null) return AIImportOperationResult<AIImportConfirmResultDto>.Ok(replay, "Trả lại kết quả Confirm trước đó.");
                }
                return Conflict<AIImportConfirmResultDto>("PHIÊN_ĐÃ_XỬ_LÝ", "Phiên đã được Confirm bằng yêu cầu khác.");
            }
            if (session.Status != AIImportSessionStatuses.ReadyToPreview)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Conflict<AIImportConfirmResultDto>("PHIÊN_KHÔNG_THỂ_CONFIRM", "Phiên không ở trạng thái sẵn sàng để Confirm.");
            }
            if (session.PreviewVersion != request.ExpectedPreviewVersion)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Conflict<AIImportConfirmResultDto>("PREVIEW_ĐÃ_THAY_ĐỔI", "Bản xem trước đã thay đổi; vui lòng tải lại.");
            }
            var failedSources = session.SourceDocuments
                .Where(source => source.Status is AIImportSourceDocumentStatuses.Failed or AIImportSourceDocumentStatuses.Processing)
                .ToList();
            if (failedSources.Count > 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return AIImportOperationResult<AIImportConfirmResultDto>.Fail(422, "NGUỒN_CHƯA_SẴN_SÀNG",
                    "Còn tài liệu nguồn lỗi hoặc đang xử lý. Hãy loại bỏ nguồn lỗi hoặc phân tích lại trước khi Confirm.",
                    failedSources.Select(source => new AIImportErrorDto
                    {
                        Code = source.ErrorCode ?? "NGUỒN_CHƯA_SẴN_SÀNG",
                        Message = $"{source.OriginalFileName}: {source.ErrorMessage ?? source.Status}",
                        Severity = AIImportIssueSeverities.Error,
                        Metadata = new Dictionary<string, object?> { ["sourceDocumentId"] = source.ImportSourceDocumentId, ["fileName"] = source.OriginalFileName }
                    }));
            }

            var validationBefore = ValidationFingerprint(session);
            await ValidateSessionAsync(session, cancellationToken);
            var validationAfter = ValidationFingerprint(session);
            if (!string.Equals(validationBefore, validationAfter, StringComparison.Ordinal))
            {
                session.PreviewVersion++;
                RefreshCounts(session);
                AddAudit(session, actor, "CONFIRM_PREFLIGHT_UPDATED", session.Status, session.Status);
                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return AIImportOperationResult<AIImportConfirmResultDto>.Fail(
                    409, "PREVIEW_ĐÃ_THAY_ĐỔI", "Dữ liệu vừa được kiểm tra lại; vui lòng xem các dòng được cập nhật trước khi Confirm.",
                    BuildBlockerDetails(session));
            }

            var blockers = _confirmCoordinator.FindBlockers(session);
            if (blockers.Count > 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return AIImportOperationResult<AIImportConfirmResultDto>.Fail(
                    422, "PREVIEW_CHƯA_SẴN_SÀNG", "Còn lỗi, dòng cần xem lại hoặc cảnh báo chưa xác nhận.",
                    BuildBlockerDetails(session));
            }

            var snapshot = ConfirmSnapshot(session);
            var requiredCreatePermissions = _confirmCoordinator.RequiredCreatePermissions(session);
            DetachSessionGraph(session);
            var begin = await _deduplication.BeginScopedAsync(
                idempotencyKey,
                "AIImport.Confirm",
                actor.StaffId,
                new { sessionId, request.ExpectedPreviewVersion, snapshot },
                sessionId,
                0,
                actor.AccountId);
            if (!begin.CanProcess)
            {
                await transaction.RollbackAsync(cancellationToken);
                if (string.Equals(begin.Status, "SUCCESS", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(begin.ResponseBody))
                {
                    var replay = Deserialize<AIImportConfirmResultDto>(begin.ResponseBody);
                    if (replay != null) return AIImportOperationResult<AIImportConfirmResultDto>.Ok(replay, "Trả lại kết quả Confirm trước đó.");
                }
                return AIImportOperationResult<AIImportConfirmResultDto>.Fail(409, begin.ErrorCode ?? "YÊU_CẦU_ĐANG_XỬ_LÝ", begin.ErrorMessage ?? "Yêu cầu đang được xử lý.");
            }
            foreach (var entityPermission in requiredCreatePermissions)
            {
                if (await RequireAsync(actor, entityPermission) != null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return AIImportOperationResult<AIImportConfirmResultDto>.Fail(403, "KHÔNG_CÓ_QUYỀN_TẠO_ENTITY", $"Tài khoản không có quyền {entityPermission}.");
                }
            }

            var claimed = await _db.ImportSessions.Where(x => x.ImportSessionId == sessionId && x.Status == AIImportSessionStatuses.ReadyToPreview && x.PreviewVersion == request.ExpectedPreviewVersion)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, AIImportSessionStatuses.Importing).SetProperty(x => x.ConfirmedAtUtc, DateTime.UtcNow), cancellationToken);
            if (claimed != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Conflict<AIImportConfirmResultDto>("PHIÊN_ĐANG_ĐƯỢC_XỬ_LÝ", "Phiên đã được một yêu cầu khác nhận xử lý.");
            }

            session = await OwnedSessionAsync(sessionId, actor, true, cancellationToken)
                ?? throw new InvalidOperationException("Không thể tải lại phiên sau khi giữ quyền xử lý.");
            var result = new AIImportConfirmResultDto { SessionId = sessionId, Status = AIImportSessionStatuses.Completed };
            foreach (var entry in _confirmCoordinator.BuildExecutionPlan(session))
            {
                var group = entry.Group;
                var item = entry.Item;
                if (item.Action == AIImportActions.Skip) { result.Skipped++; continue; }
                var values = Deserialize<Dictionary<string, string?>>(item.NormalizedDataJson) ?? new();
                try
                {
                    item.ImportedEntityId = await _entityCreator.CreateAsync(group.EntityType, values, item, actor, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    throw new AIImportRowException(group, item, ex);
                }
                item.Status = AIImportItemStatuses.Imported;
                result.Imported++;
                result.ImportedByEntity[group.EntityType.ToString()] = result.ImportedByEntity.GetValueOrDefault(group.EntityType.ToString()) + 1;
            }
            _confirmCoordinator.Complete(session, DateTime.UtcNow, Serialize(result));
            foreach (var source in session.SourceDocuments) source.SourceSnapshotJson = null;
            RefreshCounts(session);
            AddAudit(session, actor, "CONFIRM_COMPLETED", AIImportSessionStatuses.Importing, session.Status, null, Hash(idempotencyKey), Serialize(result));
            await _db.SaveChangesAsync(cancellationToken);
            await _deduplication.MarkSuccessAsync(begin.Entry!, sessionId, result);
            await transaction.CommitAsync(cancellationToken);
            return AIImportOperationResult<AIImportConfirmResultDto>.Ok(result, "Đã nhập dữ liệu nguyên tử cho toàn phiên.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogWarning(ex, "AI Smart Import confirm rolled back. SessionId={SessionId}", sessionId);
            var cause = ex is AIImportRowException rowException ? rowException.InnerException ?? ex : ex;
            var details = ex is AIImportRowException row
                ? new[] { RowError(row.Group, row.Item, BusinessErrorCode(cause), BusinessMessage(cause)) }
                : null;
            return AIImportOperationResult<AIImportConfirmResultDto>.Fail(409, BusinessErrorCode(cause), BusinessMessage(cause), details);
        }
    }

    public async Task<AIImportOperationResult<AIImportSessionDto>> CancelAsync(
        int sessionId, AIImportCancelRequest request, AdminActorContext actor, CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, PermissionConstants.AIImportCancel);
        if (access != null) return AIImportOperationResult<AIImportSessionDto>.Fail(403, "KHÔNG_CÓ_QUYỀN", access);
        var affected = await _db.ImportSessions.Where(x => x.ImportSessionId == sessionId && x.UploadedByAccountId == actor.AccountId
                && x.PreviewVersion == request.ExpectedPreviewVersion
                && (x.Status == AIImportSessionStatuses.ReadyToPreview || x.Status == AIImportSessionStatuses.Failed))
            .ExecuteUpdateAsync(x => x.SetProperty(s => s.Status, AIImportSessionStatuses.Cancelled)
                .SetProperty(s => s.SourceSnapshotJson, (string?)null), cancellationToken);
        if (affected != 1)
        {
            var current = await OwnedSessionAsync(sessionId, actor, false, cancellationToken);
            if (current == null) return NotFound<AIImportSessionDto>();
            if (current.Status == AIImportSessionStatuses.Cancelled)
                return AIImportOperationResult<AIImportSessionDto>.Ok(
                    await BuildSessionDtoAsync(sessionId, null, null, 1, _options.DefaultPageSize, actor, cancellationToken),
                    "Phiên đã được hủy trước đó.");
            if (current.PreviewVersion != request.ExpectedPreviewVersion)
                return Conflict<AIImportSessionDto>("PREVIEW_ĐÃ_THAY_ĐỔI", "Bản xem trước đã thay đổi; vui lòng tải lại trạng thái phiên.");
            return Conflict<AIImportSessionDto>("PHIÊN_ĐÃ_XỬ_LÝ", $"Không thể hủy phiên ở trạng thái {current.Status}.");
        }
        await _db.ImportSourceDocuments.Where(source => source.ImportSessionId == sessionId)
            .ExecuteUpdateAsync(update => update.SetProperty(source => source.SourceSnapshotJson, (string?)null), cancellationToken);
        var session = await OwnedSessionAsync(sessionId, actor, false, cancellationToken);
        var audit = new ImportAudit
        {
            ImportSessionId = sessionId, StaffId = actor.StaffId, AccountId = actor.AccountId,
            Action = "CANCEL", StatusAfter = AIImportSessionStatuses.Cancelled,
            PromptVersion = session!.PromptVersion, SchemaVersion = session.SchemaVersion,
            ExtractionVersion = session.ExtractionVersion, PreviewVersion = session.PreviewVersion,
            SourceFormat = session.SourceFormat, CreatedAtUtc = DateTime.UtcNow
        };
        _db.ImportAudits.Add(audit);
        await _db.SaveChangesAsync(cancellationToken);
        return AIImportOperationResult<AIImportSessionDto>.Ok(await BuildSessionDtoAsync(sessionId, null, null, 1, _options.DefaultPageSize, actor, cancellationToken));
    }

    public async Task<AIImportOperationResult<AIImportSessionDto>> RemoveSourceAsync(
        int sessionId,
        int sourceDocumentId,
        int expectedPreviewVersion,
        AdminActorContext actor,
        CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, PermissionConstants.AIImportAnalyze);
        if (access != null) return AIImportOperationResult<AIImportSessionDto>.Fail(403, "KHÔNG_CÓ_QUYỀN", access);
        var session = await OwnedSessionAsync(sessionId, actor, true, cancellationToken);
        if (session == null) return NotFound<AIImportSessionDto>();
        var editable = CheckEditable<AIImportSessionDto>(session, expectedPreviewVersion);
        if (editable != null) return editable;
        var source = session.SourceDocuments.SingleOrDefault(item => item.ImportSourceDocumentId == sourceDocumentId);
        if (source == null || source.Status == AIImportSourceDocumentStatuses.Removed) return NotFound<AIImportSessionDto>();
        var groups = session.Groups.Where(group => group.ImportSourceDocumentId == sourceDocumentId).ToList();
        _db.ImportGroups.RemoveRange(groups);
        foreach (var group in groups) session.Groups.Remove(group);
        source.Status = AIImportSourceDocumentStatuses.Removed;
        source.SourceSnapshotJson = null;
        source.ErrorCode = null;
        source.ErrorMessage = null;
        await ValidateSessionAsync(session, cancellationToken);
        _mutationCoordinator.AdvancePreview(session);
        RefreshCounts(session);
        AddAudit(session, actor, "SOURCE_REMOVED", session.Status, session.Status);
        await _db.SaveChangesAsync(cancellationToken);
        return AIImportOperationResult<AIImportSessionDto>.Ok(
            await BuildSessionDtoAsync(sessionId, null, null, 1, _options.DefaultPageSize, actor, cancellationToken),
            "Đã loại tài liệu nguồn khỏi phiên.");
    }

    public async Task<AIImportOperationResult<AIImportOcrCapabilityDto>> GetOcrCapabilityAsync(
        AdminActorContext actor,
        CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, PermissionConstants.AIImportView);
        if (access != null) return AIImportOperationResult<AIImportOcrCapabilityDto>.Fail(403, "KHÔNG_CÓ_QUYỀN", access);
        var state = _ocrRuntimeSettings == null ? LegacyOcrState() : await _ocrRuntimeSettings.GetAsync(cancellationToken);
        return AIImportOperationResult<AIImportOcrCapabilityDto>.Ok(new AIImportOcrCapabilityDto
        {
            InfrastructureConfigured = state.InfrastructureConfigured,
            ProviderReady = state.ProviderReady,
            EffectiveEnabled = state.EffectiveEnabled,
            Provider = state.Provider,
            ProviderVersion = state.ProviderVersion,
            Languages = state.Languages,
            HealthStatus = state.HealthStatus,
            HealthMessage = state.HealthMessage,
            LastHealthCheckedAtUtc = state.LastHealthCheckedAtUtc
        });
    }

    public async Task<AIImportOperationResult<AIImportHistoryDto>> GetHistoryAsync(
        int page, int pageSize, AdminActorContext actor, CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, PermissionConstants.AIImportHistory);
        if (access != null) return AIImportOperationResult<AIImportHistoryDto>.Fail(403, "KHÔNG_CÓ_QUYỀN", access);
        (page, pageSize) = _sessionQuery.NormalizePage(page, pageSize, _options.DefaultPageSize, _options.MaximumPageSize);
        var query = _db.ImportSessions.AsNoTracking().Where(x => x.UploadedByAccountId == actor.AccountId).OrderByDescending(x => x.CreatedAtUtc);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => new AIImportHistoryItemDto
        {
            SessionId = x.ImportSessionId, FileName = x.FileName, SourceFormat = x.SourceFormat,
            ExtractionModes = x.Groups.Select(group => group.ExtractionMode).Distinct().ToList(),
            Status = x.Status, PreviewVersion = x.PreviewVersion,
            TotalRows = x.TotalRows, ImportedRows = x.Groups.SelectMany(g => g.Items).Count(i => i.Status == AIImportItemStatuses.Imported),
            CreatedAtUtc = x.CreatedAtUtc, CompletedAtUtc = x.CompletedAtUtc
        }).ToListAsync(cancellationToken);
        return AIImportOperationResult<AIImportHistoryDto>.Ok(new AIImportHistoryDto { Items = items, Page = PageDto(page, pageSize, total) });
    }

    private ImportGroup CreatePersistentGroup(AIImportSourceGroup sourceGroup, ImportSourceDocument? sourceDocument = null)
    {
        var group = new ImportGroup
        {
            ImportSourceDocumentId = sourceDocument?.ImportSourceDocumentId,
            SourceDocument = sourceDocument,
            SheetName = Limit(sourceGroup.SourceLocator.Sheet ?? sourceGroup.SourceLabel, 150),
            RegionAddress = Limit(sourceGroup.SourceLocator.Region ?? SourceAddress(sourceGroup.SourceLocator), 50),
            SourceLabel = Limit(sourceGroup.SourceLabel, 200),
            SourceLocatorJson = Serialize(sourceGroup.SourceLocator),
            ExtractionMode = sourceGroup.ExtractionMode,
            HeaderRow = sourceGroup.HeaderOrdinal,
            EntityType = sourceGroup.EntityType,
            MappingJson = Serialize(sourceGroup.Mapping),
            SourceHeadersJson = Serialize(sourceGroup.SourceHeaders),
            SourceColumnsJson = Serialize(sourceGroup.SourceColumns),
            IssuesJson = Serialize(sourceGroup.Issues),
            DependencyOrder = _entityRegistry.Find(sourceGroup.EntityType)?.DependencyOrder ?? 30,
            Confidence = sourceGroup.Confidence,
            LayoutConfidence = sourceGroup.LayoutConfidence,
            Status = sourceGroup.EntityType == AIImportEntityType.Unknown
                ? AIImportItemStatuses.ReviewRequired
                : AIImportItemStatuses.Valid
        };
        foreach (var candidate in sourceGroup.Candidates)
        {
            group.Items.Add(BuildItem(group, candidate.SortOrder, candidate.RawData, candidate.SourceTrace,
                candidate.MappedData, candidate.Confidence, candidate.AIErrorCode, candidate.SourceLocator,
                candidate.EvidenceSnippet, candidate.AiConfidence, candidate.LayoutConfidence,
                candidate.OcrConfidence, candidate.FieldEvidence, candidate.Issues));
        }
        return group;
    }

    private ImportItem BuildItem(
        ImportGroup group,
        int row,
        Dictionary<string, string?> raw,
        Dictionary<string, string?> trace,
        Dictionary<string, string?> mapped,
        decimal confidence,
        string? aiError,
        AIImportSourceLocator sourceLocator,
        string evidenceSnippet,
        decimal? aiConfidence,
        decimal? layoutConfidence,
        decimal? ocrConfidence,
        IReadOnlyDictionary<string, AIImportFieldEvidence>? fieldEvidence,
        IReadOnlyCollection<AIImportErrorDto>? sourceIssues)
    {
        var item = new ImportItem
        {
            SourceRow = row,
            RawDataJson = Serialize(raw),
            SourceTraceJson = Serialize(trace),
            SourceLocatorJson = Serialize(sourceLocator),
            EvidenceSnippet = Limit(evidenceSnippet, 4000),
            Confidence = confidence,
            AiConfidence = aiConfidence,
            LayoutConfidence = layoutConfidence,
            OcrConfidence = ocrConfidence,
            FieldEvidenceJson = Serialize(fieldEvidence ?? new Dictionary<string, AIImportFieldEvidence>()),
            SourceIssuesJson = Serialize(sourceIssues ?? [])
        };
        ApplyValidation(item, group.EntityType, mapped, confidence, aiError, sourceIssues: sourceIssues);
        return item;
    }

    private void ApplyValidation(
        ImportItem item,
        AIImportEntityType entityType,
        IReadOnlyDictionary<string, string?> values,
        decimal confidence,
        string? aiError,
        bool manuallyReviewed = false,
        IReadOnlyCollection<AIImportErrorDto>? sourceIssues = null,
        bool resetDecision = true)
    {
        sourceIssues ??= Deserialize<List<AIImportErrorDto>>(item.SourceIssuesJson) ?? [];
        var result = _previewValidator.ValidateCandidate(entityType, values, confidence, sourceIssues,
            manuallyReviewed, item.Status, item.Action, aiError);
        var itemLocator = PositionDto(Deserialize<AIImportSourceLocator>(item.SourceLocatorJson));
        foreach (var issue in result.Issues)
        {
            issue.SourceLocator ??= itemLocator;
            issue.Position ??= itemLocator;
        }
        item.NormalizedDataJson = Serialize(result.NormalizedData);
        SetIssues(item, result.Issues, manuallyReviewed);
        item.Status = result.Status;
        if (resetDecision)
        {
            item.Action = AIImportActions.Create;
            item.WarningsAcknowledged = false;
        }
    }

    private async Task ValidateSessionAsync(
        ImportSession session,
        CancellationToken cancellationToken,
        AIImportValidationScope? scope = null)
    {
        var all = session.Groups.SelectMany(x => x.Items.Select(i => (Group: x, Item: i))).ToList();
        var affectedIds = _previewValidator.ResolveScope(all, scope ?? AIImportValidationScope.FullSession());
        foreach (var automaticSkip in session.Groups.SelectMany(group => group.Items)
                     .Where(item => affectedIds.Contains(item.ImportItemId))
                     .Where(item => item.Action == AIImportActions.Skip
                                    && AllIssues(item).Any(issue => issue.Code is "TRÙNG_TRONG_FILE" or "TRÙNG_TRONG_PHIÊN" or "ĐÃ_TỒN_TẠI")))
        {
            automaticSkip.Action = AIImportActions.Create;
            automaticSkip.Status = AIImportItemStatuses.Valid;
        }
        foreach (var (group, item) in all.Where(x => affectedIds.Contains(x.Item.ImportItemId) && x.Item.Action == AIImportActions.Create))
        {
            var values = Deserialize<Dictionary<string, string?>>(item.NormalizedDataJson) ?? new();
            var manualReviewValid = item.ManualReviewConfirmed
                                    && !string.IsNullOrWhiteSpace(item.ManualReviewPayloadHash)
                                    && string.Equals(item.ManualReviewPayloadHash,
                                        AIImportValidationContract.PayloadHash(item.NormalizedDataJson),
                                        StringComparison.Ordinal);
            if (!manualReviewValid) ResetManualReview(item);
            ApplyValidation(item, group.EntityType, values, item.Confidence, null, manualReviewValid,
                Deserialize<List<AIImportErrorDto>>(item.SourceIssuesJson) ?? [], resetDecision: false);
        }

        foreach (var groupItems in all.Where(x => affectedIds.Contains(x.Item.ImportItemId) && x.Item.Action == AIImportActions.Create && x.Item.Status != AIImportItemStatuses.Error)
                     .GroupBy(x => (x.Group.EntityType, Key: AIImportBusinessKeys.Create(x.Group.EntityType, Deserialize<Dictionary<string, string?>>(x.Item.NormalizedDataJson) ?? new()))))
        {
            if (string.IsNullOrWhiteSpace(groupItems.Key.Key) || groupItems.Count() <= 1) continue;
            var payloadGroups = groupItems.GroupBy(item => CanonicalPayload(item.Item.NormalizedDataJson), StringComparer.Ordinal).ToList();
            var sourceIds = groupItems.Select(entry => entry.Group.ImportSourceDocumentId).Where(id => id.HasValue).Distinct().ToList();
            var crossesFiles = sourceIds.Count > 1;
            if (payloadGroups.Count == 1)
            {
                foreach (var duplicate in groupItems.Skip(1))
                {
                    duplicate.Item.Action = AIImportActions.Skip;
                    duplicate.Item.Status = AIImportItemStatuses.Skipped;
                    SetIssues(duplicate.Item, AllIssues(duplicate.Item).Concat(new[]
                    {
                        AIImportValidationContract.Issue(crossesFiles ? "TRÙNG_TRONG_PHIÊN" : "TRÙNG_TRONG_FILE",
                            crossesFiles
                                ? "Bản ghi trùng khóa nghiệp vụ và nội dung giữa các tệp trong phiên nên được mặc định bỏ qua."
                                : "Bản ghi trùng khóa nghiệp vụ và nội dung trong tài liệu nên được mặc định bỏ qua.",
                            AIImportIssueSeverities.Warning,
                            resolution: AIImportIssueResolutions.Acknowledge,
                            metadata: new Dictionary<string, object?> { ["sourceDocumentIds"] = sourceIds })
                    }), duplicate.Item.ManualReviewConfirmed);
                }
                continue;
            }

            foreach (var conflict in groupItems)
                AddIssue(conflict.Item, AIImportValidationContract.Issue(
                    crossesFiles ? "XUNG_ĐỘT_DỮ_LIỆU_GIỮA_CÁC_TỆP" : "XUNG_ĐỘT_DỮ_LIỆU_TRONG_TÀI_LIỆU",
                    crossesFiles
                        ? "Cùng khóa nghiệp vụ xuất hiện với nội dung khác nhau giữa các tệp; hãy đối chiếu nguồn và chỉ giữ một bản ghi."
                        : "Cùng khóa nghiệp vụ nhưng nội dung khác nhau; hãy bỏ qua các bản ghi sai và chỉ giữ lại một bản ghi.",
                    AIImportIssueSeverities.Review,
                    resolution: AIImportIssueResolutions.SkipConflict,
                    metadata: new Dictionary<string, object?> { ["businessKey"] = groupItems.Key.Key, ["sourceDocumentIds"] = sourceIds }));
        }

        var affectedValues = all.Where(entry => affectedIds.Contains(entry.Item.ImportItemId))
            .Select(entry => (entry.Group.EntityType,
                Values: Deserialize<Dictionary<string, string?>>(entry.Item.NormalizedDataJson) ?? new()))
            .ToList();
        HashSet<string> Inputs(AIImportEntityType entity, params string[] fields) => affectedValues
            .Where(entry => entry.EntityType == entity)
            .SelectMany(entry => fields.Select(field => entry.Values.GetValueOrDefault(field)))
            .Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var categoryInputs = Inputs(AIImportEntityType.Category, "CategoryCode", "Name");
        categoryInputs.UnionWith(Inputs(AIImportEntityType.Drink, "Category"));
        var drinkInputs = Inputs(AIImportEntityType.Drink, "DrinkCode", "Name");
        var sizeInputs = Inputs(AIImportEntityType.Size, "SizeCode", "Name");
        var ingredientInputs = Inputs(AIImportEntityType.Ingredient, "Code", "Name");
        var supplierInputs = Inputs(AIImportEntityType.Supplier, "TaxCode");
        var unitInputs = Inputs(AIImportEntityType.Ingredient, "BaseUnit");
        var productTypeInputs = Inputs(AIImportEntityType.Drink, "ProductType");

        var categoryKeys = await _db.DrinkCategories.AsNoTracking()
            .Where(x => categoryInputs.Contains(x.CategoryCode) || categoryInputs.Contains(x.Name))
            .Select(x => new { x.CategoryCode, x.Name }).ToListAsync(cancellationToken);
        var activeCategoryKeys = await _db.DrinkCategories.AsNoTracking().Where(x => x.Active
                && (categoryInputs.Contains(x.CategoryCode) || categoryInputs.Contains(x.Name)))
            .Select(x => new { x.CategoryCode, x.Name }).ToListAsync(cancellationToken);
        var drinkKeys = await _db.Drinks.AsNoTracking()
            .Where(x => drinkInputs.Contains(x.DrinkCode) || drinkInputs.Contains(x.Name))
            .Select(x => new { x.DrinkCode, x.Name }).ToListAsync(cancellationToken);
        var sizeKeys = await _db.Sizes.AsNoTracking()
            .Where(x => sizeInputs.Contains(x.SizeCode) || sizeInputs.Contains(x.Name))
            .Select(x => new { x.SizeCode, x.Name }).ToListAsync(cancellationToken);
        var ingredientKeys = await _db.Ingredients.AsNoTracking()
            .Where(x => ingredientInputs.Contains(x.Code) || ingredientInputs.Contains(x.Name))
            .Select(x => new { x.Code, x.Name }).ToListAsync(cancellationToken);
        var supplierKeys = await _db.Suppliers.AsNoTracking()
            .Where(x => x.TaxCode != null && supplierInputs.Contains(x.TaxCode))
            .Select(x => x.TaxCode).ToListAsync(cancellationToken);
        var allUnits = await _db.Units.AsNoTracking()
            .Where(x => unitInputs.Contains(x.UnitCode) || unitInputs.Contains(x.Name))
            .Select(x => new { x.UnitCode, x.Name, x.Active }).ToListAsync(cancellationToken);
        var allProductTypes = await _db.ProductTypes.AsNoTracking()
            .Where(x => productTypeInputs.Contains(x.Code) || productTypeInputs.Contains(x.Name))
            .Select(x => new { x.Code, x.Name, x.Active }).ToListAsync(cancellationToken);
        var activeCategories = activeCategoryKeys.Concat(session.Groups.Where(g => g.EntityType == AIImportEntityType.Category).SelectMany(g => g.Items)
            .Where(i => i.Action == AIImportActions.Create && i.Status is AIImportItemStatuses.Valid or AIImportItemStatuses.Warning)
            .Select(i => { var v = Deserialize<Dictionary<string, string?>>(i.NormalizedDataJson) ?? new(); return new { CategoryCode = v.GetValueOrDefault("CategoryCode") ?? "", Name = v.GetValueOrDefault("Name") ?? "" }; })).ToList();

        var supplierItems = all.Where(x => affectedIds.Contains(x.Item.ImportItemId)
                                           && x.Group.EntityType == AIImportEntityType.Supplier
                                           && x.Item.Action == AIImportActions.Create
                                           && x.Item.Status is not AIImportItemStatuses.Error and not AIImportItemStatuses.ReviewRequired)
            .ToList();
        var supplierMatches = new Dictionary<int, List<AdminSupplierDuplicateMatchDTO>>();
        if (supplierItems.Count > 0)
        {
            var batch = await _suppliers.FindDuplicateMatchesBatchAsync(supplierItems
                .Select(entry => SupplierDto(Deserialize<Dictionary<string, string?>>(entry.Item.NormalizedDataJson) ?? new()))
                .ToList());
            supplierMatches = supplierItems.Select((entry, index) => new { entry.Item.ImportItemId, Matches = batch[index] })
                .ToDictionary(entry => entry.ImportItemId, entry => entry.Matches);
        }

        foreach (var (group, item) in all.Where(x => affectedIds.Contains(x.Item.ImportItemId) && x.Item.Action == AIImportActions.Create && x.Item.Status is not AIImportItemStatuses.Error and not AIImportItemStatuses.ReviewRequired))
        {
            var values = Deserialize<Dictionary<string, string?>>(item.NormalizedDataJson) ?? new();
            var duplicate = group.EntityType switch
            {
                AIImportEntityType.Category => categoryKeys.Any(x => Same(x.CategoryCode, values.GetValueOrDefault("CategoryCode")) || Same(x.Name, values.GetValueOrDefault("Name"))),
                AIImportEntityType.Drink => drinkKeys.Any(x => Same(x.DrinkCode, values.GetValueOrDefault("DrinkCode")) || Same(x.Name, values.GetValueOrDefault("Name"))),
                AIImportEntityType.Size => sizeKeys.Any(x => Same(x.SizeCode, values.GetValueOrDefault("SizeCode")) || Same(x.Name, values.GetValueOrDefault("Name"))),
                AIImportEntityType.Ingredient => ingredientKeys.Any(x => Same(x.Code, values.GetValueOrDefault("Code")) || Same(x.Name, values.GetValueOrDefault("Name"))),
                AIImportEntityType.Supplier => supplierKeys.Any(x => Same(x, values.GetValueOrDefault("TaxCode"))),
                _ => false
            };
            if (duplicate)
            {
                item.Action = AIImportActions.Skip;
                item.Status = AIImportItemStatuses.Skipped;
                SetIssues(item, AllIssues(item).Concat(new[]
                {
                    AIImportValidationContract.Issue("ĐÃ_TỒN_TẠI", "Bản ghi đã tồn tại nên được mặc định bỏ qua.",
                        AIImportIssueSeverities.Warning, resolution: AIImportIssueResolutions.Acknowledge)
                }), item.ManualReviewConfirmed);
                continue;
            }
            string? referenceError = null;
            if (group.EntityType == AIImportEntityType.Drink)
            {
                var category = AIImportReferenceResolver.Resolve(values.GetValueOrDefault("Category"),
                    activeCategories, categoryKeys.Where(existing => !activeCategoryKeys.Any(active => active.CategoryCode == existing.CategoryCode)),
                    value => value.CategoryCode, value => value.Name,
                    value => session.Groups.Where(pendingGroup => pendingGroup.EntityType == AIImportEntityType.Category)
                        .SelectMany(pendingGroup => pendingGroup.Items)
                        .Any(pendingItem => pendingItem.Action == AIImportActions.Create
                            && Same((Deserialize<Dictionary<string, string?>>(pendingItem.NormalizedDataJson) ?? new()).GetValueOrDefault("CategoryCode"), value.CategoryCode)));
                var productType = AIImportReferenceResolver.Resolve(values.GetValueOrDefault("ProductType"),
                    allProductTypes.Where(value => value.Active), allProductTypes.Where(value => !value.Active),
                    value => value.Code, value => value.Name);
                referenceError = ReferenceMessage("Danh mục", category.Status,
                    "Danh mục không tồn tại hoặc không được tạo trong phiên.");
                if (category.IsResolved) values["Category"] = category.Value!.CategoryCode;
                referenceError ??= ReferenceMessage("Loại sản phẩm", productType.Status,
                    "Loại sản phẩm không tồn tại.");
                if (productType.IsResolved) values["ProductType"] = productType.Value!.Code;
            }
            else if (group.EntityType == AIImportEntityType.Ingredient)
            {
                var unit = AIImportReferenceResolver.Resolve(values.GetValueOrDefault("BaseUnit"),
                    allUnits.Where(value => value.Active), allUnits.Where(value => !value.Active),
                    value => value.UnitCode, value => value.Name);
                referenceError = ReferenceMessage("Đơn vị cơ sở", unit.Status, "Đơn vị cơ sở không tồn tại.");
                if (unit.IsResolved) values["BaseUnit"] = unit.Value!.UnitCode;
            }
            item.NormalizedDataJson = Serialize(values);
            if (referenceError != null)
            {
                var ambiguous = referenceError.Contains("khớp nhiều", StringComparison.OrdinalIgnoreCase);
                AddIssue(item, AIImportValidationContract.Issue(
                    ambiguous ? "REFERENCE_KHÔNG_DUY_NHẤT" : "REFERENCE_KHÔNG_HỢP_LỆ",
                    referenceError,
                    ambiguous ? AIImportIssueSeverities.Review : AIImportIssueSeverities.Error,
                    resolution: AIImportIssueResolutions.EditField));
            }
            if (group.EntityType == AIImportEntityType.Supplier)
            {
                var matches = supplierMatches.GetValueOrDefault(item.ImportItemId) ?? [];
                if (matches.Count > 0)
                {
                    var warnings = new List<AIImportErrorDto>
                    {
                        AIImportValidationContract.Issue("NHÀ_CUNG_CẤP_GẦN_TRÙNG",
                            $"Có {matches.Count} nhà cung cấp tương tự. Nhập lý do nếu vẫn tạo.",
                            item.SupplierDuplicateWarningId.HasValue && !string.IsNullOrWhiteSpace(item.DuplicateOverrideReason)
                                ? AIImportIssueSeverities.Warning : AIImportIssueSeverities.Review,
                            resolution: item.SupplierDuplicateWarningId.HasValue && !string.IsNullOrWhiteSpace(item.DuplicateOverrideReason)
                                ? AIImportIssueResolutions.Acknowledge : AIImportIssueResolutions.EditField)
                    };
                    warnings.AddRange(matches.Select(match => new AIImportErrorDto
                    {
                        Code = "NHÀ_CUNG_CẤP_TƯƠNG_TỰ",
                        Message = $"{match.Code} · {match.Name} · {string.Join(", ", match.MatchedSignals)}",
                        Severity = AIImportIssueSeverities.Warning
                    }));
                    SetIssues(item, AllIssues(item).Concat(warnings), item.ManualReviewConfirmed);
                }
            }
        }
        RefreshCounts(session);
    }

    private static AdminSupplierCreateDTO SupplierDto(IReadOnlyDictionary<string, string?> values) =>
        AIImportEntityCreator.SupplierDto(values);

    private async Task<ImportSession?> OwnedSessionAsync(int id, AdminActorContext actor, bool tracked, CancellationToken cancellationToken)
    {
        IQueryable<ImportSession> query = _db.ImportSessions;
        if (!tracked) query = query.AsNoTracking();
        return tracked
            ? await query.Include(x => x.SourceDocuments).Include(x => x.Groups).ThenInclude(x => x.Items).SingleOrDefaultAsync(x => x.ImportSessionId == id && x.UploadedByAccountId == actor.AccountId, cancellationToken)
            : await query.SingleOrDefaultAsync(x => x.ImportSessionId == id && x.UploadedByAccountId == actor.AccountId, cancellationToken);
    }

    private async Task<AIImportSessionDto> BuildSessionDtoAsync(int id, int? groupId, string? status, int page, int pageSize, AdminActorContext actor, CancellationToken cancellationToken)
    {
        (page, pageSize) = Page(page, pageSize);
        var session = await _db.ImportSessions.AsNoTracking().Include(x => x.SourceDocuments).Include(x => x.Groups).SingleAsync(x => x.ImportSessionId == id && x.UploadedByAccountId == actor.AccountId, cancellationToken);
        var itemQuery = _db.ImportItems.AsNoTracking().Where(x => x.Group.ImportSessionId == id);
        if (groupId.HasValue) itemQuery = itemQuery.Where(x => x.ImportGroupId == groupId.Value);
        if (!string.IsNullOrWhiteSpace(status)) itemQuery = itemQuery.Where(x => x.Status == status);
        var total = await itemQuery.CountAsync(cancellationToken);
        var items = await _sessionQuery.OrderPreviewItems(itemQuery)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var itemMap = items.GroupBy(x => x.ImportGroupId).ToDictionary(x => x.Key, x => x.Select(ItemDto).ToList());
        return new AIImportSessionDto
        {
            SessionId = session.ImportSessionId, FileName = session.FileName, SourceFormat = session.SourceFormat,
            SourceMetadata = Deserialize<Dictionary<string, object?>>(session.SourceMetadataJson) ?? new(),
            ExtractionModes = session.Groups.Select(group => group.ExtractionMode).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Status = session.Status, AnalysisVersion = session.AnalysisVersion,
            PreviewVersion = session.PreviewVersion, CreatedAtUtc = session.CreatedAtUtc, ExpiresAtUtc = session.ExpiresAtUtc,
            FailureCode = session.FailureCode, FailureMessage = session.FailureMessage,
            AnalysisWarnings = Deserialize<List<AIImportErrorDto>>(session.AnalysisWarningsJson) ?? new(),
            RequestedOcr = session.RequestedOcr, EffectiveOcr = session.EffectiveOcr,
            OcrConfigVersion = session.OcrConfigVersion,
            ConfirmBlockedBySources = session.SourceDocuments.Any(source => source.Status is AIImportSourceDocumentStatuses.Failed or AIImportSourceDocumentStatuses.Processing),
            SourceDocuments = session.SourceDocuments.OrderBy(source => source.SortOrder).Select(source => new AIImportSourceDocumentDto
            {
                SourceDocumentId = source.ImportSourceDocumentId,
                FileName = source.OriginalFileName,
                SourceFormat = source.SourceFormat,
                FileSize = source.FileSize,
                SortOrder = source.SortOrder,
                Status = source.Status,
                ErrorCode = source.ErrorCode,
                ErrorMessage = source.ErrorMessage,
                Metadata = Deserialize<Dictionary<string, object?>>(source.SourceMetadataJson) ?? new()
            }).ToList(),
            Summary = Summary(session), Page = PageDto(page, pageSize, total),
            Groups = session.Groups.OrderBy(x => x.DependencyOrder).ThenBy(x => x.ImportGroupId).Select(x => new AIImportGroupDto
            {
                GroupId = x.ImportGroupId, SourceDocumentId = x.ImportSourceDocumentId,
                SourceFileName = session.SourceDocuments.SingleOrDefault(source => source.ImportSourceDocumentId == x.ImportSourceDocumentId)?.OriginalFileName ?? session.FileName,
                SheetName = x.SheetName, RegionAddress = x.RegionAddress,
                SourceLabel = x.SourceLabel, SourceLocator = PositionDto(Deserialize<AIImportSourceLocator>(x.SourceLocatorJson)),
                ExtractionMode = x.ExtractionMode, HeaderRow = x.HeaderRow,
                EntityType = x.EntityType, Mapping = Deserialize<Dictionary<string, string?>>(x.MappingJson) ?? new(),
                SourceHeaders = Deserialize<List<string>>(x.SourceHeadersJson) ?? new(), DependencyOrder = x.DependencyOrder,
                SourceColumns = (Deserialize<List<AIImportSourceColumn>>(x.SourceColumnsJson) ?? []).Select(ColumnDto).ToList(),
                Issues = Deserialize<List<AIImportErrorDto>>(x.IssuesJson) ?? [],
                SourceRegionId = $"{session.SourceFormat}:{x.RegionAddress}",
                Confidence = x.Confidence, SourceConfidence = x.Confidence, LayoutConfidence = x.LayoutConfidence,
                Status = x.Status, Items = itemMap.GetValueOrDefault(x.ImportGroupId) ?? new()
            }).ToList()
        };
    }

    internal static IOrderedQueryable<ImportItem> OrderPreviewItems(IQueryable<ImportItem> query) => query
        .OrderBy(x => x.ImportGroupId)
        .ThenBy(x => x.Status == AIImportItemStatuses.Error ? 0
            : x.Status == AIImportItemStatuses.ReviewRequired ? 1
            : x.Status == AIImportItemStatuses.Warning && !x.WarningsAcknowledged ? 2
            : x.Status == AIImportItemStatuses.Valid || x.Status == AIImportItemStatuses.Warning ? 3
            : x.Status == AIImportItemStatuses.Skipped ? 4 : 5)
        .ThenBy(x => x.SourceRow)
        .ThenBy(x => x.ImportItemId);

    private async Task<string?> RequireAsync(AdminActorContext actor, params string[] permissionCodes)
    {
        foreach (var permission in permissionCodes)
        {
            var decision = await _permissions.HasPermissionAsync(actor.AccountId, permission, null);
            if (!decision.IsSuccess || decision.Data?.Allowed != true) return "Tài khoản không có quyền thực hiện thao tác này.";
        }
        return null;
    }

    private async Task TransitionAsync(ImportSession session, string expected, string next, AdminActorContext actor, string action, CancellationToken cancellationToken)
    {
        if (session.Status != expected) throw new InvalidOperationException("Trạng thái hiện tại không cho phép thực hiện thao tác này.");
        session.Status = next;
        AddAudit(session, actor, action, expected, next);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task FailAsync(ImportSession session, string code, string message, AdminActorContext actor, CancellationToken cancellationToken)
    {
        var before = session.Status;
        session.Status = AIImportSessionStatuses.Failed;
        session.FailureCode = code;
        session.FailureMessage = message;
        AddAudit(session, actor, "FAILED", before, session.Status, null, null, null, code);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task ExpireIfNeededAsync(ImportSession session, AdminActorContext actor, CancellationToken cancellationToken)
    {
        if (session.ExpiresAtUtc > DateTime.UtcNow || session.Status is AIImportSessionStatuses.Completed or AIImportSessionStatuses.Cancelled or AIImportSessionStatuses.Expired) return;
        await _db.ImportSessions.Where(x => x.ImportSessionId == session.ImportSessionId && x.Status == session.Status)
            .ExecuteUpdateAsync(x => x.SetProperty(s => s.Status, AIImportSessionStatuses.Expired)
                .SetProperty(s => s.SourceSnapshotJson, (string?)null), cancellationToken);
        await _db.ImportSourceDocuments.Where(source => source.ImportSessionId == session.ImportSessionId)
            .ExecuteUpdateAsync(update => update.SetProperty(source => source.SourceSnapshotJson, (string?)null), cancellationToken);
    }

    private static void AddAudit(ImportSession session, AdminActorContext actor, string action, string? before, string? after, AIImportEntityType? entity = null, string? keyHash = null, string? result = null, string? error = null) =>
        session.Audits.Add(new ImportAudit { StaffId = actor.StaffId, AccountId = actor.AccountId, Action = action, StatusBefore = before, StatusAfter = after, EntityType = entity,
            PromptVersion = session.PromptVersion, SchemaVersion = session.SchemaVersion,
            ExtractionVersion = session.ExtractionVersion, PreviewVersion = session.PreviewVersion,
            IdempotencyKeyHash = keyHash, ResultSummaryJson = result, ErrorCode = error,
            SourceFormat = session.SourceFormat, ExtractionMode = Limit(string.Join(',', session.Groups.Select(group => group.ExtractionMode).Distinct()), 200),
            OcrUsed = MetadataBool(session.SourceMetadataJson, "ocrUsed"),
            OcrPageCount = MetadataInt(session.SourceMetadataJson, "ocrPageCount"),
            OcrProvider = MetadataString(session.SourceMetadataJson, "ocrProvider"),
            OcrProviderVersion = MetadataString(session.SourceMetadataJson, "ocrProviderVersion"),
            OcrExtractionVersion = MetadataString(session.SourceMetadataJson, "extractionVersion"),
            OcrConfidenceSummaryJson = MetadataJson(session.SourceMetadataJson, "ocrConfidenceSummary"),
            AiChunkCount = AiChunkCount(session), CreatedAtUtc = DateTime.UtcNow });

    private static JsonElement? MetadataValue(string json, string key)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty(key, out var value) ? value.Clone() : null;
        }
        catch (JsonException) { return null; }
    }

    private static bool MetadataBool(string json, string key) => MetadataValue(json, key) is { } value
        && value.ValueKind is JsonValueKind.True or JsonValueKind.False && value.GetBoolean();
    private static int MetadataInt(string json, string key) => MetadataValue(json, key) is { } value
        && value.TryGetInt32(out var number) ? number : 0;
    private static string? MetadataString(string json, string key) => MetadataValue(json, key) is { } value
        && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static string? MetadataJson(string json, string key) => MetadataValue(json, key)?.GetRawText();

    private static Dictionary<string, string?> ApplyMapping(IReadOnlyDictionary<string, string?> raw, IReadOnlyDictionary<string, string?> mapping) =>
        mapping.ToDictionary(x => x.Key, x => string.IsNullOrWhiteSpace(x.Value) ? null : raw.GetValueOrDefault(x.Value), StringComparer.OrdinalIgnoreCase);
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
            var header = Clean(headerValue);
            if (header == null || raw.ContainsKey(header)) continue;
            raw[header] = values.GetValueOrDefault(column);
            trace[header] = $"{region.SheetName}!{column}{sourceRow}";
        }
        return (raw, trace);
    }
    private static AIImportRegionData RestoreRegion(ImportGroup group)
    {
        var headers = Deserialize<List<string>>(group.SourceHeadersJson) ?? new();
        var cells = new Dictionary<(int Row, int Column), string?>();
        for (var index = 0; index < headers.Count; index++) cells[(group.HeaderRow, index + 1)] = headers[index];
        foreach (var item in group.Items.OrderBy(x => x.SourceRow))
        {
            var raw = Deserialize<Dictionary<string, string?>>(item.RawDataJson) ?? new();
            for (var index = 0; index < headers.Count; index++) cells[(item.SourceRow, index + 1)] = raw.GetValueOrDefault(headers[index]);
        }
        return new AIImportRegionData
        {
            SheetName = group.SheetName,
            MinRow = group.HeaderRow,
            MaxRow = group.Items.Count == 0 ? group.HeaderRow : group.Items.Max(x => x.SourceRow),
            MinColumn = 1,
            MaxColumn = Math.Max(1, headers.Count),
            Cells = cells
        };
    }
    private static bool Same(string? left, string? right) => AIImportSchemaRegistry.Key(left) == AIImportSchemaRegistry.Key(right) && !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right);
    private static string? ReferenceMessage(string label, string status, string notFoundMessage) => status switch
    {
        AIImportReferenceStatuses.Found or AIImportReferenceStatuses.PendingInSession => null,
        AIImportReferenceStatuses.Ambiguous => $"{label} khớp nhiều bản ghi; hãy nhập code duy nhất.",
        AIImportReferenceStatuses.Inactive => $"{label} đã ngừng hoạt động.",
        AIImportReferenceStatuses.Forbidden => $"{label} chứa định danh không được phép.",
        _ => notFoundMessage
    };
    private static string CanonicalPayload(string json)
    {
        var values = Deserialize<Dictionary<string, string?>>(json) ?? new();
        return Serialize(values.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase));
    }
    private static List<AIImportErrorDto> AllIssues(ImportItem item) =>
        (Deserialize<List<AIImportErrorDto>>(item.ErrorsJson) ?? [])
        .Concat(Deserialize<List<AIImportErrorDto>>(item.WarningsJson) ?? []).ToList();
    private static void SetIssues(ImportItem item, IEnumerable<AIImportErrorDto> issues, bool manualReviewConfirmed)
    {
        var normalized = issues.GroupBy(issue => new { issue.Code, issue.Field, issue.Severity })
            .Select(group => group.First()).ToList();
        foreach (var issue in normalized)
        {
            issue.SourceLocator ??= issue.Position;
            issue.Position ??= issue.SourceLocator;
            issue.Metadata ??= new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }
        item.ErrorsJson = Serialize(normalized.Where(issue => issue.Severity is AIImportIssueSeverities.Error or AIImportIssueSeverities.Review));
        item.WarningsJson = Serialize(normalized.Where(issue => issue.Severity == AIImportIssueSeverities.Warning));
        item.Status = AIImportValidationContract.ResolveStatus(item.Status, item.Action, normalized, manualReviewConfirmed);
    }
    private static void AddIssue(ImportItem item, AIImportErrorDto issue)
    {
        var issues = AllIssues(item).Where(existing => !(existing.Code == issue.Code && existing.Field == issue.Field)).ToList();
        issues.Add(issue);
        SetIssues(item, issues, item.ManualReviewConfirmed);
    }
    private static void ResetManualReview(ImportItem item)
    {
        item.ManualReviewConfirmed = false;
        item.ManualReviewConfirmedAtUtc = null;
        item.ManualReviewConfirmedByAccountId = null;
        item.ManualReviewPayloadHash = null;
    }
    private static AIImportSourceColumnDto ColumnDto(AIImportSourceColumn column) => new()
    {
        Key = column.Key,
        Label = column.Label,
        Classification = column.Classification,
        TargetField = column.TargetField,
        SourceLocator = PositionDto(column.SourceLocator),
        Reason = column.Reason
    };
    private static void NormalizeDocumentIssues(AIImportSourceDocument document)
    {
        foreach (var warning in document.Warnings)
        {
            warning.Severity = AIImportIssueSeverities.Warning;
            warning.SourceLocator ??= warning.Position;
        }
        foreach (var error in document.Errors)
        {
            error.Severity = AIImportIssueSeverities.Error;
            error.SourceLocator ??= error.Position;
        }
    }
    private static AIImportErrorDto WithSource(AIImportErrorDto issue, ImportSourceDocument source)
    {
        issue.Metadata ??= new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        issue.Metadata["sourceDocumentId"] = source.ImportSourceDocumentId;
        issue.Metadata["fileName"] = source.OriginalFileName;
        return issue;
    }
    private async Task<AIImportOperationResult<AIImportSessionDto>> FailBatchLimitAsync(
        ImportSession session,
        AdminActorContext actor,
        CancellationToken cancellationToken)
    {
        const string message = "Tổng tài nguyên trích xuất của các tệp vượt giới hạn phiên nhập.";
        await FailAsync(session, "VƯỢT_GIỚI_HẠN_PHIÊN_NHẬP", message, actor, cancellationToken);
        return AIImportOperationResult<AIImportSessionDto>.Fail(413, "VƯỢT_GIỚI_HẠN_PHIÊN_NHẬP", message);
    }
    private AIImportOcrRuntimeState LegacyOcrState()
    {
        return new AIImportOcrRuntimeState(
            false, false,
            _options.OcrProvider, null, TesseractLocalOcrProvider.NormalizeLanguages(_options.OcrLanguages),
            false, false,
            _options.OcrReviewConfidenceThreshold, _options.OcrRenderDpi, _options.OcrMaxPages,
            _options.OcrMaxRenderedPixelsPerPage, _options.OcrMaxTotalRenderedPixels,
            _options.OcrPageTimeoutSeconds, _options.OcrTotalTimeoutSeconds,
            _options.OcrMaxConcurrentPages, "ocr-tesseract-appsettings-v1",
            "NOT_CHECKED", "Cần kiểm tra Tesseract local trong System Settings.", null);
    }
    private static bool IsResolvedMappingConflict(
        AIImportErrorDto issue,
        IReadOnlyDictionary<string, string?> mapping)
    {
        if (issue.Code != "XUNG_ĐỘT_ÁNH_XẠ") return false;
        var targetField = IssueMetadataString(issue, "targetField") ?? issue.Field;
        if (string.IsNullOrWhiteSpace(targetField)
            || !mapping.TryGetValue(targetField, out var selectedSourceKey)
            || string.IsNullOrWhiteSpace(selectedSourceKey)) return false;
        var candidates = IssueMetadataStrings(issue, "candidateSourceKeys");
        return candidates.Count == 0 || candidates.Contains(selectedSourceKey, StringComparer.OrdinalIgnoreCase);
    }
    private static string? IssueMetadataString(AIImportErrorDto issue, string key)
    {
        if (!issue.Metadata.TryGetValue(key, out var value) || value == null) return null;
        return value is JsonElement element && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : Convert.ToString(value);
    }
    private static IReadOnlyList<string> IssueMetadataStrings(AIImportErrorDto issue, string key)
    {
        if (!issue.Metadata.TryGetValue(key, out var value) || value == null) return [];
        if (value is JsonElement element && element.ValueKind == JsonValueKind.Array)
            return element.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString()!).ToList();
        return value is IEnumerable<string> strings ? strings.ToList() : [];
    }
    private static IEnumerable<AIImportErrorDto> BuildColumnIssues(
        IEnumerable<AIImportSourceColumn> columns,
        IReadOnlyDictionary<string, string?> raw)
    {
        foreach (var column in columns.Where(column => !string.IsNullOrWhiteSpace(raw.GetValueOrDefault(column.Key))))
        {
            if (column.Classification == AIImportColumnClassifications.Forbidden)
                yield return AIImportValidationContract.Issue("CỘT_CẤM",
                    $"Cột '{column.Label}' không được phép dùng trong AI Smart Import.", AIImportIssueSeverities.Error,
                    resolution: AIImportIssueResolutions.ReuploadOrSkip,
                    metadata: new Dictionary<string, object?> { ["sourceColumn"] = column.Key });
            else if (column.Classification == AIImportColumnClassifications.Unknown)
                yield return AIImportValidationContract.Issue("CỘT_KHÔNG_XÁC_ĐỊNH",
                    $"Cột '{column.Label}' không thuộc danh sách trường được phép nhập và sẽ bị bỏ qua.", AIImportIssueSeverities.Warning,
                    resolution: AIImportIssueResolutions.Acknowledge,
                    metadata: new Dictionary<string, object?> { ["sourceColumn"] = column.Key });
        }
    }
    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
    private static T? Deserialize<T>(string value) { try { return JsonSerializer.Deserialize<T>(value, JsonOptions); } catch (JsonException) { return default; } }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static (int Page, int PageSize) Page(int page, int pageSize) => (Math.Max(1, page), Math.Clamp(pageSize <= 0 ? 50 : pageSize, 1, 200));
    private static AIImportPageDto PageDto(int page, int size, int total) => new() { Page = page, PageSize = size, TotalItems = total, TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)size)) };
    private static AIImportSummaryDto Summary(ImportSession x) => new() { TotalGroups = x.TotalGroups, TotalRows = x.TotalRows, Valid = x.ValidRows, Warnings = x.WarningRows, Errors = x.ErrorRows, ReviewRequired = x.ReviewRows, Skipped = x.SkippedRows };
    private static AIImportItemDto ItemDto(ImportItem x)
    {
        var errors = Deserialize<List<AIImportErrorDto>>(x.ErrorsJson) ?? [];
        var warnings = Deserialize<List<AIImportErrorDto>>(x.WarningsJson) ?? [];
        return new AIImportItemDto
        {
            ItemId = x.ImportItemId, SourceRow = x.SourceRow,
            RawData = Deserialize<Dictionary<string, string?>>(x.RawDataJson) ?? new(),
            NormalizedData = Deserialize<Dictionary<string, string?>>(x.NormalizedDataJson) ?? new(),
            SourceTrace = Deserialize<Dictionary<string, string?>>(x.SourceTraceJson) ?? new(),
            SourceLocator = PositionDto(Deserialize<AIImportSourceLocator>(x.SourceLocatorJson)),
            EvidenceSnippet = x.EvidenceSnippet, AiConfidence = x.AiConfidence, OcrConfidence = x.OcrConfidence,
            SourceConfidence = x.Confidence, LayoutConfidence = x.LayoutConfidence,
            FieldEvidence = (Deserialize<Dictionary<string, AIImportFieldEvidence>>(x.FieldEvidenceJson) ?? new())
                .ToDictionary(pair => pair.Key, pair => new AIImportFieldEvidenceDto
                {
                    SourceKind = pair.Value.SourceKind,
                    SourceLocator = PositionDto(pair.Value.Locator),
                    RawText = pair.Value.RawText,
                    NormalizedValue = pair.Value.NormalizedValue,
                    OcrConfidence = pair.Value.OcrConfidence,
                    AiConfidence = pair.Value.AiConfidence
                }, StringComparer.OrdinalIgnoreCase),
            Status = x.Status, Action = x.Action, Errors = errors, Warnings = warnings,
            Issues = errors.Concat(warnings).ToList(), WarningsAcknowledged = x.WarningsAcknowledged,
            ManualReviewConfirmed = x.ManualReviewConfirmed,
            ManualReviewConfirmedAtUtc = x.ManualReviewConfirmedAtUtc,
            DuplicateOverrideReason = x.DuplicateOverrideReason, ImportedEntityId = x.ImportedEntityId
        };
    }
    private static bool IsFooterRow(IReadOnlyDictionary<string, string?> raw)
    {
        var values = raw.Values.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => AIImportSchemaRegistry.Key(x)).ToList();
        if (values.Count == 0) return true;
        var first = values[0];
        return values.Count <= 3 && (first.StartsWith("tong", StringComparison.Ordinal)
                                     || first.StartsWith("total", StringComparison.Ordinal)
                                     || first.StartsWith("ghichu", StringComparison.Ordinal)
                                     || first.StartsWith("note", StringComparison.Ordinal));
    }
    private static void RefreshCounts(ImportSession x) { var items = x.Groups.SelectMany(g => g.Items).ToList(); x.TotalGroups = x.Groups.Count; x.TotalRows = items.Count; x.ValidRows = items.Count(i => i.Status == AIImportItemStatuses.Valid); x.WarningRows = items.Count(i => i.Status == AIImportItemStatuses.Warning); x.ErrorRows = items.Count(i => i.Status == AIImportItemStatuses.Error); x.ReviewRows = items.Count(i => i.Status == AIImportItemStatuses.ReviewRequired); x.SkippedRows = items.Count(i => i.Action == AIImportActions.Skip); }
    private static ConfirmSnapshotItem[] ConfirmSnapshot(ImportSession session) => session.Groups
        .OrderBy(x => x.ImportGroupId).SelectMany(x => x.Items.OrderBy(i => i.ImportItemId))
        .Select(x => new ConfirmSnapshotItem(x.ImportItemId, x.Action, x.NormalizedDataJson, x.WarningsAcknowledged,
            x.ManualReviewConfirmed, x.ManualReviewPayloadHash, x.SupplierDuplicateWarningId, x.DuplicateOverrideReason))
        .ToArray();
    private static string ValidationFingerprint(ImportSession session) => Serialize(session.Groups
        .OrderBy(x => x.ImportGroupId).SelectMany(x => x.Items.OrderBy(i => i.ImportItemId))
        .Select(x => new { x.ImportItemId, x.Action, x.Status, x.NormalizedDataJson, x.ErrorsJson, x.WarningsJson,
            x.WarningsAcknowledged, x.ManualReviewConfirmed, x.ManualReviewPayloadHash,
            x.DuplicateOverrideReason, x.SupplierDuplicateWarningId })
        .ToArray());
    private static List<AIImportErrorDto> BuildBlockerDetails(ImportSession session) => session.Groups
        .SelectMany(group => group.Items
            .Where(item => item.Action != AIImportActions.Skip
                           && (item.Status is AIImportItemStatuses.Error or AIImportItemStatuses.ReviewRequired
                               || (item.Status == AIImportItemStatuses.Warning && !item.WarningsAcknowledged)))
            .SelectMany(item =>
            {
                var issues = (Deserialize<List<AIImportErrorDto>>(item.ErrorsJson) ?? new())
                    .Concat(Deserialize<List<AIImportErrorDto>>(item.WarningsJson) ?? new()).ToList();
                if (issues.Count == 0)
                    issues.Add(new AIImportErrorDto { Code = item.Status, Message = "Dòng cần được kiểm tra trước khi Confirm." });
                return issues.Select(issue => new AIImportErrorDto
                {
                    ItemId = item.ImportItemId,
                    Code = issue.Code,
                    Message = issue.Message,
                    Field = issue.Field,
                    Severity = issue.Severity,
                    Metadata = issue.Metadata,
                    Position = ItemPosition(group, item, issue.Position?.Column),
                    SourceLocator = ItemPosition(group, item, issue.Position?.Column)
                });
            }))
        .OrderBy(x => x.Position?.Sheet).ThenBy(x => x.Position?.Row).ToList();
    private static AIImportErrorDto RowError(ImportGroup group, ImportItem item, string code, string message) => new()
    {
        ItemId = item.ImportItemId,
        Code = code,
        Message = message,
        Position = ItemPosition(group, item)
    };

    private static AIImportPositionDto ItemPosition(ImportGroup group, ImportItem item, string? column = null)
    {
        var position = PositionDto(Deserialize<AIImportSourceLocator>(item.SourceLocatorJson)) ?? new AIImportPositionDto();
        position.SourceFormat ??= group.Session?.SourceFormat;
        position.Sheet ??= group.SheetName;
        position.Region ??= group.RegionAddress;
        position.Row ??= item.SourceRow;
        position.Column ??= column;
        return position;
    }

    private static AIImportPositionDto? PositionDto(AIImportSourceLocator? locator) => locator == null ? null : new AIImportPositionDto
    {
        SourceFormat = locator.SourceFormat,
        Sheet = locator.Sheet,
        Region = locator.Region,
        Row = locator.Row,
        Column = locator.Column,
        Section = locator.Section,
        Paragraph = locator.Paragraph,
        Table = locator.Table,
        TableRow = locator.TableRow,
        TableColumn = locator.TableColumn,
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

    private static string SourceAddress(AIImportSourceLocator locator) => locator.SourceFormat switch
    {
        AIImportSourceFormats.Docx when locator.Table.HasValue => $"Table {locator.Table}/Row {locator.TableRow}",
        AIImportSourceFormats.Docx => $"Paragraph {locator.Paragraph}",
        AIImportSourceFormats.Pdf => $"Page {locator.Page}/Block {locator.Block}",
        _ => locator.Region ?? "Nguồn dữ liệu"
    };

    private static int AiChunkCount(ImportSession session)
    {
        var metadata = Deserialize<Dictionary<string, JsonElement>>(session.SourceMetadataJson);
        return metadata != null && metadata.TryGetValue("aiChunkCount", out var value) && value.TryGetInt32(out var count) ? count : 0;
    }

    private static string Limit(string? value, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "Nguồn dữ liệu" : value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }
    private void DetachSessionGraph(ImportSession session)
    {
        foreach (var item in session.Groups.SelectMany(x => x.Items)) _db.Entry(item).State = EntityState.Detached;
        foreach (var group in session.Groups) _db.Entry(group).State = EntityState.Detached;
        _db.Entry(session).State = EntityState.Detached;
    }
    private static AIImportOperationResult<T> NotFound<T>() => AIImportOperationResult<T>.Fail(404, "KHÔNG_TÌM_THẤY_PHIÊN", "Không tìm thấy phiên thuộc tài khoản hiện tại.");
    private static AIImportOperationResult<T> Conflict<T>(string code, string message) => AIImportOperationResult<T>.Fail(409, code, message);
    private static AIImportOperationResult<T>? CheckEditable<T>(ImportSession session, int expected) => session.Status != AIImportSessionStatuses.ReadyToPreview ? Conflict<T>("PHIÊN_KHÔNG_THỂ_SỬA", "Phiên không ở trạng thái xem trước.") : session.PreviewVersion != expected ? Conflict<T>("PREVIEW_ĐÃ_THAY_ĐỔI", "Bản xem trước đã thay đổi; vui lòng tải lại.") : null;
    private static string BusinessErrorCode(Exception ex) => ex is SupplierDomainException supplier ? supplier.Code : FindSql(ex) is { Number: 2601 or 2627 } ? "DỮ_LIỆU_ĐÃ_TỒN_TẠI" : ex is DuplicateDataException ? "DỮ_LIỆU_ĐÃ_TỒN_TẠI" : "IMPORT_THẤT_BẠI";
    private static string BusinessMessage(Exception ex) => FindSql(ex) is { Number: 2601 or 2627 } ? "Dữ liệu nghiệp vụ đã tồn tại; toàn bộ phiên đã được rollback." : ex is SupplierDomainException or DuplicateDataException or InvalidOperationException or ArgumentException ? ex.Message : "Không thể tạo dữ liệu; toàn bộ phiên đã được rollback.";
    private static SqlException? FindSql(Exception ex) { for (var current = ex; current != null; current = current.InnerException!) if (current is SqlException sql) return sql; return null; }
    private sealed record ConfirmSnapshotItem(
        int ImportItemId,
        string Action,
        string NormalizedDataJson,
        bool WarningsAcknowledged,
        bool ManualReviewConfirmed,
        string? ManualReviewPayloadHash,
        Guid? SupplierDuplicateWarningId,
        string? DuplicateOverrideReason);
    private sealed record PreparedUpload(IFormFile File, byte[] Content);
    private sealed class AIImportRowException : Exception
    {
        public AIImportRowException(ImportGroup group, ImportItem item, Exception innerException)
            : base(innerException.Message, innerException)
        {
            Group = group;
            Item = item;
        }
        public ImportGroup Group { get; }
        public ImportItem Item { get; }
    }
}
