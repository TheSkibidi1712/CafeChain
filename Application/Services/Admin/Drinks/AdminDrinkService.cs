using CafeChain.Application.DTOs.Admin.Drinks;
using CafeChain.Application.Interfaces.Admin.Drinks;
using CafeChain.Infrastrusture.Interfaces.Admin.Drinks;
using CafeChain.Models.Drinks;
using Microsoft.AspNetCore.Hosting;

namespace CafeChain.Application.Services.Admin.Drinks
{
    public class AdminDrinkService : IAdminDrinkService
    {
        private readonly IAdminDrinkRepository _drinkRepository;
        private readonly IWebHostEnvironment _env;

        public AdminDrinkService(IAdminDrinkRepository drinkRepository, IWebHostEnvironment env)
        {
            _drinkRepository = drinkRepository;
            _env = env;
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
            // 1. Validate
            if (await _drinkRepository.IsDrinkNameExistsAsync(dto.Name))
                throw new ArgumentException("Tên nước uống đã tồn tại.");

            // 2. Create entity
            var drink = new Drink
            {
                Name = dto.Name,
                CategoryId = dto.CategoryId,
                ProductTypeId = dto.ProductTypeId,
                Description = dto.Description,
                Active = true,
                CreatedAt = DateTime.Now
            };

            // 3. Save drink trước để lấy ID
            int drinkId = await _drinkRepository.CreateDrinkAsync(drink);

            // 4. Handle images (tách riêng → AN TOÀN 100%)
            if (dto.ImageFiles != null && dto.ImageFiles.Any())
            {
                string folder = Path.Combine(_env.WebRootPath, "Images", "DrinkImages");

                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                for (int i = 0; i < dto.ImageFiles.Count; i++)
                {
                    var file = dto.ImageFiles[i];

                    if (file == null || file.Length == 0)
                        continue;

                    string fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
                    string path = Path.Combine(folder, fileName);

                    using (var stream = new FileStream(path, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    var image = new DrinkImage
                    {
                        DrinkId = drinkId,
                        ImageUrl = "/Images/DrinkImages/" + fileName,
                        IsDefault = dto.DefaultImageIndex.HasValue
                                    ? dto.DefaultImageIndex.Value == i
                                    : i == 0
                    };

                    await _drinkRepository.AddDrinkImageAsync(image);
                }
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
        }

        public async Task ToggleDrinkStatusAsync(int id)
        {
            await _drinkRepository.ToggleDrinkStatusAsync(id);
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
                throw new Exception("File không hợp lệ");

            string folder = Path.Combine(_env.WebRootPath, "Images", "DrinkImages");

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            string fileName = Guid.NewGuid() + Path.GetExtension(imageFile.FileName);
            string path = Path.Combine(folder, fileName);

            using (var stream = new FileStream(path, FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }

            var entity = new DrinkImage
            {
                DrinkId = drinkId,
                ImageUrl = "/Images/DrinkImages/" + fileName,
                IsDefault = false
            };

            await _drinkRepository.AddDrinkImageAsync(entity);

            // 🔥 QUAN TRỌNG: Save trước để có ID
            // (repo của mày đã Save trong Add rồi nên OK)

            if (isDefault)
            {
                await _drinkRepository.SetDefaultDrinkImageAsync(drinkId, entity.DrinkImageId);
            }
        }

        public async Task SetDefaultDrinkImageAsync(int drinkId, int drinkImageId)
        {
            await _drinkRepository.SetDefaultDrinkImageAsync(drinkId, drinkImageId);
        }

        public async Task DeleteDrinkImageAsync(int drinkImageId)
        {
            var image = await _drinkRepository.GetDrinkImageByIdAsync(drinkImageId);
            if (image != null)
            {
                // Delete physical file
                var relativePath = image.ImageUrl.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
                var filePath = Path.Combine(_env.WebRootPath, relativePath);
                
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
                
                await _drinkRepository.DeleteDrinkImageAsync(drinkImageId);
            }
        }

        public async Task UpdateDrinkImageAsync(int drinkImageId, IFormFile newImageFile)
        {
            if (newImageFile == null || newImageFile.Length == 0)
                throw new Exception("File không hợp lệ");

            var image = await _drinkRepository.GetDrinkImageByIdAsync(drinkImageId);
            if (image == null)
                throw new KeyNotFoundException("Không tìm thấy ảnh.");

            // 1. Xóa file vật lý cũ
            var oldRelativePath = image.ImageUrl.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
            var oldFilePath = Path.Combine(_env.WebRootPath, oldRelativePath);
            if (System.IO.File.Exists(oldFilePath))
            {
                System.IO.File.Delete(oldFilePath);
            }

            // 2. Lưu file mới
            string folder = Path.Combine(_env.WebRootPath, "Images", "DrinkImages");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            string fileName = Guid.NewGuid() + Path.GetExtension(newImageFile.FileName);
            string newFilePath = Path.Combine(folder, fileName);

            using (var stream = new FileStream(newFilePath, FileMode.Create))
            {
                await newImageFile.CopyToAsync(stream);
            }

            // 3. Cập nhật ImageUrl (entity đang được EF track)
            image.ImageUrl = "/Images/DrinkImages/" + fileName;

            await _drinkRepository.UpdateDrinkImageAsync(image);
        }
    }
}
