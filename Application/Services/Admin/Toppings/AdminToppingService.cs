using CafeChain.Application.DTOs.Admin.Toppings;
using CafeChain.Application.Interfaces.Admin.Toppings;
using CafeChain.Infrastrusture.Interfaces.Admin.Toppings;
using CafeChain.Models.Drinks;
namespace CafeChain.Application.Services.Admin.Toppings
{
    public class AdminToppingService : IAdminToppingService
    {
        private readonly IAdminToppingRepository _repo;

        public AdminToppingService(IAdminToppingRepository repo)
        {
            _repo = repo;
        }

        // ================= GET ALL =================
        public async Task<IEnumerable<ToppingDto>> GetAllAsync()
        {
            var data = await _repo.GetAllAsync();

            return data.Select(x => new ToppingDto
            {
                ToppingId = x.ToppingId,
                Name = x.Name,
                Price = x.Price,
                ImageUrl = x.ImageUrl,
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
                Name = x.Name,
                Price = x.Price,
                ImageUrl = x.ImageUrl,
                Active = x.Active
            });
        }

        // ================= GET BY ID =================
        public async Task<ToppingDto?> GetByIdAsync(int id)
        {
            var entity = await _repo.GetByIdAsync(id);

            if (entity == null) return null;

            return new ToppingDto
            {
                ToppingId = entity.ToppingId,
                Name = entity.Name,
                Price = entity.Price,
                ImageUrl = entity.ImageUrl,
                Active = entity.Active
            };
        }

        // ================= CREATE =================
        public async Task CreateAsync(ToppingDto dto)
        {
            Validate(dto);

            // 🔥 Check duplicate
            if (await _repo.ExistsByNameAsync(dto.Name))
                throw new Exception("Tên topping đã tồn tại");

            var entity = new Topping
            {
                Name = dto.Name.Trim(),
                Price = dto.Price,
                ImageUrl = dto.ImageUrl,
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

            // 🔥 Check duplicate (exclude chính nó)
            if (await _repo.ExistsByNameAsync(dto.Name, dto.ToppingId))
                throw new Exception("Tên topping đã tồn tại");

            entity.Name = dto.Name.Trim();
            entity.Price = dto.Price;
            entity.ImageUrl = dto.ImageUrl;

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

            if (string.IsNullOrWhiteSpace(dto.Name))
                throw new Exception("Tên topping không được để trống");

            if (dto.Name.Length > 100)
                throw new Exception("Tên topping tối đa 100 ký tự");

            if (dto.Price <= 0) // 🔥 FIX QUAN TRỌNG
                throw new Exception("Giá phải lớn hơn 0");
        }
    }
}
