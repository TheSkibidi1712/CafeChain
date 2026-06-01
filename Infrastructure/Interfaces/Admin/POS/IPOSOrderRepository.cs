using CafeChain.Models.Customers;
using CafeChain.Models.Drinks;
using CafeChain.Models.Orders;
using CafeChain.Models.Payments;
using CafeChain.Models.Vouchers;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CafeChain.Infrastrusture.Interfaces.Admin.POS
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
        Task<Drink> GetDrinkWithSizesAsync(int drinkId);
        Task<List<Topping>> GetToppingsByIdsAsync(List<int> toppingIds);

        // === ORDER CRUD ===
        Task<Order> CreateOrderAsync(Order order);
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

        // === TRANSACTION ===
        Task BeginTransactionAsync();
        Task CommitTransactionAsync();
        Task RollbackTransactionAsync();
        Task SaveChangesAsync();
    }
}
