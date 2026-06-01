using CafeChain.Data;
using CafeChain.Infrastrusture.Interfaces.Admin.POS;
using CafeChain.Models.Customers;
using CafeChain.Models.Drinks;
using CafeChain.Models.Orders;
using CafeChain.Models.Payments;
using CafeChain.Models.Vouchers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CafeChain.Infrastrusture.Repositories.Admin.POS
{
    public class POSOrderRepository : IPOSOrderRepository
    {
        private readonly AppDbContext _context;
        private IDbContextTransaction _transaction;

        public POSOrderRepository(AppDbContext context)
        {
            _context = context;
        }

        // === MENU ===
        public async Task<List<int>> GetStoreDrinkIdsAsync(int storeId)
        {
            return await _context.StoreDrinks
                .Where(sd => sd.StoreId == storeId && sd.Active)
                .Select(sd => sd.DrinkId)
                .ToListAsync();
        }

        public async Task<List<object>> GetCategoriesWithDrinksAsync(List<int> storeDrinkIds)
        {
            var categories = await _context.DrinkCategories
                .Where(c => c.Active)
                .Select(c => new
                {
                    c.CategoryId,
                    c.Name,
                    Drinks = c.Drinks
                        .Where(d => d.Active && storeDrinkIds.Contains(d.DrinkId))
                        .Select(d => new
                        {
                            d.DrinkId,
                            d.Name,
                            ImageUrl = d.DrinkImages
                                .Where(di => di.IsDefault)
                                .Select(di => di.ImageUrl)
                                .FirstOrDefault() ?? d.DrinkImages.Select(di => di.ImageUrl).FirstOrDefault(),
                            Sizes = d.DrinkSizes
                                .Where(ds => ds.Active)
                                .Select(ds => new { ds.SizeId, SizeName = ds.Size.Name, ds.Price }).ToList(),
                            Toppings = d.DrinkToppings
                                .Select(dt => new { dt.ToppingId, ToppingName = dt.Topping.Name, Price = dt.Topping.Price, ImageUrl = dt.Topping.ImageUrl }).ToList()
                        }).ToList()
                })
                .Where(c => c.Drinks.Any())
                .ToListAsync();

            return categories.Cast<object>().ToList();
        }

        public async Task<List<object>> GetStoreToppingsAsync(int storeId)
        {
            var toppings = await _context.StoreToppings
                .Where(st => st.StoreId == storeId && st.Active)
                .Select(st => new { st.ToppingId, ToppingName = st.Topping.Name, Price = st.Topping.Price, ImageUrl = st.Topping.ImageUrl })
                .ToListAsync();

            return toppings.Cast<object>().ToList();
        }

        // === CUSTOMER ===
        public async Task<object> SearchCustomerByPhoneAsync(string phone)
        {
            return await _context.CustomerPhones
                .Where(cp => cp.Phone.Contains(phone))
                .Select(cp => new
                {
                    cp.Customer.CustomerId,
                    cp.Customer.FullName,
                    Phone = cp.Phone,
                    cp.Customer.CurrentPoints,
                    cp.Customer.TotalOrders,
                    cp.Customer.TotalSpent,
                    MemberLevel = cp.Customer.MemberLevel != null ? cp.Customer.MemberLevel.Name : "Thành viên"
                })
                .FirstOrDefaultAsync();
        }

        // === DRINK & TOPPING LOOKUP ===
        public async Task<Drink> GetDrinkWithSizesAsync(int drinkId)
        {
            return await _context.Drinks
                .Include(d => d.DrinkSizes).ThenInclude(ds => ds.Size)
                .FirstOrDefaultAsync(d => d.DrinkId == drinkId);
        }

        public async Task<List<Topping>> GetToppingsByIdsAsync(List<int> toppingIds)
        {
            return await _context.Toppings
                .Where(t => toppingIds.Contains(t.ToppingId))
                .ToListAsync();
        }

        // === ORDER CRUD ===
        public async Task<Order> CreateOrderAsync(Order order)
        {
            _context.Orders.Add(order);
            await _context.SaveChangesAsync();
            return order;
        }

        public async Task CreatePaymentAsync(Payment payment)
        {
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();
        }

        public async Task CreateOrderVoucherAsync(OrderVoucher orderVoucher)
        {
            _context.OrderVouchers.Add(orderVoucher);
            await _context.SaveChangesAsync();
        }

        public async Task CreateVoucherUsageAsync(VoucherUsage usage)
        {
            _context.VoucherUsages.Add(usage);
            await _context.SaveChangesAsync();
        }

        // === CUSTOMER LOYALTY ===
        public async Task<Customer> GetCustomerByIdAsync(int customerId)
        {
            return await _context.Customers.FindAsync(customerId);
        }

        public async Task UpdateCustomerAsync(Customer customer)
        {
            _context.Customers.Update(customer);
            await _context.SaveChangesAsync();
        }

        public async Task CreatePointTransactionAsync(CafeChain.Models.Loyalties.PointTransaction pt)
        {
            _context.PointTransactions.Add(pt);
            await _context.SaveChangesAsync();
        }

        // === VOUCHER ===
        public async Task<Voucher> GetVoucherByCodeAsync(string code)
        {
            return await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == code);
        }

        // === CLOSE SHIFT DATA ===
        public async Task<decimal> GetTotalSalesByPaymentMethodAsync(int shiftId, int paymentMethodId)
        {
            return await _context.Orders
                .Where(o => o.WorkShiftId == shiftId)
                .Join(_context.Payments, o => o.OrderId, p => p.OrderId, (o, p) => p)
                .Where(p => p.PaymentMethodId == paymentMethodId)
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;
        }

        public async Task<int> GetCompletedOrderCountAsync(int shiftId)
        {
            return await _context.Orders
                .CountAsync(o => o.WorkShiftId == shiftId && o.OrderStatusId == 4);
        }

        // === TRANSACTION ===
        public async Task BeginTransactionAsync()
        {
            _transaction = await _context.Database.BeginTransactionAsync();
        }

        public async Task CommitTransactionAsync()
        {
            if (_transaction != null) await _transaction.CommitAsync();
        }

        public async Task RollbackTransactionAsync()
        {
            if (_transaction != null) await _transaction.RollbackAsync();
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
