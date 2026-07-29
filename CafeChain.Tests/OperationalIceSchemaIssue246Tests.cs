// using CafeChain.Application.Constants;
// using CafeChain.Models.Enums.Inventory;
// using CafeChain.Models.Inventories.Ice;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.EntityFrameworkCore.Metadata;
// using Xunit;

// namespace CafeChain.Tests;

// public sealed class OperationalIceSchemaIssue246Tests : IntegrationTestBase
// {
//     [Fact]
//     public void Model_ContainsAllOperationalIceAggregateTables()
//     {
//         using var context = CreateDbContext();
//         var tables = context.Model.GetEntityTypes()
//             .Select(entity => entity.GetTableName())
//             .Where(table => table != null)
//             .ToHashSet(StringComparer.Ordinal);

//         Assert.Contains("OperationalShifts", tables);
//         Assert.Contains("OperationalShiftWorkShifts", tables);
//         Assert.Contains("IcePolicies", tables);
//         Assert.Contains("IceAllocations", tables);
//         Assert.Contains("IceSupplementalIssues", tables);
//         Assert.Contains("IceCarryOvers", tables);
//         Assert.Contains("IceInventoryPostings", tables);
//     }

//     [Fact]
//     public void Model_HasRequiredIdempotencyAndConcurrencyGuards()
//     {
//         using var context = CreateDbContext();

//         AssertUniqueIndex<IceAllocation>(context, nameof(IceAllocation.PublicId));
//         AssertUniqueIndex<IceAllocation>(context, nameof(IceAllocation.ReservationReference));
//         AssertUniqueIndex<IceAllocation>(context, nameof(IceAllocation.OperationalShiftId), nameof(IceAllocation.IngredientId));
//         AssertUniqueIndex<OperationalShiftWorkShift>(context, nameof(OperationalShiftWorkShift.WorkShiftId));
//         AssertUniqueIndex<IceInventoryPosting>(context, nameof(IceInventoryPosting.IdempotencyKey));
//         AssertUniqueIndex<IceInventoryPosting>(context, nameof(IceInventoryPosting.IceAllocationId), nameof(IceInventoryPosting.Revision), nameof(IceInventoryPosting.PostingType));

//         AssertRowVersion<OperationalShift>(context);
//         AssertRowVersion<IcePolicy>(context);
//         AssertRowVersion<IceAllocation>(context);
//         AssertRowVersion<IceSupplementalIssue>(context);
//         AssertRowVersion<IceCarryOver>(context);
//     }

//     [Fact]
//     public async Task PermissionSeeds_GrantLeastPrivilegeByRole()
//     {
//         using var context = CreateDbContext();

//         var permissions = await context.Permissions
//             .Where(permission => permission.PermissionId >= 200 && permission.PermissionId <= 203)
//             .OrderBy(permission => permission.PermissionId)
//             .Select(permission => permission.Code)
//             .ToListAsync();

//         Assert.Equal(
//             [
//                 OperationalIcePermissions.View,
//                 OperationalIcePermissions.Manage,
//                 OperationalIcePermissions.Approve,
//                 OperationalIcePermissions.Policy
//             ],
//             permissions);

//         var cashierPermissionIds = await context.RolePermissions
//             .Where(link => link.RoleId == 4 && link.PermissionId >= 200)
//             .Select(link => link.PermissionId)
//             .ToListAsync();
//         Assert.Equal([200], cashierPermissionIds);

//         var shiftLeadPermissionIds = await context.RolePermissions
//             .Where(link => link.RoleId == 8 && link.PermissionId >= 200)
//             .OrderBy(link => link.PermissionId)
//             .Select(link => link.PermissionId)
//             .ToListAsync();
//         Assert.Equal([200, 201], shiftLeadPermissionIds);

//         var storeManagerPermissionIds = await context.RolePermissions
//             .Where(link => link.RoleId == 3 && link.PermissionId >= 200)
//             .OrderBy(link => link.PermissionId)
//             .Select(link => link.PermissionId)
//             .ToListAsync();
//         Assert.Equal([200, 201, 202, 203], storeManagerPermissionIds);
//     }

//     [Fact]
//     public void InventoryTransactionType_ReservesStableValueForIceVariance()
//     {
//         Assert.Equal(17, (int)InventoryTransactionTypeEnum.ICE_VARIANCE_OUT);
//     }

//     private static void AssertUniqueIndex<TEntity>(DbContext context, params string[] propertyNames)
//     {
//         var entityType = context.Model.FindEntityType(typeof(TEntity));
//         Assert.NotNull(entityType);
//         var index = entityType!.GetIndexes().SingleOrDefault(candidate =>
//             candidate.Properties.Select(property => property.Name).SequenceEqual(propertyNames));
//         Assert.NotNull(index);
//         Assert.True(index!.IsUnique);
//     }

//     private static void AssertRowVersion<TEntity>(DbContext context)
//     {
//         var entityType = context.Model.FindEntityType(typeof(TEntity));
//         Assert.NotNull(entityType);
//         var rowVersion = entityType!.FindProperty("RowVersion");
//         Assert.NotNull(rowVersion);
//         Assert.Equal(ValueGenerated.OnAddOrUpdate, rowVersion!.ValueGenerated);
//     }
// }
