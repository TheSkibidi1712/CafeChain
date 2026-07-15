using System.Threading.Tasks;
using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Suppliers;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Inventories
{
    public class IngredientSupplierPackageValidator : IIngredientSupplierPackageValidator
    {
        private readonly AppDbContext _context;
        private readonly IPhysicalUnitConversionService _physical;

        public IngredientSupplierPackageValidator(
            AppDbContext context,
            IPhysicalUnitConversionService physical)
        {
            _context = context;
            _physical = physical;
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
            if (currentPrice < 0)
                return ServiceResult.Failure("Giá một gói mua không được âm.");

            if (packageQuantity.HasValue && packageQuantity.Value <= 0)
                return ServiceResult.Failure("Hàm lượng trong gói phải lớn hơn 0.");

            if (requirePackageQuantity && (!packageQuantity.HasValue || packageQuantity.Value <= 0))
                return ServiceResult.Failure("Gói mua đang Active yêu cầu hàm lượng trong gói (PackageQuantity) > 0.");

            var ingredient = await _context.Ingredients
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.IngredientId == ingredientId);
            if (ingredient == null)
                return ServiceResult.Failure($"Không tìm thấy nguyên liệu #{ingredientId}.");
            if (!ingredient.Active && isActive)
                return ServiceResult.Failure("Không thể kích hoạt nguồn cung cho nguyên liệu đã ngưng hoạt động.");

            var supplier = await _context.Suppliers
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SupplierId == supplierId);
            if (supplier == null)
                return ServiceResult.Failure($"Không tìm thấy nhà cung cấp #{supplierId}.");
            if (!supplier.Active && isActive)
                return ServiceResult.Failure("Không thể kích hoạt nguồn cung của nhà cung cấp đã ngưng hoạt động.");

            var unit = await _context.Units
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UnitId == unitId);
            if (unit == null)
                return ServiceResult.Failure($"Không tìm thấy đơn vị #{unitId}.");
            if (!unit.Active)
                return ServiceResult.Failure($"Đơn vị #{unitId} không còn hiệu lực.");

            if (PackageUnitCodes.IsRejectedCommercialPackaging(unit.UnitCode))
            {
                return ServiceResult.Failure(
                    $"Đơn vị đóng gói thương mại '{unit.UnitCode}' không được dùng làm đơn vị nội dung gói. " +
                    "Dùng đơn vị vật lý (g/kg/ml/l) hoặc đơn vị đếm kho khi BaseUnit là pcs.");
            }

            // Countable inventory: Dem only when UnitId == BaseUnitId
            if (unit.Type == UnitType.Dem && unitId != ingredient.BaseUnitId)
            {
                return ServiceResult.Failure(
                    "Đơn vị đếm chỉ được dùng làm nội dung gói khi trùng với đơn vị kho cơ sở của nguyên liệu (pcs).");
            }

            // Mass/volume: same unit or physical convert
            if (unit.Type == UnitType.KhoiLuong || unit.Type == UnitType.TheTich)
            {
                if (unitId != ingredient.BaseUnitId)
                {
                    var convert = await _physical.ConvertAsync(1m, unitId, ingredient.BaseUnitId);
                    if (!convert.IsSuccess)
                    {
                        return ServiceResult.Failure(
                            $"Đơn vị nội dung không quy đổi được sang đơn vị kho cơ sở: {convert.Message}");
                    }
                }
            }
            else if (unit.Type != UnitType.Dem)
            {
                return ServiceResult.Failure("Loại đơn vị nội dung gói không được hỗ trợ.");
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

                var convert = await _physical.ConvertAsync(1m, unitId, ingredient.BaseUnitId);
                return convert.IsSuccess;
            }

            return false;
        }

        public Task<bool> HasCompletePackageDefinitionAsync(IngredientSupplier offer)
        {
            return HasCompletePackageDefinitionAsync(
                offer.IngredientId,
                offer.UnitId,
                offer.PackageQuantity);
        }
    }
}
