namespace CafeChain.Models.Enums.Inventory;

public enum InventoryTransferDiscrepancyPostingType
{
    DESTINATION_REJECTED = 1,
    RETURN_REQUESTED = 2,
    RETURNED_TO_SOURCE = 3,
    WRITTEN_OFF = 4,
    CLOSED_SHORTAGE = 5
}
