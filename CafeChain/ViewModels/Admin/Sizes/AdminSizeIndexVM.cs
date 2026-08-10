using CafeChain.ViewModels.Shared;

namespace CafeChain.ViewModels.Admin.Sizes;

public sealed class AdminSizeIndexVM
{
    public string? Keyword { get; init; }
    public bool? Active { get; init; }
    public int AllCount { get; init; }
    public int ActiveCount { get; init; }
    public int InactiveCount { get; init; }
    public int PageSize { get; init; } = 10;
    public PaginatedListViewModel<AdminSizeVM> Sizes { get; init; }
        = new([], 0, 1, 10);
}
