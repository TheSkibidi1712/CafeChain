using CafeChain.Application.DTOs.Admin.InventoryDocuments;
using CafeChain.Application.Interfaces.Admin.InventoryDocuments;
using CafeChain.Models.Enums.Inventory;
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
            if (filter.Page <= 0)
                filter.Page = 1;

            filter.PageSize = 10;

            // mặc định mở tab nhập kho
            if (!filter.Type.HasValue)
            {
                filter.Type = InventoryDocumentType.IMPORT;
            }
            
            var result = await _service.GetPagedAsync(filter);

            ViewBag.Keyword = filter.Keyword;
            ViewBag.Type = filter.Type;
            ViewBag.FromDate = filter.FromDate;
            ViewBag.ToDate = filter.ToDate;
            ViewBag.CurrentPage = filter.Page;
            ViewBag.PageSize = 10;
            ViewBag.TotalPages = (int)Math.Ceiling(
                (double)result.TotalRecords / 10
            );

            return View(result);
        }
    }
}

