using CafeChain.Application.Options;
using CafeChain.Application.Services.POS;
using Xunit;

namespace CafeChain.Tests.POS
{
    public class POSCashTenderHardeningTests
    {
        private const decimal Step = POSPaymentOptions.DefaultCashDenominationStep;

        [Fact]
        public void TemporaryCash_Rejects66()
        {
            Assert.Equal(
                "Số tiền mặt phải là bội số của 1.000đ.",
                POSCashAmountValidator.Validate(66m, Step));
        }

        [Fact]
        public void TemporaryCash_Rejects500021()
        {
            Assert.NotNull(POSCashAmountValidator.Validate(500021m, Step));
        }

        [Fact]
        public void TemporaryCash_Accepts20000()
        {
            Assert.Null(POSCashAmountValidator.Validate(20000m, Step));
        }

        [Fact]
        public void FinalCash_500000For33000_Calculates467000Change()
        {
            const decimal receivedAmount = 500000m;
            const decimal amountDue = 33000m;

            Assert.Null(POSCashAmountValidator.Validate(receivedAmount, Step));
            Assert.Equal(467000m, receivedAmount - amountDue);
        }
    }
}
