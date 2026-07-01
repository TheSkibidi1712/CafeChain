using CafeChain.Application.DTOs.Admin.InventoryDocuments.Create;
using CafeChain.Application.Interfaces.Admin.InventoryDocuments;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Documents;
using CafeChain.Models.Inventories.Ingredients;
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
            return new AdminInventoryDocumentCreateVM
            {
                Type = type,
                DocumentDate = DateTime.Now,
                Code = await _repository.GenerateDocumentCodeAsync(type),
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
                        CurrentPrice = x.CurrentPrice,
                        MinimumOrderQuantity = x.MinimumOrderQuantity,
                        BaseUnitId = x.Ingredient.BaseUnitId,
                        BaseUnitName = x.Ingredient.BaseUnit.Name,
                        BaseUnitCode = x.Ingredient.BaseUnit.UnitCode,
                        ConversionFactorToBase = conversionFactor ?? 0,
                        CanConvertToBase = conversionFactor.HasValue
                    };
                })
                .ToList();
        }

        public async Task<InventoryCreateSummaryDTO> CalculateSummaryAsync(CreateInventoryDocumentDTO dto)
        {
            await NormalizeCreateDetailsAsync(dto);

            return await BuildSummaryAsync(dto);
        }

        // =====================================================
        // CREATE METHODS
        // =====================================================

        public async Task<int> SaveDraftAsync(CreateInventoryDocumentDTO dto)
        {
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
            await _repository.BeginTransactionAsync();

            try
            {
                await NormalizeCreateDetailsAsync(dto);

                await _validationService.ValidateCreateAsync(dto);

                var document = await CreateDocumentAsync(dto);

                await CreateDetailsAsync(document.InventoryDocumentId, dto);

                document = await _repository.GetDocumentForConfirmAsync(document.InventoryDocumentId) ?? throw new Exception("Không tìm thấy chứng từ.");

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
                Code = await _repository.GenerateDocumentCodeAsync(dto.Type),
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
                    Code = await _repository.GenerateDocumentCodeAsync(dto.Type),
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
