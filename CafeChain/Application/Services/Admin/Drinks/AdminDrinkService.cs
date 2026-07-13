using CafeChain.Application.Constants.Cloudinaries;
using CafeChain.Application.DTOs.Admin.Drinks;
using CafeChain.Application.Interfaces.Admin.Drinks;
using CafeChain.Application.Interfaces.Cloudinaries;
using CafeChain.Infrastrusture.Interfaces.Admin.Drinks;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Cloudinaries;
using CafeChain.ViewModels.Admin.Drinks;
using CafeChain.ViewModels.Shared;

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
            return drinks.Select(MapToDto);
        }

        public async Task<AdminDrinkIndexViewModel> GetIndexDataAsync(AdminDrinkFilterDTO filter)
        {
            NormalizeFilter(filter);

            var counts = await _drinkRepository.GetDrinkCountsAsync(filter.Keyword);

            var result = await _drinkRepository.GetPaginatedDrinksAsync(
                filter.Keyword,
                filter.Active,
                filter.Page,
                filter.PageSize);

            var totalPages = CalculateTotalPages(result.TotalCount, filter.PageSize);

            if (filter.Page > totalPages)
            {
                filter.Page = totalPages;

                result = await _drinkRepository.GetPaginatedDrinksAsync(
                    filter.Keyword,
                    filter.Active,
                    filter.Page,
                    filter.PageSize);
            }

            var items = result.Items
                .Select(MapToDto)
                .ToList();

            return new AdminDrinkIndexViewModel
            {
                Filter = filter,
                Drinks = new PaginatedListViewModel<AdminDrinkDTO>(
                    items,
                    result.TotalCount,
                    filter.Page,
                    filter.PageSize),
                TotalCount = counts.TotalCount,
                ActiveCount = counts.ActiveCount,
                InactiveCount = counts.InactiveCount
            };
        }

        public async Task<AdminDrinkDTO> GetDrinkByIdAsync(int id)
        {
            var drink = await _drinkRepository.GetDrinkByIdAsync(id);
            if (drink == null) return null;

            return MapToDto(drink);
        }

        public async Task<int> CreateDrinkAsync(AdminDrinkCreateDTO dto)
        {
            var drinkCode = Normalize(dto.DrinkCode);
            var name = Normalize(dto.Name);
            var description = Normalize(dto.Description);

            ValidateDrinkCore(drinkCode, name, dto.CategoryId, dto.ProductTypeId);

            if (await _drinkRepository.IsDrinkCodeExistsAsync(drinkCode))
            {
                throw new ArgumentException("Mã nước uống đã tồn tại.");
            }

            if (await _drinkRepository.IsDrinkNameExistsAsync(name))
            {
                throw new ArgumentException("Tên nước uống đã tồn tại.");
            }

            var drink = new Drink
            {
                DrinkCode = drinkCode,
                Name = name,
                CategoryId = dto.CategoryId,
                ProductTypeId = dto.ProductTypeId,
                Description = description,
                Active = true,
                CreatedAt = DateTime.Now
            };

            await _drinkRepository.CreateDrinkAsync(drink);

            await _drinkRepository.SaveChangesAsync();

            int drinkId = drink.DrinkId;

            var imageFiles = dto.ImageFiles?
                .Where(file => file != null && file.Length > 0)
                .ToList() ?? new List<IFormFile>();

            if (imageFiles.Any())
            {
                var defaultImageIndex = dto.DefaultImageIndex.HasValue &&
                                        dto.DefaultImageIndex.Value >= 0 &&
                                        dto.DefaultImageIndex.Value < imageFiles.Count
                    ? dto.DefaultImageIndex.Value
                    : 0;

                for (int i = 0; i < imageFiles.Count; i++)
                {
                    var file = imageFiles[i];

                    var uploadResult =
                        await _cloudinaryService.UploadAsync(file, ImageFolder.DrinkImages, ImageCategory.Drink);

                    var image = new DrinkImage
                    {
                        DrinkId = drinkId,
                        ImageUrl = uploadResult.Url,
                        PublicId = uploadResult.PublicId,
                        CreatedAt = DateTime.Now,
                        IsDefault = i == defaultImageIndex
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
                DrinkCode = drink.DrinkCode,
                Name = drink.Name,
                CategoryId = drink.CategoryId ?? 0,
                ProductTypeId = drink.ProductTypeId,
                Description = drink.Description,
                Active = drink.Active
            };
        }

        public async Task UpdateDrinkAsync(AdminDrinkUpdateDTO updateDTO)
        {
            var drinkCode = Normalize(updateDTO.DrinkCode);
            var name = Normalize(updateDTO.Name);
            var description = Normalize(updateDTO.Description);

            ValidateDrinkCore(drinkCode, name, updateDTO.CategoryId, updateDTO.ProductTypeId);

            if (await _drinkRepository.IsDrinkCodeExistsAsync(drinkCode, updateDTO.DrinkId))
            {
                throw new ArgumentException("Mã nước uống đã tồn tại.");
            }

            if (await _drinkRepository.IsDrinkNameExistsAsync(name, updateDTO.DrinkId))
            {
                throw new ArgumentException("Tên nước uống đã tồn tại.");
            }

            var drink = await _drinkRepository.GetDrinkByIdAsync(updateDTO.DrinkId);
            if (drink == null) throw new KeyNotFoundException("Không tìm thấy nước uống.");

            drink.DrinkCode = drinkCode;
            drink.Name = name;
            drink.CategoryId = updateDTO.CategoryId == 0 ? null : updateDTO.CategoryId;
            drink.ProductTypeId = updateDTO.ProductTypeId;
            drink.Description = description;
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

            var drink = await _drinkRepository.GetDrinkByIdAsync(drinkId);

            if (drink == null)
            {
                throw new KeyNotFoundException("Không tìm thấy nước uống.");
            }

            var uploadResult = await _cloudinaryService.UploadAsync(imageFile, ImageFolder.DrinkImages, ImageCategory.Drink);

            var hasDefaultImage = await _drinkRepository.HasDefaultImageAsync(drinkId);
            var shouldSetDefault = isDefault || !hasDefaultImage;

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

            if (shouldSetDefault)
            {
                await _drinkRepository.SetDefaultDrinkImageAsync(
                    drinkId,
                    entity.DrinkImageId);

                await _drinkRepository.SaveChangesAsync();
            }
        }

        public async Task SetDefaultDrinkImageAsync(int drinkId, int drinkImageId)
        {
            var image = await _drinkRepository.GetDrinkImageByIdAsync(drinkImageId);

            if (image == null)
            {
                throw new KeyNotFoundException("Không tìm thấy ảnh.");
            }

            if (image.DrinkId != drinkId)
            {
                throw new ArgumentException("Ảnh không thuộc nước uống này.");
            }

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

            var uploadResult = await _cloudinaryService.UploadAsync(newImageFile, ImageFolder.DrinkImages, ImageCategory.Drink);
            var oldPublicId = image.PublicId;

            image.ImageUrl = uploadResult.Url;
            image.PublicId = uploadResult.PublicId;

            await _drinkRepository.UpdateDrinkImageAsync(image);

            await _drinkRepository.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(oldPublicId))
            {
                await _cloudinaryService.DeleteAsync(oldPublicId);
            }
        }

        private static string Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim();
        }

        private static void NormalizeFilter(AdminDrinkFilterDTO filter)
        {
            filter.Keyword = string.IsNullOrWhiteSpace(filter.Keyword)
                ? null
                : filter.Keyword.Trim();

            filter.Page = filter.Page <= 0
                ? 1
                : filter.Page;

            filter.PageSize = filter.PageSize switch
            {
                25 => 25,
                50 => 50,
                _ => 10
            };
        }

        private static int CalculateTotalPages(int totalCount, int pageSize)
        {
            if (totalCount <= 0)
            {
                return 1;
            }

            return (int)Math.Ceiling(totalCount / (double)pageSize);
        }

        private static AdminDrinkDTO MapToDto(Drink drink)
        {
            return new AdminDrinkDTO
            {
                DrinkId = drink.DrinkId,
                DrinkCode = drink.DrinkCode,
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

        private static void ValidateDrinkCore(
            string drinkCode,
            string name,
            int categoryId,
            int productTypeId)
        {
            if (string.IsNullOrWhiteSpace(drinkCode))
            {
                throw new ArgumentException("Vui lòng nhập mã nước uống.");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Vui lòng nhập tên nước uống.");
            }

            if (categoryId <= 0)
            {
                throw new ArgumentException("Vui lòng chọn danh mục.");
            }

            if (productTypeId <= 0)
            {
                throw new ArgumentException("Vui lòng chọn loại sản phẩm.");
            }
        }
    }
}
