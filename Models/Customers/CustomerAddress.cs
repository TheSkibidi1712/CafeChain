using CafeChain.Models.Locations;

namespace CafeChain.Models.Customers
{
    public class CustomerAddress
    {
        public int CusAId { get; set; }
        public int CusId { get; set; }
        public string Address { get; set; }
        public int? WarId { get; set; }

        public virtual Customer Customer { get; set; }
        public virtual Ward Ward { get; set; }
    }
}
