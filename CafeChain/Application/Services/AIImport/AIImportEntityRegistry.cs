using CafeChain.Application.Constants;
using CafeChain.Models.AIImport;

namespace CafeChain.Application.Services.AIImport;

public sealed record AIImportEntityDescriptor(
    AIImportEntityType EntityType,
    string CreatePermission,
    int DependencyOrder,
    IReadOnlyList<string> BusinessKeyFields);

public sealed class AIImportEntityRegistry
{
    private static readonly IReadOnlyDictionary<AIImportEntityType, AIImportEntityDescriptor> Descriptors =
        new Dictionary<AIImportEntityType, AIImportEntityDescriptor>
        {
            [AIImportEntityType.Category] = new(AIImportEntityType.Category, PermissionConstants.CategoryCreate, 10, ["CategoryCode", "Name"]),
            [AIImportEntityType.Drink] = new(AIImportEntityType.Drink, PermissionConstants.DrinkCreate, 20, ["DrinkCode", "Name"]),
            [AIImportEntityType.Size] = new(AIImportEntityType.Size, PermissionConstants.SizeCreate, 30, ["SizeCode", "Name"]),
            [AIImportEntityType.Ingredient] = new(AIImportEntityType.Ingredient, PermissionConstants.IngredientCreate, 30, ["Code", "Name"]),
            [AIImportEntityType.Supplier] = new(AIImportEntityType.Supplier, PermissionConstants.SupplierCreate, 30, ["TaxCode"])
        };

    public IReadOnlyCollection<AIImportEntityType> SupportedEntities => Descriptors.Keys.ToArray();

    public AIImportEntityDescriptor? Find(AIImportEntityType entityType) =>
        Descriptors.GetValueOrDefault(entityType);

    public AIImportEntityDescriptor Get(AIImportEntityType entityType) =>
        Find(entityType) ?? throw new InvalidOperationException("Loại dữ liệu nằm ngoài phạm vi nhập dữ liệu thông minh.");

    public string BusinessKey(AIImportEntityType entityType, IReadOnlyDictionary<string, string?> values)
    {
        var descriptor = Find(entityType);
        if (descriptor == null) return string.Empty;
        var value = descriptor.BusinessKeyFields
            .Select(values.GetValueOrDefault)
            .FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate));
        return AIImportSchemaRegistry.Key(value);
    }
}

public enum AIImportValidationScopeKind
{
    Full,
    Group,
    Item
}

public sealed record AIImportValidationScope(
    AIImportValidationScopeKind Kind,
    int? GroupId = null,
    int? ItemId = null,
    AIImportEntityType? PreviousEntityType = null,
    string? PreviousBusinessKey = null,
    IReadOnlyCollection<string>? PreviousReferenceTokens = null)
{
    public static AIImportValidationScope FullSession() => new(AIImportValidationScopeKind.Full);
    public static AIImportValidationScope ForGroup(int groupId, AIImportEntityType previousEntityType) =>
        new(AIImportValidationScopeKind.Group, GroupId: groupId, PreviousEntityType: previousEntityType);
    public static AIImportValidationScope ForItem(
        int itemId,
        AIImportEntityType entityType,
        string? previousBusinessKey,
        IReadOnlyCollection<string>? previousReferenceTokens = null) =>
        new(AIImportValidationScopeKind.Item, ItemId: itemId, PreviousEntityType: entityType,
            PreviousBusinessKey: previousBusinessKey, PreviousReferenceTokens: previousReferenceTokens);
}
