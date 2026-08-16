using System.IO.Compression;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;

internal static class DocxFixtures
{
    private const string Dir = "02_DOCX";

    public static void Generate(FixtureCatalog catalog)
    {
        Add(catalog, R("D01", "D01_category_key_value.docx", "KEY_VALUE", "Category key-value hợp lệ", "Category", "1 valid deterministic candidate."),
            Doc(P("Mã danh mục: CAT_DOCX"), P("Tên danh mục: Cà phê rang"), P("Biểu tượng: ☕"), P("Hoạt động: true")));
        Add(catalog, R("D02", "D02_drink_key_value.docx", "KEY_VALUE", "Drink key-value hợp lệ", "Drink", "1 valid candidate; cần reference CAT_DOCX."),
            Doc(P("Mã đồ uống: DR_DOCX"), P("Tên đồ uống: Latte"), P("Mô tả: Cà phê sữa"), P("Danh mục: CAT_DOCX"), P("Loại sản phẩm: DRINK")));
        Add(catalog, R("D03", "D03_size_key_value.docx", "KEY_VALUE", "Size key-value hợp lệ", "Size", "1 valid candidate."),
            Doc(P("Mã size: SIZE_DOCX"), P("Tên size: Vừa"), P("Mô tả: Ly vừa"), P("Loại size: Cup")));
        Add(catalog, R("D04", "D04_ingredient_key_value.docx", "KEY_VALUE", "Ingredient key-value hợp lệ", "Ingredient", "1 valid candidate."),
            Doc(P("Mã nguyên liệu: ING_DOCX"), P("Tên nguyên liệu: Hạt cà phê"), P("Đơn vị cơ sở: GRAM")));
        Add(catalog, R("D05", "D05_supplier_key_value.docx", "KEY_VALUE", "Supplier key-value đủ editor fields", "Supplier", "1 valid candidate."),
            Doc(P("Tên nhà cung cấp: Công ty Nông Sản Việt"), P("Mã số thuế: 0312345681"), P("Địa chỉ: 12 Pasteur, Quận 1"),
                P("Ghi chú: Giao buổi sáng"), P("Số điện thoại: 0901000010"), P("Người liên hệ: Trần An"),
                P("SĐT liên hệ: 0901000011"), P("Email liên hệ: tran.an@cafechain.test"), P("Chức vụ: Kinh doanh")));

        Add(catalog, R("D06", "D06_category_table.docx", "TABLE", "Bảng Category chuẩn", "Category", "DOCX_TABLE_DETERMINISTIC."),
            Doc(Table([["CategoryCode", "Name", "Icon", "Active"], ["CAT_TABLE_DOCX", "Danh mục bảng", "🍵", "true"]])));

        Add(catalog, R("D07", "D07_multiple_records_blank_boundaries.docx", "BOUNDARY", "Nhiều record ngăn bằng paragraph trống", "Category", "2 candidates, locator paragraph riêng."),
            Doc(P("Mã danh mục: CAT_BLANK_1"), P("Tên danh mục: Một"), P(""), P("Mã danh mục: CAT_BLANK_2"), P("Tên danh mục: Hai")));

        Add(catalog, R("D08", "D08_heading_boundaries.docx", "BOUNDARY", "Heading phân ranh giới record", "Category", "2 candidates; heading không thành field."),
            Doc(Heading("Danh mục thứ nhất"), P("Mã danh mục: CAT_HEAD_1"), P("Tên danh mục: Heading một"),
                Heading("Danh mục thứ hai"), P("Mã danh mục: CAT_HEAD_2"), P("Tên danh mục: Heading hai")));

        Add(catalog, R("D09", "D09_list_item_boundaries.docx", "BOUNDARY", "List item phân ranh giới record", "Category", "2 candidates/list-aware boundary."),
            DocWithNumbering(
                ListP("Mã danh mục: CAT_LIST_1"), P("Tên danh mục: Danh mục list một"),
                ListP("Mã danh mục: CAT_LIST_2"), P("Tên danh mục: Danh mục list hai")));

        Add(catalog, R("D10", "D10_section_break_boundaries.docx", "BOUNDARY", "Section break giữa hai record", "Category", "2 candidates, section locator khác nhau."),
            Doc(P("Mã danh mục: CAT_SEC_1"), P("Tên danh mục: Section một"), SectionBreak(),
                P("Mã danh mục: CAT_SEC_2"), P("Tên danh mục: Section hai")));

        var gridSpanTable = Table([["CategoryCode", "Name", "Icon"], ["CAT_GRID", "Danh mục gridSpan", "☕"]]);
        gridSpanTable.Elements<W.TableRow>().First().Elements<W.TableCell>().First().TableCellProperties!
            .Append(new W.GridSpan { Val = 2 });
        Add(catalog, R("D11", "D11_gridspan_header_merge.docx", "MERGE", "gridSpan header merge", "Category", "DOCX_Ô_GỘP_CẦN_XEM_LẠI; locator theo ô vật lý."), Doc(gridSpanTable));

        var vertical = Table([["CategoryCode", "Name"], ["CAT_VM_DOCX", "Tên một"], ["", "Tên hai"]]);
        var vRows = vertical.Elements<W.TableRow>().ToArray();
        vRows[1].Elements<W.TableCell>().First().TableCellProperties!.Append(new W.VerticalMerge { Val = W.MergedCellValues.Restart });
        vRows[2].Elements<W.TableCell>().First().TableCellProperties!.Append(new W.VerticalMerge { Val = W.MergedCellValues.Continue });
        Add(catalog, R("D12", "D12_vertical_merge.docx", "MERGE", "Vertical merge qua hai dòng", "Category", "Giữ physical locator; manual review."), Doc(vertical));

        var ambiguous = Table([["CategoryCode", "Name", "Active"], ["CAT_AMBIG", "Tên mơ hồ", "true"]]);
        ambiguous.Elements<W.TableRow>().ElementAt(1).Elements<W.TableCell>().First().TableCellProperties!
            .Append(new W.GridSpan { Val = 2 });
        Add(catalog, R("D13", "D13_ambiguous_merged_ownership.docx", "MERGE", "Ô data merge sang nhiều field", "Category", "Không nhân value sang nhiều field; manual review."), Doc(ambiguous));

        Add(catalog, R("D14", "D14_tracked_insertion.docx", "REVISION", "Tracked insertion chứa final-visible value", "Category", "DOCX_TRACK_CHANGE_CẦN_XEM_LẠI; giữ inserted text."),
            Doc(P("Mã danh mục: CAT_TRACK_INS"), RevisionParagraph("Tên danh mục: ", inserted: "Tên đã chèn")));
        Add(catalog, R("D15", "D15_tracked_deletion.docx", "REVISION", "Tracked deletion + insertion", "Category", "Bỏ deleted text, giữ final-visible; manual review."),
            Doc(P("Mã danh mục: CAT_TRACK_DEL"), RevisionParagraph("Tên danh mục: ", deleted: "Tên cũ", inserted: "Tên mới")));
        Add(catalog, R("D16", "D16_tracked_move.docx", "REVISION", "Tracked moveFrom/moveTo", "Category", "Bỏ moveFrom, giữ moveTo; manual review."),
            Doc(P("Mã danh mục: CAT_TRACK_MOVE"), MoveParagraph("Tên danh mục: ", "Tên chuyển cũ", "Tên chuyển mới")));

        var nestedClear = Table([["CategoryCode", "Name"], ["CAT_NEST", "Danh mục cha"]]);
        nestedClear.Elements<W.TableRow>().ElementAt(1).Elements<W.TableCell>().ElementAt(1)
            .Append(Table([["Ghi chú", "Không thuộc record"], ["Nested", "Tách riêng"]]));
        Add(catalog, R("D17", "D17_nested_table_clear_boundary.docx", "NESTED", "Nested table có ranh giới rõ", "Category", "Không nhập text nested vào outer cell; source review."), Doc(nestedClear));

        var nestedAmbiguous = Table([["CategoryCode", "Name"], ["CAT_NEST_AMB", ""]]);
        nestedAmbiguous.Elements<W.TableRow>().ElementAt(1).Elements<W.TableCell>().ElementAt(1)
            .Append(Table([["Name"], ["Tên chỉ nằm trong nested"]]));
        Add(catalog, R("D18", "D18_nested_table_ambiguous.docx", "NESTED", "Nested table giữ field bắt buộc của outer", "Category", "KHÔNG_XÁC_ĐỊNH_RANH_GIỚI_BẢN_GHI."), Doc(nestedAmbiguous));

        Add(catalog, R("D19", "D19_header_footer_isolation.docx", "ISOLATION", "Header/footer chứa record giả", "Category", "Chỉ CAT_BODY được đọc."),
            DocWithHeaderFooter(
                [P("Mã danh mục: CAT_BODY"), P("Tên danh mục: Nội dung body")],
                "Mã danh mục: CAT_HEADER | Tên danh mục: Không đọc header",
                "Mã danh mục: CAT_FOOTER | Tên danh mục: Không đọc footer"));

        Add(catalog, R("D20", "D20_comments_isolation.docx", "ISOLATION", "Comment chứa record giả", "Category", "Comment không trở thành source; comments wiring hợp lệ."),
            DocWithComment("Mã danh mục: CAT_COMMENT_BODY", "Tên danh mục: Body có comment", "Mã danh mục: CAT_COMMENT_FAKE; Tên danh mục: Không đọc"));

        Add(catalog, R("D21", "D21_footnote_endnote_isolation.docx", "ISOLATION", "Footnote/endnote chứa record giả", "Category", "Chỉ body là source; note parts bị cô lập."),
            DocWithNotes());

        Add(catalog, R("D22", "D22_multiple_tables_different_entities.docx", "MULTI_ENTITY", "Hai bảng Category và Drink", null, "2 groups; dependency Category trước Drink."),
            Doc(Table([["CategoryCode", "Name"], ["CAT_MULTI_DOCX", "Danh mục multi"]]), P(""),
                Table([["DrinkCode", "Name", "Category", "ProductType"], ["DR_MULTI_DOCX", "Đồ uống multi", "CAT_MULTI_DOCX", "DRINK"]])));

        Add(catalog, R("D23", "D23_narrative_ai_fallback.docx", "AI_FALLBACK", "Narrative không có cấu trúc key-value", "Drink", "Deterministic có thể không tạo group; semantic AI xử lý nếu bật."),
            Doc(Heading("Danh sách sản phẩm mới"), P("Hãy tạo đồ uống mã DR_NARRATIVE có tên Espresso Tonic, thuộc danh mục CAT_COFFEE và loại sản phẩm DRINK."),
                P("Mô tả sản phẩm là cà phê espresso kết hợp tonic.")));

        Add(catalog, R("D24", "D24_multi_page_records.docx", "MULTI_PAGE", "Ba record trên ba trang", "Category", "3 candidates và page layout ổn."),
            Doc(P("Mã danh mục: CAT_PAGE_1"), P("Tên danh mục: Trang một"), PageBreak(),
                P("Mã danh mục: CAT_PAGE_2"), P("Tên danh mục: Trang hai"), PageBreak(),
                P("Mã danh mục: CAT_PAGE_3"), P("Tên danh mục: Trang ba")));

        Add(catalog, R("D25", "D25_external_relationship_active.docx", "SECURITY", "External hyperlink relationship", null, "NỘI_DUNG_CHỦ_ĐỘNG_KHÔNG_ĐƯỢC_HỖ_TRỢ.", true),
            DocWithExternalRelationship());
        Add(catalog, R("D26", "D26_field_command_active.docx", "SECURITY", "INCLUDETEXT field command", null, "NỘI_DUNG_CHỦ_ĐỘNG_KHÔNG_ĐƯỢC_HỖ_TRỢ.", true),
            Doc(FieldCommand(), P("Mã danh mục: CAT_FIELD"), P("Tên danh mục: Không an toàn")));

        catalog.WriteText(R("D27", "D27_corrupt_not_openxml.docx", "SECURITY", "Không phải gói OpenXML", null, "FILE_BỊ_HỎNG.", true), "not-a-docx");
        catalog.WriteBytes(R("D28", "D28_suspicious_compression_ratio.docx", "SECURITY", "ZIP nén bất thường", null, "FILE_QUÁ_LỚN.", true), CompressionBomb());
        catalog.WriteText(R("D29", "D29_legacy_doc_unsupported.doc", "EXTENSION", "Word .doc cũ", null, "Extension không được hỗ trợ.", true),
            "{\\rtf1\\ansi Mã danh mục: CAT_DOC_LEGACY\\par Tên danh mục: Legacy}");

        var docm = Doc(P("Mã danh mục: CAT_DOCM"), P("Tên danh mục: Đuôi docm"));
        catalog.WriteBytes(R("D30", "D30_docm_extension_unsupported.docm", "EXTENSION", "Package Word hợp lệ nhưng đuôi .docm", null, "Extension không được hỗ trợ.", true), docm);

        Add(catalog, R("D31", "D31_duplicate_labels_same_record.docx", "BOUNDARY", "Nhãn lặp trong cùng record", "Category", "KHÔNG_XÁC_ĐỊNH_RANH_GIỚI_BẢN_GHI."),
            Doc(P("Mã danh mục: CAT_DUP_LABEL"), P("Tên danh mục: Tên một"), P("Tên danh mục: Tên hai")));

        Add(catalog, R("D32", "D32_table_unknown_extra_column.docx", "COLUMN", "Bảng có cột dư unknown", "Category", "CỘT_KHÔNG_XÁC_ĐỊNH warning."),
            Doc(Table([["CategoryCode", "Name", "Màu nội bộ"], ["CAT_DOC_EXTRA", "Cột dư", "xanh"]])));
        Add(catalog, R("D33", "D33_table_forbidden_store_id.docx", "COLUMN", "Bảng có StoreId", "Category", "CỘT_CẤM blocker."),
            Doc(Table([["CategoryCode", "Name", "StoreId"], ["CAT_DOC_STORE", "Sai phạm vi", "1"]])));
    }

