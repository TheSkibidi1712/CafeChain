using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal static class ManifestWriter
{
    public static void Write(FixtureCatalog catalog, IReadOnlyList<VerificationRecord> verification)
    {
        var verificationById = verification.ToDictionary(x => x.Id, StringComparer.Ordinal);
        var enriched = catalog.Items.Select(item => new
        {
            item.Id,
            item.RelativePath,
            item.Format,
            item.Category,
            item.Scenario,
            item.EntityHint,
            item.Expected,
            item.Notes,
            item.IntentionallyInvalid,
            item.ExpectedPages,
            item.ExpectedTextLayer,
            Sha256 = Hash(catalog.PathFor(item.RelativePath)),
            Verification = verificationById[item.Id]
        }).ToList();

        File.WriteAllText(catalog.PathFor("manifest.json"), JsonSerializer.Serialize(enriched, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));

        var csv = new StringBuilder("Id,RelativePath,Format,Category,Scenario,EntityHint,Expected,IntentionallyInvalid,ExpectedPages,ExpectedTextLayer,Sha256,Verified,VerificationDetail\r\n");
        foreach (var item in enriched)
            csv.AppendLine(string.Join(',',
                Csv(item.Id), Csv(item.RelativePath), Csv(item.Format), Csv(item.Category), Csv(item.Scenario), Csv(item.EntityHint), Csv(item.Expected),
                Csv(item.IntentionallyInvalid), Csv(item.ExpectedPages), Csv(item.ExpectedTextLayer), Csv(item.Sha256), Csv(item.Verification.Passed), Csv(item.Verification.Detail)));
        File.WriteAllText(catalog.PathFor("manifest.csv"), csv.ToString(), new UTF8Encoding(true));

        var md = new StringBuilder();
        md.AppendLine("# AI Smart Import - Fixture Manifest").AppendLine();
        md.AppendLine($"Tổng số fixture: **{enriched.Count}**. Xác minh cấu trúc: **{verification.Count(x => x.Passed)}/{verification.Count}**.").AppendLine();
        md.AppendLine("| ID | File | Format | Nhóm | Entity hint | Expected outcome |");
        md.AppendLine("|---|---|---|---|---|---|");
        foreach (var item in enriched)
            md.AppendLine($"| {item.Id} | `{item.RelativePath}` | {item.Format} | {EscapeMd(item.Category)} | {EscapeMd(item.EntityHint ?? "-")} | {EscapeMd(item.Expected)} |");
        File.WriteAllText(catalog.PathFor("MANIFEST.md"), md.ToString(), new UTF8Encoding(false));

        var report = new StringBuilder("# Verification report\n\n");
        report.AppendLine("`PASS` ở đây là xác minh fixture: file tồn tại, package/page count/text-layer đúng với mục đích; không đồng nghĩa AI Import application đã PASS expected business outcome.").AppendLine();
        report.AppendLine("| ID | Result | Size | Detail |\n|---|---|---:|---|");
        foreach (var result in verification)
            report.AppendLine($"| {result.Id} | {(result.Passed ? "PASS" : "FAIL")} | {result.SizeBytes} | {EscapeMd(result.Detail)} |");
        File.WriteAllText(catalog.PathFor("VERIFICATION_REPORT.md"), report.ToString(), new UTF8Encoding(false));

        var readme = """
            # Bộ test thủ công AI Smart Import

            Mỗi file trong bốn thư mục là **một tình huống độc lập**. Không import cả thư mục như một test duy nhất.

            - `01_EXCEL`: basic, multi-sheet, multi-region, multi-entity, sparse row, extra/forbidden/duplicate columns, merged/hidden/formula, duplicate/validation và resource/security limits.
            - `02_DOCX`: key-value, table, boundary, merge, Track Changes, nested table, body isolation, nhiều trang, active content và extension/corruption.
            - `03_PDF_TEXT`: key-value/table, multi-page, multi-column, rotation, decoration, Unicode, split row, limits và security preflight.
            - `04_PDF_SCAN`: image-only OCR, chất lượng thấp, rotation, multi-page, mixed provenance, OCR page/pixel limits và multi-entity.

            Cách dùng:

            1. Mở `MANIFEST.md` hoặc lọc `manifest.csv` theo nhóm.
            2. Upload **một file** vào AI Smart Import; chọn `Entity hint` nếu manifest có ghi.
            3. Đối chiếu Preview, issue code, locator/evidence và manual-review với cột `Expected`.
            4. Với PDF scan, chạy hai lượt: `OcrEnabled=false` để xác nhận `PDF_CẦN_OCR`, rồi bật provider/fake provider để kiểm OCR/MIXED/provenance.
            5. Các file `SECURITY`, `EXTENSION`, `LIMIT`, `EMPTY` hoặc được đánh dấu intentionally invalid không dùng để Confirm.

            Lưu ý tạo fixture:

            - Excel được tạo bằng `DocumentFormat.OpenXml` của chính dự án để kiểm soát shared string, cached formula, merge, hidden state và package lỗi. Runtime `@oai/artifact-tool` không có trong môi trường tạo bộ này.
            - PDF security P24-P28 dùng marker vô hại sau `%%EOF` để kiểm chính xác byte-level preflight hiện hữu; không chứa mã khai thác.
            - PDF scan chứa JPEG raster thật; không lưu text layer, trừ các file có format `PDF_MIXED`.
            - `VERIFICATION_REPORT.md` chỉ xác minh cấu trúc fixture. Expected business outcome vẫn cần chạy qua application/test suite.
            - `APPLICATION_PROBE.md` ghi smoke probe qua parser thật. Hiện 16/19 assertion đạt; P11-P13 cố ý dùng page dictionary `/Rotate` thật và đang phơi bày regression hiện hữu `BỐ_CỤC_PDF_KHÔNG_RÕ` (parser group word trước khi normalize rotation).
            """;
        File.WriteAllText(catalog.PathFor("README.md"), readme, new UTF8Encoding(false));
    }

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string Csv(object? value)
    {
        var text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        return $"\"{text.Replace("\"", "\"\"")}\"";
    }

    private static string EscapeMd(string text) => text.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
}
