using CafeChain.Application.DTOs.Admin.Sizes;
using CafeChain.Infrastrusture.Interfaces.Admin.Sizes;
using CafeChain.Models.Drinks;
using Microsoft.EntityFrameworkCore;
using CafeChain.Application.Interfaces.Admin.Sizes;

namespace CafeChain.Application.Services.Admin.Sizes
{
    public class AdminSizeService : IAdminSizeService
    {
        private readonly IAdminSizeRepository _repo;
        public AdminSizeService(IAdminSizeRepository repo)
        {
            _repo = repo;
        }

        public async Task<(bool Success, string Error)> CreateSizeAsync(SizeDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return (false, "Tên size không được để trống");

            if (await _repo.ExistsByNameAsync(dto.Name))
                return (false, "Size đã tồn tại");

            var entity = new Size
            {
                Name = dto.Name,
                Description = dto.Description,
                Active = true
            };

            await _repo.AddAsync(entity);
            await _repo.SaveChangesAsync();

            return (true, "");
        }

        public async Task ToggleStatusAsync(int id)
        {
            var size = await _repo.GetByIdAsync(id);
            if (size != null)
            {
                size.Active = !size.Active; // Soft Delete / Deactivate
                await _repo.UpdateAsync(size);
                await _repo.SaveChangesAsync();
            }
        }

        private void ValidateSizeData(SizeDto dto)
        {
            if (string.IsNullOrEmpty(dto.Name)) throw new Exception("Name is required");
            // Thêm các logic check trùng mã size, v.v. ở đây
        }

        // Các hàm Mapping khác (Có thể dùng AutoMapper để clean hơn)
        public async Task<IEnumerable<SizeDto>> GetActiveSizesAsync()
        {
            var data = await _repo.GetAllAsync();
            return data.Select(s => new SizeDto { SizeId = s.SizeId, Name = s.Name, Description = s.Description, Active = s.Active });
        }

        public async Task<SizeDto> GetSizeByIdAsync(int id)
        {
            var s = await _repo.GetByIdAsync(id);
            return s == null ? null : new SizeDto { SizeId = s.SizeId, Name = s.Name, Description = s.Description, Active = s.Active };
        }

        public async Task<(bool Success, string Error)> UpdateSizeAsync(SizeDto dto)
        {
            var entity = await _repo.GetByIdAsync(dto.SizeId);

            if (entity == null)
                return (false, "Size không tồn tại");

            if (await _repo.ExistsByNameAsync(dto.Name, dto.SizeId))
                return (false, "Size đã tồn tại");

            entity.Name = dto.Name;
            entity.Description = dto.Description;

            await _repo.UpdateAsync(entity);
            await _repo.SaveChangesAsync();

            return (true, "");
        }
    }
}
