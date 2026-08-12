using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using CafeChain.Application.DTOs.AIImport;
using CafeChain.Application.Options;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Extensions.Options;

namespace CafeChain.Application.Services.AIImport;

public sealed record AIImportCell(int Row, int Column, string? Value, string ColumnName);

public sealed class AIImportRegionData
{
    public string SheetName { get; init; } = string.Empty;
    public int MinRow { get; init; }
    public int MaxRow { get; init; }
    public int MinColumn { get; init; }
    public int MaxColumn { get; init; }
    public string Address => $"{ColumnName(MinColumn)}{MinRow}:{ColumnName(MaxColumn)}{MaxRow}";
    public Dictionary<(int Row, int Column), string?> Cells { get; init; } = new();

    public Dictionary<string, string?> ReadRow(int row) => Enumerable.Range(MinColumn, MaxColumn - MinColumn + 1)
        .ToDictionary(ColumnName, column => Cells.GetValueOrDefault((row, column)), StringComparer.OrdinalIgnoreCase);

    public static string ColumnName(int column)
    {
        var result = string.Empty;
        while (column > 0)
        {
            column--;
            result = (char)('A' + column % 26) + result;
            column /= 26;
        }
        return result;
    }
}

public sealed class AIImportWorkbookData
{
    public List<AIImportRegionData> Regions { get; } = new();
    public List<AIImportErrorDto> Warnings { get; } = new();
    public List<AIImportErrorDto> Errors { get; } = new();
}

public interface IAIImportExcelParser
{
    Task<AIImportWorkbookData> ParseAsync(Stream stream, CancellationToken cancellationToken);
}

public sealed partial class AIImportExcelParser : IAIImportExcelParser
{
    private readonly AIImportOptions _options;

    public AIImportExcelParser(IOptions<AIImportOptions> options) => _options = options.Value;

    public async Task<AIImportWorkbookData> ParseAsync(Stream stream, CancellationToken cancellationToken)
    {
        var result = new AIImportWorkbookData();
        await using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        ValidatePackage(buffer, result);
        if (result.Errors.Count > 0) return result;

        buffer.Position = 0;
        try
        {
            using var document = SpreadsheetDocument.Open(buffer, false);
            var workbookPart = document.WorkbookPart;
            var workbook = workbookPart?.Workbook;
            var sheets = workbook?.Sheets?.Elements<Sheet>().ToList() ?? new List<Sheet>();
            if (sheets.Count == 0)
            {
                result.Errors.Add(Error("KHÔNG_TÌM_THẤY_DỮ_LIỆU", "Tệp Excel không có trang tính đọc được."));
                return result;
            }
            if (sheets.Count > _options.MaxSheets)
            {
                result.Errors.Add(Error("DỮ_LIỆU_VƯỢT_GIỚI_HẠN_MVP", $"Tệp có quá {_options.MaxSheets} trang tính."));
                return result;
            }

            var sharedStrings = workbookPart?.SharedStringTablePart?.SharedStringTable;
            var styles = workbookPart?.WorkbookStylesPart?.Stylesheet;
            var totalRows = 0;
            var totalCells = 0;
            foreach (var sheet in sheets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sheetName = sheet.Name?.Value ?? "Sheet";
                if (sheet.State?.Value == SheetStateValues.Hidden
                    || sheet.State?.Value == SheetStateValues.VeryHidden)
                {
                    result.Warnings.Add(Error("DỮ_LIỆU_ẨN", $"Trang tính \"{sheetName}\" đang ẩn nên không được nhập.", sheetName));
                    continue;
                }
                if (sheet.Id?.Value == null || workbookPart?.GetPartById(sheet.Id.Value) is not WorksheetPart worksheetPart)
                    continue;
                if (worksheetPart.Worksheet is not { } worksheet) continue;

                var hiddenColumns = ReadHiddenColumns(worksheet);
                var cells = new Dictionary<(int Row, int Column), string?>();
                var hiddenData = false;
                var maxColumn = 0;
                foreach (var row in worksheet.Descendants<Row>())
                {
                    var rowIndex = checked((int)(row.RowIndex?.Value ?? 0));
                    if (rowIndex <= 0) continue;
                    if (row.Hidden?.Value == true)
                    {
                        hiddenData |= row.Elements<Cell>().Any(x => !string.IsNullOrWhiteSpace(x.InnerText));
                        continue;
                    }
                    if (rowIndex > _options.MaxRowsPerSheet)
                    {
                        result.Errors.Add(Error("DỮ_LIỆU_VƯỢT_GIỚI_HẠN_MVP", $"Trang tính \"{sheetName}\" vượt quá {_options.MaxRowsPerSheet} dòng.", sheetName));
                        return result;
                    }
                    foreach (var cell in row.Elements<Cell>())
                    {
                        var reference = cell.CellReference?.Value;
                        if (!TryParseReference(reference, out var parsedRow, out var column)) continue;
                        if (hiddenColumns.Contains(column))
                        {
                            hiddenData |= !string.IsNullOrWhiteSpace(cell.InnerText);
                            continue;
                        }
                        if (column > _options.MaxColumnsPerSheet)
                        {
                            result.Errors.Add(Error("DỮ_LIỆU_VƯỢT_GIỚI_HẠN_MVP", $"Trang tính \"{sheetName}\" vượt quá {_options.MaxColumnsPerSheet} cột.", sheetName));
                            return result;
                        }
                        var value = ReadCellValue(cell, sharedStrings, styles, out var formulaWithoutCache);
                        if (formulaWithoutCache)
                        {
                            result.Warnings.Add(Error("KHÔNG_ĐỌC_ĐƯỢC_CÔNG_THỨC", $"Không đọc được giá trị công thức tại {reference}.", sheetName, parsedRow, AIImportRegionData.ColumnName(column)));
                        }
                        if (!string.IsNullOrWhiteSpace(value)) cells[(parsedRow, column)] = value;
                        maxColumn = Math.Max(maxColumn, column);
                        totalCells++;
                        if (totalCells > _options.MaxTotalCells)
                        {
                            result.Errors.Add(Error("DỮ_LIỆU_VƯỢT_GIỚI_HẠN_MVP", $"Tệp vượt quá {_options.MaxTotalCells} ô dữ liệu."));
                            return result;
                        }
                    }
                }

                totalRows += cells.Keys.Select(x => x.Row).Distinct().Count();
                if (totalRows > _options.MaxTotalRows)
                {
                    result.Errors.Add(Error("DỮ_LIỆU_VƯỢT_GIỚI_HẠN_MVP", $"Tệp vượt quá {_options.MaxTotalRows} dòng dữ liệu."));
                    return result;
                }
                if (hiddenData)
                    result.Warnings.Add(Error("DỮ_LIỆU_ẨN", $"Trang tính \"{sheetName}\" có dòng hoặc cột ẩn; dữ liệu ẩn đã được bỏ qua.", sheetName));

                ApplySafeMergedCells(worksheet, cells, result, sheetName);
                var regions = DetectRegions(sheetName, cells);
                if (regions.Count > _options.MaxRegionsPerSheet)
                {
                    result.Errors.Add(Error("DỮ_LIỆU_VƯỢT_GIỚI_HẠN_MVP", $"Trang tính \"{sheetName}\" có quá {_options.MaxRegionsPerSheet} vùng dữ liệu.", sheetName));
                    return result;
                }
                result.Regions.AddRange(regions);
            }
        }
        catch (OpenXmlPackageException)
        {
            result.Errors.Add(Error("FILE_BỊ_HỎNG", "Không thể đọc tệp Excel. Tệp có thể bị hỏng hoặc được bảo vệ bằng mật khẩu."));
        }
        catch (InvalidDataException)
        {
            result.Errors.Add(Error("FILE_BỊ_HỎNG", "Không thể đọc cấu trúc nén của tệp Excel."));
        }

        if (result.Errors.Count == 0 && result.Regions.Count == 0)
            result.Errors.Add(Error("KHÔNG_TÌM_THẤY_DỮ_LIỆU", "Không tìm thấy vùng dữ liệu hợp lệ trong tệp Excel."));
        return result;
    }

