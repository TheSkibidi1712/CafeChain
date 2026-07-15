namespace CafeChain.Models.Systems;

public class DocumentNumberCounter
{
    public int DocumentNumberCounterId { get; set; }
    public string CounterKey { get; set; } = string.Empty;
    public int DateKey { get; set; }
    public int LastValue { get; set; }
    public DateTime UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
