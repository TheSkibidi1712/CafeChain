using CafeChain.Application.DTOs.Admin.DrinkSizes;
using CafeChain.Application.Interfaces.Admin.DrinkSizes;
using CafeChain.Infrastrusture.Interfaces.Admin.DrinkSizes;
using CafeChain.Infrastrusture.Interfaces.Admin.Sizes;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Drink;
using CafeChain.ViewModels.Admin.DrinkSizes;

namespace CafeChain.Application.Services.Admin.DrinkSizes
{
    public class AdminDrinkSizeService : IAdminDrinkSizeService
    {
        private readonly IAdminSizeRepository _sizeRepo;
        private readonly IAdminDrinkSizeRepository _drinkSizeRepo;

        public AdminDrinkSizeService(
            IAdminSizeRepository sizeRepo,
            IAdminDrinkSizeRepository drinkSizeRepo)
        {
            _sizeRepo = sizeRepo;
            _drinkSizeRepo = drinkSizeRepo;
        }

        public async Task<IEnumerable<DrinkItemVM>> GetDrinksForSizeAsync(int sizeId)
        {
            var size = await _sizeRepo.GetByIdAsync(sizeId);

            if (size == null)
            {
                throw new Exception("Size không tồn tại");
            }

            var drinks = await _sizeRepo.GetActiveDrinksAsync();
            var drinkSizes = (await _drinkSizeRepo.GetBySizeIdAsync(sizeId)).ToList();

            return drinks.Select(d =>
            {
                var drinkSize = drinkSizes.FirstOrDefault(x => x.DrinkId == d.DrinkId);
                var assignmentValidation = ValidateProductTypeForSize(d.ProductTypeId, size);

                return new DrinkItemVM
                {
                    DrinkId = d.DrinkId,
                    Name = d.Name,
                    Description = d.Description,
                    CategoryName = d.Category?.Name,
                    ProductTypeId = d.ProductTypeId,
                    ProductTypeName = d.ProductType?.Name,
                    ImageUrl = d.DrinkImages.FirstOrDefault(x => x.IsDefault)?.ImageUrl,
                    CanAssign = assignmentValidation.IsValid,
                    AssignmentBlockReason = assignmentValidation.Error,
                    IsAssigned = drinkSize != null,
                    DrinkSizeId = drinkSize?.DrinkSizeId,
                    Price = drinkSize?.Price,
                    Active = drinkSize?.Active
                };
            });
        }

        public async Task AssignDrinkAsync(DrinkSizeDto dto)
        {
            ValidatePrice(dto.Price);

            var size = await _sizeRepo.GetByIdAsync(dto.SizeId);

            if (size == null)
            {
                throw new Exception("Size không tồn tại");
            }

            if (!size.Active)
            {
                throw new Exception("Size đang ngừng hoạt động");
            }

            var drink = await _sizeRepo.GetActiveDrinkByIdAsync(dto.DrinkId);

            if (drink == null)
            {
                throw new Exception("Drink không tồn tại hoặc đang ngừng hoạt động");
            }

            EnsureProductTypeAllowed(drink.ProductTypeId, size);

            var existing = await _drinkSizeRepo.GetByDrinkAndSizeAsync(
                dto.DrinkId,
                dto.SizeId);

            if (existing != null)
            {
                throw new Exception("Drink đã được gán size này");
            }

            await _drinkSizeRepo.AddAsync(new DrinkSize
            {
                DrinkId = dto.DrinkId,
                SizeId = dto.SizeId,
                Price = dto.Price,
                Active = true
            });

            await _drinkSizeRepo.SaveChangesAsync();
        }

        public async Task ToggleDrinkSizeAsync(int id)
        {
            var drinkSize = await _drinkSizeRepo.GetByIdAsync(id);

            if (drinkSize == null)
            {
                throw new Exception("Không tìm thấy dữ liệu");
            }

            if (!drinkSize.Active)
            {
                await EnsureCanActivateAsync(drinkSize);
            }

            drinkSize.Active = !drinkSize.Active;

            await _drinkSizeRepo.UpdateAsync(drinkSize);
            await _drinkSizeRepo.SaveChangesAsync();
        }

        public async Task UpdatePriceAsync(DrinkSizeDto dto)
        {
            var drinkSize = await _drinkSizeRepo.GetByIdAsync(dto.DrinkSizeId);

            if (drinkSize == null)
            {
                throw new Exception("Không tìm thấy dữ liệu");
            }

            ValidatePrice(dto.Price);

            drinkSize.Price = dto.Price;

            await _drinkSizeRepo.UpdateAsync(drinkSize);
            await _drinkSizeRepo.SaveChangesAsync();
        }

        private async Task EnsureCanActivateAsync(DrinkSize drinkSize)
        {
            var size = await _sizeRepo.GetByIdAsync(drinkSize.SizeId);

            if (size == null || !size.Active)
            {
                throw new Exception("Size không tồn tại hoặc đang ngừng hoạt động");
            }

            var drink = await _sizeRepo.GetActiveDrinkByIdAsync(drinkSize.DrinkId);

            if (drink == null)
            {
                throw new Exception("Drink không tồn tại hoặc đang ngừng hoạt động");
            }

            EnsureProductTypeAllowed(drink.ProductTypeId, size);
        }

        private static void EnsureProductTypeAllowed(int productTypeId, Size size)
        {
            var validation = ValidateProductTypeForSize(productTypeId, size);

            if (!validation.IsValid)
            {
                throw new Exception(validation.Error);
            }
        }

        private static (bool IsValid, string? Error) ValidateProductTypeForSize(
            int productTypeId,
            Size size)
        {
            return size.SizeType switch
            {
                SizeTypeEnum.Cup when productTypeId != (int)ProductTypeEnum.Handcrafted =>
                    (false, "Size kiểu ly chỉ được gán cho đồ uống pha chế"),

                SizeTypeEnum.Volume when productTypeId != (int)ProductTypeEnum.Retail =>
                    (false, "Size dung tích chỉ được gán cho sản phẩm bán sẵn"),

                SizeTypeEnum.Cup or SizeTypeEnum.Volume =>
                    (true, null),

                _ =>
                    (false, "Loại size không hợp lệ")
            };
        }

        private static void ValidatePrice(decimal price)
        {
            if (price <= 0)
            {
                throw new Exception("Giá không hợp lệ");
            }
        }
    }
}
