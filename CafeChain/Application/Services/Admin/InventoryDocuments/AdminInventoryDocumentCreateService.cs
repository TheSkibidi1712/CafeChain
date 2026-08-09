using CafeChain.Application.DTOs.Admin.InventoryDocuments.Create;
using CafeChain.Application.DTOs.Systems;
using CafeChain.Application.Interfaces.Admin.InventoryDocuments;
using CafeChain.Application.Interfaces.Systems;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Costing;
using CafeChain.Models.Inventories.Documents;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Suppliers;
using CafeChain.Models.Inventories.Transfers;
using CafeChain.Models.Inventories.Approvals;
using CafeChain.ViewModels.Admin.InventoryDocuments.Create;
using CafeChain.Infrastrusture.Interfaces.Admin.InventoryDocuments;
using System.Globalization;

namespace CafeChain.Application.Services.Admin.InventoryDocuments
{
    public class AdminInventoryDocumentCreateService : IAdminInventoryDocumentCreateService
    {
        private readonly IAdminInventoryDocumentRepository _repository;

        private readonly IAdminInventoryDocumentValidationService _validationService;

        private readonly IAdminInventoryDocumentConfirmService _confirmService;

        private readonly IHttpContextAccessor _httpContextAccessor;

        private readonly IRequestDeduplicationService _deduplicationService;
        private readonly IInventoryIssuePolicy _inventoryIssuePolicy;
        private readonly IInventoryIssueSettingsProvider _inventoryIssueSettingsProvider;
        private readonly IAdminActorContextAccessor _actorAccessor;
        private readonly IScopeAuthorizationService _scopeAuthorization;
        private readonly IUnitConversionService? _unitConversionService;
        private readonly IIngredientSupplierPackageValidator? _packageEligibility;

        public AdminInventoryDocumentCreateService(
            IAdminInventoryDocumentRepository repository,
            IAdminInventoryDocumentValidationService validationService,
            IAdminInventoryDocumentConfirmService confirmService,
            IHttpContextAccessor httpContextAccessor,
            IRequestDeduplicationService deduplicationService,
            IInventoryIssuePolicy inventoryIssuePolicy,
            IInventoryIssueSettingsProvider inventoryIssueSettingsProvider,
            IAdminActorContextAccessor actorAccessor,
            IScopeAuthorizationService scopeAuthorization,
            IUnitConversionService? unitConversionService = null,
            IIngredientSupplierPackageValidator? packageEligibility = null)
        {
            _repository = repository;
            _validationService = validationService;
            _confirmService = confirmService;
            _httpContextAccessor = httpContextAccessor;
            _deduplicationService = deduplicationService;
            _inventoryIssuePolicy = inventoryIssuePolicy;
            _inventoryIssueSettingsProvider = inventoryIssueSettingsProvider;
            _actorAccessor = actorAccessor;
            _scopeAuthorization = scopeAuthorization;
            _unitConversionService = unitConversionService;
            _packageEligibility = packageEligibility;
        }

        // =====================================================
        // CREATE PAGE
        // =====================================================
        public async Task<AdminInventoryDocumentCreateVM> GetCreateDataAsync(InventoryDocumentType type)
        {
            var actor = GetActor();
            var allowedStoreIds = actor.StaffId > 0
                ? (await _scopeAuthorization.GetAllowedStoresAsync(actor.StaffId))
                    .Select(x => x.StoreId)
                    .ToHashSet()
                : [];
            var stores = (await _repository.GetStoreDropdownAsync())
                .Where(x => allowedStoreIds.Contains(x.StoreId))
                .ToList();
            var effectiveType = 
                    type == InventoryDocumentType.ADJUSTMENT_IN 
                    ? InventoryDocumentType.IMPORT 
                    : type;

            var purpose = 
                    type == InventoryDocumentType.ADJUSTMENT_IN
                    ? InventoryDocumentPurpose.IMPORT_ADJUSTMENT
                    : effectiveType == InventoryDocumentType.IMPORT
                    ? InventoryDocumentPurpose.IMPORT_PURCHASE
                    : InventoryDocumentPurpose.NONE;
            var negativeInventoryPolicy = effectiveType == InventoryDocumentType.EXPORT
                ? await _inventoryIssueSettingsProvider.GetManualExternalExportSettingsAsync()
                : null;

            return new AdminInventoryDocumentCreateVM
            {
                Type = effectiveType,
                Purpose = purpose,
                DocumentDate = DateTime.Now,
                Code = await _repository.GenerateDocumentCodeAsync( effectiveType, purpose == InventoryDocumentPurpose.NONE ? null : purpose),
                Stores = stores,
                Suppliers = [],
                Summary = new InventoryCreateSummaryDTO(),
                NegativeInventoryPolicyValid = negativeInventoryPolicy?.IsValid == true,
                NegativeInventoryPolicyEnabled = negativeInventoryPolicy?.Enabled == true,
                NegativeInventoryApprovalRequired = negativeInventoryPolicy?.ApprovalRequired == true
            };
        }

        public async Task<List<CafeChain.ViewModels.Admin.InventoryDocuments.Dropdown.SupplierDropdownVM>> GetSuppliersAsync(int storeId)
        {
            if (storeId <= 0)
                return [];

            await EnsureStoreScopeAsync(storeId);
            return await _repository.GetSupplierDropdownAsync(storeId);
        }

        public async Task<List<SupplierIngredientDTO>> GetSupplierIngredientsAsync(int supplierId, int storeId)
        {
            if (supplierId <= 0 || storeId <= 0)
                return [];

            await EnsureStoreScopeAsync(storeId);
            var ingredients = await _repository.GetSupplierIngredientsAsync(supplierId, storeId);
            if (_packageEligibility != null)
            {
                var readiness = await _packageEligibility.EvaluateReadinessAsync(ingredients);
                ingredients = ingredients.Where(x =>
                    readiness.TryGetValue(x.IngredientSupplierId, out var result)
                    && result.IsReady
                    && x.Active).ToList();
            }
            var result = new List<SupplierIngredientDTO>();
            foreach (var ingredient in ingredients)
                result.Add(await MapSupplierIngredientDtoAsync(ingredient));
            return result;
        }

        public async Task<List<SupplierIngredientDTO>> GetActiveIngredientsAsync(int storeId, InventoryDocumentPurpose purpose)
        {
            if (storeId <= 0)
            {
                return [];
            }

            await EnsureStoreScopeAsync(storeId);

            var ingredients = await _repository.GetActiveIngredientsAsync();
            var inventories = await _repository.GetStoreInventoriesAsync(storeId);
            var ingredientIds = ingredients.Select(x => x.IngredientId).ToList();
            var costLayers = await _repository.GetAvailableCostLayersAsync(storeId, ingredientIds);
            var supplierPrices = await _repository.GetActiveIngredientSuppliersByIngredientIdsAsync(ingredientIds);
            var inventoryByIngredient = inventories
                .Where(x => x.IngredientId.HasValue)
                .GroupBy(x => x.IngredientId!.Value)
                .ToDictionary(x => x.Key, x => x.First());

            var priceLookup = await BuildPriceLookupAsync(costLayers, supplierPrices);

            var result = new List<SupplierIngredientDTO>();
            foreach (var ingredient in ingredients)
            {
                inventoryByIngredient.TryGetValue(ingredient.IngredientId, out var inventory);
                result.Add(await BuildStoreIngredientDtoAsync(
                    ingredient,
                    inventory?.AvailableQty ?? 0,
                    priceLookup,
                    isPriceLocked: false,
                    isQuantityLocked: false));
            }
            return result;
        }

        public async Task<List<SupplierIngredientDTO>> GetStoreInventoryIngredientsAsync(
            int storeId,
            InventoryDocumentType type,
            InventoryDocumentPurpose purpose)
        {
            if (storeId <= 0)
            {
                return [];
            }

            await EnsureStoreScopeAsync(storeId);

            var inventories = await _repository.GetStoreInventoriesAsync(storeId);

            var requiresPositiveQuantity =
                type == InventoryDocumentType.WASTE
                || type == InventoryDocumentType.EXPORT
                    && purpose == InventoryDocumentPurpose.ADJUSTMENT_OUT;
            var isSupportedSource =
                type == InventoryDocumentType.STOCK_TAKE
                || type == InventoryDocumentType.WASTE
                || type == InventoryDocumentType.EXPORT;

            if (!isSupportedSource)
            {
                return [];
            }

            var availableInventories = inventories
                .Where(x => x.IngredientId.HasValue
                    && x.Ingredient != null
                    && x.Ingredient.Active)
                .Where(x => !requiresPositiveQuantity || x.AvailableQty > 0)
                .GroupBy(x => x.IngredientId!.Value)
                .Select(x => x.First())
                .Select(x => new { Ingredient = x.Ingredient, Quantity = x.AvailableQty })
                .ToList();

            var ingredientIds =
                availableInventories
                    .Select(x => x.Ingredient.IngredientId)
                    .Distinct()
                    .ToList();

            var costLayers =  await _repository.GetAvailableCostLayersAsync(storeId, ingredientIds);

            var supplierPrices = await _repository.GetActiveIngredientSuppliersByIngredientIdsAsync(ingredientIds);

            var priceLookup = await BuildPriceLookupAsync(costLayers, supplierPrices);

            var result = new List<SupplierIngredientDTO>();
            foreach (var source in availableInventories.OrderBy(x => x.Ingredient.Name))
            {
                result.Add(await BuildStoreIngredientDtoAsync(
                    source.Ingredient,
                    source.Quantity,
                    priceLookup,
                    isPriceLocked: true,
                    isQuantityLocked: false));
            }
            return result;
        }

        public async Task<InventoryCreateSummaryDTO> CalculateSummaryAsync(CreateInventoryDocumentDTO dto)
        {
            NormalizeImportDocumentType(dto);
            NormalizeNegativeStockRequest(dto);
            await EnsureStoreScopeAsync(dto.StoreId);

            await NormalizeCreateDetailsAsync(dto);

            return await BuildSummaryAsync(dto);
        }

