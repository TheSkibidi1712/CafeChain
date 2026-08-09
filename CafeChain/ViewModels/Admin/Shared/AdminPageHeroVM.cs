namespace CafeChain.ViewModels.Admin.Shared;

public sealed class AdminPageHeroVM
{
    public string Eyebrow { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string IconClass { get; init; } = "fas fa-layer-group";
    public string? StatusLabel { get; init; }
    public string? StatusCssClass { get; init; }
    public IReadOnlyList<AdminPageHeroBreadcrumbVM> Breadcrumbs { get; init; } = [];
    public IReadOnlyList<AdminPageHeroActionVM> Actions { get; init; } = [];
}

public sealed class AdminPageHeroBreadcrumbVM
{
    public string Label { get; init; } = string.Empty;
    public string? Url { get; init; }
}

public sealed class AdminPageHeroActionVM
{
    public string Label { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string IconClass { get; init; } = "fas fa-arrow-right";
    public bool IsPrimary { get; init; }
    public string? Id { get; init; }
}
