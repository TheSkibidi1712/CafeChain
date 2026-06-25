using CafeChain.Models.Orders;
namespace CafeChain.Models.Drinks
{
    public class Size
    {
        public int SizeId { get; set; }
        public string SizeCode { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Active { get; set; }

        public virtual ICollection<DrinkSize> DrinkSizes { get; set; }
        public virtual ICollection<OrderDetail> OrderDetails { get; set; }
    }
}
