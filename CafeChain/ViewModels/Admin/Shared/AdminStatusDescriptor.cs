using System.Diagnostics;
using System.Globalization;

namespace CafeChain.ViewModels.Admin.Shared
{
    /// <summary>
    /// Presentation-only status chip. Each domain maps its own codes to descriptors;
    /// do not merge domain state machines into one enum.
    /// </summary>
    public sealed class AdminStatusDescriptor
    {
        public string Code { get; init; } = "";
        public string Label { get; init; } = "";
        public string CssClass { get; init; } = "";
        public string? AccessibleDescription { get; init; }

        public static AdminStatusDescriptor Of(string code, string label, string cssClass, string? accessible = null)
            => new()
            {
                Code = code,
                Label = label,
                CssClass = cssClass,
                AccessibleDescription = accessible ?? label
            };
    }

    /// <summary>Domain-specific display mappers (labels preserved from #126–#128 UX).</summary>
    public static class AdminStatusDisplay
    {
        private static readonly CultureInfo VietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");

        public static AdminStatusDescriptor RecipeActive(bool active) =>
            active
                ? AdminStatusDescriptor.Of("active", "Hoạt động", "rb-status-active")
                : AdminStatusDescriptor.Of("inactive", "Ngưng", "rb-status-inactive");

        public static AdminStatusDescriptor RecipeCost(bool? costComplete, string? costStatusText = null)
        {
            if (costComplete == true)
                return AdminStatusDescriptor.Of("cost_complete", costStatusText ?? "Đủ dữ liệu", "rb-status-complete");
            if (costComplete == false)
                return AdminStatusDescriptor.Of("cost_incomplete", costStatusText ?? "Thiếu dữ liệu", "rb-status-incomplete");
            return AdminStatusDescriptor.Of("cost_unknown", costStatusText ?? "—", "rb-status-inactive");
        }

        public static AdminStatusDescriptor PreparedItemConfig(string configStatusKey, string configStatusLabel)
        {
            var css = configStatusKey switch
            {
                "has_active" => "rb-status-complete",
                "inactive" => "rb-status-inactive",
                _ => "rb-status-incomplete"
            };
            return AdminStatusDescriptor.Of(configStatusKey, configStatusLabel, css);
        }

        public static AdminStatusDescriptor RestockRequest(string status) =>
            status?.ToUpperInvariant() switch
            {
                "DRAFT" => AdminStatusDescriptor.Of(status, "Nháp", "ops-badge-neutral"),
                "SUBMITTED" => AdminStatusDescriptor.Of(status, "Đã gửi", "ops-badge-warning"),
                "PROCESSING" => AdminStatusDescriptor.Of(status, "Đang xử lý", "ops-badge-info"),
                "PARTIALLY_RECEIVED" => AdminStatusDescriptor.Of(status, "Đã nhận một phần", "ops-badge-warning"),
                "COMPLETED" => AdminStatusDescriptor.Of(status, "Hoàn thành", "ops-badge-success"),
                "REJECTED" => AdminStatusDescriptor.Of(status, "Đã từ chối", "ops-badge-danger"),
                "CANCELLED" => AdminStatusDescriptor.Of(status, "Đã hủy", "ops-badge-neutral"),
                _ => Unknown("Yêu cầu nhập hàng", status, "ops-badge-neutral")
            };

        public static AdminStatusDescriptor RestockRequestPriority(string? priority) =>
            priority?.ToUpperInvariant() switch
            {
                "LOW" => AdminStatusDescriptor.Of(priority, "Thấp", "ops-badge-neutral"),
                "NORMAL" => AdminStatusDescriptor.Of(priority, "Bình thường", "ops-badge-info"),
                "HIGH" => AdminStatusDescriptor.Of(priority, "Cao", "ops-badge-warning"),
                "URGENT" => AdminStatusDescriptor.Of(priority, "Khẩn cấp", "ops-badge-danger"),
                _ => Unknown("Mức ưu tiên yêu cầu nhập hàng", priority, "ops-badge-neutral")
            };

        public static AdminStatusDescriptor BranchReceipt(string status) =>
            status?.ToUpperInvariant() switch
            {
                "DRAFT" => AdminStatusDescriptor.Of(status, "Nháp", "rb-status-incomplete"),
                "CONFIRMED" => AdminStatusDescriptor.Of(status, "Đã xác nhận", "rb-status-complete"),
                _ => Unknown("Phiếu nhận hàng", status, "rb-status-inactive")
            };

