namespace CafeChain.Application.Options;

public sealed class InventoryNotificationOptions
{
    public const string SectionName = "Notifications";

    public int InventoryCooldownMinutes { get; set; } = 15;
}
