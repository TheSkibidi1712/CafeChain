using CafeChain.Models.Enums.Inventory;

namespace CafeChain.Helpers
{
    public static class InventoryVietnameseMapper
    {
        public static string ToVietnamese(this InventoryDocumentType value)
        {
            return value switch
            {
                InventoryDocumentType.IMPORT => "Nhập kho",
                InventoryDocumentType.EXPORT => "Xuất kho",
                InventoryDocumentType.WASTE => "Hủy kho",
                InventoryDocumentType.STOCK_TAKE => "Kiểm kê",
                InventoryDocumentType.PRODUCTION_IN => "Nhập sản xuất",
                InventoryDocumentType.PRODUCTION_OUT => "Xuất sản xuất",
                InventoryDocumentType.SALES_DEDUCTION => "Trừ tồn bán hàng",
                InventoryDocumentType.ADJUSTMENT_IN => "Điều chỉnh tăng",
                InventoryDocumentType.INTERNAL_IMPORT => "Nhập nội bộ",
                _ => "Không xác định"
            };
        }

        public static string ToVietnamese(this InventoryDocumentPurpose value)
        {
            return value switch
            {
                InventoryDocumentPurpose.NONE => "Không xác định",
                InventoryDocumentPurpose.IMPORT_PURCHASE => "Nhập từ nhà cung cấp",
                InventoryDocumentPurpose.IMPORT_INTERNAL => "Nhập nội bộ",
                InventoryDocumentPurpose.IMPORT_ADJUSTMENT => "Điều chỉnh tăng",
                InventoryDocumentPurpose.SALE => "Xuất bán hàng",
                InventoryDocumentPurpose.INTERNAL_OUT => "Xuất nội bộ",
                InventoryDocumentPurpose.GIFT => "Quà tặng",
                InventoryDocumentPurpose.DEBT => "Ghi nợ",
                InventoryDocumentPurpose.SAMPLE => "Hàng mẫu",
                InventoryDocumentPurpose.ADJUSTMENT_OUT => "Điều chỉnh giảm",
                InventoryDocumentPurpose.STOCK_TAKE => "Kiểm kê",
                InventoryDocumentPurpose.DAMAGED => "Hàng hỏng",
                InventoryDocumentPurpose.EXPIRED => "Hết hạn",
                InventoryDocumentPurpose.BROKEN => "Bị vỡ",
                InventoryDocumentPurpose.CONTAMINATED => "Nhiễm bẩn",
                InventoryDocumentPurpose.LOST => "Thất thoát",
                _ => "Không xác định"
            };
        }

        public static string ToVietnamese(this InventoryDocumentStatus value)
        {
            return value switch
            {
                InventoryDocumentStatus.DRAFT => "Nháp",
                InventoryDocumentStatus.PENDING => "Chờ xử lý",
                InventoryDocumentStatus.CONFIRMED => "Đã xác nhận",
                InventoryDocumentStatus.CANCELLED => "Đã hủy",
                _ => "Không xác định"
            };
        }

        public static string ToVietnamese(this InventoryTransactionTypeEnum value)
        {
            return value switch
            {
                InventoryTransactionTypeEnum.IMPORT => "Nhập kho",
                InventoryTransactionTypeEnum.EXPORT => "Xuất kho",
                InventoryTransactionTypeEnum.WASTE => "Hủy kho",
                InventoryTransactionTypeEnum.STOCK_TAKE => "Kiểm kê",
                InventoryTransactionTypeEnum.PRODUCTION_IN => "Nhập sản xuất",
                InventoryTransactionTypeEnum.PRODUCTION_OUT => "Xuất sản xuất",
                InventoryTransactionTypeEnum.SALES_DEDUCTION => "Trừ tồn bán hàng",
                InventoryTransactionTypeEnum.SALES_RETURN => "Hoàn tồn bán hàng (refund)",
                InventoryTransactionTypeEnum.ADJUSTMENT_IN => "Điều chỉnh tăng",
                InventoryTransactionTypeEnum.ADJUSTMENT_OUT => "Điều chỉnh giảm",
                InventoryTransactionTypeEnum.OUT_TRANSFER => "Xuất chuyển kho",
                InventoryTransactionTypeEnum.IN_TRANSFER => "Nhập chuyển kho",
                InventoryTransactionTypeEnum.CONSOLIDATION_OUT => "Xuất hợp nhất tồn BTP",
                InventoryTransactionTypeEnum.CONSOLIDATION_IN => "Nhập hợp nhất tồn BTP",
                InventoryTransactionTypeEnum.BRANCH_RECEIPT_IN => "Nhập kho chi nhánh (phiếu nhận)",
                InventoryTransactionTypeEnum.TRANSFER_RETURN_IN => "Nhập hoàn chuyển kho",
                _ => "Không xác định"
            };
        }

        public static string ToVietnamese(this InventoryStockStatus value)
        {
            return value switch
            {
                InventoryStockStatus.NORMAL => "Bình thường",
                InventoryStockStatus.LOW_STOCK => "Sắp hết",
                InventoryStockStatus.NEGATIVE_PENDING => "Âm kho chờ duyệt",
                InventoryStockStatus.NEGATIVE_CONFIRMED => "Âm kho đã xác nhận",
                InventoryStockStatus.ADJUSTED => "Đã điều chỉnh",
                _ => "Không xác định"
            };
        }

        public static string ToVietnamese(this InventoryTransferStatus value)
        {
            return value switch
            {
                InventoryTransferStatus.DRAFT => "Nháp",
                InventoryTransferStatus.COMPLETED => "Hoàn tất",
                InventoryTransferStatus.CANCELLED => "Đã hủy",
                _ => "Không xác định"
            };
        }

        public static string ToVietnamese(this InventoryTransferPurpose value)
        {
            return value switch
            {
                InventoryTransferPurpose.REPLENISHMENT => "Bổ sung hàng",
                InventoryTransferPurpose.BALANCING => "Cân đối tồn kho",
                InventoryTransferPurpose.OTHER => "Khác",
                _ => "Không xác định"
            };
        }

        public static string ToVietnamese(this InventoryTransferType value)
        {
            return value switch
            {
                InventoryTransferType.STORE_TO_STORE => "Chuyển kho liên chi nhánh",
                _ => "Không xác định"
            };
        }

        public static string ToVietnameseTransactionType(string? typeName)
        {
            if (Enum.TryParse<InventoryTransactionTypeEnum>(typeName, ignoreCase: true, out var type))
            {
                return type.ToVietnamese();
            }

            return string.IsNullOrWhiteSpace(typeName)
                ? "Không xác định"
                : typeName;
        }

        public static string ToVietnameseStockStatus(string? statusName)
        {
            if (Enum.TryParse<InventoryStockStatus>(statusName, ignoreCase: true, out var status))
            {
                return status.ToVietnamese();
            }

            return string.IsNullOrWhiteSpace(statusName)
                ? "Không xác định"
                : statusName;
        }
    }
}
