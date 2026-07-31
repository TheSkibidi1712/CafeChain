using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Inventories.Procurement;
using CafeChain.Models.Inventories.Stock;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Inventories;

public sealed class SupplierQualityService : ISupplierQualityService
{
    private readonly AppDbContext _context;
    private readonly IScopeAuthorizationService _scopeAuthorization;

    public SupplierQualityService(AppDbContext context, IScopeAuthorizationService scopeAuthorization)
    {
        _context = context;
        _scopeAuthorization = scopeAuthorization;
    }

    public async Task<ServiceResult<SupplierReceiptIssueContextDto>> GetReceiptContextAsync(
        int branchReceiptLineId,
        int actorStaffId,
        IReadOnlyCollection<string> roles)
    {
        var line = await LoadReceiptLineAsync(branchReceiptLineId);
        if (line?.PurchaseOrderLine == null || !line.BranchReceipt.SupplierId.HasValue)
            return ServiceResult<SupplierReceiptIssueContextDto>.Failure(
                "Dòng nhận không liên kết với đơn mua hàng nhà cung cấp.");
        if (line.BranchReceipt.Status != BranchReceiptStatuses.Confirmed)
            return ServiceResult<SupplierReceiptIssueContextDto>.Failure(
                "Chỉ ghi nhận sự cố từ phiếu nhận đã xác nhận.");
        if (!await CanAccessAsync(actorStaffId, line.BranchReceipt.StoreId, roles))
            return ServiceResult<SupplierReceiptIssueContextDto>.Failure(
                "Bạn không có quyền truy cập dữ liệu nhà cung cấp của cửa hàng này.");

        return ServiceResult<SupplierReceiptIssueContextDto>.Success(MapContext(line));
    }

    public async Task<ServiceResult<SupplierReceiptIssueListItemDto>> CreateIssueAsync(
        CreateSupplierReceiptIssueRequest input,
        int actorStaffId,
        IReadOnlyCollection<string> roles)
    {
        if (!SupplierReceiptIssueTypes.All.Contains(input.IssueType))
            return Fail("Loại sự cố nhà cung cấp không hợp lệ.");
        if (string.IsNullOrWhiteSpace(input.Description))
            return Fail("Mô tả sự cố là bắt buộc.");
        if (input.AffectedBaseQuantity < 0)
            return Fail("Số lượng ảnh hưởng không được âm.");

        var contextResult = await GetReceiptContextAsync(input.BranchReceiptLineId, actorStaffId, roles);
        if (!contextResult.IsSuccess || contextResult.Data == null)
            return Fail(contextResult.Message);
        var receiptContext = contextResult.Data;
        var maxAffected = receiptContext.AcceptedBaseQuantity + receiptContext.RejectedBaseQuantity;
        if (input.AffectedBaseQuantity > maxAffected)
            return Fail($"Số lượng ảnh hưởng vượt tổng lượng đã xử lý {maxAffected:N3}.");

        var duplicateExists = await _context.SupplierReceiptIssues
            .AsNoTracking()
            .AnyAsync(x => x.BranchReceiptLineId == input.BranchReceiptLineId
                && x.IssueType == input.IssueType
                && (x.Status == SupplierReceiptIssueStatuses.Open
                    || x.Status == SupplierReceiptIssueStatuses.UnderReview));
        if (duplicateExists)
            return Fail("Dòng nhận đã có sự cố cùng loại đang được xử lý.", "SUPPLIER_ISSUE_ACTIVE_DUPLICATE");

        var now = DateTime.UtcNow;
        var issue = new SupplierReceiptIssue
        {
            SupplierId = receiptContext.SupplierId,
            StoreId = receiptContext.StoreId,
            PurchaseOrderId = receiptContext.PurchaseOrderId,
            PurchaseOrderLineId = receiptContext.PurchaseOrderLineId,
            BranchReceiptId = receiptContext.BranchReceiptId,
            BranchReceiptLineId = receiptContext.BranchReceiptLineId,
            IssueType = input.IssueType,
            Status = SupplierReceiptIssueStatuses.Open,
            AffectedBaseQuantity = input.AffectedBaseQuantity,
            Description = Trim(input.Description, 1000)!,
            ReportedByStaffId = actorStaffId,
            ReportedAtUtc = now,
            UpdatedAtUtc = now,
            RowVersion = new byte[] { 0 }
        };
        issue.Transitions.Add(new SupplierReceiptIssueTransition
        {
            PreviousStatus = "CREATED",
            NewStatus = SupplierReceiptIssueStatuses.Open,
            ActorStaffId = actorStaffId,
            Reason = "Ghi nhận sự cố từ phiếu nhận đã xác nhận.",
            OccurredAtUtc = now
        });
        _context.SupplierReceiptIssues.Add(issue);
        await _context.SaveChangesAsync();
        return ServiceResult<SupplierReceiptIssueListItemDto>.Success(
            await MapIssueAsync(issue.SupplierReceiptIssueId),
            "Đã ghi nhận sự cố nhà cung cấp.");
    }

