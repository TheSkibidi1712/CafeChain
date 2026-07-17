using CafeChain.Application.Interfaces.POS;
using CafeChain.Controllers.Api.v1;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Reflection;
using Xunit;

namespace CafeChain.Tests.POS
{
    public class POSCatalogControllerActivationTests
    {
        [Fact]
        public void DependencyInjectionConstructor_IsExplicitAndIncludesCatalogSnapshotService()
        {
            var selectedConstructor = Assert.Single(
                typeof(POSCatalogController)
                    .GetConstructors()
                    .Where(constructor =>
                        constructor.GetCustomAttribute<ActivatorUtilitiesConstructorAttribute>() != null));

            Assert.Contains(
                selectedConstructor.GetParameters(),
                parameter => parameter.ParameterType == typeof(IPOSCatalogSnapshotService));
        }
    }
}
