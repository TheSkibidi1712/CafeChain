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
            var pageEntities = await query
                .OrderBy(x => x.Code)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var ids = pageEntities.Select(x => x.PreparedItemId).ToList();
            var recipeStats = await LoadRecipeStatsAsync(ids);

            var items = pageEntities
                .Select(x => Map(x, recipeStats.GetValueOrDefault(x.PreparedItemId)))
                .ToList();

            return (items, total);
        }

        public async Task<AdminPreparedItemDTO?> GetByIdAsync(int id)
        {
            var entity = await _context.PreparedItems
                .AsNoTracking()
                .Include(x => x.BaseUnit)
                .FirstOrDefaultAsync(x => x.PreparedItemId == id);

            if (entity == null)
                return null;

            var stats = await LoadRecipeStatsAsync(new List<int> { id });
            return Map(entity, stats.GetValueOrDefault(id));
        }

        public async Task<List<AdminPreparedItemBomOptionDTO>> GetBomOptionsAsync(string? search = null)
        {
            var query = _context.PreparedItems
                .AsNoTracking()
                .Include(x => x.BaseUnit)
                .Where(x => x.Active);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var kw = search.Trim();
                query = query.Where(x => x.Code.Contains(kw) || x.Name.Contains(kw));
            }

            var entities = await query
                .OrderBy(x => x.Code)
                .Take(200)
                .ToListAsync();

            var ids = entities.Select(x => x.PreparedItemId).ToList();
            var recipeStats = await LoadRecipeStatsAsync(ids);

            return entities.Select(x =>
            {
                var s = recipeStats.GetValueOrDefault(x.PreparedItemId);
                return new AdminPreparedItemBomOptionDTO
                {
                    PreparedItemId = x.PreparedItemId,
                    Code = x.Code,
                    Name = x.Name,
                    BaseUnitId = x.BaseUnitId,
                    BaseUnitCode = x.BaseUnit?.UnitCode ?? "",
                    BaseUnitName = x.BaseUnit?.Name ?? "",
                    Active = x.Active,
                    ActiveRecipeId = s?.ActiveRecipeId,
                    ActiveRecipeCode = s?.ActiveRecipeCode,
                    ActiveRecipeName = s?.ActiveRecipeName,
                    VersionCount = s?.VersionCount ?? 0
                };
            }).ToList();
        }

        private sealed class PreparedItemRecipeStats
        {
            public int? ActiveRecipeId { get; set; }
            public string? ActiveRecipeCode { get; set; }
            public string? ActiveRecipeName { get; set; }
            public int VersionCount { get; set; }
        }

        /// <summary>
        /// Batch recipe projection for page IDs — no per-row recipe lookup.
        /// </summary>
        private async Task<Dictionary<int, PreparedItemRecipeStats>> LoadRecipeStatsAsync(List<int> preparedItemIds)
        {
            var result = new Dictionary<int, PreparedItemRecipeStats>();
            if (preparedItemIds == null || preparedItemIds.Count == 0)
                return result;

            foreach (var id in preparedItemIds)
                result[id] = new PreparedItemRecipeStats();

            var versionCounts = await _context.Recipes
                .AsNoTracking()
                .Where(r => r.PreparedItemId != null && preparedItemIds.Contains(r.PreparedItemId.Value))
                .GroupBy(r => r.PreparedItemId!.Value)
                .Select(g => new { PreparedItemId = g.Key, Count = g.Count() })
                .ToListAsync();

            foreach (var vc in versionCounts)
            {
                if (result.TryGetValue(vc.PreparedItemId, out var s))
                    s.VersionCount = vc.Count;
            }

            var activeRecipes = await _context.Recipes
                .AsNoTracking()
                .Where(r =>
                    r.PreparedItemId != null
                    && preparedItemIds.Contains(r.PreparedItemId.Value)
                    && r.Active
                    && r.Status == "Active")
                .Select(r => new
                {
                    PreparedItemId = r.PreparedItemId!.Value,
                    r.RecipeId,
                    r.RecipeCode,
                    r.Name
                })
                .ToListAsync();

            foreach (var ar in activeRecipes)
            {
                if (!result.TryGetValue(ar.PreparedItemId, out var s))
                    continue;
                // One Active per PreparedItem enforced by DB; first is fine.
                if (s.ActiveRecipeId.HasValue)
                    continue;
                s.ActiveRecipeId = ar.RecipeId;
                s.ActiveRecipeCode = ar.RecipeCode;
                s.ActiveRecipeName = ar.Name;
            }

            return result;
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

        private static AdminPreparedItemDTO Map(PreparedItem x, PreparedItemRecipeStats? stats = null)
        {
            stats ??= new PreparedItemRecipeStats();
            string configKey;
            string configLabel;
            if (!x.Active)
            {
                configKey = "inactive";
                configLabel = "Ngừng hoạt động";
            }
            else if (stats.ActiveRecipeId.HasValue)
            {
                configKey = "has_active";
                configLabel = "Có công thức hoạt động";
            }
            else
            {
                configKey = "no_recipe";
                configLabel = "Chưa có công thức";
            }

            return new AdminPreparedItemDTO
            {
                PreparedItemId = x.PreparedItemId,
                Code = x.Code,
                Name = x.Name,
                BaseUnitId = x.BaseUnitId,
                BaseUnitCode = x.BaseUnit?.UnitCode ?? "",
                BaseUnitName = x.BaseUnit?.Name ?? "",
                Description = x.Description,
                Active = x.Active,
                ActiveRecipeId = stats.ActiveRecipeId,
                ActiveRecipeCode = stats.ActiveRecipeCode,
                ActiveRecipeName = stats.ActiveRecipeName,
                VersionCount = stats.VersionCount,
                ConfigStatus = configLabel,
                ConfigStatusKey = configKey
            };
        }

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