    private void ValidatePackage(MemoryStream buffer, AIImportWorkbookData result)
    {
        try
        {
            buffer.Position = 0;
            using var archive = new ZipArchive(buffer, ZipArchiveMode.Read, true);
            long expanded = 0;
            foreach (var entry in archive.Entries)
            {
                expanded += entry.Length;
                if (expanded > _options.MaxExpandedBytes)
                {
                    result.Errors.Add(Error("FILE_QUÁ_LỚN", "Dung lượng dữ liệu sau giải nén vượt giới hạn cho phép."));
                    return;
                }
                if (entry.CompressedLength > 0 && entry.Length / (decimal)entry.CompressedLength > _options.MaxCompressionRatio)
                {
                    result.Errors.Add(Error("FILE_QUÁ_LỚN", "Tệp Excel có tỷ lệ nén không an toàn."));
                    return;
                }
            }
        }
        catch (InvalidDataException)
        {
            result.Errors.Add(Error("FILE_BỊ_HỎNG", "Tệp không phải gói OpenXML .xlsx hợp lệ."));
        }
        finally
        {
            buffer.Position = 0;
        }
    }

    private static HashSet<int> ReadHiddenColumns(Worksheet worksheet)
    {
        var result = new HashSet<int>();
        foreach (var column in worksheet.Descendants<Column>().Where(x => x.Hidden?.Value == true))
        {
            var min = checked((int)(column.Min?.Value ?? 0));
            var max = checked((int)(column.Max?.Value ?? 0));
            for (var value = min; value <= max && value <= 16_384; value++) result.Add(value);
        }
        return result;
    }

