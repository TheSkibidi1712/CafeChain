using System.Text.Json;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Extensions.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CafeChain.Application.Tools;

public static class PurchaseOrderConsistencyCli
{
    public static async Task<int> RunAsync(string[] args)
    {
        var applySafe = args.Any(x => string.Equals(x, "--apply-safe", StringComparison.OrdinalIgnoreCase));
        var actorStaffId = ReadIntOption(args, "--actor-staff-id");
        var outputPath = ReadStringOption(args, "--out");
        if (applySafe && actorStaffId.GetValueOrDefault() <= 0)
        {
            Console.Error.WriteLine("--apply-safe yêu cầu --actor-staff-id hợp lệ.");
            return 2;
        }

        var contentRoot = ResolveContentRoot();
        var host = Host.CreateDefaultBuilder(Array.Empty<string>())
            .ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.SetBasePath(contentRoot);
                configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
                configuration.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false);
                configuration.AddUserSecrets(typeof(Program).Assembly, optional: true);
                configuration.AddEnvironmentVariables();
            })
            .ConfigureServices((context, services) =>
            {
                services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
                services.AddCafeChainDatabase(context.Configuration);
                services.AddCafeChainApplicationServices();
            })
            .Build();

        using var scope = host.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPurchaseOrderConsistencyService>();
        var report = applySafe
            ? (await service.RepairSafeAsync(actorStaffId!.Value)).Data
            : await service.DryRunAsync();
        if (report == null)
        {
            Console.Error.WriteLine("Không thể tạo báo cáo đối soát PO.");
            return 1;
        }

        Console.WriteLine($"Mode: {(applySafe ? "APPLY_SAFE" : "DRY_RUN")}");
        Console.WriteLine($"SAFE_AUTO_REPAIR: {report.SafeAutoRepairCount}");
        Console.WriteLine($"NEEDS_REVIEW: {report.NeedsReviewCount}");
        Console.WriteLine($"INVALID_BLOCKING: {report.InvalidBlockingCount}");
        Console.WriteLine($"REPAIRED: {report.RepairedCount}");
        foreach (var item in report.Items)
            Console.WriteLine($"[{item.Classification}] {item.PurchaseOrderCode}/line {item.PurchaseOrderLineId}: {item.IssueCode} - {item.Message}");

        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            var fullPath = Path.GetFullPath(outputPath, contentRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await File.WriteAllTextAsync(fullPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Report: {fullPath}");
        }

        return report.InvalidBlockingCount > 0 ? 3 : 0;
    }

    private static int? ReadIntOption(string[] args, string name)
    {
        var value = ReadStringOption(args, name);
        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    private static string? ReadStringOption(string[] args, string name)
    {
        for (var index = 0; index < args.Length - 1; index++)
            if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
                return args[index + 1];
        return null;
    }

    private static string ResolveContentRoot()
    {
        var current = Directory.GetCurrentDirectory();
        if (File.Exists(Path.Combine(current, "CafeChain.csproj"))) return current;
        var nested = Path.Combine(current, "CafeChain");
        if (File.Exists(Path.Combine(nested, "CafeChain.csproj"))) return nested;
        throw new InvalidOperationException("Không tìm thấy CafeChain.csproj để khởi tạo audit PO.");
    }
}
