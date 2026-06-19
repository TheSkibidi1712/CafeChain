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

            var staffIdClaim = User.FindFirst("StaffId")?.Value;

            if (string.IsNullOrEmpty(staffIdClaim))
                return Unauthorized();

            request.StaffId = int.Parse(staffIdClaim);

            var vm = await _service.GetDashboardAsync(request);

            ViewBag.Role = User.FindFirst(ClaimTypes.Role)?.Value;

            return View(vm);
        }

        // Get D
        [HttpGet]
        public async Task<IActionResult> GetData([FromQuery] DashboardRequest request)
        {
            var staffIdClaim = User.FindFirst("StaffId")?.Value;

            if (string.IsNullOrEmpty(staffIdClaim))
                return Unauthorized();

            request.StaffId = int.Parse(staffIdClaim);

            var data = await _service.GetDashboardAsync(request);

            return Json(data);
        }
    }
}
