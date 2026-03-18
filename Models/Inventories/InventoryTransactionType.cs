namespace CafeChain.Models.Inventories
{
    public class InventoryTransactionType
    {
        public int Id { get; set; }
        public string Code { get; set; } // IMPORT, EXPORT, ADJUST
        public string Name { get; set; }

        public virtual ICollection<InventoryTransaction> InventoryTransactions { get; set; }
    }
}
