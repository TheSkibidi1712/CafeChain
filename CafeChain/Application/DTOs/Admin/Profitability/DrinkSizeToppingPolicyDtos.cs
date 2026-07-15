namespace CafeChain.Application.DTOs.Admin.Profitability
{
    public sealed class DrinkSizeToppingPolicyDto
    {
        public int PolicyId { get; init; }
        public int DrinkSizeId { get; init; }
        public int ToppingId { get; init; }
        public string ToppingName { get; init; } = string.Empty;
        public decimal ToppingPrice { get; init; }
        public bool IsDefaultSelected { get; init; }
        public string PriceTreatment { get; init; } = string.Empty;
        public string CostTreatment { get; init; } = string.Empty;
        public decimal QuantityPerDrink { get; init; }
        public bool IsActive { get; init; }
        public string RowVersion { get; init; } = string.Empty;
    }

    public sealed class UpsertDrinkSizeToppingPolicyRequest
    {
        public int? PolicyId { get; set; }
        public int DrinkSizeId { get; set; }
        public int ToppingId { get; set; }
        public bool IsDefaultSelected { get; set; }
        public string PriceTreatment { get; set; } = string.Empty;
        public string CostTreatment { get; set; } = string.Empty;
        public decimal QuantityPerDrink { get; set; }
        public bool IsActive { get; set; } = true;
        public string? ExpectedRowVersion { get; set; }
        public string? Reason { get; set; }
    }
}
