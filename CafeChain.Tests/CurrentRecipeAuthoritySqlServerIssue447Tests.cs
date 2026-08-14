using CafeChain.Application.Constants;
using CafeChain.Application.Services.Admin.Recipes;
using CafeChain.Application.Services.Inventories;
using CafeChain.Application.Services.Systems;
using CafeChain.Data;
using CafeChain.ViewModels.Admin.Recipes;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CafeChain.Tests;

[Trait("Category", "SqlServerIntegration")]
public sealed class CurrentRecipeAuthoritySqlServerIssue447Tests : IAsyncLifetime
{
    private const string Database = "CafeChain_Issue447Tests";
    private static readonly DateTimeOffset Now =
        new(2026, 8, 14, 3, 0, 0, TimeSpan.Zero);
    private static string ConnectionString => SqlServerTestConnection.Create(Database);

    public async Task InitializeAsync()
    {
        try
        {
            await using (var master = new SqlConnection(SqlServerTestConnection.MasterConnectionString()))
            {
                await master.OpenAsync();
                await using var command = master.CreateCommand();
                command.CommandText = $"IF DB_ID(N'{Database}') IS NULL CREATE DATABASE [{Database}];";
                await command.ExecuteNonQueryAsync();
            }

            await using var context = CreateContext();
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"SQL Server integration environment unavailable for issue #447. Database={Database}. {ex.Message}",
                ex);
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ConcurrentPublish_ForSameTarget_AllowsOneCurrentSuccess()
    {
        RecipeCreateVM firstModel;
        RecipeCreateVM secondModel;
        await using (var seed = CreateContext())
        {
            firstModel = await BuildVersionModelAsync(seed, 1);
            secondModel = await BuildVersionModelAsync(seed, 1);
        }

        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var publishes = new[] { firstModel, secondModel }.Select(async model =>
        {
            await using var context = CreateContext();
            var service = CreateService(context);
            await start.Task;
            return await service.UpdateRecipeAsync(1, model);
        }).ToArray();

        start.SetResult();
        var results = await Task.WhenAll(publishes);

        Assert.Single(results.Where(result => result.IsSuccess));
        var conflict = Assert.Single(results.Where(result => !result.IsSuccess));
        Assert.Equal(BomRecipeErrorCodes.PublishConflict, conflict.ErrorCode);
        Assert.Contains("tải lại", conflict.Message, StringComparison.OrdinalIgnoreCase);

        await using var verify = CreateContext();
        var versions = await verify.Recipes.AsNoTracking()
            .Where(recipe => recipe.DrinkId == 1 && recipe.SizeId == 1)
            .ToListAsync();
        Assert.Single(versions.Where(recipe => recipe.Active && recipe.Status == "Active"));
        Assert.Equal(2, versions.Count);
        Assert.Contains(versions, recipe =>
            recipe.RecipeId == 1
            && !recipe.Active
            && recipe.Status == "Archived");
    }

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlServer(ConnectionString)
        .Options);

    private static AdminRecipeService CreateService(AppDbContext context)
    {
        var physical = new PhysicalUnitConversionService(
            context,
            NullLogger<PhysicalUnitConversionService>.Instance);
        var clock = new FixedTimeProvider(Now);
        return new AdminRecipeService(
            context,
            new RecipeOutputNormalizer(context, physical),
            NullLogger<AdminRecipeService>.Instance,
            clock,
            new BusinessDateService(clock),
            new CurrentRecipeResolver(context));
    }

    private static async Task<RecipeCreateVM> BuildVersionModelAsync(
        AppDbContext context,
        int recipeId)
    {
        var source = await context.Recipes.AsNoTracking()
            .Include(recipe => recipe.RecipeDetails)
            .SingleAsync(recipe => recipe.RecipeId == recipeId);
        return new RecipeCreateVM
        {
            RecipeType = "POS",
            DrinkId = source.DrinkId,
            SizeId = source.SizeId,
            Active = true,
            EffectiveDate = new DateTime(2026, 8, 14),
            Details = source.RecipeDetails.Select(detail => new RecipeDetailVM
            {
                ItemCode = detail.IngredientId.HasValue
                    ? $"ING_{detail.IngredientId.Value}"
                    : $"REC_{detail.ChildRecipeId!.Value}",
                Quantity = detail.Quantity,
                UnitId = detail.UnitId,
                YieldPercentage = 100m
            }).ToList()
        };
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
