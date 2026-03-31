using CafeChain.Application.DTOs.Admin.DrinkToppings;
using CafeChain.Application.Interfaces.Admin.DrinkToppings;
using CafeChain.Infrastrusture.Interfaces.Admin.DrinkToppings;
using CafeChain.Models.Drinks;
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

            var existing = (await _repo.GetByToppingIdAsync(dto.ToppingId))
                .FirstOrDefault(x => x.DrinkId == dto.DrinkId);

            if (existing != null)
                throw new Exception("Drink đã có topping này");

            await _repo.AddAsync(new DrinkTopping
            {
                DrinkId = dto.DrinkId,
                ToppingId = dto.ToppingId,
                Active = true
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
    }
}
