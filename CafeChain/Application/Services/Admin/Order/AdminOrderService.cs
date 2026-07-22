using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin;
using CafeChain.Application.Interfaces;
using CafeChain.Application.Interfaces.Admin;
using CafeChain.Data;
using CafeChain.Hubs;
using CafeChain.Models.Loyalties;
using CafeChain.Models.Orders;
using CafeChain.Application.Policies.Orders;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CafeChain.Application.Services.Admin
{
    public class AdminOrderService : IAdminOrderService
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<OrderHub> _hubContext;
        private readonly IInventoryService _inventoryService;
        private readonly IOrderService _orderService;

        public AdminOrderService(
            AppDbContext context,
            IHubContext<OrderHub> hubContext,
            IInventoryService inventoryService,
            IOrderService orderService)
        {
            _context = context;
            _hubContext = hubContext;
            _inventoryService = inventoryService;
            _orderService = orderService;
        }

        public async Task<List<AdminOrderKanbanDto>> GetKanbanOrdersAsync(int storeId)
        {
            var activeStatuses = new[] {
                SystemConstants.OrderStatuses.Pending,
                SystemConstants.OrderStatuses.Preparing,
                SystemConstants.OrderStatuses.Ready,
                SystemConstants.OrderStatuses.Delivering
            };

            var activeOrders = await _context.Orders
                .Include(o => o.OrderType)
                .Include(o => o.Customer)
                .Include(o => o.OrderDetails).ThenInclude(od => od.OrderToppings)
                .Where(o => o.StoreId == storeId
                    && o.Source == OrderSources.Website
                    && activeStatuses.Contains(o.OrderStatusId))
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            var historyStatuses = new[] {
                SystemConstants.OrderStatuses.Completed
            };
            
            var today = DateTime.Today;

            var historyOrders = await _context.Orders
                .Include(o => o.OrderType)
                .Include(o => o.Customer)
                .Include(o => o.OrderDetails).ThenInclude(od => od.OrderToppings)
                .Where(o => o.StoreId == storeId
                    && o.Source == OrderSources.Website
                    && historyStatuses.Contains(o.OrderStatusId)
                    && o.CreatedAt >= today)
                .OrderByDescending(o => o.CreatedAt)
                .Take(20)
                .ToListAsync();

            var allOrders = activeOrders.Concat(historyOrders).ToList();

            return allOrders.Select(o => new AdminOrderKanbanDto
            {
                OrderId = o.OrderId,
                CreatedAt = o.CreatedAt,
                OrderStatusId = o.OrderStatusId,
                Total = o.Total,
                OrderTypeId = o.OrderTypeId,
                OrderTypeName = o.OrderType != null ? o.OrderType.Name : "N/A",
                Note = o.Note,
                CustomerName = o.Customer != null ? o.Customer.FullName : (o.ReceiverName ?? "Guest"),
                TotalItemCount = o.OrderDetails?.Count ?? 0,
                ItemSummaries = BuildItemSummaries(o.OrderDetails, maxItems: 4)
            }).ToList();
        }

        /// <summary>
        /// [Phase 1] Tạo danh sách tóm tắt món cho Kanban Card.
        /// Format: "2x Cà phê sữa (S) + Trân châu đen, Phô mai viên"
        /// Giới hạn maxItems dòng để card không quá dài.
        /// </summary>
        private List<string> BuildItemSummaries(ICollection<OrderDetail> details, int maxItems = 4)
        {
            if (details == null || !details.Any()) return new List<string>();

            return details.Take(maxItems).Select(od =>
            {
                var summary = $"{od.Quantity}x {od.DrinkName ?? "N/A"}";
                if (!string.IsNullOrEmpty(od.SizeName))
                    summary += $" ({od.SizeName})";

                if (od.OrderToppings != null && od.OrderToppings.Any())
                {
                    var toppingNames = od.OrderToppings.Select(ot => ot.ToppingName).ToList();
                    summary += " + " + string.Join(", ", toppingNames);
                }
                return summary;
            }).ToList();
        }

        public async Task<AdminOrderDetailDto> GetOrderDetailsAsync(int orderId, int storeId)
        {
            var order = await _context.Orders
                .Include(o => o.OrderType)
                .Include(o => o.OrderDetails).ThenInclude(od => od.Drink)
                .Include(o => o.OrderDetails).ThenInclude(od => od.Size)
                .Include(o => o.OrderDetails).ThenInclude(od => od.OrderToppings).ThenInclude(ot => ot.Topping)
                .FirstOrDefaultAsync(o => o.OrderId == orderId
                    && o.StoreId == storeId
                    && o.Source == OrderSources.Website);

            if (order == null) return null;

            return new AdminOrderDetailDto
            {
                OrderId = order.OrderId,
                ReceiverName = order.ReceiverName,
                ReceiverPhone = order.ReceiverPhone,
                DeliveryAddress = order.DeliveryAddress,
                Note = order.Note,
                Total = order.Total,
                OrderStatusId = order.OrderStatusId,
                OrderTypeId = order.OrderTypeId,
                OrderTypeName = order.OrderType?.Name,
                Items = order.OrderDetails.Select(od => new AdminOrderItemDto
                {
                    DrinkName = od.Drink?.Name,
                    SizeName = od.Size?.Name,
                    Quantity = od.Quantity,
                    Price = od.Price,
                    Note = od.Note,
                    Toppings = od.OrderToppings.Select(ot => ot.Topping.Name).ToList()
                }).ToList()
            };
        }

        public async Task AcceptOrderAsync(int orderId, int storeId)
        {
            var order = await GetBoardOrderAsync(orderId, storeId);
            if (order == null) throw new Exception("Đơn hàng không tồn tại.");

            if (order.OrderStatusId != SystemConstants.OrderStatuses.Pending)
            {
                throw new Exception($"Không thể duyệt đơn #{orderId}. Trạng thái hiện tại không hợp lệ.");
            }

            order.OrderStatusId = SystemConstants.OrderStatuses.Preparing;
            await _context.SaveChangesAsync();

            await _hubContext.Clients.Group("AdminDashboard")
                .SendAsync("ReceiveOrderStatusUpdate", orderId, order.OrderStatusId);
        }

        public async Task ReadyForPickupAsync(int orderId, int storeId)
        {
            var order = await GetBoardOrderAsync(orderId, storeId);
            if (order == null) throw new Exception("Đơn hàng không tồn tại.");

            if (order.OrderStatusId != SystemConstants.OrderStatuses.Preparing)
            {
                throw new Exception($"Không thể đánh dấu xong món cho đơn #{orderId}.");
            }

            order.OrderStatusId = SystemConstants.OrderStatuses.Ready;
            await _context.SaveChangesAsync();

            await _hubContext.Clients.Group("AdminDashboard")
                .SendAsync("ReceiveOrderStatusUpdate", orderId, order.OrderStatusId);

            // [Simulation] Tự động bắt đầu luồng giả lập tài xế nếu là đơn Giao Hàng (Type=3)
            if (order.OrderTypeId == SystemConstants.OrderTypes.Delivery)
            {
                await _orderService.SimulateDeliveryAsync(orderId);
            }
        }

        public async Task<List<DTOs.Admin.ShipperDto>> GetShippersAsync(int storeId)
        {
            var shippers = await _context.Set<CafeChain.Models.Staffs.Staff>()
                .Where(s => s.Active && s.StoreId == storeId)
                .Select(s => new DTOs.Admin.ShipperDto
                {
                    Id = s.StaffId,
                    Name = s.FullName
                })
                .ToListAsync();

            return shippers;
        }

        public async Task DispatchOrderAsync(CafeChain.Application.DTOs.Admin.DispatchOrderRequest request, int storeId)
        {
            var order = await GetBoardOrderAsync(request.OrderId, storeId);
            if (order == null) throw new Exception("Đơn hàng không tồn tại.");

            if (order.OrderStatusId != SystemConstants.OrderStatuses.Ready)
            {
                throw new Exception($"Không thể giao shipper cho đơn #{request.OrderId}.");
            }

            // ===== ZERO-TRUST VALIDATION DỰA TRÊN SHIPPER TYPE =====
            if (request.ShipperType == "INTERNAL")
            {
                if (!request.InternalShipperId.HasValue || request.InternalShipperId.Value <= 0)
                {
                    throw new Exception("Vui lòng chọn nhân viên giao hàng nội bộ.");
                }

                // Shipper nội bộ: Kiểm tra ID thực sự tồn tại và đang Active
                var staffExists = await _context.Set<CafeChain.Models.Staffs.Staff>()
                    .AnyAsync(s => s.StaffId == request.InternalShipperId.Value
                        && s.StoreId == storeId
                        && s.Active);

                if (!staffExists)
                    throw new Exception($"Nhân viên giao hàng ID #{request.InternalShipperId.Value} không tồn tại hoặc đã ngưng hoạt động.");

                order.StaffId = request.InternalShipperId.Value;
                order.Note = (order.Note ?? "") + " | [SHIPPER_NỘI_BỘ]";
            }
            else if (request.ShipperType == "EXTERNAL")
            {
                if (string.IsNullOrWhiteSpace(request.DeliveryPartner))
                {
                    throw new Exception("Vui lòng chọn đối tác giao hàng (Grab, ShopeeFood, etc).");
                }

                string partnerInfo = request.DeliveryPartner;
                if (!string.IsNullOrWhiteSpace(request.PartnerOrderCode))
                {
                    partnerInfo += $" - Mã đơn: {request.PartnerOrderCode.Trim()}";
                }

                order.Note = (order.Note ?? "") + $" | [SHIPPER_NGOÀI]: {partnerInfo}";
                // Đối tác ngoài thì StaffId có thể null
                order.StaffId = null;
            }
            else
            {
                throw new Exception("Loại giao hàng không hợp lệ.");
            }

            order.OrderStatusId = SystemConstants.OrderStatuses.Delivering;
            await _context.SaveChangesAsync();

            await _hubContext.Clients.Group("AdminDashboard")
                .SendAsync("ReceiveOrderStatusUpdate", request.OrderId, order.OrderStatusId);
        }



        public async Task CompleteOrderAsync(int orderId, int storeId)
        {
            var order = await _context.Orders
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.OrderId == orderId
                    && o.StoreId == storeId
                    && o.Source == OrderSources.Website);
            if (order == null) throw new Exception("Đơn hàng không tồn tại.");

            if (order.OrderStatusId != SystemConstants.OrderStatuses.Ready &&
                order.OrderStatusId != SystemConstants.OrderStatuses.Delivering)
            {
                throw new Exception($"Không thể hoàn thành đơn #{orderId}.");
            }

            order.OrderStatusId = SystemConstants.OrderStatuses.Completed;

            // COD: Khi giao thành công → đánh dấu đã thu tiền
            var payment = order.Payments.FirstOrDefault();
            if (payment != null && payment.PaymentStatusId == SystemConstants.PaymentStatuses.Unpaid)
            {
                payment.PaymentStatusId = SystemConstants.PaymentStatuses.Paid;
            }

            await _context.SaveChangesAsync();

            // [MISSION 2] Trừ tồn kho thực tế khi đơn hàng hoàn thành
            await _inventoryService.ConfirmInventoryDeductionAsync(orderId);

            // Cộng điểm thưởng cho khách hàng
            // Rule: 10,000 VND = 1 point

            if (order.CustomerId.HasValue && order.Total > 0)
            {
                int earnedPoints = (int)Math.Floor(order.Total / 10000);

                if (earnedPoints > 0)
                {
                    var customer = await _context.Customers
                        .FirstOrDefaultAsync(x => x.CustomerId == order.CustomerId.Value);

                    if (customer != null)
                    {
                        // =========================
                        // UPDATE SNAPSHOT
                        // =========================

                        customer.CurrentPoints += earnedPoints;

                        customer.TotalSpent += order.Total;

                        customer.TotalOrders += 1;

                        customer.LastOrderDate = DateTime.UtcNow;

                        customer.UpdatedAt = DateTime.UtcNow;

                        // =========================
                        // CREATE TRANSACTION LEDGER
                        // =========================

                        var pointTransaction = new PointTransaction
                        {
                            CustomerId = customer.CustomerId,

                            OrderId = order.OrderId,

                            // Earn => positive points
                            Points = earnedPoints,

                            // Nên seed sẵn type EARN = 1
                            PointTransactionTypeId = 1,

                            BalanceAfter = customer.CurrentPoints,

                            CreatedAt = DateTime.UtcNow,

                            // Ví dụ point expire sau 1 năm
                            ExpiredAt = DateTime.UtcNow.AddYears(1)
                        };

                        _context.PointTransactions.Add(pointTransaction);

                        // =========================
                        // AUTO MEMBER LEVEL UPDATE
                        // =========================

                        var newLevel = await _context.MemberLevels
                            .Where(x => x.MinPoints <= customer.CurrentPoints
                                && (x.MaxPoints == null || customer.CurrentPoints <= x.MaxPoints))
                            .OrderByDescending(x => x.MinPoints)
                            .FirstOrDefaultAsync();

                        if (newLevel != null)
                        {
                            customer.MemberLevelId = newLevel.MemberId;
                        }

                        await _context.SaveChangesAsync();
                    }
                }
            }


            await _hubContext.Clients.Group("AdminDashboard")
                .SendAsync("ReceiveOrderStatusUpdate", orderId, order.OrderStatusId);
        }

        public async Task CancelOrderAsync(int orderId, int storeId, string reason)
        {
            var order = await _context.Orders
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.OrderId == orderId
                    && o.StoreId == storeId
                    && o.Source == OrderSources.Website);
            if (order == null) throw new Exception("Đơn hàng không tồn tại.");

            var cancellableStatuses = new[]
            {
                SystemConstants.OrderStatuses.Pending,
                SystemConstants.OrderStatuses.Preparing,
                SystemConstants.OrderStatuses.Ready,
                SystemConstants.OrderStatuses.Delivering
            };

            if (!cancellableStatuses.Contains(order.OrderStatusId))
            {
                throw new Exception($"Không thể hủy đơn #{orderId}. Đơn đã hoàn thành hoặc đã bị hủy trước đó.");
            }

            order.OrderStatusId = SystemConstants.OrderStatuses.Cancelled;

            if (!string.IsNullOrEmpty(reason))
            {
                order.Note = (order.Note ?? "") + " | [ADMIN_CANCEL]: " + reason;
            }

            var payment = order.Payments.FirstOrDefault();
            if (payment != null)
            {
                if (payment.PaymentStatusId == SystemConstants.PaymentStatuses.Paid)
                    payment.PaymentStatusId = SystemConstants.PaymentStatuses.Refunded;
                else
                    payment.PaymentStatusId = SystemConstants.PaymentStatuses.Failed;
            }

            await _inventoryService.ReleaseInventoryForOrderAsync(orderId);

            await _context.SaveChangesAsync();

            await _hubContext.Clients.Group("AdminDashboard")
                .SendAsync("ReceiveOrderStatusUpdate", orderId, order.OrderStatusId);
        }

        public async Task<int> SimulateWebhookAsync(int storeId)
        {
            var deliveringOrders = await _context.Orders
                .Where(o => o.StoreId == storeId
                    && o.Source == OrderSources.Website
                    && o.OrderStatusId == SystemConstants.OrderStatuses.Delivering)
                .Select(o => o.OrderId)
                .ToListAsync();

            if (!deliveringOrders.Any()) return 0;

            foreach (var orderId in deliveringOrders)
            {
                await CompleteOrderAsync(orderId, storeId);
            }

            return deliveringOrders.Count;
        }

        // ===================================================
        // ORDER HISTORY — DataTables Server-Side Processing
        // ===================================================

        public async Task<DataTablesResponse<AdminOrderHistoryRowDto>> GetOrderHistoryAsync(DataTablesRequest request, int storeId)
        {
            var query = BuildHistoryQuery(request.SearchKeyword, request.DateFrom, request.DateTo, request.StatusFilter, request.PaymentMethodFilter, storeId);

            int totalRecords = await BuildHistoryQuery(null, null, null, null, null, storeId).CountAsync();
            int filteredRecords = await query.CountAsync();

            // Sorting
            var sortColumnIndex = request.Order?.FirstOrDefault()?.Column ?? 0;
            var sortDir = request.Order?.FirstOrDefault()?.Dir ?? "desc";
            var sortColumn = request.Columns?.ElementAtOrDefault(sortColumnIndex)?.Data ?? "createdAt";

            query = sortColumn.ToLower() switch
            {
                "orderId" or "orderid" => sortDir == "asc" ? query.OrderBy(o => o.OrderId) : query.OrderByDescending(o => o.OrderId),
                "total" => sortDir == "asc" ? query.OrderBy(o => o.Total) : query.OrderByDescending(o => o.Total),
                "orderStatusName" or "orderstatusname" => sortDir == "asc" ? query.OrderBy(o => o.OrderStatus.Name) : query.OrderByDescending(o => o.OrderStatus.Name),
                _ => sortDir == "asc"
                    ? query.OrderBy(o => o.Payments
                        .Where(p => p.PaymentStatusId == SystemConstants.PaymentStatuses.Paid
                            || p.PaymentStatusId == SystemConstants.PaymentStatuses.Refunded)
                        .Max(p => (DateTime?)p.PaidAt) ?? o.CreatedAt)
                    : query.OrderByDescending(o => o.Payments
                        .Where(p => p.PaymentStatusId == SystemConstants.PaymentStatuses.Paid
                            || p.PaymentStatusId == SystemConstants.PaymentStatuses.Refunded)
                        .Max(p => (DateTime?)p.PaidAt) ?? o.CreatedAt),
            };

            var data = await query
                .Skip(request.Start)
                .Take(request.Length)
                .Select(o => new AdminOrderHistoryRowDto
                {
                    OrderId = o.OrderId,
                    CreatedAt = o.Payments.Where(p => p.PaidAt.HasValue)
                        .Max(p => (DateTime?)p.PaidAt) ?? o.CreatedAt,
                    CustomerName = o.Customer != null ? o.Customer.FullName : (o.ReceiverName ?? "Khách vãng lai"),
                    CustomerPhone = o.ReceiverPhone ?? "",
                    StoreName = o.Store != null ? o.Store.Name : $"Cửa hàng #{o.StoreId}",
                    StaffName = o.Staff != null ? o.Staff.FullName : "Chưa xác định",
                    OrderTypeName = o.OrderType != null ? o.OrderType.Name : "Chưa xác định",
                    Total = o.Total,
                    PaymentMethodId = o.Payments.Any() ? o.Payments.First().PaymentMethodId : 0,
                    PaymentMethodName = "Chưa xác định",
                    OrderStatusId = o.PaymentStatusId,
                    OrderStatusName = o.PaymentStatusId == SystemConstants.PaymentStatuses.Refunded
                        ? "Đã hoàn tiền"
                        : "Đã thanh toán",
                    OrderStatusBadge = o.PaymentStatusId == SystemConstants.PaymentStatuses.Refunded
                        ? "bg-warning text-dark"
                        : "bg-success",
                    ReceiptState = "Hóa đơn chính thức đã sẵn sàng",
                    DrinkLabelState = "Tem chính thức đã sẵn sàng"
                })
                .ToListAsync();
            await ApplyPaymentDisplayAsync(data);

            return new DataTablesResponse<AdminOrderHistoryRowDto>
            {
                Draw = request.Draw,
                RecordsTotal = totalRecords,
                RecordsFiltered = filteredRecords,
                Data = data
            };
        }

        public async Task<AdminOrderHistoryDetailDto> GetOrderHistoryDetailAsync(int orderId, int storeId)
        {
            var order = await _context.Orders
                .AsSplitQuery()
                .Include(o => o.Customer)
                .Include(o => o.OrderStatus)
                .Include(o => o.OrderType)
                .Include(o => o.Store)
                .Include(o => o.Staff)
                .Include(o => o.Payments).ThenInclude(p => p.PaymentMethod)
                .Include(o => o.Payments).ThenInclude(p => p.PaymentStatus)
                .Include(o => o.OrderDetails).ThenInclude(od => od.OrderToppings)
                .FirstOrDefaultAsync(o => o.OrderId == orderId
                    && o.StoreId == storeId
                    && o.Source == OrderSources.Pos
                    && o.OrderStatusId == SystemConstants.OrderStatuses.Completed
                    && (o.PaymentStatusId == SystemConstants.PaymentStatuses.Paid
                        || o.PaymentStatusId == SystemConstants.PaymentStatuses.Refunded)
                    && o.Payments.Any(p => p.PaymentStatusId == SystemConstants.PaymentStatuses.Paid
                        || p.PaymentStatusId == SystemConstants.PaymentStatuses.Refunded));

            if (order == null) return null;

            return new AdminOrderHistoryDetailDto
            {
                OrderId = order.OrderId,
                CreatedAt = order.Payments
                    .Where(p => p.PaymentStatusId == SystemConstants.PaymentStatuses.Paid
                        || p.PaymentStatusId == SystemConstants.PaymentStatuses.Refunded)
                    .Where(p => p.PaidAt.HasValue)
                    .Max(p => (DateTime?)p.PaidAt) ?? order.CreatedAt,
                CustomerName = order.Customer?.FullName ?? order.ReceiverName ?? "Khách vãng lai",
                CustomerPhone = order.ReceiverPhone,
                DeliveryAddress = order.DeliveryAddress,
                Note = order.Note,
                Source = order.Source,
                StoreName = order.Store?.Name ?? $"Cửa hàng #{order.StoreId}",
                StaffName = order.Staff?.FullName ?? "Chưa xác định",
                OrderStatusId = order.PaymentStatusId,
                OrderStatusName = order.PaymentStatusId == SystemConstants.PaymentStatuses.Refunded
                    ? "Đã hoàn tiền"
                    : "Đã thanh toán",
                OrderStatusBadge = order.PaymentStatusId == SystemConstants.PaymentStatuses.Refunded
                    ? "badge bg-warning text-dark"
                    : "badge bg-success",
                OrderTypeName = order.OrderType?.Name,
                PaymentMethodName = OrderChannelPolicy.GetPaymentDisplay(order.Payments),
                PaymentStatusName = order.PaymentStatusId == SystemConstants.PaymentStatuses.Refunded
                    ? "Đã hoàn tiền"
                    : "Đã thanh toán",
                ReceiptState = "Hóa đơn chính thức đã sẵn sàng",
                DrinkLabelState = "Tem chính thức đã sẵn sàng",
                SubTotal = order.SubTotal,
                ShippingFee = order.ShippingFee,
                VoucherDiscount = order.VoucherDiscount,
                PointDiscount = order.PointDiscount,
                Total = order.Total,
                Payments = order.Payments
                    .OrderBy(x => x.PaymentId)
                    .Select(x => new AdminOrderHistoryPaymentDto
                    {
                        PaymentMethodName = OrderChannelPolicy.GetPaymentDisplay(new[] { x }),
                        Amount = x.Amount,
                        ReceivedAmount = x.ReceivedAmount,
                        ChangeAmount = x.ChangeAmount,
                        PaidAt = x.PaidAt,
                        TransactionCode = x.TransactionCode
                    })
                    .ToList(),
                Items = order.OrderDetails.Select(od => new AdminOrderHistoryItemDto
                {
                    DrinkName = od.DrinkName ?? "Chưa xác định",
                    SizeName = od.SizeName,
                    Quantity = od.Quantity,
                    Price = od.Price,
                    Note = od.Note,
                    Toppings = od.OrderToppings.Select(ot => new AdminOrderHistoryToppingDto
                    {
                        Name = ot.ToppingName,
                        Price = ot.Price
                    }).ToList()
                }).ToList()
            };
        }

        public async Task<List<AdminOrderHistoryRowDto>> GetFilteredOrdersForExportAsync(
            string searchKeyword, string dateFrom, string dateTo, int? statusFilter, int? paymentMethodFilter, int storeId)
        {
            var query = BuildHistoryQuery(searchKeyword, dateFrom, dateTo, statusFilter, paymentMethodFilter, storeId);

            var data = await query
                .OrderByDescending(o => o.Payments
                    .Where(p => p.PaymentStatusId == SystemConstants.PaymentStatuses.Paid
                        || p.PaymentStatusId == SystemConstants.PaymentStatuses.Refunded)
                    .Max(p => (DateTime?)p.PaidAt) ?? o.CreatedAt)
                .Select(o => new AdminOrderHistoryRowDto
                {
                    OrderId = o.OrderId,
                    CreatedAt = o.Payments.Where(p => p.PaidAt.HasValue)
                        .Max(p => (DateTime?)p.PaidAt) ?? o.CreatedAt,
                    CustomerName = o.Customer != null ? o.Customer.FullName : (o.ReceiverName ?? "Khách vãng lai"),
                    CustomerPhone = o.ReceiverPhone ?? "",
                    StoreName = o.Store != null ? o.Store.Name : $"Cửa hàng #{o.StoreId}",
                    StaffName = o.Staff != null ? o.Staff.FullName : "Chưa xác định",
                    OrderTypeName = o.OrderType != null ? o.OrderType.Name : "Chưa xác định",
                    Total = o.Total,
                    PaymentMethodId = o.Payments.Any() ? o.Payments.First().PaymentMethodId : 0,
                    PaymentMethodName = "Chưa xác định",
                    OrderStatusId = o.PaymentStatusId,
                    OrderStatusName = o.PaymentStatusId == SystemConstants.PaymentStatuses.Refunded
                        ? "Đã hoàn tiền"
                        : "Đã thanh toán",
                    OrderStatusBadge = o.PaymentStatusId == SystemConstants.PaymentStatuses.Refunded
                        ? "bg-warning text-dark"
                        : "bg-success",
                    ReceiptState = "Hóa đơn chính thức đã sẵn sàng",
                    DrinkLabelState = "Tem chính thức đã sẵn sàng"
                })
                .ToListAsync();
            await ApplyPaymentDisplayAsync(data);
            return data;
        }

        /// <summary>
        /// Xây dựng IQueryable chung cho cả DataTables lẫn Export — DRY Principle.
        /// </summary>
        private IQueryable<Order> BuildHistoryQuery(
            string searchKeyword, string dateFrom, string dateTo, int? statusFilter, int? paymentMethodFilter, int storeId)
        {
            var query = _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderStatus)
                .Include(o => o.Payments).ThenInclude(p => p.PaymentMethod)
                .Where(o => o.StoreId == storeId
                    && o.Source == OrderSources.Pos
                    && o.OrderStatusId == SystemConstants.OrderStatuses.Completed
                    && (o.PaymentStatusId == SystemConstants.PaymentStatuses.Paid
                        || o.PaymentStatusId == SystemConstants.PaymentStatuses.Refunded)
                    && o.Payments.Any(p => p.PaymentStatusId == SystemConstants.PaymentStatuses.Paid
                        || p.PaymentStatusId == SystemConstants.PaymentStatuses.Refunded))
                .AsQueryable();

            // Text search: OrderId, Customer name, Phone
            if (!string.IsNullOrWhiteSpace(searchKeyword))
            {
                var keyword = searchKeyword.Trim().ToLower();

                // Check if searching by OrderId number
                bool isNumeric = int.TryParse(keyword.Replace("#cc", "").Replace("#", ""), out int searchId);

                query = query.Where(o =>
                    (isNumeric && o.OrderId == searchId) ||
                    (o.ReceiverPhone != null && o.ReceiverPhone.Contains(keyword)) ||
                    (o.ReceiverName != null && o.ReceiverName.ToLower().Contains(keyword)) ||
                    (o.Customer != null && o.Customer.FullName.ToLower().Contains(keyword))
                );
            }

            // Date range
            if (!string.IsNullOrWhiteSpace(dateFrom) && DateTime.TryParse(dateFrom, out var from))
            {
                query = query.Where(o => (o.Payments
                    .Where(p => p.PaymentStatusId == SystemConstants.PaymentStatuses.Paid
                        || p.PaymentStatusId == SystemConstants.PaymentStatuses.Refunded)
                    .Max(p => (DateTime?)p.PaidAt) ?? o.CreatedAt) >= from);
            }
            if (!string.IsNullOrWhiteSpace(dateTo) && DateTime.TryParse(dateTo, out var to))
            {
                var exclusiveTo = to.AddDays(1);
                query = query.Where(o => (o.Payments
                    .Where(p => p.PaymentStatusId == SystemConstants.PaymentStatuses.Paid
                        || p.PaymentStatusId == SystemConstants.PaymentStatuses.Refunded)
                    .Max(p => (DateTime?)p.PaidAt) ?? o.CreatedAt) < exclusiveTo);
            }

            // Status
            if (statusFilter.HasValue && statusFilter.Value > 0)
            {
                query = query.Where(o => o.PaymentStatusId == statusFilter.Value);
            }

            // Payment method
            if (paymentMethodFilter.HasValue && paymentMethodFilter.Value > 0)
            {
                query = query.Where(o => o.Payments.Any(p => p.PaymentMethodId == paymentMethodFilter.Value));
            }

            return query;
        }

        private async Task<Order?> GetBoardOrderAsync(int orderId, int storeId)
        {
            return await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId
                && o.StoreId == storeId
                && o.Source == OrderSources.Website);
        }

        private async Task ApplyPaymentDisplayAsync(List<AdminOrderHistoryRowDto> rows)
        {
            var orderIds = rows.Select(x => x.OrderId).ToList();
            if (orderIds.Count == 0)
                return;

            var payments = await _context.Payments
                .AsNoTracking()
                .Include(x => x.PaymentMethod)
                .Where(x => orderIds.Contains(x.OrderId))
                .ToListAsync();
            var byOrder = payments.GroupBy(x => x.OrderId).ToDictionary(x => x.Key, x => x.ToList());

            foreach (var row in rows)
            {
                if (byOrder.TryGetValue(row.OrderId, out var orderPayments))
                {
                    row.PaymentMethodName = OrderChannelPolicy.GetPaymentDisplay(orderPayments);
                    row.PaymentMethodId = orderPayments.Select(x => x.PaymentMethodId).Distinct().Count() == 1
                        ? orderPayments[0].PaymentMethodId
                        : 0;
                }
            }
        }
    }
}
