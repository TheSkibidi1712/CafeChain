using CafeChain.Application.DTOs.Admin.InventoryDocuments.Create;
using CafeChain.Application.Interfaces.Admin.InventoryDocuments;
using CafeChain.Infrastrusture.Interfaces.Admin.InventoryDocuments;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Auditing;
using CafeChain.Models.Inventories.Documents;
using System.Diagnostics;
using System.Text.Json;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Model;

namespace CafeChain.Application.Services.Admin.InventoryDocuments
{
    public class AdminInventoryDocumentConfirmService : IAdminInventoryDocumentConfirmService
    {
        private readonly IAdminInventoryDocumentRepository _repository;

        private readonly IAdminInventoryDocumentValidationService _validationService;

        private readonly IAdminInventoryDocumentExportService _exportService;

        private readonly IAdminInventoryDocumentProcessService _processService;

        private readonly IAdminInventoryDocumentSnapshotService _snapshotService;

        private readonly IHttpContextAccessor _httpContextAccessor;

        private readonly IUserContext _userContext;

        public AdminInventoryDocumentConfirmService(
            IAdminInventoryDocumentRepository repository,
            IAdminInventoryDocumentValidationService validationService,
            IAdminInventoryDocumentProcessService processService,
            IAdminInventoryDocumentExportService exportService,
            IAdminInventoryDocumentSnapshotService snapshotService,
            IUserContext userContext,
            IHttpContextAccessor httpContextAccessor)
        {
            _repository = repository;
            _validationService = validationService;
            _exportService = exportService;
            _processService = processService;
            _snapshotService = snapshotService;
            _userContext = userContext;
            _httpContextAccessor = httpContextAccessor;

        }

        public async Task<InventoryDocumentMutationResultDTO?> ConfirmAsync(ConfirmInventoryDocumentDTO dto)
        {
            await _repository.BeginTransactionAsync();

            try
            {
                var document = await _repository.GetDocumentForConfirmAsync(dto.InventoryDocumentId);

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

                var processResult =
                    await ConfirmDocumentAsync(document, dto.ConfirmedByStaffId);

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

        public async Task<InventoryProcessResultDTO> ConfirmDocumentAsync(InventoryDocument document, int confirmedByStaffId)
        {
            if (document.Status == InventoryDocumentStatus.CANCELLED)
            {
                throw new InvalidOperationException("Phiếu đã hủy, không thể xác nhận.");
            }

            await _validationService.ValidateConfirmAsync(document);

            var processResult =
                await _processService.ExecuteProcessAsync(document);

            document.Status = InventoryDocumentStatus.CONFIRMED;
            document.ConfirmedAt = DateTime.UtcNow;
            document.ConfirmedBy = confirmedByStaffId;

            _repository.UpdateDocument(document);

            await _snapshotService.CreateSnapshotAsync(document);

            var log =
                new AuditLog
                {
                    TableName = nameof(InventoryDocument),
                    RecordId = document.InventoryDocumentId,
                    Action = "CONFIRM",
                    NewData = JsonSerializer.Serialize(
                        BuildConfirmAuditPayload(document)),
                    UserId = _userContext.StaffId,
                    CreatedAt = DateTime.UtcNow
                };

            await _repository.AddAuditLogAsync(log);

            return processResult;
        }

        private static object BuildConfirmAuditPayload(
            InventoryDocument document)
        {
            return new
            {
                document.InventoryDocumentId,
                document.Code,
                document.Type,
                document.Status,
                document.StoreId,
                document.StaffId,
                document.SupplierId,
                document.PartnerType,
                document.PartnerId,
                document.PartnerName,
                document.Purpose,
                document.DocumentDate,
                document.TotalAmount,
                document.VatAmount,
                document.FinalAmount,
                document.ConfirmedAt,
                document.ConfirmedBy,
                Details =
                    document.Details
                        .Select(detail => new
                        {
                            detail.InventoryDocumentDetailId,
                            detail.IngredientId,
                            IngredientName =
                                detail.Ingredient?.Name,
                            detail.UnitId,
                            UnitName =
                                detail.Unit?.Name,
                            detail.Quantity,
                            detail.BaseQuantity,
                            detail.UnitPrice,
                            detail.CostPrice,
                            detail.CostAmount,
                            detail.TotalAmount,
                            detail.Note
                        })
                        .ToList()
            };
        }

    }
}
