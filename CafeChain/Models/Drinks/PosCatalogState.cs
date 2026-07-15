namespace CafeChain.Models.Drinks
{
    public class PosCatalogState
    {
        public int PosCatalogStateId { get; set; }
        public int StoreId { get; set; }
        public long Version { get; set; }
        public string? PayloadHash { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public virtual CafeChain.Models.Stores.Store Store { get; set; } = null!;
    }
}
