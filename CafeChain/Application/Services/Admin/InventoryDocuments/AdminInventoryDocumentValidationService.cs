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

        public async Task ValidateCreateAsync(CreateInventoryDocumentDTO dto)
        {
            ValidateBasic(dto);
            ValidateImportPurpose(dto);
            ValidateExportPurpose(dto);
            ValidateWastePurpose(dto);
            ValidateStockTakePurpose(dto);
            ValidateNegativeStockOptIn(dto);
            ValidateAdjustmentNote(dto);
            ValidateWasteNote(dto);
            ValidateAdjustmentPrice(dto);

            await ValidateStoreAsync(dto.StoreId);
            await ValidateSupplierAsync(dto);
            await ValidateDetailsAsync(dto);
            await ValidateIngredientExistsAsync(dto);
            await ValidateUnitsAsync(dto);
            await ValidateStoreInventoryMembershipAsync(
                dto.StoreId,
                dto.Type,
                dto.Details.Select(x => x.IngredientId));
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

            ValidateConfirmPurpose(document);

            await ValidateStoreInventoryMembershipAsync(
                document.StoreId,
                document.Type,
                document.Details.Select(x => x.IngredientId));
        }

        private async Task ValidateStoreInventoryMembershipAsync(
            int storeId,
            InventoryDocumentType type,
            IEnumerable<int> ingredientIds)
        {
            if (type is not InventoryDocumentType.EXPORT
                and not InventoryDocumentType.WASTE
                and not InventoryDocumentType.STOCK_TAKE)
            {
                return;
            }

            foreach (var ingredientId in ingredientIds.Distinct())
            {
                if (await _repository.GetStoreInventoryAsync(storeId, ingredientId) == null)
                {
                    throw new InvalidOperationException("INGREDIENT_NOT_IN_STORE_INVENTORY");
                }
            }
        }

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

            if (!await _repository.IsActiveSupplierStoreAsync(dto.SupplierId.Value, dto.StoreId))
            {
                throw new InvalidOperationException("Nhà cung cấp chưa được kích hoạt cho cửa hàng này.");
            }
        }

        private static Task ValidateDetailsAsync(CreateInventoryDocumentDTO dto)
        {
            foreach (var item in dto.Details)
            {
                if (dto.Type == InventoryDocumentType.STOCK_TAKE)
                {
                    if (item.Quantity < 0 || item.BaseQuantity < 0)
                    {
                        throw new InvalidOperationException("Số lượng kiểm kê thực tế không được âm.");
                    }
                }
                else
                {
                    if (item.Quantity <= 0)
                    {
                        throw new InvalidOperationException("Số lượng phải lớn hơn 0.");
                    }

                    if (item.BaseQuantity <= 0)
                    {
                        throw new InvalidOperationException("Số lượng quy đổi base không hợp lệ.");
                    }
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
                var ingredient = await _repository.GetIngredientAsync(item.IngredientId);

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
                var unit = await _repository.GetUnitAsync(item.UnitId);

                if (unit == null)
                {
                    throw new InvalidOperationException("Đơn vị tính không hợp lệ.");
                }
            }
        }

        private static void ValidateImportPurpose(CreateInventoryDocumentDTO dto)
        {
            if (dto.Type != InventoryDocumentType.IMPORT)
            {
                return;
            }

            if (dto.Purpose == InventoryDocumentPurpose.IMPORT_ADJUSTMENT)
            {
                throw new InvalidOperationException(
                    "Không còn cho phép tạo phiếu điều chỉnh tăng thủ công. Vui lòng sử dụng Phiếu Kiểm Kê.");
            }

            if (dto.Purpose != InventoryDocumentPurpose.IMPORT_PURCHASE)
            {
                throw new InvalidOperationException("Mục đích phiếu nhập không hợp lệ.");
            }
        }

        private static void ValidateExportPurpose(CreateInventoryDocumentDTO dto)
        {
            if (dto.Type != InventoryDocumentType.EXPORT)
            {
                return;
            }

            if (dto.Purpose == InventoryDocumentPurpose.ADJUSTMENT_OUT)
            {
                throw new InvalidOperationException(
                    "Không còn cho phép tạo phiếu điều chỉnh giảm thủ công. Vui lòng sử dụng Phiếu Kiểm Kê.");
            }

            if (dto.Purpose != InventoryDocumentPurpose.SALE)
            {
                throw new InvalidOperationException("Mục đích phiếu xuất không hợp lệ.");
            }
        }

        private static void ValidateConfirmPurpose(InventoryDocument document)
        {
            if (document.Type == InventoryDocumentType.IMPORT
                && document.Purpose == InventoryDocumentPurpose.IMPORT_ADJUSTMENT)
            {
                throw new InvalidOperationException(
                    "Phiếu điều chỉnh tăng thủ công đã ngừng áp dụng và không thể xác nhận. Vui lòng sử dụng Phiếu Kiểm Kê.");
            }

            if (document.Type == InventoryDocumentType.EXPORT
                && document.Purpose != InventoryDocumentPurpose.SALE)
            {
                throw new InvalidOperationException(
                    "Mục đích phiếu xuất này đã ngừng áp dụng và không thể xác nhận.");
            }
        }

        private static void ValidateWastePurpose(CreateInventoryDocumentDTO dto)
        {
            if (dto.Type != InventoryDocumentType.WASTE)
            {
                return;
            }

            if (dto.Purpose != InventoryDocumentPurpose.DAMAGED
                && dto.Purpose != InventoryDocumentPurpose.EXPIRED
                && dto.Purpose != InventoryDocumentPurpose.BROKEN
                && dto.Purpose != InventoryDocumentPurpose.CONTAMINATED
                && dto.Purpose != InventoryDocumentPurpose.LOST)
            {
                throw new InvalidOperationException("Mục đích phiếu hủy kho không hợp lệ.");
            }
        }

        private static void ValidateStockTakePurpose(CreateInventoryDocumentDTO dto)
        {
            if (dto.Type != InventoryDocumentType.STOCK_TAKE)
            {
                return;
            }

            if (dto.Purpose != InventoryDocumentPurpose.STOCK_TAKE)
            {
                throw new InvalidOperationException("Mục đích phiếu kiểm kê không hợp lệ.");
            }
        }

        private static void ValidateNegativeStockOptIn(CreateInventoryDocumentDTO dto)
        {
            if (!dto.AllowNegativeStock)
            {
                return;
            }

            if (dto.Type != InventoryDocumentType.EXPORT
                || dto.Purpose != InventoryDocumentPurpose.SALE)
            {
                throw new InvalidOperationException(
                    "Chỉ Phiếu Xuất bán hàng mới được bật tùy chọn cho phép xuất âm kho.");
            }
        }

        private static void ValidateAdjustmentNote(CreateInventoryDocumentDTO dto)
        {
            var isAdjustment =
                dto.Type == InventoryDocumentType.IMPORT
                    && dto.Purpose == InventoryDocumentPurpose.IMPORT_ADJUSTMENT
                || dto.Type == InventoryDocumentType.EXPORT
                    && dto.Purpose == InventoryDocumentPurpose.ADJUSTMENT_OUT;

            if (isAdjustment && string.IsNullOrWhiteSpace(dto.Note))
            {
                throw new InvalidOperationException("Phiếu điều chỉnh phải có ghi chú lý do điều chỉnh.");
            }
        }

        private static void ValidateWasteNote(CreateInventoryDocumentDTO dto)
        {
            if (dto.Type == InventoryDocumentType.WASTE && string.IsNullOrWhiteSpace(dto.Note))
            {
                throw new InvalidOperationException("Phiếu hủy kho phải có ghi chú lý do hủy.");
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

        private static void ValidateExportSnapshot(InventoryDocumentSnapshotDTO? snapshot)
        {
            if (snapshot == null)
            {
                throw new InvalidOperationException("Phiếu chưa được snapshot.");
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
