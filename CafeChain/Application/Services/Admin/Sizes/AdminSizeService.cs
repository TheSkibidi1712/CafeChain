using CafeChain.Application.DTOs.Admin.Sizes;
using CafeChain.Application.Interfaces.Admin.Sizes;
using CafeChain.Infrastrusture.Interfaces.Admin.Sizes;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Drink;

namespace CafeChain.Application.Services.Admin.Sizes
{
    public class AdminSizeService : IAdminSizeService
    {
        private readonly IAdminSizeRepository _repo;

        public AdminSizeService(IAdminSizeRepository repo)
        {
            _repo = repo;
        }

        public async Task<IEnumerable<SizeDto>> GetActiveSizesAsync()
        {
            var data = await _repo.GetAllAsync();

            return data.Select(MapToDto);
        }

        public async Task<SizeDto?> GetSizeByIdAsync(int id)
        {
            var size = await _repo.GetByIdAsync(id);

            return size == null
                ? null
                : MapToDto(size);
        }

        public async Task<(bool Success, string Error)> CreateSizeAsync(SizeDto dto)
        {
            var sizeCode = NormalizeCode(dto.SizeCode);
            var name = Normalize(dto.Name);
            var description = Normalize(dto.Description);

            var validationError = await ValidateSizeAsync(
                name,
                sizeCode,
                description,
                dto.SizeType);

            if (!string.IsNullOrEmpty(validationError))
            {
                return (false, validationError);
            }

            var entity = new Size
            {
                SizeCode = sizeCode,
                Name = name,
                Description = description,
                SizeType = dto.SizeType,
                Active = true
            };

            await _repo.AddAsync(entity);
            await _repo.SaveChangesAsync();

            return (true, string.Empty);
        }

        public async Task<(bool Success, string Error)> UpdateSizeAsync(SizeDto dto)
        {
            var entity = await _repo.GetByIdAsync(dto.SizeId);

            if (entity == null)
            {
                return (false, "Size không tồn tại");
            }

            var name = Normalize(dto.Name);
            var description = Normalize(dto.Description);
            var sizeCode = NormalizeCode(dto.SizeCode);

            var validationError = await ValidateSizeAsync(
                name,
                sizeCode,
                description,
                dto.SizeType,
                dto.SizeId);

            if (!string.IsNullOrEmpty(validationError))
            {
                return (false, validationError);
            }

            entity.SizeCode = sizeCode;
            entity.Name = name;
            entity.Description = description;
            entity.SizeType = dto.SizeType;

            await _repo.UpdateAsync(entity);
            await _repo.SaveChangesAsync();

            return (true, string.Empty);
        }

        public async Task ToggleStatusAsync(int id)
        {
            var size = await _repo.GetByIdAsync(id);

            if (size == null)
            {
                throw new KeyNotFoundException("Không tìm thấy size.");
            }

            size.Active = !size.Active;

            await _repo.UpdateAsync(size);
            await _repo.SaveChangesAsync();
        }

        private async Task<string?> ValidateSizeAsync(
            string name,
            string sizeCode,
            string description,
            SizeTypeEnum sizeType,
            int? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "Tên size không được để trống";
            }

            if (string.IsNullOrWhiteSpace(sizeCode))
            {
                return "Mã size không được để trống";
            }

            if (sizeCode.Length > 20)
            {
                return "Mã size tối đa 20 ký tự";
            }

            if (name.Length > 50)
            {
                return "Tên size tối đa 50 ký tự";
            }

            if (description.Length > 300)
            {
                return "Mô tả tối đa 300 ký tự";
            }

            if (!Enum.IsDefined(typeof(SizeTypeEnum), sizeType))
            {
                return "Loại size không hợp lệ";
            }

            var nameExists = excludeId.HasValue
                ? await _repo.ExistsByNameAsync(name, excludeId.Value)
                : await _repo.ExistsByNameAsync(name);

            if (nameExists)
            {
                return "Size đã tồn tại";
            }

            var codeExists = excludeId.HasValue
                ? await _repo.ExistsBySizeCodeAsync(sizeCode, excludeId.Value)
                : await _repo.ExistsBySizeCodeAsync(sizeCode);

            return codeExists
                ? "Mã size đã tồn tại"
                : null;
        }

        private static SizeDto MapToDto(Size size)
        {
            return new SizeDto
            {
                SizeId = size.SizeId,
                SizeCode = size.SizeCode,
                Name = size.Name,
                Description = size.Description,
                SizeType = size.SizeType,
                Active = size.Active
            };
        }

        private static string NormalizeCode(string? value)
        {
            return Normalize(value).ToUpperInvariant();
        }

        private static string Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim();
        }
    }
}
