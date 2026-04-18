using System.Collections.Generic;
using System.Threading.Tasks;
using CafeChain.ViewModels.Cart;
using CafeChain.Models.Vouchers;

namespace CafeChain.Application.Interfaces
{
    using CafeChain.ViewModels.Orders;
    using CafeChain.ViewModels.Shared;
    using CafeChain.ViewModels.Customers;
    public interface IOrderService
    {
        /// <summary>
        /// Tạo đơn hàng mới với logic Zero-Trust (truy vấn lại giá từ DB).
        /// </summary>
        /// <param name="model">Dữ liệu từ form Checkout</param>
        /// <param name="customerId">ID khách hàng (nếu đã đăng nhập)</param>
        /// <param name="sessionCart">Danh sách món từ Session</param>
        /// <returns>OrderId vừa tạo</returns>
        Task<int> PlaceOrderAsync(CheckoutViewModel model, int? customerId, List<CartItemViewModel> sessionCart);

        /// <summary>
        /// Lấy danh sách địa chỉ đã lưu của khách hàng (lọc active và format chuẩn).
        /// </summary>
        Task<List<CustomerAddressViewModel>> GetSavedAddressesAsync(int customerId);

        /// <summary>
        /// Lấy danh sách Voucher khả dụng hiện tại
        /// </summary>
        Task<List<Voucher>> GetAvailableVouchersAsync();

        /// <summary>
        /// Lấy danh sách hóa đơn theo trạng thái và mã khách hàng, phân trang.
        /// </summary>
        Task<PagedResult<OrderHistoryViewModel>> GetCustomerOrdersAsync(int customerId, int pageIndex = 1, int pageSize = 10, string statusGroup = null);

        /// <summary>
        /// Lấy chi tiết một hóa đơn của khách hàng (Check IDOR).
        /// </summary>
        Task<OrderDetailViewModel> GetCustomerOrderDetailAsync(int orderId, int customerId);

        /// <summary>
        /// Lấy danh sách SĐT đã lưu của khách hàng, sắp xếp theo mặc định.
        /// Dùng để thay thế việc inject DbContext vào CheckoutController (Skill.md §1).
        /// </summary>
        Task<List<CustomerPhoneViewModel>> GetCustomerPhonesAsync(int customerId);

        /// <summary>
        /// Lấy tên đầy đủ của khách hàng theo ID.
        /// Dùng để thay thế việc inject DbContext vào CheckoutController (Skill.md §1).
        /// </summary>
        Task<string> GetCustomerNameAsync(int customerId);

        /// <summary>
        /// Hủy đơn hàng bởi Khách hàng (áp dụng cho đơn COD mới tạo hoặc Đơn Online chưa thanh toán).
        /// Bọc trong DbContextTransaction để bảo toàn tồn kho.
        /// </summary>
        Task<bool> CancelOrderAsync(int orderId, int customerId);
    }
}
