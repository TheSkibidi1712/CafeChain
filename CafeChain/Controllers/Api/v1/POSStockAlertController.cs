using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Inventories;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CafeChain.Controllers.Api.v1
{
    /// <summary>
    /// Issue #98 — POS shortage report (Báo thiếu hàng).
    /// </summary>
    [Route("api/v1/pos")]
    public class POSStockAlertController : PosApiController
    {
        private static readonly HashSet<string> ReportRoles = new(StringComparer.Ordinal)
        {
            RoleConstants.SalesStaff,
            RoleConstants.ShiftSupervisor,
            RoleConstants.StoreManager
        };

        private readonly IStockShortageReportService _reportService;

        public POSStockAlertController(IStockShortageReportService reportService)
        {
            _reportService = reportService;
        }

        /// <summary>
        /// POST /api/v1/pos/stock-alerts/report-shortage
        /// </summary>
        [HttpPost("stock-alerts/report-shortage")]
        public async Task<IActionResult> ReportShortage([FromBody] StockShortageReportRequestDto request)
        {
            if (!IsReportRole())
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    success = false,
                    message = "Tài khoản không có quyền báo thiếu hàng."
                });
            }

            var storeId = CurrentStoreId;
            var staffId = CurrentStaffId;

            var result = await _reportService.ReportShortageAsync(storeId, staffId, request);
            if (!result.IsSuccess)
            {
                return BadRequest(new
                {
                    success = false,
                    message = result.Message
                });
            }

            var data = result.Data!;
            var message = result.Message
                ?? "Đã gửi thông báo trong hệ thống cho Quản lý chi nhánh và Kế toán/kho.";

            if (data.EmailFailedCount > 0)
            {
                message += " Kênh email chưa gửi được nhưng thông báo realtime vẫn thành công.";
            }

            return Ok(new
            {
                success = true,
                message,
                data = new
                {
                    stockAlertId = data.StockAlertId,
                    createdOrUpdated = data.CreatedOrUpdated,
                    notificationCount = data.NotificationCount,
                    emailAttempted = data.EmailAttempted,
                    emailSentCount = data.EmailSentCount,
                    emailFailedCount = data.EmailFailedCount,
                    alertType = data.AlertType,
                    isOutOfThresholdDemand = data.IsOutOfThresholdDemand,
                    availableBaseQuantity = data.AvailableBaseQuantity,
                    minimumThresholdBaseQuantity = data.MinimumThresholdBaseQuantity,
                    decisionTargetBaseQuantity = data.DecisionTargetBaseQuantity,
                    suggestedBaseQuantity = data.SuggestedBaseQuantity,
                    warnings = data.Warnings
                }
            });
        }

        private bool IsReportRole()
        {
            var roles = User.FindAll(ClaimTypes.Role)
                .Select(c => c.Value)
                .Concat(User.FindAll("role").Select(c => c.Value));
            return roles.Any(r => ReportRoles.Contains(r));
        }
    }
}
