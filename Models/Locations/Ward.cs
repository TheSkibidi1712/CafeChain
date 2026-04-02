using CafeChain.Models.Customers;
using CafeChain.Models.Stores;

namespace CafeChain.Models.Locations
{
    public class Ward
    {
        public int WardId { get; set; }
        public string Name { get; set; }
        public int? ProvinceId { get; set; }

        public virtual Province Province { get; set; }
        public virtual ICollection<Store> Stores { get; set; }
        public virtual ICollection<CustomerAddress> CustomerAddresses { get; set; } 
    }
}
