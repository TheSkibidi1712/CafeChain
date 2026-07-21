namespace CafeChain.Models.Staffs
{
    public class ScopeType
    {
        public int ScopeTypeId { get; set; } // 1: Country, 2: Province, 3: District, 4: Ward, 5: Store
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
