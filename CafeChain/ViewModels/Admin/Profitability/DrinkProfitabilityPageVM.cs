namespace CafeChain.ViewModels.Admin.Profitability
{
    public sealed class DrinkProfitabilityPageVM
    {
        public IReadOnlyList<ProfitabilitySelectOptionVM> Stores { get; init; } = Array.Empty<ProfitabilitySelectOptionVM>();
        public IReadOnlyList<ProfitabilitySelectOptionVM> Drinks { get; init; } = Array.Empty<ProfitabilitySelectOptionVM>();
        public bool CanUpdateGlobalPrice { get; init; }
        public bool CanManageToppingPolicy { get; init; }
    }

    public sealed class ProfitabilitySelectOptionVM
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
    }
}
