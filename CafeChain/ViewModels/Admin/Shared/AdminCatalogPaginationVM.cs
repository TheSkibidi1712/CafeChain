namespace CafeChain.ViewModels.Admin.Shared;

public sealed class AdminCatalogPaginationVM
{
    public string? Keyword { get; init; }
    public bool? Active { get; init; }
    public int PageSize { get; init; }
    public int PageIndex { get; init; }
    public int TotalPages { get; init; }
    public int TotalCount { get; init; }
    public int CurrentItemCount { get; init; }
}
