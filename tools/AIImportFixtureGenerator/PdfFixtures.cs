using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QDocument = QuestPDF.Fluent.Document;
using QImageFormat = QuestPDF.Infrastructure.ImageFormat;
using DrawingColor = System.Drawing.Color;

#pragma warning disable CA1416

internal static class PdfFixtures
{
    private const string TextDir = "03_PDF_TEXT";
    private const string ScanDir = "04_PDF_SCAN";
    private static readonly TextStyle BodyStyle = TextStyle.Default.FontFamily("Arial").FontSize(12).FontColor(Colors.Grey.Darken4);

    public static void GenerateTextPdfs(FixtureCatalog catalog)
    {
        SaveText(catalog, T("P01", "P01_category_key_value.pdf", "KEY_VALUE", "Category key-value", "Category", "1 PDF_TEXT_DETERMINISTIC candidate.", 1),
            Page("Mã danh mục: CAT_PDF\nTên danh mục: Cà phê PDF\nBiểu tượng: ☕\nHoạt động: true"));
        SaveText(catalog, T("P02", "P02_drink_key_value.pdf", "KEY_VALUE", "Drink key-value", "Drink", "1 candidate; cần reference CAT_PDF.", 1),
            Page("Mã đồ uống: DR_PDF\nTên đồ uống: Espresso PDF\nMô tả: Fixture PDF\nDanh mục: CAT_PDF\nLoại sản phẩm: DRINK"));
        SaveText(catalog, T("P03", "P03_size_key_value.pdf", "KEY_VALUE", "Size key-value", "Size", "1 valid candidate.", 1),
            Page("Mã size: SIZE_PDF\nTên size: Lớn\nMô tả: Dung tích lớn\nLoại size: Volume"));
        SaveText(catalog, T("P04", "P04_ingredient_key_value.pdf", "KEY_VALUE", "Ingredient key-value", "Ingredient", "1 valid candidate.", 1),
            Page("Mã nguyên liệu: ING_PDF\nTên nguyên liệu: Sữa tươi\nĐơn vị cơ sở: MILLILITER"));
        SaveText(catalog, T("P05", "P05_supplier_key_value.pdf", "KEY_VALUE", "Supplier đầy đủ field", "Supplier", "1 valid candidate.", 1),
            Page("Tên nhà cung cấp: Công ty Sữa Việt\nMã số thuế: 0312345682\nĐịa chỉ: 20 Lê Lợi, Quận 1\nGhi chú: Chuỗi lạnh\nSố điện thoại: 0901000020\nNgười liên hệ: Lê Bình\nSĐT liên hệ: 0901000021\nEmail liên hệ: le.binh@cafechain.test\nChức vụ: Điều phối"));

        SaveText(catalog, T("P06", "P06_category_table.pdf", "TABLE", "Bảng Category theo tọa độ", "Category", "PDF_TEXT_DETERMINISTIC table.", 1),
            TablePage(["CategoryCode", "Name", "Icon", "Active"], [["CAT_PDF_TABLE", "Danh mục bảng PDF", "☕", "true"]]));

        SaveText(catalog, T("P07", "P07_multi_page_records.pdf", "MULTI_PAGE", "Mỗi trang một record", "Category", "3 page locators/candidates.", 3),
            Page("Mã danh mục: CAT_P1\nTên danh mục: Trang một"),
            Page("Mã danh mục: CAT_P2\nTên danh mục: Trang hai"),
            Page("Mã danh mục: CAT_P3\nTên danh mục: Trang ba"));

        SaveText(catalog, T("P08", "P08_multi_page_table_repeated_header.pdf", "MULTI_PAGE_TABLE", "Bảng qua nhiều trang, header lặp", "Category", "Merge table khi schema/geometry/continuation tương thích; header lặp không thành row.", 3),
            TablePage(["CategoryCode", "Name"], [["CAT_MP_1", "Multi page một"]], "Bảng danh mục - trang 1/3"),
            TablePage(["CategoryCode", "Name"], [["CAT_MP_2", "Multi page hai"]], "Bảng danh mục - trang 2/3"),
            TablePage(["CategoryCode", "Name"], [["CAT_MP_3", "Multi page ba"]], "Bảng danh mục - trang 3/3"));

        SaveText(catalog, T("P09", "P09_two_columns_clear.pdf", "COLUMN", "Hai document columns có thứ tự rõ", "Category", "2 candidates hoặc reading order chắc chắn.", 1),
            TwoColumnPage(
                "Mã danh mục: CAT_COL_LEFT\nTên danh mục: Cột trái",
                "Mã danh mục: CAT_COL_RIGHT\nTên danh mục: Cột phải",
                false));

        SaveText(catalog, T("P10", "P10_two_columns_ambiguous.pdf", "COLUMN", "Hai cột xen kẽ cùng cao độ", "Category", "THỨ_TỰ_ĐỌC_PDF_KHÔNG_RÕ/manual review.", 1),
            TwoColumnPage(
                "Mã danh mục: CAT_AMB_LEFT\nTên danh mục: Trái",
                "Mã danh mục: CAT_AMB_RIGHT\nTên danh mục: Phải",
                true));

        catalog.WriteBytes(T("P11", "P11_page_rotation_90.pdf", "ROTATION", "Page dictionary /Rotate 90", "Category", "PdfPig rotation=90; normalize coordinate/top-left.", 1), BuildRawRotatedPdf(90, "CAT_ROT_90"));
        catalog.WriteBytes(T("P12", "P12_page_rotation_180.pdf", "ROTATION", "Page dictionary /Rotate 180", "Category", "PdfPig rotation=180; normalize coordinate/top-left.", 1), BuildRawRotatedPdf(180, "CAT_ROT_180"));
        catalog.WriteBytes(T("P13", "P13_page_rotation_270.pdf", "ROTATION", "Page dictionary /Rotate 270", "Category", "PdfPig rotation=270; normalize coordinate/top-left.", 1), BuildRawRotatedPdf(270, "CAT_ROT_270"));

        SaveText(catalog, T("P14", "P14_repeated_header_footer.pdf", "DECORATION", "Header/footer lặp trên bốn trang", "Category", "Lọc decoration theo normalized text/relative position/page count.", 4),
            DecoratedPage("CAT_DEC_1", "Trang một", 1, 4), DecoratedPage("CAT_DEC_2", "Trang hai", 2, 4),
            DecoratedPage("CAT_DEC_3", "Trang ba", 3, 4), DecoratedPage("CAT_DEC_4", "Trang bốn", 4, 4));

        SaveText(catalog, T("P15", "P15_ligature_nbsp_zero_width.pdf", "UNICODE", "Ligature, NBSP, zero-width", "Category", "Normalize trước business key; raw evidence giữ nguyên.", 1),
            Page("Mã danh mục: CAT_ﬁX\nTên danh mục: Cà​ phê ﬁlter"));
        SaveText(catalog, T("P16", "P16_vietnamese_unicode_normalization.pdf", "UNICODE", "Tiếng Việt precomposed/decomposed", "Category", "Nhận alias Unicode ổn định.", 1),
            Page("Mã danh mục: CAT_UNICODE_PDF\nTên danh mục: Cà phê Đắk Lắk – đặc biệt"));

        SaveText(catalog, T("P17", "P17_split_row_across_pages.pdf", "MULTI_PAGE_TABLE", "Một row bị cắt qua hai trang", "Drink", "BẢNG_PDF_KHÔNG_RÕ nếu không đủ bằng chứng nối row.", 2),
            TablePage(["DrinkCode", "Name", "Category", "ProductType"], [["DR_SPLIT", "Espresso", "", ""]], "Row bắt đầu"),
            TablePage(["DrinkCode", "Name", "Category", "ProductType"], [["", "", "CAT_PDF", "DRINK"]], "Row tiếp tục"));

        SaveText(catalog, T("P18", "P18_table_unknown_extra_column.pdf", "COLUMN", "Bảng có cột dư", "Category", "CỘT_KHÔNG_XÁC_ĐỊNH warning.", 1),
            TablePage(["CategoryCode", "Name", "Màu nội bộ"], [["CAT_PDF_EXTRA", "Cột dư", "xanh"]]));
        SaveText(catalog, T("P19", "P19_table_forbidden_store_id.pdf", "COLUMN", "Bảng có StoreId", "Category", "CỘT_CẤM blocker.", 1),
            TablePage(["CategoryCode", "Name", "StoreId"], [["CAT_PDF_STORE", "Sai phạm vi", "1"]]));

        SaveText(catalog, T("P20", "P20_multiple_entities_separate_pages.pdf", "MULTI_ENTITY", "Category và Drink ở trang riêng", null, "2 groups; dependency Category trước Drink.", 2),
            Page("Mã danh mục: CAT_PDF_MULTI\nTên danh mục: Multi PDF"),
            Page("Mã đồ uống: DR_PDF_MULTI\nTên đồ uống: Multi Drink\nDanh mục: CAT_PDF_MULTI\nLoại sản phẩm: DRINK"));

        SaveText(catalog, T("P21", "P21_blank_pdf.pdf", "EMPTY", "PDF trắng", null, "PDF_CẦN_OCR khi không có text layer.", 1, textLayer: false), EmptyPage());

        SaveText(catalog, T("P22", "P22_two_hundred_pages_boundary.pdf", "LIMIT", "Đúng 200 trang text", "Category", "Không lỗi PdfMaxPages; nhiều candidates.", 200),
            Enumerable.Range(1, 200).Select(i => Page($"Mã danh mục: CAT_200_{i:000}\nTên danh mục: Trang {i:000}")).ToArray(), qa: false);
        SaveText(catalog, T("P23", "P23_two_hundred_one_pages_exceeded.pdf", "LIMIT", "201 trang text", null, "PDF_VƯỢT_GIỚI_HẠN.", 201),
            Enumerable.Range(1, 201).Select(i => Page($"Mã danh mục: CAT_201_{i:000}\nTên danh mục: Trang {i:000}")).ToArray(), qa: false);

        var safe = BuildPdf([Page("Mã danh mục: CAT_SECURITY\nTên danh mục: Security marker")]);
        catalog.WriteBytes(T("P24", "P24_active_uri_marker.pdf", "SECURITY", "Preflight marker /URI", null, "NỘI_DUNG_CHỦ_ĐỘNG_KHÔNG_ĐƯỢC_HỖ_TRỢ.", 1, true), AppendMarker(safe, "/URI"));
        catalog.WriteBytes(T("P25", "P25_embedded_file_marker.pdf", "SECURITY", "Preflight marker /EmbeddedFile", null, "NỘI_DUNG_CHỦ_ĐỘNG_KHÔNG_ĐƯỢC_HỖ_TRỢ.", 1, true), AppendMarker(safe, "/EmbeddedFile"));
        catalog.WriteBytes(T("P26", "P26_javascript_marker.pdf", "SECURITY", "Preflight marker /JavaScript", null, "NỘI_DUNG_CHỦ_ĐỘNG_KHÔNG_ĐƯỢC_HỖ_TRỢ.", 1, true), AppendMarker(safe, "/JavaScript"));
        catalog.WriteBytes(T("P27", "P27_launch_marker.pdf", "SECURITY", "Preflight marker /Launch", null, "NỘI_DUNG_CHỦ_ĐỘNG_KHÔNG_ĐƯỢC_HỖ_TRỢ.", 1, true), AppendMarker(safe, "/Launch"));
        catalog.WriteBytes(T("P28", "P28_encrypt_marker.pdf", "SECURITY", "Preflight marker /Encrypt", null, "PDF_CÓ_MẬT_KHẨU.", 1, true), AppendMarker(safe, "/Encrypt"));
        catalog.WriteText(T("P29", "P29_corrupt_pdf.pdf", "SECURITY", "PDF hỏng", null, "PDF_BỊ_HỎNG.", null, true), "%PDF-not-a-real-file");
        catalog.WriteText(T("P30", "P30_fake_pdf_extension.pdf", "SECURITY", "Nội dung không có PDF signature", null, "PDF_BỊ_HỎNG/signature mismatch.", null, true), "This is not a PDF");
    }