        public async Task<InventoryDocumentPreflightResultDTO> PreflightAsync(CreateInventoryDocumentDTO dto)
        {
            NormalizeImportDocumentType(dto);
            NormalizeNegativeStockRequest(dto);
            await EnsureStoreScopeAsync(dto.StoreId);
            await NormalizeCreateDetailsAsync(dto);
            await _validationService.ValidateCreateAsync(dto);
            return await EvaluateDtoIssuesAsync(dto);
        }

        // =====================================================
        // CREATE METHODS
        // =====================================================

        public async Task<int> SaveDraftAsync(CreateInventoryDocumentDTO dto)
        {
            NormalizeImportDocumentType(dto);
            NormalizeNegativeStockRequest(dto);
            await EnsureStoreScopeAsync(dto.StoreId);

            EnsureRequestKey(dto.RequestKey);

            await ApplySupplierPartnerSnapshotAsync(dto);

            await NormalizeCreateDetailsAsync(dto);

            await _validationService.ValidateCreateAsync(dto);

            var preflight = await EvaluateDtoIssuesAsync(dto);
            var blockedLine = preflight.Lines.FirstOrDefault(x => x.Outcome == InventoryIssueOutcome.Blocked);
            if (blockedLine != null)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(blockedLine.UserMessage)
                        ? "Không thể lưu phiếu vì dữ liệu tồn kho không hợp lệ."
                        : blockedLine.UserMessage);
            }

            if (dto.Details == null || !dto.Details.Any())
            {
                throw new Exception("Phiếu phải có ít nhất 1 nguyên liệu.");
            }

            await _repository.BeginTransactionAsync();

