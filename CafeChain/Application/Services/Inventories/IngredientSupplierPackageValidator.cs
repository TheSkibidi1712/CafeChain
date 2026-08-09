using System.Threading.Tasks;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Suppliers;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Inventories
{
    public class IngredientSupplierPackageValidator : IIngredientSupplierPackageValidator
    {
        private readonly AppDbContext _context;
        private readonly IPhysicalUnitConversionService _physical;
        private readonly IUnitConversionService _conversion;

        public IngredientSupplierPackageValidator(
            AppDbContext context,
            IPhysicalUnitConversionService physical,
            IUnitConversionService? conversion = null)
        {
            _context = context;
            _physical = physical;
            _conversion = conversion ?? new UnitConversionService(
                context,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<UnitConversionService>.Instance,
                physical);
        }

        public async Task<ServiceResult> ValidateAsync(
            int ingredientId,
            int supplierId,
            int unitId,
            decimal? packageQuantity,
            decimal currentPrice,
            bool isActive,
            bool requirePackageQuantity,
            int? excludeIngredientSupplierId = null)
        {
            if (ingredientId <= 0)
                return ServiceResult.Failure("Nguyên liệu không hợp lệ.");
            if (supplierId <= 0)
                return ServiceResult.Failure("Nhà cung cấp không hợp lệ.");
            if (unitId <= 0)
                return ServiceResult.Failure("Đơn vị nội dung không hợp lệ.");
            if (currentPrice <= 0)
                return ServiceResult.Failure(
                    "Giá một gói phải lớn hơn 0 để có thể sử dụng cho mua hàng.",
                    errorCode: SupplierPackageReadinessCodes.PriceInvalid);

            if (packageQuantity.HasValue && packageQuantity.Value <= 0)
                return ServiceResult.Failure("Hàm lượng trong gói phải lớn hơn 0.");

            if (requirePackageQuantity && (!packageQuantity.HasValue || packageQuantity.Value <= 0))
                return ServiceResult.Failure(
                    "Quy cách gói phải có lượng nội dung lớn hơn 0.",
                    errorCode: SupplierPackageReadinessCodes.ContentMissing);

            var ingredient = await _context.Ingredients
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.IngredientId == ingredientId);
            if (ingredient == null)
                return ServiceResult.Failure("Không tìm thấy nguyên liệu của gói mua.");
            if (!ingredient.Active && isActive)
                return ServiceResult.Failure("Không thể kích hoạt nguồn cung cho nguyên liệu đã ngưng hoạt động.");

            var supplier = await _context.Suppliers
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SupplierId == supplierId);
            if (supplier == null)
                return ServiceResult.Failure("Không tìm thấy nhà cung cấp của gói mua.");
            if (!supplier.Active && isActive)
                return ServiceResult.Failure("Không thể kích hoạt nguồn cung của nhà cung cấp đã ngưng hoạt động.");

            var unit = await _context.Units
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UnitId == unitId);
            if (unit == null)
                return ServiceResult.Failure("Không tìm thấy đơn vị nội dung của gói mua.");
            if (!unit.Active)
                return ServiceResult.Failure("Đơn vị nội dung của gói mua đã ngừng sử dụng.");

            if (PackageUnitCodes.IsRejectedCommercialPackaging(unit.UnitCode))
            {
                return ServiceResult.Failure(
                    "Đơn vị đóng gói thương mại như thùng, hộp hoặc gói không được dùng làm đơn vị nội dung. Hãy chọn đơn vị tồn kho của nguyên liệu.",
                    errorCode: SupplierPackageReadinessCodes.ContentUomInvalid);
            }

            // Countable inventory: Dem only when UnitId == BaseUnitId
            if (unit.Type == UnitType.Dem && unitId != ingredient.BaseUnitId)
            {
                return ServiceResult.Failure(
                    "Quy cách gói chưa hợp lệ với đơn vị tồn kho của nguyên liệu. Với mặt hàng dạng đếm, hãy nhập số cái trong một gói.",
                    errorCode: SupplierPackageReadinessCodes.ContentUomInvalid);
            }

            // Mass/volume: same unit or physical convert
            if (unit.Type == UnitType.KhoiLuong || unit.Type == UnitType.TheTich)
            {
                if (unitId != ingredient.BaseUnitId)
                {
                    var convert = await _conversion.ConvertAsync(
                        ingredientId,
                        1m,
                        unitId,
                        ingredient.BaseUnitId);
                    if (!convert.IsSuccess)
                    {
                        return ServiceResult.Failure(
                            "Quy cách gói chưa hợp lệ với đơn vị tồn kho của nguyên liệu.",
                            errorCode: SupplierPackageReadinessCodes.ContentUomInvalid);
                    }
                }
            }
            else if (unit.Type != UnitType.Dem)
            {
                return ServiceResult.Failure(
                    "Loại đơn vị nội dung của gói mua không được hỗ trợ.",
                    errorCode: SupplierPackageReadinessCodes.ContentUomInvalid);
            }

            var duplicate = await _context.IngredientSuppliers
                .AsNoTracking()
                .AnyAsync(x =>
                    x.IngredientId == ingredientId &&
                    x.SupplierId == supplierId &&
                    (!excludeIngredientSupplierId.HasValue ||
                     x.IngredientSupplierId != excludeIngredientSupplierId.Value));

            if (duplicate)
                return ServiceResult.Failure("Đã tồn tại bảng giá gói mua cho cặp nguyên liệu + nhà cung cấp này.");

            return ServiceResult.Success();
        }

        public async Task<bool> HasCompletePackageDefinitionAsync(
            int ingredientId,
            int unitId,
            decimal? packageQuantity)
        {
            if (!packageQuantity.HasValue || packageQuantity.Value <= 0)
                return false;

            var ingredient = await _context.Ingredients
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.IngredientId == ingredientId);
            if (ingredient == null)
                return false;

            var unit = await _context.Units
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UnitId == unitId);
            if (unit == null || !unit.Active)
                return false;

            if (PackageUnitCodes.IsRejectedCommercialPackaging(unit.UnitCode))
                return false;

            if (unit.Type == UnitType.Dem)
                return unitId == ingredient.BaseUnitId;

            if (unit.Type == UnitType.KhoiLuong || unit.Type == UnitType.TheTich)
            {
                if (unitId == ingredient.BaseUnitId)
                    return true;

                var convert = await _conversion.ConvertAsync(
                    ingredientId,
                    1m,
                    unitId,
                    ingredient.BaseUnitId);
                return convert.IsSuccess;
            }

            return false;
        }

        public async Task<bool> HasCompletePackageDefinitionAsync(IngredientSupplier offer)
        {
            return (await EvaluateReadinessAsync(offer)).HasValidPackageDefinition;
        }

        public async Task<SupplierPackageReadinessResult> EvaluateReadinessAsync(
            IngredientSupplier offer)
        {
            var results = await EvaluateReadinessAsync(new[] { offer });
            return results.TryGetValue(offer.IngredientSupplierId, out var result)
                ? result
                : SupplierPackageReadinessResult.NotReady(
                    SupplierPackageReadinessCodes.NotProcurementReady,
                    "Không tìm thấy dữ liệu gói mua để kiểm tra.");
        }

        public async Task<IReadOnlyDictionary<int, SupplierPackageReadinessResult>> EvaluateReadinessAsync(
            IEnumerable<IngredientSupplier> offers)
        {
            var offerIds = offers
                .Select(x => x.IngredientSupplierId)
                .Where(x => x > 0)
                .Distinct()
                .ToArray();
            if (offerIds.Length == 0)
                return new Dictionary<int, SupplierPackageReadinessResult>();

            var rows = await _context.IngredientSuppliers
                .AsNoTracking()
                .Include(x => x.Supplier)
                .Include(x => x.Ingredient).ThenInclude(x => x.BaseUnit)
                .Include(x => x.Unit)
                .Include(x => x.LooseProcurementUnit)
                .Where(x => offerIds.Contains(x.IngredientSupplierId))
                .ToListAsync();

            var ingredientIds = rows.Select(x => x.IngredientId).Distinct().ToArray();
            var conversions = await _context.UnitConversions
                .AsNoTracking()
                .Where(x => ingredientIds.Contains(x.IngredientId) && x.Active)
                .ToListAsync();

            return rows.ToDictionary(
                x => x.IngredientSupplierId,
                x => EvaluateLoaded(x, conversions));
        }

        public async Task<SupplierPackageReadinessResult> EvaluateProcurementEligibilityAsync(
            IngredientSupplier offer,
            PurchaseMode purchaseMode,
            int? storeId = null)
        {
            var readiness = await EvaluateReadinessAsync(offer);
            if (!readiness.IsReady)
                return readiness.NotEligible(
                    SupplierPackageReadinessCodes.NotProcurementReady,
                    readiness.Message);

            var authoritative = await _context.IngredientSuppliers
                .AsNoTracking()
                .Where(x => x.IngredientSupplierId == offer.IngredientSupplierId)
                .Select(x => new
                {
                    x.Active,
                    x.SupplierId,
                    x.AllowsLoosePurchase
                })
                .SingleOrDefaultAsync();
            if (authoritative == null || !authoritative.Active)
            {
                return readiness.NotEligible(
                    SupplierPackageReadinessCodes.Inactive,
                    "Gói mua đang ngừng sử dụng.");
            }

            if (purchaseMode == PurchaseMode.Loose && !authoritative.AllowsLoosePurchase)
            {
                return readiness.NotEligible(
                    SupplierPackageReadinessCodes.PurchaseModeInvalid,
                    "Gói mua này chưa được cấu hình để mua lẻ.");
            }

            if (storeId.HasValue)
            {
                var inScope = await _context.SupplierStores.AsNoTracking().AnyAsync(x =>
                    x.SupplierId == authoritative.SupplierId
                    && x.StoreId == storeId.Value
                    && x.Active
                    && x.Supplier.Active
                    && x.Store.Active);
                if (!inScope)
                {
                    return readiness.NotEligible(
                        SupplierPackageReadinessCodes.StoreScopeInvalid,
                        "Nhà cung cấp chưa được kích hoạt cho cửa hàng này.");
                }
            }

            return readiness.Eligible();
        }

        private static SupplierPackageReadinessResult EvaluateLoaded(
            IngredientSupplier offer,
            IReadOnlyCollection<Models.Inventories.Ingredients.UnitConversion> conversions)
        {
            if (offer.Supplier?.Active != true || offer.Ingredient?.Active != true)
            {
                return SupplierPackageReadinessResult.NotReady(
                    SupplierPackageReadinessCodes.ParentInactive,
                    "Nhà cung cấp hoặc nguyên liệu đang ngừng hoạt động.");
            }

            if (!offer.PackageQuantity.HasValue || offer.PackageQuantity.Value <= 0m)
            {
                return SupplierPackageReadinessResult.NotReady(
                    SupplierPackageReadinessCodes.ContentMissing,
                    "Quy cách gói chưa có lượng nội dung hợp lệ.");
            }

            if (offer.Unit?.Active != true
                || offer.Ingredient.BaseUnit?.Active != true
                || PackageUnitCodes.IsRejectedCommercialPackaging(offer.Unit.UnitCode)
                || !TryConversionFactor(
                    offer.IngredientId,
                    offer.Unit,
                    offer.Ingredient.BaseUnit,
                    conversions,
                    allowConfiguredCountConversion: false,
                    out var packageFactor))
            {
                return SupplierPackageReadinessResult.NotReady(
                    SupplierPackageReadinessCodes.ContentUomInvalid,
                    "Quy cách gói chưa hợp lệ với đơn vị tồn kho của nguyên liệu. Hãy cập nhật số lượng và đơn vị nội dung của một gói.");
            }

            var packageBaseQuantity = offer.PackageQuantity.Value * packageFactor;
            if (packageBaseQuantity <= 0m)
            {
                return SupplierPackageReadinessResult.NotReady(
                    SupplierPackageReadinessCodes.ContentUomInvalid,
                    "Lượng nội dung của gói không quy đổi được về đơn vị tồn kho.");
            }

            if (offer.CurrentPrice <= 0m)
            {
                return SupplierPackageReadinessResult.NotReady(
                    SupplierPackageReadinessCodes.PriceInvalid,
                    "Giá một gói phải lớn hơn 0 để có thể sử dụng cho mua hàng.",
                    hasValidPackageDefinition: true,
                    packageBaseQuantity: packageBaseQuantity);
            }

            if (offer.MinimumOrderPackageCount is <= 0 || offer.LeadTimeDays is < 0)
            {
                return SupplierPackageReadinessResult.NotReady(
                    SupplierPackageReadinessCodes.OperationalTermsInvalid,
                    "MOQ theo gói hoặc thời gian giao hàng chưa hợp lệ.",
                    hasValidPackageDefinition: true,
                    packageBaseQuantity: packageBaseQuantity);
            }

            if (offer.AllowsLoosePurchase)
            {
                if (!LoosePurchasePriceModes.IsValid(offer.LoosePriceMode)
                    || offer.LooseProcurementUnit?.Active != true
                    || !offer.CurrentProcurementUnitPrice.HasValue
                    || offer.CurrentProcurementUnitPrice.Value <= 0m
                    || offer.LooseMinimumOrderQuantity is < 0m
                    || offer.LooseQuantityStep is <= 0m
                    || !TryConversionFactor(
                        offer.IngredientId,
                        offer.LooseProcurementUnit,
                        offer.Ingredient.BaseUnit,
                        conversions,
                        allowConfiguredCountConversion: true,
                        out _))
                {
                    return SupplierPackageReadinessResult.NotReady(
                        SupplierPackageReadinessCodes.LooseContractInvalid,
                        "Cấu hình mua lẻ chưa đủ đơn vị, đơn giá, MOQ hoặc bước số lượng hợp lệ.",
                        hasValidPackageDefinition: true,
                        packageBaseQuantity: packageBaseQuantity);
                }
            }

            return SupplierPackageReadinessResult.Ready(packageBaseQuantity);
        }

        private static bool TryConversionFactor(
            int ingredientId,
            Models.Inventories.Ingredients.Unit from,
            Models.Inventories.Ingredients.Unit to,
            IReadOnlyCollection<Models.Inventories.Ingredients.UnitConversion> conversions,
            bool allowConfiguredCountConversion,
            out decimal factor)
        {
            factor = 0m;
            if (from.UnitId == to.UnitId)
            {
                factor = 1m;
                return true;
            }

            if (from.Type == UnitType.Dem || to.Type == UnitType.Dem)
            {
                if (!allowConfiguredCountConversion || from.Type != to.Type)
                    return false;
            }
            else if (from.Type == to.Type
                     && PhysicalUnitConversionRegistry.TryGetPairFactor(
                         from.UnitCode,
                         to.UnitCode,
                         from.Type,
                         to.Type,
                         out factor)
                     && factor > 0m)
            {
                return true;
            }

            var direct = conversions.FirstOrDefault(x =>
                x.IngredientId == ingredientId
                && x.FromUnitId == from.UnitId
                && x.ToUnitId == to.UnitId);
            if (direct != null && direct.FromQuantity > 0m && direct.ToQuantity > 0m)
            {
                factor = direct.ToQuantity / direct.FromQuantity;
                return factor > 0m;
            }

            var reverse = conversions.FirstOrDefault(x =>
                x.IngredientId == ingredientId
                && x.FromUnitId == to.UnitId
                && x.ToUnitId == from.UnitId);
            if (reverse != null && reverse.FromQuantity > 0m && reverse.ToQuantity > 0m)
            {
                factor = reverse.FromQuantity / reverse.ToQuantity;
                return factor > 0m;
            }

            return false;
        }
    }
}
