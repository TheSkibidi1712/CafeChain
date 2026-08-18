using System.Text.Json;
using CafeChain.Application.DTOs.AIImport;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Categories;
using CafeChain.Application.DTOs.Admin.Permissions;
using CafeChain.Application.Interfaces.AIImport;
using CafeChain.Application.Interfaces.Admin.Categories;
using CafeChain.Application.Interfaces.Admin.Drinks;
using CafeChain.Application.Interfaces.Admin.Ingredients;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.Admin.Sizes;
using CafeChain.Application.Interfaces.Admin.Suppliers;
using CafeChain.Application.Options;
using CafeChain.Application.Results;
using CafeChain.Application.Services.AIImport;
using CafeChain.Application.Services.Systems;
using CafeChain.Data;
using CafeChain.Infrastructure.Configurations;
using CafeChain.Infrastrusture.Repositories.Systems;
using CafeChain.Models.AIImport;
using CafeChain.Models.Drinks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CafeChain.Tests;

[Trait("Category", "SqlServerIntegration")]
public sealed class AIImportSqlServerTests : IAsyncLifetime
{
    private readonly string _database = $"CafeChain_AIImport_RuntimeSmoke_{Guid.NewGuid():N}";
    private string ConnectionString => SqlServerTestConnection.Create(_database);

    public async Task InitializeAsync()
    {
        await using var master = new SqlConnection(SqlServerTestConnection.MasterConnectionString());
        await master.OpenAsync();
        await using (var command = master.CreateCommand())
        {
            command.CommandText = $"IF DB_ID(N'{_database}') IS NULL CREATE DATABASE [{_database}];";
            await command.ExecuteNonQueryAsync();
        }
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task Migration_runs_twice_and_creates_all_import_contracts()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        await context.Database.MigrateAsync();
        var tables = await context.Database.SqlQueryRaw<string>(
            "SELECT name AS Value FROM sys.tables WHERE name IN ('ImportSessions','ImportSourceDocuments','ImportGroups','ImportItems','ImportAudits')")
            .ToListAsync();
        Assert.Equal(5, tables.Count);
        var applied = await context.Database.GetAppliedMigrationsAsync();
        Assert.Contains("20260815152712_InitialCreate", applied);
        Assert.Contains("20260816170000_AddPreparedItemTargetStockLevel", applied);
        Assert.Equal(2, applied.Count());
        var traceabilityColumns = await context.Database.SqlQueryRaw<string>(
                "SELECT COLUMN_NAME AS Value FROM INFORMATION_SCHEMA.COLUMNS WHERE "
                + "(TABLE_NAME = 'ImportSessions' AND COLUMN_NAME = 'ExtractionVersion') OR "
                + "(TABLE_NAME = 'ImportGroups' AND COLUMN_NAME = 'LayoutConfidence') OR "
                + "(TABLE_NAME = 'ImportItems' AND COLUMN_NAME IN ('LayoutConfidence','FieldEvidenceJson')) OR "
                + "(TABLE_NAME = 'ImportAudits' AND COLUMN_NAME IN ('ExtractionVersion','OcrProvider','OcrProviderVersion','OcrExtractionVersion','OcrConfidenceSummaryJson'))")
            .ToListAsync();
        Assert.Equal(9, traceabilityColumns.Count);
    }

    [Fact]
    public async Task Existing_deduplication_service_replays_success_and_rejects_key_payload_reuse()
    {
        await using var context = CreateContext();
        var service = new RequestDeduplicationService(new RequestDeduplicationRepository(context));
        var first = await service.BeginScopedAsync("ai-import-key-1", "AIImport.Confirm", 701, new { sessionId = 1, expectedPreviewVersion = 3 }, 1, 0, 801);
        Assert.True(first.CanProcess);
        await service.MarkSuccessAsync(first.Entry!, 1, new { sessionId = 1, status = "COMPLETED", imported = 2 });

        var replay = await service.BeginScopedAsync("ai-import-key-1", "AIImport.Confirm", 701, new { sessionId = 1, expectedPreviewVersion = 3 }, 1, 0, 801);
        Assert.False(replay.CanProcess);
        Assert.Equal("SUCCESS", replay.Status);
        Assert.Contains("COMPLETED", replay.ResponseBody);

        var reused = await service.BeginScopedAsync("ai-import-key-1", "AIImport.Confirm", 701, new { sessionId = 1, expectedPreviewVersion = 4 }, 1, 0, 801);
        Assert.False(reused.CanProcess);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", reused.ErrorCode);
        Assert.Equal(1, await context.RequestDeduplications.CountAsync(x => x.ActionName == "AIImport.Confirm" && x.RequestKey == "ai-import-key-1"));
    }