    public static void GenerateScanPdfs(FixtureCatalog catalog)
    {
        var cleanCategory = Raster(["Mã danh mục: CAT_SCAN", "Tên danh mục: Danh mục scan", "Biểu tượng: coffee", "Hoạt động: true"]);
        SaveScan(catalog, S("S01", "S01_clean_category_scan.pdf", "CLEAN", "Scan Category rõ nét", "Category", "IMAGE_BASED; OCR page 1 khi bật.", 1), [ScanPage(cleanCategory)]);

        var cleanSupplier = Raster(["Tên nhà cung cấp: Công ty Scan Việt", "Mã số thuế: 0312345683", "Số điện thoại: 0901000030", "Người liên hệ: Nguyễn Scan", "Email liên hệ: scan@cafechain.test"]);
        SaveScan(catalog, S("S02", "S02_clean_supplier_scan.pdf", "CLEAN", "Scan Supplier rõ nét", "Supplier", "OCR đủ required fields.", 1), [ScanPage(cleanSupplier)]);

        var scanTable = RasterTable(["CategoryCode", "Name", "Active"], [["CAT_SCAN_TABLE", "Bảng scan", "true"], ["CAT_SCAN_TABLE_2", "Bảng scan hai", "true"]]);
        SaveScan(catalog, S("S03", "S03_table_scan.pdf", "TABLE", "Bảng scan có đường kẻ", "Category", "OCR deterministic/table reconstruction.", 1), [ScanPage(scanTable)]);

        SaveScan(catalog, S("S04", "S04_low_contrast_scan.pdf", "QUALITY", "Scan tương phản thấp", "Category", "Có thể OCR_CONFIDENCE_THẤP/manual review.", 1),
            [ScanPage(Raster(["Mã danh mục: CAT_LOW", "Tên danh mục: Tương phản thấp"], lowContrast: true))]);
        SaveScan(catalog, S("S05", "S05_blurred_scan.pdf", "QUALITY", "Scan mờ do downscale/upscale", "Category", "Có thể OCR_CONFIDENCE_THẤP/manual review.", 1),
            [ScanPage(Raster(["Mã danh mục: CAT_BLUR", "Tên danh mục: Bị mờ"], scale: 0.42f))]);
        SaveScan(catalog, S("S06", "S06_skewed_scan.pdf", "QUALITY", "Scan lệch 7 độ", "Category", "OCR polygon/rotation evidence.", 1),
            [ScanPage(Raster(["Mã danh mục: CAT_SKEW", "Tên danh mục: Bị lệch"], angle: 7))]);
        SaveScan(catalog, S("S07", "S07_noisy_scan.pdf", "QUALITY", "Scan có nhiễu hạt", "Category", "OCR confidence/evidence review.", 1),
            [ScanPage(Raster(["Mã danh mục: CAT_NOISE", "Tên danh mục: Có nhiễu"], noise: 8500))]);
        SaveScan(catalog, S("S08", "S08_tiny_text_scan.pdf", "QUALITY", "Chữ rất nhỏ", "Category", "OCR confidence thấp hoặc output thiếu field.", 1),
            [ScanPage(Raster(["Mã danh mục: CAT_TINY", "Tên danh mục: Chữ nhỏ"], fontSize: 12))]);
        SaveScan(catalog, S("S09", "S09_rotated_image_90_scan.pdf", "ROTATION", "Ảnh scan xoay 90 độ", "Category", "OCR rotation/polygon được giữ.", 1),
            [ScanPage(RotateImage(cleanCategory, 90))]);

        SaveScan(catalog, S("S10", "S10_three_page_scan.pdf", "MULTI_PAGE", "Ba trang scan", "Category", "OCR chỉ nhận pages 1,2,3.", 3),
            [ScanPage(Raster(["Mã danh mục: CAT_SCAN_P1", "Tên danh mục: Scan trang 1"])),
             ScanPage(Raster(["Mã danh mục: CAT_SCAN_P2", "Tên danh mục: Scan trang 2"])),
             ScanPage(Raster(["Mã danh mục: CAT_SCAN_P3", "Tên danh mục: Scan trang 3"]))]);

        var small = Raster(["Mã danh mục: CAT_51", "Tên danh mục: OCR quá 50 trang"], width: 510, height: 660, fontSize: 16);
        SaveScan(catalog, S("S11", "S11_fifty_one_page_scan_limit.pdf", "LIMIT", "51 trang đều cần OCR", null, "PDF_OCR_VƯỢT_GIỚI_HẠN; không silent truncate.", 51),
            Enumerable.Range(1, 51).Select(_ => ScanPage(small)).ToArray(), qa: false);

        SaveScan(catalog, S("S12", "S12_mixed_text_and_scan_same_page.pdf", "MIXED", "Một trang có text layer và ảnh scan lớn", "Category", "MIXED; provider chỉ nhận trang 1.", 1, true),
            [MixedPage("Mã danh mục: CAT_MIXED_LAYER", Raster(["Tên danh mục: Giá trị từ scan", "Hoạt động: true"]))]);

        SaveScan(catalog, S("S13", "S13_alternating_text_scan_pages.pdf", "MIXED", "Trang 1/3 text, trang 2/4 scan", "Category", "Provider chỉ nhận pages 2,4; merge provenance riêng.", 4, true),
            [Page("Mã danh mục: CAT_ALT_1\nTên danh mục: Text trang 1"),
             ScanPage(Raster(["Mã danh mục: CAT_ALT_2", "Tên danh mục: Scan trang 2"])),
             Page("Mã danh mục: CAT_ALT_3\nTên danh mục: Text trang 3"),
             ScanPage(Raster(["Mã danh mục: CAT_ALT_4", "Tên danh mục: Scan trang 4"]))]);

        SaveScan(catalog, S("S14", "S14_two_column_scan.pdf", "COLUMN", "Scan hai cột", "Category", "OCR reading order/manual review nếu mơ hồ.", 1),
            [ScanPage(RasterColumns(["Mã danh mục: CAT_SCAN_LEFT", "Tên danh mục: Trái"], ["Mã danh mục: CAT_SCAN_RIGHT", "Tên danh mục: Phải"]))]);

        SaveScan(catalog, S("S15", "S15_repeated_header_scan.pdf", "DECORATION", "Scan nhiều trang có header/footer lặp", "Category", "Lọc decoration sau OCR.", 3),
            Enumerable.Range(1, 3).Select(i => ScanPage(Raster([$"BÁO CÁO AI IMPORT", $"Mã danh mục: CAT_SCAN_DEC_{i}", $"Tên danh mục: Scan {i}", $"Trang {i}/3"]))).ToArray());

        SaveScan(catalog, S("S16", "S16_sparse_fields_scan.pdf", "SPARSE", "Scan có field nằm xa nhau", "Supplier", "Semantic grouping theo page/block; không đoán field thiếu.", 1),
            [ScanPage(Raster(["Tên nhà cung cấp: NCC Sparse", "", "", "Số điện thoại: 0901000040", "", "Người liên hệ: Sparse Contact"]))]);

        SaveScan(catalog, S("S17", "S17_blank_image_scan.pdf", "EMPTY", "Trang chỉ có ảnh trắng", null, "PDF_CẦN_OCR; OCR output có thể không hợp lệ/không candidate.", 1),
            [ScanPage(BlankRaster())]);

        SaveScan(catalog, S("S18", "S18_oversized_page_pixel_guard.pdf", "LIMIT", "Page 2200x2200 pt cần >20M pixel ở 200 DPI", null, "PDF_OCR_VƯỢT_GIỚI_HẠN trước provider.", 1),
            [ScanPage(cleanCategory, new PageSize(2200, 2200))]);

        SaveScan(catalog, S("S19", "S19_scan_unknown_extra_columns.pdf", "COLUMN", "Bảng scan có cột dư", "Category", "Field provenance OCR + unknown column warning.", 1),
            [ScanPage(RasterTable(["CategoryCode", "Name", "Màu nội bộ"], [["CAT_SCAN_EXTRA", "Scan cột dư", "xanh"]]))]);

        var entityPages = new[]
        {
            Raster(["Mã danh mục: CAT_SCAN_ALL", "Tên danh mục: Danh mục scan all"]),
            Raster(["Mã đồ uống: DR_SCAN_ALL", "Tên đồ uống: Drink scan all", "Danh mục: CAT_SCAN_ALL", "Loại sản phẩm: DRINK"]),
            Raster(["Mã size: SIZE_SCAN_ALL", "Tên size: Vừa", "Loại size: Cup"]),
            Raster(["Mã nguyên liệu: ING_SCAN_ALL", "Tên nguyên liệu: Đường", "Đơn vị cơ sở: GRAM"]),
            Raster(["Tên nhà cung cấp: NCC Scan All", "Mã số thuế: 0312345684", "Số điện thoại: 0901000050", "Người liên hệ: Scan All"])
        };
        SaveScan(catalog, S("S20", "S20_all_entities_five_page_scan.pdf", "MULTI_ENTITY", "Năm entity trên năm trang scan", null, "5 OCR groups; dependency order giữ nguyên.", 5),
            entityPages.Select(image => ScanPage(image)).ToArray());
    }

