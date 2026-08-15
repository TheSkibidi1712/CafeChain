using System.Text.Json;
using System.Data;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Production;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.Admin.Production;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Options;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Production;
using CafeChain.Models.Inventories.Stock;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CafeChain.Application.Services.Admin.Production;

public sealed class ProductionRunOperationsService : IProductionRunOperationsService
{
    private readonly AppDbContext _context;
    private readonly IAdminPermissionService _permissions;
    private readonly IUnitConversionService _unitConversion;
    private readonly IPhysicalUnitConversionService _physicalConversion;
    private readonly IProductionReadinessService _readiness;
    private readonly ProductionOperationsOptions _productionOptions;

    public ProductionRunOperationsService(
        AppDbContext context,
        IAdminPermissionService permissions,
        IUnitConversionService unitConversion,
        IPhysicalUnitConversionService physicalConversion,
        IProductionReadinessService readiness,
        IOptions<ProductionOperationsOptions>? productionOptions = null)
    {
        _context = context;
        _permissions = permissions;
        _unitConversion = unitConversion;
        _physicalConversion = physicalConversion;
        _readiness = readiness;
        _productionOptions = productionOptions?.Value ?? new ProductionOperationsOptions();
    }

    public Task<ServiceResult<ProductionRunOperationResultDto>> ReleaseAsync(
        int productionRunId,
        int actorStaffId)
        => TransitionAsync(
            productionRunId,
            actorStaffId,
            ProductionRunStatus.Planned,
            ProductionRunStatus.Released,
            PermissionConstants.ProductionOrderRelease,
            "Đã phát hành lệnh sản xuất.",
            requireReadiness: true);

    public Task<ServiceResult<ProductionRunOperationResultDto>> StartAsync(
        int productionRunId,
        int actorStaffId)
        => TransitionAsync(
            productionRunId,
            actorStaffId,
            ProductionRunStatus.Released,
            ProductionRunStatus.InProgress,
            PermissionConstants.ProductionOrderStart,
            "Đã bắt đầu sản xuất.");

    public async Task<ServiceResult<ProductionRunOperationResultDto>> RecordActualAsync(
        RecordProductionActualRequest request,
        int actorStaffId)
    {
        if (request == null || request.ProductionRunId <= 0)
            return Failure(ProductionRunOperationErrorCodes.InvalidActual, "Thông tin thực tế sản xuất chưa hợp lệ.");

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var run = await LoadForUpdateAsync(request.ProductionRunId);
            if (run == null || run.ContractVersion != 2)
            {
                await transaction.RollbackAsync();
                return Failure(ProductionRunOperationErrorCodes.NotFound, "Không tìm thấy lệnh sản xuất theo mẻ.");
            }

            var authorization = await AuthorizeAsync(
                run,
                actorStaffId,
                PermissionConstants.ProductionOrderRecordActual);
            if (!authorization.IsSuccess)
            {
                await transaction.RollbackAsync();
                return Failure(authorization.ErrorCode!, authorization.Message);
            }

            var existingOutput = await _context.ProductionRunOutputs
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.ProductionRunId == run.ProductionRunId);
            if (existingOutput != null)
            {
                var equivalent = existingOutput.ActualProducedBase == request.ActualProducedBase
                    && existingOutput.AcceptedOutputBase == request.AcceptedOutputBase
                    && existingOutput.RejectedOutputBase == request.RejectedOutputBase;
                await transaction.RollbackAsync();
                return equivalent
                    ? ServiceResult<ProductionRunOperationResultDto>.Success(
                        ToResult(run, existingOutput, wasReplay: true),
                        "Số liệu thực tế đã được ghi nhận trước đó.")
                    : Failure(
                        ProductionRunOperationErrorCodes.InvalidState,
                        "Số liệu thực tế đã được ghi nhận và không thể ghi đè.");
            }

            if (run.Status != ProductionRunStatus.InProgress)
            {
                await transaction.RollbackAsync();
                return Failure(
                    ProductionRunOperationErrorCodes.InvalidState,
                    "Chỉ lệnh đang sản xuất mới được ghi nhận số liệu thực tế.");
            }

