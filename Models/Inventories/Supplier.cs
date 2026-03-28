namespace CafeChain.Models.Inventories
{
    public class Supplier
    {
        public int SupplierId { get; set; }

        public string Code { get; set; } // SUP00001
        public string Name { get; set; }

        public string Phone { get; set; }
        public string Address { get; set; }

        public decimal DebtAmount { get; set; } // công nợ hiện tại

        public bool Active { get; set; }

        public virtual ICollection<StockImport> StockImports { get; set; }
    }
}
