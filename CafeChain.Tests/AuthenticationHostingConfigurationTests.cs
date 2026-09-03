using CafeChain.Extensions.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CafeChain.Tests;

public sealed class AuthenticationHostingConfigurationTests
{
    [Fact]
    public void Production_requires_an_explicit_data_protection_key_path()
    {
        var services = new ServiceCollection();
        var configuration = Configuration(new Dictionary<string, string?>());

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddCafeChainDataProtection(configuration, Environment(Environments.Production)));

        Assert.Contains("DataProtection:KeysPath", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Shared_key_directory_survives_a_service_provider_restart()
    {
        var keyDirectory = Directory.CreateTempSubdirectory("cafechain-dp-");
        try
        {
            var configuration = Configuration(new Dictionary<string, string?>
            {
                ["DataProtection:KeysPath"] = keyDirectory.FullName
            });
            var environment = Environment(Environments.Production);

            string protectedValue;
            using (var first = Provider(configuration, environment))
            {
                protectedValue = first.GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector("cookie-restart-regression")
                    .Protect("account-42");
            }

            using var second = Provider(configuration, environment);
            var unprotectedValue = second.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("cookie-restart-regression")
                .Unprotect(protectedValue);

            Assert.Equal("account-42", unprotectedValue);
        }
        finally
        {
            keyDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public void A_different_key_directory_cannot_unprotect_an_existing_value()
    {
        var firstDirectory = Directory.CreateTempSubdirectory("cafechain-dp-a-");
        var secondDirectory = Directory.CreateTempSubdirectory("cafechain-dp-b-");
        try
        {
            var environment = Environment(Environments.Production);
            var firstConfiguration = Configuration(new Dictionary<string, string?>
            {
                ["DataProtection:KeysPath"] = firstDirectory.FullName
            });
            var secondConfiguration = Configuration(new Dictionary<string, string?>
            {
                ["DataProtection:KeysPath"] = secondDirectory.FullName
            });

            string protectedValue;
            using (var first = Provider(firstConfiguration, environment))
            {
                protectedValue = first.GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector("cookie-key-ring-regression")
                    .Protect("account-42");
            }

            using var second = Provider(secondConfiguration, environment);
            Assert.ThrowsAny<System.Security.Cryptography.CryptographicException>(() =>
                second.GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector("cookie-key-ring-regression")
                    .Unprotect(protectedValue));
        }
        finally
        {
            firstDirectory.Delete(recursive: true);
            secondDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public void Authentication_uses_the_dedicated_host_only_cookie()
    {
        var services = new ServiceCollection();
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "test-only-signing-key-at-least-32-characters"
        });

        services.AddLogging();
        services.AddCafeChainAuthentication(configuration, Environment(Environments.Production));

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);

        Assert.Equal(".CafeChain.Auth", options.Cookie.Name);
        Assert.Equal("/", options.Cookie.Path);
        Assert.Null(options.Cookie.Domain);
        Assert.True(options.Cookie.HttpOnly);
        Assert.Equal(Microsoft.AspNetCore.Http.CookieSecurePolicy.Always, options.Cookie.SecurePolicy);
        Assert.Equal(Microsoft.AspNetCore.Http.SameSiteMode.Lax, options.Cookie.SameSite);
    }

    [Fact]
    public void Web_services_use_sql_server_for_distributed_session_state()
    {
        var services = new ServiceCollection();
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\MSSQLLocalDB;Database=CafeChainTests;Trusted_Connection=True;",
            ["SessionCache:SchemaName"] = "dbo",
            ["SessionCache:TableName"] = "SessionCache"
        });

        services.AddCafeChainWeb(configuration);

        using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<IDistributedCache>();
        Assert.Equal("SqlServerCache", cache.GetType().Name);
    }

    [Theory]
    [InlineData("/hubs/inventory-notifications/negotiate")]
    [InlineData("/hubs/workshifts/negotiate")]
    public async Task SignalR_bearer_query_token_is_accepted_only_for_supported_hubs(string path)
    {
        var services = new ServiceCollection();
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "test-only-signing-key-at-least-32-characters"
        });
        services.AddLogging();
        services.AddCafeChainAuthentication(configuration, Environment(Environments.Production));

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        var httpContext = new DefaultHttpContext { RequestServices = provider };
        httpContext.Request.Path = path;
        httpContext.Request.QueryString = new QueryString("?access_token=test-token");
        var context = new MessageReceivedContext(
            httpContext,
            new Microsoft.AspNetCore.Authentication.AuthenticationScheme(
                JwtBearerDefaults.AuthenticationScheme,
                JwtBearerDefaults.AuthenticationScheme,
                typeof(JwtBearerHandler)),
            options);

        await options.Events.OnMessageReceived(context);

        Assert.Equal("test-token", context.Token);
    }

    [Fact]
    public void Customer_claim_refresh_preserves_the_existing_authentication_properties()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "CafeChain", "Controllers", "CustomerController.cs"));

        Assert.Equal(2, source.Split("authentication.Properties ??", StringSplitOptions.None).Length - 1);
    }

    private static ServiceProvider Provider(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCafeChainDataProtection(configuration, environment);
        return services.BuildServiceProvider();
    }

    private static IConfiguration Configuration(IDictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null
               && !File.Exists(Path.Combine(directory.FullName, "CafeChain", "CafeChain.csproj")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }

    private static IWebHostEnvironment Environment(string name) => new TestWebHostEnvironment
    {
        EnvironmentName = name,
        ApplicationName = "CafeChain.Tests",
        ContentRootPath = AppContext.BaseDirectory,
        ContentRootFileProvider = new NullFileProvider(),
        WebRootPath = AppContext.BaseDirectory,
        WebRootFileProvider = new NullFileProvider()
    };

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "CafeChain.Tests";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
