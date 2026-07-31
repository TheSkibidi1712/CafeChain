using CafeChain.Application.Constants;
using CafeChain.Application.Authorization;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Inventories;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers;

[Area("Admin")]
[RequirePermission(PermissionConstants.PurchaseOrderViewBatch)]
public sealed class AdminPurchaseOrderBatchesController : Controller
{
    private readonly IPurchaseOrderBatchService _service;
    private readonly IPurchaseOrderBatchDocumentService _documentService;
    private readonly IAdminActorContextAccessor _actorAccessor;

    public AdminPurchaseOrderBatchesController(
        IPurchaseOrderBatchService service,
        IPurchaseOrderBatchDocumentService documentService,
        IAdminActorContextAccessor actorAccessor)
    {
        _service = service;
        _documentService = documentService;
        _actorAccessor = actorAccessor;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? status = null, int? supplierId = null)
    {
        var result = await _service.ListAsync(status, supplierId, _actorAccessor.Get(User));
        if (!result.IsSuccess) return Failure(result.ErrorCode, result.Message);
        ViewBag.Actor = _actorAccessor.Get(User);
        ViewBag.Status = status;
        ViewBag.SupplierId = supplierId;
        return View(result.Data);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var actor = _actorAccessor.Get(User);
        var result = await _service.GetDetailAsync(id, actor);
        if (!result.IsSuccess) return Failure(result.ErrorCode, result.Message);
        var revisions = await _documentService.ListAsync(id, actor);
        if (!revisions.IsSuccess) return Failure(revisions.ErrorCode, revisions.Message);
        return View(new PurchaseOrderBatchDetailPageDto
        {
            Batch = result.Data!,
            DocumentRevisions = revisions.Data!,
            Actor = actor,
            ZaloMessage = BuildZaloMessage(result.Data!)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(PermissionConstants.PurchaseOrderCreateBatch)]
    public async Task<IActionResult> Create(CreatePurchaseOrderBatchRequest request)
    {
        var result = await _service.CreateAsync(request, _actorAccessor.Get(User));
        if (!result.IsSuccess)
        {
            TempData["Error"] = result.Message;
            return RedirectToAction("Index", "AdminPurchaseAdviceConsolidation");
        }
        TempData["Success"] = "Đã tạo đơn đặt hàng gộp và các đơn đặt hàng chi nhánh.";
        return RedirectToAction(nameof(Details), new { id = result.Data!.PurchaseOrderBatchId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(PermissionConstants.PurchaseOrderApprove)]
    public async Task<IActionResult> Approve(int id, PurchaseOrderBatchTransitionRequest request)
    {
        var result = await _service.ApproveAsync(id, request, _actorAccessor.Get(User));
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Đã duyệt đơn đặt hàng gộp và các đơn đặt hàng chi nhánh." : result.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(PermissionConstants.PurchaseOrderCancel)]
    public async Task<IActionResult> Cancel(int id, PurchaseOrderBatchTransitionRequest request)
    {
        var result = await _service.CancelAsync(id, request, _actorAccessor.Get(User));
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Đã hủy đơn đặt hàng gộp." : result.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(PermissionConstants.PurchaseOrderExport)]
    public async Task<IActionResult> GeneratePdf(int id)
    {
        var result = await _documentService.GenerateAsync(id, _actorAccessor.Get(User));
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? $"Đã sẵn sàng {result.Data!.FileName}."
            : result.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    [RequirePermission(PermissionConstants.PurchaseOrderExport)]
    public async Task<IActionResult> DownloadRevision(int revisionId)
    {
        var result = await _documentService.DownloadAsync(revisionId, _actorAccessor.Get(User));
        if (!result.IsSuccess) return Failure(result.ErrorCode, result.Message);
        return File(result.Data!.Content, result.Data.ContentType, result.Data.FileName);
    }

    [HttpGet]
    [RequirePermission(PermissionConstants.PurchaseOrderExport)]
    public async Task<IActionResult> PrintRevision(int revisionId)
    {
        var result = await _documentService.DownloadAsync(revisionId, _actorAccessor.Get(User));
        if (!result.IsSuccess) return Failure(result.ErrorCode, result.Message);
        Response.Headers.ContentDisposition = $"inline; filename=\"{result.Data!.FileName}\"";
        return File(result.Data.Content, result.Data.ContentType);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(PermissionConstants.PurchaseOrderSend)]
    public async Task<IActionResult> MarkRevisionSent(int id, int revisionId, MarkPurchaseOrderBatchDocumentSentRequest request)
    {
        var result = await _documentService.MarkSentAsync(id, revisionId, request, _actorAccessor.Get(User));
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "Đã ghi nhận gửi tài liệu cho nhà cung cấp."
            : result.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    private IActionResult Failure(string code, string message)
    {
        if (code == PurchaseOrderBatchErrorCodes.Forbidden) return Forbid();
        if (code == PurchaseOrderBatchErrorCodes.NotFound) return NotFound(message);
        TempData["Error"] = message;
        return RedirectToAction(nameof(Index));
    }

    private static string BuildZaloMessage(PurchaseOrderBatchDetailDto batch)
    {
        var amount = batch.TotalAmount.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
        var deliveryRange = batch.ExpectedDeliveryFrom.Date == batch.ExpectedDeliveryTo.Date
            ? batch.ExpectedDeliveryFrom.ToString("dd/MM/yyyy")
            : $"{batch.ExpectedDeliveryFrom:dd/MM/yyyy} - {batch.ExpectedDeliveryTo:dd/MM/yyyy}";
        return $"CafeChain gửi Đơn đặt hàng {batch.BatchNumber}.\n\n" +
               $"Nhà cung cấp: {batch.SupplierName}\n" +
               $"Tổng giá trị dự kiến: {amount} ₫\n" +
               $"Số chi nhánh giao hàng: {batch.StoreCount}\n" +
               $"Ngày giao mong muốn: {deliveryRange}\n\n" +
               "Chi tiết vui lòng xem file PDF đính kèm.";
    }
}