    private sealed record PdfPage(Action<PageDescriptor> Compose, PageSize? Size = null);

    private static FixtureRecord T(string id, string name, string category, string scenario, string? hint, string expected,
        int? pages, bool invalid = false, bool? textLayer = true) =>
        new(id, $"{TextDir}/{name}", "PDF_TEXT", category, scenario, hint, expected,
            invalid && category == "SECURITY" ? "Security marker được chèn sau %%EOF để kiểm đúng preflight byte scanner; không phải payload khai thác." : "PDF text thật, tạo bằng QuestPDF; locator phải theo page/block/bbox.",
            invalid, pages, textLayer);

    private static FixtureRecord S(string id, string name, string category, string scenario, string? hint, string expected,
        int pages, bool? textLayer = false) =>
        new(id, $"{ScanDir}/{name}", category == "MIXED" ? "PDF_MIXED" : "PDF_SCAN", category, scenario, hint, expected,
            "PDF scan chứa ảnh raster JPEG thật và không có text layer, trừ fixture MIXED được ghi rõ.", false, pages, textLayer);

    private static PdfPage Page(string text) => new(page =>
    {
        BasePage(page);
        page.Content().Padding(42).Text(text).Style(BodyStyle).LineHeight(1.45f);
    });

    private static PdfPage EmptyPage() => new(page =>
    {
        BasePage(page);
        page.Content().Background(Colors.White);
    });