    private static FixtureRecord R(string id, string name, string category, string scenario, string? hint, string expected, bool invalid = false) =>
        new(id, $"{Dir}/{name}", name.EndsWith(".doc", StringComparison.OrdinalIgnoreCase) ? "DOC" : name.EndsWith(".docm", StringComparison.OrdinalIgnoreCase) ? "DOCM" : "DOCX",
            category, scenario, hint, expected, "DOCX dùng compact_reference_guide: Letter, lề 1 inch, Calibri 11, table fixed DXA.", invalid);

    private static void Add(FixtureCatalog catalog, FixtureRecord record, byte[] bytes) => catalog.WriteBytes(record, bytes);

    private static W.Paragraph P(string text) => new(new W.ParagraphProperties(new W.SpacingBetweenLines { After = "120", Line = "300", LineRule = W.LineSpacingRuleValues.Auto }),
        new W.Run(new W.Text(text) { Space = SpaceProcessingModeValues.Preserve }));

    private static W.Paragraph Heading(string text) => new(new W.ParagraphProperties(new W.ParagraphStyleId { Val = "Heading1" }), new W.Run(new W.Text(text)));

    private static W.Paragraph ListP(string text) => new(
        new W.ParagraphProperties(
            new W.NumberingProperties(new W.NumberingLevelReference { Val = 0 }, new W.NumberingId { Val = 1 }),
            new W.SpacingBetweenLines { After = "80", Line = "300", LineRule = W.LineSpacingRuleValues.Auto }),
        new W.Run(new W.Text(text)));