            var outputValidation = ValidateOutput(request, run);
            if (!outputValidation.IsSuccess)
            {
                await transaction.RollbackAsync();
                return Failure(outputValidation.ErrorCode!, outputValidation.Message);
            }

            var plannedInputs = await BuildPlannedInputsAsync(run);
            if (!plannedInputs.IsSuccess || plannedInputs.Data == null)
            {
                await transaction.RollbackAsync();
                return Failure(
                    plannedInputs.ErrorCode ?? ProductionRunOperationErrorCodes.ActualInputsIncomplete,
                    plannedInputs.Message);
            }

            var actualInputs = NormalizeActualInputs(request.Inputs);
            if (!actualInputs.IsSuccess || actualInputs.Data == null)
            {
                await transaction.RollbackAsync();
                return Failure(actualInputs.ErrorCode!, actualInputs.Message);
            }

            var expectedKeys = plannedInputs.Data.Keys.OrderBy(x => x).ToArray();
            var actualKeys = actualInputs.Data.Keys.OrderBy(x => x).ToArray();
            if (!expectedKeys.SequenceEqual(actualKeys)
                || actualInputs.Data.Values.Sum(x => x.ActualBaseQuantity) <= 0)
            {
                await transaction.RollbackAsync();
                return Failure(
                    ProductionRunOperationErrorCodes.ActualInputsIncomplete,
                    "Phải xác nhận đầy đủ số lượng thực tế cho từng đầu vào của công thức.");
            }

            var now = DateTime.UtcNow;
            foreach (var (key, planned) in plannedInputs.Data)
            {
                var actual = actualInputs.Data[key];
                _context.ProductionRunInputActuals.Add(new ProductionRunInputActual
                {
                    ProductionRunId = run.ProductionRunId,
                    IngredientId = planned.IngredientId,
                    PreparedItemId = planned.PreparedItemId,
                    BaseUnitId = planned.BaseUnitId,
                    PlannedBaseQuantity = planned.PlannedBaseQuantity,
                    ActualBaseQuantity = actual.ActualBaseQuantity,
                    ConfirmedByStaffId = actorStaffId,
                    ConfirmedAtUtc = now
                });
            }

            var variancePercent = CalculateVariancePercent(
                run.ExpectedOutputBase!.Value,
                request.AcceptedOutputBase);
            var tolerance = run.YieldVarianceTolerancePercent
                ?? _productionOptions.DefaultYieldVarianceTolerancePercent;
            var requiresApproval = variancePercent > tolerance || request.AcceptedOutputBase == 0;
            var output = new ProductionRunOutput
            {
                ProductionRunId = run.ProductionRunId,
                BaseUnitId = run.OutputBaseUnitId!.Value,
                ExpectedOutputBase = run.ExpectedOutputBase.Value,
                ActualProducedBase = request.ActualProducedBase,
                AcceptedOutputBase = request.AcceptedOutputBase,
                RejectedOutputBase = request.RejectedOutputBase,
                VariancePercent = variancePercent,
                Reason = Clean(request.Reason),
                RecordedByStaffId = actorStaffId,
                RecordedAtUtc = now
            };
            _context.ProductionRunOutputs.Add(output);

            var previous = run.Status;
            run.Status = requiresApproval
                ? ProductionRunStatus.AwaitingVarianceApproval
                : ProductionRunStatus.AwaitingAcceptance;
            run.ActualRecordedByStaffId = actorStaffId;
            run.ActualRecordedAtUtc = now;
            run.VarianceReason = Clean(request.Reason);
            AddTransition(run, previous, run.Status, actorStaffId, request.Reason, new
            {
                request.ActualProducedBase,
                request.AcceptedOutputBase,
                request.RejectedOutputBase,
                VariancePercent = variancePercent,
                TolerancePercent = tolerance
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return ServiceResult<ProductionRunOperationResultDto>.Success(
                ToResult(run, output),
                requiresApproval
                    ? "Đã ghi nhận thực tế; chênh lệch cần được phê duyệt trước khi nhập kho."
                    : "Đã ghi nhận thực tế; lệnh đang chờ xác nhận nhập kho.");
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            _context.ChangeTracker.Clear();
            return Failure(
                ProductionRunOperationErrorCodes.Concurrency,
                "Lệnh sản xuất vừa được người khác cập nhật. Vui lòng tải lại dữ liệu.");
        }
    }

