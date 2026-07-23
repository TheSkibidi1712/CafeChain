namespace CafeChain.Application.Options
{
    public sealed class POSPaymentOptions
    {
        public const decimal DefaultCashDenominationStep = 1000m;

        public decimal CashDenominationStep { get; set; } = DefaultCashDenominationStep;

        public decimal GetEffectiveCashDenominationStep()
        {
            return CashDenominationStep > 0m && decimal.Truncate(CashDenominationStep) == CashDenominationStep
                ? CashDenominationStep
                : DefaultCashDenominationStep;
        }
    }
}