    private static PdfPage DecoratedPage(string code, string name, int pageNumber, int total) => new(page =>
    {
        BasePage(page);
        page.Header().PaddingHorizontal(42).PaddingTop(20).Text("CAFECHAIN - DANH SÁCH DANH MỤC").Style(BodyStyle.FontSize(9).FontColor(Colors.Grey.Medium));
        page.Content().Padding(42).Text($"Mã danh mục: {code}\nTên danh mục: {name}").Style(BodyStyle).LineHeight(1.45f);
        page.Footer().PaddingHorizontal(42).PaddingBottom(20).AlignRight().Text($"Trang {pageNumber}/{total}").Style(BodyStyle.FontSize(9).FontColor(Colors.Grey.Medium));
    });

    private static PdfPage TablePage(string[] headers, string[][] rows, string? caption = null) => new(page =>
    {
        BasePage(page);
        page.Content().Padding(36).Column(column =>
        {
            if (!string.IsNullOrWhiteSpace(caption)) column.Item().PaddingBottom(12).Text(caption).Style(BodyStyle.SemiBold().FontSize(14));
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(cols => { foreach (var _ in headers) cols.RelativeColumn(); });
                foreach (var header in headers)
                    table.Cell().Background(Colors.Teal.Darken2).Padding(8).Text(header).Style(BodyStyle.SemiBold().FontColor(Colors.White).FontSize(10));
                foreach (var row in rows)
                    foreach (var value in row)
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(8).Text(value).Style(BodyStyle.FontSize(10));
            });
        });
    });

    private static PdfPage TwoColumnPage(string left, string right, bool ambiguous) => new(page =>
    {
        BasePage(page);
        page.Content().Padding(36).Row(row =>
        {
            row.RelativeItem().PaddingRight(12).TranslateY(ambiguous ? 0 : -20).Border(0.7f).BorderColor(Colors.Grey.Lighten1).Padding(14).Text(left).Style(BodyStyle.FontSize(10));
            row.RelativeItem().PaddingLeft(12).TranslateY(ambiguous ? 0 : 35).Border(0.7f).BorderColor(Colors.Grey.Lighten1).Padding(14).Text(right).Style(BodyStyle.FontSize(10));
        });
    });

    private static PdfPage RotatedPage(string text, int angle) => new(page =>
    {
        BasePage(page);
        var container = page.Content().Padding(80).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(20);
        if (angle == 90) container.RotateRight().Text(text).Style(BodyStyle);
        else if (angle == 180) container.RotateRight().RotateRight().Text(text).Style(BodyStyle);
        else container.RotateLeft().Text(text).Style(BodyStyle);
    });

    private static PdfPage ScanPage(byte[] image, PageSize? size = null) => new(page =>
    {
        BasePage(page, size);
        page.Content().Padding(8).Image(image).FitArea();
    }, size);

    private static PdfPage MixedPage(string textLayer, byte[] image) => new(page =>
    {
        BasePage(page);
        page.Content().Padding(20).Column(column =>
        {
            column.Item().Height(30).Text(textLayer).Style(BodyStyle.FontSize(9).FontColor(Colors.Grey.Darken1));
            column.Item().Image(image).FitArea();
        });
    });

    private static void BasePage(PageDescriptor page, PageSize? size = null)
    {
        page.Size(size ?? PageSizes.Letter);
        page.Margin(0);
        page.PageColor(Colors.White);
        page.DefaultTextStyle(BodyStyle);
    }

    private static void SaveText(FixtureCatalog catalog, FixtureRecord record, params PdfPage[] pages) => SaveText(catalog, record, pages, true);
    private static void SaveText(FixtureCatalog catalog, FixtureRecord record, PdfPage[] pages, bool qa) => SavePdf(catalog, record, pages, qa);

    private static void SaveScan(FixtureCatalog catalog, FixtureRecord record, PdfPage[] pages, bool qa = true) => SavePdf(catalog, record, pages, qa);

    private static void SavePdf(FixtureCatalog catalog, FixtureRecord record, IReadOnlyList<PdfPage> pages, bool qa)
    {
        var document = BuildDocument(pages);
        catalog.WriteBytes(record, document.GeneratePdf());
        if (qa && pages.Count <= 12)
        {
            var qaRoot = Path.Combine(Path.GetTempPath(), "CafeChain-AIImport-Fixture-QA", record.Id);
            Directory.CreateDirectory(qaRoot);
            var settings = new ImageGenerationSettings { ImageFormat = QImageFormat.Png, RasterDpi = 96 };
            var images = document.GenerateImages(settings).ToArray();
            for (var i = 0; i < images.Length; i++) File.WriteAllBytes(Path.Combine(qaRoot, $"page-{i + 1:000}.png"), images[i]);
        }
    }

    private static byte[] BuildPdf(IReadOnlyList<PdfPage> pages) => BuildDocument(pages).GeneratePdf();

    private static IDocument BuildDocument(IReadOnlyList<PdfPage> pages) => QDocument.Create(container =>
    {
        foreach (var page in pages) container.Page(page.Compose);
    });

    private static byte[] AppendMarker(byte[] pdf, string marker)
    {
        var suffix = Encoding.ASCII.GetBytes($"\n% AI_IMPORT_SECURITY_FIXTURE {marker}\n");
        return [.. pdf, .. suffix];
    }

    private static byte[] BuildRawRotatedPdf(int rotation, string categoryCode)
    {
        var content = $"BT /F1 12 Tf 72 720 Td (CategoryCode: {categoryCode}) Tj 0 -24 Td (Name: Rotated {rotation}) Tj ET";
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Rotate {rotation} /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
        };
        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, Encoding.ASCII, 1024, true) { NewLine = "\n" };
        writer.WriteLine("%PDF-1.4");
        writer.Flush();
        var offsets = new List<long> { 0 };
        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(stream.Position);
            writer.WriteLine($"{index + 1} 0 obj");
            writer.WriteLine(objects[index]);
            writer.WriteLine("endobj");
            writer.Flush();
        }
        var xref = stream.Position;
        writer.WriteLine("xref");
        writer.WriteLine($"0 {objects.Length + 1}");
        writer.WriteLine("0000000000 65535 f ");
        foreach (var offset in offsets.Skip(1)) writer.WriteLine($"{offset:0000000000} 00000 n ");
        writer.WriteLine($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] Raster(string[] lines, int width = 1275, int height = 1650, float fontSize = 28,
        bool lowContrast = false, float scale = 1f, float angle = 0, int noise = 0)
    {
        var renderWidth = Math.Max(320, (int)(width * scale));
        var renderHeight = Math.Max(420, (int)(height * scale));
        using var small = new Bitmap(renderWidth, renderHeight, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(small))
        {
            graphics.Clear(lowContrast ? DrawingColor.FromArgb(238, 238, 235) : DrawingColor.White);
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            graphics.TranslateTransform(renderWidth / 2f, renderHeight / 2f);
            graphics.RotateTransform(angle);
            graphics.TranslateTransform(-renderWidth / 2f, -renderHeight / 2f);
            using var font = new Font("Arial", Math.Max(8, fontSize * scale), FontStyle.Regular, GraphicsUnit.Pixel);
            using var brush = new SolidBrush(lowContrast ? DrawingColor.FromArgb(150, 150, 145) : DrawingColor.FromArgb(28, 32, 38));
            var y = 150f * scale;
            foreach (var line in lines)
            {
                graphics.DrawString(line, font, brush, 110f * scale, y);
                y += Math.Max(46, fontSize * 1.8f) * scale;
            }
            if (noise > 0)
            {
                var random = new Random(20260815);
                using var noiseBrush = new SolidBrush(DrawingColor.FromArgb(105, 105, 105));
                for (var i = 0; i < noise; i++) graphics.FillRectangle(noiseBrush, random.Next(renderWidth), random.Next(renderHeight), 1, 1);
            }
        }

        using var final = scale == 1f ? new Bitmap(small) : new Bitmap(small, width, height);
        using var stream = new MemoryStream();
        final.Save(stream, System.Drawing.Imaging.ImageFormat.Jpeg);
        return stream.ToArray();
    }

    private static byte[] RasterColumns(string[] left, string[] right)
    {
        using var bitmap = new Bitmap(1275, 1650, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(DrawingColor.White);
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using var font = new Font("Arial", 23, FontStyle.Regular, GraphicsUnit.Pixel);
            using var brush = new SolidBrush(DrawingColor.FromArgb(25, 28, 32));
            for (var i = 0; i < left.Length; i++) graphics.DrawString(left[i], font, brush, 60, 180 + i * 54);
            for (var i = 0; i < right.Length; i++) graphics.DrawString(right[i], font, brush, 665, 180 + i * 54);
            graphics.DrawLine(Pens.LightGray, 630, 120, 630, 1500);
        }
        return Jpeg(bitmap);
    }

    private static byte[] RasterTable(string[] headers, string[][] rows)
    {
        using var bitmap = new Bitmap(1275, 1650, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(DrawingColor.White);
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using var headerFont = new Font("Arial", 20, FontStyle.Bold, GraphicsUnit.Pixel);
            using var bodyFont = new Font("Arial", 19, FontStyle.Regular, GraphicsUnit.Pixel);
            using var brush = new SolidBrush(DrawingColor.FromArgb(28, 32, 38));
            using var headerBrush = new SolidBrush(DrawingColor.FromArgb(220, 235, 234));
            var left = 60;
            var top = 160;
            var width = 1155;
            var rowHeight = 62;
            var colWidth = width / headers.Length;
            graphics.FillRectangle(headerBrush, left, top, width, rowHeight);
            for (var c = 0; c <= headers.Length; c++) graphics.DrawLine(Pens.Gray, left + c * colWidth, top, left + c * colWidth, top + rowHeight * (rows.Length + 1));
            for (var r = 0; r <= rows.Length + 1; r++) graphics.DrawLine(Pens.Gray, left, top + r * rowHeight, left + width, top + r * rowHeight);
            for (var c = 0; c < headers.Length; c++) graphics.DrawString(headers[c], headerFont, brush, left + c * colWidth + 8, top + 18);
            for (var r = 0; r < rows.Length; r++)
                for (var c = 0; c < headers.Length; c++)
                    graphics.DrawString(rows[r][c], bodyFont, brush, left + c * colWidth + 8, top + (r + 1) * rowHeight + 18);
        }
        return Jpeg(bitmap);
    }

    private static byte[] BlankRaster()
    {
        using var bitmap = new Bitmap(1275, 1650, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(bitmap)) graphics.Clear(DrawingColor.White);
        return Jpeg(bitmap);
    }

    private static byte[] RotateImage(byte[] image, int angle)
    {
        using var source = new Bitmap(new MemoryStream(image));
        source.RotateFlip(angle switch
        {
            90 => RotateFlipType.Rotate90FlipNone,
            180 => RotateFlipType.Rotate180FlipNone,
            270 => RotateFlipType.Rotate270FlipNone,
            _ => RotateFlipType.RotateNoneFlipNone
        });
        return Jpeg(source);
    }

    private static byte[] Jpeg(System.Drawing.Image image)
    {
        using var stream = new MemoryStream();
        image.Save(stream, System.Drawing.Imaging.ImageFormat.Jpeg);
        return stream.ToArray();
    }
}

#pragma warning restore CA1416
