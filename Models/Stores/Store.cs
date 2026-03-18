using CafeChain.Models.Locations;
using CafeChain.Models.Payments;
using CafeChain.Models.Staffs;
using CafeChain.Models.Orders;

namespace CafeChain.Models.Stores
{
    public class Store
    {
        public int StoreId { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public string Phone { get; set; }
        public int? WardId { get; set; }
        public bool Active { get; set; }
        public DateTime CreatedAt { get; set; }

        public virtual Ward Ward { get; set; }

        public virtual ICollection<Staff> Staffs { get; set; }
        public virtual ICollection<DiningTable> DiningTables { get; set; }
        public virtual ICollection<StoreDrink> StoreDrinks { get; set; }
        public virtual ICollection<StoreTopping> StoreToppings { get; set; }
        public virtual ICollection<StoreInventory> StoreInventories { get; set; }
        public virtual ICollection<CashSession> CashSessions { get; set; }
        public virtual ICollection<Order> Orders { get; set; }
    }
}
