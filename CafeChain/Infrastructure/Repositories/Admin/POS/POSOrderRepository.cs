using CafeChain.Data;
using CafeChain.Application.Constants;
using CafeChain.Infrastructure.Interfaces.Admin.POS;
using CafeChain.Models.Customers;
using CafeChain.Models.Drinks;
using CafeChain.Models.Orders;
using CafeChain.Models.Payments;
using CafeChain.Models.Stores;
using CafeChain.Models.Vouchers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using CafeChain.Application.Policies.Orders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CafeChain.Infrastructure.Repositories.Admin.POS
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
        public async Task<Drink> GetDrinkWithSizesAsync(int drinkId, int storeId)
        {
            return await _context.Drinks
                .Include(d => d.DrinkSizes).ThenInclude(ds => ds.Size)
                .FirstOrDefaultAsync(d => d.DrinkId == drinkId
                    && d.Active
                    && d.StoreDrinks.Any(sd => sd.StoreId == storeId && sd.Active));
        }

        public async Task<List<Topping>> GetValidToppingsForOrderItemAsync(int storeId, int drinkId, List<int> toppingIds)
        {
            return await _context.Toppings
                .Where(t => toppingIds.Contains(t.ToppingId)
                    && t.Active
                    && t.StoreToppings.Any(st => st.StoreId == storeId && st.Active)
                    && t.DrinkToppings.Any(dt => dt.DrinkId == drinkId))
                .ToListAsync();
        }

        // === ORDER CRUD ===
        public async Task<Order> CreateOrderAsync(Order order)
        {
            _context.Orders.Add(order);
            await _context.SaveChangesAsync();
            return order;
        }

        /// <summary>
        /// ADR-0002: Tìm Order đã tồn tại theo ClientOrderId (Idempotency check).
        /// Sử dụng Unique Filtered Index IX_Orders_ClientOrderId_Unique — O(1) lookup.
        /// </summary>
        public async Task<Order?> FindOrderByClientOrderIdAsync(Guid clientOrderId)
        {
            return await _context.Orders
                .Include(o => o.Payments)
                .Include(o => o.OrderDetails)
                    .ThenInclude(detail => detail.OrderToppings)
                .FirstOrDefaultAsync(o => o.ClientOrderId == clientOrderId);
        }

        public async Task<Order?> GetOrderForReprintAsync(int orderId, int storeId)
        {
            return await _context.Orders
                .Include(o => o.Store)
                .Include(o => o.Staff)
                .Include(o => o.Payments)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.OrderToppings)
                .FirstOrDefaultAsync(o => o.OrderId == orderId && o.StoreId == storeId);
        }

        /// <summary>
        /// Issue #68: Phân trang lịch sử đơn hàng POS.
        /// Dùng .Select() projection — EF Core dịch thành 1 SQL duy nhất, không N+1.
        /// </summary>
        public async Task<(List<Application.DTOs.POS.POSOrderHistoryDto> Items, int TotalCount)> GetOrderHistoryAsync(
            int storeId, int page, int pageSize)
        {
            var query = _context.Orders
                .Where(o => o.StoreId == storeId
                    && o.Source == "POS"
                    && o.OrderStatusId == SystemConstants.OrderStatuses.Completed
                    && (o.PaymentStatusId == SystemConstants.PaymentStatuses.Paid
                        || o.PaymentStatusId == SystemConstants.PaymentStatuses.Refunded)
                    && o.Payments.Any(p => p.PaymentStatusId == SystemConstants.PaymentStatuses.Paid
                        || p.PaymentStatusId == SystemConstants.PaymentStatuses.Refunded))
                .OrderByDescending(o => o.CreatedAt);

            var totalCount = await query.CountAsync();

            var pageOrders = await query
                .AsNoTracking()
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new Application.DTOs.POS.POSOrderHistoryDto
                {
                    OrderId = o.OrderId,
                    ClientOrderId = o.ClientOrderId.HasValue ? o.ClientOrderId.Value.ToString() : null,
                    StoreId = o.StoreId,
                    StoreName = o.Store != null ? o.Store.Name : $"Cửa hàng #{o.StoreId}",
                    WorkShiftId = o.WorkShiftId,
                    Source = o.Source,
                    OrderType = o.OrderType != null ? o.OrderType.Name : "Chưa xác định",
                    CreatedAt = o.CreatedAt,
                    Total = o.Total,
                    PaymentMethod = "Chưa xác định",
                    OrderStatusId = o.OrderStatusId,
                    OrderStatusName = o.OrderStatus != null ? o.OrderStatus.Name : "Chưa xác định",
                    PaymentStatusId = o.PaymentStatusId,
                    PaymentStatusName = o.PaymentStatus != null ? o.PaymentStatus.Name : "Chưa xác định",
                    StaffName = o.Staff != null ? o.Staff.FullName : "POS",
                    Note = o.Note
                })
                .ToListAsync();

            var orderIds = pageOrders.Select(o => o.OrderId).ToList();
            if (orderIds.Count == 0)
                return (pageOrders, totalCount);

            var paymentRows = await _context.Payments
                .AsNoTracking()
                .Where(p => orderIds.Contains(p.OrderId))
                .OrderBy(p => p.PaymentId)
                .Select(p => new
                {
                    p.OrderId,
                    DisplayKey = p.PaymentMethod != null
                        ? (p.PaymentMethod.Code ?? p.PaymentMethod.Name)
                        : null,
                    Payment = new Application.DTOs.POS.POSPaymentHistoryDto
                    {
                        PaymentMethodId = p.PaymentMethodId,
                        PaymentMethod = p.PaymentMethod != null ? p.PaymentMethod.Name : "Chưa xác định",
                        PaymentStatusId = p.PaymentStatusId,
                        PaymentStatus = p.PaymentStatus != null ? p.PaymentStatus.Name : "Chưa xác định",
                        Amount = p.Amount,
                        PaidAt = p.PaidAt,
                        TransactionCode = p.TransactionCode
                    }
                })
                .ToListAsync();

            var detailRows = await _context.OrderDetails
                .AsNoTracking()
                .Where(od => orderIds.Contains(od.OrderId))
                .OrderBy(od => od.OrderDetailId)
                .Select(od => new
                {
                    od.OrderId,
                    Detail = new Application.DTOs.POS.POSOrderDetailHistoryDto
                    {
                        DrinkName = od.DrinkName,
                        SizeName = od.SizeName,
                        Quantity = od.Quantity,
                        Price = od.Price,
                        LineTotal = od.Price * od.Quantity,
                        Note = od.Note,
                        Toppings = od.OrderToppings
                            .Select(ot => ot.ToppingName)
                            .ToList()
                    }
                })
                .ToListAsync();

            var paymentsByOrder = paymentRows
                .GroupBy(row => row.OrderId)
                .ToDictionary(group => group.Key, group => group.Select(row => row.Payment).ToList());
            var paymentDisplayByOrder = paymentRows
                .GroupBy(row => row.OrderId)
                .ToDictionary(
                    group => group.Key,
                    group => OrderChannelPolicy.GetPaymentDisplay(group.Select(row => row.DisplayKey)));

            var detailsByOrder = detailRows
                .GroupBy(row => row.OrderId)
                .ToDictionary(group => group.Key, group => group.Select(row => row.Detail).ToList());

            foreach (var order in pageOrders)
            {
                if (paymentsByOrder.TryGetValue(order.OrderId, out var payments))
                {
                    order.Payments = payments;
                    order.PaidAt = payments.Where(x => x.PaidAt.HasValue).Max(x => x.PaidAt);
                    order.PaymentMethod = paymentDisplayByOrder[order.OrderId];
                }

                if (detailsByOrder.TryGetValue(order.OrderId, out var details))
                {
                    order.OrderDetails = details;
                }
            }

            return (pageOrders, totalCount);
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
                .Where(p => p.PaymentStatusId == SystemConstants.PaymentStatuses.Paid)
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;
        }

        public async Task<int> GetCompletedOrderCountAsync(int shiftId)
        {
            return await _context.Orders
                .CountAsync(o => o.WorkShiftId == shiftId && o.OrderStatusId == SystemConstants.OrderStatuses.Completed);
        }

        // === AUDIT LOG ===
        public async Task<InvoiceAuditLog?> GetPendingAuditLogAsync(int cashierId, string actionName, int windowMinutes)
        {
            var cutoff = DateTime.Now.AddMinutes(-windowMinutes);
            return await _context.InvoiceAuditLogs
                .Where(al => al.CashierId == cashierId && al.ActionName == actionName && al.OrderId == null && al.CreatedAt >= cutoff)
                .OrderByDescending(al => al.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task UpdateAuditLogOrderIdAsync(int auditLogId, int orderId)
        {
            var log = await _context.InvoiceAuditLogs.FindAsync(auditLogId);
            if (log != null)
            {
                log.OrderId = orderId;
                await _context.SaveChangesAsync();
            }
        }

        public async Task CreateAuditLogAsync(InvoiceAuditLog log)
        {
            _context.InvoiceAuditLogs.Add(log);
            await _context.SaveChangesAsync();
        }

        // === CUSTOMER REGISTRATION ===
        public async Task<bool> HasDuplicatePhoneAsync(string phone)
        {
            return await _context.CustomerPhones.AnyAsync(cp => cp.Phone == phone);
        }

        public async Task<Customer> RegisterCustomerAsync(Customer customer, CustomerPhone phone)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();

                phone.CustomerId = customer.CustomerId;
                _context.CustomerPhones.Add(phone);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
                return customer;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // === POS TERMINAL ===
        public async Task<PosTerminal?> GetTerminalByIdAsync(string terminalId)
        {
            return await _context.PosTerminals.FindAsync(terminalId);
        }

        public async Task CreateTerminalAsync(PosTerminal terminal)
        {
            _context.PosTerminals.Add(terminal);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateTerminalAsync(PosTerminal terminal)
        {
            _context.PosTerminals.Update(terminal);
            await _context.SaveChangesAsync();
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
