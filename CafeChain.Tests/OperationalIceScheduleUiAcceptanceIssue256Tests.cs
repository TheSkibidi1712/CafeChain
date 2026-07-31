using Xunit;

namespace CafeChain.Tests;

public sealed class OperationalIceScheduleUiAcceptanceIssue256Tests
{
    private static readonly string IndexView = ReadRepoFile(
        "CafeChain", "Areas", "Admin", "Views", "AdminOperationalIce", "Index.cshtml");
    private static readonly string Controller = ReadRepoFile(
        "CafeChain", "Areas", "Admin", "Controllers", "AdminOperationalIceController.cs");

    [Fact]
    public void ScheduleMode_ShowsLoadingState()
    {
        Assert.Contains("scheduleLoadingState", IndexView);
        Assert.Contains("Đang tải lịch làm việc…", IndexView);
        Assert.Contains("setScheduleState('loading')", IndexView);
    }

    [Fact]
    public void ScheduleMode_ShowsScheduleSelectorWhenDataExists()
    {
        Assert.Contains("id=\"scheduleSource\"", IndexView);
        Assert.Contains("scheduleHasDataState", IndexView);
        Assert.Contains("payload.data.forEach", IndexView);
    }

    [Fact]
    public void ScheduleMode_PrefillsNameTimeLeadAndTarget()
    {
        Assert.Contains("shiftName.value = option.dataset.name", IndexView);
        Assert.Contains("shiftStartDisplay.value = displayDateTime(option.dataset.start)", IndexView);
        Assert.Contains("scheduleShiftLeadId.value = option.dataset.leadId", IndexView);
        Assert.Contains("Định mức đá đề xuất", IndexView);
    }

    [Fact]
    public void ScheduleMode_BindsSourceScheduleShiftId()
    {
        Assert.Contains("name=\"SourceScheduleShiftId\"", IndexView);
        Assert.Contains("sourceScheduleShiftId.value = option.value", IndexView);
        Assert.Contains("sourceScheduleShiftId.disabled = !fromSchedule", IndexView);
    }

    [Fact]
    public void ScheduleMode_ShowsClearEmptyStateWhenNoScheduleExists()
    {
        Assert.Contains("scheduleEmptyState", IndexView);
        Assert.Contains("Không có lịch làm việc phù hợp.", IndexView);
        Assert.Contains("Bạn có thể tạo lịch nhân sự trước hoặc chuyển sang tạo ca thủ công.", IndexView);
    }

    [Fact]
    public void ScheduleMode_EmptyState_DisablesCreateButton()
    {
        Assert.Contains("state !== 'has-data'", IndexView);
        Assert.Contains("setScheduleState('empty')", IndexView);
        Assert.Contains("id=\"createShiftButton\"", IndexView);
    }

    [Fact]
    public void ScheduleMode_EmptyState_CanSwitchToManual()
    {
        Assert.Contains("switchToManualButton", IndexView);
        Assert.Contains("manualMode.dispatchEvent", IndexView);
        Assert.Contains("Chuyển sang tạo thủ công", IndexView);
    }

    [Fact]
    public void ScheduleMode_DoesNotSilentlyCreateManualShift()
    {
        Assert.Contains("const fromSchedule = mode === 'schedule';", IndexView);
        Assert.DoesNotContain("mode === 'schedule' && Boolean(scheduleSelect)", IndexView);
    }

    [Fact]
    public void ScheduleMode_DoesNotSubmitStaffScheduleWithoutSourceId()
    {
        Assert.Contains("|| !sourceScheduleShiftId.value", IndexView);
        Assert.Contains("clearScheduleAuthority();", IndexView);
        Assert.Contains("sourceScheduleShiftId.disabled = !fromSchedule", IndexView);
    }

    [Fact]
    public void ScheduleMode_ShowsErrorStateWhenLoadingFails()
    {
        Assert.Contains("scheduleErrorState", IndexView);
        Assert.Contains("Không thể tải lịch làm việc. Vui lòng thử lại.", IndexView);
        Assert.Contains("setScheduleState('error')", IndexView);
    }

    [Fact]
    public void ScheduleMode_ErrorState_IsNotTreatedAsEmpty()
    {
        Assert.Contains("if (payload.data.length === 0)", IndexView);
        Assert.Contains("setScheduleState('empty')", IndexView);
        Assert.Contains("catch (error)", IndexView);
        Assert.Contains("setScheduleState('error')", IndexView);
    }

    [Fact]
    public void ScheduleMode_RetryReloadsSchedules()
    {
        Assert.Contains("retryScheduleButton?.addEventListener('click', loadSchedules)", IndexView);
        Assert.Contains("Thử lại", IndexView);
    }

    [Fact]
    public void ScheduleMode_DoesNotExposeTechnicalException()
    {
        Assert.Contains("message = \"Không thể tải lịch làm việc. Vui lòng thử lại.\"", Controller);
        Assert.DoesNotContain("message = exception.Message", Controller);
        Assert.DoesNotContain("error?.message", IndexView);
    }

    [Fact]
    public void SwitchToManual_ClearsScheduleSource()
    {
        Assert.Contains("clearScheduleAuthority();", IndexView);
        Assert.Contains("sourceScheduleShiftId.disabled = true", IndexView);
        Assert.Contains("creationSource.value = fromSchedule ? 'StaffSchedule' : 'Manual'", IndexView);
    }

    [Fact]
    public void SwitchToSchedule_ClearsStaleManualAuthority()
    {
        Assert.Contains("clearSchedulePrefill();", IndexView);
        Assert.Contains("loadSchedules();", IndexView);
        Assert.Contains("captureManualDraft()", IndexView);
    }

    [Fact]
    public void ManualMode_SubmitsNullScheduleSource()
    {
        Assert.Contains("sourceScheduleShiftId.value = ''", IndexView);
        Assert.Contains("sourceScheduleShiftId.disabled = true", IndexView);
        Assert.Contains("scheduleShiftLeadId.disabled = true", IndexView);
    }

    [Fact]
    public void ScheduleEndpoint_UsesStoreDateScopeAndReturnsBusinessError()
    {
        Assert.Contains("public async Task<IActionResult> ScheduleOptions(", Controller);
        Assert.Contains("_storeScopeResolver.ResolveAsync(actor, storeId", Controller);
        Assert.Contains("businessDate.Date", Controller);
        Assert.Contains("StatusCodes.Status500InternalServerError", Controller);
    }

    private static string ReadRepoFile(params string[] path) =>
        File.ReadAllText(Path.Combine([FindRepoRoot(), .. path]));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null
               && !File.Exists(Path.Combine(directory.FullName, "CafeChain", "CafeChain.slnx")))
            directory = directory.Parent;
        return directory?.FullName
               ?? throw new DirectoryNotFoundException("Không tìm thấy repository root.");
    }
}