    public async Task<ServiceResult<ProductionRunOperationResultDto>> ApproveVarianceAsync(
        int productionRunId,
        int actorStaffId,
        string? reason)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var run = await LoadForUpdateAsync(productionRunId);
        if (run == null || run.ContractVersion != 2)
        {
            await transaction.RollbackAsync();
            return Failure(ProductionRunOperationErrorCodes.NotFound, "Không tìm thấy lệnh sản xuất theo mẻ.");
        }

        var authorization = await AuthorizeAsync(
            run,
            actorStaffId,
            PermissionConstants.ProductionOrderApproveVariance);
        if (!authorization.IsSuccess)
        {
            await transaction.RollbackAsync();
            return Failure(authorization.ErrorCode!, authorization.Message);
        }
        if (run.Status == ProductionRunStatus.AwaitingAcceptance
            && run.VarianceApprovedByStaffId.HasValue)
        {
            await transaction.RollbackAsync();
            return ServiceResult<ProductionRunOperationResultDto>.Success(
                ToResult(run, await LoadOutputAsync(run.ProductionRunId), wasReplay: true),
                "Chênh lệch đã được phê duyệt trước đó.");
        }
        if (run.Status != ProductionRunStatus.AwaitingVarianceApproval)
        {
            await transaction.RollbackAsync();
            return Failure(
                ProductionRunOperationErrorCodes.InvalidState,
                "Lệnh không ở trạng thái chờ duyệt chênh lệch.");
        }
        if (run.ActualRecordedByStaffId == actorStaffId)
        {
            await transaction.RollbackAsync();
            return Failure(
                ProductionRunOperationErrorCodes.MakerChecker,
                "Người ghi nhận thực tế không được tự duyệt chênh lệch của cùng lệnh sản xuất.");
        }

