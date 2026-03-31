using CafeChain.Application.DTOs.Admin.DrinkSizes;
using CafeChain.Application.Interfaces.Admin.DrinkSizes;
using CafeChain.Models.Drinks;
using CafeChain.ViewModels.Admin.DrinkSizes;
using CafeChain.Infrastrusture.Interfaces.Admin.DrinkSizes;
using CafeChain.Infrastrusture.Interfaces.Admin.Sizes;
namespace CafeChain.Application.Services.Admin.DrinkSizes
{
    public class AdminDrinkSizeService : IAdminDrinkSizeService
    {
        private readonly IAdminSizeRepository _drinkRepo;
        private readonly IAdminDrinkSizeRepository _drinkSizeRepo;

        public AdminDrinkSizeService(IAdminSizeRepository drinkRepo, IAdminDrinkSizeRepository drinkSizeRepo)
        {
            _drinkRepo = drinkRepo;
            _drinkSizeRepo = drinkSizeRepo;
        }

        public async Task<IEnumerable<DrinkItemVM>> GetDrinksForSizeAsync(int sizeId)
        {
            var drinks = await _drinkRepo.GetActiveDrinksAsync(); // CHỈ ACTIVE

            var drinkSizes = await _drinkSizeRepo.GetBySizeIdAsync(sizeId);

            return drinks.Select(d =>
            {
                var ds = drinkSizes.FirstOrDefault(x => x.DrinkId == d.DrinkId);

                return new DrinkItemVM
                {
                    DrinkId = d.DrinkId,
                    Name = d.Name,
                    Description = d.Description,
                    CategoryName = d.Category?.Name,
                    ProductTypeName = d.ProductType?.Name,
                    ImageUrl = d.DrinkImages.FirstOrDefault(x => x.IsDefault)?.ImageUrl,

                    IsAssigned = ds != null,
                    DrinkSizeId = ds?.DrinkSizeId,
                    Price = ds?.Price,
                    Active = ds?.Active
                };
            });
        }

        public async Task AssignDrinkAsync(DrinkSizeDto dto)
        {
            Validate(dto);

            // check size tồn tại
            var sizeExists = await _drinkRepo.GetAllAsync();
            if (!sizeExists.Any(x => x.SizeId == dto.SizeId))
                throw new Exception("Size không tồn tại");

            // check drink tồn tại
            var drinks = await _drinkRepo.GetActiveDrinksAsync();
            if (!drinks.Any(x => x.DrinkId == dto.DrinkId))
                throw new Exception("Drink không tồn tại");

            // check duplicate
            var existing = (await _drinkSizeRepo.GetBySizeIdAsync(dto.SizeId))
                .FirstOrDefault(x => x.DrinkId == dto.DrinkId);

            if (existing != null)
                throw new Exception("Drink đã được gán size này");

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
            var ds = await _drinkSizeRepo.GetByIdAsync(id);
            ds.Active = !ds.Active;

            await _drinkSizeRepo.UpdateAsync(ds);
            await _drinkSizeRepo.SaveChangesAsync();
        }

        public async Task UpdatePriceAsync(DrinkSizeDto dto)
        {
            var ds = await _drinkSizeRepo.GetByIdAsync(dto.DrinkSizeId);

            if (ds == null)
                throw new Exception("Không tìm thấy dữ liệu");

            if (dto.Price <= 0)
                throw new Exception("Giá không hợp lệ");

            ds.Price = dto.Price;

            await _drinkSizeRepo.UpdateAsync(ds);
            await _drinkSizeRepo.SaveChangesAsync();
        }

        private void Validate(DrinkSizeDto dto)
        {
            if (dto.Price <= 0)
                throw new Exception("Giá không hợp lệ");

        }
    }
}
