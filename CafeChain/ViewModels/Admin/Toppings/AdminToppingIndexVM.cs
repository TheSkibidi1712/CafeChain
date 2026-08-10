using CafeChain.ViewModels.Shared;

namespace CafeChain.ViewModels.Admin.Toppings;

public sealed class AdminToppingIndexVM
{
    public string? Keyword { get; init; }
    public bool? Active { get; init; }
    public int AllCount { get; init; }
    public int ActiveCount { get; init; }
    public int InactiveCount { get; init; }
    public int PageSize { get; init; } = 10;
    public PaginatedListViewModel<AdminToppingVM> Toppings { get; init; }
        = new([], 0, 1, 10);
}
