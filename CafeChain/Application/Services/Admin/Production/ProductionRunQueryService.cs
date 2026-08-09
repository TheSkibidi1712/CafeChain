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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CafeChain.Application.Services.Admin.Production;

public sealed class ProductionRunQueryService : IProductionRunQueryService
{
    private readonly AppDbContext _context;
    private readonly IAdminPermissionService _permissions;
    private readonly IUnitConversionService _unitConversion;
    private readonly IPhysicalUnitConversionService _physicalConversion;
    private readonly ProductionOperationsOptions _productionOptions;

    public ProductionRunQueryService(
        AppDbContext context,
        IAdminPermissionService permissions,
        IUnitConversionService unitConversion,
        IPhysicalUnitConversionService physicalConversion,
        IOptions<ProductionOperationsOptions>? productionOptions = null)
    {
        _context = context;
        _permissions = permissions;
        _unitConversion = unitConversion;
        _physicalConversion = physicalConversion;
        _productionOptions = productionOptions?.Value ?? new ProductionOperationsOptions();
    }

    public async Task<ServiceResult<ProductionRunListDto>> GetPageAsync(ProductionRunListQuery query, int accountId)
    {
        if (query.StoreId <= 0 || accountId <= 0)
            return Failure<ProductionRunListDto>("Cửa hàng hoặc tài khoản chưa hợp lệ.");
        if (!await AllowedAsync(accountId, PermissionConstants.ProductionOrderView, query.StoreId))
            return Forbidden<ProductionRunListDto>();

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 10, 50);
        var runs = _context.ProductionRuns.AsNoTracking().Where(x => x.StoreId == query.StoreId);
        if (!string.IsNullOrWhiteSpace(query.Status)
            && Enum.TryParse<ProductionRunStatus>(query.Status, true, out var status))
        {
            runs = runs.Where(x => x.Status == status);
        }

