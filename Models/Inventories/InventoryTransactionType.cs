namespace CafeChain.Models.Inventories
{
    public class InventoryTransactionType
    {
        public int InventoryTransactionTypeId { get; set; }
        public string Code { get; set; } // IMPORT, EXPORT, ADJUST
        public string Name { get; set; }

        public virtual ICollection<InventoryTransaction> InventoryTransactions { get; set; }
    }
}