    private static W.Paragraph PageBreak() => new(new W.Run(new W.Break { Type = W.BreakValues.Page }));

    private static W.Paragraph SectionBreak() => new(new W.ParagraphProperties(new W.SectionProperties(
        new W.SectionType { Val = W.SectionMarkValues.NextPage }, LetterPage(), LetterMargins())));

    private static W.Paragraph RevisionParagraph(string prefix, string? deleted = null, string? inserted = null)
    {
        var paragraph = P(prefix);
        if (deleted != null)
        {
            var deletedRun = new W.DeletedRun { Id = "101", Author = "Fixture Generator", Date = DateTime.UtcNow };
            deletedRun.Append(new W.Run(new W.DeletedText(deleted)));
            paragraph.Append(deletedRun);
        }
        if (inserted != null)
        {
            var insertedRun = new W.InsertedRun { Id = "102", Author = "Fixture Generator", Date = DateTime.UtcNow };
            insertedRun.Append(new W.Run(new W.Text(inserted)));
            paragraph.Append(insertedRun);
        }
        return paragraph;
    }

    private static W.Paragraph MoveParagraph(string prefix, string movedFrom, string movedTo)
    {
        var paragraph = P(prefix);
        var from = new OpenXmlUnknownElement("w:moveFrom");
        from.SetAttribute(new OpenXmlAttribute("w", "id", "http://schemas.openxmlformats.org/wordprocessingml/2006/main", "201"));
        from.Append(new W.Run(new W.DeletedText(movedFrom)));
        var to = new OpenXmlUnknownElement("w:moveTo");
        to.SetAttribute(new OpenXmlAttribute("w", "id", "http://schemas.openxmlformats.org/wordprocessingml/2006/main", "202"));
        to.Append(new W.Run(new W.Text(movedTo)));
        paragraph.Append(from, to);
        return paragraph;
    }

