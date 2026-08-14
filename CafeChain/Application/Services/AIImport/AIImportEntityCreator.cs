using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Categories;
using CafeChain.Application.DTOs.Admin.Drinks;
using CafeChain.Application.DTOs.Admin.Ingredients;
using CafeChain.Application.DTOs.Admin.Sizes;
using CafeChain.Application.DTOs.Admin.Suppliers;
using CafeChain.Application.Interfaces.Admin.Categories;
using CafeChain.Application.Interfaces.Admin.Drinks;
using CafeChain.Application.Interfaces.Admin.Ingredients;
using CafeChain.Application.Interfaces.Admin.Sizes;
using CafeChain.Application.Interfaces.Admin.Suppliers;
using CafeChain.Data;
using CafeChain.Models.AIImport;
using CafeChain.Models.Enums.Drink;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.AIImport;

public sealed class AIImportEntityCreator(
    AppDbContext db,
    IAdminCategoryService categories,
    IAdminDrinkService drinks,
    IAdminSizeService sizes,
    IAdminIngredientService ingredients,
    IAdminSupplierService suppliers)
{
    public async Task<int> CreateAsync(
        AIImportEntityType entity,
        Dictionary<string, string?> values,
        ImportItem item,
        AdminActorContext actor,
        CancellationToken cancellationToken)
    {
        switch (entity)
        {
            case AIImportEntityType.Category:
                return (await categories.CreateCategoryAsync(new AdminCreateCategoryDto
                {
                    CategoryCode = values.GetValueOrDefault("CategoryCode"),
                    Name = values.GetValueOrDefault("Name")!,
                    Icon = values.GetValueOrDefault("Icon"),
                    Active = !bool.TryParse(values.GetValueOrDefault("Active"), out var active) || active
                })).CategoryId;
            case AIImportEntityType.Drink:
            {
                var categoryCode = values.GetValueOrDefault("Category");
                var productTypeCode = values.GetValueOrDefault("ProductType");
                var category = await db.DrinkCategories.SingleAsync(value => value.Active
                    && value.CategoryCode == categoryCode, cancellationToken);
                var type = await db.ProductTypes.AsNoTracking().SingleAsync(value => value.Active
                    && value.Code == productTypeCode, cancellationToken);
                return await drinks.CreateDrinkAsync(new AdminDrinkCreateDTO
                {
                    DrinkCode = values.GetValueOrDefault("DrinkCode")!,
                    Name = values.GetValueOrDefault("Name")!,
                    Description = values.GetValueOrDefault("Description") ?? "",
                    CategoryId = category.CategoryId,
                    ProductTypeId = type.ProductTypeId,
                    ImageFiles = []
                });
            }
            case AIImportEntityType.Size:
            {
                var dto = new SizeDto
                {
                    SizeCode = values.GetValueOrDefault("SizeCode")!,
                    Name = values.GetValueOrDefault("Name")!,
                    Description = values.GetValueOrDefault("Description") ?? "",
                    SizeType = Enum.Parse<SizeTypeEnum>(values.GetValueOrDefault("SizeType")!, true)
                };
                var created = await sizes.CreateSizeAsync(dto);
                if (!created.Success) throw new InvalidOperationException(created.Error);
                return await db.Sizes.Where(value => value.SizeCode == dto.SizeCode)
                    .Select(value => value.SizeId).SingleAsync(cancellationToken);
            }
            case AIImportEntityType.Ingredient:
            {
                var unitValue = values.GetValueOrDefault("BaseUnit");
                var unit = await db.Units.AsNoTracking().SingleAsync(value => value.Active
                    && value.UnitCode == unitValue, cancellationToken);
                return await ingredients.CreateAsync(new AdminIngredientCreateDTO
                {
                    Code = values.GetValueOrDefault("Code")!,
                    Name = values.GetValueOrDefault("Name")!,
                    BaseUnitId = unit.UnitId
                });
            }
            case AIImportEntityType.Supplier:
            {
                var dto = SupplierDto(values);
                dto.DuplicateWarningId = item.SupplierDuplicateWarningId;
                dto.DuplicateOverrideReason = item.DuplicateOverrideReason;
                return await suppliers.CreateAsync(dto, actor.StaffId);
            }
            default:
                throw new InvalidOperationException("Entity ngoài phạm vi AI Smart Import MVP.");
        }
    }

    public static AdminSupplierCreateDTO SupplierDto(IReadOnlyDictionary<string, string?> values) => new()
    {
        Name = values.GetValueOrDefault("Name") ?? "",
        TaxCode = values.GetValueOrDefault("TaxCode"),
        Address = values.GetValueOrDefault("Address"),
        Note = values.GetValueOrDefault("Note"),
        PrimaryPhone = values.GetValueOrDefault("PrimaryPhone") ?? "",
        PrimaryContactName = values.GetValueOrDefault("PrimaryContactName") ?? "",
        PrimaryContactPhone = values.GetValueOrDefault("PrimaryContactPhone"),
        PrimaryContactEmail = values.GetValueOrDefault("PrimaryContactEmail"),
        PrimaryContactPosition = values.GetValueOrDefault("PrimaryContactPosition")
    };
}
