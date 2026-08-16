using System.IO.Compression;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

internal static class ExcelFixtures
{
    private sealed record Block(int Row, int Column, object?[][] Values, bool Header = true);
    private sealed record SheetSpec(
        string Name,
        List<Block> Blocks,
        bool Hidden = false,
        HashSet<int>? HiddenRows = null,
        HashSet<int>? HiddenColumns = null,
        List<string>? Merges = null,
        Action<WorksheetPart>? Customize = null);

    private sealed record FormulaValue(string Formula, string? CachedValue);
    private sealed record SharedValue(string Value);

    public static void Generate(FixtureCatalog catalog)
    {
        var dir = "01_EXCEL";
        Basic(catalog, "E01", $"{dir}/E01_category_valid_basic.xlsx", "Category", CategoryRows("CAT_COFFEE", "Cà phê"));
        Basic(catalog, "E02", $"{dir}/E02_drink_valid_basic.xlsx", "Drink", DrinkRows("DR_AMERICANO", "Americano", "CAT_COFFEE"));
        Basic(catalog, "E03", $"{dir}/E03_size_valid_basic.xlsx", "Size", SizeRows("SIZE_M", "Vừa", "Cup"));
        Basic(catalog, "E04", $"{dir}/E04_ingredient_valid_basic.xlsx", "Ingredient", IngredientRows("ING_BEAN", "Hạt cà phê", "GRAM"));
        Basic(catalog, "E05", $"{dir}/E05_supplier_valid_basic.xlsx", "Supplier", SupplierRows("Công ty Cà phê Sạch", "0312345679"));

        AddWorkbook(catalog, Record("E06", $"{dir}/E06_all_entities_separate_sheets.xlsx", "MULTI_SHEET", "Năm entity trên năm sheet", null,
            "5 groups CREATE; Category phải đứng trước Drink."),
            Sheet("Danh mục", CategoryRows("CAT_TEA", "Trà")),
            Sheet("Đồ uống", DrinkRows("DR_TEA", "Trà nóng", "CAT_TEA")),
            Sheet("Kích cỡ", SizeRows("SIZE_L", "Lớn", "Volume")),
            Sheet("Nguyên liệu", IngredientRows("ING_TEA", "Lá trà", "GRAM")),
            Sheet("Nhà cung cấp", SupplierRows("NCC Trà Việt", "0312345680")));

        AddWorkbook(catalog, Record("E07", $"{dir}/E07_multiple_sheets_same_entity.xlsx", "MULTI_SHEET", "Cùng entity ở nhiều sheet", "Category", "2 Category groups."),
            Sheet("Danh mục 1", CategoryRows("CAT_COFFEE_1", "Cà phê pha máy")),
            Sheet("Danh mục 2", CategoryRows("CAT_TEA_2", "Trà trái cây")));

        AddWorkbook(catalog, Record("E08", $"{dir}/E08_two_regions_horizontal.xlsx", "REGION", "Hai bảng ngang cách nhau bằng cột trống", "Category", "2 stable SourceRegionId."),
            new SheetSpec("Danh mục", [
                new Block(1, 1, CategoryRows("CAT_H1", "Nóng")),
                new Block(1, 5, CategoryRows("CAT_H2", "Lạnh"))]));

        AddWorkbook(catalog, Record("E09", $"{dir}/E09_two_regions_vertical.xlsx", "REGION", "Hai bảng dọc có tiêu đề ngăn cách", null, "2 regions, Category + Drink."),
            new SheetSpec("Dữ liệu", [
                new Block(1, 1, CategoryRows("CAT_V", "Cà phê")),
                new Block(4, 1, new object?[][] { ["Danh sách đồ uống"], [null] }, false),
                new Block(6, 1, DrinkRows("DR_V", "Đen đá", "CAT_V"))]));

        AddWorkbook(catalog, Record("E10", $"{dir}/E10_same_region_category_drink_projection.xlsx", "MULTI_ENTITY", "Category và Drink dùng chung một bảng", null,
            "Chỉ projection có đủ required fields mới sinh candidate."),
            Sheet("Danh mục và đồ uống", [
                ["CategoryCode", "Tên danh mục", "DrinkCode", "Tên đồ uống", "Category", "ProductType"],
                ["CAT_MIX", "Đồ uống lạnh", "DR_MIX", "Cold Brew", "CAT_MIX", "DRINK"]]));

        AddWorkbook(catalog, Record("E11", $"{dir}/E11_sparse_mixed_rows.xlsx", "MULTI_ENTITY", "Sparse row được phân loại theo từng dòng", null,
            "Dòng Category và Drink không bị ép cùng entity."),
            Sheet("Dữ liệu thưa", [
                ["CategoryCode", "Tên danh mục", "DrinkCode", "Tên đồ uống", "Category", "ProductType"],
                ["CAT_SPARSE", "Trà", null, null, null, null],
                [null, null, "DR_SPARSE", "Trà đào", "CAT_SPARSE", "DRINK"]]));

        AddWorkbook(catalog, Record("E12", $"{dir}/E12_metadata_before_table.xlsx", "LAYOUT", "Metadata và title trước bảng", "Category", "Bảng bắt đầu tại hàng 5."),
            new SheetSpec("Danh mục", [
                new Block(1, 1, [["BÁO CÁO DANH MỤC"], ["Ngày lập"], ["2026-08-15"]], false),
                new Block(5, 1, CategoryRows("CAT_META", "Danh mục có metadata"))]));

        AddWorkbook(catalog, Record("E13", $"{dir}/E13_footer_after_table.xlsx", "LAYOUT", "Footer sau bảng", "Category", "Footer không thành candidate."),
            new SheetSpec("Danh mục", [
                new Block(1, 1, CategoryRows("CAT_FOOT", "Danh mục footer")),
                new Block(5, 1, [["Người lập: QA"], ["Hết dữ liệu"]], false)]));

        AddWorkbook(catalog, Record("E14", $"{dir}/E14_blank_rows_and_columns.xlsx", "LAYOUT", "Nhiều hàng/cột trắng trong và quanh dữ liệu", "Category", "Không sinh row rỗng."),
            new SheetSpec("Danh mục", [new Block(3, 3, [
                ["CategoryCode", null, "Name", "Icon", "Active"],
                ["CAT_BLANK", null, "Danh mục khoảng trắng", "☕", true],
                [null, null, null, null, null],
                ["CAT_BLANK_2", null, "Dòng sau khoảng trắng", "🍵", true]])]));

        AddWorkbook(catalog, Record("E15", $"{dir}/E15_known_ignored_columns.xlsx", "COLUMN", "Cột metadata được biết", "Category", "TC_ID/ExpectedCode/SeedDependency = IGNORED."),
            Sheet("Danh mục", [
                ["CategoryCode", "Name", "TC_ID", "ExpectedCode", "SeedDependency"],
                ["CAT_IGN", "Danh mục ignored", "TC-15", "VALID", "none"]]));

        AddWorkbook(catalog, Record("E16", $"{dir}/E16_unknown_extra_columns.xlsx", "COLUMN", "Cột dư chưa biết", "Category", "Cột Ghi chú lạ = UNKNOWN/manual mapping."),
            Sheet("Danh mục", [
                ["CategoryCode", "Name", "Ghi chú lạ", "Màu nội bộ"],
                ["CAT_UNKNOWN", "Danh mục cột dư", "không thuộc schema", "xanh"]]));

        AddWorkbook(catalog, Record("E17", $"{dir}/E17_forbidden_store_id.xlsx", "COLUMN", "Cột StoreId bị cấm", "Category", "FORBIDDEN; không confirm."),
            Sheet("Danh mục", [["CategoryCode", "Name", "StoreId"], ["CAT_STORE", "Sai phạm vi", 1]]));

        AddWorkbook(catalog, Record("E18", $"{dir}/E18_forbidden_sql_command.xlsx", "COLUMN", "Cột SQL/Command bị cấm", "Category", "FORBIDDEN; không thực thi nội dung."),
            Sheet("Danh mục", [["CategoryCode", "Name", "SQL", "Command"], ["CAT_SQL", "Không an toàn", "DROP TABLE", "DELETE"]]));

        AddWorkbook(catalog, Record("E19", $"{dir}/E19_duplicate_header.xlsx", "HEADER", "Hai cột Name trùng nhau", "Category", "Name [B]/Name [C], mapping Name = null."),
            Sheet("Danh mục", [["CategoryCode", "Name", "Name"], ["CAT_DUP_HEAD", "Tên A", "Tên B"]]));

        AddWorkbook(catalog, Record("E20", $"{dir}/E20_repeated_header_mid_data.xlsx", "HEADER", "Header lặp giữa dữ liệu", "Category", "Header lặp không thành business row."),
            Sheet("Danh mục", [
                ["CategoryCode", "Name", "Icon", "Active"],
                ["CAT_RH1", "Một", "☕", true],
                ["CategoryCode", "Name", "Icon", "Active"],
                ["CAT_RH2", "Hai", "🍵", true]]));

        AddWorkbook(catalog, Record("E21", $"{dir}/E21_vertical_merge_safe.xlsx", "MERGE", "Merge dọc an toàn", "Category", "Giá trị A2 truyền xuống A3; vẫn cần kiểm tra ownership."),
            new SheetSpec("Danh mục", [new Block(1, 1, [
                ["CategoryCode", "Name"], ["CAT_VM", "Tên thứ nhất"], [null, "Tên thứ hai"]])],
                Merges: ["A2:A3"]));

        AddWorkbook(catalog, Record("E22", $"{dir}/E22_horizontal_merge_ambiguous.xlsx", "MERGE", "Merge ngang header mơ hồ", "Category", "Warning Ô_GỘP_KHÔNG_HỢP_LỆ."),
            new SheetSpec("Danh mục", [new Block(1, 1, [["CategoryCode", null], ["CAT_HM", "Tên bị mơ hồ"]])],
                Merges: ["A1:B1"]));

        AddWorkbook(catalog, Record("E23", $"{dir}/E23_hidden_sheet.xlsx", "HIDDEN", "Sheet ẩn chứa dữ liệu", "Category", "Warning DỮ_LIỆU_ẨN; sheet ẩn bị bỏ qua."),
            Sheet("Hiển thị", CategoryRows("CAT_VISIBLE", "Hiển thị")),
            Sheet("Ẩn", CategoryRows("CAT_HIDDEN", "Không được đọc"), hidden: true));

        AddWorkbook(catalog, Record("E24", $"{dir}/E24_hidden_row.xlsx", "HIDDEN", "Dòng dữ liệu ẩn", "Category", "Warning DỮ_LIỆU_ẨN; CAT_HIDDEN_ROW không xuất hiện."),
            new SheetSpec("Danh mục", [new Block(1, 1, [
                ["CategoryCode", "Name"], ["CAT_VISIBLE_ROW", "Hiển thị"], ["CAT_HIDDEN_ROW", "Ẩn"]])], HiddenRows: [3]));

        AddWorkbook(catalog, Record("E25", $"{dir}/E25_hidden_column.xlsx", "HIDDEN", "Cột ẩn", "Category", "Warning DỮ_LIỆU_ẨN; Secret không xuất hiện."),
            new SheetSpec("Danh mục", [new Block(1, 1, [
                ["CategoryCode", "Name", "Secret"], ["CAT_HIDDEN_COL", "Hiển thị", "Không đọc"]])], HiddenColumns: [3]));

        AddWorkbook(catalog, Record("E26", $"{dir}/E26_cell_types_and_cached_formula.xlsx", "CELL_TYPE", "Shared/inline/numeric/boolean/formula cached", "Category", "Đọc đúng true và cached formula 2."),
            new SheetSpec("Danh mục", [new Block(1, 1, [
                [new SharedValue("CategoryCode"), "Name", "Active", "Formula"],
                ["CAT_TYPES", "Kiểu ô", true, new FormulaValue("1+1", "2")]])]));

        AddWorkbook(catalog, Record("E27", $"{dir}/E27_formula_without_cached_value.xlsx", "CELL_TYPE", "Formula không có cached value", "Category", "Không suy đoán công thức; cell rỗng hoặc source issue."),
            Sheet("Danh mục", [["CategoryCode", "Name", "Active"], ["CAT_NO_CACHE", "Formula chưa tính", new FormulaValue("TRUE()", null)]]));

        AddWorkbook(catalog, Record("E28", $"{dir}/E28_duplicate_same_payload.xlsx", "DUPLICATE", "Trùng business key cùng payload", "Category", "Duplicate cohort; không tạo hai bản ghi."),
            Sheet("Danh mục", [
                ["CategoryCode", "Name"], ["CAT_DUP", "Cùng dữ liệu"], ["CAT_DUP", "Cùng dữ liệu"]]));

        AddWorkbook(catalog, Record("E29", $"{dir}/E29_duplicate_conflicting_payload.xlsx", "DUPLICATE", "Trùng key khác payload", "Category", "Xung đột duplicate; blocker."),
            Sheet("Danh mục", [
                ["CategoryCode", "Name"], ["CAT_CONFLICT", "Tên A"], ["CAT_CONFLICT", "Tên B"]]));

        AddWorkbook(catalog, Record("E30", $"{dir}/E30_missing_required_fields.xlsx", "VALIDATION", "Thiếu required fields", null, "TRƯỜNG_BẮT_BUỘC."),
            Sheet("Dữ liệu", [["CategoryCode", "Name"], ["CAT_ONLY", null], [null, "Chỉ có tên"]]));

        AddWorkbook(catalog, Record("E31", $"{dir}/E31_invalid_field_values.xlsx", "VALIDATION", "Giá trị không hợp lệ", null, "Icon/Active/SizeType/TaxCode/email báo lỗi typed."),
            Sheet("Danh mục", [["CategoryCode", "Name", "Icon", "Active"], ["C", "X", "not-icon", "maybe"]]),
            Sheet("Size", [["SizeCode", "Name", "SizeType"], ["SZ_BAD", "Sai", "Mass"]]),
            Sheet("Supplier", [["Name", "TaxCode", "PrimaryPhone", "PrimaryContactName", "PrimaryContactEmail"], ["NCC Sai", "ABC", "0901", "A", "not-email"]]));

        AddWorkbook(catalog, Record("E32", $"{dir}/E32_unicode_and_whitespace.xlsx", "NORMALIZATION", "Unicode, NBSP, zero-width, khoảng trắng", "Category", "Normalize key nhưng giữ raw evidence."),
            Sheet("Danh mục", [["Mã danh mục", "Tên danh mục"], ["  cat_unicode  ", "Cà​ phê sữa"]]));

        AddWorkbook(catalog, Record("E33", $"{dir}/E33_empty_workbook.xlsx", "EMPTY", "Workbook chỉ có sheet trống", null, "Không có group/candidate; báo layout/schema phù hợp."),
            new SheetSpec("Trống", []));

        AddWorkbook(catalog, Record("E34", $"{dir}/E34_unrecognized_headers.xlsx", "HEADER", "Header không khớp schema", null, "Entity Unknown/low confidence."),
            Sheet("Dữ liệu", [["Cột A", "Cột B", "Cột C"], ["x", "y", "z"]]));

        AddWorkbook(catalog, Record("E35", $"{dir}/E35_twenty_sheets_boundary.xlsx", "LIMIT", "Đúng 20 sheet", "Category", "Không lỗi MaxSheets."),
            Enumerable.Range(1, 20).Select(i => Sheet($"S{i:00}", CategoryRows($"CAT_S{i:00}", $"Sheet {i:00}"))).ToArray());

        AddWorkbook(catalog, Record("E36", $"{dir}/E36_twenty_one_sheets_exceeded.xlsx", "LIMIT", "21 sheet", null, "DỮ_LIỆU_VƯỢT_GIỚI_HẠN_MVP."),
            Enumerable.Range(1, 21).Select(i => Sheet($"S{i:00}", CategoryRows($"CAT_X{i:00}", $"Sheet {i:00}"))).ToArray());

        var wide = new object?[2][];
        wide[0] = Enumerable.Range(1, 101).Select(i => (object?)$"Column{i:000}").ToArray();
        wide[1] = Enumerable.Range(1, 101).Select(i => (object?)$"Value{i:000}").ToArray();
        AddWorkbook(catalog, Record("E37", $"{dir}/E37_one_hundred_one_columns_exceeded.xlsx", "LIMIT", "101 cột", null, "DỮ_LIỆU_VƯỢT_GIỚI_HẠN_MVP."), Sheet("Wide", wide));

        var tall = new object?[10_001][];
        tall[0] = ["CategoryCode", "Name"];
        for (var i = 1; i < tall.Length; i++) tall[i] = [$"CAT_{i:00000}", $"Danh mục {i:00000}"];
        AddWorkbook(catalog, Record("E38", $"{dir}/E38_ten_thousand_one_rows_exceeded.xlsx", "LIMIT", "10.001 dòng", null, "DỮ_LIỆU_VƯỢT_GIỚI_HẠN_MVP."), Sheet("Tall", tall));

        var regionBlocks = Enumerable.Range(0, 21)
            .Select(i => new Block(1, 1 + i * 3, CategoryRows($"CAT_R{i:00}", $"Region {i:00}"))).ToList();
        AddWorkbook(catalog, Record("E39", $"{dir}/E39_twenty_one_regions_exceeded.xlsx", "LIMIT", "21 vùng dữ liệu", null, "DỮ_LIỆU_VƯỢT_GIỚI_HẠN_MVP."),
            new SheetSpec("Regions", regionBlocks));

        var bomb = Record("E40", $"{dir}/E40_suspicious_compression_ratio.xlsx", "SECURITY", "ZIP có tỷ lệ nén bất thường", null, "FILE_QUÁ_LỚN.", true);
        catalog.WriteBytes(bomb, CompressionBomb());

        catalog.WriteText(Record("E41", $"{dir}/E41_corrupt_not_openxml.xlsx", "SECURITY", "Không phải gói OpenXML", null, "FILE_BỊ_HỎNG.", true), "not-an-xlsx");
        catalog.WriteText(Record("E42", $"{dir}/E42_fake_xlsx_contains_pdf.xlsx", "SECURITY", "PDF giả đuôi xlsx", null, "FILE_BỊ_HỎNG/signature mismatch.", true), "%PDF-1.7\nFake xlsx");
        catalog.WriteText(Record("E43", $"{dir}/E43_legacy_xls_unsupported.xls", "EXTENSION", "Định dạng .xls cũ", null, "Extension không được hỗ trợ.", true), "CategoryCode\tName\r\nCAT_XLS\tLegacy");
    }