    private static string? ReadCellValue(Cell cell, SharedStringTable? sharedStrings, Stylesheet? styles, out bool formulaWithoutCache)
    {
        formulaWithoutCache = cell.CellFormula != null && cell.CellValue == null && cell.InlineString == null;
        if (cell.DataType?.Value == CellValues.SharedString
            && int.TryParse(cell.CellValue?.InnerText, out var index))
            return sharedStrings?.Elements<SharedStringItem>().ElementAtOrDefault(index)?.InnerText?.Trim();
        if (cell.DataType?.Value == CellValues.InlineString) return cell.InlineString?.InnerText?.Trim();
        if (cell.DataType?.Value == CellValues.Boolean) return cell.CellValue?.InnerText == "1" ? "true" : "false";
        var text = cell.CellValue?.InnerText ?? cell.InnerText;
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (cell.StyleIndex?.Value is uint styleIndex && IsDateStyle(styles, styleIndex)
            && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial))
        {
            try { return DateTime.FromOADate(serial).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture); }
            catch (ArgumentException) { return text.Trim(); }
        }
        return text.Trim();
    }

    private static bool IsDateStyle(Stylesheet? styles, uint styleIndex)
    {
        var formatId = styles?.CellFormats?.Elements<CellFormat>().ElementAtOrDefault((int)styleIndex)?.NumberFormatId?.Value;
        if (formatId is >= 14 and <= 22 or 45 or 46 or 47) return true;
        var code = styles?.NumberingFormats?.Elements<NumberingFormat>()
            .FirstOrDefault(x => x.NumberFormatId?.Value == formatId)?.FormatCode?.Value;
        return code != null && DateFormatRegex().IsMatch(code);
    }

    private static void ApplySafeMergedCells(Worksheet worksheet, Dictionary<(int Row, int Column), string?> cells,
        AIImportWorkbookData result, string sheetName)
    {
        foreach (var merged in worksheet.Descendants<MergeCell>())
        {
            var parts = merged.Reference?.Value?.Split(':');
            if (parts?.Length != 2 || !TryParseReference(parts[0], out var startRow, out var startColumn)
                || !TryParseReference(parts[1], out var endRow, out var endColumn)) continue;
            if (startColumn == endColumn && cells.TryGetValue((startRow, startColumn), out var value)
                && !string.IsNullOrWhiteSpace(value))
            {
                for (var row = startRow + 1; row <= endRow; row++)
                    cells.TryAdd((row, startColumn), value);
            }
            else
            {
                result.Warnings.Add(Error("Ô_GỘP_KHÔNG_HỢP_LỆ", $"Vùng ô gộp {merged.Reference?.Value} cần được xem lại.", sheetName, startRow, AIImportRegionData.ColumnName(startColumn)));
            }
        }
    }

    private static List<AIImportRegionData> DetectRegions(string sheetName, Dictionary<(int Row, int Column), string?> cells)
    {
        var remaining = cells.Keys.ToHashSet();
        var result = new List<AIImportRegionData>();
        while (remaining.Count > 0)
        {
            var seed = remaining.First();
            var queue = new Queue<(int Row, int Column)>();
            var component = new HashSet<(int Row, int Column)>();
            queue.Enqueue(seed);
            remaining.Remove(seed);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                component.Add(current);
                foreach (var next in new[]
                {
                    (current.Row - 1, current.Column), (current.Row + 1, current.Column),
                    (current.Row, current.Column - 1), (current.Row, current.Column + 1)
                })
                {
                    if (remaining.Remove(next)) queue.Enqueue(next);
                }
            }
            var minRow = component.Min(x => x.Row);
            var maxRow = component.Max(x => x.Row);
            var minColumn = component.Min(x => x.Column);
            var maxColumn = component.Max(x => x.Column);
            if (maxRow <= minRow || maxColumn <= minColumn) continue;
            result.Add(new AIImportRegionData
            {
                SheetName = sheetName,
                MinRow = minRow,
                MaxRow = maxRow,
                MinColumn = minColumn,
                MaxColumn = maxColumn,
                Cells = cells.Where(x => x.Key.Row >= minRow && x.Key.Row <= maxRow
                                         && x.Key.Column >= minColumn && x.Key.Column <= maxColumn)
                    .ToDictionary(x => x.Key, x => x.Value)
            });
        }
        return result.OrderBy(x => x.MinRow).ThenBy(x => x.MinColumn).ToList();
    }

    private static bool TryParseReference(string? reference, out int row, out int column)
    {
        row = 0;
        column = 0;
        if (string.IsNullOrWhiteSpace(reference)) return false;
        var match = CellReferenceRegex().Match(reference.Replace("$", string.Empty, StringComparison.Ordinal));
        if (!match.Success || !int.TryParse(match.Groups[2].Value, out row)) return false;
        foreach (var character in match.Groups[1].Value.ToUpperInvariant())
            column = checked(column * 26 + character - 'A' + 1);
        return column > 0;
    }

    private static AIImportErrorDto Error(string code, string message, string? sheet = null, int? row = null, string? column = null) => new()
    {
        Code = code,
        Message = message,
        Position = sheet == null ? null : new AIImportPositionDto { Sheet = sheet, Row = row, Column = column }
    };

    [GeneratedRegex("^([A-Za-z]+)([0-9]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex CellReferenceRegex();

    [GeneratedRegex("[dmyhs]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DateFormatRegex();
}
