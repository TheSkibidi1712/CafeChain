using CafeChain.Application.DTOs.Admin.InventoryDocuments.Create;
using CafeChain.Application.DTOs.Admin.InventoryDocuments.Snapshot;
using CafeChain.Application.Interfaces.Admin.InventoryDocuments;
using CafeChain.Infrastrusture.Interfaces.Admin.InventoryDocuments;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Documents;


namespace CafeChain.Application.Services.Admin.InventoryDocuments
{
    public class AdminInventoryDocumentValidationService : IAdminInventoryDocumentValidationService
    {
        private readonly IAdminInventoryDocumentRepository _repository;

        public AdminInventoryDocumentValidationService(IAdminInventoryDocumentRepository repository)
        {
            _repository = repository;
        }

        // =====================================================
        // BUSINESS VALIDATION
        // =====================================================

        public async Task ValidateCreateAsync(CreateInventoryDocumentDTO dto)
        {
            ValidateBasic(dto);

            ValidateImportPurpose(dto);

            ValidateExportPurpose(dto);

            ValidateAdjustmentNote(dto);

            ValidateAdjustmentPrice(dto);

            await ValidateStoreAsync(dto.StoreId);

            await ValidateSupplierAsync(dto);

            await ValidateDetailsAsync(dto);

            await ValidateIngredientExistsAsync(dto);

            await ValidateUnitsAsync(dto);

            await ValidateInventoryAsync(dto);
        }

        public async Task ValidateConfirmAsync(InventoryDocument document)
        {
            if (document.Status == InventoryDocumentStatus.CONFIRMED)
            {
                throw new InvalidOperationException("Phiếu đã được xác nhận.");
            }

            if (!document.Details.Any())
            {
                throw new InvalidOperationException("Phiếu không có chi tiết.");
            }
        }

        // ======================================
        // PRIVATE METHODS
        // ======================================

        private static void ValidateBasic(CreateInventoryDocumentDTO dto)
        {
            if (dto.DocumentDate == default)
            {
                throw new InvalidOperationException("Ngày chứng từ không hợp lệ.");
            }

            if (dto.Details == null || !dto.Details.Any())
            {
                throw new InvalidOperationException("Phiếu phải có ít nhất một nguyên liệu.");
            }
        }

        private async Task ValidateStoreAsync(int storeId)
        {
            var store = await _repository.GetStoreAsync(storeId);

            if (store == null)
            {
                throw new InvalidOperationException("Cửa hàng không tồn tại.");
            }

            if (!store.Active)
            {
                throw new InvalidOperationException("Cửa hàng đã ngừng hoạt động.");
            }
        }

        private async Task ValidateSupplierAsync(CreateInventoryDocumentDTO dto)
        {
            if (dto.Type != InventoryDocumentType.IMPORT
                || dto.Purpose != InventoryDocumentPurpose.IMPORT_PURCHASE)
            {
                return;
            }

            if (!dto.SupplierId.HasValue)
            {
                throw new InvalidOperationException("Chưa chọn nhà cung cấp.");
            }

            var supplier = await _repository.GetSupplierAsync(dto.SupplierId.Value);

            if (supplier == null)
            {
                throw new InvalidOperationException("Nhà cung cấp không tồn tại.");
            }

            if (!supplier.Active)
            {
                throw new InvalidOperationException("Nhà cung cấp đã ngừng hoạt động.");
            }
        }

        private static Task ValidateDetailsAsync(CreateInventoryDocumentDTO dto)
        {
            foreach (var item in dto.Details)
            {
                if (item.Quantity <= 0)
                {
                    throw new InvalidOperationException("Số lượng phải lớn hơn 0.");
                }

                if (item.BaseQuantity <= 0)
                {
                    throw new InvalidOperationException("Base Quantity không hợp lệ.");
                }

                if (item.UnitId <= 0)
                {
                    throw new InvalidOperationException("Đơn vị tính không hợp lệ.");
                }
            }

            return Task.CompletedTask;
        }

        private async Task ValidateIngredientExistsAsync(CreateInventoryDocumentDTO dto)
        {
            foreach (var item in dto.Details)
            {
                var ingredient =
                    await _repository.GetIngredientAsync(item.IngredientId);

                if (ingredient == null)
                {
                    throw new InvalidOperationException("Nguyên liệu không tồn tại.");
                }
            }
        }

