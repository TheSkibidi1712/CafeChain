namespace CafeChain.Models.Inventories.StockTake
{
    public class StockTakeSession
    {
        public int StockTakeSessionId { get; set; }

        public int StoreId { get; set; }
        public int StaffId { get; set; }

        public string Code { get; set; }

        public DateTime CreatedAt { get; set; }

        public virtual ICollection<StockTakeDetail> Details { get; set; }
    }
}
