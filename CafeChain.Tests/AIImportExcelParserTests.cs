using System.IO.Compression;
using CafeChain.Application.Options;
using CafeChain.Application.Services.AIImport;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Extensions.Options;
using CafeChain.Application.Interfaces.AI;
using CafeChain.Models.AIImport;
using Moq;

namespace CafeChain.Tests;

public sealed class AIImportExcelParserTests
{
    [Fact]
    public async Task Parse_reads_shared_inline_numeric_boolean_and_cached_formula_values()
    {
        await using var stream = Workbook((workbook, worksheetPart) =>
        {
            var sharedPart = workbook.AddNewPart<SharedStringTablePart>();
            sharedPart.SharedStringTable = new SharedStringTable(new SharedStringItem(new Text("CategoryCode")));
            worksheetPart.Worksheet = new Worksheet(new SheetData(
                Row(1,
                    Cell("A1", "0", CellValues.SharedString),
                    InlineCell("B1", "Name"),
                    InlineCell("C1", "Active"),
                    InlineCell("D1", "Formula")),
                Row(2,
                    InlineCell("A2", "CF01"),
                    InlineCell("B2", "Cà phê"),
                    Cell("C2", "1", CellValues.Boolean),
                    FormulaCell("D2", "1+1", "2"))));
        });

        var result = await Parser().ParseAsync(stream, default);

        Assert.Empty(result.Errors);
        var region = Assert.Single(result.Regions);
        Assert.Equal("CategoryCode", region.Cells[(1, 1)]);
        Assert.Equal("Name", region.Cells[(1, 2)]);
        Assert.Equal("true", region.Cells[(2, 3)]);
        Assert.Equal("2", region.Cells[(2, 4)]);
    }

    [Fact]
    public async Task Parse_skips_hidden_sheet_row_and_column_and_emits_warning()
    {
        await using var stream = Workbook((_, worksheetPart) =>
        {
            worksheetPart.Worksheet = new Worksheet(
                new Columns(new Column { Min = 4, Max = 4, Hidden = true }),
                new SheetData(
                    Row(1, InlineCell("A1", "Code"), InlineCell("B1", "Name"), InlineCell("D1", "Secret")),
                    Row(2, InlineCell("A2", "A"), InlineCell("B2", "Visible")),
                    new Row(InlineCell("A3", "B"), InlineCell("B3", "Hidden")) { RowIndex = 3, Hidden = true }));
        }, addHiddenSheet: true);

        var result = await Parser().ParseAsync(stream, default);

        Assert.Empty(result.Errors);
        Assert.Contains(result.Warnings, x => x.Code == "DỮ_LIỆU_ẨN");
        Assert.DoesNotContain(result.Regions.SelectMany(x => x.Cells.Values), x => x == "Secret" || x == "Hidden");
    }

    [Fact]
    public async Task Parse_propagates_safe_vertical_merge_but_marks_ambiguous_horizontal_merge()
    {
        await using var stream = Workbook((_, worksheetPart) =>
        {
            worksheetPart.Worksheet = new Worksheet(
                new SheetData(
                    Row(1, InlineCell("A1", "Category"), InlineCell("B1", "Name")),
                    Row(2, InlineCell("A2", "Coffee"), InlineCell("B2", "Latte")),
                    Row(3, InlineCell("B3", "Cappuccino"))),
                new MergeCells(new MergeCell { Reference = "A2:A3" }, new MergeCell { Reference = "A1:B1" }));
        });

        var result = await Parser().ParseAsync(stream, default);

        var region = Assert.Single(result.Regions);
        Assert.Equal("Coffee", region.Cells[(3, 1)]);
        Assert.Contains(result.Warnings, x => x.Code == "Ô_GỘP_KHÔNG_HỢP_LỆ");
    }