            try
            {
                var dedup = await _deduplicationService.BeginAsync(
                    dto.RequestKey,
                    dto.DocumentId.HasValue
                        ? "InventoryDocument.UpdateDraft"
                        : "InventoryDocument.CreateDraft",
                    GetCurrentStaffId(),
                    dto);

                if (!dedup.CanProcess)
                {
                    await _repository.RollbackTransactionAsync();

                    if (dedup.Status == "SUCCESS" && dedup.ReferenceId.HasValue)
                    {
                        return dedup.ReferenceId.Value;
                    }

                    throw BuildDeduplicationException(dedup);
                }

                var summary = await BuildSummaryAsync(dto);

                if (dto.DocumentId.HasValue)
                {
                    var existingDraft = await _repository.GetDocumentForConfirmAsync(dto.DocumentId.Value)
                        ?? throw new InvalidOperationException("Không tìm thấy phiếu nháp cần cập nhật.");
                    await EnsureStoreScopeAsync(existingDraft.StoreId);
                    if (existingDraft.Status != InventoryDocumentStatus.DRAFT)
                        throw new InvalidOperationException("Chỉ được cập nhật phiếu DRAFT.");
                    EnsureRowVersionMatches(existingDraft.RowVersion, dto.RowVersion);

                    var oldDetails = existingDraft.Details.ToList();
                    _repository.RemoveDocumentDetails(oldDetails);
                    existingDraft.Details.Clear();

                    existingDraft.StoreId = dto.StoreId;
                    existingDraft.DocumentDate = dto.DocumentDate;
                    existingDraft.Type = dto.Type;
                    existingDraft.Purpose = dto.Purpose;
                    existingDraft.RequestKey = dto.RequestKey;
                    existingDraft.PartnerType = dto.PartnerType;
                    existingDraft.PartnerId = dto.PartnerId;
                    existingDraft.PartnerName = dto.PartnerName;
                    existingDraft.SupplierId = dto.SupplierId;
                    existingDraft.Note = dto.Note;
                    existingDraft.AllowNegativeStock = dto.AllowNegativeStock;
                    existingDraft.NegativeReason = dto.NegativeReason;
                    existingDraft.TotalAmount = summary.TotalAmount;
                    existingDraft.VatAmount = summary.VatAmount;
                    existingDraft.FinalAmount = summary.FinalAmount;
                    _repository.UpdateDocument(existingDraft);
                    await _repository.AddDocumentDetailsAsync(
                        BuildDocumentDetails(existingDraft.InventoryDocumentId, dto));
                    await _repository.SaveChangesAsync();

                    await _deduplicationService.MarkSuccessAsync(
                        dedup.Entry!, existingDraft.InventoryDocumentId,
                        new { documentId = existingDraft.InventoryDocumentId });
                    await _repository.CommitTransactionAsync();
                    return existingDraft.InventoryDocumentId;
                }

                var document = await BuildDraftDocument(dto, summary);

                await _repository.AddDocumentAsync(document);

                await _repository.SaveChangesAsync();

                var details = BuildDocumentDetails(document.InventoryDocumentId, dto);

                await _repository.AddDocumentDetailsAsync(details);

                await _repository.SaveChangesAsync();

                await _deduplicationService.MarkSuccessAsync(
                    dedup.Entry!,
                    document.InventoryDocumentId,
                    new { documentId = document.InventoryDocumentId });

                await _repository.CommitTransactionAsync();

                return document.InventoryDocumentId;
            }
            catch
            {
                await _repository.RollbackTransactionAsync();

                throw;
            }
        }

        public async Task<InventoryDocumentMutationResultDTO> CreateAndConfirmAsync(CreateInventoryDocumentDTO dto)
        {
            if (dto.DocumentId.HasValue)
            {
                if (string.IsNullOrWhiteSpace(dto.RowVersion))
                    throw new InvalidOperationException("ROW_VERSION_REQUIRED");
                var result = await ConfirmDraftAsync(dto.DocumentId.Value, dto.RequestKey, dto.RowVersion);
                return result ?? throw new InvalidOperationException("Không tìm thấy phiếu nháp cần submit.");
            }

            NormalizeImportDocumentType(dto);
            NormalizeNegativeStockRequest(dto);
            await EnsureStoreScopeAsync(dto.StoreId);

            EnsureRequestKey(dto.RequestKey);

            await ApplySupplierPartnerSnapshotAsync(dto);

            await _repository.BeginTransactionAsync();

            try
            {
                var dedup = await _deduplicationService.BeginAsync(
                    dto.RequestKey,
                    GetCreateActionName(dto),
                    GetCurrentStaffId(),
                    dto);

                if (!dedup.CanProcess)
                {
                    await _repository.RollbackTransactionAsync();

                    if (dedup.Status == "SUCCESS" && dedup.ReferenceId.HasValue)
                    {
                        return new InventoryDocumentMutationResultDTO
                        {
                            DocumentId = dedup.ReferenceId.Value
                        };
                    }

                    throw BuildDeduplicationException(dedup);
                }

                await NormalizeCreateDetailsAsync(dto);

                await _validationService.ValidateCreateAsync(dto);

                var document = await CreateDocumentAsync(dto);

                await CreateDetailsAsync(document.InventoryDocumentId, dto);

                document = await _repository.GetDocumentForConfirmAsync(document.InventoryDocumentId) ?? throw new Exception("Không tìm thấy chứng từ.");

                var preflight = await EvaluateDocumentIssuesAsync(document, null, scopeAuthorized: false);
                var blocked = preflight.FirstOrDefault(x => x.Decision.Outcome == InventoryIssueOutcome.Blocked);
                if (blocked != null)
                    throw new InvalidOperationException(BuildIssueUserMessage(blocked));

                var requiresApproval = preflight.Where(x => x.Decision.Outcome == InventoryIssueOutcome.ApprovalRequired).ToList();
                if (requiresApproval.Count > 0)
                {
                    var approval = BuildApproval(document, requiresApproval, dedup.Entry!.PayloadHash, dto.RequestKey!);
                    await _repository.AddNegativeApprovalAsync(approval);
                    await _repository.SaveChangesAsync();

                    var pendingResponse = new InventoryDocumentMutationResultDTO
                    {
                        DocumentId = document.InventoryDocumentId,
                        Status = InventoryDocumentStatus.PENDING,
                        ApprovalId = approval.InventoryNegativeApprovalId
                    };
                    await _deduplicationService.MarkSuccessAsync(dedup.Entry!, document.InventoryDocumentId, pendingResponse);
                    await _repository.CommitTransactionAsync();
                    return pendingResponse;
                }

                var processResult = await _confirmService.ConfirmDocumentAsync(document, GetCurrentStaffId());

                await _repository.SaveChangesAsync();

                var response = new InventoryDocumentMutationResultDTO
                {
                    DocumentId = document.InventoryDocumentId,
                    Status = InventoryDocumentStatus.CONFIRMED,
                    Warnings = processResult.Warnings
                };

                await _deduplicationService.MarkSuccessAsync(
                    dedup.Entry!,
                    document.InventoryDocumentId,
                    response);

                await _repository.CommitTransactionAsync();

                return response;
            }
            catch
            {
                await _repository.RollbackTransactionAsync();
                throw;
            }
        }

        public async Task<InventoryDocumentMutationResultDTO?> ConfirmDraftAsync(
            int documentId,
            string? requestKey,
            string? rowVersion = null)
        {
            EnsureRequestKey(requestKey);

            RequestDeduplicationBeginResult? dedup = null;

            await _repository.BeginTransactionAsync();

            try
            {
                var document = await _repository.GetDocumentForConfirmAsync(documentId);

                if (document == null)
                {
                    await _repository.RollbackTransactionAsync();

                    return null;
                }

                await EnsureStoreScopeAsync(document.StoreId);
                if (!string.IsNullOrWhiteSpace(rowVersion))
                    EnsureRowVersionMatches(document.RowVersion, rowVersion);

                dedup = await _deduplicationService.BeginAsync(
                    requestKey,
                    GetConfirmActionName(document),
                    GetCurrentStaffId(),
                    new { documentId, requestKey },
                    documentId);

                if (!dedup.CanProcess)
                {
                    await _repository.RollbackTransactionAsync();

                    if (dedup.Status == "SUCCESS" && dedup.ReferenceId.HasValue)
                    {
                        return new InventoryDocumentMutationResultDTO
                        {
                            DocumentId = dedup.ReferenceId.Value,
                            Status = document.Status
                        };
                    }

                    throw BuildDeduplicationException(dedup);
                }

                if (document.Status == InventoryDocumentStatus.CANCELLED)
                {
                    throw new InvalidOperationException("Phiếu đã hủy, không thể xác nhận.");
                }

                if (document.Status == InventoryDocumentStatus.CONFIRMED)
                {
                    var alreadyConfirmedResponse = new InventoryDocumentMutationResultDTO
                    {
                        DocumentId = document.InventoryDocumentId,
                        Status = InventoryDocumentStatus.CONFIRMED
                    };

                    await _deduplicationService.MarkSuccessAsync(
                        dedup.Entry!,
                        document.InventoryDocumentId,
                        alreadyConfirmedResponse);

                    await _repository.CommitTransactionAsync();

                    return alreadyConfirmedResponse;
                }

                if (document.Status != InventoryDocumentStatus.DRAFT && document.Status != InventoryDocumentStatus.PENDING)
                {
                    throw new InvalidOperationException("Trạng thái phiếu không hợp lệ để xác nhận.");
                }

                var existingApproval = await _repository.GetNegativeApprovalForUpdateAsync(document.InventoryDocumentId);
                if (existingApproval?.Status == InventoryNegativeApprovalStatuses.Requested)
                {
                    var pendingReplay = new InventoryDocumentMutationResultDTO
                    {
                        DocumentId = document.InventoryDocumentId,
                        Status = InventoryDocumentStatus.PENDING,
                        ApprovalId = existingApproval.InventoryNegativeApprovalId
                    };
                    await _deduplicationService.MarkSuccessAsync(dedup.Entry!, document.InventoryDocumentId, pendingReplay);
                    await _repository.CommitTransactionAsync();
                    return pendingReplay;
                }

                var preflight = await EvaluateDocumentIssuesAsync(document, null, scopeAuthorized: false);
                var blocked = preflight.FirstOrDefault(x => x.Decision.Outcome == InventoryIssueOutcome.Blocked);
                if (blocked != null)
                    throw new InvalidOperationException(BuildIssueUserMessage(blocked));
                var approvalLines = preflight.Where(x => x.Decision.Outcome == InventoryIssueOutcome.ApprovalRequired).ToList();
                if (approvalLines.Count > 0)
                {
                    var approval = BuildApproval(document, approvalLines, dedup.Entry!.PayloadHash, requestKey!);
                    await _repository.AddNegativeApprovalAsync(approval);
                    document.Status = InventoryDocumentStatus.PENDING;
                    _repository.UpdateDocument(document);
                    await _repository.SaveChangesAsync();
                    var pending = new InventoryDocumentMutationResultDTO
                    {
                        DocumentId = document.InventoryDocumentId,
                        Status = InventoryDocumentStatus.PENDING,
                        ApprovalId = approval.InventoryNegativeApprovalId
                    };
                    await _deduplicationService.MarkSuccessAsync(dedup.Entry!, document.InventoryDocumentId, pending);
                    await _repository.CommitTransactionAsync();
                    return pending;
                }

                var processResult = await _confirmService.ConfirmDocumentAsync(document, GetCurrentStaffId());

                await _repository.SaveChangesAsync();

                var response = new InventoryDocumentMutationResultDTO
                {
                    DocumentId = document.InventoryDocumentId,
                    Status = InventoryDocumentStatus.CONFIRMED,
                    Warnings = processResult.Warnings
                };

                await _deduplicationService.MarkSuccessAsync(
                    dedup.Entry!,
                    document.InventoryDocumentId,
                    response);

                await _repository.CommitTransactionAsync();

                return response;
            }
            catch (Exception ex)
            {
                await MarkFailedIfPossibleAsync(dedup, ex.Message);

                await _repository.RollbackTransactionAsync();

                throw;
            }
        }

        public async Task<InventoryDocumentMutationResultDTO> ApproveNegativeAsync(int documentId, string? reviewNote)
        {
            await _repository.BeginTransactionAsync();
            try
            {
                var document = await _repository.GetDocumentForConfirmAsync(documentId)
                    ?? throw new InvalidOperationException("Không tìm thấy phiếu kho.");
                await EnsureStoreScopeAsync(document.StoreId);
                var actor = GetActor();
                EnsureCanApprove(actor.RoleNames);

                var approval = await _repository.GetNegativeApprovalForUpdateAsync(documentId)
                    ?? throw new InvalidOperationException("Không tìm thấy yêu cầu phê duyệt.");
                if (approval.Status != InventoryNegativeApprovalStatuses.Requested)
                    throw new InvalidOperationException("Yêu cầu phê duyệt xuất âm không còn ở trạng thái chờ duyệt.");
                if (approval.RequesterStaffId == actor.StaffId)
                    throw new InvalidOperationException("Người tạo yêu cầu không được tự phê duyệt phiếu xuất âm của mình.");

                approval.ApproverStaffId = actor.StaffId;
                approval.Status = InventoryNegativeApprovalStatuses.Approved;
                approval.ReviewNote = string.IsNullOrWhiteSpace(reviewNote) ? null : reviewNote.Trim();
                approval.ReviewedAt = DateTime.UtcNow;
                approval.ScopeAuthorized = true;
                document.NegativeApproval = approval;

                var reevaluated = await EvaluateDocumentIssuesAsync(document, approval, scopeAuthorized: true);
                var stale = reevaluated.FirstOrDefault(x => x.Decision.Outcome != InventoryIssueOutcome.Allowed);
                if (stale != null)
                    throw new InvalidOperationException(
                        $"Không thể phê duyệt vì dữ liệu tồn kho hoặc chính sách đã thay đổi. {BuildIssueUserMessage(stale)}");

                var processResult = await _confirmService.ConfirmDocumentAsync(document, actor.StaffId);
                _repository.UpdateNegativeApproval(approval);
                await _repository.SaveChangesAsync();
                await _repository.CommitTransactionAsync();
                return new InventoryDocumentMutationResultDTO
                {
                    DocumentId = documentId,
                    Status = InventoryDocumentStatus.CONFIRMED,
                    ApprovalId = approval.InventoryNegativeApprovalId,
                    Warnings = processResult.Warnings
                };
            }
            catch
            {
                await _repository.RollbackTransactionAsync();
                throw;
            }
        }

        public async Task<InventoryDocumentMutationResultDTO> RejectNegativeAsync(int documentId, string reviewNote)
        {
            if (string.IsNullOrWhiteSpace(reviewNote))
                throw new InvalidOperationException("Review note là bắt buộc khi từ chối.");

            await _repository.BeginTransactionAsync();
            try
            {
                var document = await _repository.GetDocumentForConfirmAsync(documentId)
                    ?? throw new InvalidOperationException("Không tìm thấy phiếu kho.");
                await EnsureStoreScopeAsync(document.StoreId);
                var actor = GetActor();
                EnsureCanApprove(actor.RoleNames);
                var approval = await _repository.GetNegativeApprovalForUpdateAsync(documentId)
                    ?? throw new InvalidOperationException("Không tìm thấy yêu cầu phê duyệt.");
                if (approval.Status != InventoryNegativeApprovalStatuses.Requested)
                    throw new InvalidOperationException("Yêu cầu phê duyệt xuất âm không còn ở trạng thái chờ duyệt.");
                if (approval.RequesterStaffId == actor.StaffId)
                    throw new InvalidOperationException("Người tạo yêu cầu không được tự từ chối yêu cầu xuất âm của mình.");

                approval.Status = InventoryNegativeApprovalStatuses.Rejected;
                approval.ApproverStaffId = actor.StaffId;
                approval.ReviewNote = reviewNote.Trim();
                approval.ReviewedAt = DateTime.UtcNow;
                document.Status = InventoryDocumentStatus.CANCELLED;
                document.NegativeApproval = approval;
                _repository.UpdateNegativeApproval(approval);
                _repository.UpdateDocument(document);
                await _repository.SaveChangesAsync();
                await _repository.CommitTransactionAsync();
                return new InventoryDocumentMutationResultDTO
                {
                    DocumentId = documentId,
                    Status = InventoryDocumentStatus.CANCELLED,
                    ApprovalId = approval.InventoryNegativeApprovalId
                };
            }
            catch
            {
                await _repository.RollbackTransactionAsync();
                throw;
            }
        }

        public async Task<bool> CancelInventoryDocumentAsync(int documentId, string? requestKey)
        {
            EnsureRequestKey(requestKey);

            RequestDeduplicationBeginResult? dedup = null;

            await _repository.BeginTransactionAsync();

            try
            {
                var document = await _repository.GetDocumentForConfirmAsync(documentId);

                if (document == null)
                {
                    await _repository.RollbackTransactionAsync();

                    return false;
                }

                await EnsureStoreScopeAsync(document.StoreId);

                dedup = await _deduplicationService.BeginAsync(
                    requestKey,
                    "InventoryDocument.Cancel",
                    GetCurrentStaffId(),
                    new { documentId, requestKey },
                    documentId);

                if (!dedup.CanProcess)
                {
                    await _repository.RollbackTransactionAsync();

                    if (dedup.Status == "SUCCESS")
                    {
                        return true;
                    }

                    throw BuildDeduplicationException(dedup);
                }

                if (document.Status == InventoryDocumentStatus.CONFIRMED)
                {
                    throw new InvalidOperationException("Phiếu đã xác nhận, không thể hủy.");
                }

                if (document.Status == InventoryDocumentStatus.CANCELLED)
                {
                    await _deduplicationService.MarkSuccessAsync(
                        dedup.Entry!,
                        document.InventoryDocumentId,
                        new { cancelled = true, documentId });

                    await _repository.CommitTransactionAsync();

                    return true;
                }

                document.Status = InventoryDocumentStatus.CANCELLED;
                document.IsProcessing = false;

                var approval = await _repository.GetNegativeApprovalForUpdateAsync(documentId);
                if (approval?.Status == InventoryNegativeApprovalStatuses.Requested)
                {
                    approval.Status = InventoryNegativeApprovalStatuses.Cancelled;
                    approval.ReviewNote = "Cancelled by requester before review.";
                    approval.ReviewedAt = DateTime.UtcNow;
                    _repository.UpdateNegativeApproval(approval);
                }

                _repository.UpdateDocument(document);

                await _repository.SaveChangesAsync();

                await _deduplicationService.MarkSuccessAsync(
                    dedup.Entry!,
                    document.InventoryDocumentId,
                    new { cancelled = true, documentId });

                await _repository.CommitTransactionAsync();

                return true;
            }
            catch (Exception ex)
            {
                await MarkFailedIfPossibleAsync(dedup, ex.Message);

                await _repository.RollbackTransactionAsync();

                throw;
            }
        }


        // =====================================================
        // PRIVATE METHODS
        // =====================================================

        private async Task<InventoryDocument> BuildDraftDocument(CreateInventoryDocumentDTO dto, InventoryCreateSummaryDTO summary)
        {
            return new InventoryDocument
            {
                Code = await _repository.GenerateDocumentCodeAsync(dto.Type, dto.Purpose),
                StoreId = dto.StoreId,
                StaffId = GetCurrentStaffId(),
                DocumentDate = dto.DocumentDate,
                Type = dto.Type,
                Purpose = dto.Purpose,
                Status = InventoryDocumentStatus.DRAFT,
                RequestKey = dto.RequestKey,
                PartnerType = dto.PartnerType,
                PartnerId = dto.PartnerId,
                PartnerName = dto.PartnerName,
                SupplierId = dto.SupplierId,
                Note = dto.Note,
                AllowNegativeStock = dto.AllowNegativeStock,
                NegativeReason = dto.NegativeReason,
                TotalAmount = summary.TotalAmount,
                VatAmount = summary.VatAmount,
                FinalAmount = summary.FinalAmount,
                IsProcessing = false
            };
        }

        private List<InventoryDocumentDetail> BuildDocumentDetails(int documentId, CreateInventoryDocumentDTO dto)
        {
            var isQuantityOnlyDocument = IsQuantityOnlyDocumentType(dto.Type);

            return dto.Details
                .Select(x => new InventoryDocumentDetail
                {
                    InventoryDocumentId = documentId,
                    IngredientId = x.IngredientId,
                    Quantity = x.Quantity,
                    BaseQuantity = x.BaseQuantity,
                    UnitId = x.UnitId,
                    UnitPrice = isQuantityOnlyDocument ? 0 : x.UnitPrice,
                    CostPrice = isQuantityOnlyDocument ? 0 : x.CostPrice,
                    CostAmount = isQuantityOnlyDocument ? 0 : x.CostAmount,
                    TotalAmount = isQuantityOnlyDocument ? 0 : x.TotalAmount,
                    Note = NormalizeDetailNote(dto.Note)
                })
                .ToList();
        }

        private async Task<InventoryDocument> CreateDocumentAsync(CreateInventoryDocumentDTO dto)
        {
            var summary = await BuildSummaryAsync(dto);

            var document =
                new InventoryDocument
                {
                    Code = await _repository.GenerateDocumentCodeAsync(dto.Type, dto.Purpose),
                    StoreId = dto.StoreId,
                    StaffId = GetCurrentStaffId(),
                    DocumentDate = dto.DocumentDate,
                    Type = dto.Type,
                    Purpose = dto.Purpose,
                    RequestKey = dto.RequestKey,
                    PartnerType = dto.PartnerType,
                    SupplierId = dto.SupplierId,
                    PartnerId = dto.PartnerId,
                    PartnerName = dto.PartnerName,
                    Status = InventoryDocumentStatus.PENDING,
                    TotalAmount = summary.TotalAmount,
                    VatAmount = summary.VatAmount,
                    FinalAmount = summary.FinalAmount,
                    Note = dto.Note,
                    AllowNegativeStock = dto.AllowNegativeStock,
                    NegativeReason = dto.NegativeReason
                };

            await _repository.AddDocumentAsync(document);

            await _repository.SaveChangesAsync();

            return document;
        }

        private async Task CreateDetailsAsync(int documentId, CreateInventoryDocumentDTO dto)
        {
            var isQuantityOnlyDocument = IsQuantityOnlyDocumentType(dto.Type);

            var details =
                dto.Details
                .Select(x =>
                    new InventoryDocumentDetail
                    {
                        InventoryDocumentId = documentId,
                        IngredientId = x.IngredientId,
                        Quantity = x.Quantity,
                        BaseQuantity = x.BaseQuantity,
                        UnitId = x.UnitId,
                        UnitPrice = isQuantityOnlyDocument ? 0 : x.UnitPrice,
                        CostPrice = isQuantityOnlyDocument ? 0 : x.CostPrice,
                        CostAmount = isQuantityOnlyDocument ? 0 : x.CostAmount,
                        TotalAmount = isQuantityOnlyDocument ? 0 : x.TotalAmount,
                        Note = NormalizeDetailNote(dto.Note)
                    });

            await _repository.AddDocumentDetailsAsync(details);

            await _repository.SaveChangesAsync();
        }

        private static void NormalizeImportDocumentType(CreateInventoryDocumentDTO dto)
        {
            if (dto.Type == InventoryDocumentType.ADJUSTMENT_IN)
            {
                dto.Type = InventoryDocumentType.IMPORT;
                dto.Purpose = InventoryDocumentPurpose.IMPORT_ADJUSTMENT;
                dto.SupplierId = null;
                return;
            }

            if (dto.Type == InventoryDocumentType.IMPORT && dto.Purpose == InventoryDocumentPurpose.NONE)
            {
                dto.Purpose = InventoryDocumentPurpose.IMPORT_PURCHASE;
            }

            if (dto.Type == InventoryDocumentType.IMPORT)
            {
                if (dto.Purpose == InventoryDocumentPurpose.IMPORT_PURCHASE && dto.SupplierId.HasValue)
                {
                    dto.PartnerType = InventoryPartnerType.SUPPLIER;
                    dto.PartnerId = dto.SupplierId;
                }

                if (dto.Purpose != InventoryDocumentPurpose.IMPORT_PURCHASE)
                {
                    dto.SupplierId = null;
                }

                if (dto.Purpose == InventoryDocumentPurpose.IMPORT_ADJUSTMENT)
                {
                    ClearPartner(dto);
                }
            }

            if (dto.Type == InventoryDocumentType.EXPORT)
            {
                if (dto.Purpose == InventoryDocumentPurpose.SALE && !string.IsNullOrWhiteSpace(dto.PartnerName))
                {
                    dto.PartnerType = InventoryPartnerType.CUSTOMER;
                    dto.PartnerId = null;
                    dto.PartnerName = dto.PartnerName.Trim();
                }
                else if (dto.Purpose == InventoryDocumentPurpose.SALE)
                {
                    ClearPartner(dto);
                }

                if (dto.Purpose == InventoryDocumentPurpose.ADJUSTMENT_OUT)
                {
                    ClearPartner(dto);
                }
            }
        }

        private static void NormalizeNegativeStockRequest(CreateInventoryDocumentDTO dto)
        {
            if (!dto.AllowNegativeStock)
            {
                dto.NegativeReason = null;
                return;
            }

            dto.NegativeReason = string.IsNullOrWhiteSpace(dto.NegativeReason)
                ? null
                : dto.NegativeReason.Trim();
        }

        private async Task ApplySupplierPartnerSnapshotAsync(CreateInventoryDocumentDTO dto)
        {
            if (dto.Type != InventoryDocumentType.IMPORT || dto.Purpose != InventoryDocumentPurpose.IMPORT_PURCHASE)
            {
                return;
            }

            if (!dto.SupplierId.HasValue || dto.SupplierId.Value <= 0)
            {
                ClearPartner(dto);
                return;
            }

            var supplier =  await _repository.GetSupplierAsync(dto.SupplierId.Value) ?? throw new InvalidOperationException("Nhà cung cấp không tồn tại hoặc đã bị xóa.");

            dto.SupplierId = supplier.SupplierId;
            dto.PartnerType = InventoryPartnerType.SUPPLIER;
            dto.PartnerId = supplier.SupplierId;
            dto.PartnerName = string.IsNullOrWhiteSpace(supplier.Name) ? $"Nhà cung cấp #{supplier.SupplierId}" : supplier.Name.Trim();
        }

        private static void ClearPartner(CreateInventoryDocumentDTO dto)
        {
            dto.PartnerType = InventoryPartnerType.NONE;
            dto.PartnerId = null;
            dto.PartnerName = null;
        }

        private async Task<SupplierIngredientDTO> BuildStoreIngredientDtoAsync(
            Ingredient ingredient,
            decimal availableBaseQuantity,
            IReadOnlyDictionary<int, (decimal BaseUnitCost, string PriceSource)> priceLookup,
            bool isPriceLocked,
            bool isQuantityLocked)
        {
            var unitOptions = await GetUnitOptionsAsync(ingredient);

            var defaultUnit = unitOptions.FirstOrDefault(x => x.IsBaseUnit) ?? unitOptions.FirstOrDefault();

            var hasPrice = priceLookup.TryGetValue(ingredient.IngredientId, out var price);

            var baseUnitCost = hasPrice ? price.BaseUnitCost : (decimal?)null;

            var conversionFactor = defaultUnit?.ConversionFactorToBase ?? 0;

            var unitPrice = baseUnitCost.HasValue && conversionFactor > 0
                ? baseUnitCost.Value * conversionFactor
                : (decimal?)null;

            return new SupplierIngredientDTO
            {
                IngredientId = ingredient.IngredientId,
                IngredientName = ingredient.Name,
                UnitId = defaultUnit?.UnitId ?? ingredient.BaseUnitId,
                UnitName = defaultUnit?.UnitName ?? ingredient.BaseUnit?.Name ?? string.Empty,
                UnitCode = defaultUnit?.UnitCode ?? ingredient.BaseUnit?.UnitCode ?? string.Empty,
                CurrentPrice = unitPrice ?? 0,
                PackagePrice = unitPrice ?? 0,
                PackageQuantity = 1,
                PackageUnitId = defaultUnit?.UnitId ?? ingredient.BaseUnitId,
                PackageUnitCode = defaultUnit?.UnitCode ?? ingredient.BaseUnit?.UnitCode ?? string.Empty,
                PackageUnitName = defaultUnit?.UnitName ?? ingredient.BaseUnit?.Name ?? string.Empty,
                HasCompletePackageDefinition = unitPrice.HasValue,
                BaseUnitId = ingredient.BaseUnitId,
                BaseUnitName = ingredient.BaseUnit?.Name ?? string.Empty,
                BaseUnitCode = ingredient.BaseUnit?.UnitCode ?? string.Empty,
                ConversionFactorToBase = conversionFactor,
                CanConvertToBase = conversionFactor > 0,
                AvailableBaseQuantity = availableBaseQuantity,
                SuggestedBaseUnitCost = baseUnitCost,
                SuggestedUnitPrice = unitPrice,
                CanAutoFillUnitPrice = unitPrice.HasValue,
                PriceSource = hasPrice ? price.PriceSource : "Chưa có giá gợi ý",
                PriceSemantics = baseUnitCost.HasValue
                    ? InventoryPriceSemantics.BaseUnitCost
                    : InventoryPriceSemantics.None,
                IsQuantityLocked = isQuantityLocked,
                IsPriceLocked = isPriceLocked && unitPrice.HasValue,
                UnitOptions = unitOptions
            };
        }

        /// <summary>
        /// Supplier-sourced ingredient for IMPORT create (#111 package-safe suggestions).
        /// </summary>
        private static SupplierIngredientDTO MapSupplierIngredientDto(IngredientSupplier x)
        {
            var packagePrice = GetCurrentSupplierPrice(x);
            var conversionFactor = CalculateConversionFactorToBase(x.Ingredient, x.UnitId, throwIfMissing: false);
            var hasCompletePackage = x.PackageQuantity.HasValue
                && x.PackageQuantity.Value > 0
                && x.Unit != null
                && x.Unit.Active;
            var canAutoFill = hasCompletePackage
                && conversionFactor > 0
                && packagePrice > 0m;
            var suggestedUnitPrice = canAutoFill
                ? packagePrice / x.PackageQuantity!.Value
                : (decimal?)null;
            var suggestedBaseUnitCost = canAutoFill
                ? packagePrice / (x.PackageQuantity!.Value * conversionFactor!.Value)
                : (decimal?)null;

            return new SupplierIngredientDTO
            {
                IngredientSupplierId = x.IngredientSupplierId,
                IngredientId = x.IngredientId,
                IngredientName = x.Ingredient.Name,
                UnitId = x.UnitId,
                UnitName = x.Unit.Name,
                UnitCode = x.Unit.UnitCode,
                CurrentPrice = packagePrice,
                PackagePrice = packagePrice,
                PackageQuantity = x.PackageQuantity,
                PackageUnitId = x.UnitId,
                PackageUnitCode = x.Unit.UnitCode,
                PackageUnitName = x.Unit.Name,
                HasCompletePackageDefinition = hasCompletePackage,
                MinimumOrderQuantity = x.MinimumOrderQuantity,
                BaseUnitId = x.Ingredient.BaseUnitId,
                BaseUnitName = x.Ingredient.BaseUnit.Name,
                BaseUnitCode = x.Ingredient.BaseUnit.UnitCode,
                ConversionFactorToBase = conversionFactor ?? 0,
                CanConvertToBase = conversionFactor.HasValue,
                SuggestedBaseUnitCost = suggestedBaseUnitCost,
                SuggestedUnitPrice = suggestedUnitPrice,
                CanAutoFillUnitPrice = canAutoFill,
                PriceSource = "Bảng giá nhà cung cấp",
                PriceSemantics = canAutoFill
                    ? InventoryPriceSemantics.PurchasePackage
                    : InventoryPriceSemantics.None,
                IsQuantityLocked = false,
                IsPriceLocked = canAutoFill,
                UnitOptions = BuildSupplierUnitOptions(x, conversionFactor)
            };
        }

        private async Task<SupplierIngredientDTO> MapSupplierIngredientDtoAsync(IngredientSupplier x)
        {
            var packagePrice = GetCurrentSupplierPrice(x);
            var conversionFactor = await GetConversionFactorToBaseAsync(x.Ingredient, x.UnitId, throwIfMissing: false);
            var hasCompletePackage =
                x.PackageQuantity.HasValue
                && x.PackageQuantity.Value > 0
                && x.Unit != null
                && x.Unit.Active;

            var canAutoFill =
                hasCompletePackage
                && conversionFactor.HasValue
                && conversionFactor.Value > 0
                && packagePrice > 0m;

            decimal? suggestedUnitPrice = canAutoFill
                ? packagePrice / x.PackageQuantity!.Value
                : null;
            decimal? suggestedBaseUnitCost = canAutoFill
                ? packagePrice / (x.PackageQuantity!.Value * conversionFactor!.Value)
                : null;

            return new SupplierIngredientDTO
            {
                IngredientSupplierId = x.IngredientSupplierId,
                IngredientId = x.IngredientId,
                IngredientName = x.Ingredient.Name,
                UnitId = x.UnitId,
                UnitName = x.Unit.Name,
                UnitCode = x.Unit.UnitCode,
                CurrentPrice = packagePrice,
                PackagePrice = packagePrice,
                PackageQuantity = x.PackageQuantity,
                PackageUnitId = x.UnitId,
                PackageUnitCode = x.Unit.UnitCode,
                PackageUnitName = x.Unit.Name,
                HasCompletePackageDefinition = hasCompletePackage,
                MinimumOrderQuantity = x.MinimumOrderQuantity,
                BaseUnitId = x.Ingredient.BaseUnitId,
                BaseUnitName = x.Ingredient.BaseUnit.Name,
                BaseUnitCode = x.Ingredient.BaseUnit.UnitCode,
                ConversionFactorToBase = conversionFactor ?? 0,
                CanConvertToBase = conversionFactor.HasValue,
                SuggestedBaseUnitCost = suggestedBaseUnitCost,
                SuggestedUnitPrice = suggestedUnitPrice,
                CanAutoFillUnitPrice = canAutoFill,
                PriceSource = "Bảng giá nhà cung cấp",
                PriceSemantics = canAutoFill
                    ? InventoryPriceSemantics.PurchasePackage
                    : InventoryPriceSemantics.None,
                IsQuantityLocked = false,
                IsPriceLocked = canAutoFill,
                UnitOptions = BuildSupplierUnitOptions(x, conversionFactor)
            };
        }

        private async Task<Dictionary<int, (decimal BaseUnitCost, string PriceSource)>> BuildPriceLookupAsync(
            IEnumerable<InventoryCostLayer> costLayers,
            IEnumerable<IngredientSupplier> supplierPrices)
        {
            var result =
                costLayers
                    .Where(x => x.IngredientId.HasValue)
                    .GroupBy(x => x.IngredientId!.Value)
                    .Where(x => x.Sum(layer => layer.RemainingQuantity) > 0)
                    .ToDictionary(
                        x => x.Key,
                        x =>
                        {
                            var quantity = x.Sum(layer => layer.RemainingQuantity);

                            var amount = x.Sum(layer => layer.RemainingQuantity * layer.UnitCost);

                            return (BaseUnitCost: amount / quantity, PriceSource: "Giá vốn FIFO bình quân còn tồn");
                        });

            foreach (var group in supplierPrices.GroupBy(x => x.IngredientId))
            {
                if (result.ContainsKey(group.Key))
                {
                    continue;
                }

                var supplier =
                    group
                        .Where(x => GetCurrentSupplierPrice(x) > 0)
                        .OrderByDescending(x => x.IsPrimary)
                        .ThenByDescending(GetSupplierPriceEffectiveDate)
                        .FirstOrDefault();

                if (supplier == null)
                {
                    continue;
                }

                if (!supplier.PackageQuantity.HasValue || supplier.PackageQuantity.Value <= 0m)
                {
                    continue;
                }

                var factor = await GetConversionFactorToBaseAsync(
                    supplier.Ingredient,
                    supplier.UnitId,
                    throwIfMissing: false);
                var baseCost = factor.HasValue && factor.Value > 0
                    ? GetCurrentSupplierPrice(supplier) / (supplier.PackageQuantity.Value * factor.Value)
                    : 0;

                if (baseCost <= 0)
                {
                    continue;
                }

                result[group.Key] =
                    (baseCost, "Giá nhà cung cấp gần nhất");
            }

            return result;
        }

        private async Task<List<InventoryUnitOptionDTO>> GetUnitOptionsAsync(Ingredient ingredient)
        {
            if (_unitConversionService == null)
                return BuildUnitOptions(ingredient);

            var result = await _unitConversionService.GetActiveUnitOptionsAsync(ingredient.IngredientId);
            if (!result.IsSuccess)
                throw new InvalidOperationException(result.Message);

            return result.Data.ToList();
        }

        private async Task<decimal?> GetConversionFactorToBaseAsync(
            Ingredient ingredient,
            int unitId,
            bool throwIfMissing)
        {
            if (_unitConversionService == null)
                return CalculateConversionFactorToBase(ingredient, unitId, throwIfMissing);

            var result = await _unitConversionService.ConvertAsync(
                ingredient.IngredientId,
                1m,
                unitId,
                ingredient.BaseUnitId);
            if (result.IsSuccess && result.Data > 0m)
                return result.Data;

            if (throwIfMissing)
                throw new InvalidOperationException(result.Message);
            return null;
        }

        private static List<InventoryUnitOptionDTO> BuildUnitOptions(Ingredient ingredient)
        {
            var options = new List<InventoryUnitOptionDTO>();

            if (ingredient.BaseUnit != null)
            {
                options.Add(
                    new InventoryUnitOptionDTO
                    {
                        UnitId = ingredient.BaseUnitId,
                        UnitName = ingredient.BaseUnit.Name,
                        UnitCode = ingredient.BaseUnit.UnitCode,
                        ConversionFactorToBase = 1,
                        IsBaseUnit = true
                    });
            }

            foreach (var conversion in ingredient.UnitConversions
                .Where(x =>
                    x.Active
                    && x.ToUnitId == ingredient.BaseUnitId
                    && x.FromQuantity > 0
                    && x.ToQuantity > 0)
                .OrderBy(x => x.FromUnit?.UnitCode ?? x.FromUnitId.ToString(CultureInfo.InvariantCulture)))
            {
                if (options.Any(x => x.UnitId == conversion.FromUnitId))
                {
                    continue;
                }

                options.Add(
                    new InventoryUnitOptionDTO
                    {
                        UnitId = conversion.FromUnitId,
                        UnitName = conversion.FromUnit?.Name ?? conversion.FromUnitId.ToString(CultureInfo.InvariantCulture),
                        UnitCode = conversion.FromUnit?.UnitCode ?? conversion.FromUnitId.ToString(CultureInfo.InvariantCulture),
                        ConversionFactorToBase = conversion.ToQuantity / conversion.FromQuantity,
                        IsBaseUnit = false
                    });
            }

            return options;
        }

        private static List<InventoryUnitOptionDTO> BuildBaseUnitOptions(Ingredient ingredient)
        {
            return
            [
                new InventoryUnitOptionDTO
                {
                    UnitId = ingredient.BaseUnitId,
                    UnitName = ingredient.BaseUnit?.Name ?? string.Empty,
                    UnitCode = ingredient.BaseUnit?.UnitCode ?? string.Empty,
                    ConversionFactorToBase = 1,
                    IsBaseUnit = true
                }
            ];
        }

        private static List<InventoryUnitOptionDTO> BuildSupplierUnitOptions(IngredientSupplier supplier, decimal? conversionFactor)
        {
            return
            [
                new InventoryUnitOptionDTO
                {
                    UnitId = supplier.UnitId,
                    UnitName = supplier.Unit?.Name ?? string.Empty,
                    UnitCode = supplier.Unit?.UnitCode ?? string.Empty,
                    ConversionFactorToBase = conversionFactor ?? 0,
                    IsBaseUnit = supplier.UnitId == supplier.Ingredient.BaseUnitId
                }
            ];
        }

        private static decimal GetCurrentSupplierPrice(IngredientSupplier supplier)
        {
            var currentHistory =
                supplier.PriceHistories
                    .Where(x => x.IsCurrent)
                    .OrderByDescending(x => x.EffectiveDate)
                    .FirstOrDefault();

            return currentHistory?.Price > 0
                ? currentHistory.Price
                : supplier.CurrentPrice;
        }

        private static DateTime GetSupplierPriceEffectiveDate(IngredientSupplier supplier)
        {
            return supplier.PriceHistories
                .Where(x => x.IsCurrent)
                .Select(x => x.EffectiveDate)
                .DefaultIfEmpty(DateTime.MinValue)
                .Max();
        }

        private async Task NormalizeCreateDetailsAsync(CreateInventoryDocumentDTO dto)
        {
            if (dto.Details == null || !dto.Details.Any())
            {
                return;
            }

            var isStockTake =
                dto.Type == InventoryDocumentType.STOCK_TAKE;

            var isQuantityOnlyDocument =
                IsQuantityOnlyDocumentType(dto.Type);

            Dictionary<int, IngredientSupplier> purchaseOffers = new();
            var isPurchaseImport = dto.Type == InventoryDocumentType.IMPORT
                && dto.Purpose == InventoryDocumentPurpose.IMPORT_PURCHASE;
            if (isPurchaseImport)
            {
                if (!dto.SupplierId.HasValue)
                    throw new InvalidOperationException("Chưa chọn nhà cung cấp.");
                if (!await _repository.IsActiveSupplierStoreAsync(dto.SupplierId.Value, dto.StoreId))
                    throw new InvalidOperationException("Nhà cung cấp chưa được kích hoạt cho cửa hàng này.");
                if (dto.Details.Any(x => !x.IngredientSupplierId.HasValue || x.IngredientSupplierId <= 0))
                    throw new InvalidOperationException("INGREDIENT_SUPPLIER_REQUIRED: Chọn gói mua đang hoạt động cho từng nguyên liệu.");

                purchaseOffers = (await _repository.GetActiveIngredientSuppliersByIdsAsync(
                        dto.Details.Select(x => x.IngredientSupplierId!.Value)))
                    .ToDictionary(x => x.IngredientSupplierId);
            }

            foreach (var item in dto.Details)
            {
                if ((!isStockTake && item.Quantity <= 0) || (isStockTake && item.Quantity < 0))
                    throw new InvalidOperationException("INVALID_QUANTITY: Số lượng phải lớn hơn 0; kiểm kê chỉ cho phép giá trị từ 0 trở lên.");

                var ingredient =
                    await _repository.GetIngredientAsync(item.IngredientId)
                    ?? throw new InvalidOperationException("Nguyên liệu không tồn tại.");

                var conversion = _unitConversionService == null
                    ? null
                    : await _unitConversionService.ConvertAsync(
                        item.IngredientId,
                        item.Quantity,
                        item.UnitId,
                        ingredient.BaseUnitId);
                if (conversion != null && !conversion.IsSuccess)
                    throw new InvalidOperationException(conversion.Message);

                var conversionFactor = conversion != null
                    ? (item.Quantity == 0 ? 0 : conversion.Data / item.Quantity)
                    : CalculateConversionFactorToBase(ingredient, item.UnitId, throwIfMissing: true) ?? 0;

                item.BaseQuantity = conversion?.Data ?? item.Quantity * conversionFactor;

                if (isPurchaseImport)
                {
                    if (!purchaseOffers.TryGetValue(item.IngredientSupplierId!.Value, out var offer)
                        || offer.SupplierId != dto.SupplierId
                        || offer.IngredientId != item.IngredientId
                        || !offer.Active
                        || !offer.Supplier.Active
                        || !offer.Ingredient.Active
                        || !offer.Unit.Active
                        || !offer.PackageQuantity.HasValue
                        || offer.PackageQuantity <= 0)
                    {
                        throw new InvalidOperationException("Gói mua không hợp lệ hoặc đã ngừng hoạt động.");
                    }

                    if (_packageEligibility != null)
                    {
                        var eligibility = await _packageEligibility.EvaluateProcurementEligibilityAsync(
                            offer,
                            PurchaseMode.Packaged,
                            dto.StoreId);
                        if (!eligibility.IsProcurementEligible)
                        {
                            throw new InvalidOperationException(
                                $"Gói mua chưa sẵn sàng để nhập hàng. {eligibility.Message}");
                        }
                    }

                    var currentPrices = offer.PriceHistories.Where(x => x.IsCurrent).ToList();
                    if (currentPrices.Count != 1)
                        throw new InvalidOperationException("Gói mua phải có đúng một mức giá hiện tại.");

                    var currentPrice = currentPrices[0];
                    if (currentPrice.Price <= 0
                        || currentPrice.PackageQuantity != offer.PackageQuantity
                        || currentPrice.PackageUnitId != offer.UnitId)
                    {
                        throw new InvalidOperationException("SUPPLIER_PRICE_SNAPSHOT_MISMATCH: Giá hiện tại không khớp quy cách gói mua.");
                    }

                    var packageUnitToBase = await GetConversionFactorToBaseAsync(
                        offer.Ingredient, offer.UnitId, throwIfMissing: true) ?? 0;
                    var packageBaseQuantity = offer.PackageQuantity.Value * packageUnitToBase;
                    if (packageBaseQuantity <= 0)
                        throw new InvalidOperationException("PACKAGE_CONVERSION_INVALID: Không quy đổi được gói mua về đơn vị tồn kho.");

                    var equivalentPackageCount = item.BaseQuantity / packageBaseQuantity;
                    if (equivalentPackageCount < offer.MinimumOrderPackageCount.GetValueOrDefault())
                        throw new InvalidOperationException($"MINIMUM_ORDER_NOT_MET: Số lượng nhập thấp hơn MOQ {offer.MinimumOrderPackageCount:N3} gói.");

                    item.UnitPrice = Math.Round(
                        currentPrice.Price * conversionFactor / packageBaseQuantity,
                        4,
                        MidpointRounding.AwayFromZero);
                    item.CostPrice = Math.Round(
                        currentPrice.Price / packageBaseQuantity,
                        4,
                        MidpointRounding.AwayFromZero);
                }

                if (isQuantityOnlyDocument)
                {
                    item.UnitPrice = 0;
                    item.TotalAmount = 0;
                    item.CostPrice = 0;
                    item.CostAmount = 0;
                    continue;
                }

                item.TotalAmount =
                    item.Quantity * item.UnitPrice;

                if (item.CostPrice.HasValue && item.CostPrice.Value >= 0)
                {
                    item.CostAmount = item.BaseQuantity * item.CostPrice.Value;
                }
            }
        }

        private async Task<InventoryCreateSummaryDTO> BuildSummaryAsync(CreateInventoryDocumentDTO dto)
        {
            var isQuantityOnlyDocument =
                IsQuantityOnlyDocumentType(dto.Type);

            InventoryCreateSummaryDTO summary = new()
            {
                TotalItems = dto.Details.Count,
                TotalQuantity = dto.Details.Sum(x => x.Quantity),
                TotalAmount = isQuantityOnlyDocument
                    ? 0
                    : dto.Details.Sum(x => x.TotalAmount),
                VatRate = 0,
                VatAmount = 0
            };

            summary.FinalAmount = summary.TotalAmount + summary.VatAmount;
            summary.BaseQuantities = await BuildBaseQuantitySummaryAsync(dto);
            summary.BaseQuantityText = FormatBaseQuantityText(summary.BaseQuantities);

            return summary;
        }

        private static bool IsQuantityOnlyDocumentType(InventoryDocumentType type)
        {
            return type == InventoryDocumentType.STOCK_TAKE
                || type == InventoryDocumentType.WASTE;
        }

        private async Task<List<InventoryBaseQuantitySummaryDTO>> BuildBaseQuantitySummaryAsync(CreateInventoryDocumentDTO dto)
        {
            var result = new Dictionary<int, InventoryBaseQuantitySummaryDTO>();

            foreach (var item in dto.Details)
            {
                if (item.IngredientId <= 0 || item.BaseQuantity <= 0)
                {
                    continue;
                }

                var ingredient = await _repository.GetIngredientAsync(item.IngredientId);

                if (ingredient?.BaseUnit == null)
                {
                    continue;
                }

                if (!result.TryGetValue(ingredient.BaseUnitId, out var summary))
                {
                    summary =
                        new InventoryBaseQuantitySummaryDTO
                        {
                            UnitId = ingredient.BaseUnitId,
                            UnitCode = ingredient.BaseUnit.UnitCode,
                            UnitName = ingredient.BaseUnit.Name
                        };

                    result.Add(ingredient.BaseUnitId, summary);
                }

                summary.Quantity += item.BaseQuantity;
            }

            return result.Values.OrderBy(x => x.UnitCode).ToList();
        }

        private static decimal? CalculateConversionFactorToBase(Ingredient ingredient, int unitId, bool throwIfMissing)
        {
            if (unitId == ingredient.BaseUnitId)
            {
                return 1;
            }

            var conversion =
                ingredient.UnitConversions
                    .FirstOrDefault(x =>
                        x.Active
                        && x.FromUnitId == unitId
                        && x.ToUnitId == ingredient.BaseUnitId);

            if (conversion == null || conversion.FromQuantity <= 0)
            {
                if (throwIfMissing)
                {
                    throw new InvalidOperationException(
                        $"Chưa cấu hình quy đổi đơn vị cho nguyên liệu {ingredient.Name}.");
                }

                return null;
            }

            return conversion.ToQuantity / conversion.FromQuantity;
        }

        private static string FormatBaseQuantityText(IEnumerable<InventoryBaseQuantitySummaryDTO> baseQuantities)
        {
            var text =
                baseQuantities
                    .Where(x => x.Quantity > 0)
                    .Select(x => $"{FormatQuantity(x.Quantity)} {x.UnitCode}")
                    .ToList();

            return text.Any()
                ? string.Join(", ", text)
                : "0";
        }

        private static string FormatQuantity(decimal quantity)
        {
            return quantity.ToString(
                "#,0.###",
                CultureInfo.GetCultureInfo("vi-VN"));
        }

        private static bool IsSameQuantity(decimal left, decimal right)
        {
            return Math.Abs(left - right) < 0.001m;
        }

        private async Task<InventoryDocumentPreflightResultDTO> EvaluateDtoIssuesAsync(CreateInventoryDocumentDTO dto)
        {
            var operation = ResolveIssueOperation(dto.Type, dto.Purpose);
            if (!operation.HasValue)
                return new InventoryDocumentPreflightResultDTO { Outcome = InventoryIssueOutcome.Allowed };

            var result = new InventoryDocumentPreflightResultDTO { Outcome = InventoryIssueOutcome.Allowed };
            foreach (var detail in dto.Details.OrderBy(x => x.IngredientId))
            {
                var inventory = await _repository.GetStoreInventoryAsync(dto.StoreId, detail.IngredientId);
                var ingredient = await _repository.GetIngredientAsync(detail.IngredientId);
                var selectedUnit = await _repository.GetUnitAsync(detail.UnitId);
                var conversionFactor = detail.Quantity == 0m
                    ? 1m
                    : detail.BaseQuantity / detail.Quantity;
                if (conversionFactor <= 0m)
                    conversionFactor = 1m;
                var decision = await _inventoryIssuePolicy.EvaluateAsync(new InventoryIssueRequest(
                    operation.Value,
                    dto.StoreId,
                    detail.IngredientId,
                    null,
                    inventory?.AvailableQty ?? 0,
                    detail.BaseQuantity,
                    inventory?.MaxNegativeQty,
                    dto.Purpose.ToString(),
                    dto.NegativeReason,
                    null,
                    null,
                    null,
                    dto.AllowNegativeStock));
                var ingredientName = ingredient?.Name ?? $"Nguyên liệu #{detail.IngredientId}";
                var unitCode = selectedUnit?.UnitCode ?? ingredient?.BaseUnit?.UnitCode ?? "đơn vị";
                var beforeDisplayQty = decision.BeforeQty / conversionFactor;
                var issueDisplayQty = decision.IssueQty / conversionFactor;
                var projectedAfterDisplayQty = decision.ProjectedAfterQty / conversionFactor;
                var effectiveLimitDisplayQty = decision.EffectiveMaxNegativeQty / conversionFactor;
                result.Lines.Add(new InventoryDocumentPreflightLineDTO
                {
                    IngredientId = detail.IngredientId,
                    IngredientName = ingredientName,
                    BeforeQty = decision.BeforeQty,
                    IssueQty = decision.IssueQty,
                    ProjectedAfterQty = decision.ProjectedAfterQty,
                    EffectiveMaxNegativeQty = decision.EffectiveMaxNegativeQty,
                    UnitId = detail.UnitId,
                    UnitCode = unitCode,
                    ConversionFactorToBase = conversionFactor,
                    BeforeDisplayQty = beforeDisplayQty,
                    IssueDisplayQty = issueDisplayQty,
                    ProjectedAfterDisplayQty = projectedAfterDisplayQty,
                    EffectiveMaxNegativeDisplayQty = effectiveLimitDisplayQty,
                    Outcome = decision.Outcome,
                    ReasonCode = decision.ReasonCode,
                    UserMessage = BuildIssueUserMessage(
                        ingredientName,
                        unitCode,
                        decision,
                        beforeDisplayQty,
                        issueDisplayQty,
                        projectedAfterDisplayQty,
                        effectiveLimitDisplayQty)
                });
                result.PolicyVersion = decision.PolicyVersion;
            }

            result.Outcome = AggregateOutcome(result.Lines.Select(x => x.Outcome));
            return result;
        }

        private async Task<List<DocumentIssueEvaluation>> EvaluateDocumentIssuesAsync(
            InventoryDocument document,
            InventoryNegativeApproval? approval,
            bool scopeAuthorized)
        {
            var operation = ResolveIssueOperation(document.Type, document.Purpose);
            if (!operation.HasValue)
                return [];

            var result = new List<DocumentIssueEvaluation>();
            foreach (var detail in document.Details.OrderBy(x => x.IngredientId))
            {
                var inventory = await _repository.GetStoreInventoryForUpdateAsync(document.StoreId, detail.IngredientId);
                if (inventory == null)
                {
                    if (document.Type is InventoryDocumentType.EXPORT or InventoryDocumentType.WASTE)
                    {
                        throw new InvalidOperationException("INGREDIENT_NOT_IN_STORE_INVENTORY");
                    }

                    inventory = await _repository.GetOrCreateStoreInventoryForIngredientAsync(
                        document.StoreId,
                        detail.IngredientId);
                }
                var approvalLine = approval?.Lines.FirstOrDefault(x => x.InventoryDocumentDetailId == detail.InventoryDocumentDetailId);
                InventoryApprovalEvidence? evidence = null;
                if (approval?.Status == InventoryNegativeApprovalStatuses.Approved && approvalLine != null)
                {
                    evidence = new InventoryApprovalEvidence(
                        approval.InventoryNegativeApprovalId,
                        approval.StoreId,
                        approvalLine.IngredientId,
                        approvalLine.PreparedItemId,
                        approvalLine.BeforeQty,
                        approvalLine.ProjectedAfterQty,
                        approvalLine.EffectiveMaxNegativeQty,
                        approval.PolicyVersion,
                        approval.RequesterStaffId.ToString(CultureInfo.InvariantCulture),
                        approval.ApproverStaffId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                        true,
                        scopeAuthorized,
                        approvalLine.IssueQty,
                        approval.Reason,
                        approvalLine.InventoryRowVersion);
                }

                var decision = await _inventoryIssuePolicy.EvaluateAsync(new InventoryIssueRequest(
                    operation.Value,
                    document.StoreId,
                    detail.IngredientId,
                    null,
                    inventory.AvailableQty,
                    detail.BaseQuantity,
                    inventory.MaxNegativeQty,
                    document.Purpose.ToString(),
                    document.NegativeReason,
                    approval?.PolicyVersion,
                    evidence,
                    inventory.RowVersion,
                    document.AllowNegativeStock));
                result.Add(new DocumentIssueEvaluation(detail, inventory, decision));
            }

            return result;
        }

        private InventoryNegativeApproval BuildApproval(
            InventoryDocument document,
            IReadOnlyCollection<DocumentIssueEvaluation> evaluations,
            string payloadHash,
            string requestKey)
        {
            var actor = GetActor();
            var approval = new InventoryNegativeApproval
            {
                InventoryDocumentId = document.InventoryDocumentId,
                StoreId = document.StoreId,
                RequesterStaffId = actor.StaffId,
                Status = InventoryNegativeApprovalStatuses.Requested,
                Reason = document.NegativeReason!.Trim(),
                PolicyVersion = evaluations.Select(x => x.Decision.PolicyVersion).Distinct().Single(),
                RequestKey = requestKey.Trim(),
                PayloadHash = payloadHash,
                RequestedAt = DateTime.UtcNow,
                Lines = evaluations.Select(x => new InventoryNegativeApprovalLine
                {
                    InventoryDocumentDetailId = x.Detail.InventoryDocumentDetailId,
                    StoreInventoryId = x.Inventory.StoreInventoryId,
                    IngredientId = x.Detail.IngredientId,
                    BeforeQty = x.Decision.BeforeQty,
                    IssueQty = x.Decision.IssueQty,
                    ProjectedAfterQty = x.Decision.ProjectedAfterQty,
                    EffectiveMaxNegativeQty = x.Decision.EffectiveMaxNegativeQty,
                    InventoryRowVersion = x.Inventory.RowVersion.ToArray()
                }).ToList()
            };
            document.NegativeApproval = approval;
            return approval;
        }

        private static InventoryIssueOperation? ResolveIssueOperation(
            InventoryDocumentType type,
            InventoryDocumentPurpose purpose) => type switch
        {
            InventoryDocumentType.EXPORT when purpose == InventoryDocumentPurpose.ADJUSTMENT_OUT => InventoryIssueOperation.AdjustmentOut,
            InventoryDocumentType.EXPORT => InventoryIssueOperation.ManualExternalExport,
            InventoryDocumentType.WASTE => InventoryIssueOperation.Waste,
            InventoryDocumentType.PRODUCTION_OUT => InventoryIssueOperation.ProductionOut,
            InventoryDocumentType.SALES_DEDUCTION => InventoryIssueOperation.PosBlindSale,
            _ => null
        };

        private static string BuildIssueUserMessage(DocumentIssueEvaluation evaluation)
        {
            var ingredientName = evaluation.Detail.Ingredient?.Name
                ?? $"Nguyên liệu #{evaluation.Detail.IngredientId}";
            var unitCode = evaluation.Detail.Ingredient?.BaseUnit?.UnitCode ?? "đơn vị";
            return BuildIssueUserMessage(
                ingredientName,
                unitCode,
                evaluation.Decision,
                evaluation.Decision.BeforeQty,
                evaluation.Decision.IssueQty,
                evaluation.Decision.ProjectedAfterQty,
                evaluation.Decision.EffectiveMaxNegativeQty);
        }

        private static string BuildIssueUserMessage(
            string ingredientName,
            string unitCode,
            InventoryIssueDecision decision,
            decimal beforeQuantity,
            decimal issueQuantity,
            decimal projectedAfterQuantity,
            decimal effectiveLimit)
        {
            if (!decision.IsNegative)
            {
                return string.Empty;
            }

            var shortage = Math.Max(issueQuantity - beforeQuantity, 0);
            var summary =
                $"Nguyên liệu \"{ingredientName}\": tồn hiện tại {FormatQuantity(beforeQuantity)} {unitCode}, " +
                $"yêu cầu xuất {FormatQuantity(issueQuantity)} {unitCode}, thiếu {FormatQuantity(shortage)} {unitCode}.";

            return decision.ReasonCode switch
            {
                InventoryIssueReasonCodes.ManualNegativeOptInRequired =>
                    $"Không thể xuất vượt tồn kho. {summary} Hãy giảm số lượng hoặc bật \"Cho phép xuất âm kho\" nếu nghiệp vụ được phép.",
                InventoryIssueReasonCodes.ManualNegativeFeatureDisabled =>
                    $"Hệ thống đang tắt chức năng xuất âm kho. {summary} Hãy giảm số lượng hoặc liên hệ người quản trị cấu hình kho.",
                InventoryIssueReasonCodes.ManualNegativeReasonRequired =>
                    $"Phiếu xuất âm chưa có lý do. {summary} Hãy nhập lý do xuất âm trước khi tiếp tục.",
                InventoryIssueReasonCodes.ManualNegativeLimitExceeded =>
                    $"Mức xuất âm vượt giới hạn cho phép {FormatQuantity(effectiveLimit)} {unitCode}. {summary} Hãy giảm số lượng xuất.",
                InventoryIssueReasonCodes.ManualNegativeApprovalRequired =>
                    $"{summary} Phiếu sẽ làm tồn kho âm {FormatQuantity(Math.Abs(projectedAfterQuantity))} {unitCode} và cần người có quyền phê duyệt trước khi xác nhận.",
                InventoryIssueReasonCodes.NegativeSettingInvalid =>
                    $"Cấu hình giới hạn xuất âm chưa hợp lệ. {summary} Hãy liên hệ người quản trị cấu hình kho.",
                InventoryIssueReasonCodes.WasteNegativeForbidden =>
                    $"Phiếu Hủy không được làm tồn kho âm. {summary} Hãy giảm số lượng hủy.",
                InventoryIssueReasonCodes.AdjustmentOutNegativeForbidden =>
                    $"Điều chỉnh giảm không được làm tồn kho âm. {summary} Hãy sử dụng Phiếu Kiểm Kê và kiểm tra lại số lượng thực tế.",
                InventoryIssueReasonCodes.ProductionOutNegativeForbidden =>
                    $"Xuất cho sản xuất không được làm tồn kho âm. {summary} Hãy bổ sung tồn kho hoặc giảm số lượng.",
                InventoryIssueReasonCodes.TransferSourceNegativeForbidden =>
                    $"Kho nguồn không đủ tồn để chuyển. {summary} Hãy giảm số lượng chuyển.",
                _ =>
                    $"Không thể thực hiện vì số lượng yêu cầu vượt tồn kho hiện tại. {summary} Hãy kiểm tra lại số lượng và chính sách kho."
            };
        }

        private static InventoryIssueOutcome AggregateOutcome(IEnumerable<InventoryIssueOutcome> outcomes)
        {
            var values = outcomes.ToList();
            if (values.Contains(InventoryIssueOutcome.Blocked))
                return InventoryIssueOutcome.Blocked;
            return values.Contains(InventoryIssueOutcome.ApprovalRequired)
                ? InventoryIssueOutcome.ApprovalRequired
                : InventoryIssueOutcome.Allowed;
        }

        private async Task EnsureStoreScopeAsync(int storeId)
        {
            var actor = GetActor();
            if (actor.StaffId <= 0 || !await _scopeAuthorization.CanAccessStoreAsync(actor.StaffId, storeId))
                throw new UnauthorizedAccessException("APPROVAL_SCOPE_FORBIDDEN");
        }

        private static InvalidOperationException BuildDeduplicationException(
            RequestDeduplicationBeginResult dedup)
        {
            var message = dedup.ErrorMessage ?? "RequestKey đã được xử lý.";
            return new InvalidOperationException(string.IsNullOrWhiteSpace(dedup.ErrorCode)
                ? message
                : $"{dedup.ErrorCode}: {message}");
        }

        private static void EnsureRowVersionMatches(byte[] current, string? suppliedBase64)
        {
            if (string.IsNullOrWhiteSpace(suppliedBase64))
                throw new InvalidOperationException("ROW_VERSION_REQUIRED");

            byte[] supplied;
            try
            {
                supplied = Convert.FromBase64String(suppliedBase64);
            }
            catch (FormatException)
            {
                throw new InvalidOperationException("ROW_VERSION_INVALID");
            }

            if (!current.AsSpan().SequenceEqual(supplied))
                throw new InvalidOperationException("CONCURRENCY_CONFLICT");
        }

        private AdminActorContext GetActor()
        {
            var user = _httpContextAccessor.HttpContext?.User
                ?? throw new UnauthorizedAccessException("Không xác định được actor.");
            return _actorAccessor.Get(user);
        }

        private static void EnsureCanApprove(IReadOnlyList<string> roles)
        {
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                RoleConstants.BusinessOwner,
                RoleConstants.SystemAdmin,
                RoleConstants.AreaManager,
                RoleConstants.AccountantWarehouse
            };
            if (!roles.Any(allowed.Contains))
                throw new UnauthorizedAccessException("Role hiện tại không được duyệt tồn âm.");
        }

        private sealed record DocumentIssueEvaluation(
            InventoryDocumentDetail Detail,
            CafeChain.Models.Stores.StoreInventory Inventory,
            InventoryIssueDecision Decision);

        private static string GetCreateActionName(CreateInventoryDocumentDTO dto)
        {
            return dto.Type switch
            {
                InventoryDocumentType.IMPORT => "InventoryDocument.CreateImport",
                InventoryDocumentType.EXPORT => "InventoryDocument.CreateExport",
                _ => $"InventoryDocument.Create.{dto.Type}"
            };
        }

        private static string GetConfirmActionName(InventoryDocument document)
        {
            return document.Type switch
            {
                InventoryDocumentType.IMPORT => "InventoryDocument.ConfirmImport",
                InventoryDocumentType.EXPORT => "InventoryDocument.ConfirmExport",
                _ => $"InventoryDocument.Confirm.{document.Type}"
            };
        }

        private int GetCurrentStaffId()
        {
            return int.Parse(_httpContextAccessor.HttpContext!.User.FindFirst("StaffId")!.Value);
        }

        private static void EnsureRequestKey(string? requestKey)
        {
            if (string.IsNullOrWhiteSpace(requestKey))
            {
                throw new InvalidOperationException("RequestKey là bắt buộc.");
            }
        }

        private async Task MarkFailedIfPossibleAsync(
            RequestDeduplicationBeginResult? dedup,
            string message)
        {
            if (dedup?.Entry == null)
            {
                return;
            }

            try
            {
                await _deduplicationService.MarkFailedAsync(
                    dedup.Entry,
                    new { success = false, message });
            }
            catch
            {
                // Best effort only; the business transaction rollback remains the source of truth.
            }
        }

        private static string NormalizeDetailNote(string? note)
        {
            return string.IsNullOrWhiteSpace(note)
                ? string.Empty
                : note.Trim();
        }
    }
}
