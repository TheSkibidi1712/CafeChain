namespace CafeChain.Application.Options;

public sealed class ProductionOperationsOptions
{
    public const string SectionName = "ProductionOperations";
    public decimal DefaultYieldVarianceTolerancePercent { get; set; } = 5m;
}