    private static void Basic(FixtureCatalog catalog, string id, string path, string entity, object?[][] rows) =>
        AddWorkbook(catalog, Record(id, path, "BASIC", $"{entity} hợp lệ tối thiểu", entity, "Preview valid CREATE candidate."), Sheet(entity, rows));

    private static FixtureRecord Record(string id, string path, string category, string scenario, string? hint, string expected, bool invalid = false) =>
        new(id, path, "XLSX", category, scenario, hint, expected, "Fixture OpenXML độc lập; không phụ thuộc dữ liệu ngoài trừ reference được ghi rõ.", invalid);

    private static SheetSpec Sheet(string name, object?[][] values, bool hidden = false) => new(name, [new Block(1, 1, values)], hidden);

    private static object?[][] CategoryRows(string code, string name) =>
        [["CategoryCode", "Name", "Icon", "Active"], [code, name, "☕", true]];
    private static object?[][] DrinkRows(string code, string name, string category) =>
        [["DrinkCode", "Name", "Description", "Category", "ProductType"], [code, name, "Fixture test", category, "DRINK"]];
    private static object?[][] SizeRows(string code, string name, string type) =>
        [["SizeCode", "Name", "Description", "SizeType"], [code, name, "Fixture test", type]];
    private static object?[][] IngredientRows(string code, string name, string unit) =>
        [["Code", "Name", "BaseUnit"], [code, name, unit]];
    private static object?[][] SupplierRows(string name, string taxCode) =>
        [["Name", "TaxCode", "Address", "Note", "PrimaryPhone", "PrimaryContactName", "PrimaryContactPhone", "PrimaryContactEmail", "PrimaryContactPosition"],
         [name, taxCode, "1 Nguyễn Huệ, Quận 1", "Fixture test", "0901000001", "Nguyễn Minh Anh", "0901000002", "fixture@cafechain.test", "Kinh doanh"]];

