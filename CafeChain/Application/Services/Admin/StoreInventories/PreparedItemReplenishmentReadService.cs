using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Production;
using CafeChain.Application.DTOs.Admin.Replenishment;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.Admin.StoreInventories;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Inventories;
using CafeChain.Data;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Enums.Unit;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Admin.StoreInventories;

public sealed class PreparedItemReplenishmentReadService : IPreparedItemReplenishmentReadService
{
    private const int MaxOpenRunLimit = 20;

    private static readonly ProductionRunStatus[] CreditableStatuses =
    [
        ProductionRunStatus.Planned,
        ProductionRunStatus.Released,
        ProductionRunStatus.InProgress,
        ProductionRunStatus.AwaitingVarianceApproval,
        ProductionRunStatus.AwaitingAcceptance
    ];

    private readonly AppDbContext _context;
    private readonly IAdminPermissionService _permissions;

    public PreparedItemReplenishmentReadService(
        AppDbContext context,
        IAdminPermissionService permissions)
    {
        _context = context;
        _permissions = permissions;
    }

    public async Task<ServiceResult<PreparedItemReplenishmentDto>> GetAsync(
        int accountId,
        int storeId,
        int preparedItemId,
        int openRunLimit = 5)
    {
        if (accountId <= 0 || storeId <= 0 || preparedItemId <= 0)
            return ServiceResult<PreparedItemReplenishmentDto>.Failure("Thông tin tra cứu nhu cầu bổ sung không hợp lệ.");

        var permission = await _permissions.HasPermissionAsync(
            accountId,
            PermissionConstants.InventoryThresholdView,
            storeId);
        if (!permission.IsSuccess || permission.Data?.Allowed != true)
            return ServiceResult<PreparedItemReplenishmentDto>.Failure("Bạn không có quyền xem nhu cầu bổ sung tại chi nhánh này.");

        openRunLimit = Math.Clamp(openRunLimit, 1, MaxOpenRunLimit);

        var stock = await _context.StoreInventories
            .AsNoTracking()
            .Where(x => x.StoreId == storeId
                && x.PreparedItemId == preparedItemId
                && x.BtpIdentityState == BtpIdentityState.Canonical
                && x.QuantitySemanticsStatus == InventoryQuantitySemanticsStatus.BaseUnitConfirmed
                && x.SupersededByStoreInventoryId == null)
            .Select(x => new
            {
                x.StoreInventoryId,
                x.StoreId,
                StoreName = x.Store.Name,
                PreparedItemId = x.PreparedItemId!.Value,
                PreparedItemName = x.PreparedItem!.Name,
                PreparedItemCode = x.PreparedItem.Code,
                BaseUnitId = x.PreparedItem.BaseUnitId,
                BaseUnitCode = x.PreparedItem.BaseUnit.UnitCode,
                BaseUnitName = x.PreparedItem.BaseUnit.Name,
                BaseUnitType = x.PreparedItem.BaseUnit.Type,
                x.AvailableQty,
                x.ReservedQty,
                x.MinStockLevel,
                x.TargetStockLevel,
                x.RowVersion
            })
            .SingleOrDefaultAsync();

        if (stock == null)
        {
            return ServiceResult<PreparedItemReplenishmentDto>.Failure(
                "Không tìm thấy tồn kho bán thành phẩm chuẩn tại chi nhánh đã chọn.",
                errorCode: "CANONICAL_PREPARED_ITEM_STOCK_NOT_FOUND");
        }

        var activeAlert = await _context.StockAlerts
            .AsNoTracking()
            .Where(x => x.StoreId == storeId
                && x.PreparedItemId == preparedItemId
                && StockAlertStatuses.ActiveValues.Contains(x.Status))
            .OrderByDescending(x => x.UpdatedAt)
            .ThenByDescending(x => x.StockAlertId)
            .Select(x => new PreparedItemAlertSummaryDto
            {
                StockAlertId = x.StockAlertId,
                StatusLabel = x.Status == StockAlertStatuses.Confirmed ? "Đã xác nhận" : "Đang mở",
                UpdatedAtUtc = x.UpdatedAt
            })
            .FirstOrDefaultAsync();

        var activeRequest = await _context.RestockRequests
            .AsNoTracking()
            .Where(x => x.StoreId == storeId
                && x.PreparedItemId == preparedItemId
                && RestockRequestStatuses.ActiveValues.Contains(x.Status))
            .OrderByDescending(x => x.UpdatedAt)
            .ThenByDescending(x => x.RestockRequestId)
            .Select(x => new PreparedItemRequestSummaryDto
            {
                RestockRequestId = x.RestockRequestId,
                ReferenceCode = x.ReferenceCode,
                StatusLabel = x.Status == RestockRequestStatuses.Draft ? "Bản nháp"
                    : x.Status == RestockRequestStatuses.Submitted ? "Đã gửi"
                    : x.Status == RestockRequestStatuses.Processing ? "Đang xử lý"
                    : "Đã bổ sung một phần",
                UpdatedAtUtc = x.UpdatedAt
            })
            .FirstOrDefaultAsync();

        var openAllocations = _context.RestockSourcingAllocations
            .AsNoTracking()
            .Where(x => x.RestockRequest.StoreId == storeId
                && x.RestockRequest.PreparedItemId == preparedItemId
                && x.DecisionType == RestockSourcingDecisionTypes.Production
                && x.Status == RestockSourcingAllocationStatuses.Active
                && x.ProductionRunId.HasValue
                && x.ProductionRun != null
                && CreditableStatuses.Contains(x.ProductionRun.Status));

        List<CoverageByUnit> coverageByUnit;
        if (_context.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
        {
            // SQLite cannot SUM decimal. Production SQL Server executes the grouped query below.
            var sqliteRows = await openAllocations
                .Select(x => new
                {
                    x.ProcurementUnitId,
                    x.ProcurementUnit.UnitCode,
                    x.ProcurementUnit.Type,
                    x.ProcurementQuantity
                })
                .ToListAsync();
            coverageByUnit = sqliteRows
                .GroupBy(x => new { x.ProcurementUnitId, x.UnitCode, x.Type })
                .Select(x => new CoverageByUnit(
                    x.Key.ProcurementUnitId,
                    x.Key.UnitCode,
                    x.Key.Type,
                    x.Sum(a => a.ProcurementQuantity)))
                .ToList();
        }
        else
        {
            coverageByUnit = await openAllocations
                .GroupBy(x => new
                {
                    x.ProcurementUnitId,
                    x.ProcurementUnit.UnitCode,
                    x.ProcurementUnit.Type
                })
                .Select(x => new CoverageByUnit(
                    x.Key.ProcurementUnitId,
                    x.Key.UnitCode,
                    x.Key.Type,
                    x.Sum(a => a.ProcurementQuantity)))
                .ToListAsync();
        }

        var coverageAvailable = TryNormalizeCoverage(
            coverageByUnit,
            stock.BaseUnitId,
            stock.BaseUnitCode,
            stock.BaseUnitType,
            out var openCoverageBase);

        var openRunTotal = await openAllocations
            .Select(x => x.ProductionRunId!.Value)
            .Distinct()
            .CountAsync();

        var openRunRows = await openAllocations
            .OrderByDescending(x => x.ProductionRun!.CreatedAt)
            .ThenByDescending(x => x.ProductionRunId)
            .Take(openRunLimit)
            .Select(x => new OpenRunRow(
                x.ProductionRunId!.Value,
                x.ProductionRun!.RecipeId,
                x.ProductionRun.Status,
                x.ProcurementQuantity,
                x.ProcurementUnitId,
                x.ProcurementUnit.UnitCode,
                x.ProcurementUnit.Type,
                x.ProductionRun.CreatedAt))
            .ToListAsync();

        var usable = stock.AvailableQty - stock.ReservedQty;
        var grossNeed = stock.TargetStockLevel.HasValue
            ? Math.Max(stock.TargetStockLevel.Value - usable, 0m)
            : (decimal?)null;
        var netNeed = grossNeed.HasValue && coverageAvailable
            ? Math.Max(grossNeed.Value - openCoverageBase, 0m)
            : (decimal?)null;

        var status = PreparedItemReplenishmentDataStatuses.Ready;
        var message = "Dữ liệu nhu cầu bổ sung đã sẵn sàng.";
        if (!stock.TargetStockLevel.HasValue)
        {
            status = PreparedItemReplenishmentDataStatuses.TargetNotConfigured;
            message = "Cần cấu hình mức tồn mục tiêu trước khi tính số lượng cần bổ sung.";
        }
        else if (!coverageAvailable)
        {
            status = PreparedItemReplenishmentDataStatuses.OpenCoverageUnitIncompatible;
            message = "Không thể quy đổi nguồn sản xuất đang mở về đơn vị tồn kho cơ sở.";
        }

        return ServiceResult<PreparedItemReplenishmentDto>.Success(new PreparedItemReplenishmentDto
        {
            StoreInventoryId = stock.StoreInventoryId,
            StoreId = stock.StoreId,
            StoreName = stock.StoreName,
            PreparedItemId = stock.PreparedItemId,
            PreparedItemName = stock.PreparedItemName,
            PreparedItemCode = stock.PreparedItemCode,
            BaseUnitId = stock.BaseUnitId,
            BaseUnitCode = stock.BaseUnitCode,
            BaseUnitName = stock.BaseUnitName,
            OnHandBase = stock.AvailableQty,
            ReservedBase = stock.ReservedQty,
            UsableBase = usable,
            LowThresholdBase = stock.MinStockLevel,
            TargetStockBase = stock.TargetStockLevel,
            IsLow = stock.MinStockLevel.HasValue && usable < stock.MinStockLevel.Value,
            GrossNeedBase = grossNeed,
            OpenProductionCoverageBase = coverageAvailable ? openCoverageBase : null,
            NetNeedBase = netNeed,
            ActiveAlert = activeAlert,
            ActiveRestockRequest = activeRequest,
            OpenProductionRuns = MapOpenRuns(
                openRunRows,
                stock.BaseUnitId,
                stock.BaseUnitCode,
                stock.BaseUnitType),
            OpenProductionRunTotal = openRunTotal,
            HasMoreOpenProductionRuns = openRunTotal > openRunRows.Count,
            DataStatus = status,
            BusinessMessageVi = message,
            RowVersion = Convert.ToBase64String(stock.RowVersion ?? [])
        });
    }

    private static bool TryNormalizeCoverage(
        IEnumerable<CoverageByUnit> rows,
        int baseUnitId,
        string baseUnitCode,
        UnitType baseUnitType,
        out decimal total)
    {
        total = 0m;
        foreach (var row in rows)
        {
            if (!TryConvertToBase(
                    row.Quantity,
                    row.UnitId,
                    row.UnitCode,
                    row.UnitType,
                    baseUnitId,
                    baseUnitCode,
                    baseUnitType,
                    out var converted))
            {
                total = 0m;
                return false;
            }

            total += converted;
        }

        return true;
    }

    private static IReadOnlyList<PreparedItemOpenProductionRunDto> MapOpenRuns(
        IEnumerable<OpenRunRow> rows,
        int baseUnitId,
        string baseUnitCode,
        UnitType baseUnitType)
    {
        var result = new List<PreparedItemOpenProductionRunDto>();
        foreach (var row in rows)
        {
            if (!TryConvertToBase(
                    row.Quantity,
                    row.UnitId,
                    row.UnitCode,
                    row.UnitType,
                    baseUnitId,
                    baseUnitCode,
                    baseUnitType,
                    out var coverageBase))
            {
                continue;
            }

            result.Add(new PreparedItemOpenProductionRunDto
            {
                ProductionRunId = row.ProductionRunId,
                RecipeId = row.RecipeId,
                StatusLabel = ProductionRunDisplay.Status(row.Status),
                CoverageBase = coverageBase,
                BaseUnitCode = baseUnitCode,
                CreatedAtUtc = row.CreatedAtUtc
            });
        }

        return result;
    }

    private static bool TryConvertToBase(
        decimal quantity,
        int unitId,
        string unitCode,
        UnitType unitType,
        int baseUnitId,
        string baseUnitCode,
        UnitType baseUnitType,
        out decimal converted)
    {
        if (unitId == baseUnitId)
        {
            converted = quantity;
            return true;
        }

        if (PhysicalUnitConversionRegistry.TryGetPairFactor(
                unitCode,
                baseUnitCode,
                unitType,
                baseUnitType,
                out var factor))
        {
            converted = quantity * factor;
            return true;
        }

        converted = 0m;
        return false;
    }

    private sealed record CoverageByUnit(int UnitId, string UnitCode, UnitType UnitType, decimal Quantity);

    private sealed record OpenRunRow(
        int ProductionRunId,
        int RecipeId,
        ProductionRunStatus Status,
        decimal Quantity,
        int UnitId,
        string UnitCode,
        UnitType UnitType,
        DateTime CreatedAtUtc);
}
