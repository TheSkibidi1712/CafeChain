using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Permissions;
using CafeChain.Application.Interfaces.AIImport;
using CafeChain.Application.Interfaces.Admin.Categories;
using CafeChain.Application.Interfaces.Admin.Drinks;
using CafeChain.Application.Interfaces.Admin.Ingredients;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.Admin.Sizes;
using CafeChain.Application.Interfaces.Admin.Suppliers;
using CafeChain.Application.Interfaces.Systems;
using CafeChain.Application.Options;
using CafeChain.Application.Results;
using CafeChain.Application.Services.AIImport;
using CafeChain.Data;
using CafeChain.Infrastructure.Configurations;
using CafeChain.Models.AIImport;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CafeChain.Tests;

public sealed class AIImportSessionPaginationTests : IntegrationTestBase
{
    private static readonly AdminActorContext Actor = new() { AccountId = 992, StaffId = 991 };

    [Fact]
    public async Task Initial_page_uses_first_group_rows_while_summary_remains_session_wide()
    {
        await using var context = CreateDbContext();
        var session = new ImportSession
        {
            FileName = "multi-sheet.xlsx",
            FileHash = new string('A', 64),
            FileSize = 1_024,
            SourceFormat = AIImportSourceFormats.Xlsx,
            UploadedByStaffId = Actor.StaffId,
            UploadedByAccountId = Actor.AccountId,
            Status = AIImportSessionStatuses.ReadyToPreview,
            PreviewVersion = 1,
            TotalGroups = 2,
            TotalRows = 92,
            ValidRows = 92,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
            Groups =
            [
                Group("Sheet 1", dependencyOrder: 10, rowCount: 14),
                Group("Sheet 2", dependencyOrder: 20, rowCount: 78)
            ]
        };
        context.ImportSessions.Add(session);
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetSessionAsync(
            session.ImportSessionId,
            groupId: null,
            status: null,
            page: 1,
            pageSize: 10,
            Actor,
            CancellationToken.None);

        Assert.True(result.Success, $"{result.ErrorCode}: {result.Message}");
        Assert.NotNull(result.Data);
        Assert.Equal(92, result.Data.Summary.TotalRows);
        Assert.Equal(14, result.Data.Page.TotalItems);
        Assert.Equal(2, result.Data.Page.TotalPages);
        Assert.Equal(10, result.Data.Groups[0].Items.Count);
        Assert.Empty(result.Data.Groups[1].Items);
    }

    [Fact]
    public async Task Requested_group_controls_page_count_and_rows()
    {
        await using var context = CreateDbContext();
        var session = Session(
            Group("Sheet 1", dependencyOrder: 10, rowCount: 14),
            Group("Sheet 2", dependencyOrder: 20, rowCount: 78));
        context.ImportSessions.Add(session);
        await context.SaveChangesAsync();
        var secondGroupId = session.Groups.Single(group => group.DependencyOrder == 20).ImportGroupId;

        var result = await CreateService(context).GetSessionAsync(
            session.ImportSessionId,
            secondGroupId,
            status: null,
            page: 1,
            pageSize: 50,
            Actor,
            CancellationToken.None);

        Assert.True(result.Success, $"{result.ErrorCode}: {result.Message}");
        Assert.NotNull(result.Data);
        Assert.Equal(92, result.Data.Summary.TotalRows);
        Assert.Equal(78, result.Data.Page.TotalItems);
        Assert.Equal(2, result.Data.Page.TotalPages);
        Assert.Empty(result.Data.Groups[0].Items);
        Assert.Equal(50, result.Data.Groups[1].Items.Count);
    }

    [Fact]
    public async Task Session_without_groups_returns_an_empty_page()
    {
        await using var context = CreateDbContext();
        var session = Session();
        session.TotalGroups = 0;
        session.TotalRows = 0;
        session.ValidRows = 0;
        context.ImportSessions.Add(session);
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetSessionAsync(
            session.ImportSessionId,
            groupId: null,
            status: null,
            page: 1,
            pageSize: 50,
            Actor,
            CancellationToken.None);

        Assert.True(result.Success, $"{result.ErrorCode}: {result.Message}");
        Assert.NotNull(result.Data);
        Assert.Equal(0, result.Data.Page.TotalItems);
        Assert.Equal(1, result.Data.Page.TotalPages);
        Assert.Empty(result.Data.Groups);
    }

    private static ImportSession Session(params ImportGroup[] groups) => new()
    {
        FileName = "multi-sheet.xlsx",
        FileHash = new string('A', 64),
        FileSize = 1_024,
        SourceFormat = AIImportSourceFormats.Xlsx,
        UploadedByStaffId = Actor.StaffId,
        UploadedByAccountId = Actor.AccountId,
        Status = AIImportSessionStatuses.ReadyToPreview,
        PreviewVersion = 1,
        TotalGroups = 2,
        TotalRows = 92,
        ValidRows = 92,
        CreatedAtUtc = DateTime.UtcNow,
        ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
        Groups = groups.ToList()
    };

    private static ImportGroup Group(string sheetName, int dependencyOrder, int rowCount) => new()
    {
        SheetName = sheetName,
        RegionAddress = $"A1:B{rowCount + 1}",
        SourceLabel = sheetName,
        HeaderRow = 1,
        EntityType = AIImportEntityType.Category,
        DependencyOrder = dependencyOrder,
        Confidence = 1,
        Status = AIImportItemStatuses.Valid,
        MappingJson = "{}",
        SourceHeadersJson = "[]",
        Items = Enumerable.Range(1, rowCount).Select(index => new ImportItem
        {
            SourceRow = index + 1,
            Status = AIImportItemStatuses.Valid,
            Action = AIImportActions.Create,
            Confidence = 1
        }).ToList()
    };

    private static AIImportService CreateService(AppDbContext context)
    {
        var permissions = new Mock<IAdminPermissionService>();
        permissions.Setup(service => service.HasPermissionAsync(Actor.AccountId, It.IsAny<string>(), null))
            .ReturnsAsync((int accountId, string permissionCode, int? storeId) =>
                ServiceResult<PermissionDecisionDto>.Success(new PermissionDecisionDto
                {
                    AccountId = accountId,
                    PermissionCode = permissionCode,
                    TargetStoreId = storeId,
                    Allowed = true
                }));

        var schemas = new AIImportSchemaRegistry();
        var options = Options.Create(new AIImportOptions());
        var suppliers = Mock.Of<IAdminSupplierService>();
        var entityRegistry = new AIImportEntityRegistry();
        return new AIImportService(
            context,
            Mock.Of<IAIImportDocumentPipeline>(),
            Mock.Of<IAIImportRegionAnalyzer>(),
            schemas,
            Mock.Of<IRequestDeduplicationService>(),
            permissions.Object,
            suppliers,
            options,
            Options.Create(new OllamaOptions { Model = "test" }),
            NullLogger<AIImportService>.Instance,
            new AIImportEntityCreator(
                context,
                Mock.Of<IAdminCategoryService>(),
                Mock.Of<IAdminDrinkService>(),
                Mock.Of<IAdminSizeService>(),
                Mock.Of<IAdminIngredientService>(),
                suppliers),
            new AIImportPreviewValidator(
                new AIImportCandidateValidator(schemas, options),
                new AIImportResolutionEngine()),
            entityRegistry,
            new AIImportAnalysisCoordinator(),
            new AIImportPreviewMutationCoordinator(),
            new AIImportConfirmCoordinator(entityRegistry),
            new AIImportSessionQuery());
    }
}
