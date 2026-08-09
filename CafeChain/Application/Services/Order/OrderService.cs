using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CafeChain.Application.Interfaces;
using CafeChain.Application.Constants;
using CafeChain.Data;
using CafeChain.Models.Orders;
using CafeChain.Models.Payments;
using CafeChain.Models.Vouchers;
using CafeChain.ViewModels.Cart;
using CafeChain.ViewModels.Orders;
using CafeChain.ViewModels.Customers;
using CafeChain.ViewModels.Shared;
using CafeChain.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace CafeChain.Application.Services.Cart
{
    public class OrderService : IOrderService
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly IInventoryService _inventoryService;
        private readonly IHubContext<OrderHub> _hubContext;
        private readonly IServiceScopeFactory _scopeFactory;

        public OrderService(
            AppDbContext context, 
            IMemoryCache cache, 
            IInventoryService inventoryService,
            IHubContext<OrderHub> hubContext,
            IServiceScopeFactory scopeFactory)
        {
            _context = context;
            _cache = cache;
            _inventoryService = inventoryService;
            _hubContext = hubContext;
            _scopeFactory = scopeFactory;
        }

        public async Task<int> PlaceOrderAsync(CheckoutViewModel model, int? customerId, List<CartItemViewModel> sessionCart)
        {
            if (sessionCart == null || !sessionCart.Any())
                throw new Exception("Giỏ hàng trống.");

            // 1. Idempotency Check (Khóa xử lý kép)
            if (_cache.TryGetValue(model.CheckoutToken, out _))
            {
                throw new Exception("Giao dịch đang được xử lý hoặc đã hoàn tất, vui lòng không nhấn thanh toán lại.");
            }
            // Khóa token 2 phút để tránh submit nhiều lần
            _cache.Set(model.CheckoutToken, true, TimeSpan.FromMinutes(2));

            // 2. DbContext Transaction
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (!customerId.HasValue) throw new Exception("Không thể định danh khách hàng.");

                var isDeliveryOrder = model.OrderTypeId == SystemConstants.OrderTypes.Delivery;
                if (!isDeliveryOrder && model.OrderTypeId != SystemConstants.OrderTypes.TakeAway)
                {
                    throw new Exception("Loại đơn Website không hợp lệ. Chỉ hỗ trợ nhận tại cửa hàng hoặc giao hàng.");
                }

                // ============================================================
                // Anti-Spam Rate Limit — Chỉ chặn đơn ONLINE đang CÒN HIỆU LỰC
                // COD (Pending=2) không bị tính vì sẽ thu tiền khi giao hàng
                // ============================================================
                var expiredCutoff = DateTime.Now.AddMinutes(-5);

                // [FIX CORE] Tự động hủy đơn ONLINE đã hết hạn trước khi kiểm tra
                // Lý do: không thể phụ thuộc hoàn toàn vào Worker (có thể lag 1 phút)
                // Chỉ hủy đơn Pending (=1, online), KHÔNG đụng đơn COD (=2)
                var expiredOnlineOrders = await _context.Orders
                    .Include(o => o.Payments)
                    .Where(o => o.CustomerId == customerId.Value
                             && o.OrderStatusId == SystemConstants.OrderStatuses.Pending
                             && o.CreatedAt < expiredCutoff)
                    .ToListAsync();

                foreach (var expiredOrder in expiredOnlineOrders)
                {
                    expiredOrder.OrderStatusId = SystemConstants.OrderStatuses.Cancelled;
                    expiredOrder.Note = (expiredOrder.Note ?? "") + " [HỆ THỐNG: Hủy do quá thời gian giữ chỗ]";
                    foreach (var p in expiredOrder.Payments.Where(p => p.PaymentStatusId == SystemConstants.PaymentStatuses.Unpaid))
                        p.PaymentStatusId = SystemConstants.PaymentStatuses.Failed;
                }
                if (expiredOnlineOrders.Any())
                    await _context.SaveChangesAsync(); // Dọn đơn rác trước khi tạo đơn mới

                // Bây giờ đếm đơn ONLINE đang CÒN HIỆU LỰC (mới tạo trong 5 phút gần đây)
                var pendingOrdersCount = await _context.Orders
                    .CountAsync(o => o.CustomerId == customerId.Value
                                  && o.OrderStatusId == SystemConstants.OrderStatuses.Pending
                                  && o.CreatedAt >= expiredCutoff); // Chỉ đơn chưa hết hạn
                                  
                if (pendingOrdersCount >= 2) // Quá 2 đơn online treo đang hiệu lực
                {
                    throw new Exception("Bạn đang có đơn hàng chờ thanh toán. Vui lòng thanh toán hoặc hủy đơn cũ trước khi đặt mới để tránh lãng phí nguyên liệu!");
                }


                var customer = await _context.Customers.FirstOrDefaultAsync(c => c.CustomerId == customerId.Value);
                var phone = await _context.CustomerPhones.FirstOrDefaultAsync(p => p.CustomerPhoneId == model.SelectedPhoneId && p.CustomerId == customerId.Value);
                var address = await _context.CustomerAddresses
                    .Include(a => a.Ward)
                    .Include(a => a.Province)
                    .FirstOrDefaultAsync(a => a.CustomerAddressId == model.SelectedAddressId && a.CustomerId == customerId.Value && !a.IsDeleted);

                if (customer == null || phone == null || address == null)
                {
                    throw new Exception("Thông tin liên hệ (Địa chỉ hoặc Số điện thoại) không tồn tại. Vui lòng chọn lại.");
                }

                int defaultStoreId = 1;

                // 3. Gọi abstraction Của Inventory Service (Reserve Inventory)
                // Hệ thống sẽ ném Exception kèm tên nguyên liệu nếu không đủ hàng
                await _inventoryService.ReserveInventoryForOrderAsync(defaultStoreId, sessionCart);

                // 4. Khởi tạo State ban đầu cho mọi loại đơn hàng
                int orderStatusId = SystemConstants.OrderStatuses.Pending; // Status 1: Chờ xác nhận
                if (model.PaymentMethodId == 2 || model.PaymentMethodId == 3) // Chuyển khoản hoặc MoMo
                {
                    orderStatusId = SystemConstants.OrderStatuses.AwaitingPayment; // Chờ thanh toán
                }
                
                int paymentStatusId = SystemConstants.PaymentStatuses.Unpaid; // Status 1: Chưa thanh toán

                var order = new Order
                {
                    CustomerId = customerId,
                    StoreId = defaultStoreId,
                    OrderStatusId = orderStatusId,
                    PaymentStatusId = paymentStatusId,
                    OrderTypeId = model.OrderTypeId,
                    Source = "Website",
                    Note = model.Note ?? "",
                    CreatedAt = DateTime.Now,
                    
                    ReceiverName = customer.FullName,
                    ReceiverPhone = phone.Phone,
                    DeliveryAddress = isDeliveryOrder ? address.DisplayAddress : null,
                    ShippingFee = isDeliveryOrder ? 15000 : 0,

                    TableId = null,
                    StaffId = null,
                    
                    OrderDetails = new List<OrderDetail>()
                };

                decimal subTotal = 0;

                // 5. Zero-Trust Price Calculation & Snapshotting
                foreach (var item in sessionCart)
                {
                    // Zero-Trust Security: Quantity Check
                    if (item.Quantity <= 0)
                    {
                        throw new Exception("Phát hiện số lượng sản phẩm không hợp lệ (Zero/Negative).");
                    }

                    var drinkSize = await _context.DrinkSizes
                        .Include(ds => ds.Drink)
                        .Include(ds => ds.Size)
                        .FirstOrDefaultAsync(ds => ds.DrinkId == item.DrinkId && ds.SizeId == item.SizeId);

                    if (drinkSize == null || !drinkSize.Active || !drinkSize.Drink.Active)
                        throw new Exception($"Sản phẩm '{item.Name}' hiện không còn khả dụng.");

                    var detail = new OrderDetail
                    {
                        DrinkId = item.DrinkId,
                        SizeId = item.SizeId,
                        DrinkName = drinkSize.Drink.Name, // Snapshot
                        SizeName = drinkSize.Size.Name, // Snapshot
                        Price = drinkSize.Price, // Snapshot
                        Quantity = item.Quantity,
                        Note = item.Note ?? "",
                        OrderToppings = new List<OrderTopping>()
                    };

                    decimal itemToppingTotal = 0;

                    if (item.ToppingIds != null && item.ToppingIds.Any())
                    {
                        // [FIX 3] TOPPING SANITIZER: Loại bỏ ID trùng lặp và giới hạn tối đa 5 topping/ly
                        var sanitizedToppingIds = item.ToppingIds.Distinct().Take(5).ToList();

                        var toppings = await _context.Toppings
                            .Where(t => sanitizedToppingIds.Contains(t.ToppingId))
                            .ToListAsync();

                        foreach (var tId in sanitizedToppingIds)
                        {
                            var topping = toppings.FirstOrDefault(t => t.ToppingId == tId);
                            if (topping == null || !topping.Active)
                                throw new Exception("Một số Topping đã ngừng bán.");

                            detail.OrderToppings.Add(new OrderTopping
                            {
                                ToppingId = topping.ToppingId,
                                ToppingName = topping.Name,     // Snapshot
                                Price = topping.Price             // Snapshot
                            });
                            itemToppingTotal += topping.Price;
                        }
                    }

                    detail.Price += itemToppingTotal; 
                    subTotal += detail.Price * detail.Quantity;
                    order.OrderDetails.Add(detail);
                }

                order.SubTotal = subTotal;

                // Soft-removal: voucher + loyalty out of product scope — reject non-empty payload.
                if (model.SelectedVoucherId.HasValue || (model.PointsUsed.HasValue && model.PointsUsed.Value > 0))
                {
                    throw new Exception(ProductScopeErrorCodes.VoucherOrLoyaltyNotAvailableMessage);
                }

                order.VoucherDiscount = 0;
                order.PointDiscount = 0;
                order.PointsUsed = 0;

                // Total = line goods + shipping only (no voucher/points).
                var totalBeforePoint = subTotal + order.ShippingFee;
                if (totalBeforePoint < 0) totalBeforePoint = 0;

                order.Total = Math.Round(totalBeforePoint, 0, MidpointRounding.AwayFromZero);
                if (order.Total < 0) order.Total = 0;


                // 7. Lưu Order & Payment
                _context.Orders.Add(order);
                
                var payment = new Payment
                {
                    Order = order,
                    Amount = order.Total,
                    PaymentMethodId = model.PaymentMethodId,
                    PaymentStatusId = paymentStatusId,
                    TransactionCode = null
                };
                _context.Payments.Add(payment);

                try 
                {
                    await _context.SaveChangesAsync();
                }
                catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
                {
                    var realError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                    throw new Exception($"LỖI DATABASE: {realError}");
                }

                await transaction.CommitAsync();

                // [FIX BUG] Bắn SignalR báo có đơn mới cho Admin (Chỉ áp dụng cho đơn COD = 1)
                // Đơn Online sẽ được bắn ở PaymentController/Webhook sau khi thanh toán xong.
                if (model.PaymentMethodId == 1) // 1 là CASH (COD)
                {
                    await _hubContext.Clients.Group("AdminDashboard").SendAsync("ReceiveNewOrder", order.OrderId);
                }

                return order.OrderId;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                
                // Giải phóng Idempotency key nếu có lỗi để user được checkout lại
                _cache.Remove(model.CheckoutToken);
                throw;
            }
        }

        public async Task<List<CustomerAddressViewModel>> GetSavedAddressesAsync(int customerId)
        {
            var addresses = await _context.CustomerAddresses
                .Include(a => a.Ward)
                .Include(a => a.Province)
                .Where(a => a.CustomerId == customerId && !a.IsDeleted)
                .OrderByDescending(a => a.IsDefault) 
                .ToListAsync();

            return addresses.Select(a => new CustomerAddressViewModel
            {
                CustomerAddressId = a.CustomerAddressId,
                Address = a.Address,
                DisplayAddress = a.DisplayAddress,
                ProvinceId = a.ProvinceId,
                WardId = a.WardId,
                IsDefault = a.IsDefault
            }).ToList();
        }

        public Task<List<Voucher>> GetAvailableVouchersAsync()
            => Task.FromResult(new List<Voucher>());

        public Task<List<Voucher>> GetCustomerValidVouchersAsync(int customerId)
            => Task.FromResult(new List<Voucher>());

        public async Task<PagedResult<OrderHistoryViewModel>> GetCustomerOrdersAsync(int customerId, int pageIndex = 1, int pageSize = 10, string statusGroup = null)
        {
            var query = _context.Orders
                .Include(o => o.Store)
                .Include(o => o.OrderStatus)
                .Where(o => o.CustomerId == customerId);

            // Tab Group Mapping — đồng bộ với SystemConstants.OrderStatuses mới [1-6]
            if (!string.IsNullOrEmpty(statusGroup))
            {
                int[] statusIds = statusGroup.ToLower() switch
                {
                    "pending"    => new[] { SystemConstants.OrderStatuses.Pending },
                    "processing" => new[] { 
                        SystemConstants.OrderStatuses.Preparing, 
                        SystemConstants.OrderStatuses.Ready, 
                        SystemConstants.OrderStatuses.Delivering 
                    },
                    "completed"  => new[] { SystemConstants.OrderStatuses.Completed },
                    "cancelled"  => new[] { SystemConstants.OrderStatuses.Cancelled },
                    _ => Array.Empty<int>()
                };

                if (statusIds.Length > 0)
                {
                    query = query.Where(o => statusIds.Contains(o.OrderStatusId));
                }
            }

            int totalItems = await query.CountAsync();

            var items = await query
                .OrderByDescending(o => o.CreatedAt)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new OrderHistoryViewModel
                {
                    OrderId = o.OrderId,
                    CreatedAt = o.CreatedAt,
                    StoreName = o.Store != null ? o.Store.Name : null,
                    OrderStatusId = o.OrderStatusId,
                    StatusName = o.OrderStatus.Name,
                    TotalAmount = o.Total,
                    FirstItemName = o.OrderDetails.OrderBy(od => od.OrderDetailId).Select(od => od.DrinkName ?? od.Drink.Name).FirstOrDefault(),
                    FirstItemImageUrl = o.OrderDetails.OrderBy(od => od.OrderDetailId).Select(od => od.Drink.DrinkImages.FirstOrDefault(i => i.IsDefault).ImageUrl).FirstOrDefault() ?? "/images/placeholder.png",
                    AdditionalItemsCount = Math.Max(0, o.OrderDetails.Count() - 1)
                })
                .ToListAsync();

            return new PagedResult<OrderHistoryViewModel>
            {
                Items = items,
                TotalItems = totalItems,
                PageIndex = pageIndex,
                PageSize = pageSize
            };
        }

        public async Task<OrderDetailViewModel> GetCustomerOrderDetailAsync(int orderId, int customerId)
        {
            var order = await _context.Orders
                .Include(o => o.Store)
                .Include(o => o.OrderStatus)
                .Include(o => o.Payments).ThenInclude(p => p.PaymentMethod)
                .Include(o => o.OrderDetails).ThenInclude(od => od.Drink).ThenInclude(d => d.DrinkImages)
                .Include(o => o.OrderDetails).ThenInclude(od => od.OrderToppings)
                .FirstOrDefaultAsync(o => o.OrderId == orderId && o.CustomerId == customerId);

            if (order == null) return null;

            var vm = new OrderDetailViewModel
            {
                OrderId = order.OrderId,
                CreatedAt = order.CreatedAt,
                StoreName = order?.Store?.Name,
                PaymentMethodName = order.Payments.FirstOrDefault()?.PaymentMethod?.Name ?? "Chưa rõ",
                StatusName = order.OrderStatus.Name,
                Source = order.Source,

                ReceiverName = order.ReceiverName,
                ReceiverPhone = order.ReceiverPhone,
                DeliveryAddress = order.DeliveryAddress,
                Note = order.Note,

                SubTotal = order.SubTotal,
                ShippingFee = order.ShippingFee,
                VoucherDiscount = order.VoucherDiscount,
                PointDiscount = order.PointDiscount,
                FinalTotal = order.Total,
                
                Items = order.OrderDetails.Select(od => new OrderItemViewModel
                {
                    Name = od.DrinkName ?? od.Drink?.Name,
                    SizeName = od.SizeName,
                    ToppingNames = od.OrderToppings.Select(ot => $"{ot.ToppingName} (+{ot.Price:N0}đ)").ToList(),
                    Quantity = od.Quantity,
                    Price = od.Price,
                    Note = od.Note,
                    ImageUrl = od.Drink?.DrinkImages?.FirstOrDefault(i => i.IsDefault)?.ImageUrl ?? "/images/placeholder.png"
                }).ToList()
            };

            return vm;
        }

        /// <summary>
        /// Lấy danh sách SĐT đã lưu, sắp xếp SĐT mặc định lên đầu.
        /// Projection sang DTO — không trả Entity ra ngoài (Skill.md §1).
        /// </summary>
        public async Task<List<CustomerPhoneViewModel>> GetCustomerPhonesAsync(int customerId)
        {
            return await _context.CustomerPhones
                .Where(p => p.CustomerId == customerId)
                .OrderByDescending(p => p.IsDefault)
                .Select(p => new CustomerPhoneViewModel
                {
                    CustomerPhoneId = p.CustomerPhoneId,
                    Phone = p.Phone,
                    IsDefault = p.IsDefault
                })
                .ToListAsync();
        }

        /// <summary>
        /// Lấy tên đầy đủ của khách hàng. Trả null nếu không tìm thấy.
        /// </summary>
        public async Task<string> GetCustomerNameAsync(int customerId)
        {
            return await _context.Customers
                .Where(c => c.CustomerId == customerId)
                .Select(c => c.FullName)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// [FIX BUG] Hủy đơn hàng bởi Khách hàng an toàn với Transaction
        /// Di dời từ OrderController xuống Service.
        /// </summary>
        public async Task<bool> CancelOrderAsync(int orderId, int customerId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Cập nhật OrderStatus
                int rows = await _context.Database.ExecuteSqlInterpolatedAsync(
                    $@"UPDATE Orders SET OrderStatusId = {SystemConstants.OrderStatuses.Cancelled} 
                       WHERE OrderId = {orderId} AND CustomerId = {customerId} 
                       AND OrderStatusId IN ({SystemConstants.OrderStatuses.Pending}, {SystemConstants.OrderStatuses.AwaitingPayment})");

                if (rows == 0) return false;

                // 2. Cập nhật PaymentStatus
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $@"UPDATE Payments SET PaymentStatusId = {SystemConstants.PaymentStatuses.Failed} 
                       WHERE OrderId = {orderId} 
                       AND PaymentStatusId = {SystemConstants.PaymentStatuses.Unpaid}");

                // 3. Hoàn tồn kho an toàn trong Transaction
                await _inventoryService.ReleaseInventoryForOrderAsync(orderId);

                await transaction.CommitAsync();

                // 4. Bắn SignalR cập nhật Kanban
                await _hubContext.Clients.Group("AdminDashboard").SendAsync("ReceiveOrderStatusUpdate", orderId, SystemConstants.OrderStatuses.Cancelled);

                return true;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw; // Đẩy lỗi ra Controller để bắt
            }
        }

        public async Task SimulateDeliveryAsync(int orderId)
        {
            // Chạy ngầm hoàn toàn để Bartender không phải chờ
            _ = Task.Run(async () =>
            {
                try
                {
                    // Chờ 10 giây: "Tài xế đang đến..."
                    await Task.Delay(10000);

                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        var hub = scope.ServiceProvider.GetRequiredService<IHubContext<OrderHub>>();
                        
                        var order = await context.Orders
                            .Include(o => o.Payments)
                            .FirstOrDefaultAsync(o => o.OrderId == orderId);

                        if (order != null && order.OrderStatusId == SystemConstants.OrderStatuses.Ready)
                        {
                            // Bước 1: Chuyển sang Đang giao
                            order.OrderStatusId = SystemConstants.OrderStatuses.Delivering;
                            string[] drivers = { "Gia Phuc Speed", "Anh Ship Than Toc", "Robot Shipper v1", "Sieu Nhan Giao Hang" };
                            string randomDriver = drivers[new Random().Next(drivers.Length)];
                            order.Note = (order.Note ?? "") + $" | [AUTO-DRIVER]: {randomDriver} đã lấy hàng.";
                            
                            await context.SaveChangesAsync();
                            await hub.Clients.Group("AdminDashboard").SendAsync("ReceiveOrderStatusUpdate", orderId, order.OrderStatusId);
                        }
                    }

                    // Chờ thêm 30 giây: "Tài xế đang đi giao..."
                    await Task.Delay(30000);

                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        var hub = scope.ServiceProvider.GetRequiredService<IHubContext<OrderHub>>();
                        
                        var order = await context.Orders
                            .Include(o => o.Payments)
                            .FirstOrDefaultAsync(o => o.OrderId == orderId);

                        if (order != null && order.OrderStatusId == SystemConstants.OrderStatuses.Delivering)
                        {
                            // Bước 2: Hoàn thành đơn
                            order.OrderStatusId = SystemConstants.OrderStatuses.Completed;
                            order.Note = (order.Note ?? "") + " | [AUTO-DRIVER]: Giao hàng thành công.";
                            
                            // Cập nhật thanh toán nếu là đơn COD (Unpaid)
                            var payment = order.Payments.FirstOrDefault(p => p.PaymentStatusId == SystemConstants.PaymentStatuses.Unpaid);
                            if (payment != null)
                            {
                                payment.PaymentStatusId = SystemConstants.PaymentStatuses.Paid;
                                payment.PaidAt = DateTime.Now;
                                order.PaymentStatusId = SystemConstants.PaymentStatuses.Paid;
                            }

                            // Soft-removal: no loyalty earn. Customer purchase stats only.
                            if (order.CustomerId.HasValue && order.Total > 0)
                            {
                                var customer = await context.Customers
                                    .FirstOrDefaultAsync(c => c.CustomerId == order.CustomerId.Value);

                                if (customer != null)
                                {
                                    customer.TotalSpent += order.Total;
                                    customer.TotalOrders += 1;
                                    customer.LastOrderDate = DateTime.UtcNow;
                                    customer.UpdatedAt = DateTime.UtcNow;
                                }
                            }

                            await context.SaveChangesAsync();

                            // [MISSION 2] Trừ kho khi mô phỏng giao hàng thành công
                            var inventoryService = scope.ServiceProvider.GetRequiredService<IInventoryService>();
                            await inventoryService.ConfirmInventoryDeductionAsync(orderId);

                            await hub.Clients.Group("AdminDashboard").SendAsync("ReceiveOrderStatusUpdate", orderId, order.OrderStatusId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Ghi log lỗi nếu có
                    Console.WriteLine($"[SIMULATION ERROR] Order #{orderId}: {ex.Message}");
                }
            });
        }
    }
}
