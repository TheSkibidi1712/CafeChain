using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CafeChain.Application.DTOs.Auditing;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Extensions.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CafeChain.Application.Tools
{
    /// <summary>
    /// Development-only entry: <c>dotnet run --project CafeChain -- audit-purchase-units [--out path.json]</c>
    /// Read-only. Never mutates database. Does not start the web host.
    /// Exit 0 = audit ran (COMPLETE and INCOMPLETE findings both valid).
    /// Exit non-zero = invalid args, invalid output path, or technical failure.
    /// </summary>
    public static class PurchaseUnitAuditCli
    {
        public static async Task<int> RunAsync(string[] args)
        {
            try
            {
                if (!TryParseArgs(args, out var outPathArg, out var parseError))
                {
                    Console.Error.WriteLine(parseError);
                    Console.Error.WriteLine("Usage: audit-purchase-units [--out path.json]");
                    return 2;
                }

                Console.WriteLine("=== CafeChain Purchase/Unit Audit (Issue #113 Checkpoint A) ===");
                Console.WriteLine("Mode: READ-ONLY (no SaveChanges, no remediation, no web host)");
                Console.WriteLine();

                var contentRoot = ResolveContentRoot();
                var outPath = ResolveOutputPath(outPathArg, contentRoot);

                // Reject clearly invalid destinations (e.g. empty path, Windows invalid device root)
                if (string.IsNullOrWhiteSpace(outPath)
                    || outPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                {
                    Console.Error.WriteLine($"Invalid output path: {outPathArg}");
                    return 2;
                }

                var host = Host.CreateDefaultBuilder(Array.Empty<string>())
                    .ConfigureAppConfiguration((_, config) =>
                    {
                        config.SetBasePath(contentRoot);
                        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
                        config.AddJsonFile("appsettings.Development.json", optional: true);
                        config.AddEnvironmentVariables();
                    })
                    .ConfigureServices((ctx, services) =>
                    {
                        // Minimal host: DB + application services only (no web, no workers)
                        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
                        services.AddCafeChainDatabase(ctx.Configuration);
                        services.AddCafeChainApplicationServices();
                    })
                    .Build();

                using var scope = host.Services.CreateScope();
                var audit = scope.ServiceProvider.GetRequiredService<IPurchaseUnitAuditService>();
                var report = await audit.RunAuditAsync();

                WriteConsoleSummary(report);

                var dir = Path.GetDirectoryName(outPath);
                if (string.IsNullOrEmpty(dir))
                {
                    Console.Error.WriteLine($"Invalid output path (no directory): {outPath}");
                    return 2;
                }

                Directory.CreateDirectory(dir);
                var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    Converters = { new JsonStringEnumConverter() }
                });

                // Secret hygiene: refuse to write if connection-like keys appear (should never)
                if (json.Contains("Password=", StringComparison.OrdinalIgnoreCase)
                    || json.Contains("DefaultConnection", StringComparison.OrdinalIgnoreCase)
                    || json.Contains("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    Console.Error.WriteLine("Refusing to write report: possible secret leakage detected in payload.");
                    return 3;
                }

                await File.WriteAllTextAsync(outPath, json);
                // Print relative path when under content root to avoid leaking full profile paths
                var displayPath = outPath.StartsWith(contentRoot, StringComparison.OrdinalIgnoreCase)
                    ? Path.GetRelativePath(contentRoot, outPath)
                    : outPath;
                Console.WriteLine();
                Console.WriteLine($"JSON report written: {displayPath}");
                Console.WriteLine("No database rows were modified.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Audit failed: {ex.Message}");
                return 1;
            }
        }

        private static bool TryParseArgs(string[] args, out string? outPathArg, out string error)
        {
            outPathArg = null;
            error = "";
            // args[0] is "audit-purchase-units"
            for (var i = 1; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--out", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(args[i], "-o", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[i + 1]) || args[i + 1].StartsWith('-'))
                    {
                        error = "Missing value for --out.";
                        return false;
                    }
                    outPathArg = args[i + 1];
                    i++;
                    continue;
                }

                error = $"Unknown argument: {args[i]}";
                return false;
            }

            return true;
        }

        private static string ResolveContentRoot()
        {
            // dotnet run starts the compiled application beneath bin/, so walk upward
            // until the project root is found instead of treating the build folder as content.
            for (var current = new DirectoryInfo(Directory.GetCurrentDirectory()); current != null; current = current.Parent)
            {
                if (File.Exists(Path.Combine(current.FullName, "CafeChain.csproj"))
                    && File.Exists(Path.Combine(current.FullName, "appsettings.json")))
                {
                    return current.FullName;
                }

                var nestedProject = Path.Combine(current.FullName, "CafeChain");
                if (File.Exists(Path.Combine(nestedProject, "CafeChain.csproj"))
                    && File.Exists(Path.Combine(nestedProject, "appsettings.json")))
                {
                    return nestedProject;
                }
            }

            return Directory.GetCurrentDirectory();
        }

        private static string ResolveOutputPath(string? outPathArg, string contentRoot)
        {
            if (!string.IsNullOrWhiteSpace(outPathArg))
            {
                if (Path.IsPathRooted(outPathArg))
                    return Path.GetFullPath(outPathArg);

                var relativePath = outPathArg;
                if (relativePath.StartsWith(".\\", StringComparison.Ordinal)
                    || relativePath.StartsWith("./", StringComparison.Ordinal))
                {
                    relativePath = relativePath[2..];
                }
                var projectDirectoryName = new DirectoryInfo(contentRoot).Name;
                if (relativePath.StartsWith(projectDirectoryName + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    || relativePath.StartsWith(projectDirectoryName + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    var repositoryRoot = Directory.GetParent(contentRoot)?.FullName ?? contentRoot;
                    return Path.GetFullPath(Path.Combine(repositoryRoot, relativePath));
                }

                return Path.GetFullPath(Path.Combine(contentRoot, relativePath));
            }

            var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            return Path.GetFullPath(Path.Combine(
                contentRoot,
                "docs",
                "runbooks",
                "reports",
                $"purchase-unit-audit-{stamp}.json"));
        }

        private static void WriteConsoleSummary(PurchaseUnitAuditReport report)
        {
            var s = report.Summary;
            Console.WriteLine($"GeneratedAtUtc: {report.GeneratedAtUtc:O}");
            Console.WriteLine($"SchemaVersion: {report.SchemaVersion}");
            Console.WriteLine($"Mode: {report.Mode}");
            Console.WriteLine();
            Console.WriteLine("--- Offers ---");
            Console.WriteLine($"  Total:              {s.OfferCount}");
            Console.WriteLine($"  COMPLETE:           {s.OfferComplete}");
            Console.WriteLine($"  SAFE_CANDIDATE:     {s.OfferSafeCandidate}");
            Console.WriteLine($"  BUSINESS_DECISION:  {s.OfferBusinessDecision}");
            Console.WriteLine($"  INVALID:            {s.OfferInvalid}");
            Console.WriteLine();
            Console.WriteLine("--- Primaries ---");
            Console.WriteLine($"  No Active primary:       {s.IngredientsWithNoActivePrimary}");
            Console.WriteLine($"  Multiple Active primary: {s.IngredientsWithMultipleActivePrimary}");
            Console.WriteLine();
            Console.WriteLine("--- Price history rows with issues ---");
            Console.WriteLine($"  {s.PriceHistoryIssues}");
            Console.WriteLine();
            Console.WriteLine("--- Active recipes (EstimatedBomCost) ---");
            Console.WriteLine($"  COMPLETE:   {s.RecipesComplete}");
            Console.WriteLine($"  INCOMPLETE: {s.RecipesIncomplete}");
            Console.WriteLine();

            Console.WriteLine("Sample incomplete offers (up to 10):");
            foreach (var o in report.Offers
                         .Where(x => x.Classification != PurchaseUnitRemediationClass.Complete)
                         .Take(10))
            {
                Console.WriteLine(
                    $"  IS#{o.IngredientSupplierId} Ing#{o.IngredientId} {o.IngredientCode} " +
                    $"class={o.Classification} requireApproval={o.RequiresOwnerApproval} " +
                    $"primary={o.IsPrimary} pkg={o.PackageQuantity?.ToString() ?? "null"} {o.PackageUnitCode} " +
                    $"codes=[{string.Join(",", o.CostIssueCodes.Concat(o.AuditIssueCodes))}]");
            }
        }
    }
}
