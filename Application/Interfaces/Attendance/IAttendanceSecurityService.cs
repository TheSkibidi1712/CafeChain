using System.Threading.Tasks;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Attendance
{
    public interface IAttendanceSecurityService
    {
        /// <summary>
        /// Xác thực IP của nhân viên khi Check-in có hợp lệ với cấu hình mạng của Cửa hàng hay không.
        /// Áp dụng cho cả thiết bị di động (kết nối WiFi cửa hàng) và Máy tính bàn tại quầy.
        /// </summary>
        /// <param name="storeId">ID của cửa hàng nhân viên đang định chấm công</param>
        /// <param name="clientIp">IP lấy từ HttpRequest</param>
        /// <returns>ServiceResult thành công nếu hợp lệ</returns>
        Task<ServiceResult> ValidateStoreIPAsync(int storeId, string clientIp);

        /// <summary>
        /// Buộc đổi mật khẩu với nhân viên mới đăng nhập Kiosk lần đầu (RequiresPasswordChange == true)
        /// </summary>
        Task<ServiceResult> ProcessFirstLoginPasswordChangeAsync(int accountId, string oldPassword, string newPassword);

        /// <summary>
        /// Đăng ký Face ID cho nhân viên (lưu FaceDescriptor vector vào DB)
        /// </summary>
        Task<ServiceResult> RegisterFaceAsync(int accountId, string faceDescriptor);
    }
}
