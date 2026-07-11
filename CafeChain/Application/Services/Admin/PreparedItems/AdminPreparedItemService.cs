using System;
using System.Linq;
using System.Threading.Tasks;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.PreparedItems;
using CafeChain.Application.DTOs.Admin.Units;
using CafeChain.Application.Interfaces.Admin.PreparedItems;
using CafeChain.Data;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.PreparedItems;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Admin.PreparedItems
{
    public class AdminPreparedItemService : IAdminPreparedItemService
    {
        private readonly AppDbContext _context;

        public AdminPreparedItemService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<(List<AdminPreparedItemDTO> Items, int Total)> GetPagedAsync(
            string? search,
            bool? status,
            int page,
            int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            var query = _context.PreparedItems
                .AsNoTracking()
                .Include(x => x.BaseUnit)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var kw = search.Trim();
                query = query.Where(x =>
                    x.Code.Contains(kw) ||
                    x.Name.Contains(kw));
            }

            if (status.HasValue)
                query = query.Where(x => x.Active == status.Value);

            var total = await query.CountAsync();
            var items = await query
                .OrderBy(x => x.Code)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => Map(x))
                .ToListAsync();

            return (items, total);
        }

        public async Task<AdminPreparedItemDTO?> GetByIdAsync(int id)
        {
            var entity = await _context.PreparedItems
                .AsNoTracking()
                .Include(x => x.BaseUnit)
                .FirstOrDefaultAsync(x => x.PreparedItemId == id);

            return entity == null ? null : Map(entity);
        }

        public async Task<int> CreateAsync(AdminPreparedItemSaveDTO dto)
        {
            var code = NormalizeCode(dto.Code);
            var name = NormalizeText(dto.Name);
            var description = NormalizeDescription(dto.Description);

            await ValidateAsync(code, name, description, dto.BaseUnitId, excludeId: null);

            var entity = new PreparedItem
            {
                Code = code,
                Name = name,
                BaseUnitId = dto.BaseUnitId,
                Description = description,
                Active = true
            };

            _context.PreparedItems.Add(entity);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                throw new InvalidOperationException($"Mã BTP '{code}' đã tồn tại.");
            }

            return entity.PreparedItemId;
        }

        public async Task UpdateAsync(AdminPreparedItemSaveDTO dto)
        {
            if (!dto.PreparedItemId.HasValue || dto.PreparedItemId.Value <= 0)
                throw new InvalidOperationException("Thiếu mã bán thành phẩm.");

            var entity = await _context.PreparedItems
                .FirstOrDefaultAsync(x => x.PreparedItemId == dto.PreparedItemId.Value)
                ?? throw new InvalidOperationException("Không tìm thấy bán thành phẩm.");

            var code = NormalizeCode(dto.Code);
            var name = NormalizeText(dto.Name);
            var description = NormalizeDescription(dto.Description);

            await ValidateAsync(code, name, description, dto.BaseUnitId, excludeId: entity.PreparedItemId);

            entity.Code = code;
            entity.Name = name;
            entity.BaseUnitId = dto.BaseUnitId;
            entity.Description = description;
            entity.Active = dto.Active;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                throw new InvalidOperationException($"Mã BTP '{code}' đã tồn tại.");
            }
        }

        public async Task SetActiveAsync(int preparedItemId, bool active)
        {
            var entity = await _context.PreparedItems
                .FirstOrDefaultAsync(x => x.PreparedItemId == preparedItemId)
                ?? throw new InvalidOperationException("Không tìm thấy bán thành phẩm.");

            // Re-enable must revalidate current BaseUnit (exists, Active, not packaging).
            if (active)
                await ValidateBaseUnitAsync(entity.BaseUnitId);

            entity.Active = active;
            await _context.SaveChangesAsync();
        }

        public async Task<List<UnitDTO>> GetInventoryUnitsAsync()
        {
            // PackageUnitCodes helper is not EF-translatable — filter packaging client-side.
            var units = await _context.Units
                .AsNoTracking()
                .Where(u => u.Active
                    && (u.Type == UnitType.KhoiLuong
                        || u.Type == UnitType.TheTich
                        || u.Type == UnitType.Dem))
                .OrderBy(u => u.UnitCode)
                .Select(u => new UnitDTO
                {
                    UnitId = u.UnitId,
                    Name = u.Name,
                    UnitCode = u.UnitCode,
                    Type = u.Type.ToString()
                })
                .ToListAsync();

            return units
                .Where(u => !PackageUnitCodes.IsRejectedCommercialPackaging(u.UnitCode))
                .ToList();
        }

        private async Task ValidateAsync(
            string code,
            string name,
            string? description,
            int baseUnitId,
            int? excludeId)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new InvalidOperationException("Mã BTP là bắt buộc.");

            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Tên bán thành phẩm là bắt buộc.");

            if (description != null && description.Length > 500)
                throw new InvalidOperationException("Mô tả tối đa 500 ký tự.");

            await ValidateBaseUnitAsync(baseUnitId);

            var duplicate = await _context.PreparedItems
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Code == code
                    && (!excludeId.HasValue || x.PreparedItemId != excludeId.Value));

            if (duplicate)
                throw new InvalidOperationException($"Mã BTP '{code}' đã tồn tại.");
        }

        private async Task ValidateBaseUnitAsync(int baseUnitId)
        {
            var unit = await _context.Units
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UnitId == baseUnitId);

            if (unit == null)
                throw new InvalidOperationException("Đơn vị tồn kho chuẩn không tồn tại.");

            if (!unit.Active)
                throw new InvalidOperationException("Đơn vị tồn kho chuẩn không còn hiệu lực.");

            if (unit.Type != UnitType.KhoiLuong
                && unit.Type != UnitType.TheTich
                && unit.Type != UnitType.Dem)
            {
                throw new InvalidOperationException("Loại đơn vị tồn kho không được hỗ trợ.");
            }

            if (PackageUnitCodes.IsRejectedCommercialPackaging(unit.UnitCode))
            {
                throw new InvalidOperationException(
                    $"Đơn vị đóng gói thương mại '{unit.UnitCode}' không được dùng làm đơn vị tồn kho BTP. " +
                    "Dùng g/kg/ml/l hoặc pcs.");
            }
        }

        public static string NormalizeCode(string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return string.Empty;
            return code.Trim().ToUpperInvariant();
        }

        private static string NormalizeText(string? text)
            => text?.Trim() ?? string.Empty;

        private static string? NormalizeDescription(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;
            var t = text.Trim();
            return t.Length == 0 ? null : t;
        }

        private static AdminPreparedItemDTO Map(PreparedItem x) => new()
        {
            PreparedItemId = x.PreparedItemId,
            Code = x.Code,
            Name = x.Name,
            BaseUnitId = x.BaseUnitId,
            BaseUnitCode = x.BaseUnit?.UnitCode ?? "",
            BaseUnitName = x.BaseUnit?.Name ?? "",
            Description = x.Description,
            Active = x.Active
        };

        private static bool IsUniqueViolation(DbUpdateException ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            return msg.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("unique", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("IX_PreparedItems_Code", StringComparison.OrdinalIgnoreCase);
        }
    }
}