        var previous = run.Status;
        run.Status = ProductionRunStatus.AwaitingAcceptance;
        run.VarianceApprovedByStaffId = actorStaffId;
        run.VarianceApprovedAtUtc = DateTime.UtcNow;
        AddTransition(run, previous, run.Status, actorStaffId, reason, null);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return ServiceResult<ProductionRunOperationResultDto>.Success(
            ToResult(run, await LoadOutputAsync(run.ProductionRunId)),
            "Đã phê duyệt chênh lệch sản lượng.");
    }

    public async Task<ServiceResult<ProductionRunOperationResultDto>> CancelAsync(
        int productionRunId,
        int actorStaffId,
        string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return Failure(ProductionRunOperationErrorCodes.InvalidActual, "Hủy lệnh sản xuất phải có lý do.");

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var run = await LoadForUpdateAsync(productionRunId);
            if (run == null || run.ContractVersion != 2)
            {
                await transaction.RollbackAsync();
                return Failure(ProductionRunOperationErrorCodes.NotFound, "Không tìm thấy lệnh sản xuất theo mẻ.");
            }

            var authorization = await AuthorizeAsync(
                run,
                actorStaffId,
                PermissionConstants.ProductionOrderCancel);
            if (!authorization.IsSuccess)
            {
                await transaction.RollbackAsync();
                return Failure(authorization.ErrorCode!, authorization.Message);
            }

            var replay = run.Status == ProductionRunStatus.Cancelled;
            if (!replay && run.Status is not (ProductionRunStatus.Planned or ProductionRunStatus.Released))
            {
                await transaction.RollbackAsync();
                return Failure(
                    ProductionRunOperationErrorCodes.InvalidState,
                    "Trạng thái hiện tại không cho phép hủy lệnh sản xuất.");
            }

            var allocation = await _context.RestockSourcingAllocations
                .SingleOrDefaultAsync(x => x.ProductionRunId == productionRunId
                    && x.DecisionType == RestockSourcingDecisionTypes.Production);
            if (allocation != null)
            {
                var demand = await LoadRestockRequestForUpdateAsync(allocation.RestockRequestId);
                var allocations = await _context.RestockSourcingAllocations
                    .Where(x => x.RestockRequestId == allocation.RestockRequestId)
                    .ToListAsync();
                if (allocation.Status == RestockSourcingAllocationStatuses.Active)
                {
                    allocation.Status = RestockSourcingAllocationStatuses.Released;
                    allocation.ReleasedByStaffId = actorStaffId;
                    allocation.ReleasedAtUtc = DateTime.UtcNow;
                    allocation.ReleaseReason = Clean(reason);
                }

                if (demand != null)
                    RecomputeSourcingState(demand, allocations);
            }

            if (!replay)
            {
                var previous = run.Status;
                run.Status = ProductionRunStatus.Cancelled;
                AddTransition(run, previous, run.Status, actorStaffId, reason, null);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return ServiceResult<ProductionRunOperationResultDto>.Success(
                ToResult(run, null, replay),
                replay ? "Lệnh sản xuất đã được hủy trước đó." : "Đã hủy lệnh sản xuất và hoàn lại phần nhu cầu chưa sản xuất.");
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            _context.ChangeTracker.Clear();
            return Failure(
                ProductionRunOperationErrorCodes.Concurrency,
                "Lệnh sản xuất vừa được người khác cập nhật. Vui lòng tải lại dữ liệu.");
        }
        catch (Exception ex) when (FindSqlException(ex)?.Number == 1205)
        {
            await transaction.RollbackAsync();
            _context.ChangeTracker.Clear();
            return Failure(
                ProductionRunOperationErrorCodes.Concurrency,
                "Lệnh sản xuất đang được cập nhật đồng thời. Vui lòng tải lại dữ liệu.");
        }
    }

    private async Task<ServiceResult<ProductionRunOperationResultDto>> TransitionAsync(
        int productionRunId,
        int actorStaffId,
        ProductionRunStatus from,
        ProductionRunStatus to,
        string permission,
        string successMessage,
        bool requireReadiness = false)
        => await TransitionAsync(
            productionRunId,
            actorStaffId,
            new[] { from },
            to,
            permission,
            successMessage,
            null,
            requireReadiness);

    private async Task<ServiceResult<ProductionRunOperationResultDto>> TransitionAsync(
        int productionRunId,
        int actorStaffId,
        IReadOnlyCollection<ProductionRunStatus> from,
        ProductionRunStatus to,
        string permission,
        string successMessage,
        string? reason,
        bool requireReadiness = false)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var run = await LoadForUpdateAsync(productionRunId);
        if (run == null || run.ContractVersion != 2)
        {
            await transaction.RollbackAsync();
            return Failure(ProductionRunOperationErrorCodes.NotFound, "Không tìm thấy lệnh sản xuất theo mẻ.");
        }
        var authorization = await AuthorizeAsync(run, actorStaffId, permission);
        if (!authorization.IsSuccess)
        {
            await transaction.RollbackAsync();
            return Failure(authorization.ErrorCode!, authorization.Message);
        }
        if (run.Status == to)
        {
            await transaction.RollbackAsync();
            return ServiceResult<ProductionRunOperationResultDto>.Success(
                ToResult(run, null, wasReplay: true),
                successMessage);
        }
        if (!from.Contains(run.Status))
        {
            await transaction.RollbackAsync();
            return Failure(
                ProductionRunOperationErrorCodes.InvalidState,
                "Trạng thái hiện tại không cho phép thao tác này.");
        }

        if (requireReadiness)
        {
            var batchCount = run.PlannedBatchCount.HasValue
                ? run.PlannedBatchCount.Value
                : run.RequestedRunCount;
            var readiness = await _readiness.PreviewAsync(run.StoreId, run.RecipeId, batchCount);
            if (!readiness.IsSuccess || readiness.Data == null || !readiness.Data.IsReady)
            {
                await transaction.RollbackAsync();
                var reasons = readiness.Data?.Reasons
                    .Where(x => x.Blocking && !string.IsNullOrWhiteSpace(x.Message))
                    .Select(x => x.Message.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .Take(3)
                    .ToList() ?? new List<string>();
                var detail = reasons.Count > 0
                    ? string.Join(" ", reasons)
                    : readiness.Message;
                return Failure(
                    ProductionRunOperationErrorCodes.NotReady,
                    string.IsNullOrWhiteSpace(detail)
                        ? "Lệnh sản xuất chưa sẵn sàng. Vui lòng kiểm tra tồn đầu vào và các bán thành phẩm phụ thuộc."
                        : $"Lệnh sản xuất chưa sẵn sàng. {detail}");
            }
        }

        var previous = run.Status;
        run.Status = to;
        var now = DateTime.UtcNow;
        if (to == ProductionRunStatus.Released)
        {
            run.ReleasedByStaffId = actorStaffId;
            run.ReleasedAtUtc = now;
        }
        else if (to == ProductionRunStatus.InProgress)
        {
            run.StartedByStaffId = actorStaffId;
            run.StartedAtUtc = now;
        }
        AddTransition(run, previous, to, actorStaffId, reason, null);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return ServiceResult<ProductionRunOperationResultDto>.Success(ToResult(run, null), successMessage);
    }

    private async Task<ServiceResult> AuthorizeAsync(
        ProductionRun run,
        int actorStaffId,
        string permissionCode)
    {
        var accountId = await _context.Staffs
            .AsNoTracking()
            .Where(x => x.StaffId == actorStaffId && x.Active)
            .Select(x => x.AccountId)
            .SingleOrDefaultAsync();
        if (accountId <= 0)
            return ServiceResult.Failure("Không xác định được người thực hiện.", errorCode: ProductionRunOperationErrorCodes.Unauthorized);

        var permission = await _permissions.HasPermissionAsync(accountId, permissionCode, run.StoreId);
        return permission.IsSuccess && permission.Data?.Allowed == true
            ? ServiceResult.Success()
            : ServiceResult.Failure(
                "Bạn không có quyền thực hiện thao tác này tại cửa hàng.",
                errorCode: ProductionRunOperationErrorCodes.Unauthorized);
    }

    private ServiceResult ValidateOutput(RecordProductionActualRequest request, ProductionRun run)
    {
        if (!run.ExpectedOutputBase.HasValue
            || run.ExpectedOutputBase <= 0
            || !run.OutputBaseUnitId.HasValue
            || request.ActualProducedBase < 0
            || request.AcceptedOutputBase < 0
            || request.RejectedOutputBase < 0
            || request.AcceptedOutputBase + request.RejectedOutputBase != request.ActualProducedBase)
        {
            return ServiceResult.Failure(
                "Sản lượng thực tế, đạt và loại bỏ phải hợp lệ và khớp nhau.",
                errorCode: ProductionRunOperationErrorCodes.InvalidActual);
        }

        var variance = CalculateVariancePercent(run.ExpectedOutputBase.Value, request.AcceptedOutputBase);
        var tolerance = run.YieldVarianceTolerancePercent
            ?? _productionOptions.DefaultYieldVarianceTolerancePercent;
        if ((variance > tolerance || request.AcceptedOutputBase == 0)
            && string.IsNullOrWhiteSpace(request.Reason))
        {
            return ServiceResult.Failure(
                "Chênh lệch vượt ngưỡng hoặc sản lượng đạt bằng 0 phải có lý do.",
                errorCode: ProductionRunOperationErrorCodes.InvalidActual);
        }
        return ServiceResult.Success();
    }

    private async Task<ServiceResult<Dictionary<string, PlannedInput>>> BuildPlannedInputsAsync(
        ProductionRun run)
    {
        var details = await _context.RecipeDetails
            .AsNoTracking()
            .Where(x => x.RecipeId == run.RecipeId)
            .ToListAsync();
        if (details.Count == 0)
            return PlannedFailure("Công thức chưa có đầu vào BOM.");

        var result = new Dictionary<string, PlannedInput>(StringComparer.Ordinal);
        foreach (var detail in details)
        {
            var hasIngredient = detail.IngredientId.HasValue;
            var hasChild = detail.ChildRecipeId.HasValue;
            if (hasIngredient == hasChild)
                return PlannedFailure("Chi tiết công thức phải có đúng một đầu vào.");

            var raw = detail.Quantity * run.PlannedBatchCount!.Value;
            int? ingredientId = null;
            int? preparedItemId = null;
            int baseUnitId;
            decimal plannedBase;
            if (hasIngredient)
            {
                var ingredient = await _context.Ingredients.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.IngredientId == detail.IngredientId && x.Active);
                if (ingredient == null)
                    return PlannedFailure("Nguyên liệu đầu vào không còn hợp lệ.");
                var converted = await _unitConversion.ConvertAsync(
                    ingredient.IngredientId,
                    raw,
                    detail.UnitId,
                    ingredient.BaseUnitId);
                if (!converted.IsSuccess)
                    return PlannedFailure(converted.Message ?? "Không thể quy đổi đầu vào nguyên liệu.");
                ingredientId = ingredient.IngredientId;
                baseUnitId = ingredient.BaseUnitId;
                plannedBase = converted.Data;
            }
            else
            {
                var child = await _context.Recipes.AsNoTracking()
                    .Where(x => x.RecipeId == detail.ChildRecipeId)
                    .Select(x => new { x.PreparedItemId })
                    .SingleOrDefaultAsync();
                if (child == null || !child.PreparedItemId.HasValue)
                    return PlannedFailure("Bán thành phẩm phụ thuộc chưa có định danh tồn kho.");
                var prepared = await _context.PreparedItems.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.PreparedItemId == child!.PreparedItemId && x.Active);
                if (prepared == null)
                    return PlannedFailure("Bán thành phẩm phụ thuộc không còn hợp lệ.");
                var converted = await _physicalConversion.ConvertAsync(raw, detail.UnitId, prepared.BaseUnitId);
                if (!converted.IsSuccess)
                    return PlannedFailure(converted.Message ?? "Không thể quy đổi đầu vào bán thành phẩm.");
                preparedItemId = prepared.PreparedItemId;
                baseUnitId = prepared.BaseUnitId;
                plannedBase = converted.Data;
            }

            var key = IdentityKey(ingredientId, preparedItemId);
            if (result.TryGetValue(key, out var existing))
                existing.PlannedBaseQuantity += plannedBase;
            else
                result[key] = new PlannedInput(ingredientId, preparedItemId, baseUnitId, plannedBase);
        }
        return ServiceResult<Dictionary<string, PlannedInput>>.Success(result);
    }

    private static ServiceResult<Dictionary<string, ProductionActualInputRequest>> NormalizeActualInputs(
        IEnumerable<ProductionActualInputRequest>? inputs)
    {
        var result = new Dictionary<string, ProductionActualInputRequest>(StringComparer.Ordinal);
        foreach (var input in inputs ?? Array.Empty<ProductionActualInputRequest>())
        {
            if (input.IngredientId.HasValue == input.PreparedItemId.HasValue
                || input.ActualBaseQuantity < 0)
            {
                return ServiceResult<Dictionary<string, ProductionActualInputRequest>>.Failure(
                    "Đầu vào thực tế phải có đúng một định danh và số lượng không âm.",
                    errorCode: ProductionRunOperationErrorCodes.InvalidActual);
            }
            var key = IdentityKey(input.IngredientId, input.PreparedItemId);
            if (!result.TryAdd(key, input))
            {
                return ServiceResult<Dictionary<string, ProductionActualInputRequest>>.Failure(
                    "Mỗi đầu vào thực tế chỉ được khai báo một lần.",
                    errorCode: ProductionRunOperationErrorCodes.InvalidActual);
            }
        }
        return ServiceResult<Dictionary<string, ProductionActualInputRequest>>.Success(result);
    }

    private async Task<ProductionRun?> LoadForUpdateAsync(int productionRunId)
    {
        if (_context.Database.IsSqlServer())
        {
            return await _context.ProductionRuns
                .FromSqlInterpolated(
                    $@"SELECT * FROM ProductionRuns WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
                       WHERE ProductionRunId = {productionRunId}")
                .SingleOrDefaultAsync();
        }
        return await _context.ProductionRuns.SingleOrDefaultAsync(x => x.ProductionRunId == productionRunId);
    }

    private async Task<RestockRequest?> LoadRestockRequestForUpdateAsync(int restockRequestId)
    {
        if (_context.Database.IsSqlServer())
        {
            return await _context.RestockRequests
                .FromSqlInterpolated(
                    $@"SELECT * FROM RestockRequests WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
                       WHERE RestockRequestId = {restockRequestId}")
                .SingleOrDefaultAsync();
        }
        return await _context.RestockRequests.SingleOrDefaultAsync(x => x.RestockRequestId == restockRequestId);
    }

    private static void RecomputeSourcingState(
        RestockRequest demand,
        IReadOnlyCollection<RestockSourcingAllocation> allocations)
    {
        var active = allocations
            .Where(x => x.Status is RestockSourcingAllocationStatuses.Active
                or RestockSourcingAllocationStatuses.PendingPurchaseAdvice)
            .ToList();
        var allocated = active.Sum(x => x.ProcurementQuantity);
        var requested = demand.RequestedProcurementQuantity ?? demand.RequestedQuantity;
        demand.SourcingStatus = allocated <= 0
            ? RestockSourcingStatuses.Unallocated
            : allocated >= requested
                ? RestockSourcingStatuses.FullyAllocated
                : RestockSourcingStatuses.PartiallyAllocated;
        if (active.Count == 0)
            demand.SourcingDecision = null;
        demand.UpdatedAt = DateTime.UtcNow;
    }

    private Task<ProductionRunOutput?> LoadOutputAsync(int productionRunId)
        => _context.ProductionRunOutputs.AsNoTracking()
            .SingleOrDefaultAsync(x => x.ProductionRunId == productionRunId);

    private void AddTransition(
        ProductionRun run,
        ProductionRunStatus from,
        ProductionRunStatus to,
        int actorStaffId,
        string? reason,
        object? evidence)
    {
        _context.ProductionRunTransitions.Add(new ProductionRunTransition
        {
            ProductionRunId = run.ProductionRunId,
            FromStatus = from.ToString().ToUpperInvariant(),
            ToStatus = to.ToString().ToUpperInvariant(),
            ActorStaffId = actorStaffId,
            OccurredAtUtc = DateTime.UtcNow,
            Reason = Clean(reason),
            EvidenceJson = evidence == null ? null : JsonSerializer.Serialize(evidence)
        });
    }

    private static decimal CalculateVariancePercent(decimal expected, decimal accepted)
        => expected <= 0 ? 0 : Math.Round(Math.Abs(accepted - expected) / expected * 100m, 4);

    private static string IdentityKey(int? ingredientId, int? preparedItemId)
        => ingredientId.HasValue ? $"I:{ingredientId.Value}" : $"P:{preparedItemId!.Value}";

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, 500)];

    private static SqlException? FindSqlException(Exception exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
            if (current is SqlException sqlException)
                return sqlException;
        return null;
    }

    private static ProductionRunOperationResultDto ToResult(
        ProductionRun run,
        ProductionRunOutput? output,
        bool wasReplay = false)
        => new()
        {
            ProductionRunId = run.ProductionRunId,
            Status = run.Status.ToString().ToUpperInvariant(),
            ExpectedOutputBase = run.ExpectedOutputBase,
            AcceptedOutputBase = output?.AcceptedOutputBase,
            VariancePercent = output?.VariancePercent,
            RequiresVarianceApproval = run.Status == ProductionRunStatus.AwaitingVarianceApproval,
            WasReplay = wasReplay
        };

    private static ServiceResult<Dictionary<string, PlannedInput>> PlannedFailure(string message)
        => ServiceResult<Dictionary<string, PlannedInput>>.Failure(
            message,
            errorCode: ProductionRunOperationErrorCodes.ActualInputsIncomplete);

    private static ServiceResult<ProductionRunOperationResultDto> Failure(string code, string message)
        => ServiceResult<ProductionRunOperationResultDto>.Failure(message, errorCode: code);

    private sealed class PlannedInput
    {
        public PlannedInput(int? ingredientId, int? preparedItemId, int baseUnitId, decimal quantity)
        {
            IngredientId = ingredientId;
            PreparedItemId = preparedItemId;
            BaseUnitId = baseUnitId;
            PlannedBaseQuantity = quantity;
        }

        public int? IngredientId { get; }
        public int? PreparedItemId { get; }
        public int BaseUnitId { get; }
        public decimal PlannedBaseQuantity { get; set; }
    }
}