    private static void AddWorkbook(FixtureCatalog catalog, FixtureRecord record, params SheetSpec[] sheets)
    {
        catalog.WriteBytes(record, BuildWorkbook(sheets));
    }

    private static byte[] BuildWorkbook(IReadOnlyList<SheetSpec> sheets)
    {
        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, true))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook(new Sheets());
            AddStyles(workbookPart);
            SharedStringTablePart? sharedPart = null;
            var sharedIndexes = new Dictionary<string, int>(StringComparer.Ordinal);

            for (var index = 0; index < sheets.Count; index++)
            {
                var spec = sheets[index];
                var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                var sheetData = new SheetData();
                var rows = new SortedDictionary<int, SortedDictionary<int, Cell>>();
                var headerCells = new HashSet<(int Row, int Column)>();
                foreach (var block in spec.Blocks)
                {
                    for (var r = 0; r < block.Values.Length; r++)
                    {
                        for (var c = 0; c < block.Values[r].Length; c++)
                        {
                            var value = block.Values[r][c];
                            if (value == null) continue;
                            var row = block.Row + r;
                            var column = block.Column + c;
                            if (!rows.TryGetValue(row, out var rowCells)) rows[row] = rowCells = [];
                            var cell = CreateCell(row, column, value, workbookPart, ref sharedPart, sharedIndexes);
                            if (block.Header && r == 0)
                            {
                                cell.StyleIndex = 1;
                                headerCells.Add((row, column));
                            }
                            rowCells[column] = cell;
                        }
                    }
                }

                foreach (var (rowIndex, rowCells) in rows)
                {
                    var row = new Row { RowIndex = (uint)rowIndex, CustomHeight = true, Height = headerCells.Any(x => x.Row == rowIndex) ? 24d : 20d };
                    if (spec.HiddenRows?.Contains(rowIndex) == true) row.Hidden = true;
                    row.Append(rowCells.Values);
                    sheetData.Append(row);
                }

                var columns = new Columns();
                var maxColumn = rows.Values.SelectMany(x => x.Keys).DefaultIfEmpty(1).Max();
                for (var column = 1; column <= maxColumn; column++)
                {
                    var width = rows.Values
                        .Select(x => x.GetValueOrDefault(column))
                        .Where(x => x != null)
                        .Select(CellDisplayLength)
                        .DefaultIfEmpty(10).Max();
                    columns.Append(new Column
                    {
                        Min = (uint)column,
                        Max = (uint)column,
                        Width = Math.Clamp(width + 3, 10, 32),
                        CustomWidth = true,
                        Hidden = spec.HiddenColumns?.Contains(column) == true
                    });
                }

                var worksheet = new Worksheet(columns, sheetData);
                if (spec.Merges is { Count: > 0 })
                {
                    var merges = new MergeCells();
                    foreach (var merge in spec.Merges) merges.Append(new MergeCell { Reference = merge });
                    worksheet.Append(merges);
                }
                worksheetPart.Worksheet = worksheet;
                spec.Customize?.Invoke(worksheetPart);
                worksheetPart.Worksheet.Save();

                workbookPart.Workbook.Sheets!.Append(new Sheet
                {
                    Id = workbookPart.GetIdOfPart(worksheetPart),
                    SheetId = (uint)index + 1,
                    Name = spec.Name,
                    State = spec.Hidden ? SheetStateValues.Hidden : SheetStateValues.Visible
                });
            }
            sharedPart?.SharedStringTable?.Save();
            workbookPart.Workbook.Save();
        }
        return stream.ToArray();
    }

    private static Cell CreateCell(int row, int column, object value, WorkbookPart workbookPart,
        ref SharedStringTablePart? sharedPart, Dictionary<string, int> sharedIndexes)
    {
        var cell = new Cell { CellReference = $"{ColumnName(column)}{row}" };
        switch (value)
        {
            case SharedValue shared:
                sharedPart ??= workbookPart.AddNewPart<SharedStringTablePart>();
                sharedPart.SharedStringTable ??= new SharedStringTable();
                if (!sharedIndexes.TryGetValue(shared.Value, out var index))
                {
                    index = sharedIndexes.Count;
                    sharedIndexes[shared.Value] = index;
                    sharedPart.SharedStringTable.Append(new SharedStringItem(new Text(shared.Value)));
                }
                cell.DataType = CellValues.SharedString;
                cell.CellValue = new CellValue(index.ToString());
                break;
            case FormulaValue formula:
                cell.CellFormula = new CellFormula(formula.Formula);
                if (formula.CachedValue != null) cell.CellValue = new CellValue(formula.CachedValue);
                break;
            case bool boolean:
                cell.DataType = CellValues.Boolean;
                cell.CellValue = new CellValue(boolean ? "1" : "0");
                break;
            case byte or short or int or long or float or double or decimal:
                cell.DataType = CellValues.Number;
                cell.CellValue = new CellValue(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
                break;
            default:
                cell.DataType = CellValues.InlineString;
                cell.InlineString = new InlineString(new Text(Convert.ToString(value) ?? string.Empty) { Space = SpaceProcessingModeValues.Preserve });
                break;
        }
        return cell;
    }

    private static int CellDisplayLength(Cell? cell)
    {
        if (cell == null) return 0;
        return (cell.InlineString?.InnerText ?? cell.CellValue?.Text ?? cell.CellFormula?.Text ?? string.Empty).Length;
    }

    private static string ColumnName(int index)
    {
        var result = string.Empty;
        while (index > 0)
        {
            index--;
            result = (char)('A' + index % 26) + result;
            index /= 26;
        }
        return result;
    }

    private static void AddStyles(WorkbookPart workbookPart)
    {
        var styles = workbookPart.AddNewPart<WorkbookStylesPart>();
        styles.Stylesheet = new Stylesheet(
            new Fonts(
                new Font(new FontName { Val = "Aptos" }, new FontSize { Val = 11 }),
                new Font(new Bold(), new Color { Rgb = "FFFFFFFF" }, new FontName { Val = "Aptos" }, new FontSize { Val = 11 })),
            new Fills(
                new Fill(new PatternFill { PatternType = PatternValues.None }),
                new Fill(new PatternFill { PatternType = PatternValues.Gray125 }),
                new Fill(new PatternFill(new ForegroundColor { Rgb = "FF0F766E" }, new BackgroundColor { Indexed = 64 }) { PatternType = PatternValues.Solid })),
            new Borders(new Border()),
            new CellStyleFormats(new CellFormat()),
            new CellFormats(
                new CellFormat(),
                new CellFormat { FontId = 1, FillId = 2, ApplyFont = true, ApplyFill = true, Alignment = new Alignment { Vertical = VerticalAlignmentValues.Center } }));
        styles.Stylesheet.Save();
    }

    private static byte[] CompressionBomb()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry("xl/huge.xml", CompressionLevel.SmallestSize);
            using var writer = entry.Open();
            writer.Write(new byte[500_000]);
        }
        return stream.ToArray();
    }
}
