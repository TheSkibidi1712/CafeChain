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
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
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
        Assert.True(result.Data.CanConfirm);
        Assert.Empty(result.Data.ConfirmBlockers);
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
        Assert.False(result.Data.CanConfirm);
        Assert.Contains(result.Data.ConfirmBlockers,
            blocker => blocker.Code == "KHÔNG_CÓ_DỮ_LIỆU_HỢP_LỆ_ĐỂ_NHẬP");
    }

    [Fact]
    public async Task Fatal_source_error_rejects_whole_batch_without_persisting_session_or_source()
    {
        await using var context = CreateDbContext();
        var pipeline = new Mock<IAIImportDocumentPipeline>();
        pipeline.Setup(service => service.PreflightAsync(
                It.Is<AIImportSourceFile>(file => file.FileName == "valid.xlsx"),
                null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIImportSourceDocument { SourceFormat = AIImportSourceFormats.Xlsx });
        pipeline.Setup(service => service.PreflightAsync(
                It.Is<AIImportSourceFile>(file => file.FileName == "fake.xlsx"),
                null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIImportSourceDocument
            {
                SourceFormat = AIImportSourceFormats.Xlsx,
                Errors =
                {
                    AIImportValidationContract.Issue("FILE_BỊ_HỎNG",
                        "Tệp không phải gói OpenXML .xlsx hợp lệ.", AIImportIssueSeverities.Error)
                }
            });

        var result = await CreateService(context, pipeline.Object).AnalyzeAsync(
            [Upload("valid.xlsx"), Upload("fake.xlsx")], null, false, Actor, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("FILE_BỊ_HỎNG", result.ErrorCode);
        Assert.Contains(result.Details, issue => issue.Metadata.TryGetValue("fileName", out var name)
                                                && Convert.ToString(name) == "fake.xlsx"
                                                && issue.Metadata.TryGetValue("fatal", out var fatal)
                                                && Convert.ToBoolean(fatal));
        Assert.Equal(0, await context.ImportSessions.CountAsync());
        Assert.Equal(0, await context.ImportSourceDocuments.CountAsync());
    }

    [Theory]
    [InlineData("DOCX_BỊ_HỎNG")]
    [InlineData("DOCX_VƯỢT_GIỚI_HẠN")]
    [InlineData("DỮ_LIỆU_VƯỢT_GIỚI_HẠN_MVP")]
    [InlineData("OCR_OUTPUT_KHÔNG_HỢP_LỆ")]
    public async Task Hard_document_error_is_reported_per_file_and_never_creates_session(string errorCode)
    {
        await using var context = CreateDbContext();
        var pipeline = new Mock<IAIImportDocumentPipeline>();
        pipeline.Setup(service => service.PreflightAsync(
                It.IsAny<AIImportSourceFile>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIImportSourceDocument
            {
                SourceFormat = AIImportSourceFormats.Docx,
                Errors =
                {
                    AIImportValidationContract.Issue(errorCode,
                        "Tệp nguồn không đạt điều kiện phân tích.", AIImportIssueSeverities.Error)
                }
            });

        var result = await CreateService(context, pipeline.Object).AnalyzeAsync(
            [Upload("invalid.docx")], null, false, Actor, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(errorCode, result.ErrorCode);
        Assert.Contains(result.Details, issue => Convert.ToString(issue.Metadata["fileName"]) == "invalid.docx"
                                                && Convert.ToBoolean(issue.Metadata["fatal"]));
        Assert.Equal(0, await context.ImportSessions.CountAsync());
        Assert.Equal(0, await context.ImportSourceDocuments.CountAsync());
        pipeline.Verify(service => service.AnalyzePreflightedAsync(
            It.IsAny<AIImportSourceDocument>(), It.IsAny<AIImportEntityType?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Document_without_any_candidate_after_analysis_is_rejected_before_session_persistence()
    {
        await using var context = CreateDbContext();
        var document = new AIImportSourceDocument { SourceFormat = AIImportSourceFormats.Xlsx };
        var pipeline = new Mock<IAIImportDocumentPipeline>();
        pipeline.Setup(service => service.PreflightAsync(
                It.IsAny<AIImportSourceFile>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);
        pipeline.Setup(service => service.AnalyzePreflightedAsync(
                document, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var result = await CreateService(context, pipeline.Object).AnalyzeAsync(
            [Upload("empty-data.xlsx")], null, false, Actor, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("KHÔNG_TÌM_THẤY_DỮ_LIỆU", result.ErrorCode);
        Assert.Contains(result.Details, issue => Convert.ToString(issue.Metadata["fileName"]) == "empty-data.xlsx"
                                                && Convert.ToBoolean(issue.Metadata["fatal"]));
        Assert.Equal(0, await context.ImportSessions.CountAsync());
        Assert.Equal(0, await context.ImportSourceDocuments.CountAsync());
    }

    [Fact]
    public async Task Empty_uploaded_file_returns_file_detail_without_calling_pipeline_or_creating_session()
    {
        await using var context = CreateDbContext();
        var pipeline = new Mock<IAIImportDocumentPipeline>(MockBehavior.Strict);

        var result = await CreateService(context, pipeline.Object).AnalyzeAsync(
            [Upload("empty.xlsx", [])], null, false, Actor, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("FILE_QUÁ_LỚN", result.ErrorCode);
        Assert.Contains(result.Details, issue => Convert.ToString(issue.Metadata["fileName"]) == "empty.xlsx"
                                                && Convert.ToBoolean(issue.Metadata["fatal"]));
        Assert.Equal(0, await context.ImportSessions.CountAsync());
        Assert.Equal(0, await context.ImportSourceDocuments.CountAsync());
    }

    [Theory]
    [InlineData("E42_fake_xlsx_contains_pdf.xlsx", "FILE_BỊ_HỎNG")]
    [InlineData("E40_suspicious_compression_ratio.xlsx", "FILE_QUÁ_LỚN")]
    public async Task Real_fatal_excel_fixture_returns_file_error_without_persisting_import_graph(
        string fileName,
        string expectedCode)
    {
        await using var context = CreateDbContext();
        var options = Options.Create(new AIImportOptions());
        var schemas = new AIImportSchemaRegistry();
        var pipeline = new AIImportDocumentPipeline(
        [
            new AIImportExcelSourceParser(
                new AIImportExcelParser(options),
                Mock.Of<IAIImportRegionAnalyzer>(),
                schemas)
        ]);
        var path = Path.Combine(RepositoryRoot(), "CafeChain.Tests", "Fixtures", "AIImport", "01_EXCEL", fileName);

        var result = await CreateService(context, pipeline).AnalyzeAsync(
            [Upload(fileName, await File.ReadAllBytesAsync(path))], null, false, Actor, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(expectedCode, result.ErrorCode);
        Assert.Contains(result.Details, issue => Convert.ToString(issue.Metadata["fileName"]) == fileName
                                                && Convert.ToBoolean(issue.Metadata["fatal"]));
        Assert.Equal(0, await context.ImportSessions.CountAsync());
        Assert.Equal(0, await context.ImportSourceDocuments.CountAsync());
        Assert.Equal(0, await context.ImportGroups.CountAsync());
        Assert.Equal(0, await context.ImportItems.CountAsync());
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

    private static IFormFile Upload(string fileName, byte[]? content = null)
    {
        content ??= [1, 2, 3, 4];
        return new FormFile(new MemoryStream(content), 0, content.Length, "Files", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".pdf" => "application/pdf",
                _ => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            }
        };
    }

    private static AIImportService CreateService(AppDbContext context, IAIImportDocumentPipeline? pipeline = null)
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
            pipeline ?? Mock.Of<IAIImportDocumentPipeline>(),
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

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Không tìm thấy repository root.");
    }
}
