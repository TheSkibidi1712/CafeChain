using CafeChain.Application.DTOs.Admin.Suppliers;
using CafeChain.Application.Interfaces.Admin.Suppliers;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Services.Inventories;
using CafeChain.Data;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Admin.Suppliers;

public sealed class SupplierProcurementDataQualityService : ISupplierProcurementDataQualityService
{
    private readonly AppDbContext _context;
    private readonly IUnitConversionService _conversion;

    public SupplierProcurementDataQualityService(
        AppDbContext context,
        IUnitConversionService conversion)
    {
        _context = context;
        _conversion = conversion;
    }

    public async Task<SupplierProcurementDataQualityReportDTO> InspectAsync(
        CancellationToken cancellationToken = default)
    {
        var report = new SupplierProcurementDataQualityReportDTO
        {
            GeneratedAtUtc = DateTime.UtcNow,
            DryRun = true
        };
        var procurementUnitCache = new Dictionary<int, HashSet<int>>();

        async Task<HashSet<int>> ProcurementUnitIdsAsync(int ingredientId)
        {
            if (procurementUnitCache.TryGetValue(ingredientId, out var cached))
                return cached;

            var options = await _conversion.GetActiveUnitOptionsAsync(
                ingredientId,
                cancellationToken);
            var allowed = options.IsSuccess && options.Data != null
                ? ProcurementUnitPolicy.Filter(options.Data)
                    .Select(x => x.UnitId)
                    .ToHashSet()
                : new HashSet<int>();
            procurementUnitCache[ingredientId] = allowed;
            return allowed;
        }

        var offers = await _context.IngredientSuppliers
            .AsNoTracking()
            .Include(x => x.Supplier)
            .Include(x => x.Ingredient).ThenInclude(x => x.BaseUnit)
            .Include(x => x.Unit)
            .Include(x => x.LooseProcurementUnit)
            .ToListAsync(cancellationToken);
        report.ScannedOfferCount = offers.Count;

        var offerIds = offers.Select(x => x.IngredientSupplierId).ToArray();
        var referencedByActivePurchaseOrder = (await _context.PurchaseOrderLines
            .AsNoTracking()
            .Where(x => offerIds.Contains(x.IngredientSupplierId)
                && x.PurchaseOrder.Status != CafeChain.Application.Constants.PurchaseOrderStatuses.Cancelled)
            .Select(x => x.IngredientSupplierId)
            .Distinct()
            .ToListAsync(cancellationToken))
            .ToHashSet();

        foreach (var offer in offers)
        {
            var reference = $"{offer.Supplier.Code} / {offer.Ingredient.Code}";
            var structuralIssue = false;
            if (!offer.PackageQuantity.HasValue || offer.PackageQuantity <= 0m)
            {
                structuralIssue = true;
                Add(report, "PACKAGE_CONTENT_MISSING", "SupplierPackage",
                    offer.IngredientSupplierId, reference,
                    "Gói mua chưa có lượng nội dung hợp lệ.");
            }
            else
            {
                var packageConversion = await _conversion.ConvertAsync(
                    offer.IngredientId,
                    offer.PackageQuantity.Value,
                    offer.UnitId,
                    offer.Ingredient.BaseUnitId);
                if (!packageConversion.IsSuccess)
                {
                    structuralIssue = true;
                    Add(report, "PACKAGE_UOM_INCOMPATIBLE", "SupplierPackage",
                        offer.IngredientSupplierId, reference,
                        "Đơn vị nội dung của gói không quy đổi được về đơn vị tồn cơ sở.");
                }
            }

            if (offer.Unit?.Type == Models.Enums.Unit.UnitType.Dem
                && offer.UnitId != offer.Ingredient.BaseUnitId)
            {
                structuralIssue = true;
                Add(report, "COUNT_PACKAGE_CONTENT_UOM_INVALID", "SupplierPackage",
                    offer.IngredientSupplierId, reference,
                    "Mặt hàng dạng đếm đang dùng đơn vị thùng/hộp thay cho số cái trong một gói. Cần xác nhận lại quy cách.");
            }

            if (offer.CurrentPrice <= 0m)
            {
                structuralIssue = true;
                Add(report, "PACKAGE_PRICE_INVALID", "SupplierPackage",
                    offer.IngredientSupplierId, reference,
                    "Giá một gói phải lớn hơn 0 trước khi dùng cho mua hàng.");
            }

            if (offer.AllowsLoosePurchase)
            {
                if (!offer.LooseProcurementUnitId.HasValue
                    || !offer.CurrentProcurementUnitPrice.HasValue
                    || offer.CurrentProcurementUnitPrice <= 0m)
                {
                    structuralIssue = true;
                    Add(report, "LOOSE_PURCHASE_INCOMPLETE", "SupplierPackage",
                        offer.IngredientSupplierId, reference,
                        "Đã bật mua lẻ nhưng thiếu đơn vị hoặc đơn giá mua lẻ.");
                }
                else
                {
                    var allowedUnits = await ProcurementUnitIdsAsync(offer.IngredientId);
                    if (!allowedUnits.Contains(offer.LooseProcurementUnitId.Value))
                    {
                        structuralIssue = true;
                        Add(report, "LOOSE_UOM_INCOMPATIBLE", "SupplierPackage",
                            offer.IngredientSupplierId, reference,
                            "Đơn vị mua lẻ không phù hợp với nguyên liệu hoặc chưa có quy đổi hợp lệ.");
                    }
                }
            }
            else if (offer.LooseProcurementUnitId.HasValue
                     || offer.CurrentProcurementUnitPrice.HasValue
                     || offer.LooseMinimumOrderQuantity.HasValue
                     || offer.LooseQuantityStep.HasValue)
            {
                Add(report, "LOOSE_FIELDS_LEFTOVER", "SupplierPackage",
                    offer.IngredientSupplierId, reference,
                    "Gói đang tắt mua lẻ nhưng vẫn còn dữ liệu mua lẻ; cần xác nhận trước khi làm sạch.");
            }

            if (offer.Active && structuralIssue)
            {
                Add(report, "ACTIVE_PACKAGE_NOT_READY", "SupplierPackage",
                    offer.IngredientSupplierId, reference,
                    "Gói mua đang hoạt động nhưng chưa đủ điều kiện để sử dụng cho mua hàng.",
                    referencedByActivePurchaseOrder.Contains(offer.IngredientSupplierId)
                        ? "INVALID_BLOCKING"
                        : "NEEDS_REVIEW");
            }

            if (offer.IsPrimary && structuralIssue)
            {
                Add(report, "PRIMARY_PACKAGE_NOT_READY", "SupplierPackage",
                    offer.IngredientSupplierId, reference,
                    "Nguồn cung chính chưa đủ điều kiện để sử dụng cho mua hàng.",
                    "INVALID_BLOCKING");
            }
        }

        foreach (var duplicatePrimary in offers.Where(x => x.Active && x.IsPrimary)
                     .GroupBy(x => x.IngredientId).Where(x => x.Count() > 1))
        {
            foreach (var offer in duplicatePrimary)
            {
                Add(report, "MULTIPLE_PRIMARY_SOURCE", "SupplierPackage",
                    offer.IngredientSupplierId,
                    $"{offer.Supplier.Code} / {offer.Ingredient.Code}",
                    "Nguyên liệu đang có nhiều nguồn cung chính trong cùng phạm vi toàn chuỗi.");
            }
        }

        var stores = await _context.SupplierStores.AsNoTracking().ToListAsync(cancellationToken);
        report.ScannedStoreAssignmentCount = stores.Count;
        foreach (var duplicate in stores.GroupBy(x => new { x.SupplierId, x.StoreId }).Where(x => x.Count() > 1))
        {
            foreach (var assignment in duplicate)
            {
                Add(report, "DUPLICATE_SUPPLIER_STORE", "SupplierStore",
                    assignment.SupplierStoreId,
                    $"Supplier {assignment.SupplierId} / Store {assignment.StoreId}",
                    "Nhà cung cấp đang được gán trùng cho cùng một cửa hàng.");
            }
        }

        var restocks = await _context.RestockRequests.AsNoTracking()
            .Where(x => x.IngredientId.HasValue && x.ProcurementUnitId.HasValue)
            .Select(x => new
            {
                x.RestockRequestId,
                x.ReferenceCode,
                IngredientId = x.IngredientId!.Value,
                ProcurementUnitId = x.ProcurementUnitId!.Value
            })
            .ToListAsync(cancellationToken);
        report.ScannedRestockCount = restocks.Count;
        foreach (var restock in restocks)
        {
            var allowedUnits = await ProcurementUnitIdsAsync(restock.IngredientId);
            if (!allowedUnits.Contains(restock.ProcurementUnitId))
            {
                Add(report, "RESTOCK_UOM_INCOMPATIBLE", "RestockRequest",
                    restock.RestockRequestId, restock.ReferenceCode,
                    "Đơn vị nhu cầu không quy đổi được về đơn vị tồn cơ sở của nguyên liệu.");
            }
        }

        return report;
    }

    private static void Add(
        SupplierProcurementDataQualityReportDTO report,
        string code,
        string entityType,
        int entityId,
        string reference,
        string message,
        string resolution = "NEEDS_REVIEW") =>
        report.Findings.Add(new SupplierProcurementDataQualityFindingDTO
        {
            Code = code,
            EntityType = entityType,
            EntityId = entityId,
            Reference = reference,
            Message = message,
            Resolution = resolution
        });
}
