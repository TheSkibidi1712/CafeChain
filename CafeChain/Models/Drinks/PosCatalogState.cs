namespace CafeChain.Models.Drinks
{
    public class PosCatalogState
    {
        public int PosCatalogStateId { get; set; }
        public long Version { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }
}
