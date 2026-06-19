using CafeChain.Application.DTOs.Admin.DrinkToppings;
using CafeChain.Application.Interfaces.Admin.DrinkToppings;
using CafeChain.Infrastrusture.Interfaces.Admin.DrinkToppings;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Drink;
using CafeChain.ViewModels.Admin.DrinkToppings;
namespace CafeChain.Application.Services.Admin.DrinkToppings
{
    public class AdminDrinkToppingService : IAdminDrinkToppingService
    {
        private readonly IAdminDrinkToppingRepository _repo;

        public AdminDrinkToppingService(IAdminDrinkToppingRepository repo)
        {
            _repo = repo;
        }

        // =============================
        // GET DRINK LIST FOR TOPPING
        // =============================
        public async Task<IEnumerable<DrinkToppingItemVM>> GetDrinksForToppingAsync(int toppingId)
        {
            var drinks = await _repo.GetActiveDrinksAsync();
            var mappings = await _repo.GetByToppingIdAsync(toppingId);

            return drinks.Select(d =>
            {
                var dt = mappings.FirstOrDefault(x => x.DrinkId == d.DrinkId);

                return new DrinkToppingItemVM
                {
                    DrinkId = d.DrinkId,
                    Name = d.Name,
                    ImageUrl = d.DrinkImages.FirstOrDefault(x => x.IsDefault)?.ImageUrl,
                    CategoryName = d.Category?.Name,
                    ProductTypeName = d.ProductType?.Name,

                    IsAssigned = dt != null,
                    DrinkToppingId = dt?.DrinkToppingId,
                    Active = dt?.Active
                };
            });
        }

        // =============================
        // ASSIGN
        // =============================
        public async Task AssignAsync(DrinkToppingDto dto)
        {
            Validate(dto);

            await ValidateDrinkCanUseToppingAsync(dto.DrinkId, dto.ToppingId);

            var existing = (await _repo.GetByToppingIdAsync(dto.ToppingId)).FirstOrDefault(x => x.DrinkId == dto.DrinkId);

            if (existing != null)
            {
                throw new Exception("Drink đã có topping này");
            }

            await _repo.AddAsync(new DrinkTopping
            {
                DrinkId = dto.DrinkId,
                ToppingId = dto.ToppingId
            });

            await _repo.SaveChangesAsync();
        }

        // =============================
        // TOGGLE ACTIVE
        // =============================
        public async Task ToggleAsync(int id)
        {
            var entity = await _repo.GetByIdAsync(id);

            if (entity == null)
                throw new Exception("Không tìm thấy dữ liệu");

            entity.Active = !entity.Active;

            await _repo.UpdateAsync(entity);
            await _repo.SaveChangesAsync();
        }

        // =============================
        // VALIDATION
        // =============================
        private void Validate(DrinkToppingDto dto)
        {
            if (dto == null)
                throw new Exception("Dữ liệu không hợp lệ");

            if (dto.DrinkId <= 0 || dto.ToppingId <= 0)
                throw new Exception("Dữ liệu không hợp lệ");
        }


        // =============================
        // Private Helpers
        // =============================
        private async Task ValidateDrinkCanUseToppingAsync(int drinkId, int toppingId)
        {
            var drink = await _repo.GetDrinkByIdAsync(drinkId);

            if (drink == null)
            {
                throw new Exception("Không tìm thấy drink");
            }

            var topping = await _repo.GetToppingByIdAsync(toppingId);

            if (topping == null)
            {
                throw new Exception("Không tìm thấy topping");
            }

            if (!drink.Active)
            {
                throw new Exception("Drink đã ngừng hoạt động");
            }
            
            if (!topping.Active)
            {
                throw new Exception("Topping đã ngừng hoạt động");
            }

            if (drink.ProductTypeId != (int)ProductTypeEnum.Handcrafted)
            {
                throw new Exception(
                    $"Drink '{drink.Name}' thuộc loại Retail nên không được gán topping");
            }
        }
    }
}
