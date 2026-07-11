using CafeChain.Models.Inventories.Ingredients;

namespace CafeChain.Models.Inventories.PreparedItems
{
    /// <summary>
    /// Stable BTP / semi-finished inventory identity (ADR-0006 / Issue #116).
    /// Not a Recipe version. Stock cutover is later (#115/#114).
    /// </summary>
    public class PreparedItem
    {
        public int PreparedItemId { get; set; }

        public string Code { get; set; } = null!;

        public string Name { get; set; } = null!;

        public int BaseUnitId { get; set; }

        public string? Description { get; set; }

        public bool Active { get; set; }

        public virtual Unit BaseUnit { get; set; } = null!;
    }
}
