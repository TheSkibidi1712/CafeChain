using System.IO.Compression;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;

internal static class FixtureVerifier
{
    public static List<VerificationRecord> Verify(FixtureCatalog catalog)
    {
        var results = new List<VerificationRecord>();
        foreach (var fixture in catalog.Items)
        {
            var path = catalog.PathFor(fixture.RelativePath);
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists || info.Length == 0)
                    throw new InvalidDataException("File không tồn tại hoặc rỗng.");

                var details = new List<string> { $"size={info.Length}" };
                if (!fixture.IntentionallyInvalid || fixture.Id is "P24" or "P25" or "P26" or "P27" or "P28")
                {
                    switch (fixture.Format)
                    {
                        case "XLSX":
                            using (var document = SpreadsheetDocument.Open(path, false))
                            {
                                var sheetCount = document.WorkbookPart?.Workbook.Sheets?.Count() ?? 0;
                                details.Add($"sheets={sheetCount}");
                            }
                            break;
                        case "DOCX":
                        case "DOCM":
                            using (var document = WordprocessingDocument.Open(path, false))
                            {
                                if (document.MainDocumentPart?.Document?.Body == null)
                                    throw new InvalidDataException("DOCX thiếu body.");
                                details.Add($"paragraphs={document.MainDocumentPart.Document.Body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>().Count()}");
                                if (fixture.Id == "D20" && document.MainDocumentPart.WordprocessingCommentsPart == null)
                                    throw new InvalidDataException("DOCX comment thiếu comments part.");
                                if (fixture.Id == "D21" && (document.MainDocumentPart.FootnotesPart == null || document.MainDocumentPart.EndnotesPart == null))
                                    throw new InvalidDataException("DOCX notes thiếu footnotes/endnotes part.");
                                if (fixture.Id is "D14" or "D15" or "D16")
                                {
                                    var xml = document.MainDocumentPart.Document.OuterXml;
                                    if (!xml.Contains("w:ins") && !xml.Contains("w:del") && !xml.Contains("w:move"))
                                        throw new InvalidDataException("DOCX revision thiếu revision element.");
                                    details.Add("revision=true");
                                }
                            }
                            break;
                        case "PDF_TEXT":
                        case "PDF_SCAN":
                        case "PDF_MIXED":
                            using (var pdf = PdfDocument.Open(path))
                            {
                                details.Add($"pages={pdf.NumberOfPages}");
                                if (fixture.ExpectedPages.HasValue && pdf.NumberOfPages != fixture.ExpectedPages.Value)
                                    throw new InvalidDataException($"Page count {pdf.NumberOfPages}, expected {fixture.ExpectedPages}.");
                                if (fixture.Id is "P11" or "P12" or "P13")
                                {
                                    var expectedRotation = fixture.Id == "P11" ? 90 : fixture.Id == "P12" ? 180 : 270;
                                    var actualRotation = pdf.GetPage(1).Rotation.Value;
                                    if (actualRotation != expectedRotation)
                                        throw new InvalidDataException($"Rotation {actualRotation}, expected {expectedRotation}.");
                                    details.Add($"rotation={actualRotation}");
                                }
                                var extracted = string.Concat(pdf.GetPages().Take(8).Select(page => page.Text));
                                var hasText = extracted.Any(char.IsLetterOrDigit);
                                if (fixture.ExpectedTextLayer == true && !hasText)
                                    throw new InvalidDataException("Thiếu text layer mong đợi.");
                                if (fixture.ExpectedTextLayer == false && hasText)
                                    throw new InvalidDataException("Scan dự kiến image-only nhưng có text layer.");
                                details.Add($"textLayer={hasText.ToString().ToLowerInvariant()}");
                            }
                            break;
                    }
                }
                else
                {
                    details.Add("intentional-invalid=true");
                    if (fixture.RelativePath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ||
                        fixture.RelativePath.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            using var archive = ZipFile.OpenRead(path);
                            details.Add($"zipEntries={archive.Entries.Count}");
                        }
                        catch (InvalidDataException)
                        {
                            details.Add("zip=false");
                        }
                    }
                }

                var qaRoot = Path.Combine(Path.GetTempPath(), "CafeChain-AIImport-Fixture-QA", fixture.Id);
                if (Directory.Exists(qaRoot))
                    details.Add($"qaPng={Directory.GetFiles(qaRoot, "*.png").Length}");
                results.Add(new VerificationRecord(fixture.Id, fixture.RelativePath, true, info.Length, string.Join("; ", details)));
            }
            catch (Exception exception)
            {
                var size = File.Exists(path) ? new FileInfo(path).Length : 0;
                results.Add(new VerificationRecord(fixture.Id, fixture.RelativePath, false, size, exception.Message));
            }
        }
        return results;
    }
}
