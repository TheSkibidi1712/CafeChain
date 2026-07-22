using CafeChain.Application.Options;
using System.Globalization;

namespace CafeChain.Application.Services.POS
{
    public static class POSCashAmountValidator
    {
        public static string? Validate(decimal amount, decimal denominationStep, bool allowZero = false)
        {
            if (amount < 0m || (!allowZero && amount == 0m))
                return allowZero
                    ? "Số tiền mặt không được âm."
                    : "Số tiền mặt phải lớn hơn 0.";

            if (decimal.Truncate(amount) != amount)
                return "Số tiền mặt VND phải là số nguyên.";

            var effectiveStep = denominationStep > 0m
                ? denominationStep
                : POSPaymentOptions.DefaultCashDenominationStep;

            return amount % effectiveStep == 0m
                ? null
                : $"Số tiền mặt phải là bội số của {effectiveStep.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))}đ.";
        }
    }
}
