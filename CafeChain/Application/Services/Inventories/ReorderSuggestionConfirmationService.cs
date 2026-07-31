using System.Text.Json;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Systems;
using CafeChain.Application.Results;
using CafeChain.Infrastructure.Interfaces.Admin.Procurement;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Stock;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Inventories;

public sealed class ReorderSuggestionConfirmationService
    : IReorderSuggestionConfirmationService
{
    private const string DeduplicationAction = "REORDER_SUGGESTION_CONFIRM";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IReorderSuggestionConfirmationRepository _repository;
    private readonly IReorderSuggestionService _suggestions;
    private readonly IReorderSuggestionTokenService _tokens;
    private readonly IReorderSuggestionAuthorizationService _authorization;
    private readonly IRequestDeduplicationService _deduplication;
    private readonly IUnitConversionService _unitConversion;
    private readonly TimeProvider _clock;

    public ReorderSuggestionConfirmationService(
        IReorderSuggestionConfirmationRepository repository,
        IReorderSuggestionService suggestions,
        IReorderSuggestionTokenService tokens,
        IReorderSuggestionAuthorizationService authorization,
        IRequestDeduplicationService deduplication,
        IUnitConversionService unitConversion,
        TimeProvider clock)
    {
        _repository = repository;
        _suggestions = suggestions;
        _tokens = tokens;
        _authorization = authorization;
        _deduplication = deduplication;
        _unitConversion = unitConversion;
        _clock = clock;
    }

    public async Task<ServiceResult<ConfirmReorderSuggestionResultDto>> ConfirmAsync(
        ConfirmReorderSuggestionRequest request,
        AdminActorContext actor,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidRequest(request, actor))
            return Failure("Yêu cầu xác nhận không hợp lệ.",
                ReorderSuggestionConfirmationErrorCodes.InvalidRequest);

        await _repository.BeginTransactionAsync(cancellationToken);
        try
        {
            var dedup = await _deduplication.BeginAsync(
                request.RequestKey.Trim(),
                DeduplicationAction,
                actor.StaffId,
                new
                {
                    request.StoreId,
                    request.IngredientId,
                    SuggestionToken = request.SuggestionToken.Trim()
                });
            if (!dedup.CanProcess)
            {
                if (string.Equals(dedup.Status, "SUCCESS", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(dedup.ResponseBody))
                {
                    var replay = JsonSerializer.Deserialize<ConfirmReorderSuggestionResultDto>(
                        dedup.ResponseBody,
                        JsonOptions);
                    await _repository.RollbackTransactionAsync(cancellationToken);
                    if (replay != null)
                    {
                        replay.Replayed = true;
                        replay.Message = "Yêu cầu đã được xử lý trước đó; trả lại cùng kết quả.";
                        return ServiceResult<ConfirmReorderSuggestionResultDto>.Success(replay);
                    }
                }

                await _repository.RollbackTransactionAsync(cancellationToken);
                return Failure(
                    dedup.ErrorMessage ?? "Yêu cầu đang được xử lý hoặc RequestKey đã được dùng.",
                    dedup.ErrorCode == "REQUEST_IN_PROGRESS"
                        ? ReorderSuggestionConfirmationErrorCodes.RequestInProgress
                        : ReorderSuggestionConfirmationErrorCodes.InvalidRequest);
            }

            // Successful replay is handled before token validation so an
            // already-committed request remains safely replayable after expiry.
            var token = _tokens.Read(
                request.SuggestionToken,
                actor.StaffId,
                request.StoreId,
                request.IngredientId);
            if (!token.IsValid || token.Payload == null)
            {
                await _repository.RollbackTransactionAsync(cancellationToken);
                return Failure(
                    token.IsExpired
                        ? "Gợi ý đã hết hạn; vui lòng tải lại dữ liệu."
                        : "Gợi ý không còn hợp lệ; vui lòng tải lại dữ liệu.",
                    token.ErrorCode ?? ReorderSuggestionConfirmationErrorCodes.SuggestionChanged);
            }

            if (!await _authorization.CanConfirmAsync(
                    actor,
                    request.StoreId,
                    cancellationToken))
            {
                await _repository.RollbackTransactionAsync(cancellationToken);
                return Failure(
                    "Bạn không có quyền xác nhận nhập hàng cho cửa hàng này.",
                    ReorderSuggestionConfirmationErrorCodes.Unauthorized);
            }

            await _repository.AcquireIngredientLockAsync(
                request.StoreId,
                request.IngredientId,
                cancellationToken);

            var recalculated = await _suggestions.CalculateForStoreAsync(
                request.StoreId,
                token.Payload.AnalysisWindowDays,
                ingredientIds: [request.IngredientId],
                cancellationToken: cancellationToken);
            var item = recalculated.IsSuccess
                ? recalculated.Data?.Items.SingleOrDefault()
                : null;
            if (item == null)
            {
                await _repository.RollbackTransactionAsync(cancellationToken);
                return Failure(
                    recalculated.Message ?? "Không thể tính lại gợi ý nhập hàng.",
                    ReorderSuggestionConfirmationErrorCodes.DataIncomplete);
            }

            var currentFingerprint = _tokens.ComputeDecisionFingerprint(
                ReorderSuggestionContractMapper.ToDecision(item));
            if (!string.Equals(
                    token.Payload.CalculationVersion,
                    item.CalculationVersion,
                    StringComparison.Ordinal)
                || !string.Equals(
                    token.Payload.DecisionFingerprint,
                    currentFingerprint,
                    StringComparison.Ordinal))
            {
                await _repository.RollbackTransactionAsync(cancellationToken);
                return Failure(
                    "Dữ liệu tồn kho hoặc mua hàng đã thay đổi; vui lòng tải lại gợi ý.",
                    ReorderSuggestionConfirmationErrorCodes.SuggestionChanged);
            }

            if (item.SuggestionStatus == ReorderRecommendationLevels.DataIncomplete)
            {
                await _repository.RollbackTransactionAsync(cancellationToken);
                return Failure(
                    "Dữ liệu chưa đủ để xác nhận nhập hàng.",
                    ReorderSuggestionConfirmationErrorCodes.DataIncomplete);
            }
            if (!item.CanConfirm
                || !item.FinalSuggestedQuantity.HasValue
                || item.FinalSuggestedQuantity <= 0m)
            {
                await _repository.RollbackTransactionAsync(cancellationToken);
                return Failure(
                    "Gợi ý hiện không còn nhu cầu cần xác nhận.",
                    ReorderSuggestionConfirmationErrorCodes.NoRemainingDemand);
            }

            var baseQuantity = decimal.Round(
                item.FinalSuggestedQuantity.Value,
                3,
                MidpointRounding.AwayFromZero);
            if (baseQuantity <= 0m)
            {
                await _repository.RollbackTransactionAsync(cancellationToken);
                return Failure(
                    "Lượng gợi ý nhỏ hơn độ chính xác tồn kho cho phép.",
                    ReorderSuggestionConfirmationErrorCodes.NoRemainingDemand);
            }

            var baseUnit = await _repository.GetIngredientBaseUnitAsync(
                request.IngredientId,
                cancellationToken);
            if (baseUnit == null)
            {
                await _repository.RollbackTransactionAsync(cancellationToken);
                return Failure(
                    "Không tìm thấy đơn vị tồn kho cơ sở của nguyên liệu.",
                    ReorderSuggestionConfirmationErrorCodes.DataIncomplete);
            }

            var now = _clock.GetUtcNow().UtcDateTime;
            var restock = await _repository.GetActiveRequestAsync(
                request.StoreId,
                request.IngredientId,
                cancellationToken);
            var operation = restock == null
                ? ReorderSuggestionConfirmationOperations.Created
                : ReorderSuggestionConfirmationOperations.Adjusted;
            var quantityBefore = restock?.RequestedQuantity ?? 0m;

            if (restock == null)
            {
                var procurementUnit = await _repository.GetCanonicalProcurementUnitAsync(
                    baseUnit.Type,
                    cancellationToken);
                if (procurementUnit == null)
                {
                    await _repository.RollbackTransactionAsync(cancellationToken);
                    return Failure(
                        "Chưa cấu hình đơn vị mua hàng chuẩn cho loại nguyên liệu.",
                        ReorderSuggestionConfirmationErrorCodes.DataIncomplete);
                }

                var procurementQuantity = await ConvertFromBaseAsync(
                    request.IngredientId,
                    baseQuantity,
                    baseUnit,
                    procurementUnit);
                if (!procurementQuantity.IsSuccess || procurementQuantity.Data <= 0m)
                {
                    await _repository.RollbackTransactionAsync(cancellationToken);
                    return Failure(
                        procurementQuantity.Message ?? "Không quy đổi được đơn vị mua hàng.",
                        ReorderSuggestionConfirmationErrorCodes.DataIncomplete);
                }

                decimal? targetProcurement = null;
                if (item.ReorderPoint.HasValue)
                {
                    var convertedTarget = await ConvertFromBaseAsync(
                        request.IngredientId,
                        item.ReorderPoint.Value,
                        baseUnit,
                        procurementUnit);
                    if (convertedTarget.IsSuccess)
                    {
                        targetProcurement = decimal.Round(
                            convertedTarget.Data,
                            3,
                            MidpointRounding.AwayFromZero);
                    }
                }

                restock = new RestockRequest
                {
                    StoreId = request.StoreId,
                    CreatedForStoreId = request.StoreId,
                    SourceType = RestockRequestSourceTypes.ReorderSuggestion,
                    SourceReferenceId = request.RequestKey.Trim(),
                    IngredientId = request.IngredientId,
                    RequestedQuantity = baseQuantity,
                    SuggestedQuantity = baseQuantity,
                    RequestedProcurementQuantity = decimal.Round(
                        procurementQuantity.Data,
                        3,
                        MidpointRounding.AwayFromZero),
                    ProcurementUnitId = procurementUnit.UnitId,
                    TargetStockProcurementQuantity = targetProcurement,
                    SuggestionAnalysisWindowDays = token.Payload.AnalysisWindowDays,
                    SuggestionAvailableSnapshot = item.AvailableStock,
                    SuggestionMinLevelSnapshot = item.MinimumStock,
                    SuggestionAverageDailyUsageSnapshot = item.AverageDailyConsumption,
                    SuggestionLeadTimeDaysSnapshot = item.LeadTimeDays,
                    SuggestionIncomingQuantitySnapshot = item.IncomingQuantity,
                    SuggestionReason = item.Reason,
                    Status = RestockRequestStatuses.Draft,
                    Priority = item.MinimumStock.HasValue
                        && item.AvailableStock < item.MinimumStock.Value
                            ? RestockRequestPriorities.Urgent
                            : RestockRequestPriorities.High,
                    SourcingStatus = RestockSourcingStatuses.Unallocated,
                    CreatedByStaffId = actor.StaffId,
                    CreatedAt = now,
                    UpdatedAt = now,
                    Note = "Tạo từ gợi ý nhập hàng deterministic."
                };
                _repository.AddRequest(restock);
                await _repository.SaveChangesAsync(cancellationToken);
            }
            else
            {
                if (restock.ProcurementUnitId.HasValue && restock.ProcurementUnit == null)
                {
                    await _repository.RollbackTransactionAsync(cancellationToken);
                    return Failure(
                        "Yêu cầu hiện tại tham chiếu đơn vị mua hàng không còn tồn tại.",
                        ReorderSuggestionConfirmationErrorCodes.DataIncomplete);
                }

                var procurementUnit = restock.ProcurementUnit != null
                        ? new ReorderUnitRow(
                            restock.ProcurementUnit.UnitId,
                            restock.ProcurementUnit.UnitCode,
                            restock.ProcurementUnit.Type)
                        : await _repository.GetCanonicalProcurementUnitAsync(
                            baseUnit.Type,
                            cancellationToken);
                if (procurementUnit == null)
                {
                    await _repository.RollbackTransactionAsync(cancellationToken);
                    return Failure(
                        "Yêu cầu hiện tại chưa có đơn vị mua hàng hợp lệ.",
                        ReorderSuggestionConfirmationErrorCodes.DataIncomplete);
                }

                var procurementDelta = await ConvertFromBaseAsync(
                    request.IngredientId,
                    baseQuantity,
                    baseUnit,
                    procurementUnit);
                if (!procurementDelta.IsSuccess || procurementDelta.Data <= 0m)
                {
                    await _repository.RollbackTransactionAsync(cancellationToken);
                    return Failure(
                        procurementDelta.Message ?? "Không quy đổi được đơn vị mua hàng.",
                        ReorderSuggestionConfirmationErrorCodes.DataIncomplete);
                }

                restock.RequestedQuantity = checked(restock.RequestedQuantity + baseQuantity);
                restock.ProcurementUnitId = procurementUnit.UnitId;
                restock.RequestedProcurementQuantity = decimal.Round(
                    restock.RequestedProcurementQuantity.GetValueOrDefault()
                    + procurementDelta.Data,
                    3,
                    MidpointRounding.AwayFromZero);
                if (restock.SourcingStatus == RestockSourcingStatuses.FullyAllocated)
                    restock.SourcingStatus = RestockSourcingStatuses.PartiallyAllocated;
                restock.UpdatedAt = now;
            }

            var snapshot = ReorderSuggestionContractMapper.ToSnapshot(
                item,
                RestockRequestSourceTypes.ReorderSuggestion,
                operation);
            _repository.AddTransition(new RestockRequestTransition
            {
                RestockRequestId = restock.RestockRequestId,
                PreviousStatus = operation == ReorderSuggestionConfirmationOperations.Created
                    ? "NONE"
                    : restock.Status,
                NewStatus = restock.Status,
                ActorStaffId = actor.StaffId,
                OccurredAtUtc = now,
                Reason = operation == ReorderSuggestionConfirmationOperations.Created
                    ? "Tạo yêu cầu từ gợi ý nhập hàng."
                    : "Bổ sung nhu cầu mới vào yêu cầu nhập hàng đang mở.",
                QuantityBefore = quantityBefore,
                QuantityAfter = restock.RequestedQuantity,
                RequestKey = $"REORDER_CONFIRM:{request.RequestKey.Trim()}",
                SuggestionSnapshotVersion = ReorderSuggestionBusinessSnapshot.SchemaVersion,
                SuggestionSnapshotJson = JsonSerializer.Serialize(snapshot, JsonOptions)
            });
            await _repository.SaveChangesAsync(cancellationToken);

            var response = new ConfirmReorderSuggestionResultDto
            {
                RestockRequestId = restock.RestockRequestId,
                Operation = operation,
                Replayed = false,
                Message = operation == ReorderSuggestionConfirmationOperations.Created
                    ? "Đã tạo yêu cầu nhập hàng nháp."
                    : "Đã bổ sung nhu cầu vào yêu cầu nhập hàng đang mở."
            };
            await _deduplication.MarkSuccessAsync(
                dedup.Entry!,
                restock.RestockRequestId,
                response);
            await _repository.CommitTransactionAsync(cancellationToken);
            return ServiceResult<ConfirmReorderSuggestionResultDto>.Success(
                response,
                response.Message);
        }
        catch (OperationCanceledException)
        {
            await SafeRollbackAsync();
            throw;
        }
        catch (DbUpdateConcurrencyException)
        {
            await SafeRollbackAsync();
            return Failure(
                "Yêu cầu nhập hàng vừa được cập nhật bởi thao tác khác; vui lòng tải lại.",
                ReorderSuggestionConfirmationErrorCodes.ConcurrentUpdate);
        }
        catch (DbUpdateException)
        {
            await SafeRollbackAsync();
            return Failure(
                "Có thao tác đồng thời trên cùng nhu cầu nhập hàng; vui lòng tải lại.",
                ReorderSuggestionConfirmationErrorCodes.ConcurrentUpdate);
        }
        catch (TimeoutException)
        {
            await SafeRollbackAsync();
            return Failure(
                "Không thể khóa nhu cầu nhập hàng trong thời gian cho phép; vui lòng thử lại.",
                ReorderSuggestionConfirmationErrorCodes.ConcurrentUpdate);
        }
        catch (OverflowException)
        {
            await SafeRollbackAsync();
            return Failure(
                "Số lượng xác nhận vượt giới hạn lưu trữ.",
                ReorderSuggestionConfirmationErrorCodes.InvalidRequest);
        }
        catch
        {
            await SafeRollbackAsync();
            throw;
        }
    }

    private async Task<ServiceResult<decimal>> ConvertFromBaseAsync(
        int ingredientId,
        decimal quantity,
        ReorderUnitRow baseUnit,
        ReorderUnitRow procurementUnit)
    {
        if (baseUnit.UnitId == procurementUnit.UnitId
            || (baseUnit.Type == UnitType.Dem
                && procurementUnit.Type == UnitType.Dem))
        {
            return ServiceResult<decimal>.Success(quantity);
        }

        return await _unitConversion.ConvertAsync(
            ingredientId,
            quantity,
            baseUnit.UnitId,
            procurementUnit.UnitId);
    }

    private static bool IsValidRequest(
        ConfirmReorderSuggestionRequest? request,
        AdminActorContext actor) =>
        request != null
        && request.StoreId > 0
        && request.IngredientId > 0
        && actor.StaffId > 0
        && !string.IsNullOrWhiteSpace(request.SuggestionToken)
        && request.SuggestionToken.Length <= 4096
        && Guid.TryParseExact(request.RequestKey?.Trim(), "D", out var requestId)
        && requestId != Guid.Empty;

    private async Task SafeRollbackAsync()
    {
        try
        {
            await _repository.RollbackTransactionAsync();
        }
        catch
        {
            _repository.ClearTracking();
        }
    }

    private static ServiceResult<ConfirmReorderSuggestionResultDto> Failure(
        string message,
        string errorCode) =>
        ServiceResult<ConfirmReorderSuggestionResultDto>.Failure(
            message,
            errorCode: errorCode);
}
