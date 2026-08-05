namespace CafeChain.Application.Constants;

/// <summary>
/// Nhãn tiếng Việt dành cho giao diện Quản lý đá theo ca.
/// Giá trị nội bộ, API và dữ liệu lưu trữ được giữ nguyên.
/// </summary>
public static class OperationalIceDisplayText
{
    public const string Unknown = "Không xác định";

    public static string Status(string? value) => value switch
    {
        OperationalIceStatuses.Draft => "Bản nháp",
        OperationalIceStatuses.Open => "Đang mở",
        OperationalIceStatuses.PendingApproval => "Chờ duyệt",
        OperationalIceStatuses.ReconciliationRequired => "Cần đối soát",
        OperationalIceStatuses.Closed => "Đã đóng",
        OperationalIceStatuses.Cancelled => "Đã hủy",
        "Scheduled" => "Đã lên lịch",
        "Ready" => "Sẵn sàng",
        "Operating" => "Đang vận hành",
        "Rejected" => "Đã từ chối",
        "Completed" => "Hoàn tất",
        _ => Unknown
    };

    public static string CreationSource(string? value) => value switch
    {
        OperationalIceCreationSources.Manual => "Tạo thủ công",
        OperationalIceCreationSources.StaffSchedule => "Tạo từ lịch làm việc",
        _ => Unknown
    };

    public static string WorkShiftStatus(string? value) => value switch
    {
        "Open" => "Đang mở",
        "Closed" => "Đã đóng",
        "Cancelled" => "Đã hủy",
        "AwaitingPayment" => "Đang chờ thanh toán",
        "Pending" => "Đang chờ xử lý",
        "Completed" => "Hoàn tất",
        _ => Unknown
    };

    public static string SupplementStatus(string? value) => value switch
    {
        IceSupplementalIssueStatuses.Pending => "Chờ duyệt",
        IceSupplementalIssueStatuses.Approved => "Đã duyệt",
        IceSupplementalIssueStatuses.Rejected => "Đã từ chối",
        IceSupplementalIssueStatuses.Cancelled => "Đã hủy",
        _ => Unknown
    };

    public static string CarryOverStatus(string? value) => value switch
    {
        IceCarryOverStatuses.Pending => "Chờ xác nhận",
        IceCarryOverStatuses.Confirmed => "Đã xác nhận",
        IceCarryOverStatuses.Cancelled => "Đã hủy",
        _ => Unknown
    };

    public static string PostingType(string? value) => value switch
    {
        IcePostingTypes.VarianceOut => "Xuất kho do chênh lệch đá",
        _ => Unknown
    };

    public static string CostStatus(string? value) => value switch
    {
        IceCostSnapshotStatuses.Available => "Đã có dữ liệu giá vốn",
        IceCostSnapshotStatuses.Missing => "Thiếu dữ liệu giá vốn",
        "Chưa chốt ca" => "Chưa chốt ca",
        "Thiếu giá vốn FIFO/ledger cho giao dịch bán" => "Thiếu dữ liệu giá vốn theo phiếu xuất kho cho giao dịch bán",
        "Thiếu dữ liệu giá vốn theo phiếu xuất kho cho giao dịch bán" => "Thiếu dữ liệu giá vốn theo phiếu xuất kho cho giao dịch bán",
        "Cần đối soát; hệ thống không ghi tăng tồn tự động" => "Cần đối soát; hệ thống không tự tăng tồn kho.",
        "Cần đối soát; hệ thống không tự tăng tồn kho." => "Cần đối soát; hệ thống không tự tăng tồn kho.",
        "Chênh lệch chưa có bút toán giá vốn hoàn chỉnh" => "Chênh lệch chưa có đủ dữ liệu giá vốn.",
        "Chênh lệch chưa có đủ dữ liệu giá vốn." => "Chênh lệch chưa có đủ dữ liệu giá vốn.",
        "Đầy đủ theo FIFO/ledger" => "Đủ dữ liệu giá vốn theo phiếu xuất kho",
        "Đủ dữ liệu giá vốn theo phiếu xuất kho" => "Đủ dữ liệu giá vốn theo phiếu xuất kho",
        _ => "Chưa có đủ dữ liệu giá vốn"
    };

    public static string ErrorMessage(string? errorCode, string? fallback) => errorCode switch
    {
        OperationalIceErrorCodes.Forbidden or OperationalIceErrorCodes.StoreScopeForbidden
            => "Bạn không có quyền thực hiện thao tác này tại chi nhánh đã chọn.",
        OperationalIceErrorCodes.NotFound => "Không tìm thấy dữ liệu ca đá.",
        OperationalIceErrorCodes.InvalidRequest => SafeFallback(fallback, "Vui lòng kiểm tra lại thông tin đã nhập."),
        OperationalIceErrorCodes.InvalidState => SafeFallback(fallback, "Trạng thái hiện tại không cho phép thao tác này."),
        OperationalIceErrorCodes.InsufficientUsableStock => "Tồn đá khả dụng không đủ để thực hiện thao tác này.",
        OperationalIceErrorCodes.WorkShiftAlreadyLinked
            => "Ca bán hàng POS này đã được liên kết với một ca đá khác.",
        OperationalIceErrorCodes.ConcurrencyConflict
            => "Dữ liệu vừa được người khác cập nhật. Vui lòng kiểm tra lại trước khi tiếp tục.",
        OperationalIceErrorCodes.ScheduleShiftAlreadyUsed
            => "Ca làm việc này đã được dùng để tạo một ca đá khác.",
        _ => SafeFallback(fallback, "Không thể xử lý yêu cầu lúc này. Vui lòng thử lại.")
    };

    private static string SafeFallback(string? fallback, string safeDefault)
    {
        if (string.IsNullOrWhiteSpace(fallback))
            return safeDefault;

        var technicalMarkers = new[]
        {
            "WorkShift", "OperationalShift", "Allocation", "ModelState", "Exception",
            "Invalid state", "Validation failed", "Forbidden", "Conflict", "SQL", "HTTP"
        };

        return technicalMarkers.Any(marker => fallback.Contains(marker, StringComparison.OrdinalIgnoreCase))
            ? safeDefault
            : fallback;
    }
}
