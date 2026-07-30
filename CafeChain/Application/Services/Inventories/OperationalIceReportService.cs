using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Inventories;

public sealed class OperationalIceReportService : IOperationalIceReportService
{
    private readonly AppDbContext _context;
    private readonly IUnitConversionService _unitConversionService;

    public OperationalIceReportService(AppDbContext context, IUnitConversionService unitConversionService)
    {
        _context = context;
        _unitConversionService = unitConversionService;
    }

    public async Task<ServiceResult<OperationalIceReportDto>> BuildAsync(
        int iceAllocationId,
        CancellationToken cancellationToken = default)
    {
        var allocation = await _context.IceAllocations.AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.OperationalShift).ThenInclude(x => x.Store)
            .Include(x => x.OperationalShift).ThenInclude(x => x.ShiftLead)
            .Include(x => x.OperationalShift).ThenInclude(x => x.WorkShiftLinks)
            .Include(x => x.IcePolicy).ThenInclude(x => x.DisplayUnit)
            .Include(x => x.Ingredient)
            .Include(x => x.OpenedByStaff)
            .Include(x => x.ClosedByStaff)
            .Include(x => x.ReturnedByStaff)
            .Include(x => x.ReturnReceivedByStaff)
            .Include(x => x.OutgoingCarryOvers).ThenInclude(x => x.ToOperationalShift)
            .Include(x => x.OutgoingCarryOvers).ThenInclude(x => x.HandedOverByStaff)
            .Include(x => x.OutgoingCarryOvers).ThenInclude(x => x.ReceivedByStaff)
            .Include(x => x.IncomingCarryOvers).ThenInclude(x => x.FromOperationalShift)
            .Include(x => x.IncomingCarryOvers).ThenInclude(x => x.HandedOverByStaff)
            .Include(x => x.IncomingCarryOvers).ThenInclude(x => x.ReceivedByStaff)
            .Include(x => x.InventoryPostings).ThenInclude(x => x.ApprovedByStaff)
            .SingleOrDefaultAsync(x => x.IceAllocationId == iceAllocationId, cancellationToken);
        if (allocation == null)
        {
            return ServiceResult<OperationalIceReportDto>.Failure(
                "Không tìm thấy phân bổ đá để lập báo cáo.",
                errorCode: OperationalIceErrorCodes.NotFound);
        }

        var conversion = await _unitConversionService.ConvertAsync(
            allocation.IngredientId,
            1m,
            allocation.IcePolicy.DisplayUnitId);
        if (!conversion.IsSuccess || conversion.Data <= 0)
        {
            return ServiceResult<OperationalIceReportDto>.Failure(
                "Không thể lập báo cáo vì nguyên liệu đá chưa có quy đổi đơn vị hợp lệ.",
                errorCode: OperationalIceErrorCodes.InvalidRequest);
        }

        var factor = conversion.Data;
        var workShiftIds = allocation.OperationalShift.WorkShiftLinks
            .Select(x => x.WorkShiftId)
            .Distinct()
            .OrderBy(x => x)
            .ToArray();
        var movements = workShiftIds.Length == 0
            ? []
            : await (
                from movement in _context.InventoryTransactions.AsNoTracking()
                join order in _context.Orders.AsNoTracking()
                    on movement.ReferenceOrderId equals (int?)order.OrderId
                where workShiftIds.Contains(order.WorkShiftId!.Value)
                      && movement.StoreInventoryId == allocation.StoreInventoryId
                      && (movement.Type == InventoryTransactionTypeEnum.SALES_DEDUCTION
                          || movement.Type == InventoryTransactionTypeEnum.SALES_RETURN)
                select new LedgerMovement(movement.Type, movement.Quantity, movement.TotalCost))
                .ToListAsync(cancellationToken);

        var ledgerTheoreticalBase = Math.Max(0m,
            movements.Where(x => x.Type == InventoryTransactionTypeEnum.SALES_DEDUCTION).Sum(x => x.Quantity)
            - movements.Where(x => x.Type == InventoryTransactionTypeEnum.SALES_RETURN).Sum(x => x.Quantity));
        var hasCompleteTheoreticalCost = movements.Count == 0 || movements.All(x => x.TotalCost.HasValue);
        decimal? theoreticalCost = hasCompleteTheoreticalCost
            ? Math.Max(0m,
                movements.Where(x => x.Type == InventoryTransactionTypeEnum.SALES_DEDUCTION).Sum(x => x.TotalCost ?? 0m)
                - movements.Where(x => x.Type == InventoryTransactionTypeEnum.SALES_RETURN).Sum(x => x.TotalCost ?? 0m))
            : null;

        var varianceBase = allocation.VarianceQuantity;
        var variancePosting = allocation.InventoryPostings
            .Where(x => x.PostingType == IcePostingTypes.VarianceOut)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefault();
        decimal? varianceCost = varianceBase switch
        {
            null => null,
            0 => 0m,
            > 0 => variancePosting?.TotalCost,
            _ => null
        };
        decimal? actualCost = allocation.ActualUsageQuantity switch
        {
            null => null,
            _ when varianceBase is < 0 => null,
            _ when theoreticalCost.HasValue && varianceCost.HasValue => theoreticalCost.Value + varianceCost.Value,
            _ => null
        };

