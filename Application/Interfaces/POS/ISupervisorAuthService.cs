using CafeChain.Application.Results;
using System.Threading.Tasks;

namespace CafeChain.Application.Interfaces.POS
{
    public interface ISupervisorAuthService
    {
        /// <summary>
        /// Xác thực mã PIN Trưởng ca để bypass hành động nhạy cảm POS.
        /// Tích hợp brute-force protection (khóa 15 phút sau 5 lần sai).
        /// </summary>
        /// <param name="pin">Mã PIN 4 chữ số</param>
        /// <param name="cashierId">StaffId của thu ngân đang thao tác</param>
        /// <param name="storeId">StoreId hiện tại</param>
        /// <param name="actionName">Tên hành động: VOID_INVOICE, MANUAL_DISCOUNT, PRICE_OVERRIDE</param>
        /// <param name="targetId">ID đối tượng bị tác động (OrderId, DrinkId...)</param>
        /// <param name="reason">Lý do giải trình</param>
        Task<ServiceResult> AuthorizePinAsync(string pin, int cashierId, int storeId, string actionName, int targetId, string reason);

        /// <summary>
        /// Lấy số lần thử còn lại trước khi bị khóa
        /// </summary>
        Task<int> GetRemainingAttemptsAsync(int storeId);
    }
}
