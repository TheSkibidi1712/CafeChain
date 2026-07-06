using CafeChain.Application.DTOs.Admin.InventoryDocuments;
using CafeChain.Application.DTOs.Admin.InventoryDocuments.Index;
using CafeChain.Application.Interfaces.Admin.InventoryDocuments;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Application.DTOs.Admin.InventoryDocuments.Create;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AdminInventoryDocumentController : AdminBaseController
    {
        private readonly IAdminInventoryDocumentService _service;
        private readonly IAdminInventoryDocumentCreateService _serviceCreate;
        private readonly ILogger<AdminInventoryDocumentController> _logger;


        public AdminInventoryDocumentController(
            IAdminInventoryDocumentService service,
            IAdminInventoryDocumentCreateService serviceCreate,
            ILogger<AdminInventoryDocumentController> logger)
        {
            _service = service;
            _serviceCreate = serviceCreate;
            _logger = logger;
        }

        // =====================================================
        // INDEX
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> Index(AdminInventoryDocumentFilterDTO filter)
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

            return PartialView("Partials/Detail/_DetailModal", vm);
        }

        // =====================================================
        // CREATE MODAL
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> CreateModal(InventoryDocumentType type)
        {
            var vm =
                await _serviceCreate.GetCreateDataAsync(type);

            return PartialView("Partials/Create/_CreateModal", vm);
        }

        // =====================================================
        // AJAX
        // LOAD INGREDIENT
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> SupplierIngredients(int supplierId)
        {
            var data = await _serviceCreate.GetSupplierIngredientsAsync(supplierId);

            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> ActiveIngredients(int storeId, InventoryDocumentPurpose purpose = InventoryDocumentPurpose.NONE)
        {
            var data = await _serviceCreate.GetActiveIngredientsAsync(storeId, purpose);

            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> StoreExportIngredients(int storeId)
        {
            var data = await _serviceCreate.GetStoreExportIngredientsAsync(storeId);

            return Json(data);
        }

        // =====================================================
        // AJAX
        // CALCULATE
        // =====================================================

        [HttpPost]
        public async Task<IActionResult> Calculate([FromBody] CreateInventoryDocumentDTO dto)
        {
            var result = await _serviceCreate.CalculateSummaryAsync(dto);

            return Json(result);
        }

        // =====================================================
        // SAVE DRAFT
        // =====================================================

        [HttpPost]
        public async Task<IActionResult> SaveDraft([FromBody] CreateInventoryDocumentDTO dto)
        {
            var id = await _serviceCreate.SaveDraftAsync(dto);

            return Json(new
            {
                success = true,
                id
            });
        }

        // =====================================================
        // CREATE
        // =====================================================

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateInventoryDocumentDTO dto)
        {
            try
            {
                var result = await _serviceCreate.CreateAndConfirmAsync(dto);

                return Json(new
                {
                    success = true,
                    id = result.DocumentId,
                    warnings = result.Warnings
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to create and confirm inventory document.");

                return BadRequest(new
                {
                    success = false,
                    message = "Không thể tạo và xác nhận phiếu."
                });
            }
        }

        // =====================================================
        // CONFIRM DRAFT
        // =====================================================

        [HttpPost]
        public async Task<IActionResult> ConfirmDraft(int documentId, string? requestKey)
        {
            try
            {
                var result = await _serviceCreate.ConfirmDraftAsync(documentId, requestKey);

                if (result == null)
                {
                    return NotFound();
                }

                return Json(new
                {
                    success = true,
                    id = result.DocumentId,
                    warnings = result.Warnings
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to confirm inventory document draft {DocumentId}.",
                    documentId);

                return BadRequest(new
                {
                    success = false,
                    message = "Không thể xác nhận phiếu."
                });
            }
        }

        // =====================================================
        // CANCEL
        // =====================================================

        [HttpPost]
        public async Task<IActionResult> CancelInventoryDocument(int documentId, string? requestKey)
        {
            try
            {
                var success = await _serviceCreate.CancelInventoryDocumentAsync(documentId, requestKey);

                if (!success)
                {
                    return NotFound();
                }

                return Json(new
                {
                    success = true,
                    id = documentId
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Không thể hủy phiếu."
                });
            }
        }

        // =====================================================
        // EXPORT FILE
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> ExportExcel(AdminInventoryDocumentFilterDTO filter)
        {
            try
            {
                var file = await _service.ExportExcelAsync(filter);

                const string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

                var fileName = $"PhieuKho_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";

                return File(file, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export inventory documents to Excel.");

                return StatusCode(500, "Không thể xuất Excel phiếu kho.");
            }
        }

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

