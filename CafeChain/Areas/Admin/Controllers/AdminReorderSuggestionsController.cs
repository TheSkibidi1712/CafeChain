using CafeChain.Application.Authorization;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Admin.StoreScope;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CafeChain.Areas.Admin.Controllers;

[RequirePermission(PermissionConstants.ReorderSuggestionView)]
public sealed class AdminReorderSuggestionsController : AdminBaseController
{
    private readonly IReorderSuggestionService _suggestions;
    private readonly IReorderSuggestionTokenService _tokens;
    private readonly IReorderSuggestionAuthorizationService _authorization;
    private readonly IReorderSuggestionConfirmationService _confirmation;
    private readonly IAdminActorContextAccessor _actorAccessor;
    private readonly IAdminStoreScopeResolver _storeScopeResolver;

    public AdminReorderSuggestionsController(
        IReorderSuggestionService suggestions,
        IReorderSuggestionTokenService tokens,
        IReorderSuggestionAuthorizationService authorization,
        IReorderSuggestionConfirmationService confirmation,
        IAdminActorContextAccessor actorAccessor,
        IAdminStoreScopeResolver storeScopeResolver)
    {
        _suggestions = suggestions;
        _tokens = tokens;
        _authorization = authorization;
        _confirmation = confirmation;
        _actorAccessor = actorAccessor;
        _storeScopeResolver = storeScopeResolver;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        int? storeId,
        int analysisWindowDays = 30,
        CancellationToken cancellationToken = default)
    {
        var actor = _actorAccessor.Get(User);
        if (actor.StaffId <= 0)
            return Unauthorized();

        var storeScope = await _storeScopeResolver.ResolveAsync(
            actor,
            StoreScopePurpose.ReorderSuggestion,
            storeId,
            cancellationToken);
        if (!storeScope.IsResolved)
            return StoreScopeFailure(storeScope);

        var selectedStoreId = storeScope.StoreId!.Value;
        ViewBag.Stores = storeScope.AccessibleStores
            .Select(x => new SelectListItem(
                x.StoreName,
                x.StoreId.ToString()))
            .ToList();
        ViewBag.SelectedStoreId = selectedStoreId;
        SetStoreScopeViewData(storeScope);

        var result = await _suggestions.GetForStoreAsync(
            selectedStoreId,
            actor,
            analysisWindowDays,
            cancellationToken);
        if (!result.IsSuccess || result.Data == null)
        {
            ViewBag.ErrorMessage = result.Message
                ?? "Không tải được gợi ý nhập hàng.";
            return View(new ReorderSuggestionListDto
            {
                StoreId = selectedStoreId,
                AnalysisWindowDays = analysisWindowDays
            });
        }

        var canConfirm = await _authorization.CanConfirmAsync(
            actor,
            selectedStoreId,
            cancellationToken);
        foreach (var item in result.Data.Items)
        {
            item.CanConfirm &= canConfirm;
            var fingerprint = _tokens.ComputeDecisionFingerprint(
                ReorderSuggestionContractMapper.ToDecision(item));
            item.SuggestionToken = _tokens.Issue(
                actor.StaffId,
                item.StoreId,
                item.IngredientId,
                analysisWindowDays,
                item.CalculationVersion,
                fingerprint);
        }

        return View(result.Data);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Explain(
        [FromForm] ExplainReorderSuggestionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return UnprocessableEntity(new
            {
                success = false,
                code = ReorderSuggestionConfirmationErrorCodes.InvalidRequest,
                message = "Yêu cầu giải thích không hợp lệ."
            });

        var actor = _actorAccessor.Get(User);
        var scope = await _storeScopeResolver.ResolveAsync(
            actor,
            StoreScopePurpose.ReorderSuggestion,
            request.StoreId,
            cancellationToken);
        if (!scope.IsResolved
            || !await _authorization.CanViewAsync(
                actor,
                request.StoreId,
                cancellationToken))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                success = false,
                code = ReorderSuggestionConfirmationErrorCodes.Unauthorized,
                message = "Bạn không có quyền truy cập cửa hàng này."
            });
        }

        var token = _tokens.Read(
            request.SuggestionToken,
            actor.StaffId,
            request.StoreId,
            request.IngredientId);
        if (!token.IsValid || token.Payload == null)
            return Conflict(new
            {
                success = false,
                code = token.ErrorCode,
                message = token.IsExpired
                    ? "Gợi ý đã hết hạn; vui lòng tải lại."
                    : "Gợi ý đã thay đổi; vui lòng tải lại."
            });

        var current = await _suggestions.CalculateForStoreAsync(
            request.StoreId,
            token.Payload.AnalysisWindowDays,
            ingredientIds: [request.IngredientId],
            cancellationToken: cancellationToken);
        var item = current.IsSuccess
            ? current.Data?.Items.SingleOrDefault()
            : null;
        if (item == null)
            return UnprocessableEntity(new
            {
                success = false,
                code = ReorderSuggestionConfirmationErrorCodes.DataIncomplete,
                message = current.Message ?? "Không thể tính lại gợi ý."
            });

        var fingerprint = _tokens.ComputeDecisionFingerprint(
            ReorderSuggestionContractMapper.ToDecision(item));
        if (!string.Equals(
                token.Payload.CalculationVersion,
                item.CalculationVersion,
                StringComparison.Ordinal)
            || !string.Equals(
                token.Payload.DecisionFingerprint,
                fingerprint,
                StringComparison.Ordinal))
        {
            return Conflict(new
            {
                success = false,
                code = ReorderSuggestionConfirmationErrorCodes.SuggestionChanged,
                message = "Dữ liệu đã thay đổi; vui lòng tải lại gợi ý."
            });
        }

        var result = await _suggestions.ExplainCalculatedAsync(
            item,
            cancellationToken);
        if (!result.IsSuccess || result.Data == null)
            return UnprocessableEntity(new
            {
                success = false,
                message = result.Message ?? "Không tạo được giải thích."
            });

        return Json(new
        {
            success = true,
            summary = result.Data.Summary,
            explanation = result.Data.Explanation,
            risk = result.Data.Risk,
            recommendedActionText = result.Data.RecommendedActionText,
            usedOllama = result.Data.UsedOllama,
            usedFallback = result.Data.UsedFallback
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(PermissionConstants.RestockCreate)]
    public async Task<IActionResult> Confirm(
        [FromForm] ConfirmReorderSuggestionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return UnprocessableEntity(new
            {
                success = false,
                code = ReorderSuggestionConfirmationErrorCodes.InvalidRequest,
                message = "Yêu cầu xác nhận không hợp lệ."
            });

        var actor = _actorAccessor.Get(User);
        var scope = await _storeScopeResolver.ResolveAsync(
            actor,
            StoreScopePurpose.ReorderSuggestion,
            request.StoreId,
            cancellationToken);
        if (!scope.IsResolved)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                success = false,
                code = ReorderSuggestionConfirmationErrorCodes.Unauthorized,
                message = "Bạn không có quyền truy cập cửa hàng này."
            });
        }

        var result = await _confirmation.ConfirmAsync(
            request,
            actor,
            cancellationToken);
        if (result.IsSuccess && result.Data != null)
            return Json(new { success = true, data = result.Data });

        var response = new
        {
            success = false,
            code = result.ErrorCode,
            message = result.Message ?? "Không thể xác nhận gợi ý nhập hàng."
        };
        if (result.ErrorCode == ReorderSuggestionConfirmationErrorCodes.Unauthorized)
            return StatusCode(StatusCodes.Status403Forbidden, response);
        if (result.ErrorCode is ReorderSuggestionConfirmationErrorCodes.SuggestionChanged
            or ReorderSuggestionConfirmationErrorCodes.SuggestionExpired
            or ReorderSuggestionConfirmationErrorCodes.ConcurrentUpdate
            or ReorderSuggestionConfirmationErrorCodes.RequestInProgress)
        {
            return Conflict(response);
        }

        return UnprocessableEntity(response);
    }
}
