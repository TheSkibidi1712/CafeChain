using CafeChain.Application.DTOs.Admin.InventoryTransfers;
using CafeChain.Application.Interfaces.Admin.InventoryTransfers;
using CafeChain.ViewModels.Admin.InventoryTransfers;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers
{
    public class AdminInventoryTransferController : AdminBaseController
    {
        private readonly IAdminInventoryTransferService _service;
        private readonly ILogger<AdminInventoryTransferController> _logger;

        public AdminInventoryTransferController(
            IAdminInventoryTransferService service,
            ILogger<AdminInventoryTransferController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(AdminInventoryTransferIndexVM filter)
        {
            var vm = await _service.GetIndexAsync(filter);

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var vm = await _service.GetCreateDataAsync();

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            var vm = await _service.GetDetailAsync(id);

            if (vm == null)
            {
                return NotFound();
            }

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Ingredients(int fromStoreId)
        {
            try
            {
                var data = await _service.GetTransferIngredientsAsync(fromStoreId);

                return Json(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load transfer ingredients from store {StoreId}.", fromStoreId);

                return BadRequest(new
                {
                    success = false,
                    message = "Không tải được danh sách nguyên liệu chuyển kho."
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ValidateStock([FromBody] InventoryTransferMutationDTO dto)
        {
            try
            {
                var warnings = await _service.ValidateStockAsync(dto);

                return Json(new
                {
                    success = true,
                    warnings
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
                _logger.LogError(ex, "Failed to validate inventory transfer stock.");

                return BadRequest(new
                {
                    success = false,
                    message = "Không kiểm tra được tồn kho chuyển."
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateDraft([FromBody] InventoryTransferMutationDTO dto)
        {
            try
            {
                var result = await _service.CreateDraftAsync(dto);

                return Json(new
                {
                    success = true,
                    transfer = result
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
                _logger.LogError(ex, "Failed to create inventory transfer draft.");

                return BadRequest(new
                {
                    success = false,
                    message = "Không thể lưu nháp phiếu chuyển kho."
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateDraft(
            int id,
            [FromBody] InventoryTransferMutationDTO dto)
        {
            try
            {
                var result = await _service.UpdateDraftAsync(id, dto);

                return Json(new
                {
                    success = true,
                    transfer = result
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
                _logger.LogError(ex, "Failed to update inventory transfer draft {TransferId}.", id);

                return BadRequest(new
                {
                    success = false,
                    message = "Không thể cập nhật phiếu chuyển kho."
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Confirm(int id, string? requestKey)
        {
            try
            {
                if (id <= 0)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Mã phiếu chuyển kho không hợp lệ."
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

                var result = await _service.ConfirmAsync(id, requestKey);

                return Json(new
                {
                    success = true,
                    transfer = result
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
                _logger.LogError(ex, "Failed to confirm inventory transfer {TransferId}.", id);

                return BadRequest(new
                {
                    success = false,
                    message = "Không thể xác nhận phiếu chuyển kho."
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Cancel(int id, string? requestKey)
        {
            try
            {
                if (id <= 0)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Mã phiếu chuyển kho không hợp lệ."
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

                var success = await _service.CancelAsync(id, requestKey);

                if (!success)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Không tìm thấy phiếu chuyển kho."
                    });
                }

                return Json(new
                {
                    success = true,
                    id
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
                _logger.LogError(ex, "Failed to cancel inventory transfer {TransferId}.", id);

                return BadRequest(new
                {
                    success = false,
                    message = "Không thể hủy phiếu chuyển kho."
                });
            }
        }
    }
}
