using System.Threading.Tasks;
using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces.Admin.Recipes;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Enums.Unit;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Admin.Recipes
{
    public class RecipeOutputNormalizer : IRecipeOutputNormalizer
    {
        private readonly AppDbContext _context;
        private readonly IPhysicalUnitConversionService _physical;

        public RecipeOutputNormalizer(
            AppDbContext context,
            IPhysicalUnitConversionService physical)
        {
            _context = context;
            _physical = physical;
        }

        public async Task<ServiceResult<RecipeOutputNormalizationResult>> NormalizeAsync(
            int preparedItemId,
            decimal outputQuantity,
            int outputUnitId)
        {
            if (preparedItemId <= 0)
                return ServiceResult<RecipeOutputNormalizationResult>.Failure("Bán thành phẩm đầu ra là bắt buộc.");

            if (outputQuantity <= 0)
            {
                return ServiceResult<RecipeOutputNormalizationResult>.Failure(
                    "Sản lượng dự kiến sau hao hụt chuẩn phải lớn hơn 0.");
            }

            if (outputUnitId <= 0)
                return ServiceResult<RecipeOutputNormalizationResult>.Failure("Đơn vị đầu ra là bắt buộc.");

            var prepared = await _context.PreparedItems
                .AsNoTracking()
                .Include(p => p.BaseUnit)
                .FirstOrDefaultAsync(p => p.PreparedItemId == preparedItemId);

            if (prepared == null)
                return ServiceResult<RecipeOutputNormalizationResult>.Failure("Bán thành phẩm không tồn tại.");

            if (!prepared.Active)
            {
                return ServiceResult<RecipeOutputNormalizationResult>.Failure(
                    "Bán thành phẩm không còn hiệu lực — không thể gán cho công thức mới/phiên bản mới.");
            }

            var outputUnit = await _context.Units
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UnitId == outputUnitId);

            if (outputUnit == null)
                return ServiceResult<RecipeOutputNormalizationResult>.Failure("Đơn vị đầu ra không tồn tại.");

            if (!outputUnit.Active)
                return ServiceResult<RecipeOutputNormalizationResult>.Failure("Đơn vị đầu ra không còn hiệu lực.");

            if (outputUnit.Type != UnitType.KhoiLuong
                && outputUnit.Type != UnitType.TheTich
                && outputUnit.Type != UnitType.Dem)
            {
                return ServiceResult<RecipeOutputNormalizationResult>.Failure(
                    "Loại đơn vị đầu ra không được hỗ trợ.");
            }

            if (PackageUnitCodes.IsRejectedCommercialPackaging(outputUnit.UnitCode))
            {
                return ServiceResult<RecipeOutputNormalizationResult>.Failure(
                    $"Đơn vị đóng gói thương mại '{outputUnit.UnitCode}' không được dùng làm đơn vị đầu ra BTP. " +
                    "Dùng g/kg/ml/l hoặc pcs.");
            }

            // Normalize into PreparedItem base unit (no YieldPercentage).
            var convert = await _physical.ConvertAsync(
                outputQuantity,
                outputUnitId,
                prepared.BaseUnitId);

            if (!convert.IsSuccess)
            {
                return ServiceResult<RecipeOutputNormalizationResult>.Failure(
                    convert.Message
                    ?? "Không thể quy đổi sản lượng đầu ra sang đơn vị tồn kho chuẩn của BTP.",
                    errorCode: convert.ErrorCode);
            }

            var baseUnit = prepared.BaseUnit;
            return ServiceResult<RecipeOutputNormalizationResult>.Success(new RecipeOutputNormalizationResult
            {
                PreparedItemId = prepared.PreparedItemId,
                PreparedItemCode = prepared.Code,
                PreparedItemName = prepared.Name,
                BaseUnitId = prepared.BaseUnitId,
                BaseUnitCode = baseUnit?.UnitCode ?? "",
                BaseUnitName = baseUnit?.Name ?? "",
                OutputUnitId = outputUnit.UnitId,
                OutputUnitCode = outputUnit.UnitCode,
                OutputUnitName = outputUnit.Name,
                OutputQuantity = outputQuantity,
                NormalizedQuantityInBase = convert.Data
            });
        }
    }
}
