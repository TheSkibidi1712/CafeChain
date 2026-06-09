using CafeChain.Application.Constants.Cloudinaries;
using CafeChain.Application.DTOs.Admin.Drinks;
using CafeChain.Application.Interfaces.Admin.Drinks;
using CafeChain.Application.Interfaces.Cloudinaries;
using CafeChain.Infrastrusture.Interfaces.Admin.Drinks;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Cloudinaries;
using Microsoft.AspNetCore.Hosting;

namespace CafeChain.Application.Services.Admin.Drinks
{
    public class AdminDrinkService : IAdminDrinkService
    {
        private readonly IAdminDrinkRepository _drinkRepository;
        private readonly ICloudinaryService _cloudinaryService;

        public AdminDrinkService(IAdminDrinkRepository drinkRepository, ICloudinaryService cloudinaryService)
        {
            _drinkRepository = drinkRepository;
            _cloudinaryService = cloudinaryService;
        }

        public async Task<IEnumerable<AdminDrinkDTO>> GetAllDrinksAsync()
        {
            var drinks = await _drinkRepository.GetAllDrinksAsync();
            return drinks.Select(d => new AdminDrinkDTO
            {
                DrinkId = d.DrinkId,
                Name = d.Name,
                CategoryName = d.Category?.Name ?? "Không có",
                ProductTypeName = d.ProductType?.Name ?? "Không có",
                Description = d.Description,
                Active = d.Active,
                ImageUrl = d.DrinkImages?.FirstOrDefault(i => i.IsDefault)?.ImageUrl 
                           ?? d.DrinkImages?.FirstOrDefault()?.ImageUrl
                           ?? "/Images/NoImage.png"
            });
        }

        public async Task<AdminDrinkDTO> GetDrinkByIdAsync(int id)
        {
            var drink = await _drinkRepository.GetDrinkByIdAsync(id);
            if (drink == null) return null;

            return new AdminDrinkDTO
            {
                DrinkId = drink.DrinkId,
                Name = drink.Name,
                CategoryName = drink.Category?.Name ?? "Không có",
                ProductTypeName = drink.ProductType?.Name ?? "Không có",
                Description = drink.Description,
                Active = drink.Active,
                ImageUrl = drink.DrinkImages?.FirstOrDefault(i => i.IsDefault)?.ImageUrl 
                           ?? drink.DrinkImages?.FirstOrDefault()?.ImageUrl
                           ?? "/Images/NoImage.png"
            };
        }

        public async Task<int> CreateDrinkAsync(AdminDrinkCreateDTO dto)
        {
            if (await _drinkRepository.IsDrinkNameExistsAsync(dto.Name))
            {
                throw new ArgumentException("Tên nước uống đã tồn tại.");
            }

            var drink = new Drink
            {
                Name = dto.Name,
                CategoryId = dto.CategoryId,
                ProductTypeId = dto.ProductTypeId,
                Description = dto.Description,
                Active = true,
                CreatedAt = DateTime.Now
            };

            await _drinkRepository.CreateDrinkAsync(drink);

            await _drinkRepository.SaveChangesAsync();

            int drinkId = drink.DrinkId;

            if (dto.ImageFiles != null && dto.ImageFiles.Any())
            {
                for (int i = 0; i < dto.ImageFiles.Count; i++)
                {
                    var file = dto.ImageFiles[i];

                    if (file == null || file.Length == 0)
                    {
                        continue;
                    }

                    var uploadResult =
                        await _cloudinaryService.UploadAsync(file,ImageFolder.DrinkImages, ImageCategory.Drink);

                    var image = new DrinkImage
                    {
                        DrinkId = drinkId,
                        ImageUrl = uploadResult.Url,
                        PublicId = uploadResult.PublicId,
                        CreatedAt = DateTime.Now,
                        IsDefault = dto.DefaultImageIndex.HasValue
                            ? dto.DefaultImageIndex.Value == i
                            : i == 0
                    };

                    await _drinkRepository.AddDrinkImageAsync(image);
                }

                await _drinkRepository.SaveChangesAsync();
            }

            return drinkId;
        }

        public async Task<AdminDrinkUpdateDTO> GetDrinkForUpdateAsync(int id)
        {
            var drink = await _drinkRepository.GetDrinkByIdAsync(id);
            if (drink == null) return null;

            return new AdminDrinkUpdateDTO
            {
                DrinkId = drink.DrinkId,
                Name = drink.Name,
                CategoryId = drink.CategoryId ?? 0,
                ProductTypeId = drink.ProductTypeId,
                Description = drink.Description,
                Active = drink.Active
            };
        }

        public async Task UpdateDrinkAsync(AdminDrinkUpdateDTO updateDTO)
        {
            if (await _drinkRepository.IsDrinkNameExistsAsync(updateDTO.Name, updateDTO.DrinkId))
            {
                throw new ArgumentException("Tên nước uống đã tồn tại.");
            }

            var drink = await _drinkRepository.GetDrinkByIdAsync(updateDTO.DrinkId);
            if (drink == null) throw new KeyNotFoundException("Không tìm thấy nước uống.");

            drink.Name = updateDTO.Name;
            drink.CategoryId = updateDTO.CategoryId == 0 ? null : updateDTO.CategoryId;
            drink.ProductTypeId = updateDTO.ProductTypeId;
            drink.Description = updateDTO.Description;
            drink.Active = updateDTO.Active;

            await _drinkRepository.UpdateDrinkAsync(drink);

            await _drinkRepository.SaveChangesAsync();
        }

