using CafeChain.Extensions.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CafeChain.Tests;

public sealed class AuthenticationCookieSecurityTests
{
    [Fact]
    public void Development_HttpSmoke_UsesRequestSchemeForAuthCookie()
    {
        var options = BuildCookieOptions("Development");

        Assert.Equal(CookieSecurePolicy.SameAsRequest, options.Cookie.SecurePolicy);
    }

    [Fact]
    public void NonDevelopment_AlwaysRequiresHttpsForAuthCookie()
    {
        var options = BuildCookieOptions("Production");

        Assert.Equal(CookieSecurePolicy.Always, options.Cookie.SecurePolicy);
    }

    private static CookieAuthenticationOptions BuildCookieOptions(string environmentName)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "test-only-jwt-key-with-sufficient-length"
            })
            .Build();
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(x => x.EnvironmentName).Returns(environmentName);

        var services = new ServiceCollection();
        services.AddCafeChainAuthentication(configuration, environment.Object);
        using var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
