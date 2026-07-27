namespace CafeChain.Models.Enums.Inventory;

/// <summary>
/// Defines the supplier purchasing authority for a procurement line.
/// Package fields are authoritative only for Packaged; procurement quantity
/// and procurement-unit price are authoritative only for Loose.
/// </summary>
public enum PurchaseMode
{
    Packaged = 0,
    Loose = 1
}