    public async Task<ServiceResult<SupplierReceiptIssueListItemDto>> TransitionAsync(
        int issueId,
        SupplierReceiptIssueTransitionRequest input,
        int actorStaffId,
        IReadOnlyCollection<string> roles)
    {
        if (!SupplierReceiptIssueStatuses.All.Contains(input.TargetStatus))
            return Fail("Trạng thái sự cố không hợp lệ.");
        if (string.IsNullOrWhiteSpace(input.Reason))
            return Fail(input.TargetStatus == SupplierReceiptIssueStatuses.Dismissed
                ? "Lý do bỏ qua sự cố là bắt buộc."
                : "Lý do cập nhật trạng thái là bắt buộc để lưu audit.");

        var issue = await _context.SupplierReceiptIssues
            .SingleOrDefaultAsync(x => x.SupplierReceiptIssueId == issueId);
        if (issue == null) return Fail("Không tìm thấy sự cố nhà cung cấp.");
        if (!await CanAccessAsync(actorStaffId, issue.StoreId, roles))
            return Fail("Bạn không có quyền cập nhật sự cố của cửa hàng này.");
        if (!CanTransition(issue.Status, input.TargetStatus))
            return Fail($"Không thể chuyển sự cố từ {issue.Status} sang {input.TargetStatus}.");

        if (!TryParseRowVersion(input.RowVersion, out var expectedVersion))
            return Fail("Thiếu phiên bản dữ liệu. Vui lòng tải lại trang.", BranchReceiptErrorCodes.ValidationRowVersionRequired);
        if (!issue.RowVersion.SequenceEqual(expectedVersion))
            return Fail("Sự cố vừa được cập nhật bởi người khác. Vui lòng tải lại.", BranchReceiptErrorCodes.ResourceChanged);
        _context.Entry(issue).Property(x => x.RowVersion).OriginalValue = expectedVersion;

        var previous = issue.Status;
        var now = DateTime.UtcNow;
        var reason = Trim(input.Reason, 1000)!;
        issue.Status = input.TargetStatus;
        issue.UpdatedAtUtc = now;
        switch (input.TargetStatus)
        {
            case SupplierReceiptIssueStatuses.Resolved:
                issue.ResolutionNote = reason;
                issue.ResolvedByStaffId = actorStaffId;
                issue.ResolvedAtUtc = now;
                break;
            case SupplierReceiptIssueStatuses.Dismissed:
                issue.DismissReason = Trim(reason, 500);
                issue.DismissedByStaffId = actorStaffId;
                issue.DismissedAtUtc = now;
                break;
            case SupplierReceiptIssueStatuses.Closed:
                issue.ClosedAtUtc = now;
                break;
        }
        _context.SupplierReceiptIssueTransitions.Add(new SupplierReceiptIssueTransition
        {
            SupplierReceiptIssueId = issue.SupplierReceiptIssueId,
            PreviousStatus = previous,
            NewStatus = input.TargetStatus,
            ActorStaffId = actorStaffId,
            Reason = reason,
            OccurredAtUtc = now
        });

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Fail("Sự cố vừa được cập nhật bởi người khác. Vui lòng tải lại.", BranchReceiptErrorCodes.ResourceChanged);
        }

