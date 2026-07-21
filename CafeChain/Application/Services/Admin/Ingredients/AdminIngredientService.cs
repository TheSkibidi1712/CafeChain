using CafeChain.Application.DTOs.Admin.Ingredients;
using CafeChain.Application.DTOs.Admin.Units;
using CafeChain.Application.Interfaces.Admin.Ingredients;
using CafeChain.Infrastrusture.Interfaces.Admin.Ingredients;
using CafeChain.Models.Inventories.Ingredients;

namespace CafeChain.Application.Services.Admin.Ingredients;

public sealed class AdminIngredientService : IAdminIngredientService
{
    private readonly IAdminIngredientRepository _repository;
    public AdminIngredientService(IAdminIngredientRepository repository) => _repository = repository;

    public async Task<(List<AdminIngredientDTO> Items, int Total)> GetPagedAsync(
        string? search, bool? status, int page, int pageSize)
    {
        var (items, total) = await _repository.GetPagedAsync(search, status, page, pageSize);
        return (items.Select(MapList).ToList(), total);
    }

    public async Task<AdminIngredientUpdateDTO?> GetByIdAsync(int id)
    {
        var item = await _repository.GetByIdAsync(id);
        return item == null ? null : new AdminIngredientUpdateDTO
        {
            IngredientId = item.IngredientId,
            Code = item.Code,
            Name = item.Name,
            BaseUnitId = item.BaseUnitId,
            BaseUnitName = item.BaseUnit?.Name
        };
    }

    public async Task<int> CreateAsync(AdminIngredientCreateDTO dto)
    {
        Normalize(dto);
        await ValidateAsync(dto.Code, dto.Name, dto.BaseUnitId);
        var ingredient = new Ingredient
        {
            Code = dto.Code,
            Name = dto.Name,
            BaseUnitId = dto.BaseUnitId,
            Active = true
        };
        await _repository.CreateAsync(ingredient);
        await _repository.SaveChangesAsync();
        return ingredient.IngredientId;
    }

    public async Task UpdateAsync(AdminIngredientUpdateDTO dto)
    {
        var ingredient = await _repository.GetByIdAsync(dto.IngredientId)
            ?? throw new InvalidOperationException("Không tìm thấy nguyên liệu.");
        Normalize(dto);
        await ValidateAsync(dto.Code, dto.Name, dto.BaseUnitId, dto.IngredientId);

        if (ingredient.BaseUnitId != dto.BaseUnitId
            && await _repository.HasBaseUnitDependenciesAsync(ingredient.IngredientId))
            throw new InvalidOperationException(
                "Không thể đổi đơn vị tồn kho cơ sở vì nguyên liệu đã phát sinh dữ liệu phụ thuộc.");

        ingredient.Code = dto.Code;
        ingredient.Name = dto.Name;
        ingredient.BaseUnitId = dto.BaseUnitId;
        // Active is intentionally not accepted by this command.
        await _repository.UpdateAsync(ingredient);
        await _repository.SaveChangesAsync();
    }

    public async Task ToggleStatusAsync(int id)
    {
        await _repository.ToggleStatus(id);
        await _repository.SaveChangesAsync();
    }

    public async Task<List<UnitDTO>> GetUnitsAsync() =>
        (await _repository.GetActiveUnitsAsync()).Select(x => new UnitDTO
        {
            UnitId = x.UnitId,
            Name = x.Name,
            UnitCode = x.UnitCode,
            Type = x.Type.ToString()
        }).ToList();

    private async Task ValidateAsync(string code, string name, int baseUnitId, int? excludeId = null)
    {
        if (!await _repository.IsActiveUnitAsync(baseUnitId))
            throw new InvalidOperationException("Đơn vị tồn kho cơ sở không tồn tại hoặc đã ngừng hoạt động.");
        if (await _repository.IsCodeExists(code, excludeId))
            throw new InvalidOperationException("Mã nguyên liệu đã tồn tại.");
        if (await _repository.IsNameExists(name, excludeId))
            throw new InvalidOperationException("Tên nguyên liệu đã tồn tại.");
    }

    private static void Normalize(AdminIngredientCreateDTO dto)
    {
        dto.Code = (dto.Code ?? string.Empty).Trim().ToUpperInvariant();
        dto.Name = (dto.Name ?? string.Empty).Trim();
    }

    private static void Normalize(AdminIngredientUpdateDTO dto)
    {
        dto.Code = (dto.Code ?? string.Empty).Trim().ToUpperInvariant();
        dto.Name = (dto.Name ?? string.Empty).Trim();
    }

    private static AdminIngredientDTO MapList(Ingredient item) => new()
    {
        IngredientId = item.IngredientId,
        Code = item.Code,
        Name = item.Name,
        BaseUnitName = item.BaseUnit?.Name,
        Active = item.Active
    };
}
