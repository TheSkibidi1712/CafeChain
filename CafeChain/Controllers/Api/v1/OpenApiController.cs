using CafeChain.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace CafeChain.Controllers.Api.v1
{
    /// <summary>
    /// Open API Mock/Real endpoints phục vụ đối soát Kế toán/ERP.
    /// </summary>
    [ApiController]
    [Route("api/open")]
    [Produces("application/json")]
    public class OpenApiController : ControllerBase
    {
        private readonly AppDbContext _context;

        public OpenApiController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Lấy danh sách hóa đơn bán hàng theo khoảng thời gian có phân trang.
        /// </summary>
        /// <param name="from">Ngày bắt đầu (yyyy-MM-dd). Tùy chọn.</param>
        /// <param name="to">Ngày kết thúc (yyyy-MM-dd). Tùy chọn.</param>
        /// <param name="page">Số trang (1-indexed). Mặc định: 1.</param>
        /// <param name="pageSize">Kích thước trang. Mặc định: 20.</param>
        /// <returns>Danh sách hóa đơn và dữ liệu phân trang.</returns>
        [HttpGet("orders")]
        public async Task<IActionResult> GetOrders(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            var query = _context.Orders.AsNoTracking();

            if (from.HasValue)
            {
                var startDate = from.Value.Date;
                query = query.Where(o => o.CreatedAt >= startDate);
            }

            if (to.HasValue)
            {
                var endDate = to.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(o => o.CreatedAt <= endDate);
            }

            int total = await query.CountAsync();
            var orders = await query
                .OrderByDescending(o => o.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new
                {
                    o.OrderId,
                    o.StoreId,
                    o.CustomerId,
                    o.SubTotal,
                    o.VoucherDiscount,
                    o.PointDiscount,
                    o.Total,
                    o.CreatedAt,
                    o.Note,
                    o.OrderStatusId,
                    o.PaymentStatusId,
                    o.OrderTypeId,
                    o.WorkShiftId,
                    o.ClientOrderId
                })
                .ToListAsync();

            return Ok(new
            {
                page,
                pageSize,
                total,
                data = orders
            });
        }

        /// <summary>
        /// Lấy danh sách các ca làm việc két tiền (WorkShift) theo khoảng thời gian có phân trang.
        /// </summary>
        /// <param name="from">Ngày bắt đầu (yyyy-MM-dd). Tùy chọn.</param>
        /// <param name="to">Ngày kết thúc (yyyy-MM-dd). Tùy chọn.</param>
        /// <param name="page">Số trang (1-indexed). Mặc định: 1.</param>
        /// <param name="pageSize">Kích thước trang. Mặc định: 20.</param>
        /// <returns>Danh sách ca làm việc và dữ liệu phân trang.</returns>
        [HttpGet("shifts")]
        public async Task<IActionResult> GetShifts(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            var query = _context.WorkShifts.AsNoTracking();

            if (from.HasValue)
            {
                var startDate = from.Value.Date;
                query = query.Where(s => s.StartTimeUtc >= startDate);
            }

            if (to.HasValue)
            {
                var endDate = to.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(s => s.StartTimeUtc <= endDate);
            }

            int total = await query.CountAsync();
            var shifts = await query
                .OrderByDescending(s => s.StartTimeUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new
                {
                    s.ShiftId,
                    s.StoreId,
                    s.UserId,
                    StartTime = s.StartTimeUtc,
                    EndTime = s.EndTimeUtc,
                    s.StartingCash,
                    s.ExpectedEndingCash,
                    s.ActualEndingCash,
                    s.CashDiscrepancy,
                    s.Status,
                    s.DiscrepancyReason,
                    s.PosTerminalId
                })
                .ToListAsync();

            return Ok(new
            {
                page,
                pageSize,
                total,
                data = shifts
            });
        }

        /// <summary>
        /// Lấy snapshot tồn kho hiện tại của cửa hàng.
        /// </summary>
        /// <param name="storeId">Mã cửa hàng cần truy xuất.</param>
        /// <param name="page">Số trang (1-indexed). Mặc định: 1.</param>
        /// <param name="pageSize">Kích thước trang. Mặc định: 20.</param>
        /// <returns>Snapshot danh sách tồn kho nguyên liệu/công thức có phân trang.</returns>
        [HttpGet("inventory")]
        public async Task<IActionResult> GetInventory(
            [FromQuery] int storeId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            var query = _context.StoreInventories
                .AsNoTracking()
                .Where(i => i.StoreId == storeId);

            int total = await query.CountAsync();
            var inventory = await query
                .OrderBy(i => i.StoreInventoryId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(i => new
                {
                    i.StoreInventoryId,
                    i.StoreId,
                    i.IngredientId,
                    i.RecipeId,
                    i.AvailableQty,
                    i.ReservedQty,
                    i.LastUpdated
                })
                .ToListAsync();

            return Ok(new
            {
                page,
                pageSize,
                total,
                data = inventory
            });
        }
    }
}
