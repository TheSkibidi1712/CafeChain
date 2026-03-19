using CafeChain.Models.Locations;

namespace CafeChain.Models.Customers
{
    public class CustomerAddress
    {
        public int CustomerAddressId { get; set; }
        public int CustomerId { get; set; }
        public string Address { get; set; }
        public virtual Customer Customer { get; set; }
    }
}
