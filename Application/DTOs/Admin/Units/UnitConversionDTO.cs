namespace CafeChain.Application.DTOs.Admin.Units
{
    public class UnitConversionDTO
    {
        public int? UnitConversionId { get; set; }

        public int FromUnitId { get; set; }
        public decimal FromQuantity { get; set; }

        public int ToUnitId { get; set; }
        public decimal ToQuantity { get; set; }

        // 🔥 ADD để render UI
        public string? FromUnitName { get; set; }
        public string? ToUnitName { get; set; }
    }
}
