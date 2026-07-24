using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Results;
using System;
using System.Threading.Tasks;

namespace CafeChain.Application.Interfaces.POS
{
    /// <summary>
    /// Service xử lý nghiệp vụ đặt hàng POS — tuân thủ nguyên tắc Thin Controller
    /// </summary>
    public interface IPOSOrderService
    {
        /// <summary>
        /// Lấy menu data cho POS (drinks + sizes + toppings) theo storeId
        /// </summary>
        Task<ServiceResult<object>> GetMenuDataAsync(int storeId);

        /// <summary>
        /// Tìm kiếm khách hàng theo SĐT
        /// </summary>
        Task<ServiceResult<object>> SearchCustomerAsync(string phone);

        /// <summary>
        /// Xử lý toàn bộ nghiệp vụ tạo đơn hàng POS:
        /// Validate → Calculate → Create Order → Create Payments → Handle Loyalty → Handle Voucher
        /// </summary>
        Task<ServiceResult<object>> CommitOrderAsync(POSOrderCommitDto dto, int userId, int storeId);

        /// <summary>
        /// Commit Offline Order được Sync từ IndexedDB vào đúng WorkShift gốc.
        /// Không yêu cầu WorkShift đang mở và không trigger automatic print.
        /// </summary>
        Task<ServiceResult<object>> CommitOfflineSyncedOrderAsync(
            POSOrderCommitDto dto,
            OfflineOrderSyncContext syncContext);

        /// <summary>
        /// Lấy dữ liệu tóm tắt ca cho modal đóng ca
        /// </summary>
        Task<ServiceResult<object>> GetCloseShiftDataAsync(int userId, int storeId);

        /// <summary>
        /// Đăng ký nhanh khách hàng hội viên từ POS
        /// </summary>
        Task<ServiceResult<object>> RegisterCustomerAsync(CafeChain.Application.DTOs.POS.QuickCustomerRegisterDto dto);

        /// <summary>
        /// Issue #68: Lấy lịch sử đơn hàng POS có phân trang
        /// </summary>
        Task<ServiceResult<object>> GetOrderHistoryAsync(int storeId, int page, int pageSize);

        /// <summary>
        /// Gửi lệnh in lại hóa đơn hoặc tem cho đơn POS đã thanh toán.
        /// </summary>
        Task<ServiceResult<object>> ReprintOrderAsync(int orderId, POSOrderReprintRequestDto dto, int storeId);
    }
}
