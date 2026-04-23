using CafeChain.Application.Interfaces.POS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Security.Claims;
using System;
using System.Collections.Generic;
using CafeChain.Application.DTOs.POS;
using CafeChain.Data;
using CafeChain.Models.Stores;
using CafeChain.Models.Orders;

namespace CafeChain.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class AdminPOSController : Controller
    {
        private readonly IWorkShiftService _workShiftService;
        private readonly AppDbContext _context;

        public AdminPOSController(IWorkShiftService workShiftService, AppDbContext context)
        {
            _workShiftService = workShiftService;
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        public class OpenShiftRequest
        {
            public decimal StartingCash { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> OpenShift([FromBody] OpenShiftRequest request)
        {
            try
            {
                // Retrieve UserId and StoreId from Claims
                var userIdClaim = User.FindFirst("AccountId")?.Value; // Ideally StaffId, but we'll use 1 for demo if missing
                var storeIdClaim = User.FindFirst("StoreId")?.Value;

                int userId = int.TryParse(userIdClaim, out int uid) ? uid : 1; 
                int storeId = int.TryParse(storeIdClaim, out int sid) ? sid : 1; // Assuming default Store 1 if claim is missing

                var result = await _workShiftService.OpenShiftAsync(userId, storeId, request.StartingCash);

                return Json(new { success = result.IsSuccess, message = result.Message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        [HttpPost("Admin/AdminPOS/SyncOfflineOrders")]
        public async Task<IActionResult> SyncOfflineOrders([FromBody] List<OfflineOrderSyncDTO> offlineOrders)
        {
            if (offlineOrders == null || offlineOrders.Count == 0)
                return Json(new { success = false, message = "Không có dữ liệu đồng bộ." });

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                int syncedCount = 0;
                
                foreach (var orderDto in offlineOrders)
                {
                    // 1. Create main order
                    var newOrder = new Order
                    {
                        Total = orderDto.TotalAmount,
                        OrderTypeId = orderDto.OrderTypeId > 0 ? orderDto.OrderTypeId : 1, // Default DineIn
                        OrderStatusId = 4, // Trạng thái: Đã thanh toán / Hoàn thành
                        CreatedAt = DateTime.UtcNow,
                        StoreId = orderDto.StoreId ?? 1,
                        Note = "[OFFLINE-SYNC] " + orderDto.Note
                    };

                    _context.Orders.Add(newOrder);
                    await _context.SaveChangesAsync(); // Get OrderId

                    // 2. Create order details
                    foreach (var item in orderDto.Details)
                    {
                        var orderDetail = new OrderDetail
                        {
                            OrderId = newOrder.OrderId,
                            DrinkId = item.ItemId,
                            Quantity = item.Quantity,
                            Price = item.UnitPrice
                        };
                        _context.OrderDetails.Add(orderDetail);
                    }
                    
                    await _context.SaveChangesAsync();
                    
                    // 3. BLACKBOX INVENTORY DEDUCTION 
                    // TODO: Trigger Inventory Deduction Event / Service here.
                    // DO NOT TOUCH OR MODIFY INVENTORY SYSTEM.
                    // _inventoryDeductionService.DeductForOrder(newOrder.OrderId);

                    syncedCount++;
                }

                await transaction.CommitAsync();
                return Json(new { success = true, message = $"Đã đồng bộ thành công {syncedCount} đơn hàng ngoại tuyến." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Json(new { success = false, message = "Lỗi hệ thống khi đồng bộ: " + ex.Message });
            }
        }
    }
}
