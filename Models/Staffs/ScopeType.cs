namespace CafeChain.Models.Staffs
{
    public class ScopeType
    {
        public int Id { get; set; } // 1: Country, 2: Province, 3: Ward, 4: Store
        public string Code { get; set; }
        public string Name { get; set; }
    }
}
