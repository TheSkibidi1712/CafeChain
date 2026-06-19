namespace CafeChain.Application.DTOs.Admin.Units
{
    public class UnitDTO
    {
        public int UnitId { get; set; }

        public string Name { get; set; }
        public string UnitCode { get; set; }

        public string Type { get; set; } // 🔥 ADD (Weight / Volume)
    }
}
