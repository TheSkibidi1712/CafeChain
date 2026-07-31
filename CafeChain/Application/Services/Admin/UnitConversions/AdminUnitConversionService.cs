using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.UnitConversions;
using CafeChain.Application.Interfaces.Admin.UnitConversions;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Inventories;
using CafeChain.Data;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CafeChain.Application.Services.Admin.UnitConversions
{
    /// <summary>
    /// #127 Admin validation + read models for physical vs measuring vs package semantics.
    /// Does not change POS/production conversion algorithm beyond save-time guards.
    /// </summary>
    public class AdminUnitConversionService : IAdminUnitConversionService
    {
        private const decimal FactorEpsilon = 0.0000001m;
        private const decimal PackageEpsilon = 0.0001m;

        private readonly AppDbContext _context;
        private readonly IPhysicalUnitConversionService _physical;
        private readonly IUnitConversionService _unitConversion;
        private readonly ILogger<AdminUnitConversionService> _logger;

        public AdminUnitConversionService(
            AppDbContext context,
            IPhysicalUnitConversionService physical,
            IUnitConversionService unitConversion,
            ILogger<AdminUnitConversionService> logger)
        {
            _context = context;
            _physical = physical;
            _unitConversion = unitConversion;
            _logger = logger;
        }

        public List<PhysicalStandardDto> GetPhysicalStandards()
        {
            return new List<PhysicalStandardDto>
            {
                new()
                {
                    FromCode = PhysicalUnitConversionRegistry.CodeKilogram,
                    ToCode = PhysicalUnitConversionRegistry.CodeGram,
                    FromQuantity = 1m,
                    ToQuantity = 1000m,
                    Dimension = "Khối lượng",
                    DisplayText = "1 kg = 1000 g",
                    Editable = false,
                    Source = "Hệ thống"
                },
                new()
                {
                    FromCode = PhysicalUnitConversionRegistry.CodeLiter,
                    ToCode = PhysicalUnitConversionRegistry.CodeMilliliter,
                    FromQuantity = 1m,
                    ToQuantity = 1000m,
                    Dimension = "Thể tích",
                    DisplayText = "1 l = 1000 ml",
                    Editable = false,
                    Source = "Hệ thống"
                }
            };
        }

        public async Task<List<AdminIngredientOptionDto>> GetIngredientOptionsAsync(string? search = null)
        {
            var q = _context.Ingredients.AsNoTracking()
                .Include(i => i.BaseUnit)
                .Where(i => i.Active);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var kw = search.Trim();
                q = q.Where(i => i.Code.Contains(kw) || i.Name.Contains(kw));
            }

            return await q.OrderBy(i => i.Code)
                .Take(200)
                .Select(i => new AdminIngredientOptionDto
                {
                    IngredientId = i.IngredientId,
                    Code = i.Code,
                    Name = i.Name,
                    BaseUnitCode = i.BaseUnit != null ? i.BaseUnit.UnitCode : ""
                })
                .ToListAsync();
        }

        public async Task<List<AdminUnitOptionDto>> GetUnitOptionsAsync()
        {
            var units = await _context.Units.AsNoTracking()
                .Where(u => u.Active)
                .OrderBy(u => u.UnitCode)
                .ToListAsync();

            return units.Select(u =>
            {
                var code = PhysicalUnitConversionRegistry.NormalizeUnitCode(u.UnitCode);
                var isPhysical = code is "g" or "kg" or "ml" or "l";
                return new AdminUnitOptionDto
                {
                    UnitId = u.UnitId,
                    UnitCode = u.UnitCode,
                    Name = u.Name,
                    Type = u.Type.ToString(),
                    IsPackagingCount = IsPackagingCountUnit(u),
                    IsPhysicalStandard = isPhysical
                };
            }).ToList();
        }

        public async Task<AdminUnitConversionIndexDto> GetIndexAsync(string? search = null, string? statusFilter = null)
        {
            var ingredientsQuery = _context.Ingredients.AsNoTracking()
                .Include(i => i.BaseUnit)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var kw = search.Trim();
                ingredientsQuery = ingredientsQuery.Where(i =>
                    i.Code.Contains(kw) || i.Name.Contains(kw));
            }

            // Only ingredients that have conversions OR match search (show empty groups with package if search)
            var conversionIngredientIds = await _context.UnitConversions.AsNoTracking()
                .Select(c => c.IngredientId)
                .Distinct()
                .ToListAsync();

            if (string.IsNullOrWhiteSpace(search))
            {
                ingredientsQuery = ingredientsQuery.Where(i => conversionIngredientIds.Contains(i.IngredientId));
            }

            var ingredients = await ingredientsQuery
                .OrderBy(i => i.Code)
                .ToListAsync();

            var ingredientIds = ingredients.Select(i => i.IngredientId).ToList();

            var conversions = await _context.UnitConversions.AsNoTracking()
                .Include(c => c.FromUnit)
                .Include(c => c.ToUnit)
                .Where(c => ingredientIds.Contains(c.IngredientId))
                .ToListAsync();

            var primaryOffers = await _context.IngredientSuppliers.AsNoTracking()
                .Include(s => s.Unit)
                .Include(s => s.Supplier)
                .Where(s => s.Active && s.IsPrimary && ingredientIds.Contains(s.IngredientId))
                .ToListAsync();

            var offerByIng = primaryOffers
                .GroupBy(o => o.IngredientId)
                .ToDictionary(g => g.Key, g => g.First());

            var groups = new List<AdminIngredientConversionGroupDto>();

            foreach (var ing in ingredients)
            {
                offerByIng.TryGetValue(ing.IngredientId, out var offer);
                var package = await BuildPackageSummaryAsync(ing, offer);

                var rows = new List<AdminUnitConversionRowDto>();
                foreach (var c in conversions.Where(x => x.IngredientId == ing.IngredientId)
                             .OrderBy(x => x.FromUnit.UnitCode)
                             .ThenBy(x => x.ToUnit.UnitCode))
                {
                    rows.Add(await MapRowAsync(c, ing, offer));
                }

                // Skip empty groups when filtering unless they have package-only search hits
                if (rows.Count == 0 && string.IsNullOrWhiteSpace(search))
                    continue;

                var hasPkgConflict = rows.Any(r => r.HasPackageConflict);
                var hasReview = rows.Any(r => r.IsCrossDimensionMassVolume);
                string statusKey = "ok";
                string statusLabel = "Hợp lệ";
                if (hasPkgConflict)
                {
                    statusKey = "package_conflict";
                    statusLabel = "Mâu thuẫn với package";
                }
                else if (hasReview)
                {
                    statusKey = "review";
                    statusLabel = "Cần xem xét";
                }

                var group = new AdminIngredientConversionGroupDto
                {
                    IngredientId = ing.IngredientId,
                    IngredientCode = ing.Code,
                    IngredientName = ing.Name,
                    BaseUnitId = ing.BaseUnitId,
                    BaseUnitCode = ing.BaseUnit?.UnitCode ?? "",
                    BaseUnitName = ing.BaseUnit?.Name ?? "",
                    Conversions = rows,
                    PrimaryPackage = package,
                    GroupStatusKey = statusKey,
                    GroupStatusLabel = statusLabel,
                    HasPackageConflict = hasPkgConflict,
                    HasReviewRows = hasReview
                };

                if (!string.IsNullOrWhiteSpace(statusFilter)
                    && !string.Equals(statusFilter, "ALL", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(statusFilter, statusKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                groups.Add(group);
            }

            return new AdminUnitConversionIndexDto
            {
                PhysicalStandards = GetPhysicalStandards(),
                Groups = groups,
                Search = search,
                StatusFilter = statusFilter ?? "ALL",
                TotalGroups = groups.Count,
                ConflictGroupCount = groups.Count(g => g.HasPackageConflict || g.HasReviewRows)
            };
        }

        public async Task<AdminUnitConversionEvaluateResult> EvaluateAsync(AdminUnitConversionEvaluateRequest request)
        {
            var result = new AdminUnitConversionEvaluateResult();

            if (request.IngredientId <= 0)
                return Fail(result, UnitConversionErrorCodes.InvalidIngredient, "Vui lòng chọn nguyên liệu.");

            if (request.FromQuantity <= 0 || request.ToQuantity <= 0)
                return Fail(result, UnitConversionErrorCodes.InvalidFactor, "Số lượng phải lớn hơn 0.");

            if (request.FromUnitId <= 0 || request.ToUnitId <= 0)
                return Fail(result, UnitConversionErrorCodes.InvalidUnit, "Vui lòng chọn đơn vị đầu vào và đầu ra.");

            if (request.FromUnitId == request.ToUnitId)
                return Fail(result, UnitConversionErrorCodes.InvalidUnit, "Đơn vị nguồn và đích không được giống nhau.");

            var ingredient = await _context.Ingredients.AsNoTracking()
                .Include(i => i.BaseUnit)
                .FirstOrDefaultAsync(i => i.IngredientId == request.IngredientId);
            if (ingredient == null)
                return Fail(result, UnitConversionErrorCodes.InvalidIngredient, "Nguyên liệu không tồn tại.");

            var fromUnit = await _context.Units.AsNoTracking().FirstOrDefaultAsync(u => u.UnitId == request.FromUnitId);
            var toUnit = await _context.Units.AsNoTracking().FirstOrDefaultAsync(u => u.UnitId == request.ToUnitId);
            if (fromUnit == null || toUnit == null)
                return Fail(result, UnitConversionErrorCodes.InvalidUnit, "Đơn vị không tồn tại.");

            result.FromUnitCode = fromUnit.UnitCode;
            result.ToUnitCode = toUnit.UnitCode;
            result.FromUnitName = fromUnit.Name;
            result.ToUnitName = toUnit.Name;
            result.FromDimension = DimensionLabel(fromUnit.Type);
            result.ToDimension = DimensionLabel(toUnit.Type);
            result.FromIsPackagingCount = IsPackagingCountUnit(fromUnit);
            result.ToIsPackagingCount = IsPackagingCountUnit(toUnit);

            var factor = request.ToQuantity / request.FromQuantity;
            result.Factor = factor;
            result.ReverseFactor = factor == 0 ? null : 1m / factor;

            // Mass ↔ volume (and any KhoiLuong↔TheTich)
            if (IsMassVolumeCross(fromUnit.Type, toUnit.Type))
            {
                result.IsCrossDimension = true;
                result.IsMassVolumeCross = true;
                return Fail(result,
                    UnitConversionErrorCodes.CrossDimensionConversionNotSupported,
                    "Không hỗ trợ quy đổi khối lượng ↔ thể tích trong Admin. Không dùng mật độ.");
            }

            if (fromUnit.Type != toUnit.Type
                && fromUnit.Type != UnitType.Dem
                && toUnit.Type != UnitType.Dem)
            {
                result.IsCrossDimension = true;
            }

            // Physical standard pairs (g/kg/ml/l)
            if (TryGetPhysicalPairFactor(fromUnit, toUnit, out var physicalFactor))
            {
                result.PhysicalExpectedFactor = physicalFactor;
                result.IsPhysicalStandard = FactorsEqual(factor, physicalFactor);
                if (result.IsPhysicalStandard)
                {
                    return Fail(result,
                        UnitConversionErrorCodes.PhysicalStandardAlreadySupported,
                        "Quy đổi này đã được hệ thống hỗ trợ sẵn (quy đổi vật lý chuẩn). Không cần lưu row riêng.");
                }

                result.HasPhysicalConflict = true;
                return Fail(result,
                    UnitConversionErrorCodes.PhysicalConversionConflict,
                    $"Quy đổi xung đột với chuẩn vật lý (kỳ vọng hệ số {physicalFactor:0.########}, đang nhập {factor:0.########}).");
            }

            // Duplicate exact pair
            var dup = await _context.UnitConversions.AsNoTracking()
                .AnyAsync(uc =>
                    uc.IngredientId == request.IngredientId
                    && uc.FromUnitId == request.FromUnitId
                    && uc.ToUnitId == request.ToUnitId
                    && (!request.UnitConversionId.HasValue || uc.UnitConversionId != request.UnitConversionId.Value));
            if (dup)
            {
                return Fail(result,
                    UnitConversionErrorCodes.DuplicateConversionPair,
                    "Quy đổi cho cặp đơn vị này của nguyên liệu đã tồn tại.");
            }

            // Package conflict (packaging count unit vs primary supplier package content)
            var offer = await _context.IngredientSuppliers.AsNoTracking()
                .Include(s => s.Unit)
                .Include(s => s.Supplier)
                .Where(s => s.IngredientId == request.IngredientId && s.Active && s.IsPrimary)
                .FirstOrDefaultAsync();

            if (offer != null
                && offer.PackageQuantity.HasValue
                && offer.PackageQuantity.Value > 0
                && offer.Unit != null)
            {
                result.PrimaryPackageQuantity = offer.PackageQuantity;
                result.PrimaryPackageUnitCode = offer.Unit.UnitCode;
                result.PrimaryPackageUnitName = offer.Unit.Name;
                result.PrimaryPackagePrice = offer.CurrentPrice;
                result.PrimarySupplierId = offer.SupplierId;
                result.PrimarySupplierName = offer.Supplier?.Name;

                if (TryComputePackageLikeQuantity(
                        request, fromUnit, toUnit, offer, out var proposed, out var pkgQty))
                {
                    result.ProposedPackageLikeQuantity = proposed;
                    if (Math.Abs(proposed - pkgQty) > PackageEpsilon)
                    {
                        result.HasPackageConflict = true;
                        result.RequiresPackageAcknowledgement = true;
                        result.Warnings.Add(
                            $"Quy cách NCC: {pkgQty:0.####} {offer.Unit.UnitCode}/gói; " +
                            $"Quy đổi đo lường ngụ ý: {proposed:0.####} {offer.Unit.UnitCode} cho 1 {fromUnit.UnitCode}. " +
                            "Giá vốn dùng quy cách nhà cung cấp — không tự chọn winner.");
                        result.Codes.Add("PACKAGE_CONFLICT");

                        if (!request.PackageConflictAcknowledged)
                        {
                            return Fail(result,
                                UnitConversionErrorCodes.PackageConflictAcknowledgementRequired,
                                "Cần xác nhận mâu thuẫn với quy cách đóng gói nhà cung cấp trước khi lưu.");
                        }

                        result.Codes.Add("PACKAGE_CONFLICT_ACKNOWLEDGED");
                    }
                }
            }

            result.IsValid = true;
            result.Message = result.HasPackageConflict
                ? "Hợp lệ sau khi xác nhận mâu thuẫn package (conversion đo lường sẽ được lưu; package không đổi)."
                : "Hợp lệ.";
            return result;
        }

        public async Task<ServiceResult<int>> CreateAsync(AdminUnitConversionEvaluateRequest request)
        {
            var eval = await EvaluateAsync(request);
            if (!eval.IsValid)
            {
                return ServiceResult<int>.Failure(
                    eval.Message ?? "Dữ liệu không hợp lệ.",
                    errorCode: eval.ErrorCode);
            }

            if (eval.HasPackageConflict && request.PackageConflictAcknowledged)
            {
                _logger.LogWarning(
                    "[AdminUnitConversion] PACKAGE_CONFLICT_ACKNOWLEDGED IngredientId={IngredientId} FromUnitId={From} ToUnitId={To} FromQty={FromQty} ToQty={ToQty} PackageQty={Pkg} PackageUnit={PkgUnit} SupplierId={SupplierId}",
                    request.IngredientId,
                    request.FromUnitId,
                    request.ToUnitId,
                    request.FromQuantity,
                    request.ToQuantity,
                    eval.PrimaryPackageQuantity,
                    eval.PrimaryPackageUnitCode,
                    eval.PrimarySupplierId);
            }

            var entity = new UnitConversion
            {
                IngredientId = request.IngredientId,
                FromUnitId = request.FromUnitId,
                FromQuantity = request.FromQuantity,
                ToUnitId = request.ToUnitId,
                ToQuantity = request.ToQuantity,
                Active = true
            };

            _context.UnitConversions.Add(entity);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return ServiceResult<int>.Failure(
                    "Quy đổi cho cặp đơn vị này của nguyên liệu đã tồn tại.",
                    errorCode: UnitConversionErrorCodes.DuplicateConversionPair);
            }

            // Ensure supplier package not mutated
            return ServiceResult<int>.Success(entity.UnitConversionId);
        }

        public async Task<ServiceResult> UpdateAsync(AdminUnitConversionEvaluateRequest request)
        {
            if (!request.UnitConversionId.HasValue || request.UnitConversionId.Value <= 0)
                return ServiceResult.Failure("Thiếu mã quy đổi.");

            var entity = await _context.UnitConversions
                .Include(c => c.FromUnit)
                .Include(c => c.ToUnit)
                .FirstOrDefaultAsync(c => c.UnitConversionId == request.UnitConversionId.Value);
            if (entity == null)
                return ServiceResult.Failure("Không tìm thấy quy đổi.");

            // Existing mass-volume: do not allow factor change via edit
            if (entity.FromUnit != null && entity.ToUnit != null
                && IsMassVolumeCross(entity.FromUnit.Type, entity.ToUnit.Type))
            {
                return ServiceResult.Failure(
                    "Quy đổi khối lượng ↔ thể tích hiện có chỉ được ngừng/xóa, không sửa hệ số.",
                    errorCode: UnitConversionErrorCodes.CrossDimensionConversionNotSupported);
            }

            var eval = await EvaluateAsync(request);
            if (!eval.IsValid)
            {
                return ServiceResult.Failure(
                    eval.Message ?? "Dữ liệu không hợp lệ.",
                    errorCode: eval.ErrorCode);
            }

            if (eval.HasPackageConflict && request.PackageConflictAcknowledged)
            {
                _logger.LogWarning(
                    "[AdminUnitConversion] PACKAGE_CONFLICT_ACKNOWLEDGED (update) Id={Id} IngredientId={IngredientId}",
                    request.UnitConversionId,
                    request.IngredientId);
            }

            entity.IngredientId = request.IngredientId;
            entity.FromUnitId = request.FromUnitId;
            entity.FromQuantity = request.FromQuantity;
            entity.ToUnitId = request.ToUnitId;
            entity.ToQuantity = request.ToQuantity;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return ServiceResult.Failure(
                    "Quy đổi cho cặp đơn vị này của nguyên liệu đã tồn tại.",
                    errorCode: UnitConversionErrorCodes.DuplicateConversionPair);
            }

            return ServiceResult.Success("Cập nhật quy đổi thành công.");
        }

        public async Task<ServiceResult> SetActiveAsync(int unitConversionId, bool active)
        {
            var entity = await _context.UnitConversions.FindAsync(unitConversionId);
            if (entity == null)
                return ServiceResult.Failure("Không tìm thấy quy đổi.");

            if (entity.Active == active)
                return ServiceResult.Success(
                    active ? "Quy đổi đã ở trạng thái hoạt động." : "Quy đổi đã ngưng hoạt động.");

            entity.Active = active;
            await _context.SaveChangesAsync();
            return ServiceResult.Success(
                active ? "Đã kích hoạt quy đổi." : "Đã ngưng hoạt động quy đổi.");
        }

        public async Task<AdminUnitConversionEvaluateRequest?> GetForEditAsync(int unitConversionId)
        {
            var entity = await _context.UnitConversions.AsNoTracking()
                .FirstOrDefaultAsync(c => c.UnitConversionId == unitConversionId);
            if (entity == null) return null;
            return new AdminUnitConversionEvaluateRequest
            {
                UnitConversionId = entity.UnitConversionId,
                IngredientId = entity.IngredientId,
                FromUnitId = entity.FromUnitId,
                FromQuantity = entity.FromQuantity,
                ToUnitId = entity.ToUnitId,
                ToQuantity = entity.ToQuantity
            };
        }

        // ── helpers ──────────────────────────────────────────────

        private async Task<AdminUnitConversionRowDto> MapRowAsync(
            UnitConversion c,
            Ingredient ing,
            IngredientSupplier? offer)
        {
            var factor = c.FromQuantity == 0 ? 0 : c.ToQuantity / c.FromQuantity;
            var reverse = factor == 0 ? 0 : 1m / factor;
            var massVol = c.FromUnit != null && c.ToUnit != null
                && IsMassVolumeCross(c.FromUnit.Type, c.ToUnit.Type);

            var hasPkg = false;
            if (offer != null && c.FromUnit != null && c.ToUnit != null)
            {
                var req = new AdminUnitConversionEvaluateRequest
                {
                    IngredientId = c.IngredientId,
                    FromUnitId = c.FromUnitId,
                    FromQuantity = c.FromQuantity,
                    ToUnitId = c.ToUnitId,
                    ToQuantity = c.ToQuantity
                };
                hasPkg = TryComputePackageLikeQuantity(
                    req, c.FromUnit, c.ToUnit, offer, out var proposed, out var pkgQty)
                    && Math.Abs(proposed - pkgQty) > PackageEpsilon;
            }

            string statusKey = "ok";
            string statusLabel = "Hợp lệ";
            bool allowEdit = true;
            if (massVol)
            {
                statusKey = "review";
                statusLabel = "Cần xem xét";
                allowEdit = false;
            }
            else if (hasPkg)
            {
                statusKey = "package_conflict";
                statusLabel = "Mâu thuẫn với package";
            }

            return new AdminUnitConversionRowDto
            {
                UnitConversionId = c.UnitConversionId,
                IngredientId = c.IngredientId,
                FromUnitId = c.FromUnitId,
                FromUnitCode = c.FromUnit?.UnitCode ?? "",
                FromUnitName = c.FromUnit?.Name ?? "",
                FromQuantity = c.FromQuantity,
                ToUnitId = c.ToUnitId,
                ToUnitCode = c.ToUnit?.UnitCode ?? "",
                ToUnitName = c.ToUnit?.Name ?? "",
                ToQuantity = c.ToQuantity,
                Active = c.Active,
                Factor = factor,
                ReverseFactor = reverse,
                StatusKey = statusKey,
                StatusLabel = statusLabel,
                IsCrossDimensionMassVolume = massVol,
                HasPackageConflict = hasPkg,
                AllowEdit = allowEdit
            };
        }

        private async Task<AdminPackageSummaryDto> BuildPackageSummaryAsync(
            Ingredient ingredient,
            IngredientSupplier? offer)
        {
            if (offer == null)
            {
                return new AdminPackageSummaryDto
                {
                    IsComplete = false,
                    IncompleteReason = "Chưa có offer primary Active.",
                    DisplayPackage = "—",
                    DisplayBaseCost = "—"
                };
            }

            var dto = new AdminPackageSummaryDto
            {
                IngredientSupplierId = offer.IngredientSupplierId,
                SupplierId = offer.SupplierId,
                SupplierName = offer.Supplier?.Name,
                PackageQuantity = offer.PackageQuantity,
                PackageUnitCode = offer.Unit?.UnitCode,
                PackageUnitName = offer.Unit?.Name,
                PackagePrice = offer.CurrentPrice,
                IsPrimary = offer.IsPrimary,
                BaseUnitCode = ingredient.BaseUnit?.UnitCode
            };

            if (!offer.PackageQuantity.HasValue || offer.PackageQuantity.Value <= 0)
            {
                dto.IsComplete = false;
                dto.IncompleteReason = "Thiếu PackageQuantity.";
                dto.DisplayPackage = "—";
                return dto;
            }

            if (offer.Unit == null)
            {
                dto.IsComplete = false;
                dto.IncompleteReason = "Thiếu đơn vị gói.";
                return dto;
            }

            if (PackageUnitCodes.IsRejectedCommercialPackaging(offer.Unit.UnitCode))
            {
                dto.IsComplete = false;
                dto.IncompleteReason =
                    $"Đơn vị gói '{offer.Unit.UnitCode}' là đóng gói thương mại — không dùng làm đơn vị nội dung cho giá vốn.";
                dto.DisplayPackage =
                    $"1 gói = {offer.PackageQuantity:0.####} {offer.Unit.UnitCode} (không hợp lệ cho costing)";
                return dto;
            }

            dto.DisplayPackage =
                $"1 gói = {offer.PackageQuantity:0.####} {offer.Unit.UnitCode}";

            if (offer.CurrentPrice <= 0)
            {
                dto.IsComplete = false;
                dto.IncompleteReason = "Giá một gói ≤ 0.";
                return dto;
            }

            var convert = await _unitConversion.ConvertAsync(
                ingredient.IngredientId,
                offer.PackageQuantity.Value,
                offer.UnitId,
                ingredient.BaseUnitId);

            if (!convert.IsSuccess || convert.Data <= 0)
            {
                dto.IsComplete = false;
                dto.IncompleteReason = convert.Message
                    ?? "Thiếu quy đổi từ đơn vị gói sang đơn vị tồn kho cơ sở.";
                return dto;
            }

            var baseCost = offer.CurrentPrice / convert.Data;
            dto.BaseUnitCost = baseCost;
            dto.IsComplete = true;
            dto.DisplayBaseCost =
                $"{baseCost:N4} VND/{ingredient.BaseUnit?.UnitCode ?? "base"}";
            return dto;
        }

        private static bool TryComputePackageLikeQuantity(
            AdminUnitConversionEvaluateRequest request,
            Unit fromUnit,
            Unit toUnit,
            IngredientSupplier offer,
            out decimal proposedContentPerPackageUnit,
            out decimal packageQuantity)
        {
            proposedContentPerPackageUnit = 0;
            packageQuantity = offer.PackageQuantity ?? 0;
            if (packageQuantity <= 0 || offer.Unit == null)
                return false;

            // Case: 1 can = X ml  and package is Y ml
            if (IsPackagingCountUnit(fromUnit)
                && offer.UnitId == request.ToUnitId
                && request.FromQuantity > 0)
            {
                proposedContentPerPackageUnit = request.ToQuantity / request.FromQuantity;
                return true;
            }

            // Case: X ml = 1 can
            if (IsPackagingCountUnit(toUnit)
                && offer.UnitId == request.FromUnitId
                && request.ToQuantity > 0)
            {
                proposedContentPerPackageUnit = request.FromQuantity / request.ToQuantity;
                return true;
            }

            return false;
        }

        private static bool TryGetPhysicalPairFactor(Unit from, Unit to, out decimal factor)
        {
            factor = 0;
            if (from.Type != to.Type)
                return false;
            if (from.Type != UnitType.KhoiLuong && from.Type != UnitType.TheTich)
                return false;

            return PhysicalUnitConversionRegistry.TryGetPairFactor(
                from.UnitCode,
                to.UnitCode,
                from.Type,
                to.Type,
                out factor);
        }

        private static bool IsMassVolumeCross(UnitType a, UnitType b)
            => (a == UnitType.KhoiLuong && b == UnitType.TheTich)
               || (a == UnitType.TheTich && b == UnitType.KhoiLuong);

        private static bool IsPackagingCountUnit(Unit u)
            => u.Type == UnitType.Dem
               && PackageUnitCodes.IsRejectedCommercialPackaging(u.UnitCode);

        private static bool FactorsEqual(decimal a, decimal b)
            => Math.Abs(a - b) <= FactorEpsilon;

        private static string DimensionLabel(UnitType t) => t switch
        {
            UnitType.KhoiLuong => "Khối lượng",
            UnitType.TheTich => "Thể tích",
            UnitType.Dem => "Đếm / đóng gói",
            _ => t.ToString()
        };

        private static AdminUnitConversionEvaluateResult Fail(
            AdminUnitConversionEvaluateResult result,
            string code,
            string message)
        {
            result.IsValid = false;
            result.ErrorCode = code;
            result.Message = message;
            if (!result.Codes.Contains(code))
                result.Codes.Add(code);
            return result;
        }
    }
}
