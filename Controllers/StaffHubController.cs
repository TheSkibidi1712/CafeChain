using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using CafeChain.Application.Interfaces.Attendance;
using CafeChain.Application.Constants;

namespace CafeChain.Controllers
{
    [Authorize]
    public class StaffHubController : Controller
    {
        private readonly IAttendanceActionService _actionService;
        private readonly IAttendanceSecurityService _securityService;

        public StaffHubController(
            IAttendanceActionService actionService,
            IAttendanceSecurityService securityService)
        {
            _actionService = actionService;
            _securityService = securityService;
        }

        // GET: /StaffHub/Index
        public async Task<IActionResult> Index()
        {
            // Anti-IDOR: Extract accountId from authenticated Claims only
            var accountIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(accountIdStr) || !int.TryParse(accountIdStr, out int accountId))
            {
                return RedirectToAction("Login", "Account");
            }

            // Get user role for POS access check
            var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            bool canAccessPos = role == RoleConstants.Cashier
                             || role == RoleConstants.ShiftSupervisor;

            // Load dashboard data via service layer (N-Tier compliant)
            var result = await _actionService.GetKioskDataAsync(accountId);
            if (!result.IsSuccess)
            {
                TempData["ErrorMessage"] = result.Message;
                return RedirectToAction("Login", "Account");
            }

            ViewBag.StaffHubData = result.Data;
            ViewBag.CanAccessPos = canAccessPos;
            ViewBag.UserRole = role;
            ViewBag.AccountId = accountId;

            return View();
        }
    }
}
