using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin;
using CafeChain.Application.Interfaces;
using CafeChain.Application.Interfaces.Admin;
using CafeChain.Data;
using CafeChain.Hubs;
using CafeChain.Models.Orders;
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

        public async Task<List<AdminOrderKanbanDto>> GetKanbanOrdersAsync()
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
                .Where(o => activeStatuses.Contains(o.OrderStatusId))
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
                .Where(o => historyStatuses.Contains(o.OrderStatusId) && o.CreatedAt >= today)
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

        public async Task<AdminOrderDetailDto> GetOrderDetailsAsync(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.OrderType)
                .Include(o => o.OrderDetails).ThenInclude(od => od.Drink)
                .Include(o => o.OrderDetails).ThenInclude(od => od.Size)
                .Include(o => o.OrderDetails).ThenInclude(od => od.OrderToppings).ThenInclude(ot => ot.Topping)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

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

        public async Task AcceptOrderAsync(int orderId)
        {
            var order = await _context.Orders.FindAsync(orderId);
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

        public async Task ReadyForPickupAsync(int orderId)
        {
            var order = await _context.Orders.FindAsync(orderId);
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

        public async Task<List<DTOs.Admin.ShipperDto>> GetShippersAsync()
        {
            var shippers = await _context.Set<CafeChain.Models.Staffs.Staff>()
                .Where(s => s.Active)
                .Select(s => new DTOs.Admin.ShipperDto
                {
                    Id = s.StaffId,
                    Name = s.FullName
                })
                .ToListAsync();

            return shippers;
        }

        public async Task DispatchOrderAsync(CafeChain.Application.DTOs.Admin.DispatchOrderRequest request)
        {
            var order = await _context.Orders.FindAsync(request.OrderId);
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
                    .AnyAsync(s => s.StaffId == request.InternalShipperId.Value && s.Active);

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



        public async Task CompleteOrderAsync(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);
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

            await _hubContext.Clients.Group("AdminDashboard")
                .SendAsync("ReceiveOrderStatusUpdate", orderId, order.OrderStatusId);
        }

        public async Task CancelOrderAsync(int orderId, string reason)
        {
            var order = await _context.Orders
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);
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

        public async Task<int> SimulateWebhookAsync()
        {
            var deliveringOrders = await _context.Orders
                .Where(o => o.OrderStatusId == SystemConstants.OrderStatuses.Delivering)
                .ToListAsync();

            if (!deliveringOrders.Any()) return 0;

            foreach (var order in deliveringOrders)
            {
                order.OrderStatusId = SystemConstants.OrderStatuses.Completed;
            }

            await _context.SaveChangesAsync();

            foreach (var order in deliveringOrders)
            {
                await _hubContext.Clients.Group("AdminDashboard")
                    .SendAsync("ReceiveOrderStatusUpdate", order.OrderId, order.OrderStatusId);
            }

            return deliveringOrders.Count;
        }

        // ===================================================
        // ORDER HISTORY — DataTables Server-Side Processing
        // ===================================================

        public async Task<DataTablesResponse<AdminOrderHistoryRowDto>> GetOrderHistoryAsync(DataTablesRequest request)
        {
            var query = BuildHistoryQuery(request.SearchKeyword, request.DateFrom, request.DateTo, request.StatusFilter, request.PaymentMethodFilter);

            int totalRecords = await _context.Orders.CountAsync();
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
                _ => sortDir == "asc" ? query.OrderBy(o => o.CreatedAt) : query.OrderByDescending(o => o.CreatedAt),
            };

            var data = await query
                .Skip(request.Start)
                .Take(request.Length)
                .Select(o => new AdminOrderHistoryRowDto
                {
                    OrderId = o.OrderId,
                    CreatedAt = o.CreatedAt,
                    CustomerName = o.Customer != null ? o.Customer.FullName : (o.ReceiverName ?? "Khách vãng lai"),
                    CustomerPhone = o.ReceiverPhone ?? "",
                    Total = o.Total,
                    PaymentMethodId = o.Payments.Any() ? o.Payments.First().PaymentMethodId : 0,
                    PaymentMethodName = o.Payments.Any() ? o.Payments.First().PaymentMethod.Name : "N/A",
                    OrderStatusId = o.OrderStatusId,
                    OrderStatusName = o.OrderStatus.Name,
                    OrderStatusBadge = o.OrderStatus.BadgeColor
                })
                .ToListAsync();

            return new DataTablesResponse<AdminOrderHistoryRowDto>
            {
                Draw = request.Draw,
                RecordsTotal = totalRecords,
                RecordsFiltered = filteredRecords,
                Data = data
            };
        }

        public async Task<AdminOrderHistoryDetailDto> GetOrderHistoryDetailAsync(int orderId)
        {
            var order = await _context.Orders
                .AsSplitQuery()
                .Include(o => o.Customer)
                .Include(o => o.OrderStatus)
                .Include(o => o.OrderType)
                .Include(o => o.Payments).ThenInclude(p => p.PaymentMethod)
                .Include(o => o.Payments).ThenInclude(p => p.PaymentStatus)
                .Include(o => o.OrderDetails).ThenInclude(od => od.OrderToppings)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null) return null;

            var payment = order.Payments.FirstOrDefault();

            return new AdminOrderHistoryDetailDto
            {
                OrderId = order.OrderId,
                CreatedAt = order.CreatedAt,
                CustomerName = order.Customer?.FullName ?? order.ReceiverName ?? "Khách vãng lai",
                CustomerPhone = order.ReceiverPhone,
                DeliveryAddress = order.DeliveryAddress,
                Note = order.Note,
                Source = order.Source,
                OrderStatusId = order.OrderStatusId,
                OrderStatusName = order.OrderStatus?.Name,
                OrderStatusBadge = order.OrderStatus?.BadgeColor,
                OrderTypeName = order.OrderType?.Name,
                PaymentMethodName = payment?.PaymentMethod?.Name ?? "N/A",
                PaymentStatusName = payment?.PaymentStatus?.Name ?? "N/A",
                SubTotal = order.SubTotal,
                ShippingFee = order.ShippingFee,
                VoucherDiscount = order.VoucherDiscount,
                PointDiscount = order.PointDiscount,
                Total = order.Total,
                Items = order.OrderDetails.Select(od => new AdminOrderHistoryItemDto
                {
                    DrinkName = od.DrinkName ?? "N/A",
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
            string searchKeyword, string dateFrom, string dateTo, int? statusFilter, int? paymentMethodFilter)
        {
            var query = BuildHistoryQuery(searchKeyword, dateFrom, dateTo, statusFilter, paymentMethodFilter);

            return await query
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new AdminOrderHistoryRowDto
                {
                    OrderId = o.OrderId,
                    CreatedAt = o.CreatedAt,
                    CustomerName = o.Customer != null ? o.Customer.FullName : (o.ReceiverName ?? "Khách vãng lai"),
                    CustomerPhone = o.ReceiverPhone ?? "",
                    Total = o.Total,
                    PaymentMethodId = o.Payments.Any() ? o.Payments.First().PaymentMethodId : 0,
                    PaymentMethodName = o.Payments.Any() ? o.Payments.First().PaymentMethod.Name : "N/A",
                    OrderStatusId = o.OrderStatusId,
                    OrderStatusName = o.OrderStatus.Name,
                    OrderStatusBadge = o.OrderStatus.BadgeColor
                })
                .ToListAsync();
        }

        /// <summary>
        /// Xây dựng IQueryable chung cho cả DataTables lẫn Export — DRY Principle.
        /// </summary>
        private IQueryable<Order> BuildHistoryQuery(
            string searchKeyword, string dateFrom, string dateTo, int? statusFilter, int? paymentMethodFilter)
        {
            var query = _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderStatus)
                .Include(o => o.Payments).ThenInclude(p => p.PaymentMethod)
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
                query = query.Where(o => o.CreatedAt >= from);
            }
            if (!string.IsNullOrWhiteSpace(dateTo) && DateTime.TryParse(dateTo, out var to))
            {
                query = query.Where(o => o.CreatedAt < to.AddDays(1));
            }

            // Status
            if (statusFilter.HasValue && statusFilter.Value > 0)
            {
                query = query.Where(o => o.OrderStatusId == statusFilter.Value);
            }

            // Payment method
            if (paymentMethodFilter.HasValue && paymentMethodFilter.Value > 0)
            {
                query = query.Where(o => o.Payments.Any(p => p.PaymentMethodId == paymentMethodFilter.Value));
            }

            return query;
        }
    }
}