    private static W.Table Table(string?[][] values)
    {
        var widths = ColumnWidths(values.FirstOrDefault()?.Length ?? 1);
        var table = new W.Table();
        table.Append(new W.TableProperties(
            new W.TableWidth { Width = "9360", Type = W.TableWidthUnitValues.Dxa },
            new W.TableIndentation { Width = 120, Type = W.TableWidthUnitValues.Dxa },
            new W.TableLayout { Type = W.TableLayoutValues.Fixed },
            Borders(), CellMargins()));
        table.Append(new W.TableGrid(widths.Select(width => new W.GridColumn { Width = width.ToString() })));
        for (var r = 0; r < values.Length; r++)
        {
            var row = new W.TableRow();
            if (r == 0) row.Append(new W.TableRowProperties(new W.TableHeader()));
            for (var c = 0; c < values[r].Length; c++)
            {
                var cell = new W.TableCell(
                    new W.TableCellProperties(new W.TableCellWidth { Width = widths[c].ToString(), Type = W.TableWidthUnitValues.Dxa },
                        new W.Shading { Fill = r == 0 ? "E8EEF5" : "FFFFFF" },
                        new W.TableCellVerticalAlignment { Val = W.TableVerticalAlignmentValues.Center }),
                    P(values[r][c] ?? string.Empty));
                row.Append(cell);
            }
            table.Append(row);
        }
        return table;
    }

