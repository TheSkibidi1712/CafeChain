using CafeChain.Application.DTOs.Admin.InventoryDocuments;
using CafeChain.Application.Interfaces.Admin.InventoryDocuments;
using CafeChain.ViewModels.Admin.InventoryDocuments;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CafeChain.Areas.Admin.Controllers
{
    public class AdminInventoryDocumentController : AdminBaseController
    {
        private readonly IAdminInventoryDocumentService _service;

        public AdminInventoryDocumentController(IAdminInventoryDocumentService service)
        {
            _service = service;
        }

        // ================= INDEX =================
        public async Task<IActionResult> Index(InventoryDocumentFilterDTO filter)
        {
            if (filter.Page <= 0) filter.Page = 1;
            if (filter.PageSize <= 0) filter.PageSize = 10;

            var result = await _service.GetPagedAsync(filter);
            return View(result);
        }
    }
}

