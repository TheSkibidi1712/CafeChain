using CafeChain.Application.Interfaces.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace CafeChain.Areas.Admin.Controllers
{
    /// <summary>
    /// Skinny Controller cho Order Management Dashboard & KDS.
    /// [Skill.md §1] Controller chỉ điều hướng + gọi Service. Tối đa 3-5 dòng/action.
    /// [REFACTOR] Đã xóa: AppDbContext, IHubContext, IInventoryService — toàn bộ nằm trong AdminOrderService.
    /// </summary>
    [Area("Admin")]
    [Authorize]
    public class AdminOrderController : Controller
    {
        private readonly IAdminOrderService _adminOrderService;

        public AdminOrderController(IAdminOrderService adminOrderService)
        {
            _adminOrderService = adminOrderService;
        }

        // Màn hình Dashboard Order Management & KDS
        public IActionResult Index()
        {
            return View();
        }

        // Lấy danh sách đơn hàng cho Kanban board
        [HttpGet("/api/AdminOrder/GetOrders")]
        public async Task<IActionResult> GetOrders()
        {
            var data = await _adminOrderService.GetKanbanOrdersAsync();
            return Ok(data);
        }

        // Chi tiết đơn hàng cho Offcanvas
        [HttpGet("/api/AdminOrder/GetOrderDetails/{orderId}")]
        public async Task<IActionResult> GetOrderDetails(int orderId)
        {
            var data = await _adminOrderService.GetOrderDetailsAsync(orderId);
            if (data == null) return NotFound();
            return Ok(data);
        }

        // ===================================================
        // CÁC ACTION CHUYỂN TRẠNG THÁI — Skinny: chỉ gọi Service + trả kết quả
        // Guard Clauses + SignalR đều nằm trong Service (Skill.md §1, §1.1)
        // ===================================================

        [HttpPost("/api/AdminOrder/AcceptOrder/{orderId}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptOrder(int orderId)
        {
            try
            {
                await _adminOrderService.AcceptOrderAsync(orderId);
                return Ok("Đơn hàng đã được duyệt và chuyển sang Pha chế.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("/api/AdminOrder/ReadyForPickup/{orderId}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReadyForPickup(int orderId)
        {
            try
            {
                await _adminOrderService.ReadyForPickupAsync(orderId);
                return Ok("Đơn hàng đã xong món, chờ lấy.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // API lấy danh sách Shipper cho Dropdown
        [HttpGet("/api/AdminOrder/GetShippers")]
        public async Task<IActionResult> GetShippers()
        {
            var data = await _adminOrderService.GetShippersAsync();
            return Ok(data);
        }

        [HttpPost("/api/AdminOrder/Dispatched")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Dispatched([FromBody] CafeChain.Application.DTOs.Admin.DispatchOrderRequest request)
        {
            try
            {
                await _adminOrderService.DispatchOrderAsync(request);
                return Ok("Đơn hàng đã giao cho Shipper.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }



        [HttpPost("/api/AdminOrder/CompleteOrder/{orderId}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteOrder(int orderId)
        {
            try
            {
                await _adminOrderService.CompleteOrderAsync(orderId);
                return Ok("Đơn hàng đã hoàn thành.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("/api/AdminOrder/FailDelivery/{orderId}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FailDelivery(int orderId, [FromQuery] string reason)
        {
            try
            {
                await _adminOrderService.CancelOrderAsync(orderId, reason);
                return Ok("Đơn hàng đã bị hủy.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
