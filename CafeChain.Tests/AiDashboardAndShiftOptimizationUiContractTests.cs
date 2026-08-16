namespace CafeChain.Tests;

public sealed class AiDashboardAndShiftOptimizationUiContractTests
{
    [Fact]
    public void DashboardFrontend_RendersContractSectionsWithoutDuplicatingLegacyNarrative()
    {
        var script = Read(
            "CafeChain",
            "wwwroot",
            "js",
            "Admin",
            "Dashboard",
            "dashboard-intelligence.js");

        Assert.Contains("Trả lời trực tiếp", script, StringComparison.Ordinal);
        Assert.Contains("Số liệu chứng minh", script, StringComparison.Ordinal);
        Assert.Contains("Việc cần kiểm tra", script, StringComparison.Ordinal);
        Assert.Contains("Xem nguồn dữ liệu", script, StringComparison.Ordinal);
        Assert.Contains("data.sectionConfig || {}", script, StringComparison.Ordinal);
        Assert.Contains("data.directAnswer || data.summary", script, StringComparison.Ordinal);
        Assert.DoesNotContain("data.overview || []", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Điểm đáng chú ý", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Kết luận", script, StringComparison.Ordinal);
        Assert.Contains("Complete: \"Đầy đủ\"", script, StringComparison.Ordinal);
        Assert.Contains("Partial: \"Một phần\"", script, StringComparison.Ordinal);
        Assert.Contains("Insufficient: \"Chưa đủ dữ liệu\"", script, StringComparison.Ordinal);
        Assert.Contains("Fallback: \"Chế độ dự phòng\"", script, StringComparison.Ordinal);
        Assert.Contains("localizedLabel", script, StringComparison.Ordinal);
    }