        private async Task ValidateUnitsAsync(CreateInventoryDocumentDTO dto)
        {
            foreach (var item in dto.Details)
            {
                var unit =
                    await _repository.GetUnitAsync(item.UnitId);

                if (unit == null)
                {
                    throw new InvalidOperationException("Đơn vị tính không hợp lệ.");
                }
            }
        }

        private async Task ValidateInventoryAsync(CreateInventoryDocumentDTO dto)
        {
            if (IsIncreaseDocument(dto.Type))
            {
                return;
            }

            foreach (var item in dto.Details)
            {
                var inventory =
                    await _repository.GetStoreInventoryAsync(
                        dto.StoreId,
                        item.IngredientId);

                if (inventory == null)
                {
                    throw new InvalidOperationException(
                        $"Không tồn tại tồn kho cho nguyên liệu {item.IngredientId}");
                }

                if (inventory.AvailableQty < item.BaseQuantity)
                {
                    throw new InvalidOperationException(
                        $"Không đủ tồn kho.");
                }
            }
        }

        private static void ValidateImportPurpose(CreateInventoryDocumentDTO dto)
        {
            if (dto.Type != InventoryDocumentType.IMPORT)
            {
                return;
            }

            if (dto.Purpose != InventoryDocumentPurpose.IMPORT_PURCHASE
                && dto.Purpose != InventoryDocumentPurpose.IMPORT_INTERNAL
                && dto.Purpose != InventoryDocumentPurpose.IMPORT_ADJUSTMENT)
            {
                throw new InvalidOperationException("Mục đích phiếu nhập không hợp lệ.");
            }
        }

        private static void ValidateAdjustmentNote(CreateInventoryDocumentDTO dto)
        {
            var isAdjustment =
                dto.Type == InventoryDocumentType.IMPORT
                    && dto.Purpose == InventoryDocumentPurpose.IMPORT_ADJUSTMENT
                || dto.Type == InventoryDocumentType.EXPORT
                    && dto.Purpose == InventoryDocumentPurpose.ADJUSTMENT_OUT;

            if (!isAdjustment)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(dto.Note))
            {
                throw new InvalidOperationException("Phiếu điều chỉnh phải có ghi chú lý do điều chỉnh.");
            }
        }

        private static void ValidateAdjustmentPrice(CreateInventoryDocumentDTO dto)
        {
            if (dto.Type != InventoryDocumentType.IMPORT
                || dto.Purpose != InventoryDocumentPurpose.IMPORT_ADJUSTMENT)
            {
                return;
            }

            if (dto.Details.Any(x => x.UnitPrice <= 0))
            {
                throw new InvalidOperationException("Phiếu nhập điều chỉnh phải có đơn giá lớn hơn 0.");
            }
        }

        private static void ValidateExportPurpose(CreateInventoryDocumentDTO dto)
        {
            if (dto.Type != InventoryDocumentType.EXPORT)
            {
                return;
            }

            if (dto.Purpose != InventoryDocumentPurpose.SALE
                && dto.Purpose != InventoryDocumentPurpose.INTERNAL_OUT
                && dto.Purpose != InventoryDocumentPurpose.GIFT
                && dto.Purpose != InventoryDocumentPurpose.DEBT
                && dto.Purpose != InventoryDocumentPurpose.SAMPLE
                && dto.Purpose != InventoryDocumentPurpose.ADJUSTMENT_OUT)
            {
                throw new InvalidOperationException("Mục đích phiếu xuất không hợp lệ.");
            }
        }

        private static bool IsIncreaseDocument(InventoryDocumentType type)
        {
            return type == InventoryDocumentType.IMPORT
                || type == InventoryDocumentType.ADJUSTMENT_IN
                || type == InventoryDocumentType.INTERNAL_IMPORT
                || type == InventoryDocumentType.PRODUCTION_IN;
        }

        private static void ValidateExportSnapshot(InventoryDocumentSnapshotDTO? snapshot)
        {
            if (snapshot == null)
            {
                throw new InvalidOperationException(
                    "Phiếu chưa được snapshot.");
            }
        }

        private async Task ValidateSnapshotAsync(int documentId)
        {
            if (!await _repository.SnapshotExistsAsync(documentId))
            {
                throw new InvalidOperationException("Snapshot chưa tồn tại.");
            }
        }
    }
}
