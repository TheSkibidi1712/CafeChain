using CafeChain.Application.DTOs.Admin.InventoryDocuments.Create;
using CafeChain.Application.Interfaces.Admin.InventoryDocuments;
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

        public AdminInventoryDocumentCreateService(
            IAdminInventoryDocumentRepository repository,
            IAdminInventoryDocumentValidationService validationService,
            IAdminInventoryDocumentConfirmService confirmService,
            IAdminInventoryDocumentSnapshotService snapshotService,
            IHttpContextAccessor httpContextAccessor)
        {
            _repository = repository;
            _validationService = validationService;
            _confirmService = confirmService;
            _snapshotService = snapshotService;
            _httpContextAccessor = httpContextAccessor;
        }

        // =====================================================
        // CREATE PAGE
        // =====================================================
        public async Task<AdminInventoryDocumentCreateVM> GetCreateDataAsync(InventoryDocumentType type)
        {
            var effectiveType =
                type == InventoryDocumentType.ADJUSTMENT_IN
                || type == InventoryDocumentType.INTERNAL_IMPORT
                    ? InventoryDocumentType.IMPORT
                    : type;

            var purpose =
                type == InventoryDocumentType.ADJUSTMENT_IN
                    ? InventoryDocumentPurpose.IMPORT_ADJUSTMENT
                    : type == InventoryDocumentType.INTERNAL_IMPORT
                    ? InventoryDocumentPurpose.IMPORT_INTERNAL
                    : effectiveType == InventoryDocumentType.IMPORT
                    ? InventoryDocumentPurpose.IMPORT_PURCHASE
                    : InventoryDocumentPurpose.NONE;

            return new AdminInventoryDocumentCreateVM
            {
                Type = effectiveType,
                Purpose = purpose,
                DocumentDate = DateTime.Now,
                Code = await _repository.GenerateDocumentCodeAsync(
                    effectiveType,
                    purpose == InventoryDocumentPurpose.NONE ? null : purpose),
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
                    var conversionFactor =
                        CalculateConversionFactorToBase(
                            x.Ingredient,
                            x.UnitId,
                            throwIfMissing: false);

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

        public async Task<List<SupplierIngredientDTO>> GetActiveIngredientsAsync(
            int storeId,
            InventoryDocumentPurpose purpose)
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

            var inventories =
                await _repository.GetStoreInventoriesAsync(storeId);

            var availableInventories =
                inventories
                    .Where(x =>
                        x.IngredientId.HasValue
                        && x.Ingredient != null
                        && x.Ingredient.Active
                        && x.AvailableQty > 0)
                    .ToList();

            var ingredientIds =
                availableInventories
                    .Select(x => x.IngredientId!.Value)
                    .Distinct()
                    .ToList();

            var costLayers =
                await _repository.GetAvailableCostLayersAsync(storeId, ingredientIds);

            var supplierPrices =
                await _repository.GetActiveIngredientSuppliersByIngredientIdsAsync(ingredientIds);

            var priceLookup =
                BuildPriceLookup(costLayers, supplierPrices);

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

        public async Task<List<InternalTransferOptionDTO>> GetPendingInternalTransfersAsync(int storeId)
        {
            var transfers = await _repository.GetPendingTransfersToStoreAsync(storeId);

            return transfers
                .Select(x => new InternalTransferOptionDTO
                {
                    InventoryTransferId = x.InventoryTransferId,
                    ExportDocumentCode = x.ExportDocument?.Code ?? $"#{x.ExportDocumentId}",
                    FromStoreId = x.FromStoreId,
                    FromStoreName = x.FromStore?.Name ?? string.Empty,
                    ToStoreId = x.ToStoreId,
                    ToStoreName = x.ToStore?.Name ?? string.Empty,
                    CreatedAt = x.CreatedAt,
                    TotalExportQuantity = x.TotalExportQty,
                    TotalReceivedQuantity = x.TotalReceivedQty
                })
                .ToList();
        }

        public async Task<List<SupplierIngredientDTO>> GetInternalTransferIngredientsAsync(int transferId)
        {
            var transfer = await _repository.GetTransferForInternalImportAsync(transferId)
                ?? throw new InvalidOperationException("Phiếu chuyển nội bộ không tồn tại.");

            if (transfer.ImportDocumentId.HasValue
                || transfer.Status == InventoryTransferStatus.COMPLETED
                || transfer.Status == InventoryTransferStatus.CANCELLED)
            {
                throw new InvalidOperationException("Phiếu chuyển nội bộ không còn chờ nhận.");
            }

            return transfer.Details
                .OrderBy(x => x.Ingredient.Name)
                .Select(x =>
                {
                    var remainingQuantity = x.ExportQuantity - x.ReceivedQuantity;

                    return new SupplierIngredientDTO
                    {
                        IngredientId = x.IngredientId,
                        IngredientName = x.Ingredient.Name,
                        UnitId = x.Ingredient.BaseUnitId,
                        UnitName = x.Ingredient.BaseUnit?.Name ?? string.Empty,
                        UnitCode = x.Ingredient.BaseUnit?.UnitCode ?? string.Empty,
                        CurrentPrice = x.UnitPrice ?? 0,
                        MinimumOrderQuantity = remainingQuantity,
                        BaseUnitId = x.Ingredient.BaseUnitId,
                        BaseUnitName = x.Ingredient.BaseUnit?.Name ?? string.Empty,
                        BaseUnitCode = x.Ingredient.BaseUnit?.UnitCode ?? string.Empty,
                        ConversionFactorToBase = 1,
                        CanConvertToBase = true,
                        AvailableBaseQuantity = remainingQuantity,
                        SuggestedBaseUnitCost = x.UnitPrice ?? 0,
                        SuggestedUnitPrice = x.UnitPrice ?? 0,
                        PriceSource = "Phiếu xuất nội bộ",
                        IsQuantityLocked = true,
                        IsPriceLocked = true,
                        UnitOptions = BuildBaseUnitOptions(x.Ingredient)
                    };
                })
                .Where(x => x.MinimumOrderQuantity > 0)
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

            if (IsInternalImport(dto))
            {
                throw new InvalidOperationException("Nhập nội bộ phải tạo trực tiếp từ phiếu xuất nội bộ và xác nhận ngay.");
            }

            if (dto.Type == InventoryDocumentType.EXPORT
                && dto.Purpose == InventoryDocumentPurpose.INTERNAL_OUT)
            {
                throw new InvalidOperationException("Xuất nội bộ phải tạo và xác nhận ngay để sinh phiếu chuyển nội bộ.");
            }

            await NormalizeCreateDetailsAsync(dto);

            await _validationService.ValidateCreateAsync(dto);

            if (dto.Details == null || !dto.Details.Any())
            {
                throw new Exception("Phiếu phải có ít nhất 1 nguyên liệu.");
            }

            await _repository.BeginTransactionAsync();

            try
            {
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

            await _repository.BeginTransactionAsync();

            try
            {
                await NormalizeCreateDetailsAsync(dto);

                await _validationService.ValidateCreateAsync(dto);

                await ValidateInternalExportAsync(dto);

                await ValidateInternalImportAsync(dto);

                var document = await CreateDocumentAsync(dto);

                await CreateDetailsAsync(document.InventoryDocumentId, dto);

                document = await _repository.GetDocumentForConfirmAsync(document.InventoryDocumentId) ?? throw new Exception("Không tìm thấy chứng từ.");

                var processResult =
                    await _confirmService.ConfirmDocumentAsync(
                        document,
                        GetCurrentStaffId());

                await ApplyInternalImportAsync(dto, document);

                await ApplyInternalExportAsync(dto, document);

                await _repository.SaveChangesAsync();

                await _repository.CommitTransactionAsync();

                return new InventoryDocumentMutationResultDTO
                {
                    DocumentId = document.InventoryDocumentId,
                    Warnings = processResult.Warnings
                };
            }
            catch
            {
                await _repository.RollbackTransactionAsync();
                throw;
            }
        }

        public async Task<InventoryDocumentMutationResultDTO?> ConfirmDraftAsync(int documentId)
        {
            await _repository.BeginTransactionAsync();

            try
            {
                var document =
                    await _repository.GetDocumentForConfirmAsync(documentId);

                if (document == null)
                {
                    await _repository.RollbackTransactionAsync();

                    return null;
                }

                if (document.Status == InventoryDocumentStatus.CANCELLED)
                {
                    throw new InvalidOperationException("Phiếu đã hủy, không thể xác nhận.");
                }

                if (IsInternalImport(document))
                {
                    throw new InvalidOperationException("Phiếu nhập nội bộ phải được xác nhận trực tiếp từ phiếu xuất nội bộ.");
                }

                if (document.Type == InventoryDocumentType.EXPORT
                    && document.Purpose == InventoryDocumentPurpose.INTERNAL_OUT)
                {
                    throw new InvalidOperationException("Phiếu xuất nội bộ phải tạo mới và xác nhận trực tiếp để sinh phiếu chuyển nội bộ.");
                }

                if (document.Status == InventoryDocumentStatus.CONFIRMED)
                {
                    await _repository.RollbackTransactionAsync();

                    return new InventoryDocumentMutationResultDTO
                    {
                        DocumentId = document.InventoryDocumentId
                    };
                }

                if (document.Status != InventoryDocumentStatus.DRAFT
                    && document.Status != InventoryDocumentStatus.PENDING)
                {
                    throw new InvalidOperationException("Trạng thái phiếu không hợp lệ để xác nhận.");
                }

                var processResult =
                    await _confirmService.ConfirmDocumentAsync(
                        document,
                        GetCurrentStaffId());

                await _repository.SaveChangesAsync();

                await _repository.CommitTransactionAsync();

                return new InventoryDocumentMutationResultDTO
                {
                    DocumentId = document.InventoryDocumentId,
                    Warnings = processResult.Warnings
                };
            }
            catch
            {
                await _repository.RollbackTransactionAsync();

                throw;
            }
        }

        public async Task<bool> CancelInventoryDocumentAsync(int documentId)
        {
            await _repository.BeginTransactionAsync();

            try
            {
                var document =
                    await _repository.GetByIdAsync(documentId);

                if (document == null)
                {
                    await _repository.RollbackTransactionAsync();

                    return false;
                }

                if (document.Status == InventoryDocumentStatus.CONFIRMED)
                {
                    throw new InvalidOperationException("Phiếu đã xác nhận, không thể hủy.");
                }

                if (document.Status == InventoryDocumentStatus.CANCELLED)
                {
                    await _repository.RollbackTransactionAsync();

                    return true;
                }

                document.Status = InventoryDocumentStatus.CANCELLED;
                document.IsProcessing = false;

                _repository.UpdateDocument(document);

                await _repository.SaveChangesAsync();

                await _repository.CommitTransactionAsync();

                return true;
            }
            catch
            {
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
                        TotalAmount = x.TotalAmount,
                        Note = NormalizeDetailNote(dto.Note)
                    });

            await _repository.AddDocumentDetailsAsync(details);

            await _repository.SaveChangesAsync();
        }

        private async Task ValidateInternalExportAsync(CreateInventoryDocumentDTO dto)
        {
            if (dto.Type != InventoryDocumentType.EXPORT
                || dto.Purpose != InventoryDocumentPurpose.INTERNAL_OUT)
            {
                return;
            }

            if (!dto.TargetStoreId.HasValue)
            {
                throw new InvalidOperationException("Chưa chọn cửa hàng nhận cho phiếu xuất nội bộ.");
            }

            if (dto.TargetStoreId.Value == dto.StoreId)
            {
                throw new InvalidOperationException("Cửa hàng nhận phải khác cửa hàng xuất.");
            }

            var targetStore =
                await _repository.GetStoreAsync(dto.TargetStoreId.Value)
                ?? throw new InvalidOperationException("Cửa hàng nhận không tồn tại.");

            if (!targetStore.Active)
            {
                throw new InvalidOperationException("Cửa hàng nhận đã ngừng hoạt động.");
            }

            dto.PartnerType = InventoryPartnerType.STORE;
            dto.PartnerId = targetStore.StoreId;
            dto.PartnerName = targetStore.Name;
            dto.SupplierId = null;
        }

        private async Task ValidateInternalImportAsync(CreateInventoryDocumentDTO dto)
        {
            if (!IsInternalImport(dto))
            {
                return;
            }

            if (!dto.SourceTransferId.HasValue)
            {
                throw new InvalidOperationException("Chưa chọn phiếu xuất nội bộ để nhập.");
            }

            var transfer =
                await _repository.GetTransferForInternalImportAsync(dto.SourceTransferId.Value)
                ?? throw new InvalidOperationException("Phiếu xuất nội bộ không tồn tại.");

            if (transfer.ImportDocumentId.HasValue
                || transfer.Status == InventoryTransferStatus.COMPLETED
                || transfer.Status == InventoryTransferStatus.CANCELLED)
            {
                throw new InvalidOperationException("Phiếu xuất nội bộ không còn ở trạng thái chờ nhận.");
            }

            if (transfer.ToStoreId != dto.StoreId)
            {
                throw new InvalidOperationException("Cửa hàng nhận không khớp với phiếu xuất nội bộ.");
            }

            var detailsByIngredient =
                dto.Details
                    .GroupBy(x => x.IngredientId)
                    .ToDictionary(
                        x => x.Key,
                        x => x.Sum(item => item.BaseQuantity));

            foreach (var transferDetail in transfer.Details)
            {
                var remainingQuantity =
                    transferDetail.ExportQuantity - transferDetail.ReceivedQuantity;

                if (remainingQuantity <= 0)
                {
                    continue;
                }

                if (!detailsByIngredient.TryGetValue(transferDetail.IngredientId, out var receivedQuantity))
                {
                    throw new InvalidOperationException(
                        $"Thiếu nguyên liệu {transferDetail.Ingredient.Name} trong phiếu nhập nội bộ.");
                }

                if (!IsSameQuantity(receivedQuantity, remainingQuantity))
                {
                    throw new InvalidOperationException(
                        $"Số lượng nhận của {transferDetail.Ingredient.Name} phải bằng số lượng còn chờ nhận ({FormatQuantity(remainingQuantity)}).");
                }
            }

            var allowedIngredientIds =
                transfer.Details
                    .Where(x => x.ExportQuantity - x.ReceivedQuantity > 0)
                    .Select(x => x.IngredientId)
                    .ToHashSet();

            if (detailsByIngredient.Keys.Any(x => !allowedIngredientIds.Contains(x)))
            {
                throw new InvalidOperationException("Phiếu nhập nội bộ có nguyên liệu không thuộc phiếu xuất nội bộ.");
            }

            dto.Purpose = InventoryDocumentPurpose.IMPORT_INTERNAL;
            dto.PartnerType = InventoryPartnerType.STORE;
            dto.PartnerId = transfer.FromStoreId;
            dto.PartnerName = transfer.FromStore?.Name;
            dto.SupplierId = null;
        }

        private async Task ApplyInternalImportAsync(CreateInventoryDocumentDTO dto, InventoryDocument document)
        {
            if (!IsInternalImport(dto))
            {
                return;
            }

            if (!dto.SourceTransferId.HasValue)
            {
                throw new InvalidOperationException("Thiếu phiếu xuất nội bộ nguồn.");
            }

            var transfer =
                await _repository.GetTransferForInternalImportAsync(dto.SourceTransferId.Value)
                ?? throw new InvalidOperationException("Phiếu xuất nội bộ không tồn tại.");

            var receivedByIngredient =
                dto.Details
                    .GroupBy(x => x.IngredientId)
                    .ToDictionary(
                        x => x.Key,
                        x => x.Sum(item => item.BaseQuantity));

            foreach (var transferDetail in transfer.Details)
            {
                if (!receivedByIngredient.TryGetValue(transferDetail.IngredientId, out var receivedQuantity))
                {
                    continue;
                }

                transferDetail.ReceivedQuantity += receivedQuantity;
            }

            transfer.ImportDocumentId = document.InventoryDocumentId;
            transfer.TotalReceivedQty = transfer.Details.Sum(x => x.ReceivedQuantity);
            transfer.Status = InventoryTransferStatus.COMPLETED;

            _repository.UpdateTransfer(transfer);
        }

        private async Task ApplyInternalExportAsync(CreateInventoryDocumentDTO dto, InventoryDocument document)
        {
            if (dto.Type != InventoryDocumentType.EXPORT
                || dto.Purpose != InventoryDocumentPurpose.INTERNAL_OUT)
            {
                return;
            }

            if (!dto.TargetStoreId.HasValue)
            {
                throw new InvalidOperationException("Thiếu cửa hàng nhận cho phiếu xuất nội bộ.");
            }

            var transfer =
                new InventoryTransfer
                {
                    ExportDocumentId = document.InventoryDocumentId,
                    FromStoreId = document.StoreId,
                    ToStoreId = dto.TargetStoreId.Value,
                    TotalExportQty = document.Details.Sum(x => x.BaseQuantity),
                    TotalReceivedQty = 0,
                    Status = InventoryTransferStatus.PENDING,
                    CreatedAt = DateTime.UtcNow,
                    Details = document.Details
                        .Select(x =>
                            new InventoryTransferDetail
                            {
                                IngredientId = x.IngredientId,
                                ExportQuantity = x.BaseQuantity,
                                ReceivedQuantity = 0,
                                UnitPrice = x.CostPrice,
                                Note = x.Note
                            })
                        .ToList()
                };

            await _repository.AddTransferAsync(transfer);
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

            if (dto.Type == InventoryDocumentType.INTERNAL_IMPORT)
            {
                dto.Type = InventoryDocumentType.IMPORT;
                dto.Purpose = InventoryDocumentPurpose.IMPORT_INTERNAL;
                dto.SupplierId = null;
                return;
            }

            if (dto.Type == InventoryDocumentType.IMPORT
                && dto.Purpose == InventoryDocumentPurpose.NONE)
            {
                dto.Purpose = InventoryDocumentPurpose.IMPORT_PURCHASE;
            }

            if (dto.Type == InventoryDocumentType.IMPORT)
            {
                if (dto.Purpose == InventoryDocumentPurpose.IMPORT_PURCHASE
                    && dto.SupplierId.HasValue)
                {
                    dto.PartnerType = InventoryPartnerType.SUPPLIER;
                    dto.PartnerId = dto.SupplierId;
                }

                if (dto.Purpose != InventoryDocumentPurpose.IMPORT_PURCHASE)
                {
                    dto.SupplierId = null;
                }

                if (dto.Purpose != InventoryDocumentPurpose.IMPORT_INTERNAL)
                {
                    dto.SourceTransferId = null;
                }

                if (dto.Purpose == InventoryDocumentPurpose.IMPORT_ADJUSTMENT)
                {
                    ClearPartner(dto);
                }
            }

            if (dto.Type == InventoryDocumentType.EXPORT)
            {
                if (dto.Purpose == InventoryDocumentPurpose.SALE
                    && !string.IsNullOrWhiteSpace(dto.PartnerName))
                {
                    dto.PartnerType = InventoryPartnerType.CUSTOMER;
                    dto.PartnerId = null;
                    dto.PartnerName = dto.PartnerName.Trim();
                }
                else if (dto.Purpose == InventoryDocumentPurpose.SALE)
                {
                    ClearPartner(dto);
                }

                if (dto.Purpose != InventoryDocumentPurpose.INTERNAL_OUT)
                {
                    dto.TargetStoreId = null;
                }

                if (dto.Purpose == InventoryDocumentPurpose.ADJUSTMENT_OUT)
                {
                    ClearPartner(dto);
                }
            }
        }

        private static void ClearPartner(CreateInventoryDocumentDTO dto)
        {
            dto.PartnerType = InventoryPartnerType.NONE;
            dto.PartnerId = null;
            dto.PartnerName = null;
        }

        private static bool IsInternalImport(CreateInventoryDocumentDTO dto)
        {
            return dto.Type == InventoryDocumentType.IMPORT
                && dto.Purpose == InventoryDocumentPurpose.IMPORT_INTERNAL;
        }

        private static bool IsInternalImport(InventoryDocument document)
        {
            return (document.Type == InventoryDocumentType.IMPORT
                    && document.Purpose == InventoryDocumentPurpose.IMPORT_INTERNAL)
                || document.Type == InventoryDocumentType.INTERNAL_IMPORT;
        }

        private static SupplierIngredientDTO BuildStoreIngredientDto(
            Ingredient ingredient,
            decimal availableBaseQuantity,
            IReadOnlyDictionary<int, (decimal BaseUnitCost, string PriceSource)> priceLookup,
            bool isPriceLocked,
            bool isQuantityLocked)
        {
            var unitOptions =
                BuildUnitOptions(ingredient);

            var defaultUnit =
                unitOptions.FirstOrDefault(x => x.IsBaseUnit)
                ?? unitOptions.FirstOrDefault();

            var hasPrice =
                priceLookup.TryGetValue(
                    ingredient.IngredientId,
                    out var price);

            var baseUnitCost =
                hasPrice ? price.BaseUnitCost : 0;

            var conversionFactor =
                defaultUnit?.ConversionFactorToBase ?? 0;

            var unitPrice =
                conversionFactor > 0
                    ? baseUnitCost * conversionFactor
                    : 0;

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

        private static Dictionary<int, (decimal BaseUnitCost, string PriceSource)> BuildPriceLookup(
            IEnumerable<InventoryCostLayer> costLayers,
            IEnumerable<IngredientSupplier> supplierPrices)
        {
            var result =
                costLayers
                    .GroupBy(x => x.IngredientId)
                    .Where(x => x.Sum(layer => layer.RemainingQuantity) > 0)
                    .ToDictionary(
                        x => x.Key,
                        x =>
                        {
                            var quantity =
                                x.Sum(layer => layer.RemainingQuantity);

                            var amount =
                                x.Sum(layer => layer.RemainingQuantity * layer.UnitCost);

                            return (
                                BaseUnitCost: amount / quantity,
                                PriceSource: "Giá vốn FIFO bình quân còn tồn");
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

        private static List<InventoryIngredientUnitOptionDTO> BuildUnitOptions(
            Ingredient ingredient)
        {
            var options =
                new List<InventoryIngredientUnitOptionDTO>();

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

        private static List<InventoryIngredientUnitOptionDTO> BuildBaseUnitOptions(
            Ingredient ingredient)
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

        private static List<InventoryIngredientUnitOptionDTO> BuildSupplierUnitOptions(
            IngredientSupplier supplier,
            decimal? conversionFactor)
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

        private static decimal CalculateSupplierBaseUnitCost(
            IngredientSupplier supplier)
        {
            var conversionFactor =
                CalculateConversionFactorToBase(
                    supplier.Ingredient,
                    supplier.UnitId,
                    throwIfMissing: false);

            if (!conversionFactor.HasValue || conversionFactor.Value <= 0)
            {
                return 0;
            }

            return GetCurrentSupplierPrice(supplier) / conversionFactor.Value;
        }

        private static decimal GetCurrentSupplierPrice(
            IngredientSupplier supplier)
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

        private static DateTime GetSupplierPriceEffectiveDate(
            IngredientSupplier supplier)
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

            foreach (var item in dto.Details)
            {
                if (item.Quantity <= 0)
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

                item.TotalAmount =
                    item.Quantity * item.UnitPrice;
            }
        }

        private async Task<InventoryCreateSummaryDTO> BuildSummaryAsync(CreateInventoryDocumentDTO dto)
        {
            InventoryCreateSummaryDTO summary = new()
            {
                TotalItems = dto.Details.Count,
                TotalQuantity = dto.Details.Sum(x => x.Quantity),
                TotalAmount = dto.Details.Sum(x => x.TotalAmount),
                VatRate = 0,
                VatAmount = 0
            };

            summary.FinalAmount = summary.TotalAmount + summary.VatAmount;
            summary.BaseQuantities = await BuildBaseQuantitySummaryAsync(dto);
            summary.BaseQuantityText = FormatBaseQuantityText(summary.BaseQuantities);

            return summary;
        }

        private async Task<List<InventoryBaseQuantitySummaryDTO>> BuildBaseQuantitySummaryAsync(
            CreateInventoryDocumentDTO dto)
        {
            var result = new Dictionary<int, InventoryBaseQuantitySummaryDTO>();

            foreach (var item in dto.Details)
            {
                if (item.IngredientId <= 0 || item.BaseQuantity <= 0)
                {
                    continue;
                }

                var ingredient =
                    await _repository.GetIngredientAsync(item.IngredientId);

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

            return result
                .Values
                .OrderBy(x => x.UnitCode)
                .ToList();
        }

        private static decimal? CalculateConversionFactorToBase(
            Ingredient ingredient,
            int unitId,
            bool throwIfMissing)
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

        private static string FormatBaseQuantityText(
            IEnumerable<InventoryBaseQuantitySummaryDTO> baseQuantities)
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

        private int GetCurrentStaffId()
        {
            return int.Parse(_httpContextAccessor.HttpContext!.User.FindFirst("StaffId")!.Value);
        }

        private static string NormalizeDetailNote(string? note)
        {
            return string.IsNullOrWhiteSpace(note)
                ? string.Empty
                : note.Trim();
        }
    }
}
