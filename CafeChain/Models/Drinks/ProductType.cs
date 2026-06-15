namespace CafeChain.Models.Drinks
{
    public class ProductType
    {
        public int ProductTypeId { get; set; }

        public string Code { get; set; }
        // HANDCRAFTED, RETAIL

        public string Name { get; set; }
        // Pha chế, Đóng chai

        public bool Active { get; set; }

        public virtual ICollection<Drink> Drinks { get; set; }
    }
}
