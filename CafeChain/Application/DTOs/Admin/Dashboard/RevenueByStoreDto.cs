namespace CafeChain.Application.DTOs.Admin.Dashboard
{
    public class RevenueByStoreDto
    {
        public int StoreId { get; set; }
        public string Name { get; set; }

        public int TotalOrders { get; set; }
        public decimal Revenue { get; set; }
    }
}
