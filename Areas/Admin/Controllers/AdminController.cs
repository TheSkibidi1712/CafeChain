using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers
{
    public class AdminController : AdminBaseController
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}

