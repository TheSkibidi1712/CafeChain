using CafeChain.Application.DTOs.Admin.InventoryDocuments;
using CafeChain.Application.Interfaces.Admin.InventoryDocuments;
using CafeChain.Models.Enums.Inventory;
using CafeChain.ViewModels.Admin.InventoryDocuments;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CafeChain.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AdminInventoryDocumentController : AdminBaseController
    {
        private readonly IAdminInventoryDocumentService _service;

        public AdminInventoryDocumentController(IAdminInventoryDocumentService service)
        {
            _service = service;
        }

        // =====================================================
        // INDEX
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> Index(
            AdminInventoryDocumentFilterDTO filter)
        {
            filter.Page = filter.Page <= 0 ? 1 : filter.Page;

            filter.PageSize = filter.PageSize <= 0 ? 20 : filter.PageSize;

            var vm = await _service.GetIndexDataAsync(filter);

            return View(vm);
        }


        // =====================================================
        // DETAIL MODAL
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> DetailModal(int documentId)
        {
            var vm = await _service.GetDetailAsync(documentId);

            if (vm == null)
            {
                return NotFound();
            }

            return PartialView("Partials/_DetailModal", vm);
        }

        // =====================================================
        // EXPORT FILE
        // =====================================================

        [HttpPost]
        public async Task<IActionResult> ExportFile([FromBody] ExportInventoryDocumentDTO dto)
        {
            var file = await _service.ExportFileAsync(dto);

            if (file == null)
            {
                return BadRequest("Phiếu chưa xác nhận hoặc không có snapshot.");
            }

            var contentType = dto.ExportType == InventoryDocumentExportType.PDF ? "application/pdf" : "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

            var extension = dto.ExportType == InventoryDocumentExportType.PDF ? "pdf" : "docx";

            return File(file, contentType, $"InventoryDocument_{dto.DocumentId}.{extension}");
        }

    }
}