        public static AdminStatusDescriptor PurchaseAdvice(string? status) =>
            status?.ToUpperInvariant() switch
            {
                "DRAFT" => AdminStatusDescriptor.Of(status, "Nháp", "pa-badge-draft"),
                "SUBMITTED" => AdminStatusDescriptor.Of(status, "Đã gửi", "pa-badge-submitted"),
                "UNDER_REVIEW" => AdminStatusDescriptor.Of(status, "Đang xem xét", "pa-badge-review"),
                "PARTIALLY_ALLOCATED" => AdminStatusDescriptor.Of(status, "Đã đưa vào đơn một phần", "pa-badge-review"),
                "ALLOCATED" or "FULLY_ALLOCATED" => AdminStatusDescriptor.Of(status, "Đã đưa vào đơn", "pa-badge-complete"),
                "PARTIALLY_FULFILLED" => AdminStatusDescriptor.Of(status, "Đã nhận một phần", "pa-badge-partial"),
                "COMPLETED" => AdminStatusDescriptor.Of(status, "Hoàn thành", "pa-badge-complete"),
                "REJECTED" => AdminStatusDescriptor.Of(status, "Đã từ chối", "pa-badge-rejected"),
                "CANCELLED" => AdminStatusDescriptor.Of(status, "Đã hủy", "pa-badge-cancelled"),
                _ => Unknown("Đề nghị mua hàng", status, "pa-badge-unknown")
            };

        public static AdminStatusDescriptor PurchaseAdvicePriority(string? priority) =>
            priority?.ToUpperInvariant() switch
            {
                "LOW" => AdminStatusDescriptor.Of(priority, "Thấp", "pa-priority-low"),
                "NORMAL" => AdminStatusDescriptor.Of(priority, "Bình thường", "pa-priority-normal"),
                "HIGH" => AdminStatusDescriptor.Of(priority, "Cao", "pa-priority-high"),
                "URGENT" => AdminStatusDescriptor.Of(priority, "Khẩn cấp", "pa-priority-urgent"),
                _ => Unknown("Mức ưu tiên", priority, "pa-priority-unknown")
            };

        public static AdminStatusDescriptor PurchaseOrderBatch(string? status) =>
            status?.ToUpperInvariant() switch
            {
                "DRAFT" => AdminStatusDescriptor.Of(status, "Nháp", "pa-batch-status-draft"),
                "PENDING_APPROVAL" => AdminStatusDescriptor.Of(status, "Chờ duyệt", "pa-batch-status-pending_approval"),
                "APPROVED" => AdminStatusDescriptor.Of(status, "Đã duyệt", "pa-batch-status-approved"),
                "PDF_GENERATED" => AdminStatusDescriptor.Of(status, "Đã tạo PDF", "pa-batch-status-pdf_generated"),
                "SENT_TO_SUPPLIER" => AdminStatusDescriptor.Of(status, "Đã gửi Nhà cung cấp", "pa-batch-status-sent_to_supplier"),
                "SUPPLIER_CONFIRMED" => AdminStatusDescriptor.Of(status, "Nhà cung cấp đã xác nhận", "pa-batch-status-supplier_confirmed"),
                "PARTIALLY_RECEIVED" => AdminStatusDescriptor.Of(status, "Đã nhận một phần", "pa-batch-status-partially_received"),
                "COMPLETED" => AdminStatusDescriptor.Of(status, "Hoàn thành", "pa-batch-status-completed"),
                "CANCELLED" => AdminStatusDescriptor.Of(status, "Đã hủy", "pa-batch-status-cancelled"),
                _ => Unknown("Đơn đặt hàng gộp", status, "pa-batch-status-unknown")
            };

