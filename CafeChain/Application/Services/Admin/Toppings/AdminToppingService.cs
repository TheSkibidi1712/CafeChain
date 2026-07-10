using CafeChain.Application.Constants.Cloudinaries;
using CafeChain.Application.DTOs.Admin.Toppings;
using CafeChain.Application.Interfaces.Admin.Toppings;
using CafeChain.Application.Interfaces.Cloudinaries;
using CafeChain.Infrastrusture.Interfaces.Admin.Toppings;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Cloudinaries;
namespace CafeChain.Application.Services.Admin.Toppings
{
    public class AdminToppingService : IAdminToppingService
    {
        private readonly IAdminToppingRepository _repo;
        private readonly ICloudinaryService _cloudinaryService;


        public AdminToppingService(IAdminToppingRepository repo, ICloudinaryService cloudinaryService)
        {
            _repo = repo;
            _cloudinaryService = cloudinaryService;
        }

        // ================= GET ALL =================
        public async Task<IEnumerable<ToppingDto>> GetAllAsync()
        {
            var data = await _repo.GetAllAsync();

            return data.Select(x => new ToppingDto
            {
                ToppingId = x.ToppingId,
                ToppingCode = x.ToppingCode,
                Name = x.Name,
                Price = x.Price,
                ImageUrl = x.ImageUrl,
                ImagePublicId = x.ImagePublicId,
                Active = x.Active
            });
        }

        // ================= GET ACTIVE =================
        public async Task<IEnumerable<ToppingDto>> GetActiveAsync()
        {
            var data = await _repo.GetActiveAsync();

            return data.Select(x => new ToppingDto
            {
                ToppingId = x.ToppingId,
                ToppingCode = x.ToppingCode,
                Name = x.Name,
                Price = x.Price,
                ImageUrl = x.ImageUrl,
                ImagePublicId = x.ImagePublicId,
                Active = x.Active
            });
        }

        // ================= GET BY ID =================
        public async Task<ToppingDto?> GetByIdAsync(int id)
        {
            var entity = await _repo.GetByIdAsync(id);

            if (entity == null)
                return null;

            return new ToppingDto
            {
                ToppingId = entity.ToppingId,
                ToppingCode = entity.ToppingCode,
                Name = entity.Name,
                Price = entity.Price,
                ImageUrl = entity.ImageUrl,
                ImagePublicId = entity.ImagePublicId,
                Active = entity.Active
            };
        }

        // ================= CREATE =================
        public async Task CreateAsync(ToppingDto dto)
        {
            Validate(dto);

            if (await _repo.ExistsByToppingCodeAsync(dto.ToppingCode))
                throw new Exception("Mã topping đã tồn tại");

            if (await _repo.ExistsByNameAsync(dto.Name))
                throw new Exception("Tên topping đã tồn tại");

            string? imageUrl = null;
            string? imagePublicId = null;

            if (dto.ImageFile != null)
            {
                var uploadResult = await _cloudinaryService.UploadAsync(dto.ImageFile, ImageFolder.ToppingImages, ImageCategory.Topping);

                imageUrl = uploadResult.Url;
                imagePublicId = uploadResult.PublicId;
            }

            var entity = new Topping
            {
                ToppingCode = dto.ToppingCode,
                Name = dto.Name,
                Price = dto.Price,

                ImageUrl = imageUrl,
                ImagePublicId = imagePublicId,

                Active = true
            };

            await _repo.AddAsync(entity);
            await _repo.SaveChangesAsync();
        }

        // ================= UPDATE =================
        public async Task UpdateAsync(ToppingDto dto)
        {
            Validate(dto);

            var entity = await _repo.GetByIdAsync(dto.ToppingId);

            if (entity == null)
                throw new Exception("Topping không tồn tại");

            if (await _repo.ExistsByNameAsync(dto.Name, dto.ToppingId))
                throw new Exception("Tên topping đã tồn tại");

            if (await _repo.ExistsByToppingCodeAsync(dto.ToppingCode, dto.ToppingId))
                throw new Exception("Mã topping đã tồn tại");

            entity.ToppingCode = dto.ToppingCode;
            entity.Name = dto.Name;
            entity.Price = dto.Price;

            if (dto.ImageFile != null)
            {
                if (!string.IsNullOrWhiteSpace(entity.ImagePublicId))
                {
                    await _cloudinaryService.DeleteAsync(
                        entity.ImagePublicId);
                }

                var uploadResult =
                    await _cloudinaryService.UploadAsync(
                        dto.ImageFile,
                        ImageFolder.ToppingImages,
                        ImageCategory.Topping);

                entity.ImageUrl = uploadResult.Url;
                entity.ImagePublicId = uploadResult.PublicId;
            }

            _repo.Update(entity);

            await _repo.SaveChangesAsync();
        }

        // ================= TOGGLE STATUS =================
        public async Task ToggleStatusAsync(int id)
        {
            var entity = await _repo.GetByIdAsync(id);

            if (entity == null)
                throw new Exception("Topping không tồn tại");

            entity.Active = !entity.Active;

            _repo.Update(entity);
            await _repo.SaveChangesAsync();
        }

        // ================= VALIDATION =================
        private void Validate(ToppingDto dto)
        {
            if (dto == null)
                throw new Exception("Dữ liệu không hợp lệ");

            if (string.IsNullOrWhiteSpace(dto.ToppingCode))
                throw new Exception("Mã topping không được để trống");

            dto.ToppingCode = dto.ToppingCode.Trim().ToUpperInvariant();

            if (dto.ToppingCode.Length > 50)
                throw new Exception("Mã topping tối đa 50 ký tự");

            if (string.IsNullOrWhiteSpace(dto.Name))
                throw new Exception("Tên topping không được để trống");

            dto.Name = dto.Name.Trim();

            if (dto.Name.Length > 100)
                throw new Exception("Tên topping tối đa 100 ký tự");

            if (dto.Price <= 0)
                throw new Exception("Giá phải lớn hơn 0");

            if (dto.ToppingId == 0 && dto.ImageFile == null)
                throw new Exception("Vui lòng chọn ảnh topping");
        }
    }
}
