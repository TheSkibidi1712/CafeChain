using CafeChain.Application.DTOs.Admin.Dashboard;
using CafeChain.Application.Interfaces.Admin.Dashboard;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CafeChain.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class DashboardController : Controller
    {
        private readonly IDashboardService _service;

        public DashboardController(IDashboardService service)
        {
            _service = service;
        }

        public async Task<IActionResult> Index(DashboardRequest request)
        {
            if (request.FromDate == default)
                request.FromDate = DateTime.Today.AddDays(-7);

            if (request.ToDate == default)
                request.ToDate = DateTime.Today;

            var staffIdClaim = User.FindFirst("StaffId")?.Value;

            if (string.IsNullOrEmpty(staffIdClaim))
                return Unauthorized();

            request.StaffId = int.Parse(staffIdClaim);

            var vm = await _service.GetDashboardAsync(request);

            ViewBag.Role = User.FindFirst(ClaimTypes.Role)?.Value;

            return View(vm);
        }
    }
}
