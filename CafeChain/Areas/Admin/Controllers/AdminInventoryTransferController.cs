using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.InventoryTransfers;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Admin.InventoryTransfers;
using CafeChain.Application.Interfaces.Security;
using CafeChain.ViewModels.Admin.InventoryTransfers;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers
{
    public class AdminInventoryTransferController : AdminBaseController
    {
        private readonly IAdminInventoryTransferService _service;
        private readonly IAdminActorContextAccessor _actor;
        private readonly IScopeAuthorizationService _scopeAuthorization;
        private readonly ILogger<AdminInventoryTransferController> _logger;

        public AdminInventoryTransferController(
            IAdminInventoryTransferService service,
            IAdminActorContextAccessor actor,
            IScopeAuthorizationService scopeAuthorization,
            ILogger<AdminInventoryTransferController> logger)
        {
            _service = service;
            _actor = actor;
            _scopeAuthorization = scopeAuthorization;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(AdminInventoryTransferIndexVM filter)
        {
            var allowedStoreIds = await ResolveReadStoreScopeAsync();
            if (allowedStoreIds is { Count: 0 })
                return Forbid();

            var vm = await _service.GetIndexAsync(filter, allowedStoreIds);

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            if (!CanMutateTransfers())
                return Forbid();
            var allowedStoreIds = await ResolveMutationStoreScopeAsync();
            if (allowedStoreIds is { Count: 0 })
                return Forbid();

            var vm = await _service.GetCreateDataAsync(allowedStoreIds);

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

            if (!await CanReadTransferAsync(vm.FromStoreId, vm.ToStoreId))
                return Forbid();

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Items(int fromStoreId)
        {
            try
            {
                if (!await CanMutateStoreAsync(fromStoreId))
                    return Forbid();
                var data = await _service.GetTransferItemsAsync(fromStoreId);

                return Json(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load transfer items from store {StoreId}.", fromStoreId);

                return BadRequest(new
                {
                    success = false,
                    message = "Không tải được danh sách hàng hóa chuyển kho."
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ValidateStock([FromBody] InventoryTransferMutationDTO dto)
        {
            try
            {
                if (!await CanMutateTransferAsync(dto.FromStoreId, dto.ToStoreId))
                    return Forbid();
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
                if (!await CanMutateTransferAsync(dto.FromStoreId, dto.ToStoreId))
                    return Forbid();
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
                var current = await _service.GetDetailAsync(id);
                if (current == null)
                    return NotFound();
                if (!await CanMutateTransferAsync(current.FromStoreId, current.ToStoreId)
                    || !await CanMutateTransferAsync(dto.FromStoreId, dto.ToStoreId))
                    return Forbid();
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

                var current = await _service.GetDetailAsync(id);
                if (current == null)
                    return NotFound();
                if (!await CanMutateTransferAsync(current.FromStoreId, current.ToStoreId))
                    return Forbid();

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

                var current = await _service.GetDetailAsync(id);
                if (current == null)
                    return NotFound();
                if (!await CanMutateTransferAsync(current.FromStoreId, current.ToStoreId))
                    return Forbid();

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

        private bool IsGlobalDocumentRole() =>
            User.IsInRole(RoleConstants.BusinessOwner)
            || User.IsInRole(RoleConstants.AccountantWarehouse);

        private bool CanMutateTransfers() =>
            IsGlobalDocumentRole() || User.IsInRole(RoleConstants.AreaManager);

        private async Task<List<int>?> ResolveReadStoreScopeAsync()
        {
            if (IsGlobalDocumentRole())
                return null;

            var context = _actor.Get(User);
            if (User.IsInRole(RoleConstants.AreaManager))
                return (await _scopeAuthorization.GetAllowedStoresAsync(context.StaffId))
                    .Select(x => x.StoreId)
                    .Distinct()
                    .ToList();

            if (User.IsInRole(RoleConstants.StoreManager)
                || User.IsInRole(RoleConstants.ShiftSupervisor)
                || User.IsInRole(RoleConstants.SalesStaff))
                return context.StoreId > 0 ? new List<int> { context.StoreId } : [];

            return [];
        }

        private async Task<List<int>?> ResolveMutationStoreScopeAsync()
        {
            if (IsGlobalDocumentRole())
                return null;
            if (!User.IsInRole(RoleConstants.AreaManager))
                return [];

            var context = _actor.Get(User);
            return (await _scopeAuthorization.GetAllowedStoresAsync(context.StaffId))
                .Select(x => x.StoreId)
                .Distinct()
                .ToList();
        }

        private async Task<bool> CanReadTransferAsync(int fromStoreId, int toStoreId)
        {
            var allowedStoreIds = await ResolveReadStoreScopeAsync();
            return allowedStoreIds == null
                   || allowedStoreIds.Contains(fromStoreId)
                   || allowedStoreIds.Contains(toStoreId);
        }

        private async Task<bool> CanMutateStoreAsync(int storeId)
        {
            if (!CanMutateTransfers() || storeId <= 0)
                return false;
            if (IsGlobalDocumentRole())
                return true;

            var context = _actor.Get(User);
            return await _scopeAuthorization.CanAccessStoreAsync(context.StaffId, storeId);
        }

        private async Task<bool> CanMutateTransferAsync(int fromStoreId, int toStoreId) =>
            fromStoreId > 0
            && toStoreId > 0
            && await CanMutateStoreAsync(fromStoreId)
            && await CanMutateStoreAsync(toStoreId);
    }
}
