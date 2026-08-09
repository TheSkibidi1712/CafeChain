namespace CafeChain.ViewModels.Admin.Shared;

public sealed class AdminLifecycleStepperVM
{
    public string AccessibleLabel { get; init; } = "Tiến trình";
    public IReadOnlyList<AdminLifecycleStepVM> Steps { get; init; } = [];
}

public sealed class AdminLifecycleStepVM
{
    public string Label { get; init; } = string.Empty;
    public string IconClass { get; init; } = "fas fa-circle";
    public bool IsCompleted { get; init; }
    public bool IsCurrent { get; init; }
}

public sealed class AdminNextActionPanelVM
{
    public string Title { get; init; } = "Bước tiếp theo";
    public string Description { get; init; } = string.Empty;
    public string? RoleLabel { get; init; }
    public string IconClass { get; init; } = "fas fa-arrow-right";
}