    private static int[] ColumnWidths(int count)
    {
        var result = Enumerable.Repeat(9360 / Math.Max(1, count), Math.Max(1, count)).ToArray();
        result[^1] += 9360 - result.Sum();
        return result;
    }

    private static W.TableBorders Borders() => new(
        new W.TopBorder { Val = W.BorderValues.Single, Size = 4, Color = "B8C2CC" },
        new W.LeftBorder { Val = W.BorderValues.Single, Size = 4, Color = "B8C2CC" },
        new W.BottomBorder { Val = W.BorderValues.Single, Size = 4, Color = "B8C2CC" },
        new W.RightBorder { Val = W.BorderValues.Single, Size = 4, Color = "B8C2CC" },
        new W.InsideHorizontalBorder { Val = W.BorderValues.Single, Size = 4, Color = "D7DEE5" },
        new W.InsideVerticalBorder { Val = W.BorderValues.Single, Size = 4, Color = "D7DEE5" });

    private static W.TableCellMarginDefault CellMargins() => new(
        new W.TopMargin { Width = "80", Type = W.TableWidthUnitValues.Dxa },
        new W.StartMargin { Width = "120", Type = W.TableWidthUnitValues.Dxa },
        new W.BottomMargin { Width = "80", Type = W.TableWidthUnitValues.Dxa },
        new W.EndMargin { Width = "120", Type = W.TableWidthUnitValues.Dxa });

