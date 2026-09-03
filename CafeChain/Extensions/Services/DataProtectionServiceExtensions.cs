using Microsoft.AspNetCore.DataProtection;

namespace CafeChain.Extensions.Services;

public sealed record DataProtectionHostingState(
    bool UsesPersistentKeys,
    bool KeyDirectoryReady);

public static class DataProtectionServiceExtensions
{
    public static IServiceCollection AddCafeChainDataProtection(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var configuredPath = configuration["DataProtection:KeysPath"]?.Trim();
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            if (environment.IsProduction())
            {
                throw new InvalidOperationException(
                    "DataProtection:KeysPath is required in Production. " +
                    "Use a persistent directory outside the publish root and grant the application pool read/write access.");
            }

            services.AddDataProtection().SetApplicationName("CafeChain");
            services.AddSingleton(new DataProtectionHostingState(false, false));
            return services;
        }

        var keyDirectoryPath = Path.GetFullPath(configuredPath, environment.ContentRootPath);
        EnsureProductionPathIsOutsideContentRoot(keyDirectoryPath, environment);
        EnsureDirectoryIsWritable(keyDirectoryPath);

        var builder = services.AddDataProtection()
            .SetApplicationName("CafeChain")
            .PersistKeysToFileSystem(new DirectoryInfo(keyDirectoryPath));

        if (OperatingSystem.IsWindows())
        {
            builder.ProtectKeysWithDpapi(protectToLocalMachine: true);
        }
        else if (environment.IsProduction())
        {
            throw new PlatformNotSupportedException(
                "Production Data Protection is configured for Windows DPAPI because CafeChain is hosted on Plesk/IIS.");
        }

        services.AddSingleton(new DataProtectionHostingState(true, true));
        return services;
    }

    private static void EnsureProductionPathIsOutsideContentRoot(
        string keyDirectoryPath,
        IWebHostEnvironment environment)
    {
        if (!environment.IsProduction()) return;

        var contentRoot = Path.GetFullPath(environment.ContentRootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var candidate = keyDirectoryPath
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (candidate.StartsWith(contentRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "DataProtection:KeysPath must be outside the application content/publish root in Production.");
        }
    }

    private static void EnsureDirectoryIsWritable(string keyDirectoryPath)
    {
        if (!Directory.Exists(keyDirectoryPath))
        {
            throw new InvalidOperationException(
                $"The configured Data Protection key directory does not exist: '{keyDirectoryPath}'.");
        }

        var probePath = Path.Combine(keyDirectoryPath, $".cafechain-write-probe-{Guid.NewGuid():N}");
        try
        {
            using (File.Create(probePath, bufferSize: 1, FileOptions.DeleteOnClose))
            {
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                $"The configured Data Protection key directory is not writable: '{keyDirectoryPath}'.",
                exception);
        }
    }
}
