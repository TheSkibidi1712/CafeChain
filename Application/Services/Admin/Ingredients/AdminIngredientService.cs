using CafeChain.Application.DTOs.Admin.Ingredients;
using CafeChain.Application.Interfaces.Admin.Ingredients;
using CafeChain.Infrastrusture.Interfaces.Admin.Ingredients;
using CafeChain.Application.DTOs.Admin.Units;
using CafeChain.Models.Inventories;

namespace CafeChain.Application.Services.Admin.Ingredients
{
    public class AdminIngredientService : IAdminIngredientService
    {
        private readonly IAdminIngredientRepository _repo;

        public AdminIngredientService(IAdminIngredientRepository repo)
        {
            _repo = repo;
        }

        // ================= GET Paged =================
        public async Task<(List<AdminIngredientDTO> Items, int Total)> GetPagedAsync(string? search, bool? status, int page, int pageSize)
        {
            var (data, total) = await _repo.GetPagedAsync(search, status, page, pageSize);

            return (
                data.Select(MapToListDTO).ToList(),
                total
            );
        }

        // ================= GET BY ID =================
        public async Task<AdminIngredientUpdateDTO?> GetByIdAsync(int id)
        {
            var entity = await _repo.GetByIdAsync(id);
            if (entity == null) return null;

            return MapToUpdateDTO(entity);
        }

        // ================= CREATE =================
        public async Task<int> CreateAsync(AdminIngredientCreateDTO dto)
        {
            dto.Code = NormalizeCode(dto.Code);
            dto.Name = NormalizeText(dto.Name);

            await ValidateAsync(dto.Code, dto.Name);
            ValidateConversions(dto.Conversions);

            var ingredient = new Ingredient
            {
                Code = dto.Code,
                Name = dto.Name,
                BaseUnitId = dto.BaseUnitId,
                Active = true,
                UnitConversions = MapConversions(dto.Conversions) // 🔥 gán trực tiếp
            };

            await _repo.CreateAsync(ingredient);

            // 🔥 SAVE 1 LẦN (EF tự insert cả conversions)
            await _repo.SaveChangesAsync();
            return ingredient.IngredientId;
        }

        // ================= UPDATE =================
        public async Task UpdateAsync(AdminIngredientUpdateDTO dto)
        {
            var ingredient = await _repo.GetByIdAsync(dto.IngredientId);
            if (ingredient == null)
                throw new Exception("Không tìm thấy nguyên liệu");

            dto.Code = NormalizeCode(dto.Code);
            dto.Name = NormalizeText(dto.Name);


            await ValidateAsync(dto.Code, dto.Name, dto.IngredientId);
            ValidateConversions(dto.Conversions);

            // update basic
            ingredient.Code = dto.Code;
            ingredient.Name = dto.Name;
            ingredient.BaseUnitId = dto.BaseUnitId;
            ingredient.Active = dto.Active;

            await _repo.UpdateAsync(ingredient);

            // 🔥 replace conversions
            var conversions = MapConversions(dto.Conversions);
            await _repo.ReplaceConversionsAsync(ingredient.IngredientId, conversions);

            await _repo.SaveChangesAsync();
        }

        // ================= TOGGLE =================
        public async Task ToggleStatusAsync(int id)
        {
            await _repo.ToggleStatus(id);
            await _repo.SaveChangesAsync();
        }

        // ================= GET UNITS =================
        public async Task<List<UnitDTO>> GetUnitsAsync()
        {
            var units = await _repo.GetActiveUnitsAsync();

            return units.Select(x => new UnitDTO
            {
                UnitId = x.UnitId,
                Name = x.Name,
                UnitCode = x.UnitCode,
                Type = x.Type.ToString() // 🔥 FIX
            }).ToList();
        }

        // =========================================================
        // ================= PRIVATE HELPERS =======================
        // =========================================================

        private static AdminIngredientDTO MapToListDTO(Ingredient x)
        {
            return new AdminIngredientDTO
            {
                IngredientId = x.IngredientId,
                Code = x.Code,
                Name = x.Name,
                BaseUnitName = x.BaseUnit?.Name,
                Active = x.Active
            };
        }

        private static AdminIngredientUpdateDTO MapToUpdateDTO(Ingredient x)
        {
            return new AdminIngredientUpdateDTO
            {
                IngredientId = x.IngredientId,
                Code = x.Code,
                Name = x.Name,
                BaseUnitId = x.BaseUnitId,
                BaseUnitName = x.BaseUnit?.Name, // 🔥 FIX
                Active = x.Active,

                Conversions = x.UnitConversions.Select(c => new UnitConversionDTO
                {
                    UnitConversionId = c.UnitConversionId,
                    FromUnitId = c.FromUnitId,
                    FromQuantity = c.FromQuantity,
                    ToUnitId = c.ToUnitId,
                    ToQuantity = c.ToQuantity,

                    FromUnitName = c.FromUnit?.Name, // 🔥 FIX
                    ToUnitName = c.ToUnit?.Name      // 🔥 FIX
                }).ToList()
            };
        }

        private static List<UnitConversion> MapConversions(List<UnitConversionDTO>? list)
        {
            if (list == null || !list.Any())
                return new List<UnitConversion>();

            return list.Select(c => new UnitConversion
            {
                FromUnitId = c.FromUnitId,
                FromQuantity = c.FromQuantity,
                ToUnitId = c.ToUnitId,
                ToQuantity = c.ToQuantity
            }).ToList();
        }

        private async Task ValidateAsync(string code, string name, int? excludeId = null)
        {
            if (await _repo.IsCodeExists(code, excludeId))
                throw new Exception("Mã nguyên liệu đã tồn tại");

            if (await _repo.IsNameExists(name, excludeId))
                throw new Exception("Tên nguyên liệu đã tồn tại");
        }

        private void ValidateConversions(List<UnitConversionDTO>? list)
        {
            if (list == null || !list.Any())
                return;

            var set = new HashSet<string>();

            foreach (var c in list)
            {
                // ❌ không cho unit giống nhau
                if (c.FromUnitId == c.ToUnitId)
                    throw new Exception("Không thể quy đổi cùng 1 đơn vị");

                // ❌ quantity <= 0
                if (c.FromQuantity <= 0 || c.ToQuantity <= 0)
                    throw new Exception("Số lượng phải lớn hơn 0");

                // 🔥 normalize key (tránh A->B và B->A)
                var key1 = $"{c.FromUnitId}-{c.ToUnitId}";
                var key2 = $"{c.ToUnitId}-{c.FromUnitId}";

                if (set.Contains(key1) || set.Contains(key2))
                    throw new Exception("Quy đổi đơn vị bị trùng hoặc đảo chiều");

                set.Add(key1);
            }
        }

        private static string NormalizeCode(string code)
            => code?.Trim().ToUpper() ?? "";

        private static string NormalizeText(string text)
            => text?.Trim() ?? "";
    }
}
