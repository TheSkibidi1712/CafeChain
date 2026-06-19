using CafeChain.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace CafeChain.Controllers.Api.v1
{
    /// <summary>
    /// Open API endpoints dành cho hệ thống Kế toán / ERP tích hợp.
    /// Issue #47: Mock endpoints chuẩn OpenAPI — Swagger UI tự sinh tài liệu.
    /// </summary>
    [ApiController]
    [Route("api/v1/erp")]
    [Produces("application/json")]
    [Authorize(Roles = "Admin,Accountant")]
    public class ErpIntegrationController : ControllerBase
    {
        private readonly AppDbContext _context;

        // VAT rate mặc định 8% theo quy định thuế GTGT Việt Nam (Nghị định 44/2023)
        private const decimal VAT_RATE = 0.08m;

        public ErpIntegrationController(AppDbContext context)
        {
            _context = context;
        }

        // ============================================================
        // ENDPOINT 1: Daily Sales Summary — Tổng hợp doanh thu theo ngày
        // ============================================================
        /// <summary>
        /// Trả về tổng hợp doanh thu bán hàng của từng cửa hàng trong ngày được chọn.
        /// Dữ liệu nhóm theo StoreId, bao gồm: doanh thu, VAT, chiết khấu, phân tách tiền mặt/chuyển khoản.
        /// </summary>
        /// <param name="date">Ngày cần truy xuất (yyyy-MM-dd). Mặc định: hôm nay.</param>
        /// <returns>Danh sách tổng hợp doanh thu theo cửa hàng.</returns>
        /// <response code="200">Trả về dữ liệu tổng hợp thành công.</response>
        /// <response code="400">Tham số date không hợp lệ.</response>
        /// <response code="401">Chưa đăng nhập hoặc token hết hạn.</response>
        [HttpGet("daily-sales-summary")]
        [ProducesResponseType(typeof(DailySalesSummaryResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetDailySalesSummary([FromQuery] DateTime? date)
        {
            var targetDate = date?.Date ?? DateTime.Today;

            // Lấy tất cả Order trong ngày kèm Payment
            var ordersQuery = _context.Orders
                .AsNoTracking()
                .Include(o => o.Payments)
                .Where(o => o.CreatedAt.Date == targetDate);

            // Group theo StoreId
            var storeSummaries = await ordersQuery
                .GroupBy(o => o.StoreId)
                .Select(g => new StoreSalesSummaryDto
                {
                    StoreId = g.Key,
                    TotalOrders = g.Count(),
                    TotalRevenue = g.Sum(o => o.Total),
                    TotalDiscount = g.Sum(o => o.VoucherDiscount + o.PointDiscount),

                    // Phân tách theo phương thức thanh toán
                    // PaymentMethodId: 1 = Cash, 2+ = Banking/E-Wallet
                    CashAmount = g.SelectMany(o => o.Payments)
                        .Where(p => p.PaymentMethodId == 1)
                        .Sum(p => (decimal?)p.Amount) ?? 0m,
                    BankingAmount = g.SelectMany(o => o.Payments)
                        .Where(p => p.PaymentMethodId != 1)
                        .Sum(p => (decimal?)p.Amount) ?? 0m
                })
                .ToListAsync();

            // Tính VAT lý thuyết server-side (tránh complex expression trong EF LINQ)
            foreach (var s in storeSummaries)
            {
                // VAT = Revenue × Rate / (1 + Rate) — tách thuế từ giá đã bao gồm VAT
                s.TotalVat = Math.Round(s.TotalRevenue * VAT_RATE / (1 + VAT_RATE), 0);
            }

            var response = new DailySalesSummaryResponse
            {
                Date = targetDate.ToString("yyyy-MM-dd"),
                GeneratedAt = DateTime.UtcNow,
                TotalStores = storeSummaries.Count,
                Stores = storeSummaries
            };

            return Ok(response);
        }

        // ============================================================
        // ENDPOINT 2: Inventory Movements — Lịch sử biến động kho
        // ============================================================
        /// <summary>
        /// Trả về lịch sử biến động kho (InventoryTransaction) trong ngày được chọn.
        /// Bao gồm: nguyên liệu, số lượng trước/sau, loại giao dịch (SALES_DEDUCTION, IMPORT, ...).
        /// </summary>
        /// <param name="date">Ngày cần truy xuất (yyyy-MM-dd). Mặc định: hôm nay.</param>
        /// <param name="storeId">Lọc theo cửa hàng (tùy chọn).</param>
        /// <returns>Danh sách biến động kho.</returns>
        /// <response code="200">Trả về dữ liệu biến động kho thành công.</response>
        /// <response code="400">Tham số date không hợp lệ.</response>
        /// <response code="401">Chưa đăng nhập hoặc token hết hạn.</response>
        [HttpGet("inventory-movements")]
        [ProducesResponseType(typeof(InventoryMovementsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetInventoryMovements(
            [FromQuery] DateTime? date,
            [FromQuery] int? storeId)
        {
            var targetDate = date?.Date ?? DateTime.Today;

            var query = _context.InventoryTransactions
                .AsNoTracking()
                .Include(t => t.StoreInventory)
                    .ThenInclude(si => si.Ingredient)
                .Include(t => t.StoreInventory)
                    .ThenInclude(si => si.Store)
                .Where(t => t.CreatedAt.Date == targetDate);

            // Optional filter theo StoreId
            if (storeId.HasValue)
            {
                query = query.Where(t => t.StoreInventory.StoreId == storeId.Value);
            }

            var movements = await query
                .OrderBy(t => t.StoreInventory.StoreId)
                .ThenBy(t => t.CreatedAt)
                .Select(t => new InventoryMovementDto
                {
                    TransactionId = t.InventoryTransactionId,
                    StoreId = t.StoreInventory.StoreId,
                    StoreName = t.StoreInventory.Store.Name,
                    IngredientId = t.StoreInventory.IngredientId,
                    IngredientName = t.StoreInventory.Ingredient != null
                        ? t.StoreInventory.Ingredient.Name
                        : (t.StoreInventory.RecipeId != null ? "[BTP] Recipe #" + t.StoreInventory.RecipeId : "N/A"),
                    BeforeQty = t.BeforeQty,
                    QuantityChanged = t.Quantity,
                    AfterQty = t.AfterQty,
                    TransactionType = t.Type.ToString(),
                    Timestamp = t.CreatedAt
                })
                .ToListAsync();

            var response = new InventoryMovementsResponse
            {
                Date = targetDate.ToString("yyyy-MM-dd"),
                GeneratedAt = DateTime.UtcNow,
                StoreId = storeId,
                TotalMovements = movements.Count,
                Movements = movements
            };

            return Ok(response);
        }
    }

    // ============================================================
    // RESPONSE DTOs — Chuẩn OpenAPI cho Swagger UI
    // ============================================================

    /// <summary>Phản hồi tổng hợp doanh thu bán hàng theo ngày.</summary>
    public class DailySalesSummaryResponse
    {
        /// <summary>Ngày truy xuất (yyyy-MM-dd).</summary>
        public string Date { get; set; } = string.Empty;

        /// <summary>Thời điểm hệ thống sinh báo cáo (UTC).</summary>
        public DateTime GeneratedAt { get; set; }

        /// <summary>Tổng số cửa hàng có doanh thu trong ngày.</summary>
        public int TotalStores { get; set; }

        /// <summary>Chi tiết doanh thu từng cửa hàng.</summary>
        public System.Collections.Generic.List<StoreSalesSummaryDto> Stores { get; set; } = new();
    }

    /// <summary>Tổng hợp doanh thu một cửa hàng trong ngày.</summary>
    public class StoreSalesSummaryDto
    {
        /// <summary>Mã cửa hàng.</summary>
        public int StoreId { get; set; }

        /// <summary>Tổng doanh thu (VNĐ) — đã bao gồm VAT.</summary>
        public decimal TotalRevenue { get; set; }

        /// <summary>Thuế GTGT lý thuyết 8% (VNĐ) — tách ngược từ giá bán đã bao gồm VAT.</summary>
        public decimal TotalVat { get; set; }

        /// <summary>Tổng chiết khấu (Voucher + Điểm thưởng) (VNĐ).</summary>
        public decimal TotalDiscount { get; set; }

        /// <summary>Doanh thu thanh toán tiền mặt (VNĐ).</summary>
        public decimal CashAmount { get; set; }

        /// <summary>Doanh thu chuyển khoản / ví điện tử (VNĐ).</summary>
        public decimal BankingAmount { get; set; }

        /// <summary>Tổng số đơn hàng.</summary>
        public int TotalOrders { get; set; }
    }

    /// <summary>Phản hồi danh sách biến động kho theo ngày.</summary>
    public class InventoryMovementsResponse
    {
        /// <summary>Ngày truy xuất (yyyy-MM-dd).</summary>
        public string Date { get; set; } = string.Empty;

        /// <summary>Thời điểm hệ thống sinh báo cáo (UTC).</summary>
        public DateTime GeneratedAt { get; set; }

        /// <summary>Lọc theo cửa hàng (null = tất cả).</summary>
        public int? StoreId { get; set; }

        /// <summary>Tổng số bản ghi biến động.</summary>
        public int TotalMovements { get; set; }

        /// <summary>Chi tiết từng biến động kho.</summary>
        public System.Collections.Generic.List<InventoryMovementDto> Movements { get; set; } = new();
    }

    /// <summary>Chi tiết một biến động kho.</summary>
    public class InventoryMovementDto
    {
        /// <summary>Mã giao dịch kho.</summary>
        public int TransactionId { get; set; }

        /// <summary>Mã cửa hàng.</summary>
        public int StoreId { get; set; }

        /// <summary>Tên cửa hàng.</summary>
        public string StoreName { get; set; } = string.Empty;

        /// <summary>Mã nguyên liệu (null nếu là Bán thành phẩm).</summary>
        public int? IngredientId { get; set; }

        /// <summary>Tên nguyên liệu.</summary>
        public string IngredientName { get; set; } = string.Empty;

        /// <summary>Số lượng tồn TRƯỚC giao dịch.</summary>
        public decimal BeforeQty { get; set; }

        /// <summary>Số lượng thay đổi (luôn dương — hướng xử lý qua TransactionType).</summary>
        public decimal QuantityChanged { get; set; }

        /// <summary>Số lượng tồn SAU giao dịch.</summary>
        public decimal AfterQty { get; set; }

        /// <summary>Loại giao dịch: SALES_DEDUCTION, IMPORT, EXPORT, WASTE, STOCK_TAKE, PRODUCTION_IN, PRODUCTION_OUT.</summary>
        public string TransactionType { get; set; } = string.Empty;

        /// <summary>Thời điểm phát sinh giao dịch (UTC).</summary>
        public DateTime Timestamp { get; set; }
    }

    /// <summary>Phản hồi lỗi chuẩn.</summary>
    public class ErrorResponse
    {
        /// <summary>Mô tả lỗi.</summary>
        public string Message { get; set; } = string.Empty;
    }
}