        public static AdminStatusDescriptor PurchaseOrder(string? status) =>
            status?.ToUpperInvariant() switch
            {
                "DRAFT" => AdminStatusDescriptor.Of(status, "Nháp", "ops-badge-neutral"),
                "APPROVED" => AdminStatusDescriptor.Of(status, "Đã duyệt", "ops-badge-success"),
                "SENT" or "MARKED_AS_SENT" => AdminStatusDescriptor.Of(status, "Đã gửi Nhà cung cấp", "ops-badge-info"),
                "PARTIALLY_RECEIVED" => AdminStatusDescriptor.Of(status, "Đã nhận một phần", "ops-badge-warning"),
                "COMPLETED" => AdminStatusDescriptor.Of(status, "Hoàn thành", "ops-badge-success"),
                "CANCELLED" => AdminStatusDescriptor.Of(status, "Đã hủy", "ops-badge-danger"),
                _ => Unknown("Đơn đặt hàng", status, "ops-badge-neutral")
            };

        public static AdminStatusDescriptor PurchaseOrderBatchDocument(string? status) =>
            status?.ToUpperInvariant() switch
            {
                "GENERATED" => AdminStatusDescriptor.Of(status, "Sẵn sàng gửi", "pa-batch-status-pdf_generated"),
                "SENT" => AdminStatusDescriptor.Of(status, "Đã gửi", "pa-batch-status-sent_to_supplier"),
                "SUPERSEDED" => AdminStatusDescriptor.Of(status, "Đã thay thế", "pa-batch-status-cancelled"),
                _ => Unknown("Phiên bản PDF", status, "pa-batch-status-unknown")
            };

        public static string PurchaseOrderBatchDocumentChannel(string? channel) =>
            channel?.ToUpperInvariant() switch
            {
                "ZALO_MANUAL" => "Zalo",
                "EMAIL_MANUAL" => "Email",
                _ => UnknownLabel("Kênh gửi", channel)
            };

        public static string RestockFulfillmentDocumentType(string? documentType) =>
            documentType?.ToUpperInvariant() switch
            {
                "BRANCH_RECEIPT" => "Phiếu nhận hàng",
                "INVENTORY_TRANSFER" => "Phiếu điều chuyển",
                _ => UnknownLabel("Loại chứng từ thực hiện yêu cầu nhập hàng", documentType)
            };

        public static string RestockFulfillmentSource(string? sourceType) =>
            sourceType?.ToUpperInvariant() switch
            {
                "SUPPLIER" => "Nhà cung cấp",
                "MANUAL" => "Ghi nhận thủ công",
                _ => UnknownLabel("Nguồn thực hiện yêu cầu nhập hàng", sourceType)
            };

        public static string RestockFulfillmentStatus(string? status) =>
            status?.ToUpperInvariant() switch
            {
                "PLANNED" => "Dự kiến",
                "LINKED" => "Đã liên kết",
                "RECEIVED" => "Đã nhận",
                "CANCELLED" => "Đã hủy",
                _ => UnknownLabel("Trạng thái thực hiện yêu cầu nhập hàng", status)
            };

        public static AdminStatusDescriptor StockAlert(string? status) =>
            status?.ToUpperInvariant() switch
            {
                "OPEN" => AdminStatusDescriptor.Of(status, "Chờ quản lý", "ops-badge-warning"),
                "CONFIRMED" => AdminStatusDescriptor.Of(status, "Đã xác nhận", "ops-badge-info"),
                "REJECTED" or "MANAGER_REJECTED" => AdminStatusDescriptor.Of(status, "Đã báo sai", "ops-badge-neutral"),
                "RESOLVED" => AdminStatusDescriptor.Of(status, "Đã khôi phục", "ops-badge-success"),
                "CLOSED" => AdminStatusDescriptor.Of(status, "Đã đóng", "ops-badge-neutral"),
                _ => Unknown("Cảnh báo kho", status, "ops-badge-neutral")
            };

        public static string StockAlertType(string? alertType) =>
            alertType?.ToUpperInvariant() switch
            {
                "OUT_OF_STOCK" => "Hết hàng",
                "LOW_STOCK" => "Sắp hết",
                "MANUAL_REVIEW" => "Báo thiếu thủ công — Cần xác minh",
                _ => UnknownLabel("Loại cảnh báo kho", alertType)
            };

        public static string StockAlertSource(string? source) =>
            source?.ToUpperInvariant() switch
            {
                "SALES_REPORT" => "Nhân viên báo thiếu",
                "POS_SALE" => "Bán hàng POS",
                "OFFLINE_SYNC" => "Đồng bộ ngoại tuyến",
                "MANUAL_CHECK" => "Kiểm tra thủ công",
                "INVENTORY_TRANSACTION" => "Biến động kho",
                "AUTO" => "Hệ thống tự động",
                _ => UnknownLabel("Nguồn cảnh báo kho", source)
            };