    [Fact]
    public async Task Parse_rejects_non_openxml_and_suspicious_compression_ratio()
    {
        await using var fake = new MemoryStream("not an xlsx"u8.ToArray());
        var invalid = await Parser().ParseAsync(fake, default);
        Assert.Contains(invalid.Errors, x => x.Code == "FILE_BỊ_HỎNG");

        await using var bomb = new MemoryStream();
        using (var archive = new ZipArchive(bomb, ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry("xl/huge.xml", CompressionLevel.SmallestSize);
            await using var writer = entry.Open();
            await writer.WriteAsync(new byte[200_000]);
        }
        bomb.Position = 0;
        var suspicious = await Parser(maxCompressionRatio: 2).ParseAsync(bomb, default);
        Assert.Contains(suspicious.Errors, x => x.Code == "FILE_QUÁ_LỚN");
    }

    [Fact]
    public async Task Parse_detects_multiple_regions_and_sheets()
    {
        await using var stream = Workbook((workbook, worksheetPart) =>
        {
            worksheetPart.Worksheet = new Worksheet(new SheetData(
                Row(1, InlineCell("A1", "Code"), InlineCell("B1", "Name"), InlineCell("E1", "Code"), InlineCell("F1", "Name")),
                Row(2, InlineCell("A2", "A"), InlineCell("B2", "One"), InlineCell("E2", "B"), InlineCell("F2", "Two"))));
            AddSheet(workbook, "Second", new Worksheet(new SheetData(
                Row(1, InlineCell("A1", "Code"), InlineCell("B1", "Name")),
                Row(2, InlineCell("A2", "C"), InlineCell("B2", "Three")))));
        });

        var result = await Parser().ParseAsync(stream, default);

        Assert.Empty(result.Errors);
        Assert.Equal(3, result.Regions.Count);
        Assert.Equal(2, result.Regions.Select(x => x.SheetName).Distinct().Count());
    }

    [Fact]
    public async Task Parse_splits_two_vertical_tables_even_when_a_title_connects_their_cells()
    {
        await using var stream = Workbook((_, worksheetPart) =>
        {
            worksheetPart.Worksheet = new Worksheet(new SheetData(
                Row(1, InlineCell("A1", "CategoryCode"), InlineCell("B1", "Name")),
                Row(2, InlineCell("A2", "CAT01"), InlineCell("B2", "Cà phê")),
                Row(3, InlineCell("A3", "Danh sách đồ uống")),
                Row(4, InlineCell("A4", "DrinkCode"), InlineCell("B4", "Name"),
                    InlineCell("C4", "Category"), InlineCell("D4", "ProductType")),
                Row(5, InlineCell("A5", "DR01"), InlineCell("B5", "Đen đá"),
                    InlineCell("C5", "CAT01"), InlineCell("D5", "DRINK"))));
        });

        var result = await Parser().ParseAsync(stream, default);

        Assert.Empty(result.Errors);
        Assert.Equal(2, result.Regions.Count);
        Assert.Equal(new[] { 1, 4 }, result.Regions.Select(region => region.MinRow));
    }

    [Fact]
    public async Task Duplicate_header_issue_exposes_target_field_and_position_stable_source_keys()
    {
        await using var stream = Workbook((_, worksheetPart) =>
        {
            worksheetPart.Worksheet = new Worksheet(new SheetData(
                Row(1, InlineCell("A1", "CategoryCode"), InlineCell("B1", "Name"), InlineCell("C1", "Name")),
                Row(2, InlineCell("A2", "CAT01"), InlineCell("B2", "Tên A"), InlineCell("C2", "Tên B"))));
        });
        var schemas = new AIImportSchemaRegistry();
        var ollama = new Mock<IOllamaClient>(MockBehavior.Strict);
        var analyzer = new AIImportRegionAnalyzer(schemas, ollama.Object, Options.Create(new AIImportOptions()));
        var parser = new AIImportExcelSourceParser(Parser(), analyzer, schemas);

        var result = await parser.ParseAsync(new AIImportSourceFile("duplicate.xlsx", stream.ToArray()), AIImportEntityType.Category, default);

        var group = Assert.Single(result.Groups);
        Assert.Contains(group.SourceColumns, column => column.Key == "Name [B]");
        Assert.Contains(group.SourceColumns, column => column.Key == "Name [C]");
        var issue = Assert.Single(group.Issues.Where(issue => issue.Code == "XUNG_ĐỘT_ÁNH_XẠ"));
        Assert.Equal("Name", issue.Field);
        Assert.Equal("Name", issue.Metadata["targetField"]);
        Assert.Equal(new[] { "Name [B]", "Name [C]" }, Assert.IsType<string[]>(issue.Metadata["candidateSourceKeys"]));
    }

    private static AIImportExcelParser Parser(decimal maxCompressionRatio = 100) => new(Options.Create(new AIImportOptions
    {
        MaxCompressionRatio = maxCompressionRatio
    }));

    private static MemoryStream Workbook(Action<WorkbookPart, WorksheetPart> configure, bool addHiddenSheet = false)
    {
        var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, true))
        {
            var workbook = document.AddWorkbookPart();
            workbook.Workbook = new Workbook(new Sheets());
            var worksheet = workbook.AddNewPart<WorksheetPart>();
            configure(workbook, worksheet);
            worksheet.Worksheet!.Save();
            workbook.Workbook.Sheets!.Append(new Sheet { Id = workbook.GetIdOfPart(worksheet), SheetId = 1, Name = "Main" });
            if (addHiddenSheet)
            {
                var hidden = workbook.AddNewPart<WorksheetPart>();
                hidden.Worksheet = new Worksheet(new SheetData(Row(1, InlineCell("A1", "Hidden"), InlineCell("B1", "Data"))));
                workbook.Workbook.Sheets.Append(new Sheet { Id = workbook.GetIdOfPart(hidden), SheetId = 2, Name = "Hidden", State = SheetStateValues.Hidden });
            }
            workbook.Workbook.Save();
        }
        stream.Position = 0;
        return stream;
    }

    private static void AddSheet(WorkbookPart workbook, string name, Worksheet worksheet)
    {
        var part = workbook.AddNewPart<WorksheetPart>(); part.Worksheet = worksheet; part.Worksheet.Save();
        var sheets = workbook.Workbook!.Sheets!;
        var id = (uint)(sheets.Count() + 1);
        sheets.Append(new Sheet { Id = workbook.GetIdOfPart(part), SheetId = id, Name = name });
    }

    private static Row Row(uint index, params Cell[] cells) => new(cells) { RowIndex = index };
    private static Cell Cell(string reference, string value, CellValues type) => new() { CellReference = reference, DataType = type, CellValue = new CellValue(value) };
    private static Cell InlineCell(string reference, string value) => new() { CellReference = reference, DataType = CellValues.InlineString, InlineString = new InlineString(new Text(value)) };
    private static Cell FormulaCell(string reference, string formula, string cached) => new() { CellReference = reference, CellFormula = new CellFormula(formula), CellValue = new CellValue(cached) };
}