    [Fact]
    public async Task Concurrent_confirm_claims_only_one_request()
    {
        var id = await SeedReadySessionAsync();
        var claims = await Task.WhenAll(
            ClaimAsync(id, AIImportSessionStatuses.Importing),
            ClaimAsync(id, AIImportSessionStatuses.Importing));

        Assert.Equal(1, claims.Sum());
        await using var verify = CreateContext();
        Assert.Equal(AIImportSessionStatuses.Importing, await verify.ImportSessions.Where(x => x.ImportSessionId == id).Select(x => x.Status).SingleAsync());
    }

    [Fact]
    public async Task Confirm_cancel_race_allows_exactly_one_transition()
    {
        var id = await SeedReadySessionAsync();
        var outcomes = await Task.WhenAll(
            ClaimAsync(id, AIImportSessionStatuses.Importing),
            ClaimAsync(id, AIImportSessionStatuses.Cancelled));

        Assert.Equal(1, outcomes.Sum());
        await using var verify = CreateContext();
        Assert.Contains(await verify.ImportSessions.Where(x => x.ImportSessionId == id).Select(x => x.Status).SingleAsync(),
            new[] { AIImportSessionStatuses.Importing, AIImportSessionStatuses.Cancelled });
    }

    [Fact]
    public async Task Two_business_writers_cannot_create_same_category_key()
    {
        var code = "AI-UNIQUE-" + Guid.NewGuid().ToString("N")[..8];
        async Task<bool> Insert(string name)
        {
            await using var context = CreateContext();
            context.DrinkCategories.Add(new DrinkCategory { CategoryCode = code, Name = name, Active = true });
            try { await context.SaveChangesAsync(); return true; }
            catch (DbUpdateException) { return false; }
        }

        var results = await Task.WhenAll(Insert("AI category one " + code), Insert("AI category two " + code));
        Assert.Single(results.Where(x => x));
        await using var verify = CreateContext();
        Assert.Equal(1, await verify.DrinkCategories.CountAsync(x => x.CategoryCode == code));
    }