    [Fact]
    public void DashboardAi_UserFacingLanguage_UsesVietnameseBusinessTerms()
    {
        var script = Read(
            "CafeChain",
            "wwwroot",
            "js",
            "Admin",
            "Dashboard",
            "dashboard-intelligence.js");
        var guide = Read(
            "CafeChain",
            "Areas",
            "Admin",
            "Views",
            "Dashboard",
            "Guide.cshtml");

        Assert.Contains("Bằng chứng", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("kết quả dự phòng an toàn", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Deterministic fallback", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Evidence:", script, StringComparison.Ordinal);
        Assert.DoesNotContain("AnswerFocus", guide, StringComparison.Ordinal);
        Assert.DoesNotContain("Chart / Evidence / Limitation", guide, StringComparison.Ordinal);
        Assert.DoesNotContain("Retry", guide, StringComparison.Ordinal);
    }

    [Fact]
    public void ScheduleConfigurationForm_HasFourLabeledGroupsAndNoProposalActions()
    {
        var view = Read(
            "CafeChain",
            "Areas",
            "Admin",
            "Views",
            "AdminShiftOptimization",
            "Index.cshtml");
        var script = Read(
            "CafeChain",
            "wwwroot",
            "js",
            "Admin",
            "StaffShift",
            "shift-optimization.js");

        Assert.Contains("<fieldset", view, StringComparison.Ordinal);
        Assert.Contains("<legend", view, StringComparison.Ordinal);
        Assert.Contains("aria-describedby", view, StringComparison.Ordinal);
        Assert.Contains("shiftOptimizationSetup", view, StringComparison.Ordinal);
        Assert.Contains("shiftOptimizationConfig", view, StringComparison.Ordinal);
        Assert.Contains("Cấu hình dữ liệu lịch", view, StringComparison.Ordinal);
        Assert.DoesNotContain("cảnh báo thiếu lịch", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cảnh báo ca thiếu", view, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Thứ 2", view, StringComparison.Ordinal);
        Assert.Contains("renderConfig", script, StringComparison.Ordinal);
        Assert.Contains("configOutput", script, StringComparison.Ordinal);
        Assert.Contains("statusLabels", script, StringComparison.Ordinal);
        Assert.DoesNotContain("data-generate", view, StringComparison.Ordinal);
        Assert.DoesNotContain("data-explain", view, StringComparison.Ordinal);
        Assert.DoesNotContain("data-apply", view, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"proposal\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Tạo đề xuất", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Giải thích đề xuất", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Áp dụng lịch", script, StringComparison.Ordinal);
        Assert.DoesNotContain("reasonCodeLabels", script, StringComparison.Ordinal);
        Assert.DoesNotContain("shiftProposalResult", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ScheduleProposalApisAndAiSkill_AreRemovedWhileHistoricalEntitiesRemain()
    {
        var controller = Read(
            "CafeChain", "Areas", "Admin", "Controllers",
            "AdminShiftOptimizationController.cs");
        var serviceInterface = Read(
            "CafeChain", "Application", "Interfaces", "Admin", "Staffs",
            "IShiftOptimizationService.cs");
        var repositoryInterface = Read(
            "CafeChain", "Infrastructure", "Interfaces", "Admin", "Staffs",
            "IShiftOptimizationRepository.cs");
        var aiInterface = Read(
            "CafeChain", "Application", "Interfaces", "AI", "IAIService.cs");
        var skillCatalog = Read(
            "CafeChain", "Application", "Services", "AI", "AISkillCatalog.cs");
        var settings = Read("CafeChain", "appsettings.json");
        var models = Read("CafeChain", "Models", "Staffs", "ShiftIntelligenceModels.cs");
        var dbContext = Read("CafeChain", "Data", "AppDbContext.cs");

        Assert.DoesNotContain(" Generate(", controller, StringComparison.Ordinal);
        Assert.DoesNotContain(" Proposal(", controller, StringComparison.Ordinal);
        Assert.DoesNotContain(" Explain(", controller, StringComparison.Ordinal);
        Assert.DoesNotContain(" Apply(", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("GenerateAsync", serviceInterface, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplyAsync", serviceInterface, StringComparison.Ordinal);
        Assert.DoesNotContain("GetProposalAsync", repositoryInterface, StringComparison.Ordinal);
        Assert.DoesNotContain("GetScheduledStatusAsync", repositoryInterface, StringComparison.Ordinal);
        Assert.DoesNotContain("ExplainShiftProposalAsync", aiInterface, StringComparison.Ordinal);
        Assert.DoesNotContain("shift-proposal-explanation", skillCatalog, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ShiftOptimization\"", settings, StringComparison.Ordinal);

        Assert.Contains("class ScheduleOptimizationProposal", models, StringComparison.Ordinal);
        Assert.Contains("class ScheduleOptimizationAssignment", models, StringComparison.Ordinal);
        Assert.Contains("DbSet<ScheduleOptimizationProposal>", dbContext, StringComparison.Ordinal);
        Assert.Contains("DbSet<ScheduleOptimizationAssignment>", dbContext, StringComparison.Ordinal);
    }

    [Fact]
    public void ScheduleGapNotificationSubsystem_IsRemovedAndLegacyRowsAreSuppressed()
    {
        var realtime = Read(
            "CafeChain",
            "wwwroot",
            "js",
            "Admin",
            "Notifications",
            "inventory-notification-realtime.js");
        var settings = Read("CafeChain", "appsettings.json");
        var developmentSettings = Read("CafeChain", "appsettings.Development.json");
        var workers = Read("CafeChain", "Extensions", "Services", "WorkerServiceExtensions.cs");
        var services = Read("CafeChain", "Extensions", "Services", "ApplicationServiceExtensions.cs");
        var repository = Read(
            "CafeChain", "Infrastructure", "Repositories", "Operations",
            "StaffNotificationRepository.cs");
        var root = FindRoot();

        Assert.False(File.Exists(Path.Combine(root, "CafeChain", "Application", "Workers", "StaffScheduleGapNotificationWorker.cs")));
        Assert.False(File.Exists(Path.Combine(root, "CafeChain", "Application", "Services", "Admin", "Staffs", "StaffScheduleGapNotificationService.cs")));
        Assert.DoesNotContain("StaffScheduleGapNotificationWorker", workers, StringComparison.Ordinal);
        Assert.DoesNotContain("IStaffScheduleGapNotificationService", services, StringComparison.Ordinal);
        Assert.DoesNotContain("STAFF_SCHEDULE_GAP", realtime, StringComparison.Ordinal);
        Assert.DoesNotContain("StaffScheduleNotifications", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("StaffScheduleNotifications", developmentSettings, StringComparison.Ordinal);
        Assert.Contains("RetiredScheduleGapType", repository, StringComparison.Ordinal);
        Assert.Contains("x.Type != RetiredScheduleGapType", repository, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplanationSchema_RequiresGroundedCompactResponseContract()
    {
        var schema = Read(
            "CafeChain",
            "Resources",
            "AI",
            "schemas",
            "dashboard-insight-explanation.schema.json");

        Assert.Contains("\"directAnswer\"", schema, StringComparison.Ordinal);
        Assert.Contains("\"proofPoints\"", schema, StringComparison.Ordinal);
        Assert.Contains("\"actionToCheck\"", schema, StringComparison.Ordinal);
        Assert.Contains("\"usedEvidenceIds\"", schema, StringComparison.Ordinal);
        Assert.Contains("\"limitations\"", schema, StringComparison.Ordinal);
        Assert.Contains("\"verifyCondition\"", schema, StringComparison.Ordinal);
        Assert.Contains("\"maxItems\": 3", schema, StringComparison.Ordinal);
        Assert.DoesNotContain("\"priority\"", schema, StringComparison.Ordinal);
        Assert.Contains("\"additionalProperties\": false", schema, StringComparison.Ordinal);
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([FindRoot(), .. parts]));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "CafeChain")))
            directory = directory.Parent;
        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Không tìm thấy repository root.");
    }
}
