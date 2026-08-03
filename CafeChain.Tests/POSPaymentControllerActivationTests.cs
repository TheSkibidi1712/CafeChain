using CafeChain.Application.Interfaces.POS;
using CafeChain.Controllers.Api.v1;
using System.Linq;
using System.Reflection;
using Xunit;

namespace CafeChain.Tests.POS
{
    public class POSPaymentControllerActivationTests
    {
        [Fact]
        public void Controller_DependsOnlyOnPaymentCancellationService()
        {
            var constructor = Assert.Single(typeof(POSPaymentController).GetConstructors());
            var parameter = Assert.Single(constructor.GetParameters());
            Assert.Equal(typeof(IPOSPaymentCancellationService), parameter.ParameterType);
        }
    }
}
