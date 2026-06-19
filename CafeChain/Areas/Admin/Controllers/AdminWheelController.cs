using CafeChain.Application.Services.Admin.Vouchers;
using CafeChain.Models.Vouchers;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AdminWheelController : Controller
    {
        private readonly IAdminWheelService _wheelService;

        public AdminWheelController(IAdminWheelService wheelService)
        {
            _wheelService = wheelService;
        }

        public async Task<IActionResult> Index()
        {
            var config = await _wheelService.GetActiveConfigAsync();
            if (config == null)
            {
                var configs = await _wheelService.GetAllConfigsAsync();
                config = configs.FirstOrDefault();
            }

            // Nếu chưa có bất kỳ vòng quay nào, tạo cái mặc định
            if (config == null)
            {
                var newConfig = new WheelConfig
                {
                    Name = "Vòng quay may mắn",
                    SpinCost = 1000,
                    SlotCount = 8,
                    Active = true,
                    CreatedAt = DateTime.Now
                };
                await _wheelService.CreateConfigAsync(newConfig);
                config = newConfig;
            }

            ViewBag.Vouchers = await _wheelService.GetAvailableVouchersAsync();
            return View(config);
        }

        [HttpPost]
        public async Task<IActionResult> Create(int WheelConfigId, string Name, int SpinCost, int SlotCount, bool Active)
        {
            if (WheelConfigId > 0)
            {
                var existing = await _wheelService.GetConfigByIdAsync(WheelConfigId);
                if (existing != null)
                {
                    existing.Name = Name;
                    existing.SpinCost = SpinCost;
                    existing.SlotCount = SlotCount;
                    existing.Active = Active;
                    await _wheelService.UpdateConfigAsync(existing);
                    TempData["Success"] = "Đã cập nhật cấu hình vòng quay!";
                }
            }
            else
            {
                var newConfig = new WheelConfig
                {
                    Name = Name,
                    SpinCost = SpinCost,
                    SlotCount = SlotCount,
                    Active = Active,
                    CreatedAt = DateTime.Now
                };
                await _wheelService.CreateConfigAsync(newConfig);
                TempData["Success"] = "Đã tạo vòng quay mới!";
            }
            
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var success = await _wheelService.ToggleStatusAsync(id);
            return Json(new { success });
        }

        [HttpGet]
        public async Task<IActionResult> GetConfigJson(int id)
        {
            var config = await _wheelService.GetConfigByIdAsync(id);
            if (config == null) return NotFound();

            return Json(new
            {
                config.WheelConfigId,
                config.Name,
                config.SpinCost,
                config.SlotCount,
                config.Active,
                prizes = config.Prizes.Select(p => new
                {
                    p.SlotIndex,
                    p.VoucherId,
                    p.Probability,
                    p.IsLose,
                    voucherCode = p.Voucher?.Code
                })
            });
        }

        [HttpPost]
        public async Task<IActionResult> SavePrizes(int WheelConfigId, List<WheelPrize> Prizes)
        {
            var success = await _wheelService.SavePrizesAsync(WheelConfigId, Prizes);
            return Json(new { success });
        }
    }
}
