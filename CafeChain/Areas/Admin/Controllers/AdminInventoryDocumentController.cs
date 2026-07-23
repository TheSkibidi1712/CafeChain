using CafeChain.Application.DTOs.Admin.InventoryDocuments;
using CafeChain.Application.DTOs.Admin.InventoryDocuments.Index;
using CafeChain.Application.Interfaces.Admin.InventoryDocuments;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Application.DTOs.Admin.InventoryDocuments.Create;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace CafeChain.Areas.Admin.Controllers
{
    [Area("Admin")]
    [AutoValidateAntiforgeryToken]
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

            try
            {
                var vm = await _service.GetIndexDataAsync(filter);

                return View(vm);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
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
            if (type is not InventoryDocumentType.EXPORT
                and not InventoryDocumentType.STOCK_TAKE
                and not InventoryDocumentType.WASTE)
            {
                return BadRequest("Loại phiếu này không được phép tạo trực tiếp từ màn hình Phiếu Kho.");
            }

            var vm =
                await _serviceCreate.GetCreateDataAsync(type);

            return PartialView("Partials/Create/_CreateModal", vm);
        }

        // =====================================================
        // AJAX
        // LOAD INGREDIENT
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> Suppliers(int storeId)
        {
            var data = await _serviceCreate.GetSuppliersAsync(storeId);

            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> SupplierIngredients(int supplierId, int storeId)
        {
            var data = await _serviceCreate.GetSupplierIngredientsAsync(supplierId, storeId);

            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> ActiveIngredients(int storeId, InventoryDocumentPurpose purpose = InventoryDocumentPurpose.NONE)
        {
            var data = await _serviceCreate.GetActiveIngredientsAsync(storeId, purpose);

            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> StoreInventoryIngredients(
            int storeId,
            InventoryDocumentType type,
            InventoryDocumentPurpose purpose = InventoryDocumentPurpose.NONE)
        {
            try
            {
                var data = await _serviceCreate.GetStoreInventoryIngredientsAsync(storeId, type, purpose);

                return Json(data);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpGet]
        public IActionResult PendingInternalTransfers(int storeId)
        {
            return StatusCode(
                StatusCodes.Status410Gone,
                new
                {
                    success = false,
                    message = "Chuyển kho liên chi nhánh đã được tách sang Phiếu Chuyển Kho."
                });
        }

        [HttpGet]
        public IActionResult InternalTransferIngredients(int transferId)
        {
            return StatusCode(
                StatusCodes.Status410Gone,
                new
                {
                    success = false,
                    message = "Nhập nội bộ không còn xử lý bằng InventoryDocument."
                });
        }

        // =====================================================
        // AJAX
        // CALCULATE
        // =====================================================

        [HttpPost]
        public async Task<IActionResult> Preflight([FromBody] CreateInventoryDocumentDTO dto)
        {
            try
            {
                return Json(new { success = true, preflight = await _serviceCreate.PreflightAsync(dto) });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return UnprocessableEntity(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Calculate([FromBody] CreateInventoryDocumentDTO dto)
        {
            try
            {
                var result = await _serviceCreate.CalculateSummaryAsync(dto);

                return Json(result);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Invalid inventory document calculate request.");

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
                    "Failed to calculate inventory document summary.");

                return BadRequest(new
                {
                    success = false,
                    message = "Không thể tính tổng phiếu kho."
                });
            }
        }

        // =====================================================
        // SAVE DRAFT
        // =====================================================

        [HttpPost]
        public async Task<IActionResult> SaveDraft([FromBody] CreateInventoryDocumentDTO dto)
        {
            try
            {
                var id = await _serviceCreate.SaveDraftAsync(dto);

                return Json(new
                {
                    success = true,
                    id
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Invalid inventory document draft request.");

                return MutationFailure(ex);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrency conflict while saving inventory document draft.");
                return Conflict(new { success = false, message = "CONCURRENCY_CONFLICT" });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(
                    ex,
                    "Database error while saving inventory document draft.");

                return BadRequest(
                    ErrorResponse(
                        "Không thể lưu nháp phiếu kho do dữ liệu chưa phù hợp."));
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to save inventory document draft.");

                return BadRequest(new
                {
                    success = false,
                    message = "Không thể lưu nháp phiếu kho."
                });
            }
        }

        // =====================================================
        // SUBMIT
        // =====================================================

        [HttpPost]
        public async Task<IActionResult> Submit([FromBody] CreateInventoryDocumentDTO dto)
        {
            try
            {
                var result = await _serviceCreate.CreateAndConfirmAsync(dto);

                return Json(new
                {
                    success = true,
                    id = result.DocumentId,
                    status = result.Status.ToString(),
                    approvalId = result.ApprovalId,
                    warnings = result.Warnings
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Invalid create and confirm inventory document request.");

                return MutationFailure(ex);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrency conflict while submitting inventory document.");
                return Conflict(new { success = false, message = "CONCURRENCY_CONFLICT" });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(
                    ex,
                    "Database error while creating and confirming inventory document.");

                return BadRequest(
                    ErrorResponse(
                        "Không thể tạo và xác nhận phiếu do dữ liệu chưa phù hợp."));
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
                if (documentId <= 0)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Mã phiếu không hợp lệ."
                    });
                }

                if (string.IsNullOrWhiteSpace(requestKey))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "RequestKey là bắt buộc."
                    });
                }

                var result = await _serviceCreate.ConfirmDraftAsync(documentId, requestKey);

                if (result == null)
                {
                    _logger.LogWarning(
                        "Inventory document draft {DocumentId} was not found when confirming.",
                        documentId);

                    return NotFound(new
                    {
                        success = false,
                        message = "Không tìm thấy phiếu cần xác nhận."
                    });
                }

                return Json(new
                {
                    success = true,
                    id = result.DocumentId,
                    status = result.Status.ToString(),
                    approvalId = result.ApprovalId,
                    warnings = result.Warnings
                });
            }
            catch (InvalidOperationException ex)
            {
                return MutationFailure(ex);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new { success = false, message = "CONCURRENCY_CONFLICT" });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(
                    ex,
                    "Database error while confirming inventory document draft {DocumentId}.",
                    documentId);

                return BadRequest(
                    ErrorResponse(
                        "Không thể xác nhận phiếu do dữ liệu tồn kho chưa phù hợp."));
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
        public async Task<IActionResult> ApproveNegative(int id, [FromBody] InventoryNegativeReviewDTO dto)
        {
            try
            {
                return Json(new { success = true, document = await _serviceCreate.ApproveNegativeAsync(id, dto.ReviewNote) });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return MutationFailure(ex);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new { success = false, message = "CONCURRENCY_CONFLICT" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> RejectNegative(int id, [FromBody] InventoryNegativeReviewDTO dto)
        {
            try
            {
                return Json(new { success = true, document = await _serviceCreate.RejectNegativeAsync(id, dto.ReviewNote ?? string.Empty) });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return MutationFailure(ex);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new { success = false, message = "CONCURRENCY_CONFLICT" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CancelInventoryDocument(int documentId, string? requestKey)
        {
            try
            {
                if (documentId <= 0)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Mã phiếu không hợp lệ."
                    });
                }

                if (string.IsNullOrWhiteSpace(requestKey))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "RequestKey là bắt buộc."
                    });
                }

                var success = await _serviceCreate.CancelInventoryDocumentAsync(documentId, requestKey);

                if (!success)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Không tìm thấy phiếu cần hủy."
                    });
                }

                return Json(new
                {
                    success = true,
                    id = documentId
                });
            }
            catch (InvalidOperationException ex)
            {
                return MutationFailure(ex);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new { success = false, message = "CONCURRENCY_CONFLICT" });
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
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
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

        [HttpGet]
        public async Task<IActionResult> ExportFile(int id, InventoryDocumentExportType exportType)
        {
            var dto = new ExportInventoryDocumentDTO
            {
                DocumentId = id,
                ExportType = exportType
            };
            var file = await _service.ExportFileAsync(dto);
            if (file == null)
                return UnprocessableEntity("Phiếu chưa CONFIRMED hoặc không có confirmed snapshot.");

            var isPdf = exportType == InventoryDocumentExportType.PDF;
            return File(
                file,
                isPdf
                    ? "application/pdf"
                    : "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                $"InventoryDocument_{id}.{(isPdf ? "pdf" : "docx")}");
        }

        private object ErrorResponse(string message)
        {
            var traceId =
                Activity.Current?.Id ??
                HttpContext.TraceIdentifier;

            return new
            {
                success = false,
                message,
                traceId
            };
        }

        private IActionResult MutationFailure(InvalidOperationException exception)
        {
            var message = exception.Message;
            if (message.Contains("IDEMPOTENCY_KEY_REUSED", StringComparison.Ordinal)
                || message.Contains("CONCURRENCY", StringComparison.Ordinal)
                || message.Contains("ROW_VERSION", StringComparison.Ordinal)
                || message.Contains("STALE", StringComparison.Ordinal)
                || message.Contains("REQUEST_IN_PROGRESS", StringComparison.Ordinal)
                || message.Contains("REQUEST_PREVIOUSLY_FAILED", StringComparison.Ordinal)
                || message.Contains("REQUEST_EXPIRED", StringComparison.Ordinal)
                || message.Contains("REQUEST_KEY_UNAVAILABLE", StringComparison.Ordinal))
            {
                return Conflict(new { success = false, message });
            }

            if (message.Contains("REQUIRED", StringComparison.OrdinalIgnoreCase)
                || message.Contains("INVALID", StringComparison.OrdinalIgnoreCase)
                || message.Contains("bắt buộc", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { success = false, message });
            }

            return UnprocessableEntity(new { success = false, message });
        }

    }
}

