using System.Collections.Generic;
using CafeChain.Application.DTOs.Admin.UnitConversions;

namespace CafeChain.ViewModels.Admin.UnitConversions
{
    public class AdminUnitConversionIndexPageVM
    {
        public AdminUnitConversionIndexDto Data { get; set; } = new();
        public bool CanWrite { get; set; } = true;
        public string? Search { get; set; }
        public string? Status { get; set; }
    }

    public class AdminUnitConversionFormPageVM
    {
        public UnitConversionVM Form { get; set; } = new();
        public List<AdminIngredientOptionDto> Ingredients { get; set; } = new();
        public List<AdminUnitOptionDto> Units { get; set; } = new();
        public List<PhysicalStandardDto> PhysicalStandards { get; set; } = new();
        public AdminUnitConversionEvaluateResult? Eval { get; set; }
        public string? EvalErrorCode { get; set; }
        public bool IsEdit { get; set; }
    }
}
