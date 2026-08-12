using System.Data;
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
    private readonly IAIImportExcelParser _parser;
    private readonly IAIImportRegionAnalyzer _analyzer;
    private readonly IAIImportSchemaRegistry _schemas;
    private readonly IRequestDeduplicationService _deduplication;
    private readonly IAdminPermissionService _permissions;
    private readonly IAdminCategoryService _categories;
    private readonly IAdminDrinkService _drinks;
    private readonly IAdminSizeService _sizes;
    private readonly IAdminIngredientService _ingredients;
    private readonly IAdminSupplierService _suppliers;
    private readonly AIImportOptions _options;
    private readonly string _ollamaModel;
    private readonly ILogger<AIImportService> _logger;

    public AIImportService(
        AppDbContext db,
        IAIImportExcelParser parser,
        IAIImportRegionAnalyzer analyzer,
        IAIImportSchemaRegistry schemas,
        IRequestDeduplicationService deduplication,
        IAdminPermissionService permissions,
        IAdminCategoryService categories,
        IAdminDrinkService drinks,
        IAdminSizeService sizes,
        IAdminIngredientService ingredients,
        IAdminSupplierService suppliers,
        IOptions<AIImportOptions> options,
        IOptions<OllamaOptions> ollamaOptions,
        ILogger<AIImportService> logger)
    {
        _db = db;
        _parser = parser;
        _analyzer = analyzer;
        _schemas = schemas;
        _deduplication = deduplication;
        _permissions = permissions;
        _categories = categories;
        _drinks = drinks;
        _sizes = sizes;
        _ingredients = ingredients;
        _suppliers = suppliers;
        _options = options.Value;
        _ollamaModel = ollamaOptions.Value.Model;
        _logger = logger;
    }

    public async Task<AIImportOperationResult<AIImportSessionDto>> AnalyzeAsync(
        IFormFile? file,
        AIImportEntityType? entityHint,
        AdminActorContext actor,
        CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, PermissionConstants.AIImportUpload, PermissionConstants.AIImportAnalyze);
        if (access != null) return AIImportOperationResult<AIImportSessionDto>.Fail(403, "KHÔNG_CÓ_QUYỀN", access);
        if (file == null || file.Length <= 0)
            return AIImportOperationResult<AIImportSessionDto>.Fail(400, "FILE_BẮT_BUỘC", "Vui lòng chọn tệp .xlsx.");
        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            return AIImportOperationResult<AIImportSessionDto>.Fail(400, "ĐỊNH_DẠNG_KHÔNG_HỖ_TRỢ", "MVP chỉ hỗ trợ tệp .xlsx.");
        if (file.Length > _options.MaxFileBytes)
            return AIImportOperationResult<AIImportSessionDto>.Fail(413, "FILE_QUÁ_LỚN", $"Tệp vượt giới hạn {_options.MaxFileBytes / 1024 / 1024} MB.");

        await using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        var session = new ImportSession
        {
            FileName = Path.GetFileName(file.FileName),
            FileHash = Convert.ToHexString(SHA256.HashData(buffer.ToArray())),
            FileSize = file.Length,
            UploadedByStaffId = actor.StaffId,
            UploadedByAccountId = actor.AccountId,
            StoreId = 0,
            Status = AIImportSessionStatuses.Uploaded,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(_options.SessionLifetimeHours)
        };
        _db.ImportSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);
        await TransitionAsync(session, AIImportSessionStatuses.Uploaded, AIImportSessionStatuses.Analyzing, actor, "ANALYZE_STARTED", cancellationToken);

        try
        {
            buffer.Position = 0;
            var workbook = await _parser.ParseAsync(buffer, cancellationToken);
            session.AnalysisWarningsJson = Serialize(workbook.Warnings);
            if (workbook.Errors.Count > 0)
            {
                await FailAsync(session, workbook.Errors[0].Code, workbook.Errors[0].Message, actor, cancellationToken);
                return AIImportOperationResult<AIImportSessionDto>.Fail(422, workbook.Errors[0].Code, workbook.Errors[0].Message, workbook.Errors);
            }

            await TransitionAsync(session, AIImportSessionStatuses.Analyzing, AIImportSessionStatuses.Validating, actor, "PARSING_COMPLETED", cancellationToken);
            foreach (var region in workbook.Regions)
            {
                var analysis = await _analyzer.AnalyzeAsync(region, entityHint, cancellationToken);
                if (analysis.UsedAI) session.ModelName = _ollamaModel;
                var headers = region.ReadRow(analysis.HeaderRow).Values.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()).ToList();
                var group = new ImportGroup
                {
                    SheetName = region.SheetName,
                    RegionAddress = region.Address,
                    HeaderRow = analysis.HeaderRow,
                    EntityType = analysis.EntityType,
                    MappingJson = Serialize(analysis.Mapping),
                    SourceHeadersJson = Serialize(headers),
                    DependencyOrder = DependencyOrder(analysis.EntityType),
                    Confidence = analysis.Confidence,
                    Status = analysis.EntityType == AIImportEntityType.Unknown ? AIImportItemStatuses.ReviewRequired : AIImportItemStatuses.Valid
                };
                session.Groups.Add(group);
                foreach (var rowNumber in Enumerable.Range(analysis.HeaderRow + 1, Math.Max(0, region.MaxRow - analysis.HeaderRow)))
                {
                    var (raw, trace) = ReadNamedRow(region, analysis.HeaderRow, rowNumber);
                    if (raw.Values.All(string.IsNullOrWhiteSpace)) continue;
                    if (IsFooterRow(raw)) continue;
                    var mapped = ApplyMapping(raw, analysis.Mapping);
                    var item = BuildItem(group, rowNumber, raw, trace, mapped, analysis.Confidence, analysis.AIErrorCode);
                    group.Items.Add(item);
                }
            }
            await _db.SaveChangesAsync(cancellationToken);
            await ValidateSessionAsync(session, cancellationToken);
            session.PreviewVersion = 1;
            session.Status = AIImportSessionStatuses.ReadyToPreview;
            RefreshCounts(session);
            AddAudit(session, actor, "ANALYZE_COMPLETED", AIImportSessionStatuses.Validating, session.Status);
            await _db.SaveChangesAsync(cancellationToken);
            return AIImportOperationResult<AIImportSessionDto>.Ok(await BuildSessionDtoAsync(session.ImportSessionId, null, null, 1, _options.DefaultPageSize, actor, cancellationToken), "Đã phân tích tệp Excel.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "AI Smart Import analyze failed. SessionId={SessionId}", session.ImportSessionId);
            await FailAsync(session, "PHÂN_TÍCH_THẤT_BẠI", "Không thể hoàn tất phân tích tệp Excel.", actor, cancellationToken);
            return AIImportOperationResult<AIImportSessionDto>.Fail(500, "PHÂN_TÍCH_THẤT_BẠI", "Không thể hoàn tất phân tích tệp Excel.");
        }
    }

    public async Task<AIImportOperationResult<AIImportSessionDto>> ReanalyzeAsync(int sessionId, AdminActorContext actor, CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, PermissionConstants.AIImportAnalyze);
        if (access != null) return AIImportOperationResult<AIImportSessionDto>.Fail(403, "KHÔNG_CÓ_QUYỀN", access);
        var session = await OwnedSessionAsync(sessionId, actor, true, cancellationToken);
        if (session == null) return NotFound<AIImportSessionDto>();
        if (session.Status is AIImportSessionStatuses.Completed or AIImportSessionStatuses.Importing or AIImportSessionStatuses.Cancelled)
            return Conflict<AIImportSessionDto>("PHIÊN_ĐÃ_XỬ_LÝ", "Phiên không thể phân tích lại.");
        foreach (var group in session.Groups)
        {
            var mapping = Deserialize<Dictionary<string, string?>>(group.MappingJson) ?? new(StringComparer.OrdinalIgnoreCase);
            if (group.EntityType == AIImportEntityType.Unknown || group.Status == AIImportItemStatuses.ReviewRequired)
            {
                var region = RestoreRegion(group);
                var analysis = await _analyzer.AnalyzeAsync(region, null, cancellationToken);
                group.EntityType = analysis.EntityType;
                group.MappingJson = Serialize(analysis.Mapping);
                group.Confidence = analysis.Confidence;
                group.DependencyOrder = DependencyOrder(analysis.EntityType);
                group.Status = analysis.EntityType == AIImportEntityType.Unknown
                    ? AIImportItemStatuses.ReviewRequired
                    : AIImportItemStatuses.Valid;
                mapping = analysis.Mapping;
                if (analysis.UsedAI) session.ModelName = _ollamaModel;
            }
            foreach (var item in group.Items)
            {
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
        AddAudit(session, actor, "REANALYZE", null, session.Status);
        await _db.SaveChangesAsync(cancellationToken);
        return AIImportOperationResult<AIImportSessionDto>.Ok(await BuildSessionDtoAsync(sessionId, null, null, 1, _options.DefaultPageSize, actor, cancellationToken));
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
            return AIImportOperationResult<AIImportSessionDto>.Fail(400, "MAPPING_KHÔNG_HỢP_LỆ", "Mapping chứa entity/field lạ hoặc một cột nguồn được dùng nhiều lần.");
        var session = await OwnedSessionAsync(sessionId, actor, true, cancellationToken);
        if (session == null) return NotFound<AIImportSessionDto>();
        var editable = CheckEditable<AIImportSessionDto>(session, request.ExpectedPreviewVersion);
        if (editable != null) return editable;
        var group = session.Groups.SingleOrDefault(x => x.ImportGroupId == groupId);
        if (group == null) return NotFound<AIImportSessionDto>();
        group.EntityType = request.EntityType;
        group.MappingJson = Serialize(request.Mapping);
        group.DependencyOrder = DependencyOrder(request.EntityType);
        group.Status = AIImportItemStatuses.Valid;
        foreach (var item in group.Items)
        {
            var raw = Deserialize<Dictionary<string, string?>>(item.RawDataJson) ?? new();
            ApplyValidation(item, group.EntityType, ApplyMapping(raw, request.Mapping), group.Confidence, null);
        }
        await ValidateSessionAsync(session, cancellationToken);
        session.PreviewVersion++;
        RefreshCounts(session);
        AddAudit(session, actor, "GROUP_UPDATED", session.Status, session.Status, group.EntityType);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict<AIImportSessionDto>("PREVIEW_ĐÃ_THAY_ĐỔI", "Preview đã được request khác cập nhật; vui lòng tải lại.");
        }
        return AIImportOperationResult<AIImportSessionDto>.Ok(await BuildSessionDtoAsync(sessionId, groupId, null, 1, _options.DefaultPageSize, actor, cancellationToken));
    }

    public async Task<AIImportOperationResult<AIImportSessionDto>> UpdateItemAsync(
        int sessionId, int itemId, AIImportItemPatchRequest request, AdminActorContext actor, CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, PermissionConstants.AIImportAnalyze);
        if (access != null) return AIImportOperationResult<AIImportSessionDto>.Fail(403, "KHÔNG_CÓ_QUYỀN", access);
        if (request.Action is not (AIImportActions.Create or AIImportActions.Skip))
            return AIImportOperationResult<AIImportSessionDto>.Fail(400, "ACTION_KHÔNG_HỢP_LỆ", "Chỉ hỗ trợ CREATE hoặc SKIP.");
        var session = await OwnedSessionAsync(sessionId, actor, true, cancellationToken);
        if (session == null) return NotFound<AIImportSessionDto>();
        var editable = CheckEditable<AIImportSessionDto>(session, request.ExpectedPreviewVersion);
        if (editable != null) return editable;
        var item = session.Groups.SelectMany(x => x.Items).SingleOrDefault(x => x.ImportItemId == itemId);
        if (item == null) return NotFound<AIImportSessionDto>();
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
            item.Status = AIImportItemStatuses.Skipped;
            item.ErrorsJson = "[]";
            item.WarningsJson = "[]";
        }
        else
        {
            ApplyValidation(item, item.Group.EntityType, request.Values, item.Confidence, null);
            item.WarningsAcknowledged = request.WarningsAcknowledged;
            if (supplierWarning != null)
            {
                item.SupplierDuplicateWarningId = supplierWarning.WarningId;
                item.WarningsAcknowledged = true;
                var warnings = Deserialize<List<AIImportErrorDto>>(item.WarningsJson) ?? new();
                warnings.Add(new AIImportErrorDto { Code = "NHÀ_CUNG_CẤP_GẦN_TRÙNG", Message = $"Tìm thấy {supplierWarning.Matches.Count} nhà cung cấp tương tự; lý do override đã được ghi nhận." });
                warnings.AddRange(supplierWarning.Matches.Select(match => new AIImportErrorDto
                {
                    Code = "NHÀ_CUNG_CẤP_TƯƠNG_TỰ",
                    Message = $"{match.Code} · {match.Name} · {string.Join(", ", match.MatchedSignals)}"
                }));
                item.WarningsJson = Serialize(warnings);
                item.Status = AIImportItemStatuses.Warning;
            }
        }
        await ValidateSessionAsync(session, cancellationToken);
        session.PreviewVersion++;
        RefreshCounts(session);
        AddAudit(session, actor, "ITEM_UPDATED", session.Status, session.Status, item.Group.EntityType);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict<AIImportSessionDto>("PREVIEW_ĐÃ_THAY_ĐỔI", "Preview đã được request khác cập nhật; vui lòng tải lại.");
        }
        return AIImportOperationResult<AIImportSessionDto>.Ok(await BuildSessionDtoAsync(sessionId, item.ImportGroupId, null, 1, _options.DefaultPageSize, actor, cancellationToken));
    }

    public async Task<AIImportOperationResult<AIImportConfirmResultDto>> ConfirmAsync(
        int sessionId, string? idempotencyKey, AIImportConfirmRequest request, AdminActorContext actor, CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, PermissionConstants.AIImportConfirm);
        if (access != null) return AIImportOperationResult<AIImportConfirmResultDto>.Fail(403, "KHÔNG_CÓ_QUYỀN", access);
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return AIImportOperationResult<AIImportConfirmResultDto>.Fail(400, "IDEMPOTENCY_KEY_BẮT_BUỘC", "Header Idempotency-Key là bắt buộc.");

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
                return Conflict<AIImportConfirmResultDto>("PREVIEW_ĐÃ_THAY_ĐỔI", "Preview đã thay đổi; vui lòng tải lại.");
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

            var blockers = session.Groups.SelectMany(x => x.Items).Where(x =>
                x.Action != AIImportActions.Skip &&
                (x.Status is AIImportItemStatuses.Error or AIImportItemStatuses.ReviewRequired
                 || (x.Status == AIImportItemStatuses.Warning && !x.WarningsAcknowledged))).ToList();
            if (blockers.Count > 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return AIImportOperationResult<AIImportConfirmResultDto>.Fail(
                    422, "PREVIEW_CHƯA_SẴN_SÀNG", "Còn lỗi, dòng cần xem lại hoặc cảnh báo chưa xác nhận.",
                    BuildBlockerDetails(session));
            }

            var snapshot = ConfirmSnapshot(session);
            var createEntities = session.Groups.SelectMany(x => x.Items.Where(i => i.Action == AIImportActions.Create)).Select(x => x.Group.EntityType).Distinct().ToArray();
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
            foreach (var entity in createEntities)
            {
                var entityPermission = CreatePermission(entity);
                if (entityPermission == null || await RequireAsync(actor, entityPermission) != null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return AIImportOperationResult<AIImportConfirmResultDto>.Fail(403, "KHÔNG_CÓ_QUYỀN_TẠO_ENTITY", $"Tài khoản không có quyền {entityPermission ?? entity.ToString()}.");
                }
            }

            var claimed = await _db.ImportSessions.Where(x => x.ImportSessionId == sessionId && x.Status == AIImportSessionStatuses.ReadyToPreview && x.PreviewVersion == request.ExpectedPreviewVersion)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, AIImportSessionStatuses.Importing).SetProperty(x => x.ConfirmedAtUtc, DateTime.UtcNow), cancellationToken);
            if (claimed != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Conflict<AIImportConfirmResultDto>("PHIÊN_ĐANG_ĐƯỢC_XỬ_LÝ", "Phiên đã được request khác nhận xử lý.");
            }

            session = await OwnedSessionAsync(sessionId, actor, true, cancellationToken)
                ?? throw new InvalidOperationException("Không thể tải lại phiên sau khi claim.");
            var result = new AIImportConfirmResultDto { SessionId = sessionId, Status = AIImportSessionStatuses.Completed };
            foreach (var group in session.Groups.OrderBy(x => x.DependencyOrder).ThenBy(x => x.ImportGroupId))
            foreach (var item in group.Items.OrderBy(x => x.SourceRow))
            {
                if (item.Action == AIImportActions.Skip) { result.Skipped++; continue; }
                var values = Deserialize<Dictionary<string, string?>>(item.NormalizedDataJson) ?? new();
                try
                {
                    item.ImportedEntityId = await CreateEntityAsync(group.EntityType, values, item, actor, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    throw new AIImportRowException(group, item, ex);
                }
                item.Status = AIImportItemStatuses.Imported;
                result.Imported++;
                result.ImportedByEntity[group.EntityType.ToString()] = result.ImportedByEntity.GetValueOrDefault(group.EntityType.ToString()) + 1;
            }
            session.Status = AIImportSessionStatuses.Completed;
            session.CompletedAtUtc = DateTime.UtcNow;
            session.ResultJson = Serialize(result);
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
            .ExecuteUpdateAsync(x => x.SetProperty(s => s.Status, AIImportSessionStatuses.Cancelled), cancellationToken);
        if (affected != 1) return Conflict<AIImportSessionDto>("PREVIEW_ĐÃ_THAY_ĐỔI", "Phiên đã thay đổi hoặc đang được xử lý.");
        var session = await OwnedSessionAsync(sessionId, actor, false, cancellationToken);
        AddAudit(session!, actor, "CANCEL", null, AIImportSessionStatuses.Cancelled);
        await _db.SaveChangesAsync(cancellationToken);
        return AIImportOperationResult<AIImportSessionDto>.Ok(await BuildSessionDtoAsync(sessionId, null, null, 1, _options.DefaultPageSize, actor, cancellationToken));
    }

    public async Task<AIImportOperationResult<AIImportHistoryDto>> GetHistoryAsync(
        int page, int pageSize, AdminActorContext actor, CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, PermissionConstants.AIImportHistory);
        if (access != null) return AIImportOperationResult<AIImportHistoryDto>.Fail(403, "KHÔNG_CÓ_QUYỀN", access);
        (page, pageSize) = Page(page, pageSize);
        var query = _db.ImportSessions.AsNoTracking().Where(x => x.UploadedByAccountId == actor.AccountId).OrderByDescending(x => x.CreatedAtUtc);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => new AIImportHistoryItemDto
        {
            SessionId = x.ImportSessionId, FileName = x.FileName, Status = x.Status, PreviewVersion = x.PreviewVersion,
            TotalRows = x.TotalRows, ImportedRows = x.Groups.SelectMany(g => g.Items).Count(i => i.Status == AIImportItemStatuses.Imported),
            CreatedAtUtc = x.CreatedAtUtc, CompletedAtUtc = x.CompletedAtUtc
        }).ToListAsync(cancellationToken);
        return AIImportOperationResult<AIImportHistoryDto>.Ok(new AIImportHistoryDto { Items = items, Page = PageDto(page, pageSize, total) });
    }

    private ImportItem BuildItem(
        ImportGroup group,
        int row,
        Dictionary<string, string?> raw,
        Dictionary<string, string?> trace,
        Dictionary<string, string?> mapped,
        decimal confidence,
        string? aiError)
    {
        var item = new ImportItem { SourceRow = row, RawDataJson = Serialize(raw), SourceTraceJson = Serialize(trace), Confidence = confidence };
        ApplyValidation(item, group.EntityType, mapped, confidence, aiError);
        return item;
    }

    private void ApplyValidation(ImportItem item, AIImportEntityType entityType, IReadOnlyDictionary<string, string?> values, decimal confidence, string? aiError)
    {
        if (entityType == AIImportEntityType.Unknown)
        {
            item.NormalizedDataJson = Serialize(values);
            item.Status = AIImportItemStatuses.ReviewRequired;
            item.ErrorsJson = Serialize(new[] { new AIImportErrorDto { Code = aiError ?? "KHÔNG_XÁC_ĐỊNH_SCHEMA", Message = "Cần chọn entity và mapping trước khi Confirm." } });
            item.WarningsJson = "[]";
            return;
        }
        var normalized = _schemas.Normalize(entityType, values);
        var errors = _schemas.Validate(entityType, normalized);
        item.NormalizedDataJson = Serialize(normalized);
        item.ErrorsJson = Serialize(errors);
        item.WarningsJson = "[]";
        item.Status = errors.Count > 0 ? AIImportItemStatuses.Error : confidence < _options.ReviewConfidenceThreshold ? AIImportItemStatuses.ReviewRequired : AIImportItemStatuses.Valid;
        item.Action = AIImportActions.Create;
        item.WarningsAcknowledged = false;
    }

    private async Task ValidateSessionAsync(ImportSession session, CancellationToken cancellationToken)
    {
        var all = session.Groups.SelectMany(x => x.Items.Select(i => (Group: x, Item: i))).ToList();
        foreach (var (group, item) in all.Where(x => x.Item.Action == AIImportActions.Create))
        {
            var values = Deserialize<Dictionary<string, string?>>(item.NormalizedDataJson) ?? new();
            if (group.EntityType == AIImportEntityType.Unknown)
            {
                item.Status = AIImportItemStatuses.ReviewRequired;
                item.ErrorsJson = Serialize(new[] { new AIImportErrorDto { Code = "KHÔNG_XÁC_ĐỊNH_SCHEMA", Message = "Cần chọn entity và mapping trước khi Confirm." } });
                item.WarningsJson = "[]";
                continue;
            }

            var normalized = _schemas.Normalize(group.EntityType, values);
            var errors = _schemas.Validate(group.EntityType, normalized);
            item.NormalizedDataJson = Serialize(normalized);
            item.ErrorsJson = Serialize(errors);
            item.WarningsJson = "[]";
            item.Status = errors.Count > 0
                ? AIImportItemStatuses.Error
                : item.Confidence < _options.ReviewConfidenceThreshold
                    ? AIImportItemStatuses.ReviewRequired
                    : AIImportItemStatuses.Valid;
        }

        foreach (var groupItems in all.Where(x => x.Item.Action == AIImportActions.Create && x.Item.Status != AIImportItemStatuses.Error)
                     .GroupBy(x => (x.Group.EntityType, Key: BusinessKey(x.Group.EntityType, Deserialize<Dictionary<string, string?>>(x.Item.NormalizedDataJson) ?? new()))))
        {
            if (string.IsNullOrWhiteSpace(groupItems.Key.Key) || groupItems.Count() <= 1) continue;
            foreach (var duplicate in groupItems.Skip(1))
            {
                duplicate.Item.Action = AIImportActions.Skip;
                duplicate.Item.Status = AIImportItemStatuses.Skipped;
                duplicate.Item.WarningsJson = Serialize(new[] { new AIImportErrorDto { Code = "TRÙNG_TRONG_FILE", Message = "Dòng trùng chắc chắn trong file, mặc định SKIP." } });
            }
        }

        var categoryKeys = await _db.DrinkCategories.AsNoTracking().Select(x => new { x.CategoryCode, x.Name }).ToListAsync(cancellationToken);
        var activeCategoryKeys = await _db.DrinkCategories.AsNoTracking().Where(x => x.Active).Select(x => new { x.CategoryCode, x.Name }).ToListAsync(cancellationToken);
        var drinkKeys = await _db.Drinks.AsNoTracking().Select(x => new { x.DrinkCode, x.Name }).ToListAsync(cancellationToken);
        var sizeKeys = await _db.Sizes.AsNoTracking().Select(x => new { x.SizeCode, x.Name }).ToListAsync(cancellationToken);
        var ingredientKeys = await _db.Ingredients.AsNoTracking().Select(x => new { x.Code, x.Name }).ToListAsync(cancellationToken);
        var supplierKeys = await _db.Suppliers.AsNoTracking().Select(x => new { x.TaxCode, x.Name }).ToListAsync(cancellationToken);
        var units = await _db.Units.AsNoTracking().Where(x => x.Active).Select(x => new { x.UnitCode, x.Name }).ToListAsync(cancellationToken);
        var productTypes = await _db.ProductTypes.AsNoTracking().Where(x => x.Active).Select(x => new { x.Code, x.Name }).ToListAsync(cancellationToken);
        var activeCategories = activeCategoryKeys.Concat(session.Groups.Where(g => g.EntityType == AIImportEntityType.Category).SelectMany(g => g.Items)
            .Where(i => i.Action == AIImportActions.Create && i.Status is AIImportItemStatuses.Valid or AIImportItemStatuses.Warning)
            .Select(i => { var v = Deserialize<Dictionary<string, string?>>(i.NormalizedDataJson) ?? new(); return new { CategoryCode = v.GetValueOrDefault("CategoryCode") ?? "", Name = v.GetValueOrDefault("Name") ?? "" }; })).ToList();

        foreach (var (group, item) in all.Where(x => x.Item.Action == AIImportActions.Create && x.Item.Status is not AIImportItemStatuses.Error and not AIImportItemStatuses.ReviewRequired))
        {
            var values = Deserialize<Dictionary<string, string?>>(item.NormalizedDataJson) ?? new();
            var duplicate = group.EntityType switch
            {
                AIImportEntityType.Category => categoryKeys.Any(x => Same(x.CategoryCode, values.GetValueOrDefault("CategoryCode")) || Same(x.Name, values.GetValueOrDefault("Name"))),
                AIImportEntityType.Drink => drinkKeys.Any(x => Same(x.DrinkCode, values.GetValueOrDefault("DrinkCode")) || Same(x.Name, values.GetValueOrDefault("Name"))),
                AIImportEntityType.Size => sizeKeys.Any(x => Same(x.SizeCode, values.GetValueOrDefault("SizeCode")) || Same(x.Name, values.GetValueOrDefault("Name"))),
                AIImportEntityType.Ingredient => ingredientKeys.Any(x => Same(x.Code, values.GetValueOrDefault("Code")) || Same(x.Name, values.GetValueOrDefault("Name"))),
                AIImportEntityType.Supplier => supplierKeys.Any(x => Same(x.TaxCode, values.GetValueOrDefault("TaxCode")) || Same(x.Name, values.GetValueOrDefault("Name"))),
                _ => false
            };
            if (duplicate)
            {
                item.Action = AIImportActions.Skip;
                item.Status = AIImportItemStatuses.Skipped;
                item.WarningsJson = Serialize(new[] { new AIImportErrorDto { Code = "ĐÃ_TỒN_TẠI", Message = "Bản ghi đã tồn tại, mặc định SKIP." } });
                continue;
            }
            string? referenceError = null;
            if (group.EntityType == AIImportEntityType.Drink)
            {
                var category = activeCategories.FirstOrDefault(x => Same(x.CategoryCode, values.GetValueOrDefault("Category")) || Same(x.Name, values.GetValueOrDefault("Category")));
                var productType = productTypes.FirstOrDefault(x => Same(x.Code, values.GetValueOrDefault("ProductType")) || Same(x.Name, values.GetValueOrDefault("ProductType")));
                if (category == null) referenceError = "Danh mục không tồn tại/không hoạt động và không được tạo trong phiên.";
                else values["Category"] = category.CategoryCode;
                if (productType == null) referenceError ??= "Loại sản phẩm không tồn tại hoặc đã ngừng hoạt động.";
                else values["ProductType"] = productType.Code;
            }
            else if (group.EntityType == AIImportEntityType.Ingredient)
            {
                var unit = units.FirstOrDefault(x => Same(x.UnitCode, values.GetValueOrDefault("BaseUnit")) || Same(x.Name, values.GetValueOrDefault("BaseUnit")));
                if (unit == null) referenceError = "Đơn vị cơ sở không tồn tại hoặc đã ngừng hoạt động.";
                else values["BaseUnit"] = unit.UnitCode;
            }
            item.NormalizedDataJson = Serialize(values);
            if (referenceError != null)
            {
                item.Status = AIImportItemStatuses.Error;
                item.ErrorsJson = Serialize(new[] { new AIImportErrorDto { Code = "REFERENCE_KHÔNG_HỢP_LỆ", Message = referenceError } });
            }
            if (group.EntityType == AIImportEntityType.Supplier)
            {
                var matches = await _suppliers.FindDuplicateMatchesAsync(SupplierDto(values));
                if (matches.Count > 0)
                {
                    item.Status = item.SupplierDuplicateWarningId.HasValue && !string.IsNullOrWhiteSpace(item.DuplicateOverrideReason)
                        ? AIImportItemStatuses.Warning
                        : AIImportItemStatuses.ReviewRequired;
                    var warnings = new List<AIImportErrorDto>
                    {
                        new() { Code = "NHÀ_CUNG_CẤP_GẦN_TRÙNG", Message = $"Có {matches.Count} nhà cung cấp tương tự. Nhập lý do nếu vẫn tạo." }
                    };
                    warnings.AddRange(matches.Select(match => new AIImportErrorDto
                    {
                        Code = "NHÀ_CUNG_CẤP_TƯƠNG_TỰ",
                        Message = $"{match.Code} · {match.Name} · {string.Join(", ", match.MatchedSignals)}"
                    }));
                    item.WarningsJson = Serialize(warnings);
                }
            }
        }
        RefreshCounts(session);
    }

    private async Task<int> CreateEntityAsync(AIImportEntityType entity, Dictionary<string, string?> v, ImportItem item, AdminActorContext actor, CancellationToken cancellationToken)
    {
        switch (entity)
        {
            case AIImportEntityType.Category:
                return (await _categories.CreateCategoryAsync(new AdminCreateCategoryDto { CategoryCode = v.GetValueOrDefault("CategoryCode"), Name = v.GetValueOrDefault("Name")!, Icon = v.GetValueOrDefault("Icon"), Active = !bool.TryParse(v.GetValueOrDefault("Active"), out var active) || active })).CategoryId;
            case AIImportEntityType.Drink:
            {
                var category = await _db.DrinkCategories.FirstAsync(x => x.Active && (x.CategoryCode == v.GetValueOrDefault("Category") || x.Name == v.GetValueOrDefault("Category")), cancellationToken);
                var type = await _db.ProductTypes.AsNoTracking().FirstAsync(x => x.Active && (x.Code == v.GetValueOrDefault("ProductType") || x.Name == v.GetValueOrDefault("ProductType")), cancellationToken);
                return await _drinks.CreateDrinkAsync(new AdminDrinkCreateDTO { DrinkCode = v.GetValueOrDefault("DrinkCode")!, Name = v.GetValueOrDefault("Name")!, Description = v.GetValueOrDefault("Description") ?? "", CategoryId = category.CategoryId, ProductTypeId = type.ProductTypeId, ImageFiles = [] });
            }
            case AIImportEntityType.Size:
            {
                var dto = new SizeDto { SizeCode = v.GetValueOrDefault("SizeCode")!, Name = v.GetValueOrDefault("Name")!, Description = v.GetValueOrDefault("Description") ?? "", SizeType = Enum.Parse<SizeTypeEnum>(v.GetValueOrDefault("SizeType")!, true) };
                var created = await _sizes.CreateSizeAsync(dto);
                if (!created.Success) throw new InvalidOperationException(created.Error);
                return await _db.Sizes.Where(x => x.SizeCode == dto.SizeCode).Select(x => x.SizeId).SingleAsync(cancellationToken);
            }
            case AIImportEntityType.Ingredient:
            {
                var unitValue = v.GetValueOrDefault("BaseUnit");
                var unit = await _db.Units.AsNoTracking().FirstAsync(x => x.Active && (x.UnitCode == unitValue || x.Name == unitValue), cancellationToken);
                return await _ingredients.CreateAsync(new AdminIngredientCreateDTO { Code = v.GetValueOrDefault("Code")!, Name = v.GetValueOrDefault("Name")!, BaseUnitId = unit.UnitId });
            }
            case AIImportEntityType.Supplier:
            {
                var dto = SupplierDto(v);
                dto.DuplicateWarningId = item.SupplierDuplicateWarningId;
                dto.DuplicateOverrideReason = item.DuplicateOverrideReason;
                return await _suppliers.CreateAsync(dto, actor.StaffId);
            }
            default: throw new InvalidOperationException("Entity ngoài phạm vi AI Smart Import MVP.");
        }
    }

    private static AdminSupplierCreateDTO SupplierDto(IReadOnlyDictionary<string, string?> v) => new()
    {
        Name = v.GetValueOrDefault("Name") ?? "", TaxCode = v.GetValueOrDefault("TaxCode"), Address = v.GetValueOrDefault("Address"), Note = v.GetValueOrDefault("Note"),
        PrimaryPhone = v.GetValueOrDefault("PrimaryPhone") ?? "", PrimaryContactName = v.GetValueOrDefault("PrimaryContactName") ?? "",
        PrimaryContactPhone = v.GetValueOrDefault("PrimaryContactPhone"), PrimaryContactEmail = v.GetValueOrDefault("PrimaryContactEmail"), PrimaryContactPosition = v.GetValueOrDefault("PrimaryContactPosition")
    };

    private async Task<ImportSession?> OwnedSessionAsync(int id, AdminActorContext actor, bool tracked, CancellationToken cancellationToken)
    {
        IQueryable<ImportSession> query = _db.ImportSessions;
        if (!tracked) query = query.AsNoTracking();
        return tracked
            ? await query.Include(x => x.Groups).ThenInclude(x => x.Items).SingleOrDefaultAsync(x => x.ImportSessionId == id && x.UploadedByAccountId == actor.AccountId, cancellationToken)
            : await query.SingleOrDefaultAsync(x => x.ImportSessionId == id && x.UploadedByAccountId == actor.AccountId, cancellationToken);
    }

    private async Task<AIImportSessionDto> BuildSessionDtoAsync(int id, int? groupId, string? status, int page, int pageSize, AdminActorContext actor, CancellationToken cancellationToken)
    {
        (page, pageSize) = Page(page, pageSize);
        var session = await _db.ImportSessions.AsNoTracking().Include(x => x.Groups).SingleAsync(x => x.ImportSessionId == id && x.UploadedByAccountId == actor.AccountId, cancellationToken);
        var itemQuery = _db.ImportItems.AsNoTracking().Where(x => x.Group.ImportSessionId == id);
        if (groupId.HasValue) itemQuery = itemQuery.Where(x => x.ImportGroupId == groupId.Value);
        if (!string.IsNullOrWhiteSpace(status)) itemQuery = itemQuery.Where(x => x.Status == status);
        var total = await itemQuery.CountAsync(cancellationToken);
        var items = await OrderPreviewItems(itemQuery)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var itemMap = items.GroupBy(x => x.ImportGroupId).ToDictionary(x => x.Key, x => x.Select(ItemDto).ToList());
        return new AIImportSessionDto
        {
            SessionId = session.ImportSessionId, FileName = session.FileName, Status = session.Status, AnalysisVersion = session.AnalysisVersion,
            PreviewVersion = session.PreviewVersion, CreatedAtUtc = session.CreatedAtUtc, ExpiresAtUtc = session.ExpiresAtUtc,
            FailureCode = session.FailureCode, FailureMessage = session.FailureMessage,
            AnalysisWarnings = Deserialize<List<AIImportErrorDto>>(session.AnalysisWarningsJson) ?? new(),
            Summary = Summary(session), Page = PageDto(page, pageSize, total),
            Groups = session.Groups.OrderBy(x => x.DependencyOrder).ThenBy(x => x.ImportGroupId).Select(x => new AIImportGroupDto
            {
                GroupId = x.ImportGroupId, SheetName = x.SheetName, RegionAddress = x.RegionAddress, HeaderRow = x.HeaderRow,
                EntityType = x.EntityType, Mapping = Deserialize<Dictionary<string, string?>>(x.MappingJson) ?? new(),
                SourceHeaders = Deserialize<List<string>>(x.SourceHeadersJson) ?? new(), DependencyOrder = x.DependencyOrder,
                Confidence = x.Confidence, Status = x.Status, Items = itemMap.GetValueOrDefault(x.ImportGroupId) ?? new()
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
            if (!decision.IsSuccess || decision.Data?.Allowed != true) return $"Thiếu quyền {permission}.";
        }
        return null;
    }

    private async Task TransitionAsync(ImportSession session, string expected, string next, AdminActorContext actor, string action, CancellationToken cancellationToken)
    {
        if (session.Status != expected) throw new InvalidOperationException($"Chuyển trạng thái không hợp lệ: {session.Status} → {next}.");
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
            .ExecuteUpdateAsync(x => x.SetProperty(s => s.Status, AIImportSessionStatuses.Expired), cancellationToken);
    }

    private static void AddAudit(ImportSession session, AdminActorContext actor, string action, string? before, string? after, AIImportEntityType? entity = null, string? keyHash = null, string? result = null, string? error = null) =>
        session.Audits.Add(new ImportAudit { StaffId = actor.StaffId, AccountId = actor.AccountId, Action = action, StatusBefore = before, StatusAfter = after, EntityType = entity,
            PromptVersion = session.PromptVersion, SchemaVersion = session.SchemaVersion, PreviewVersion = session.PreviewVersion, IdempotencyKeyHash = keyHash, ResultSummaryJson = result, ErrorCode = error, CreatedAtUtc = DateTime.UtcNow });

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
    private static int DependencyOrder(AIImportEntityType entity) => entity == AIImportEntityType.Category ? 10 : entity == AIImportEntityType.Drink ? 20 : 30;
    private static string? CreatePermission(AIImportEntityType entity) => entity switch { AIImportEntityType.Category => PermissionConstants.CategoryCreate, AIImportEntityType.Drink => PermissionConstants.DrinkCreate, AIImportEntityType.Size => PermissionConstants.SizeCreate, AIImportEntityType.Ingredient => PermissionConstants.IngredientCreate, AIImportEntityType.Supplier => PermissionConstants.SupplierCreate, _ => null };
    private static string BusinessKey(AIImportEntityType entity, IReadOnlyDictionary<string, string?> v) => AIImportSchemaRegistry.Key(entity switch { AIImportEntityType.Category => v.GetValueOrDefault("CategoryCode") ?? v.GetValueOrDefault("Name"), AIImportEntityType.Drink => v.GetValueOrDefault("DrinkCode") ?? v.GetValueOrDefault("Name"), AIImportEntityType.Size => v.GetValueOrDefault("SizeCode") ?? v.GetValueOrDefault("Name"), AIImportEntityType.Ingredient => v.GetValueOrDefault("Code") ?? v.GetValueOrDefault("Name"), AIImportEntityType.Supplier => v.GetValueOrDefault("TaxCode") ?? v.GetValueOrDefault("Name"), _ => null });
    private static bool Same(string? left, string? right) => AIImportSchemaRegistry.Key(left) == AIImportSchemaRegistry.Key(right) && !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right);
    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
    private static T? Deserialize<T>(string value) { try { return JsonSerializer.Deserialize<T>(value, JsonOptions); } catch (JsonException) { return default; } }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static (int Page, int PageSize) Page(int page, int pageSize) => (Math.Max(1, page), Math.Clamp(pageSize <= 0 ? 50 : pageSize, 1, 200));
    private static AIImportPageDto PageDto(int page, int size, int total) => new() { Page = page, PageSize = size, TotalItems = total, TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)size)) };
    private static AIImportSummaryDto Summary(ImportSession x) => new() { TotalGroups = x.TotalGroups, TotalRows = x.TotalRows, Valid = x.ValidRows, Warnings = x.WarningRows, Errors = x.ErrorRows, ReviewRequired = x.ReviewRows, Skipped = x.SkippedRows };
    private static AIImportItemDto ItemDto(ImportItem x) => new() { ItemId = x.ImportItemId, SourceRow = x.SourceRow, RawData = Deserialize<Dictionary<string, string?>>(x.RawDataJson) ?? new(), NormalizedData = Deserialize<Dictionary<string, string?>>(x.NormalizedDataJson) ?? new(), SourceTrace = Deserialize<Dictionary<string, string?>>(x.SourceTraceJson) ?? new(), Status = x.Status, Action = x.Action, Errors = Deserialize<List<AIImportErrorDto>>(x.ErrorsJson) ?? new(), Warnings = Deserialize<List<AIImportErrorDto>>(x.WarningsJson) ?? new(), WarningsAcknowledged = x.WarningsAcknowledged, DuplicateOverrideReason = x.DuplicateOverrideReason, ImportedEntityId = x.ImportedEntityId };
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
        .Select(x => new ConfirmSnapshotItem(x.ImportItemId, x.Action, x.NormalizedDataJson, x.WarningsAcknowledged, x.DuplicateOverrideReason))
        .ToArray();
    private static string ValidationFingerprint(ImportSession session) => Serialize(session.Groups
        .OrderBy(x => x.ImportGroupId).SelectMany(x => x.Items.OrderBy(i => i.ImportItemId))
        .Select(x => new { x.ImportItemId, x.Action, x.Status, x.NormalizedDataJson, x.ErrorsJson, x.WarningsJson, x.WarningsAcknowledged, x.DuplicateOverrideReason, x.SupplierDuplicateWarningId })
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
                    Position = new AIImportPositionDto
                    {
                        Sheet = group.SheetName,
                        Region = group.RegionAddress,
                        Row = item.SourceRow,
                        Column = issue.Position?.Column
                    }
                });
            }))
        .OrderBy(x => x.Position?.Sheet).ThenBy(x => x.Position?.Row).ToList();
    private static AIImportErrorDto RowError(ImportGroup group, ImportItem item, string code, string message) => new()
    {
        ItemId = item.ImportItemId,
        Code = code,
        Message = message,
        Position = new AIImportPositionDto { Sheet = group.SheetName, Region = group.RegionAddress, Row = item.SourceRow }
    };
    private void DetachSessionGraph(ImportSession session)
    {
        foreach (var item in session.Groups.SelectMany(x => x.Items)) _db.Entry(item).State = EntityState.Detached;
        foreach (var group in session.Groups) _db.Entry(group).State = EntityState.Detached;
        _db.Entry(session).State = EntityState.Detached;
    }
    private static AIImportOperationResult<T> NotFound<T>() => AIImportOperationResult<T>.Fail(404, "KHÔNG_TÌM_THẤY_PHIÊN", "Không tìm thấy phiên thuộc tài khoản hiện tại.");
    private static AIImportOperationResult<T> Conflict<T>(string code, string message) => AIImportOperationResult<T>.Fail(409, code, message);
    private static AIImportOperationResult<T>? CheckEditable<T>(ImportSession session, int expected) => session.Status != AIImportSessionStatuses.ReadyToPreview ? Conflict<T>("PHIÊN_KHÔNG_THỂ_SỬA", "Phiên không ở trạng thái Preview.") : session.PreviewVersion != expected ? Conflict<T>("PREVIEW_ĐÃ_THAY_ĐỔI", "Preview đã thay đổi; vui lòng tải lại.") : null;
    private static string BusinessErrorCode(Exception ex) => ex is SupplierDomainException supplier ? supplier.Code : FindSql(ex) is { Number: 2601 or 2627 } ? "DỮ_LIỆU_ĐÃ_TỒN_TẠI" : ex is DuplicateDataException ? "DỮ_LIỆU_ĐÃ_TỒN_TẠI" : "IMPORT_THẤT_BẠI";
    private static string BusinessMessage(Exception ex) => FindSql(ex) is { Number: 2601 or 2627 } ? "Dữ liệu nghiệp vụ đã tồn tại; toàn bộ phiên đã được rollback." : ex is SupplierDomainException or DuplicateDataException or InvalidOperationException or ArgumentException ? ex.Message : "Không thể tạo dữ liệu; toàn bộ phiên đã được rollback.";
    private static SqlException? FindSql(Exception ex) { for (var current = ex; current != null; current = current.InnerException!) if (current is SqlException sql) return sql; return null; }
    private sealed record ConfirmSnapshotItem(int ImportItemId, string Action, string NormalizedDataJson, bool WarningsAcknowledged, string? DuplicateOverrideReason);
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