    [Fact]
    public async Task Confirm_valid_category_reloads_rowversion_after_claim_and_completes_atomically()
    {
        var code = "AIROWVERSION" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var name = "Danh mục RowVersion " + code;
        var normalized = JsonSerializer.Serialize(new Dictionary<string, string?>
        {
            ["CategoryCode"] = code,
            ["Name"] = name,
            ["Icon"] = "☕",
            ["Active"] = "true"
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        int sessionId;
        await using (var seed = CreateContext())
        {
            var session = new ImportSession
            {
                FileName = "rowversion.xlsx", FileHash = new string('B', 64), FileSize = 100,
                UploadedByStaffId = 991, UploadedByAccountId = 992, StoreId = 0,
                Status = AIImportSessionStatuses.ReadyToPreview, PreviewVersion = 7,
                TotalGroups = 1, TotalRows = 1, ValidRows = 1,
                CreatedAtUtc = DateTime.UtcNow, ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
                Groups = new List<ImportGroup>
                {
                    new()
                    {
                        SheetName = "Category", RegionAddress = "A1:D2", HeaderRow = 1,
                        EntityType = AIImportEntityType.Category, DependencyOrder = 10,
                        Confidence = 1, Status = AIImportItemStatuses.Valid,
                        MappingJson = "{}", SourceHeadersJson = "[]",
                        Items = new List<ImportItem>
                        {
                            new()
                            {
                                SourceRow = 2, RawDataJson = normalized, NormalizedDataJson = normalized,
                                SourceTraceJson = "{}", Status = AIImportItemStatuses.Valid,
                                Action = AIImportActions.Create, ErrorsJson = "[]", WarningsJson = "[]",
                                Confidence = 1
                            }
                        }
                    }
                }
            };
            seed.ImportSessions.Add(session);
            await seed.SaveChangesAsync();
            sessionId = session.ImportSessionId;
        }

        await using (var context = CreateContext())
        {
            var categoryService = new Mock<IAdminCategoryService>();
            categoryService.Setup(x => x.CreateCategoryAsync(It.IsAny<AdminCreateCategoryDto>()))
                .Returns(async (AdminCreateCategoryDto dto) =>
                {
                    var entity = new DrinkCategory
                    {
                        CategoryCode = dto.CategoryCode!, Name = dto.Name, Icon = dto.Icon, Active = dto.Active
                    };
                    context.DrinkCategories.Add(entity);
                    await context.SaveChangesAsync();
                    return new CafeChain.ViewModels.Admin.Categories.AdminCategoryViewModel
                    {
                        CategoryId = entity.CategoryId, CategoryCode = entity.CategoryCode,
                        Name = entity.Name, Icon = entity.Icon, Active = entity.Active
                    };
                });
            var permissions = new Mock<IAdminPermissionService>();
            permissions.Setup(x => x.HasPermissionAsync(992, It.IsAny<string>(), It.IsAny<int?>()))
                .ReturnsAsync((int accountId, string permissionCode, int? storeId) =>
                    ServiceResult<PermissionDecisionDto>.Success(new PermissionDecisionDto
                    {
                        AccountId = accountId, PermissionCode = permissionCode, TargetStoreId = storeId, Allowed = true
                    }));

            var schemas = new AIImportSchemaRegistry();
            var drinkService = Mock.Of<IAdminDrinkService>();
            var sizeService = Mock.Of<IAdminSizeService>();
            var ingredientService = Mock.Of<IAdminIngredientService>();
            var supplierService = Mock.Of<IAdminSupplierService>();
            var options = Options.Create(new AIImportOptions());
            var entityRegistry = new AIImportEntityRegistry();
            var entityCreator = new AIImportEntityCreator(context, categoryService.Object, drinkService,
                sizeService, ingredientService, supplierService);
            var service = new AIImportService(
                context,
                Mock.Of<IAIImportDocumentPipeline>(),
                Mock.Of<IAIImportRegionAnalyzer>(),
                schemas,
                new RequestDeduplicationService(new RequestDeduplicationRepository(context)),
                permissions.Object,
                supplierService,
                options,
                Options.Create(new OllamaOptions { Model = "test" }),
                NullLogger<AIImportService>.Instance,
                entityCreator,
                new AIImportPreviewValidator(
                    new AIImportCandidateValidator(schemas, options),
                    new AIImportResolutionEngine()),
                entityRegistry,
                new AIImportAnalysisCoordinator(),
                new AIImportPreviewMutationCoordinator(),
                new AIImportConfirmCoordinator(entityRegistry),
                new AIImportSessionQuery());

            var result = await service.ConfirmAsync(
                sessionId,
                "rowversion-" + Guid.NewGuid().ToString("N"),
                new AIImportConfirmRequest { ExpectedPreviewVersion = 7 },
                new AdminActorContext { StaffId = 991, AccountId = 992 },
                default);

            Assert.True(result.Success, $"{result.ErrorCode}: {result.Message}");
            Assert.Equal(AIImportSessionStatuses.Completed, result.Data?.Status);
            Assert.Equal(1, result.Data?.Imported);
        }

        await using var verify = CreateContext();
        Assert.Equal(AIImportSessionStatuses.Completed,
            await verify.ImportSessions.Where(x => x.ImportSessionId == sessionId).Select(x => x.Status).SingleAsync());
        Assert.Equal(1, await verify.DrinkCategories.CountAsync(x => x.CategoryCode == code));
    }

    private async Task<int> SeedReadySessionAsync()
    {
        await using var context = CreateContext();
        var session = new ImportSession
        {
            FileName = "race.xlsx", FileHash = new string('A', 64), FileSize = 10,
            UploadedByStaffId = 1, UploadedByAccountId = 1, StoreId = 0,
            Status = AIImportSessionStatuses.ReadyToPreview, PreviewVersion = 1,
            CreatedAtUtc = DateTime.UtcNow, ExpiresAtUtc = DateTime.UtcNow.AddHours(1)
        };
        context.ImportSessions.Add(session); await context.SaveChangesAsync(); return session.ImportSessionId;
    }

    private async Task<int> ClaimAsync(int id, string target)
    {
        await using var context = CreateContext();
        return await context.ImportSessions.Where(x => x.ImportSessionId == id && x.Status == AIImportSessionStatuses.ReadyToPreview && x.PreviewVersion == 1)
            .ExecuteUpdateAsync(x => x.SetProperty(s => s.Status, target));
    }

    private AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options);
}
