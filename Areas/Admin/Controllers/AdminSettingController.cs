using System.Collections.Generic;
using System.Threading.Tasks;
using CafeChain.Application.Interfaces.Admin.Settings;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AdminSettingController : AdminBaseController
    {
        private readonly IAdminSettingService _settingService;

        public AdminSettingController(IAdminSettingService settingService)
        {
            _settingService = settingService;
        }

        public async Task<IActionResult> Index()
        {
            var dict = await _settingService.GetSettingsDictionaryAsync();
            return View(dict);
        }

        [HttpPost]
        public async Task<IActionResult> Update(Dictionary<string, string> Settings)
        {
            var res = await _settingService.SaveSettingsAsync(Settings);
            if (!res.IsSuccess)
            {
                TempData["ErrorMessage"] = res.Message;
            }
            else
            {
                TempData["SuccessMessage"] = res.Message;
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
