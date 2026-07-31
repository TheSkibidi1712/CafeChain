using CafeChain.Application.Constants;
using CafeChain.Application.Authorization;
using CafeChain.Application.DTOs.Admin.InventoryTransfers;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Admin.InventoryTransfers;
using CafeChain.Application.Interfaces.Security;
using CafeChain.ViewModels.Admin.InventoryTransfers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Areas.Admin.Controllers
{
    [AutoValidateAntiforgeryToken]
    [RequirePermission(PermissionConstants.InventoryTransferView)]
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
        [RequirePermission(PermissionConstants.InventoryTransferCreateDraft)]
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

        [RequirePermission(PermissionConstants.InventoryTransferCreateDraft)]
        [HttpPost]
        public async Task<IActionResult> Preflight([FromBody] InventoryTransferMutationDTO dto)
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
                return MutationFailure(ex);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
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

        [RequirePermission(PermissionConstants.InventoryTransferCreateDraft)]
        [HttpPost]
        public async Task<IActionResult> SaveDraft([FromBody] InventoryTransferMutationDTO dto)
        {
            try
            {
                if (!await CanMutateTransferAsync(dto.FromStoreId, dto.ToStoreId))
                    return Forbid();
                var result = dto.TransferId.HasValue
                    ? await _service.UpdateDraftAsync(dto.TransferId.Value, dto)
                    : await _service.CreateDraftAsync(dto);

                return Json(new
                {
                    success = true,
                    transfer = result
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
                return Conflict(new
                {
                    success = false,
                    code = BranchReceiptErrorCodes.ResourceChanged,
                    message = "Dữ liệu đã được người khác cập nhật. Vui lòng tải lại.",
                    traceId = HttpContext.TraceIdentifier
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

        [RequirePermission(PermissionConstants.InventoryTransferUpdateDraft)]
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
                return MutationFailure(ex);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (DbUpdateConcurrencyException)
            {
                return ResourceChangedFailure();
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

        [RequirePermission(PermissionConstants.InventoryTransferDispatch)]
        [HttpPost]
        public async Task<IActionResult> Dispatch(int id, string? requestKey)
        {
            if (id <= 0 || string.IsNullOrWhiteSpace(requestKey))
                return BadRequest(new { success = false, message = "Id và RequestKey là bắt buộc." });

            var current = await _service.GetDetailAsync(id);
            if (current == null)
                return NotFound();
            if (!await CanMutateTransferAsync(current.FromStoreId, current.ToStoreId))
                return Forbid();

            try
            {
                return Json(new { success = true, transfer = await _service.DispatchAsync(id, requestKey) });
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
                return ResourceChangedFailure();
            }
        }

        [RequirePermission(PermissionConstants.InventoryTransferReceive)]
        [HttpPost]
        public async Task<IActionResult> Receive(int id, [FromBody] InventoryTransferReceiveDTO dto)
        {
            if (id <= 0 || string.IsNullOrWhiteSpace(dto.RequestKey))
                return BadRequest(new { success = false, message = "Id và RequestKey là bắt buộc." });

            var current = await _service.GetDetailAsync(id);
            if (current == null)
                return NotFound();
            if (!await CanOperateStoreAsync(current.ToStoreId))
                return Forbid();

            try
            {
                return Json(new { success = true, transfer = await _service.ReceiveAsync(id, dto) });
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
                return ResourceChangedFailure();
            }
        }

        [RequirePermission(PermissionConstants.InventoryTransferRequestReturn)]
        [HttpPost]
        public async Task<IActionResult> RequestReturn(int id, [FromBody] InventoryTransferResolutionDTO dto)
        {
            var current = await _service.GetDetailAsync(id);
            if (current == null)
                return NotFound();
            if (!await CanOperateStoreAsync(current.ToStoreId))
                return Forbid();
            return await ExecuteMutationAsync(
                () => _service.RequestReturnAsync(id, dto),
                "Không thể tạo yêu cầu hoàn trả.");
        }

        [RequirePermission(PermissionConstants.InventoryTransferConfirmReturn)]
        [HttpPost]
        public async Task<IActionResult> ConfirmReturn(int id, [FromBody] InventoryTransferResolutionDTO dto)
        {
            var current = await _service.GetDetailAsync(id);
            if (current == null)
                return NotFound();
            if (!await CanOperateStoreAsync(current.FromStoreId))
                return Forbid();
            return await ExecuteMutationAsync(
                () => _service.ConfirmReturnAsync(id, dto),
                "Không thể xác nhận hàng hoàn trả.");
        }

        [RequirePermission(PermissionConstants.InventoryTransferResolveDiscrepancy)]
        [HttpPost]
        public async Task<IActionResult> ResolveShortage(int id, [FromBody] InventoryTransferResolutionDTO dto)
        {
            if (!User.IsInRole(RoleConstants.BusinessOwner)
                && !User.IsInRole(RoleConstants.SystemAdmin))
                return Forbid();
            return await ExecuteMutationAsync(
                () => _service.ResolveShortageAsync(id, dto),
                "Không thể đóng phần thiếu.");
        }

        [RequirePermission(PermissionConstants.InventoryTransferResolveDiscrepancy)]
        [HttpPost]
        public async Task<IActionResult> CreateFollowUp(int id, [FromBody] InventoryTransferFollowUpDTO dto)
        {
            if (!CanMutateTransfers())
                return Forbid();
            return await ExecuteMutationAsync(
                () => _service.CreateFollowUpAsync(id, dto),
                "Không thể tạo phiếu gửi bù.");
        }

        [HttpGet]
        public async Task<IActionResult> DiscrepancyDryRun()
        {
            if (!CanMutateTransfers())
                return Forbid();
            return Json(new
            {
                success = true,
                dryRun = true,
                rows = await _service.GetDiscrepancyDryRunAsync()
            });
        }

        [RequirePermission(PermissionConstants.InventoryTransferCancel)]
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
                return MutationFailure(ex);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (DbUpdateConcurrencyException)
            {
                return ResourceChangedFailure();
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
            || User.IsInRole(RoleConstants.AccountantWarehouse)
            || User.IsInRole(RoleConstants.SystemAdmin);

        private bool CanMutateTransfers() =>
            User.IsInRole(RoleConstants.BusinessOwner)
            || User.IsInRole(RoleConstants.AccountantWarehouse)
            || User.IsInRole(RoleConstants.SystemAdmin);

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

        private async Task<bool> CanOperateStoreAsync(int storeId)
        {
            if (storeId <= 0)
                return false;
            if (User.IsInRole(RoleConstants.BusinessOwner)
                || User.IsInRole(RoleConstants.AccountantWarehouse)
                || User.IsInRole(RoleConstants.SystemAdmin))
                return true;
            if (!User.IsInRole(RoleConstants.StoreManager)
                && !User.IsInRole(RoleConstants.ShiftSupervisor))
                return false;
            var context = _actor.Get(User);
            return await _scopeAuthorization.CanAccessStoreAsync(context.StaffId, storeId);
        }

        private async Task<bool> CanMutateTransferAsync(int fromStoreId, int toStoreId) =>
            fromStoreId > 0
            && toStoreId > 0
            && await CanMutateStoreAsync(fromStoreId)
            && await CanMutateStoreAsync(toStoreId);

        private IActionResult MutationFailure(InvalidOperationException exception)
        {
            var message = exception.Message;
            if (message == BranchReceiptErrorCodes.ResourceChanged)
            {
                return Conflict(new
                {
                    success = false,
                    code = BranchReceiptErrorCodes.ResourceChanged,
                    message = "Dữ liệu đã được người khác cập nhật. Vui lòng tải lại.",
                    traceId = HttpContext.TraceIdentifier
                });
            }
            if (message == BranchReceiptErrorCodes.ValidationRowVersionRequired)
            {
                return BadRequest(new
                {
                    success = false,
                    code = BranchReceiptErrorCodes.ValidationRowVersionRequired,
                    message = "Thiếu phiên bản dữ liệu. Vui lòng tải lại.",
                    traceId = HttpContext.TraceIdentifier
                });
            }
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

        private IActionResult ResourceChangedFailure() =>
            Conflict(new
            {
                success = false,
                code = BranchReceiptErrorCodes.ResourceChanged,
                message = "Dữ liệu đã được người khác cập nhật. Vui lòng tải lại.",
                traceId = HttpContext.TraceIdentifier
            });

        private async Task<IActionResult> ExecuteMutationAsync(
            Func<Task<InventoryTransferMutationResultDTO>> mutation,
            string fallbackMessage)
        {
            try
            {
                return Json(new { success = true, transfer = await mutation() });
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
                return ResourceChangedFailure();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Inventory transfer discrepancy mutation failed.");
                return BadRequest(new { success = false, message = fallbackMessage });
            }
        }
    }
}