        public async Task ToggleDrinkStatusAsync(int id)
        {
            await _drinkRepository.ToggleDrinkStatusAsync(id);

            await _drinkRepository.SaveChangesAsync();
        }

        public async Task<IEnumerable<DrinkCategory>> GetDrinkCategoriesAsync()
        {
            return await _drinkRepository.GetDrinkCategoriesAsync();
        }

        public async Task<IEnumerable<ProductType>> GetProductTypesAsync()
        {
            return await _drinkRepository.GetProductTypesAsync();
        }

        // Image Management
        public async Task<IEnumerable<AdminDrinkImageDTO>> GetDrinkImagesAsync(int drinkId)
        {
            var images = await _drinkRepository.GetDrinkImagesAsync(drinkId);
            return images.Select(img => new AdminDrinkImageDTO
            {
                DrinkImageId = img.DrinkImageId,
                DrinkId = img.DrinkId,
                ImageUrl = img.ImageUrl,
                IsDefault = img.IsDefault
            });
        }

        public async Task AddDrinkImageAsync(int drinkId, IFormFile imageFile, bool isDefault)
        {
            if (imageFile == null || imageFile.Length == 0)
            {
                throw new Exception("File không hợp lệ");
            }

            var uploadResult = await _cloudinaryService.UploadAsync(imageFile, ImageFolder.DrinkImages, ImageCategory.Drink);

            var entity = new DrinkImage
            {
                DrinkId = drinkId,
                ImageUrl = uploadResult.Url,
                PublicId = uploadResult.PublicId,
                CreatedAt = DateTime.Now,
                IsDefault = false
            };

            await _drinkRepository.AddDrinkImageAsync(entity);

            await _drinkRepository.SaveChangesAsync();

            if (isDefault)
            {
                await _drinkRepository.SetDefaultDrinkImageAsync(
                    drinkId,
                    entity.DrinkImageId);

                await _drinkRepository.SaveChangesAsync();
            }
        }

        public async Task SetDefaultDrinkImageAsync(int drinkId, int drinkImageId)
        {
            await _drinkRepository.SetDefaultDrinkImageAsync(drinkId, drinkImageId);

            await _drinkRepository.SaveChangesAsync();
        }

        public async Task DeleteDrinkImageAsync(int drinkImageId)
        {
            var image = await _drinkRepository.GetDrinkImageByIdAsync(drinkImageId);

            if (image == null)
                throw new KeyNotFoundException("Không tìm thấy ảnh.");

            var allImages = (await _drinkRepository.GetDrinkImagesAsync(image.DrinkId))
                .OrderBy(x => x.DrinkImageId)
                .ToList();

            // CASE 1: chỉ còn đúng 1 ảnh và đó là ảnh default
            if (allImages.Count == 1 && image.IsDefault)
            {
                throw new Exception("Không thể xóa ảnh mặc định duy nhất của sản phẩm.");
            }

            // CASE 2: nếu xóa ảnh default và vẫn còn ảnh khác
            if (image.IsDefault)
            {
                var nextDefault = allImages
                    .FirstOrDefault(x => x.DrinkImageId != image.DrinkImageId);

                if (nextDefault != null)
                {
                    await _drinkRepository.SetDefaultDrinkImageAsync(
                        image.DrinkId,
                        nextDefault.DrinkImageId
                    );
                }
            }

            // Xóa file vật lý
            if (!string.IsNullOrWhiteSpace(image.PublicId))
            {
                await _cloudinaryService.DeleteAsync(
                    image.PublicId);
            }

            await _drinkRepository.DeleteDrinkImageAsync(drinkImageId);

            await _drinkRepository.SaveChangesAsync();
        }

        public async Task UpdateDrinkImageAsync(int drinkImageId, IFormFile newImageFile)
        {
            if (newImageFile == null || newImageFile.Length == 0)
            {
                throw new Exception("File không hợp lệ");
            }

            var image = await _drinkRepository.GetDrinkImageByIdAsync(drinkImageId);

            if (image == null)
            {
                throw new KeyNotFoundException("Không tìm thấy ảnh.");
            }

            if (!string.IsNullOrWhiteSpace(image.PublicId))
            {
                await _cloudinaryService.DeleteAsync(image.PublicId);
            }

            var uploadResult = await _cloudinaryService.UploadAsync(newImageFile, ImageFolder.DrinkImages, ImageCategory.Drink);

            image.ImageUrl = uploadResult.Url;
            image.PublicId = uploadResult.PublicId;

            await _drinkRepository.UpdateDrinkImageAsync(image);

            await _drinkRepository.SaveChangesAsync();
        }
    }
}
