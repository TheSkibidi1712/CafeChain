using System.Text;
using CafeChain.Application.Options;
using CafeChain.Application.Services.AIImport;
using CafeChain.Models.AIImport;
using Microsoft.Extensions.Options;

internal sealed record ProbeResult(string Id, bool Passed, string Detail);

internal static class ApplicationProbe
{
    public static async Task<List<ProbeResult>> RunAsync(FixtureCatalog catalog)
    {
        var results = new List<ProbeResult>();
        var options = Options.Create(new AIImportOptions { OcrEnabled = false });
        var excel = new AIImportExcelParser(options);
        var docx = new AIImportDocxSourceParser(options);
        var pdf = new AIImportPdfSourceParser(options);

        await Excel("E01", result => result.Errors.Count == 0 && result.Regions.Count == 1, "basic workbook -> 1 region");
        await Excel("E06", result => result.Errors.Count == 0 && result.Regions.Count == 5, "5 sheets -> 5 regions");
        await Excel("E23", result => result.Warnings.Any(x => x.Code == "DỮ_LIỆU_ẨN"), "hidden sheet warning");
        await Excel("E36", result => result.Errors.Any(x => x.Code == "DỮ_LIỆU_VƯỢT_GIỚI_HẠN_MVP"), "sheet limit error");
        await Excel("E40", result => result.Errors.Any(x => x.Code == "FILE_QUÁ_LỚN"), "compression-ratio error");
        await Excel("E41", result => result.Errors.Any(x => x.Code == "FILE_BỊ_HỎNG"), "corrupt package error");

        await Source("D01", docx, AIImportEntityType.Category,
            result => result.Errors.Count == 0 && result.Groups.SelectMany(x => x.Candidates).Any(), "DOCX basic candidate");
        await Source("D14", docx, AIImportEntityType.Category,
            result => result.Warnings.Any(x => x.Code == "DOCX_TRACK_CHANGE_CẦN_XEM_LẠI"), "tracked insertion review");
        await Source("D19", docx, AIImportEntityType.Category,
            result => result.Groups.SelectMany(x => x.Candidates).Any(x => x.MappedData.GetValueOrDefault("CategoryCode") == "CAT_BODY")
                      && result.Groups.SelectMany(x => x.Candidates).All(x => x.MappedData.GetValueOrDefault("CategoryCode") != "CAT_HEADER"), "header/footer isolation");
        await Source("D25", docx, null,
            result => result.Errors.Any(x => x.Code == "NỘI_DUNG_CHỦ_ĐỘNG_KHÔNG_ĐƯỢC_HỖ_TRỢ"), "DOCX active content guard");

        await Source("P01", pdf, AIImportEntityType.Category,
            result => result.Errors.Count == 0 && result.Groups.SelectMany(x => x.Candidates).Any(), "PDF text basic candidate");
        await Source("P11", pdf, AIImportEntityType.Category,
            result => result.Errors.Count == 0 && result.Groups.SelectMany(x => x.Candidates).Any(), "PDF /Rotate 90 candidate");
        await Source("P12", pdf, AIImportEntityType.Category,
            result => result.Errors.Count == 0 && result.Groups.SelectMany(x => x.Candidates).Any(), "PDF /Rotate 180 candidate");
        await Source("P13", pdf, AIImportEntityType.Category,
            result => result.Errors.Count == 0 && result.Groups.SelectMany(x => x.Candidates).Any(), "PDF /Rotate 270 candidate");
        await Source("P21", pdf, null,
            result => result.Errors.Any(x => x.Code == "PDF_CẦN_OCR"), "blank PDF requires OCR");
        await Source("P23", pdf, null,
            result => result.Errors.Any(x => x.Code == "PDF_VƯỢT_GIỚI_HẠN"), "PDF page limit");
        await Source("P24", pdf, null,
            result => result.Errors.Any(x => x.Code == "NỘI_DUNG_CHỦ_ĐỘNG_KHÔNG_ĐƯỢC_HỖ_TRỢ"), "PDF active marker guard");
        await Source("S01", pdf, AIImportEntityType.Category,
            result => result.Errors.Any(x => x.Code == "PDF_CẦN_OCR"), "image-only PDF requires OCR when disabled");
        await Source("S12", pdf, AIImportEntityType.Category,
            result => result.Errors.Any(x => x.Code == "PDF_CẦN_OCR"), "mixed PDF requires OCR when disabled");

        var report = new StringBuilder("# Application parser probe\n\n");
        report.AppendLine("Ma trận smoke test đại diện chạy trực tiếp qua parser hiện hữu của CafeChain với `OcrEnabled=false`.\n");
        report.AppendLine("| Fixture | Result | Assertion |\n|---|---|---|");
        foreach (var result in results)
            report.AppendLine($"| {result.Id} | {(result.Passed ? "PASS" : "FAIL")} | {result.Detail.Replace("|", "\\|")} |");
        File.WriteAllText(catalog.PathFor("APPLICATION_PROBE.md"), report.ToString(), new UTF8Encoding(false));
        return results;

        async Task Excel(string id, Func<AIImportWorkbookData, bool> assertion, string detail)
        {
            try
            {
                var record = catalog.Items.Single(x => x.Id == id);
                await using var stream = File.OpenRead(catalog.PathFor(record.RelativePath));
                var result = await excel.ParseAsync(stream, default);
                results.Add(new ProbeResult(id, assertion(result), detail + $"; errors={result.Errors.Count}; warnings={result.Warnings.Count}; regions={result.Regions.Count}"));
            }
            catch (Exception exception)
            {
                results.Add(new ProbeResult(id, false, detail + "; " + exception.Message));
            }
        }

        async Task Source(string id, IAIImportSourceParser parser, AIImportEntityType? hint,
            Func<AIImportSourceDocument, bool> assertion, string detail)
        {
            try
            {
                var record = catalog.Items.Single(x => x.Id == id);
                var bytes = await File.ReadAllBytesAsync(catalog.PathFor(record.RelativePath));
                var result = await parser.ParseAsync(new AIImportSourceFile(Path.GetFileName(record.RelativePath), bytes, "application/octet-stream"), hint, default);
                var errorCodes = string.Join(',', result.Errors.Select(x => x.Code));
                var warningCodes = string.Join(',', result.Warnings.Select(x => x.Code));
                results.Add(new ProbeResult(id, assertion(result), detail + $"; errors={result.Errors.Count}[{errorCodes}]; warnings={result.Warnings.Count}[{warningCodes}]; groups={result.Groups.Count}"));
            }
            catch (Exception exception)
            {
                results.Add(new ProbeResult(id, false, detail + "; " + exception.Message));
            }
        }
    }
}