    private static byte[] Doc(params OpenXmlElement[] bodyElements) => CreateDocument(null, null, null, bodyElements);

    private static byte[] DocWithNumbering(params OpenXmlElement[] bodyElements) => CreateDocument(null, null, AddNumbering, bodyElements);

    private static byte[] DocWithHeaderFooter(OpenXmlElement[] bodyElements, string header, string footer) =>
        CreateDocument(header, footer, null, bodyElements);

    private static byte[] CreateDocument(string? header, string? footer, Action<MainDocumentPart>? extra, params OpenXmlElement[] bodyElements)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var main = document.AddMainDocumentPart();
            AddStyles(main);
            var body = new W.Body();
            body.Append(bodyElements.Select(x => x.CloneNode(true)));
            var section = new W.SectionProperties(LetterPage(), LetterMargins());
            if (header != null)
            {
                var part = main.AddNewPart<HeaderPart>();
                part.Header = new W.Header(P(header));
                section.PrependChild(new W.HeaderReference { Id = main.GetIdOfPart(part), Type = W.HeaderFooterValues.Default });
            }
            if (footer != null)
            {
                var part = main.AddNewPart<FooterPart>();
                part.Footer = new W.Footer(P(footer));
                section.PrependChild(new W.FooterReference { Id = main.GetIdOfPart(part), Type = W.HeaderFooterValues.Default });
            }
            body.Append(section);
            main.Document = new W.Document(body);
            extra?.Invoke(main);
            main.Document.Save();
        }
        return stream.ToArray();
    }

    private static W.PageSize LetterPage() => new() { Width = 12240, Height = 15840, Orient = W.PageOrientationValues.Portrait };
    private static W.PageMargin LetterMargins() => new() { Top = 1440, Right = 1440, Bottom = 1440, Left = 1440, Header = 708, Footer = 708, Gutter = 0 };

    private static void AddStyles(MainDocumentPart main)
    {
        var part = main.AddNewPart<StyleDefinitionsPart>();
        part.Styles = new W.Styles(
            Style("Normal", "Normal", 22, "000000", 0, 120, false),
            Style("Heading1", "Heading 1", 32, "2E74B5", 360, 200, true),
            Style("Heading2", "Heading 2", 26, "2E74B5", 280, 140, true),
            Style("Heading3", "Heading 3", 24, "1F4D78", 200, 100, true));
        part.Styles.Save();
    }

    private static W.Style Style(string id, string name, int halfPoints, string color, int before, int after, bool bold)
    {
        var runProps = new W.StyleRunProperties(new W.RunFonts { Ascii = "Calibri", HighAnsi = "Calibri", EastAsia = "Calibri" },
            new W.Color { Val = color }, new W.FontSize { Val = halfPoints.ToString() });
        if (bold) runProps.Append(new W.Bold());
        return new W.Style(
            new W.StyleName { Val = name },
            new W.BasedOn { Val = id == "Normal" ? null : "Normal" },
            new W.StyleParagraphProperties(new W.SpacingBetweenLines { Before = before.ToString(), After = after.ToString(), Line = "300", LineRule = W.LineSpacingRuleValues.Auto }),
            runProps) { Type = W.StyleValues.Paragraph, StyleId = id, Default = id == "Normal" };
    }

    private static void AddNumbering(MainDocumentPart main)
    {
        var part = main.AddNewPart<NumberingDefinitionsPart>();
        var level = new W.Level(
            new W.StartNumberingValue { Val = 1 }, new W.NumberingFormat { Val = W.NumberFormatValues.Bullet },
            new W.LevelText { Val = "•" }, new W.LevelJustification { Val = W.LevelJustificationValues.Left },
            new W.PreviousParagraphProperties(new W.Tabs(new W.TabStop { Val = W.TabStopValues.Number, Position = 540 }),
                new W.Indentation { Left = "540", Hanging = "270" })) { LevelIndex = 0 };
        part.Numbering = new W.Numbering(new W.AbstractNum(level) { AbstractNumberId = 1 },
            new W.NumberingInstance(new W.AbstractNumId { Val = 1 }) { NumberID = 1 });
        part.Numbering.Save();
    }

    private static byte[] DocWithExternalRelationship()
    {
        var bytes = Doc(P("Mã danh mục: CAT_EXTERNAL"), P("Tên danh mục: External relationship"));
        using var stream = new MemoryStream();
        stream.Write(bytes);
        stream.Position = 0;
        using (var document = WordprocessingDocument.Open(stream, true))
            document.MainDocumentPart!.AddHyperlinkRelationship(new Uri("https://example.com/unsafe"), true);
        return stream.ToArray();
    }

    private static W.Paragraph FieldCommand() => new(
        new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.Begin }),
        new W.Run(new W.FieldCode(" INCLUDETEXT \"https://example.com/data.txt\" ")),
        new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.End }));

    private static byte[] DocWithComment(string codeLine, string nameLine, string commentText)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var main = document.AddMainDocumentPart();
            AddStyles(main);
            var targetRun = new W.Run(new W.Text(nameLine));
            var paragraph = new W.Paragraph(new W.CommentRangeStart { Id = "0" }, targetRun,
                new W.CommentRangeEnd { Id = "0" }, new W.Run(new W.CommentReference { Id = "0" }));
            main.Document = new W.Document(new W.Body(P(codeLine), paragraph, new W.SectionProperties(LetterPage(), LetterMargins())));
            var commentsPart = main.AddNewPart<WordprocessingCommentsPart>();
            commentsPart.Comments = new W.Comments(new W.Comment(P(commentText))
            { Id = "0", Author = "Fixture Generator", Date = DateTime.UtcNow, Initials = "FG" });
            commentsPart.Comments.Save();
            main.Document.Save();
        }
        return stream.ToArray();
    }

    private static byte[] DocWithNotes()
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var main = document.AddMainDocumentPart();
            AddStyles(main);
            var body = new W.Body(
                P("Mã danh mục: CAT_NOTE_BODY"),
                new W.Paragraph(new W.Run(new W.Text("Tên danh mục: Body notes")), new W.Run(new W.FootnoteReference { Id = 1 })),
                new W.Paragraph(new W.Run(new W.Text("Nội dung chính")), new W.Run(new W.EndnoteReference { Id = 1 })),
                new W.SectionProperties(LetterPage(), LetterMargins()));
            main.Document = new W.Document(body);
            var footnotes = main.AddNewPart<FootnotesPart>();
            footnotes.Footnotes = new W.Footnotes(
                SeparatorFootnote(-1, true), SeparatorFootnote(0, false),
                new W.Footnote(P("Mã danh mục: CAT_FAKE_FOOTNOTE; Tên danh mục: Không đọc footnote")) { Id = 1 });
            var endnotes = main.AddNewPart<EndnotesPart>();
            endnotes.Endnotes = new W.Endnotes(
                SeparatorEndnote(-1, true), SeparatorEndnote(0, false),
                new W.Endnote(P("Mã danh mục: CAT_FAKE_ENDNOTE; Tên danh mục: Không đọc endnote")) { Id = 1 });
            footnotes.Footnotes.Save();
            endnotes.Endnotes.Save();
            main.Document.Save();
        }
        return stream.ToArray();
    }

    private static W.Footnote SeparatorFootnote(int id, bool separator) => new(
        new W.Paragraph(new W.Run(separator ? new W.SeparatorMark() : new W.ContinuationSeparatorMark())))
        { Id = id, Type = separator ? W.FootnoteEndnoteValues.Separator : W.FootnoteEndnoteValues.ContinuationSeparator };
    private static W.Endnote SeparatorEndnote(int id, bool separator) => new(
        new W.Paragraph(new W.Run(separator ? new W.SeparatorMark() : new W.ContinuationSeparatorMark())))
        { Id = id, Type = separator ? W.FootnoteEndnoteValues.Separator : W.FootnoteEndnoteValues.ContinuationSeparator };

    private static byte[] CompressionBomb()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry("word/document.xml", CompressionLevel.SmallestSize);
            using var writer = entry.Open();
            writer.Write(new byte[500_000]);
        }
        return stream.ToArray();
    }
}