        var total = await runs.CountAsync();
        var rows = await (
            from run in runs
            join recipe in _context.Recipes.AsNoTracking() on run.RecipeId equals recipe.RecipeId
            join prepared in _context.PreparedItems.AsNoTracking() on recipe.PreparedItemId equals prepared.PreparedItemId into preparedItems
            from prepared in preparedItems.DefaultIfEmpty()
            join store in _context.Stores.AsNoTracking() on run.StoreId equals store.StoreId
            join staff in _context.Staffs.AsNoTracking() on run.CreatedByStaffId equals staff.StaffId
            join unit in _context.Units.AsNoTracking() on run.OutputBaseUnitId equals unit.UnitId into units
            from unit in units.DefaultIfEmpty()
            join allocation in _context.RestockSourcingAllocations.AsNoTracking() on run.ProductionRunId equals allocation.ProductionRunId into allocations
            from allocation in allocations.DefaultIfEmpty()
            orderby run.CreatedAt descending, run.ProductionRunId descending
            select new ProductionRunListItemDto
            {
                ProductionRunId = run.ProductionRunId,
                StoreId = run.StoreId,
                StoreName = store.Name,
                RecipeName = recipe.Name,
                OutputName = prepared != null ? prepared.Name : "Đầu ra chưa xác định",
                ContractVersion = run.ContractVersion,
                RequestedRunCount = run.RequestedRunCount,
                PlannedBatchCount = run.PlannedBatchCount,
                ExpectedOutputBase = run.ExpectedOutputBase,
                OutputUnitCode = unit != null ? unit.UnitCode : string.Empty,
                Status = run.Status,
                CreatedAt = run.CreatedAt,
                CreatedByName = staff.FullName,
                RestockRequestId = allocation != null ? allocation.RestockRequestId : null
            })
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return ServiceResult<ProductionRunListDto>.Success(new ProductionRunListDto
        {
            Items = rows,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        });
    }

    public async Task<ServiceResult<ProductionRunDetailDto>> GetDetailAsync(int productionRunId, int accountId)
    {
        var run = await _context.ProductionRuns.AsNoTracking()
            .Include(x => x.Store)
            .Include(x => x.Recipe).ThenInclude(x => x.PreparedItem)
            .Include(x => x.CreatedByStaff)
            .Include(x => x.ActualInputs).ThenInclude(x => x.Ingredient)
            .Include(x => x.ActualInputs).ThenInclude(x => x.PreparedItem)
            .Include(x => x.ActualInputs).ThenInclude(x => x.BaseUnit)
            .Include(x => x.ActualOutput).ThenInclude(x => x!.RecordedByStaff)
            .Include(x => x.Transitions).ThenInclude(x => x.ActorStaff)
            .SingleOrDefaultAsync(x => x.ProductionRunId == productionRunId);
        if (run == null)
            return Failure<ProductionRunDetailDto>("Không tìm thấy lệnh sản xuất.", "PRODUCTION_RUN_NOT_FOUND");
        if (!await AllowedAsync(accountId, PermissionConstants.ProductionOrderView, run.StoreId))
            return Forbidden<ProductionRunDetailDto>();

        var unitCode = run.OutputBaseUnitId.HasValue
            ? await UnitCodeAsync(run.OutputBaseUnitId.Value)
            : string.Empty;
        var allocation = await _context.RestockSourcingAllocations.AsNoTracking()
            .Include(x => x.RestockRequest)
            .SingleOrDefaultAsync(x => x.ProductionRunId == run.ProductionRunId);
        var postedQuantities = allocation == null
            ? new List<decimal>()
            : await _context.RestockFulfillmentPostings.AsNoTracking()
                .Where(x => x.RestockRequestId == allocation.RestockRequestId)
                .Select(x => x.Quantity)
                .ToListAsync();
        var actorStaffId = await _context.Staffs.AsNoTracking()
            .Where(x => x.AccountId == accountId && x.Active)
            .Select(x => (int?)x.StaffId)
            .FirstOrDefaultAsync();

        var dto = new ProductionRunDetailDto
        {
            ProductionRunId = run.ProductionRunId,
            ContractVersion = run.ContractVersion,
            StoreId = run.StoreId,
            StoreName = run.Store.Name,
            RecipeId = run.RecipeId,
            RecipeName = run.Recipe.Name,
            OutputName = run.Recipe.PreparedItem?.Name ?? "Đầu ra chưa xác định",
            PlannedBatchCount = run.PlannedBatchCount,
            RequestedRunCount = run.RequestedRunCount,
            ExpectedOutputPerBatchBase = run.ExpectedOutputPerBatchBase,
            ExpectedOutputBase = run.ExpectedOutputBase,
            OutputUnitCode = unitCode,
            YieldVarianceTolerancePercent = run.YieldVarianceTolerancePercent
                ?? _productionOptions.DefaultYieldVarianceTolerancePercent,
            Status = run.Status,
            Notes = run.Notes,
            CreatedAt = run.CreatedAt,
            CreatedByName = run.CreatedByStaff.FullName,
            CompletedAt = run.CompletedAt,
            TotalInputCost = run.TotalInputCost,
            OutputUnitCost = run.OutputUnitCost,
            RestockRequestId = allocation?.RestockRequestId,
            RestockReferenceCode = allocation?.RestockRequest.ReferenceCode,
            RestockRequestedQuantity = allocation?.RestockRequest.RequestedQuantity,
            RestockFulfilledQuantity = postedQuantities.Sum(),
            Output = run.ActualOutput == null ? null : new ProductionRunOutputDetailDto
            {
                ExpectedOutputBase = run.ActualOutput.ExpectedOutputBase,
                ActualProducedBase = run.ActualOutput.ActualProducedBase,
                AcceptedOutputBase = run.ActualOutput.AcceptedOutputBase,
                RejectedOutputBase = run.ActualOutput.RejectedOutputBase,
                VariancePercent = run.ActualOutput.VariancePercent,
                Reason = run.ActualOutput.Reason,
                RecordedByName = run.ActualOutput.RecordedByStaff.FullName,
                RecordedAtUtc = run.ActualOutput.RecordedAtUtc
            },
            Transitions = run.Transitions
                .OrderByDescending(x => x.OccurredAtUtc)
                .Select(x => new ProductionRunTransitionDetailDto
                {
                    FromStatusLabel = ProductionRunDisplay.Status(x.FromStatus),
                    ToStatusLabel = ProductionRunDisplay.Status(x.ToStatus),
                    ActorName = x.ActorStaff.FullName,
                    OccurredAtUtc = x.OccurredAtUtc,
                    Reason = x.Reason
                }).ToList()
        };
        dto.Inputs = await BuildInputsAsync(run);
        dto.CanRelease = run.ContractVersion == 2 && run.Status == ProductionRunStatus.Planned
            && await AllowedAsync(accountId, PermissionConstants.ProductionOrderRelease, run.StoreId);
        dto.CanStart = run.ContractVersion == 2 && run.Status == ProductionRunStatus.Released
            && await AllowedAsync(accountId, PermissionConstants.ProductionOrderStart, run.StoreId);
        dto.CanRecordActual = run.ContractVersion == 2 && run.Status == ProductionRunStatus.InProgress
            && await AllowedAsync(accountId, PermissionConstants.ProductionOrderRecordActual, run.StoreId);
        dto.CanApproveVariance = run.ContractVersion == 2
            && run.Status == ProductionRunStatus.AwaitingVarianceApproval
            && actorStaffId != run.ActualRecordedByStaffId
            && await AllowedAsync(accountId, PermissionConstants.ProductionOrderApproveVariance, run.StoreId);
        dto.CanAcceptOutput = run.ContractVersion == 2 && run.Status == ProductionRunStatus.AwaitingAcceptance
            && await AllowedAsync(accountId, PermissionConstants.ProductionOrderAcceptOutput, run.StoreId);
        dto.CanCancel = run.ContractVersion == 2
            && run.Status is ProductionRunStatus.Planned or ProductionRunStatus.Released
            && await AllowedAsync(accountId, PermissionConstants.ProductionOrderCancel, run.StoreId);
        return ServiceResult<ProductionRunDetailDto>.Success(dto);
    }

    private async Task<IReadOnlyList<ProductionRunInputDetailDto>> BuildInputsAsync(ProductionRun run)
    {
        if (run.ActualInputs.Count > 0)
        {
            return run.ActualInputs.OrderBy(x => x.ProductionRunInputActualId)
                .Select(x => new ProductionRunInputDetailDto
                {
                    IngredientId = x.IngredientId,
                    PreparedItemId = x.PreparedItemId,
                    Code = x.Ingredient?.Code ?? x.PreparedItem?.Code ?? string.Empty,
                    Name = x.Ingredient?.Name ?? x.PreparedItem?.Name ?? "Đầu vào",
                    BaseUnitId = x.BaseUnitId,
                    BaseUnitCode = x.BaseUnit.UnitCode,
                    PlannedBaseQuantity = x.PlannedBaseQuantity,
                    ActualBaseQuantity = x.ActualBaseQuantity
                }).ToList();
        }

        if (run.ContractVersion != 2 || !run.PlannedBatchCount.HasValue)
            return [];
        var details = await _context.RecipeDetails.AsNoTracking()
            .Where(x => x.RecipeId == run.RecipeId)
            .ToListAsync();
        var inputs = new List<ProductionRunInputDetailDto>();
        foreach (var detail in details)
        {
            var raw = detail.Quantity * run.PlannedBatchCount.Value;
            if (detail.IngredientId.HasValue)
            {
                var ingredient = await _context.Ingredients.AsNoTracking()
                    .SingleAsync(x => x.IngredientId == detail.IngredientId);
                var converted = await _unitConversion.ConvertAsync(
                    ingredient.IngredientId, raw, detail.UnitId, ingredient.BaseUnitId);
                if (!converted.IsSuccess)
                    continue;
                inputs.Add(new ProductionRunInputDetailDto
                {
                    IngredientId = ingredient.IngredientId,
                    Code = ingredient.Code,
                    Name = ingredient.Name,
                    BaseUnitId = ingredient.BaseUnitId,
                    BaseUnitCode = await UnitCodeAsync(ingredient.BaseUnitId),
                    PlannedBaseQuantity = converted.Data
                });
            }
            else if (detail.ChildRecipeId.HasValue)
            {
                var child = await _context.Recipes.AsNoTracking()
                    .Include(x => x.PreparedItem)
                    .SingleOrDefaultAsync(x => x.RecipeId == detail.ChildRecipeId);
                if (child?.PreparedItem == null)
                    continue;
                var converted = await _physicalConversion.ConvertAsync(
                    raw, detail.UnitId, child.PreparedItem.BaseUnitId);
                if (!converted.IsSuccess)
                    continue;
                inputs.Add(new ProductionRunInputDetailDto
                {
                    PreparedItemId = child.PreparedItem.PreparedItemId,
                    Code = child.PreparedItem.Code,
                    Name = child.PreparedItem.Name,
                    BaseUnitId = child.PreparedItem.BaseUnitId,
                    BaseUnitCode = await UnitCodeAsync(child.PreparedItem.BaseUnitId),
                    PlannedBaseQuantity = converted.Data
                });
            }
        }
        return inputs;
    }

    private Task<string> UnitCodeAsync(int unitId) => _context.Units.AsNoTracking()
        .Where(x => x.UnitId == unitId)
        .Select(x => x.UnitCode)
        .SingleAsync();

    private async Task<bool> AllowedAsync(int accountId, string permission, int storeId)
    {
        var result = await _permissions.HasPermissionAsync(accountId, permission, storeId);
        return result.IsSuccess && result.Data?.Allowed == true;
    }

    private static ServiceResult<T> Forbidden<T>()
        => Failure<T>("Bạn không có quyền xem lệnh sản xuất tại cửa hàng này.", "PRODUCTION_RUN_FORBIDDEN");

    private static ServiceResult<T> Failure<T>(string message, string code = "PRODUCTION_RUN_INVALID")
        => ServiceResult<T>.Failure(message, errorCode: code);
}
