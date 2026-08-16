using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

#pragma warning disable CA1416

internal static class QaContactSheetWriter
{
    public static void Write()
    {
        var qaRoot = Path.Combine(Path.GetTempPath(), "CafeChain-AIImport-Fixture-QA");
        if (!Directory.Exists(qaRoot)) return;
        WriteGroup(qaRoot, "P", Path.Combine(qaRoot, "CONTACT_PDF_TEXT.png"));
        WriteGroup(qaRoot, "S", Path.Combine(qaRoot, "CONTACT_PDF_SCAN.png"));
    }

    private static void WriteGroup(string qaRoot, string prefix, string output)
    {
        var images = Directory.GetDirectories(qaRoot, $"{prefix}*")
            .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
            .SelectMany(folder => Directory.GetFiles(folder, "page-*.png").OrderBy(x => x, StringComparer.Ordinal)
                .Select(path => (Label: $"{Path.GetFileName(folder)}-{Path.GetFileNameWithoutExtension(path).Replace("page-", "p")}", Path: path)))
            .ToList();
        if (images.Count == 0) return;

        const int columns = 5;
        const int cellWidth = 260;
        const int cellHeight = 365;
        const int thumbWidth = 240;
        const int thumbHeight = 320;
        var rows = (int)Math.Ceiling(images.Count / (double)columns);
        using var sheet = new Bitmap(columns * cellWidth, rows * cellHeight, PixelFormat.Format24bppRgb);
        using var graphics = Graphics.FromImage(sheet);
        graphics.Clear(Color.White);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        using var font = new Font("Arial", 11, FontStyle.Bold, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(Color.FromArgb(25, 32, 39));
        using var border = new Pen(Color.FromArgb(190, 200, 210), 1);

        for (var index = 0; index < images.Count; index++)
        {
            var x = index % columns * cellWidth + 10;
            var y = index / columns * cellHeight + 8;
            using var image = Image.FromFile(images[index].Path);
            var scale = Math.Min(thumbWidth / (double)image.Width, thumbHeight / (double)image.Height);
            var width = (int)(image.Width * scale);
            var height = (int)(image.Height * scale);
            graphics.DrawRectangle(border, x - 1, y - 1, thumbWidth + 2, thumbHeight + 2);
            graphics.DrawImage(image, x + (thumbWidth - width) / 2, y + (thumbHeight - height) / 2, width, height);
            graphics.DrawString(images[index].Label, font, brush, x, y + thumbHeight + 10);
        }
        sheet.Save(output, ImageFormat.Png);
    }
}

#pragma warning restore CA1416
