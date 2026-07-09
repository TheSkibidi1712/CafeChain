using CafeChain.Application.DTOs.Admin.InventoryDocuments.Create;
using CafeChain.Application.DTOs.Systems;
using CafeChain.Application.Interfaces.Admin.InventoryDocuments;
using CafeChain.Application.Interfaces.Systems;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Costing;
using CafeChain.Models.Inventories.Documents;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Suppliers;
using CafeChain.Models.Inventories.Transfers;
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

        private readonly IAdminInventoryDocumentSnapshotService _snapshotService;

        private readonly IHttpContextAccessor _httpContextAccessor;

        private readonly IRequestDeduplicationService _deduplicationService;

        public AdminInventoryDocumentCreateService(
            IAdminInventoryDocumentRepository repository,
            IAdminInventoryDocumentValidationService validationService,
            IAdminInventoryDocumentConfirmService confirmService,
            IAdminInventoryDocumentSnapshotService snapshotService,
            IHttpContextAccessor httpContextAccessor,
            IRequestDeduplicationService deduplicationService)
        {
            _repository = repository;
            _validationService = validationService;
            _confirmService = confirmService;
            _snapshotService = snapshotService;
            _httpContextAccessor = httpContextAccessor;
            _deduplicationService = deduplicationService;
        }

        // =====================================================
        // CREATE PAGE
        // =====================================================
        public async Task<AdminInventoryDocumentCreateVM> GetCreateDataAsync(InventoryDocumentType type)
        {
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

            return new AdminInventoryDocumentCreateVM
            {
                Type = effectiveType,
                Purpose = purpose,
                DocumentDate = DateTime.Now,
                Code = await _repository.GenerateDocumentCodeAsync( effectiveType, purpose == InventoryDocumentPurpose.NONE ? null : purpose),
                Stores = await _repository.GetStoreDropdownAsync(),
                Suppliers = await _repository.GetSupplierDropdownAsync(),
                Summary = new InventoryCreateSummaryDTO()
            };
        }

        public async Task<List<SupplierIngredientDTO>> GetSupplierIngredientsAsync(int supplierId)
        {
            var ingredients = await _repository.GetSupplierIngredientsAsync(supplierId);

            return ingredients
                .Select(x =>
                {
                    var conversionFactor = CalculateConversionFactorToBase(x.Ingredient, x.UnitId, throwIfMissing: false);

                    return new SupplierIngredientDTO
                    {
                        IngredientSupplierId = x.IngredientSupplierId,
                        IngredientId = x.IngredientId,
                        IngredientName = x.Ingredient.Name,
                        UnitId = x.UnitId,
                        UnitName = x.Unit.Name,
                        UnitCode = x.Unit.UnitCode,
                        CurrentPrice = GetCurrentSupplierPrice(x),
                        MinimumOrderQuantity = x.MinimumOrderQuantity,
                        BaseUnitId = x.Ingredient.BaseUnitId,
                        BaseUnitName = x.Ingredient.BaseUnit.Name,
                        BaseUnitCode = x.Ingredient.BaseUnit.UnitCode,
                        ConversionFactorToBase = conversionFactor ?? 0,
                        CanConvertToBase = conversionFactor.HasValue,
                        SuggestedBaseUnitCost = CalculateSupplierBaseUnitCost(x),
                        SuggestedUnitPrice = GetCurrentSupplierPrice(x),
                        PriceSource = "Bảng giá nhà cung cấp",
                        IsQuantityLocked = false,
                        IsPriceLocked = true,
                        UnitOptions = BuildSupplierUnitOptions(x, conversionFactor)
                    };
                })
                .ToList();
        }

        public async Task<List<SupplierIngredientDTO>> GetActiveIngredientsAsync(int storeId, InventoryDocumentPurpose purpose)
        {
            if (storeId <= 0)
            {
                return [];
            }

            var ingredients = await _repository.GetActiveIngredientsAsync();
            var inventories = await _repository.GetStoreInventoriesAsync(storeId);
            var ingredientIds = ingredients.Select(x => x.IngredientId).ToList();
            var costLayers = await _repository.GetAvailableCostLayersAsync(storeId, ingredientIds);
            var supplierPrices = await _repository.GetActiveIngredientSuppliersByIngredientIdsAsync(ingredientIds);
            var inventoryByIngredient = inventories
                .Where(x => x.IngredientId.HasValue)
                .GroupBy(x => x.IngredientId!.Value)
                .ToDictionary(x => x.Key, x => x.First());

            var priceLookup = BuildPriceLookup(costLayers, supplierPrices);

            return ingredients
                .Select(x =>
                {
                    inventoryByIngredient.TryGetValue(x.IngredientId, out var inventory);

                    return BuildStoreIngredientDto(
                        x,
                        inventory?.AvailableQty ?? 0,
                        priceLookup,
                        isPriceLocked: false,
                        isQuantityLocked: false);
                })
                .ToList();
        }

        public async Task<List<SupplierIngredientDTO>> GetStoreExportIngredientsAsync(int storeId)
        {
            if (storeId <= 0)
            {
                return [];
            }

            var inventories = await _repository.GetStoreInventoriesAsync(storeId);

            var availableInventories =
                inventories
                    .Where(x =>
                        x.IngredientId.HasValue && x.Ingredient != null && x.Ingredient.Active && x.AvailableQty > 0)
                    .ToList();

            var ingredientIds =
                availableInventories
                    .Select(x => x.IngredientId!.Value)
                    .Distinct()
                    .ToList();

            var costLayers =  await _repository.GetAvailableCostLayersAsync(storeId, ingredientIds);

            var supplierPrices = await _repository.GetActiveIngredientSuppliersByIngredientIdsAsync(ingredientIds);

            var priceLookup = BuildPriceLookup(costLayers, supplierPrices);

            return availableInventories
                .OrderBy(x => x.Ingredient.Name)
                .Select(x =>
                    BuildStoreIngredientDto(
                        x.Ingredient,
                        x.AvailableQty,
                        priceLookup,
                        isPriceLocked: true,
                        isQuantityLocked: false))
                .ToList();
        }

        public async Task<InventoryCreateSummaryDTO> CalculateSummaryAsync(CreateInventoryDocumentDTO dto)
        {
            NormalizeImportDocumentType(dto);

            await NormalizeCreateDetailsAsync(dto);

            return await BuildSummaryAsync(dto);
        }

        // =====================================================
        // CREATE METHODS
        // =====================================================

        public async Task<int> SaveDraftAsync(CreateInventoryDocumentDTO dto)
        {
            NormalizeImportDocumentType(dto);

            EnsureRequestKey(dto.RequestKey);

            await ApplySupplierPartnerSnapshotAsync(dto);

            await NormalizeCreateDetailsAsync(dto);

            await _validationService.ValidateCreateAsync(dto);

            if (dto.Details == null || !dto.Details.Any())
            {
                throw new Exception("Phiếu phải có ít nhất 1 nguyên liệu.");
            }

            await _repository.BeginTransactionAsync();

            try
            {
                var dedup = await _deduplicationService.BeginAsync(
                    dto.RequestKey,
                    "InventoryDocument.CreateDraft",
                    GetCurrentStaffId(),
                    dto);

                if (!dedup.CanProcess)
                {
                    await _repository.RollbackTransactionAsync();

                    if (dedup.Status == "SUCCESS" && dedup.ReferenceId.HasValue)
                    {
                        return dedup.ReferenceId.Value;
                    }

                    throw new InvalidOperationException(dedup.ErrorMessage);
                }

                var summary = await BuildSummaryAsync(dto);

                var document = await BuildDraftDocument(dto, summary);

                await _repository.AddDocumentAsync(document);

                await _repository.SaveChangesAsync();

                var details = BuildDocumentDetails(document.InventoryDocumentId, dto);

                await _repository.AddDocumentDetailsAsync(details);

                await _repository.SaveChangesAsync();

                document =
                    await _repository.GetDocumentForConfirmAsync(document.InventoryDocumentId)
                    ?? throw new Exception("Không tìm thấy chứng từ.");

                await _snapshotService.CreateSnapshotAsync(document);

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
            NormalizeImportDocumentType(dto);

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

                    throw new InvalidOperationException(dedup.ErrorMessage);
                }

                await NormalizeCreateDetailsAsync(dto);

                await _validationService.ValidateCreateAsync(dto);

                var document = await CreateDocumentAsync(dto);

                await CreateDetailsAsync(document.InventoryDocumentId, dto);

                document = await _repository.GetDocumentForConfirmAsync(document.InventoryDocumentId) ?? throw new Exception("Không tìm thấy chứng từ.");

                var processResult = await _confirmService.ConfirmDocumentAsync(document, GetCurrentStaffId());

                await _repository.SaveChangesAsync();

                var response = new InventoryDocumentMutationResultDTO
                {
                    DocumentId = document.InventoryDocumentId,
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

        public async Task<InventoryDocumentMutationResultDTO?> ConfirmDraftAsync(int documentId, string? requestKey)
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
                            DocumentId = dedup.ReferenceId.Value
                        };
                    }

                    throw new InvalidOperationException(dedup.ErrorMessage);
                }

                if (document.Status == InventoryDocumentStatus.CANCELLED)
                {
                    throw new InvalidOperationException("Phiếu đã hủy, không thể xác nhận.");
                }

                if (document.Status == InventoryDocumentStatus.CONFIRMED)
                {
                    var alreadyConfirmedResponse = new InventoryDocumentMutationResultDTO
                    {
                        DocumentId = document.InventoryDocumentId
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

                var processResult = await _confirmService.ConfirmDocumentAsync(document, GetCurrentStaffId());

                await _repository.SaveChangesAsync();

                var response = new InventoryDocumentMutationResultDTO
                {
                    DocumentId = document.InventoryDocumentId,
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

        public async Task<bool> CancelInventoryDocumentAsync(int documentId, string? requestKey)
        {
            EnsureRequestKey(requestKey);

            RequestDeduplicationBeginResult? dedup = null;

            await _repository.BeginTransactionAsync();

            try
            {
                var document = await _repository.GetByIdAsync(documentId);

                if (document == null)
                {
                    await _repository.RollbackTransactionAsync();

                    return false;
                }

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

                    throw new InvalidOperationException(dedup.ErrorMessage);
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
                TotalAmount = summary.TotalAmount,
                VatAmount = summary.VatAmount,
                FinalAmount = summary.FinalAmount,
                IsProcessing = false
            };
        }

        private List<InventoryDocumentDetail> BuildDocumentDetails(int documentId, CreateInventoryDocumentDTO dto)
        {
            return dto.Details
                .Select(x => new InventoryDocumentDetail
                {
                    InventoryDocumentId = documentId,
                    IngredientId = x.IngredientId,
                    Quantity = x.Quantity,
                    BaseQuantity = x.BaseQuantity,
                    UnitId = x.UnitId,
                    UnitPrice = x.UnitPrice,
                    CostPrice = x.CostPrice,
                    CostAmount = x.CostAmount,
                    TotalAmount = x.TotalAmount,
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
                    Note = dto.Note
                };

            await _repository.AddDocumentAsync(document);

            await _repository.SaveChangesAsync();

            return document;
        }

        private async Task CreateDetailsAsync(int documentId, CreateInventoryDocumentDTO dto)
        {
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
                        UnitPrice = x.UnitPrice,
                        CostPrice = x.CostPrice,
                        CostAmount = x.CostAmount,
                        TotalAmount = x.TotalAmount,
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

        private static SupplierIngredientDTO BuildStoreIngredientDto(
            Ingredient ingredient,
            decimal availableBaseQuantity,
            IReadOnlyDictionary<int, (decimal BaseUnitCost, string PriceSource)> priceLookup,
            bool isPriceLocked,
            bool isQuantityLocked)
        {
            var unitOptions = BuildUnitOptions(ingredient);

            var defaultUnit = unitOptions.FirstOrDefault(x => x.IsBaseUnit) ?? unitOptions.FirstOrDefault();

            var hasPrice = priceLookup.TryGetValue(ingredient.IngredientId, out var price);

            var baseUnitCost = hasPrice ? price.BaseUnitCost : 0;

            var conversionFactor = defaultUnit?.ConversionFactorToBase ?? 0;

            var unitPrice = conversionFactor > 0 ? baseUnitCost * conversionFactor : 0;

            return new SupplierIngredientDTO
            {
                IngredientId = ingredient.IngredientId,
                IngredientName = ingredient.Name,
                UnitId = defaultUnit?.UnitId ?? ingredient.BaseUnitId,
                UnitName = defaultUnit?.UnitName ?? ingredient.BaseUnit?.Name ?? string.Empty,
                UnitCode = defaultUnit?.UnitCode ?? ingredient.BaseUnit?.UnitCode ?? string.Empty,
                CurrentPrice = unitPrice,
                BaseUnitId = ingredient.BaseUnitId,
                BaseUnitName = ingredient.BaseUnit?.Name ?? string.Empty,
                BaseUnitCode = ingredient.BaseUnit?.UnitCode ?? string.Empty,
                ConversionFactorToBase = conversionFactor,
                CanConvertToBase = conversionFactor > 0,
                AvailableBaseQuantity = availableBaseQuantity,
                SuggestedBaseUnitCost = baseUnitCost,
                SuggestedUnitPrice = unitPrice,
                PriceSource = hasPrice ? price.PriceSource : "Chưa có giá gợi ý",
                IsQuantityLocked = isQuantityLocked,
                IsPriceLocked = isPriceLocked,
                UnitOptions = unitOptions
            };
        }

        private static Dictionary<int, (decimal BaseUnitCost, string PriceSource)> BuildPriceLookup(IEnumerable<InventoryCostLayer> costLayers, IEnumerable<IngredientSupplier> supplierPrices)
        {
            var result =
                costLayers
                    .GroupBy(x => x.IngredientId)
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

                var baseCost =
                    CalculateSupplierBaseUnitCost(supplier);

                if (baseCost <= 0)
                {
                    continue;
                }

                result[group.Key] =
                    (baseCost, "Giá nhà cung cấp gần nhất");
            }

            return result;
        }

        private static List<InventoryIngredientUnitOptionDTO> BuildUnitOptions(Ingredient ingredient)
        {
            var options = new List<InventoryIngredientUnitOptionDTO>();

            if (ingredient.BaseUnit != null)
            {
                options.Add(
                    new InventoryIngredientUnitOptionDTO
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
                    new InventoryIngredientUnitOptionDTO
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

        private static List<InventoryIngredientUnitOptionDTO> BuildBaseUnitOptions(Ingredient ingredient)
        {
            return
            [
                new InventoryIngredientUnitOptionDTO
                {
                    UnitId = ingredient.BaseUnitId,
                    UnitName = ingredient.BaseUnit?.Name ?? string.Empty,
                    UnitCode = ingredient.BaseUnit?.UnitCode ?? string.Empty,
                    ConversionFactorToBase = 1,
                    IsBaseUnit = true
                }
            ];
        }

        private static List<InventoryIngredientUnitOptionDTO> BuildSupplierUnitOptions(IngredientSupplier supplier, decimal? conversionFactor)
        {
            return
            [
                new InventoryIngredientUnitOptionDTO
                {
                    UnitId = supplier.UnitId,
                    UnitName = supplier.Unit?.Name ?? string.Empty,
                    UnitCode = supplier.Unit?.UnitCode ?? string.Empty,
                    ConversionFactorToBase = conversionFactor ?? 0,
                    IsBaseUnit = supplier.UnitId == supplier.Ingredient.BaseUnitId
                }
            ];
        }

        private static decimal CalculateSupplierBaseUnitCost(IngredientSupplier supplier)
        {
            var conversionFactor = CalculateConversionFactorToBase(supplier.Ingredient, supplier.UnitId, throwIfMissing: false);

            if (!conversionFactor.HasValue || conversionFactor.Value <= 0)
            {
                return 0;
            }

            return GetCurrentSupplierPrice(supplier) / conversionFactor.Value;
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

            foreach (var item in dto.Details)
            {
                if (item.Quantity <= 0 && !isStockTake)
                {
                    continue;
                }

                var ingredient =
                    await _repository.GetIngredientAsync(item.IngredientId)
                    ?? throw new InvalidOperationException("Nguyên liệu không tồn tại.");

                var conversionFactor =
                    CalculateConversionFactorToBase(
                        ingredient,
                        item.UnitId,
                        throwIfMissing: true)
                    ?? 0;

                item.BaseQuantity =
                    item.Quantity * conversionFactor;

                if (isQuantityOnlyDocument)
                {
                    item.UnitPrice = 0;
                    item.TotalAmount = 0;
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
