using CafeChain.Application.Options;
using CafeChain.Controllers.Api.v1;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Linq;
using System.Reflection;
using Xunit;

namespace CafeChain.Tests.POS
{
    public class POSPaymentControllerActivationTests
    {
        [Fact]
        public void DependencyInjectionConstructor_IsExplicitAndIncludesPaymentOptions()
        {
            var selectedConstructor = Assert.Single(
                typeof(POSPaymentController)
                    .GetConstructors()
                    .Where(constructor =>
                        constructor.GetCustomAttribute<ActivatorUtilitiesConstructorAttribute>() != null));

            Assert.Contains(
                selectedConstructor.GetParameters(),
                parameter => parameter.ParameterType == typeof(IOptions<POSPaymentOptions>));
        }
    }
}
