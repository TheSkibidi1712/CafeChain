using System.Text.Json;

namespace CafeChain.Tests;

public sealed class SecretConfigurationHygieneTests
{
    private static readonly string[] WebSecretPaths =
    [
        "ConnectionStrings:DefaultConnection",
        "Email:Password",
        "PayOS:ClientId",
        "PayOS:ApiKey",
        "PayOS:ChecksumKey",
        "Cloudinary:ApiKey",
        "Cloudinary:ApiSecret",
        "PrintBridge:ApiKey",
        "Jwt:Key",
        "Pexels:ApiKey"
    ];

    [Fact]
    public void TrackedAppsettings_DoNotContainCredentials()
    {
        AssertPathsAreAbsent("CafeChain", "appsettings.json", WebSecretPaths);
        AssertPathsAreAbsent("CafeChain", "appsettings.Development.json", WebSecretPaths);
        AssertPathsAreAbsent(
            "CafeChain.PrintBridge",
            "appsettings.json",
            ["PrintBridge:ApiKey"]);
        AssertPathsAreAbsent(
            "CafeChain.PrintBridge",
            "appsettings.Development.json",
            ["PrintBridge:ApiKey"]);
    }

    [Fact]
    public void JwtIssuers_DoNotUseHardCodedFallbacks()
    {
        var authentication = Read(
            "CafeChain", "Extensions", "Services", "AuthenticationServiceExtensions.cs");
        var staffHub = Read("CafeChain", "Controllers", "StaffHubController.cs");

        Assert.DoesNotContain("if (environment.IsProduction())", authentication, StringComparison.Ordinal);
        Assert.DoesNotContain("configuration[\"Jwt:Key\"] ??", authentication, StringComparison.Ordinal);
        Assert.DoesNotContain("_configuration[\"Jwt:Key\"] ??", staffHub, StringComparison.Ordinal);
        Assert.Contains("Jwt:Key is required", authentication, StringComparison.Ordinal);
        Assert.Contains("Jwt:Key is required", staffHub, StringComparison.Ordinal);
    }

    private static void AssertPathsAreAbsent(
        string project,
        string file,
        IEnumerable<string> forbiddenPaths)
    {
        using var document = JsonDocument.Parse(Read(project, file));

        foreach (var path in forbiddenPaths)
        {
            Assert.False(
                HasPath(document.RootElement, path.Split(':')),
                $"Tracked configuration '{project}/{file}' must not contain '{path}'.");
        }
    }

    private static bool HasPath(JsonElement element, IReadOnlyList<string> segments)
    {
        foreach (var segment in segments)
        {
            if (element.ValueKind != JsonValueKind.Object
                || !element.TryGetProperty(segment, out element))
            {
                return false;
            }
        }

        return true;
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([FindRoot(), .. parts]));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "CafeChain")))
            directory = directory.Parent;

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
