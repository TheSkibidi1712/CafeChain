namespace CafeChain.Models.Inventories
{
    public class InventoryTransactionType
    {
        public int InventoryTransactionTypeId { get; set; }

        public string Code { get; set; }
        // IMPORT, EXPORT, STOCK_TAKE, WASTE

        public string Name { get; set; }

        public bool IsSystem { get; set; } // khóa không cho sửa

        public virtual ICollection<InventoryTransaction> InventoryTransactions { get; set; }
    }
}