        public static string InventoryTransactionType(string? transactionType) =>
            transactionType?.ToUpperInvariant() switch
            {
                "IMPORT" => "Nhập kho",
                "EXPORT" => "Xuất kho",
                "WASTE" => "Hao hụt",
                "STOCK_TAKE" => "Kiểm kê",
                "PRODUCTION_IN" => "Nhập từ sản xuất",
                "PRODUCTION_OUT" => "Xuất cho sản xuất",
                "SALES_DEDUCTION" => "Trừ tồn bán hàng",
                "ADJUSTMENT_IN" => "Điều chỉnh tăng",
                "ADJUSTMENT_OUT" => "Điều chỉnh giảm",
                "OUT_TRANSFER" => "Xuất điều chuyển",
                "IN_TRANSFER" => "Nhập điều chuyển",
                "CONSOLIDATION_OUT" => "Chuyển khỏi dòng nguồn",
                "CONSOLIDATION_IN" => "Chuyển vào dòng chuẩn",
                "BRANCH_RECEIPT_IN" => "Nhập từ phiếu nhận hàng",
                "SALES_RETURN" => "Nhập hoàn hàng bán",
                _ => UnknownLabel("Loại biến động kho", transactionType)
            };

        public static AdminStatusDescriptor SupplierReceiptIssue(string? status) =>
            status?.ToUpperInvariant() switch
            {
                "OPEN" => AdminStatusDescriptor.Of(status, "Đang mở", "ops-badge-warning"),
                "UNDER_REVIEW" => AdminStatusDescriptor.Of(status, "Đang xem xét", "ops-badge-info"),
                "RESOLVED" => AdminStatusDescriptor.Of(status, "Đã xử lý", "ops-badge-success"),
                "DISMISSED" => AdminStatusDescriptor.Of(status, "Đã bỏ qua", "ops-badge-neutral"),
                "CLOSED" => AdminStatusDescriptor.Of(status, "Đã đóng", "ops-badge-neutral"),
                _ => Unknown("Sự cố Nhà cung cấp", status, "ops-badge-neutral")
            };

        public static string SupplierReceiptIssueType(string? issueType) =>
            issueType?.ToUpperInvariant() switch
            {
                "LATE_DELIVERY" => "Giao trễ",
                "SHORT_DELIVERY" => "Giao thiếu",
                "WRONG_ITEM" => "Sai mặt hàng",
                "DAMAGED" => "Hư hỏng",
                "EXPIRED" => "Hết hạn",
                "QUALITY_FAILURE" => "Không đạt chất lượng",
                "PACKAGING_FAILURE" => "Lỗi bao bì",
                "DOCUMENT_MISMATCH" => "Sai chứng từ",
                "OTHER" => "Khác",
                _ => UnknownLabel("Loại sự cố Nhà cung cấp", issueType)
            };

        public static string Quantity(decimal value) => value.ToString("0.###", VietnameseCulture);

        public static string Currency(decimal value) => $"{value.ToString("N0", VietnameseCulture)} ₫";

        public static string Date(DateTime value) => value.ToString("dd/MM/yyyy", VietnameseCulture);

        public static string DateTime(DateTime value) => value.ToString("dd/MM/yyyy HH:mm", VietnameseCulture);

        public static AdminStatusDescriptor UnitConversionRow(string statusKey, string statusLabel)
        {
            var css = statusKey switch
            {
                "ok" => "rb-status-complete",
                "package_conflict" => "rb-status-incomplete",
                "review" => "rb-status-incomplete",
                _ => "rb-status-inactive"
            };
            return AdminStatusDescriptor.Of(statusKey, statusLabel, css);
        }

        private static AdminStatusDescriptor Unknown(string domain, string? code, string cssClass)
            => AdminStatusDescriptor.Of(code ?? string.Empty, UnknownLabel(domain, code), cssClass);

        private static string UnknownLabel(string domain, string? code)
        {
            Trace.TraceWarning("Không có nhãn hiển thị cho {0}: {1}", domain, code ?? "<null>");
            return "Không xác định";
        }
    }
}
