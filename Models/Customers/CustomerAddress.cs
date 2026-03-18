using CafeChain.Models.Locations;

namespace CafeChain.Models.Customers
{
    public class CustomerAddress
    {
        public int CustomerAddressId { get; set; }
        public int CustomerId { get; set; }
        public string Address { get; set; }
        public int? WardId { get; set; }

        public virtual Customer Customer { get; set; }
        public virtual Ward Ward { get; set; }
    }
}