        var costStatus = ResolveCostStatus(allocation.ActualUsageQuantity, varianceBase, theoreticalCost, varianceCost);
        var approvedBy = allocation.InventoryPostings.OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => x.ApprovedByStaff.FullName)
            .FirstOrDefault();

        var report = new OperationalIceReportDto
        {
            IceAllocationId = allocation.IceAllocationId,
            OperationalShiftId = allocation.OperationalShiftId,
            StoreId = allocation.OperationalShift.StoreId,
            StoreName = allocation.OperationalShift.Store.Name,
            BusinessDate = allocation.OperationalShift.BusinessDate,
            OperationalShiftName = allocation.OperationalShift.Name,
            StartAtUtc = allocation.OperationalShift.StartAtUtc,
            EndAtUtc = allocation.OperationalShift.EndAtUtc,
            Status = allocation.Status,
            IngredientName = allocation.Ingredient.Name,
            UnitName = PhysicalUnitConversionRegistry.NormalizeUnitCode(
                allocation.IcePolicy.DisplayUnit.UnitCode),
            OpeningCarry = allocation.OpeningCarryQuantity / factor,
            InitialIssued = allocation.InitialIssuedQuantity / factor,
            SupplementalIssued = allocation.SupplementalIssuedQuantity / factor,
            ReturnedQuantity = allocation.ReturnedQuantity / factor,
            ClosingCarry = allocation.ClosingCarryQuantity / factor,
            ActualUsage = allocation.ActualUsageQuantity / factor,
            TheoreticalUsage = allocation.TheoreticalUsageQuantity / factor,
            LedgerTheoreticalUsage = ledgerTheoreticalBase / factor,
            Variance = allocation.VarianceQuantity / factor,
            TheoreticalCost = theoreticalCost,
            VarianceCost = varianceCost,
            ActualCost = actualCost,
            CostStatus = costStatus,
            IssuedBy = allocation.OpenedByStaff?.FullName,
            ShiftLead = allocation.OperationalShift.ShiftLead?.FullName,
            ReturnedBy = allocation.ReturnedByStaff?.FullName,
            ReturnReceivedBy = allocation.ReturnReceivedByStaff?.FullName,
            ClosedBy = allocation.ClosedByStaff?.FullName,
            ApprovedBy = approvedBy,
            CloseReason = allocation.CloseReason,
            ReconciliationReason = allocation.ReconciliationReason,
            WorkShiftIds = workShiftIds,
            CarryOvers = allocation.OutgoingCarryOvers.Select(x => new OperationalIceReportCarryDto
                {
                    Direction = "Giao",
                    OtherShiftName = x.ToOperationalShift.Name,
                    Quantity = x.Quantity / factor,
                    Status = x.Status,
                    HandedOverBy = x.HandedOverByStaff.FullName,
                    ReceivedBy = x.ReceivedByStaff?.FullName,
                    ConfirmedAtUtc = x.ConfirmedAtUtc
                })
                .Concat(allocation.IncomingCarryOvers.Select(x => new OperationalIceReportCarryDto
                {
                    Direction = "Nhận",
                    OtherShiftName = x.FromOperationalShift.Name,
                    Quantity = x.Quantity / factor,
                    Status = x.Status,
                    HandedOverBy = x.HandedOverByStaff.FullName,
                    ReceivedBy = x.ReceivedByStaff?.FullName,
                    ConfirmedAtUtc = x.ConfirmedAtUtc
                }))
                .OrderBy(x => x.ConfirmedAtUtc)
                .ToList(),
            InventoryPostings = allocation.InventoryPostings.OrderBy(x => x.CreatedAtUtc)
                .Select(x => new OperationalIceReportPostingDto
                {
                    IceInventoryPostingId = x.IceInventoryPostingId,
                    PostingType = x.PostingType,
                    IdempotencyKey = x.IdempotencyKey,
                    InventoryTransactionId = x.InventoryTransactionId,
                    Quantity = x.Quantity / factor,
                    UnitCost = x.UnitCost * factor,
                    TotalCost = x.TotalCost,
                    ApprovedBy = x.ApprovedByStaff.FullName,
                    Reason = x.Reason,
                    CreatedAtUtc = x.CreatedAtUtc
                }).ToList()
        };

        return ServiceResult<OperationalIceReportDto>.Success(report, "Đã lập báo cáo đá từ dữ liệu ledger.");
    }

    private static string ResolveCostStatus(
        decimal? actualUsage,
        decimal? variance,
        decimal? theoreticalCost,
        decimal? varianceCost)
    {
        if (!actualUsage.HasValue)
            return "Chưa chốt ca";
        if (!theoreticalCost.HasValue)
            return "Thiếu giá vốn FIFO/ledger cho giao dịch bán";
        if (variance is < 0)
            return "Cần đối soát; hệ thống không ghi tăng tồn tự động";
        if (variance is > 0 && !varianceCost.HasValue)
            return "Chênh lệch chưa có bút toán giá vốn hoàn chỉnh";
        return "Đầy đủ theo FIFO/ledger";
    }

    private sealed record LedgerMovement(InventoryTransactionTypeEnum Type, decimal Quantity, decimal? TotalCost);
}
