using System.Text;
using System.Text.Json;
using QuestPDF.Infrastructure;

Console.OutputEncoding = Encoding.UTF8;
QuestPDF.Settings.License = LicenseType.Community;

var outputRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "CafeChain.Tests", "TestData", "AIImportFixtures"));
if (args.Length > 0)
    outputRoot = Path.GetFullPath(args[0]);

Directory.CreateDirectory(outputRoot);
var qaRoot = Path.Combine(Path.GetTempPath(), "CafeChain-AIImport-Fixture-QA");
if (Directory.Exists(qaRoot)) Directory.Delete(qaRoot, true);
var catalog = new FixtureCatalog(outputRoot);

ExcelFixtures.Generate(catalog);
DocxFixtures.Generate(catalog);
PdfFixtures.GenerateTextPdfs(catalog);
PdfFixtures.GenerateScanPdfs(catalog);

var verification = FixtureVerifier.Verify(catalog);
ManifestWriter.Write(catalog, verification);
QaContactSheetWriter.Write();
var applicationProbe = await ApplicationProbe.RunAsync(catalog);

Console.WriteLine($"Generated {catalog.Items.Count} fixtures in {outputRoot}");
Console.WriteLine($"Verification: {verification.Count(x => x.Passed)}/{verification.Count} passed");
Console.WriteLine($"Application probe: {applicationProbe.Count(x => x.Passed)}/{applicationProbe.Count} passed");

internal sealed record FixtureRecord(
    string Id,
    string RelativePath,
    string Format,
    string Category,
    string Scenario,
    string? EntityHint,
    string Expected,
    string Notes,
    bool IntentionallyInvalid = false,
    int? ExpectedPages = null,
    bool? ExpectedTextLayer = null);

internal sealed record VerificationRecord(
    string Id,
    string RelativePath,
    bool Passed,
    long SizeBytes,
    string Detail);

internal sealed class FixtureCatalog(string root)
{
    public string Root { get; } = root;
    public List<FixtureRecord> Items { get; } = [];

    public string PathFor(string relativePath)
    {
        var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return path;
    }

    public void Add(FixtureRecord record) => Items.Add(record);

    public void WriteBytes(FixtureRecord record, byte[] content)
    {
        File.WriteAllBytes(PathFor(record.RelativePath), content);
        Add(record);
    }

    public void WriteText(FixtureRecord record, string content)
    {
        File.WriteAllText(PathFor(record.RelativePath), content, new UTF8Encoding(false));
        Add(record);
    }
}
