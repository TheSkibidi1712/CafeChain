namespace CafeChain.ViewModels.Admin.StoreMenu
{
    public sealed class StoreMenuPageVM
    {
        public IReadOnlyList<StoreMenuStoreOptionVM> Stores { get; init; } = Array.Empty<StoreMenuStoreOptionVM>();
        public bool CanPublish { get; init; }
        public bool CanOperate { get; init; }
        public bool CanProvision { get; init; }
        public bool CanOverridePrice { get; init; }
    }

    public sealed class StoreMenuStoreOptionVM
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
    }
}
