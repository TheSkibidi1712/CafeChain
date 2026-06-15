using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = "RequireAdminPanelAccess")]
    public abstract class AdminBaseController : Controller
    {
        // Controller cha - Nơi chứa các logic dùng chung cho toàn bộ Admin Area
        // Các Controller con chỉ cần kế thừa AdminBaseController, không cần viết lại [Area] hay [Authorize]
    }
}
