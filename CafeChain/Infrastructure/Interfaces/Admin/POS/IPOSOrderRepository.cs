using CafeChain.Application.DTOs.POS;
using CafeChain.Models.Customers;
using CafeChain.Models.Drinks;
using CafeChain.Models.Orders;
using CafeChain.Models.Payments;
using CafeChain.Models.Stores;
using CafeChain.Models.Vouchers;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CafeChain.Infrastructure.Interfaces.Admin.POS
{
    /// <summary>
    /// Repository xử lý data access cho POS Order module
    /// Tuân thủ: Repository pattern — tách query khỏi Service
    /// </summary>
    public interface IPOSOrderRepository
    {
        // === MENU ===
        Task<List<int>> GetStoreDrinkIdsAsync(int storeId);
        Task<List<object>> GetCategoriesWithDrinksAsync(List<int> storeDrinkIds);
        Task<List<object>> GetStoreToppingsAsync(int storeId);

        // === CUSTOMER ===
        Task<object> SearchCustomerByPhoneAsync(string phone);

        // === DRINK & TOPPING LOOKUP ===
        Task<Drink> GetDrinkWithSizesAsync(int drinkId, int storeId);
        Task<List<Topping>> GetValidToppingsForOrderItemAsync(int storeId, int drinkId, List<int> toppingIds);

        // === ORDER CRUD ===
        Task<Order> CreateOrderAsync(Order order);

        /// <summary>
        /// ADR-0002: Tìm Order theo ClientOrderId để kiểm tra idempotency khi sync offline.
        /// Trả về null nếu chưa tồn tại — an toàn để commit đơn mới.
        /// </summary>
        Task<Order?> FindOrderByClientOrderIdAsync(Guid clientOrderId);

        /// <summary>
        /// Load order đã commit để in lại, giới hạn theo store hiện tại của POS.
        /// </summary>
        Task<Order?> GetOrderForReprintAsync(int orderId, int storeId);

        /// <summary>
        /// Issue #68: Lấy danh sách đơn hàng POS có phân trang.
        /// Sử dụng .Select() projection để tránh N+1.
        /// </summary>
        Task<(List<POSOrderHistoryDto> Items, int TotalCount)> GetOrderHistoryAsync(int storeId, int page, int pageSize);

        Task CreatePaymentAsync(Payment payment);
        Task CreateOrderVoucherAsync(OrderVoucher orderVoucher);
        Task CreateVoucherUsageAsync(VoucherUsage usage);

        // === CUSTOMER LOYALTY ===
        Task<Customer> GetCustomerByIdAsync(int customerId);
        Task UpdateCustomerAsync(Customer customer);
        Task CreatePointTransactionAsync(CafeChain.Models.Loyalties.PointTransaction pointTransaction);

        // === VOUCHER ===
        Task<Voucher> GetVoucherByCodeAsync(string code);

        // === CLOSE SHIFT DATA ===
        Task<decimal> GetTotalSalesByPaymentMethodAsync(int shiftId, int paymentMethodId);
        Task<int> GetCompletedOrderCountAsync(int shiftId);

        // === AUDIT LOG ===
        Task<InvoiceAuditLog?> GetPendingAuditLogAsync(int cashierId, string actionName, int windowMinutes);
        Task UpdateAuditLogOrderIdAsync(int auditLogId, int orderId);
        /// <summary>Post-success audit only — not an authorization token.</summary>
        Task CreateAuditLogAsync(InvoiceAuditLog log);

        // === CUSTOMER REGISTRATION ===
        Task<bool> HasDuplicatePhoneAsync(string phone);
        Task<Customer> RegisterCustomerAsync(Customer customer, CustomerPhone phone);

        // === POS TERMINAL ===
        Task<PosTerminal?> GetTerminalByIdAsync(string terminalId);
        Task CreateTerminalAsync(PosTerminal terminal);
        Task UpdateTerminalAsync(PosTerminal terminal);

        // === TRANSACTION ===
        Task BeginTransactionAsync();
        Task CommitTransactionAsync();
        Task RollbackTransactionAsync();
        Task SaveChangesAsync();
    }
}
