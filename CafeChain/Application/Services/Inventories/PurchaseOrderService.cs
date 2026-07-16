using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.DTOs.Admin.RestockRequests;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Inventories.Procurement;
using CafeChain.Models.Inventories.Stock;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Inventories
{
    public sealed class PurchaseOrderService : IPurchaseOrderService
    {
        private readonly AppDbContext _context;
        private readonly IPhysicalUnitConversionService _conversion;
        private readonly IRestockAllocationService _allocations;

        public PurchaseOrderService(
            AppDbContext context,
            IPhysicalUnitConversionService conversion,
            IRestockAllocationService allocations)
        {
            _context = context;
            _conversion = conversion;
            _allocations = allocations;
        }

        public async Task<ServiceResult<PurchaseOrderDetailDto>> CreateDraftAsync(
            CreatePurchaseOrderRequest input,
            int actorStaffId,
            IReadOnlyCollection<string> roles)
        {
            if (!CanManage(roles)) return Fail("Bạn không có quyền tạo đơn mua hàng.");
            if (input.StoreId <= 0 || input.SupplierId <= 0 || input.Lines.Count == 0)
                return Fail("Cửa hàng, nhà cung cấp và ít nhất một dòng hàng là bắt buộc.");
            if (input.Lines.Any(x => x.PackageCount <= 0)) return Fail("Số gói đặt phải lớn hơn 0.");
            if (input.Lines.Where(x => x.RestockRequestId.HasValue)
                .GroupBy(x => x.RestockRequestId!.Value)
                .Any(x => x.Count() > 1))
                return Fail("Mỗi yêu cầu nhập chỉ được liên kết một lần trong cùng đơn mua hàng.");

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var supplierStore = await _context.SupplierStores.AsNoTracking()
                    .AnyAsync(x => x.StoreId == input.StoreId && x.SupplierId == input.SupplierId && x.Active);
                if (!supplierStore) return Fail("Nhà cung cấp không hoạt động tại cửa hàng đã chọn.");

                var order = new PurchaseOrder
                {
                    Code = $"PO-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
                    StoreId = input.StoreId,
                    SupplierId = input.SupplierId,
                    Status = PurchaseOrderStatuses.Draft,
                    OrderDate = DateTime.UtcNow,
                    ExpectedDeliveryAtUtc = input.ExpectedDeliveryAtUtc,
                    CreatedByStaffId = actorStaffId,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow,
                    Note = Trim(input.Note, 1000)
                };

                foreach (var requested in input.Lines)
                {
                    var offer = await _context.IngredientSuppliers.AsNoTracking()
                        .Include(x => x.Ingredient)
                        .SingleOrDefaultAsync(x => x.IngredientSupplierId == requested.IngredientSupplierId);
                    if (offer == null || !offer.Active || offer.SupplierId != input.SupplierId
                        || offer.IngredientId != requested.IngredientId)
                        return Fail("Gói mua không khớp nhà cung cấp hoặc nguyên liệu.");
                    if (!offer.PackageQuantity.HasValue || offer.PackageQuantity.Value <= 0)
                        return Fail("Gói mua chưa cấu hình lượng trong gói.");
                    if (requested.PackageCount < offer.MinimumOrderPackageCount.GetValueOrDefault())
                        return Fail($"Số gói đặt thấp hơn MOQ {offer.MinimumOrderPackageCount:N3}.");

                    var converted = await _conversion.ConvertAsync(
                        requested.PackageCount * offer.PackageQuantity.Value,
                        offer.UnitId,
                        offer.Ingredient.BaseUnitId);
                    if (!converted.IsSuccess || converted.Data <= 0)
                        return Fail(converted.Message ?? "Không quy đổi được số lượng đặt về đơn vị tồn kho.");

                    if (requested.RestockRequestId.HasValue)
                    {
                        var allocation = await _allocations.ValidateAllocationAsync(new RestockAllocationValidationRequest
                        {
                            RestockRequestId = requested.RestockRequestId.Value,
                            DestinationStoreId = input.StoreId,
                            IngredientId = requested.IngredientId,
                            AllocationQuantity = converted.Data,
                            ActorStaffId = actorStaffId,
                            ActorRoles = roles,
                            AllowOverallocationOverride = input.AllowOverallocationOverride,
                            OverrideReason = input.OverallocationOverrideReason,
                            RequestKey = order.Code
                        });
                        if (!allocation.IsSuccess) return Fail(allocation.Message ?? "Phân bổ đơn mua không hợp lệ.");
                    }

                    order.Lines.Add(new PurchaseOrderLine
                    {
                        RestockRequestId = requested.RestockRequestId,
                        IngredientId = requested.IngredientId,
                        IngredientSupplierId = requested.IngredientSupplierId,
                        PackageUnitIdSnapshot = offer.UnitId,
                        PackageQuantitySnapshot = offer.PackageQuantity.Value,
                        PackagePriceSnapshot = offer.CurrentPrice,
                        PackageCount = requested.PackageCount,
                        OrderedBaseQuantity = converted.Data,
                        PromisedLeadTimeDaysSnapshot = offer.LeadTimeDays.GetValueOrDefault(),
                        Note = Trim(requested.Note, 500)
                    });
                }

                _context.PurchaseOrders.Add(order);
                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                return ServiceResult<PurchaseOrderDetailDto>.Success(await MapAsync(order.PurchaseOrderId), "Đã tạo đơn mua hàng nháp.");
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return Fail($"Không tạo được đơn mua hàng: {ex.Message}");
            }
        }

        public Task<ServiceResult<PurchaseOrderDetailDto>> ApproveAsync(int id, int actorStaffId, IReadOnlyCollection<string> roles) =>
            TransitionAsync(id, PurchaseOrderStatuses.Draft, PurchaseOrderStatuses.Approved, actorStaffId, roles);

        public Task<ServiceResult<PurchaseOrderDetailDto>> MarkSentAsync(int id, int actorStaffId, IReadOnlyCollection<string> roles) =>
            TransitionAsync(id, PurchaseOrderStatuses.Approved, PurchaseOrderStatuses.MarkedAsSent, actorStaffId, roles);

        public async Task<ServiceResult<PurchaseOrderDetailDto>> CancelAsync(
            int id, int actorStaffId, IReadOnlyCollection<string> roles, string reason)
        {
            if (!CanManage(roles)) return Fail("Bạn không có quyền hủy đơn mua hàng.");
            if (string.IsNullOrWhiteSpace(reason)) return Fail("Lý do hủy là bắt buộc.");
            var order = await _context.PurchaseOrders.Include(x => x.Lines).ThenInclude(x => x.ReceiptPostings)
                .SingleOrDefaultAsync(x => x.PurchaseOrderId == id);
            if (order == null) return Fail("Không tìm thấy đơn mua hàng.");
            if (order.Status is PurchaseOrderStatuses.Completed or PurchaseOrderStatuses.Cancelled
                || order.Lines.Any(x => x.ReceiptPostings.Count > 0))
                return Fail("Không thể hủy đơn đã nhận hàng hoặc đã kết thúc.");
            order.Status = PurchaseOrderStatuses.Cancelled;
            order.CancelledAtUtc = DateTime.UtcNow;
            order.UpdatedAtUtc = DateTime.UtcNow;
            order.Note = Trim($"{order.Note}\nCANCEL: {reason.Trim()}", 1000);
            await _context.SaveChangesAsync();
            return ServiceResult<PurchaseOrderDetailDto>.Success(await MapAsync(id), "Đã hủy đơn mua hàng.");
        }

        public async Task<ServiceResult<PurchaseOrderDetailDto>> GetDetailAsync(int id)
        {
            var dto = await MapAsync(id);
            return dto.PurchaseOrderId == 0 ? Fail("Không tìm thấy đơn mua hàng.") : ServiceResult<PurchaseOrderDetailDto>.Success(dto);
        }

        public async Task<IReadOnlyList<PurchaseOrderListItemDto>> ListAsync(int? storeId, string? status)
        {
            var query = _context.PurchaseOrders.AsNoTracking()
                .Include(x => x.Store)
                .Include(x => x.Supplier)
                .Include(x => x.Lines)
                .AsQueryable();
            if (storeId.HasValue) query = query.Where(x => x.StoreId == storeId);
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
            var orders = await query.OrderByDescending(x => x.CreatedAtUtc).ToListAsync();
            return orders.Select(x => new PurchaseOrderListItemDto
            {
                PurchaseOrderId = x.PurchaseOrderId,
                Code = x.Code,
                StoreName = x.Store.Name,
                SupplierName = x.Supplier.Name,
                Status = x.Status,
                OrderDate = x.OrderDate,
                TotalAmount = x.Lines.Sum(l => l.PackageCount * l.PackagePriceSnapshot)
            }).ToList();
        }

        public async Task<ServiceResult> ValidateReceiptLineAsync(BranchReceipt receipt, BranchReceiptLine line)
        {
            if (!line.PurchaseOrderLineId.HasValue) return ServiceResult.Success();
            var poLine = await LoadLineForUpdateAsync(line.PurchaseOrderLineId.Value);
            if (poLine == null) return ServiceResult.Failure("Không tìm thấy dòng đơn mua hàng.");
            if (poLine.PurchaseOrder.Status is not (PurchaseOrderStatuses.Approved or PurchaseOrderStatuses.MarkedAsSent or PurchaseOrderStatuses.PartiallyReceived))
                return ServiceResult.Failure($"Đơn mua hàng không thể nhận ở trạng thái {poLine.PurchaseOrder.Status}.");
            if (poLine.PurchaseOrder.StoreId != receipt.StoreId || poLine.PurchaseOrder.SupplierId != receipt.SupplierId)
                return ServiceResult.Failure("Cửa hàng hoặc nhà cung cấp trên phiếu nhận không khớp đơn mua.");
            if (poLine.IngredientId != line.IngredientId || poLine.RestockRequestId != line.RestockRequestId)
                return ServiceResult.Failure("Nguyên liệu hoặc yêu cầu nhập không khớp dòng đơn mua.");
            if (line.ReceivedBaseQuantity < 0 || line.RejectedBaseQuantity < 0
                || line.ReceivedBaseQuantity + line.RejectedBaseQuantity <= 0)
                return ServiceResult.Failure("Số lượng chấp nhận/loại bỏ không hợp lệ.");
            var disposedRows = await _context.PurchaseOrderReceiptPostings.AsNoTracking()
                .Where(x => x.PurchaseOrderLineId == poLine.PurchaseOrderLineId)
                .Select(x => new { x.AcceptedBaseQuantity, x.RejectedBaseQuantity })
                .ToListAsync();
            var disposed = disposedRows.Sum(x => x.AcceptedBaseQuantity + x.RejectedBaseQuantity);
            if (disposed + line.ReceivedBaseQuantity + line.RejectedBaseQuantity > poLine.OrderedBaseQuantity)
                return ServiceResult.Failure("Tổng số lượng nhận vượt số lượng còn lại của dòng đơn mua.");
            return ServiceResult.Success();
        }

        public async Task<ServiceResult> RegisterReceiptPostingAsync(BranchReceipt receipt, BranchReceiptLine line, int actorStaffId)
        {
            if (!line.PurchaseOrderLineId.HasValue) return ServiceResult.Success();
            var ownsTransaction = _context.Database.CurrentTransaction == null;
            await using var transaction = ownsTransaction
                ? await _context.Database.BeginTransactionAsync()
                : null;
            try
            {
                if (await _context.PurchaseOrderReceiptPostings
                    .AnyAsync(x => x.BranchReceiptLineId == line.BranchReceiptLineId))
                    return ServiceResult.Success("Dòng nhận đã được ghi nhận trước đó.");

                // Serialize on the PO line before the second replay check. A concurrent
                // confirmation may complete the PO while this transaction is waiting.
                await LoadLineForUpdateAsync(line.PurchaseOrderLineId.Value);
                if (await _context.PurchaseOrderReceiptPostings
                    .AnyAsync(x => x.BranchReceiptLineId == line.BranchReceiptLineId))
                    return ServiceResult.Success("Dòng nhận đã được ghi nhận trước đó.");

                var validation = await ValidateReceiptLineAsync(receipt, line);
                if (!validation.IsSuccess) return validation;

                var poLine = await LoadLineForUpdateAsync(line.PurchaseOrderLineId.Value);
                _context.PurchaseOrderReceiptPostings.Add(new PurchaseOrderReceiptPosting
                {
                    PurchaseOrderLineId = poLine!.PurchaseOrderLineId,
                    BranchReceiptLineId = line.BranchReceiptLineId,
                    AcceptedBaseQuantity = line.ReceivedBaseQuantity,
                    RejectedBaseQuantity = line.RejectedBaseQuantity,
                    CreatedByStaffId = actorStaffId,
                    CreatedAtUtc = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                var order = poLine.PurchaseOrder;
                var orderedQuantities = await _context.PurchaseOrderLines.AsNoTracking()
                    .Where(x => x.PurchaseOrderId == order.PurchaseOrderId)
                    .Select(x => x.OrderedBaseQuantity)
                    .ToListAsync();
                var postingQuantities = await _context.PurchaseOrderReceiptPostings.AsNoTracking()
                    .Where(x => x.PurchaseOrderLine.PurchaseOrderId == order.PurchaseOrderId)
                    .Select(x => new { x.AcceptedBaseQuantity, x.RejectedBaseQuantity })
                    .ToListAsync();
                var disposedTotal = postingQuantities.Sum(x => x.AcceptedBaseQuantity + x.RejectedBaseQuantity);
                var orderedTotal = orderedQuantities.Sum();
                order.Status = disposedTotal >= orderedTotal
                    ? PurchaseOrderStatuses.Completed
                    : disposedTotal > 0 ? PurchaseOrderStatuses.PartiallyReceived : order.Status;
                order.CompletedAtUtc = order.Status == PurchaseOrderStatuses.Completed ? DateTime.UtcNow : null;
                order.UpdatedAtUtc = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                if (transaction != null) await transaction.CommitAsync();
                return ServiceResult.Success("Đã ghi nhận số lượng nhận theo đơn mua.");
            }
            catch
            {
                if (transaction != null) await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task<ServiceResult<PurchaseOrderDetailDto>> TransitionAsync(
            int id, string expected, string next, int actorStaffId, IReadOnlyCollection<string> roles)
        {
            if (!CanManage(roles)) return Fail("Bạn không có quyền cập nhật đơn mua hàng.");
            var order = await _context.PurchaseOrders.SingleOrDefaultAsync(x => x.PurchaseOrderId == id);
            if (order == null) return Fail("Không tìm thấy đơn mua hàng.");
            if (order.Status != expected) return Fail($"Chỉ chuyển {next} từ {expected}. Trạng thái hiện tại: {order.Status}.");
            order.Status = next;
            order.UpdatedAtUtc = DateTime.UtcNow;
            if (next == PurchaseOrderStatuses.Approved) { order.ApprovedByStaffId = actorStaffId; order.ApprovedAtUtc = DateTime.UtcNow; }
            if (next == PurchaseOrderStatuses.MarkedAsSent) { order.SentByStaffId = actorStaffId; order.SentAtUtc = DateTime.UtcNow; }
            await _context.SaveChangesAsync();
            return ServiceResult<PurchaseOrderDetailDto>.Success(await MapAsync(id), $"Đã chuyển đơn mua sang {next}.");
        }

        private async Task<PurchaseOrderLine?> LoadLineForUpdateAsync(int id)
        {
            if (_context.Database.IsSqlServer())
            {
                var line = await _context.PurchaseOrderLines.FromSqlInterpolated(
                    $@"SELECT * FROM PurchaseOrderLines WITH (UPDLOCK, HOLDLOCK, ROWLOCK) WHERE PurchaseOrderLineId = {id}")
                    .SingleOrDefaultAsync();
                if (line != null) await _context.Entry(line).Reference(x => x.PurchaseOrder).LoadAsync();
                return line;
            }
            return await _context.PurchaseOrderLines.Include(x => x.PurchaseOrder).SingleOrDefaultAsync(x => x.PurchaseOrderLineId == id);
        }

        private async Task<PurchaseOrderDetailDto> MapAsync(int id)
        {
            var order = await _context.PurchaseOrders.AsNoTracking()
                .Include(x => x.Store).Include(x => x.Supplier)
                .Include(x => x.Lines).ThenInclude(x => x.Ingredient)
                .Include(x => x.Lines).ThenInclude(x => x.PackageUnitSnapshot)
                .Include(x => x.Lines).ThenInclude(x => x.ReceiptPostings)
                .SingleOrDefaultAsync(x => x.PurchaseOrderId == id);
            if (order == null) return new PurchaseOrderDetailDto();
            return new PurchaseOrderDetailDto
            {
                PurchaseOrderId = order.PurchaseOrderId,
                Code = order.Code,
                StoreId = order.StoreId,
                StoreName = order.Store.Name,
                SupplierId = order.SupplierId,
                SupplierName = order.Supplier.Name,
                Status = order.Status,
                OrderDate = order.OrderDate,
                ExpectedDeliveryAtUtc = order.ExpectedDeliveryAtUtc,
                Note = order.Note,
                TotalAmount = order.Lines.Sum(x => x.PackageCount * x.PackagePriceSnapshot),
                Lines = order.Lines.Select(x =>
                {
                    var accepted = x.ReceiptPostings.Sum(p => p.AcceptedBaseQuantity);
                    var rejected = x.ReceiptPostings.Sum(p => p.RejectedBaseQuantity);
                    return new PurchaseOrderLineDto
                    {
                        PurchaseOrderLineId = x.PurchaseOrderLineId,
                        RestockRequestId = x.RestockRequestId,
                        IngredientId = x.IngredientId,
                        IngredientName = x.Ingredient.Name,
                        PackageCount = x.PackageCount,
                        PackageQuantitySnapshot = x.PackageQuantitySnapshot,
                        PackageUnitName = x.PackageUnitSnapshot.Name,
                        PackagePriceSnapshot = x.PackagePriceSnapshot,
                        OrderedBaseQuantity = x.OrderedBaseQuantity,
                        AcceptedBaseQuantity = accepted,
                        RejectedBaseQuantity = rejected,
                        RemainingBaseQuantity = Math.Max(0m, x.OrderedBaseQuantity - accepted - rejected),
                        PromisedLeadTimeDaysSnapshot = x.PromisedLeadTimeDaysSnapshot
                    };
                }).ToList()
            };
        }

        private static bool CanManage(IReadOnlyCollection<string> roles) =>
            roles.Contains(RoleConstants.AccountantWarehouse)
            || roles.Contains(RoleConstants.BusinessOwner)
            || roles.Contains(RoleConstants.AreaManager);

        private static string? Trim(string? value, int max) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, max)];

        private static ServiceResult<PurchaseOrderDetailDto> Fail(string message) =>
            ServiceResult<PurchaseOrderDetailDto>.Failure(message);
    }
}