        return ServiceResult<SupplierReceiptIssueListItemDto>.Success(
            await MapIssueAsync(issue.SupplierReceiptIssueId),
            "Đã cập nhật trạng thái sự cố.");
    }

    public async Task<ServiceResult<SupplierQualityDashboardDto>> GetDashboardAsync(
        int storeId,
        int? supplierId,
        DateTime fromUtc,
        DateTime toUtc,
        int actorStaffId,
        IReadOnlyCollection<string> roles)
    {
        if (storeId <= 0 || fromUtc >= toUtc || toUtc - fromUtc > TimeSpan.FromDays(366))
            return ServiceResult<SupplierQualityDashboardDto>.Failure("Cửa hàng hoặc khoảng thời gian không hợp lệ.");
        if (!await CanAccessAsync(actorStaffId, storeId, roles))
            return ServiceResult<SupplierQualityDashboardDto>.Failure(
                "Bạn không có quyền xem chất lượng nhà cung cấp của cửa hàng này.");

        var issueQuery = _context.SupplierReceiptIssues.AsNoTracking()
            .Where(x => x.StoreId == storeId
                && x.BranchReceipt.ReceivedAt >= fromUtc
                && x.BranchReceipt.ReceivedAt < toUtc);
        if (supplierId.HasValue) issueQuery = issueQuery.Where(x => x.SupplierId == supplierId.Value);
        var issueIds = await issueQuery.OrderByDescending(x => x.ReportedAtUtc)
            .Select(x => x.SupplierReceiptIssueId)
            .ToListAsync();
        var issues = new List<SupplierReceiptIssueListItemDto>();
        foreach (var id in issueIds) issues.Add(await MapIssueAsync(id));

        var dashboard = new SupplierQualityDashboardDto
        {
            StoreId = storeId,
            SupplierId = supplierId,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            Issues = issues
        };
        if (supplierId.HasValue)
            dashboard.Performance = await CalculatePerformanceAsync(storeId, supplierId.Value, fromUtc, toUtc);
        return ServiceResult<SupplierQualityDashboardDto>.Success(dashboard);
    }

    private async Task<SupplierPerformanceDto> CalculatePerformanceAsync(
        int storeId,
        int supplierId,
        DateTime fromUtc,
        DateTime toUtc)
    {
        var confirmedReceipts = await _context.BranchReceipts.AsNoTracking()
            .Where(x => x.StoreId == storeId && x.SupplierId == supplierId
                && x.Status == BranchReceiptStatuses.Confirmed
                && x.ReceivedAt >= fromUtc && x.ReceivedAt < toUtc)
            .Select(x => new { x.BranchReceiptId, x.ReceivedAt })
            .ToListAsync();

        var orders = await _context.PurchaseOrders.AsNoTracking()
            .Where(x => x.StoreId == storeId && x.SupplierId == supplierId
                && x.Status == PurchaseOrderStatuses.Completed
                && x.Lines.Any(line => line.ReceiptPostings.Any(posting =>
                    posting.BranchReceiptLine.BranchReceipt.Status == BranchReceiptStatuses.Confirmed
                    && posting.BranchReceiptLine.BranchReceipt.ReceivedAt >= fromUtc
                    && posting.BranchReceiptLine.BranchReceipt.ReceivedAt < toUtc)))
            .Include(x => x.Lines)
                .ThenInclude(x => x.ReceiptPostings)
                    .ThenInclude(x => x.BranchReceiptLine)
                        .ThenInclude(x => x.BranchReceipt)
            .AsSplitQuery()
            .ToListAsync();
        var deliveries = orders.Select(order => new
            {
                Order = order,
                ConfirmedPostings = order.Lines.SelectMany(x => x.ReceiptPostings)
                    .Where(x => x.BranchReceiptLine.BranchReceipt.Status == BranchReceiptStatuses.Confirmed)
                    .ToList()
            })
            .Where(x => x.ConfirmedPostings.Count > 0)
            .Select(x => new
            {
                x.Order,
                x.ConfirmedPostings,
                DeliveryAt = x.ConfirmedPostings.Max(p => p.BranchReceiptLine.BranchReceipt.ReceivedAt)
            })
            .Where(x => x.DeliveryAt >= fromUtc && x.DeliveryAt < toUtc)
            .ToList();

        var expectedSamples = deliveries.Where(x => x.Order.ExpectedDeliveryAtUtc.HasValue).ToList();
        var onTimeCount = expectedSamples.Count(x => x.DeliveryAt <= x.Order.ExpectedDeliveryAtUtc!.Value);
        var ordered = deliveries.Sum(x => x.Order.Lines.Sum(l => l.OrderedBaseQuantity));
        var accepted = deliveries.Sum(x => x.ConfirmedPostings.Sum(p => p.AcceptedBaseQuantity));
        var rejected = deliveries.Sum(x => x.ConfirmedPostings.Sum(p => p.RejectedBaseQuantity));
        var receiptIds = confirmedReceipts.Select(x => x.BranchReceiptId).ToArray();
        var receiptsWithIssue = receiptIds.Length == 0
            ? 0
            : await _context.SupplierReceiptIssues.AsNoTracking()
                .Where(x => receiptIds.Contains(x.BranchReceiptId)
                    && x.Status != SupplierReceiptIssueStatuses.Dismissed
                    && x.DismissReason == null)
                .Select(x => x.BranchReceiptId)
                .Distinct()
                .CountAsync();
        var delayDays = expectedSamples.Select(x => Math.Max(
            0d,
            (x.DeliveryAt - x.Order.ExpectedDeliveryAtUtc!.Value).TotalDays)).ToList();

        var performance = new SupplierPerformanceDto
        {
            StoreId = storeId,
            SupplierId = supplierId,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            CompletedDeliveryCount = deliveries.Count,
            ConfirmedReceiptCount = confirmedReceipts.Count,
            ExpectedDateSampleCount = expectedSamples.Count,
            OnTimeRate = Percentage(onTimeCount, expectedSamples.Count),
            FillRate = Percentage(accepted, ordered),
            RejectionRate = Percentage(rejected, accepted + rejected),
            IssueRate = Percentage(receiptsWithIssue, confirmedReceipts.Count),
            AverageDelayDays = delayDays.Count == 0 ? 0m : Math.Round((decimal)delayDays.Average(), 2)
        };
        performance.Status = ResolvePerformanceStatus(performance);
        return performance;
    }

    private async Task<BranchReceiptLine?> LoadReceiptLineAsync(int id) =>
        await _context.BranchReceiptLines.AsNoTracking()
            .Include(x => x.BranchReceipt).ThenInclude(x => x.Store)
            .Include(x => x.BranchReceipt).ThenInclude(x => x.Supplier)
            .Include(x => x.PurchaseOrderLine).ThenInclude(x => x!.Ingredient)
            .Include(x => x.PurchaseOrderLine).ThenInclude(x => x!.PurchaseOrder)
            .SingleOrDefaultAsync(x => x.BranchReceiptLineId == id);

    private static SupplierReceiptIssueContextDto MapContext(BranchReceiptLine line)
    {
        var poLine = line.PurchaseOrderLine!;
        var order = poLine.PurchaseOrder;
        return new SupplierReceiptIssueContextDto
        {
            BranchReceiptLineId = line.BranchReceiptLineId,
            BranchReceiptId = line.BranchReceiptId,
            BranchReceiptCode = line.BranchReceipt.ReceiptCode,
            StoreId = line.BranchReceipt.StoreId,
            StoreName = line.BranchReceipt.Store.Name,
            SupplierId = line.BranchReceipt.SupplierId!.Value,
            SupplierName = line.BranchReceipt.Supplier!.Name,
            PurchaseOrderId = order.PurchaseOrderId,
            PurchaseOrderCode = order.Code,
            PurchaseOrderLineId = poLine.PurchaseOrderLineId,
            IngredientName = poLine.Ingredient.Name,
            AcceptedBaseQuantity = line.ReceivedBaseQuantity,
            RejectedBaseQuantity = line.RejectedBaseQuantity,
            ReceivedAtUtc = line.BranchReceipt.ReceivedAt,
            ExpectedDeliveryAtUtc = order.ExpectedDeliveryAtUtc,
            SuggestedIssueType = order.ExpectedDeliveryAtUtc.HasValue
                && line.BranchReceipt.ReceivedAt > order.ExpectedDeliveryAtUtc.Value
                    ? SupplierReceiptIssueTypes.LateDelivery
                    : line.RejectedBaseQuantity > 0 ? SupplierReceiptIssueTypes.QualityFailure : null
        };
    }

    private async Task<SupplierReceiptIssueListItemDto> MapIssueAsync(int id)
    {
        var issue = await _context.SupplierReceiptIssues.AsNoTracking()
            .Include(x => x.Supplier).Include(x => x.Store)
            .Include(x => x.PurchaseOrder)
            .Include(x => x.PurchaseOrderLine).ThenInclude(x => x.Ingredient)
            .Include(x => x.BranchReceipt)
            .Include(x => x.ReportedByStaff)
            .SingleAsync(x => x.SupplierReceiptIssueId == id);
        return new SupplierReceiptIssueListItemDto
        {
            SupplierReceiptIssueId = issue.SupplierReceiptIssueId,
            SupplierId = issue.SupplierId,
            SupplierName = issue.Supplier.Name,
            StoreId = issue.StoreId,
            StoreName = issue.Store.Name,
            PurchaseOrderId = issue.PurchaseOrderId,
            PurchaseOrderCode = issue.PurchaseOrder.Code,
            PurchaseOrderLineId = issue.PurchaseOrderLineId,
            BranchReceiptId = issue.BranchReceiptId,
            BranchReceiptCode = issue.BranchReceipt.ReceiptCode,
            BranchReceiptLineId = issue.BranchReceiptLineId,
            IngredientName = issue.PurchaseOrderLine.Ingredient.Name,
            IssueType = issue.IssueType,
            Status = issue.Status,
            AffectedBaseQuantity = issue.AffectedBaseQuantity,
            Description = issue.Description,
            ResolutionNote = issue.ResolutionNote,
            DismissReason = issue.DismissReason,
            ReportedByName = issue.ReportedByStaff.FullName,
            ReportedAtUtc = issue.ReportedAtUtc,
            RowVersion = Convert.ToBase64String(issue.RowVersion ?? Array.Empty<byte>())
        };
    }

    private async Task<bool> CanAccessAsync(
        int actorStaffId,
        int storeId,
        IReadOnlyCollection<string> roles)
    {
        if (!roles.Any(x => x is RoleConstants.BusinessOwner or RoleConstants.AccountantWarehouse
                or RoleConstants.AreaManager or RoleConstants.StoreManager
                or RoleConstants.SystemAdmin))
            return false;
        return await _scopeAuthorization.CanAccessStoreAsync(actorStaffId, storeId);
    }

    private static bool CanTransition(string current, string target) => (current, target) switch
    {
        (SupplierReceiptIssueStatuses.Open, SupplierReceiptIssueStatuses.UnderReview) => true,
        (SupplierReceiptIssueStatuses.Open or SupplierReceiptIssueStatuses.UnderReview,
            SupplierReceiptIssueStatuses.Resolved or SupplierReceiptIssueStatuses.Dismissed) => true,
        (SupplierReceiptIssueStatuses.Resolved or SupplierReceiptIssueStatuses.Dismissed,
            SupplierReceiptIssueStatuses.Closed) => true,
        _ => false
    };

    private static bool TryParseRowVersion(string value, out byte[] rowVersion)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            rowVersion = Array.Empty<byte>();
            return false;
        }

        try
        {
            rowVersion = Convert.FromBase64String(value);
            return rowVersion.Length > 0;
        }
        catch (FormatException)
        {
            rowVersion = Array.Empty<byte>();
            return false;
        }
    }

    private static decimal Percentage(decimal numerator, decimal denominator) =>
        denominator <= 0 ? 0m : Math.Round(numerator / denominator * 100m, 2);

    private static string ResolvePerformanceStatus(SupplierPerformanceDto value)
    {
        if (value.CompletedDeliveryCount < 3) return SupplierPerformanceStatuses.InsufficientData;
        if (value.OnTimeRate >= 90m && value.FillRate >= 95m && value.RejectionRate <= 2m)
            return SupplierPerformanceStatuses.Good;
        if (value.OnTimeRate >= 75m && value.FillRate >= 85m && value.RejectionRate <= 5m)
            return SupplierPerformanceStatuses.Watch;
        return SupplierPerformanceStatuses.Risk;
    }

    private static string? Trim(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static ServiceResult<SupplierReceiptIssueListItemDto> Fail(string message, string? errorCode = null) =>
        ServiceResult<SupplierReceiptIssueListItemDto>.Failure(message, errorCode: errorCode);
}
