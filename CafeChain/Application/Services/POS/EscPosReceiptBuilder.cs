using CafeChain.Application.Interfaces.POS;
using CafeChain.Models.Orders;
using System;
using System.Collections.Generic;
using System.Text;

namespace CafeChain.Application.Services.POS
{
    /// <summary>
    /// ADR-0003: ESC/POS Receipt Builder cho máy in nhiệt 80mm.
    /// 
    /// Tham chiếu ESC/POS command set:
    ///   - ESC @ (0x1B 0x40)         : Initialize printer (reset)
    ///   - ESC a n (0x1B 0x61 n)     : Justify (0=left, 1=center, 2=right)
    ///   - ESC E n (0x1B 0x45 n)     : Bold on/off (1=on, 0=off)
    ///   - GS ! n (0x1D 0x21 n)      : Character size (bit 0-3 = width, bit 4-7 = height)
    ///   - GS V m (0x1D 0x56 0x42 n) : Partial cut with feed n lines
    ///   - ESC p m t1 t2 (0x1B 0x70) : Kick cash drawer via RJ11 connector
    ///   - LF (0x0A)                 : Line feed
    /// 
    /// Encoding: Codepage 437 (US English) — mặc định của hầu hết máy in nhiệt.
    /// Tiếng Việt: Không dùng dấu (stripped) vì máy in nhiệt giá rẻ không hỗ trợ UTF-8.
    /// </summary>
    public class EscPosReceiptBuilder : IEscPosBuilder
    {
        // ═══════════════════════════════════════════════════════════
        // ESC/POS Command Constants — HEX values
        // ═══════════════════════════════════════════════════════════

        /// <summary>ESC @ — Initialize printer (reset all settings)</summary>
        private static readonly byte[] CMD_INIT = { 0x1B, 0x40 };

        /// <summary>ESC a 1 — Center alignment</summary>
        private static readonly byte[] CMD_ALIGN_CENTER = { 0x1B, 0x61, 0x01 };

        /// <summary>ESC a 0 — Left alignment</summary>
        private static readonly byte[] CMD_ALIGN_LEFT = { 0x1B, 0x61, 0x00 };

        /// <summary>ESC a 2 — Right alignment</summary>
        private static readonly byte[] CMD_ALIGN_RIGHT = { 0x1B, 0x61, 0x02 };

        /// <summary>ESC E 1 — Bold ON</summary>
        private static readonly byte[] CMD_BOLD_ON = { 0x1B, 0x45, 0x01 };

        /// <summary>ESC E 0 — Bold OFF</summary>
        private static readonly byte[] CMD_BOLD_OFF = { 0x1B, 0x45, 0x00 };

        /// <summary>GS ! 0x11 — Double width + Double height</summary>
        private static readonly byte[] CMD_SIZE_DOUBLE = { 0x1D, 0x21, 0x11 };

        /// <summary>GS ! 0x01 — Double width, normal height</summary>
        private static readonly byte[] CMD_SIZE_WIDE = { 0x1D, 0x21, 0x01 };

        /// <summary>GS ! 0x00 — Normal size (reset)</summary>
        private static readonly byte[] CMD_SIZE_NORMAL = { 0x1D, 0x21, 0x00 };

        /// <summary>LF — Line feed</summary>
        private static readonly byte[] CMD_LF = { 0x0A };

        /// <summary>
        /// GS V 66 3 — Partial cut, feed 3 lines before cut.
        /// 0x1D 0x56 = GS V, 0x42 = partial cut mode, 0x03 = feed 3 lines.
        /// </summary>
        private static readonly byte[] CMD_CUT = { 0x1D, 0x56, 0x42, 0x03 };

        /// <summary>
        /// ESC p 0 25 250 — Kick cash drawer pin 2 (RJ11 connector).
        /// 0x1B 0x70 = ESC p, 0x00 = pin 2, 0x19 = on time (25×2ms=50ms), 0xFA = off time (250×2ms=500ms).
        /// Tương thích EPSON TM series, StarPRNT, Bixolon.
        /// </summary>
        private static readonly byte[] CMD_KICK_DRAWER = { 0x1B, 0x70, 0x00, 0x19, 0xFA };

        /// <summary>Số ký tự trên 1 dòng (máy in 80mm, font A)</summary>
        private const int LINE_WIDTH = 42;

        // ═══════════════════════════════════════════════════════════
        // PUBLIC API
        // ═══════════════════════════════════════════════════════════

        public byte[] BuildReceipt(Order order, string storeName, string cashierName, decimal cashReceived, bool isCashPayment)
        {
            var buffer = new List<byte>();

            // 1. Initialize printer
            buffer.AddRange(CMD_INIT);

            // 2. Cash Drawer Kick — NẾU thanh toán tiền mặt, kick TRƯỚC khi in
            //    Máy in nhận lệnh kick tức thì, không cần đợi in xong
            if (isCashPayment)
            {
                buffer.AddRange(CMD_KICK_DRAWER);
            }

            // ── HEADER: Store name ──
            buffer.AddRange(CMD_ALIGN_CENTER);
            buffer.AddRange(CMD_SIZE_DOUBLE);
            buffer.AddRange(CMD_BOLD_ON);
            buffer.AddRange(TextToBytes(StripVietnamese(storeName)));
            buffer.AddRange(CMD_LF);
            buffer.AddRange(CMD_SIZE_NORMAL);
            buffer.AddRange(CMD_BOLD_OFF);

            // ── SUB-HEADER: Order info ──
            buffer.AddRange(CMD_ALIGN_CENTER);
            buffer.AddRange(TextToBytes($"HOA DON BAN HANG"));
            buffer.AddRange(CMD_LF);
            buffer.AddRange(TextToBytes(DashedLine()));
            buffer.AddRange(CMD_LF);

            // ── Order metadata ──
            buffer.AddRange(CMD_ALIGN_LEFT);
            buffer.AddRange(TextToBytes($"Ma don  : #{order.OrderId}"));
            buffer.AddRange(CMD_LF);
            buffer.AddRange(TextToBytes($"Ngay    : {order.CreatedAt:dd/MM/yyyy HH:mm}"));
            buffer.AddRange(CMD_LF);
            buffer.AddRange(TextToBytes($"Thu ngan: {StripVietnamese(cashierName)}"));
            buffer.AddRange(CMD_LF);
            buffer.AddRange(TextToBytes($"Loai    : {GetOrderTypeName(order.OrderTypeId)}"));
            buffer.AddRange(CMD_LF);
            buffer.AddRange(TextToBytes(DashedLine()));
            buffer.AddRange(CMD_LF);

            // ── Column header ──
            buffer.AddRange(CMD_BOLD_ON);
            buffer.AddRange(TextToBytes(FormatColumns("Mon", "SL", "T.Tien")));
            buffer.AddRange(CMD_LF);
            buffer.AddRange(CMD_BOLD_OFF);
            buffer.AddRange(TextToBytes(DashedLine()));
            buffer.AddRange(CMD_LF);

            // ── Order items ──
            if (order.OrderDetails != null)
            {
                foreach (var detail in order.OrderDetails)
                {
                    string itemName = StripVietnamese(detail.DrinkName ?? "???");
                    if (!string.IsNullOrEmpty(detail.SizeName))
                        itemName += $" ({detail.SizeName})";

                    string qty = detail.Quantity.ToString();
                    string lineTotal = FormatMoney(detail.Price * detail.Quantity);

                    buffer.AddRange(TextToBytes(FormatColumns(itemName, qty, lineTotal)));
                    buffer.AddRange(CMD_LF);

                    // Toppings (indented)
                    if (detail.OrderToppings != null)
                    {
                        foreach (var topping in detail.OrderToppings)
                        {
                            string toppingLine = $"  + {StripVietnamese(topping.ToppingName ?? "Topping")}";
                            string toppingPrice = FormatMoney(topping.Price);
                            buffer.AddRange(TextToBytes(FormatColumns(toppingLine, "", toppingPrice)));
                            buffer.AddRange(CMD_LF);
                        }
                    }
                }
            }

            buffer.AddRange(TextToBytes(DashedLine()));
            buffer.AddRange(CMD_LF);

            // ── Totals section ──
            buffer.AddRange(CMD_BOLD_ON);
            buffer.AddRange(TextToBytes(FormatRight("Tam tinh:", FormatMoney(order.SubTotal))));
            buffer.AddRange(CMD_LF);

            if (order.VoucherDiscount > 0)
            {
                buffer.AddRange(CMD_BOLD_OFF);
                buffer.AddRange(TextToBytes(FormatRight("Giam voucher:", $"-{FormatMoney(order.VoucherDiscount)}")));
                buffer.AddRange(CMD_LF);
            }

            if (order.PointDiscount > 0)
            {
                buffer.AddRange(CMD_BOLD_OFF);
                buffer.AddRange(TextToBytes(FormatRight("Giam diem:", $"-{FormatMoney(order.PointDiscount)}")));
                buffer.AddRange(CMD_LF);
            }

            // TOTAL — size lớn
            buffer.AddRange(CMD_SIZE_WIDE);
            buffer.AddRange(CMD_BOLD_ON);
            buffer.AddRange(TextToBytes(FormatRight("TONG:", FormatMoney(order.Total))));
            buffer.AddRange(CMD_LF);
            buffer.AddRange(CMD_SIZE_NORMAL);
            buffer.AddRange(CMD_BOLD_OFF);

            // Cash payment details
            if (isCashPayment && cashReceived > 0)
            {
                buffer.AddRange(TextToBytes(FormatRight("Tien khach dua:", FormatMoney(cashReceived))));
                buffer.AddRange(CMD_LF);
                decimal change = cashReceived - order.Total;
                if (change > 0)
                {
                    buffer.AddRange(TextToBytes(FormatRight("Tien thoi:", FormatMoney(change))));
                    buffer.AddRange(CMD_LF);
                }
            }

            buffer.AddRange(TextToBytes(DashedLine()));
            buffer.AddRange(CMD_LF);

            // ── Footer ──
            buffer.AddRange(CMD_ALIGN_CENTER);
            buffer.AddRange(TextToBytes("Cam on quy khach!"));
            buffer.AddRange(CMD_LF);
            buffer.AddRange(TextToBytes("Hen gap lai!"));
            buffer.AddRange(CMD_LF);
            buffer.AddRange(CMD_LF);

            // ── Feed + Partial Cut ──
            buffer.AddRange(CMD_LF);
            buffer.AddRange(CMD_LF);
            buffer.AddRange(CMD_CUT);

            // ── Reset printer ──
            buffer.AddRange(CMD_INIT);

            return buffer.ToArray();
        }

        /// <summary>
        /// Build tem pha chế cho từng ly trong đơn.
        /// Nghiệp vụ: tem dán lên ly, phục vụ nhân viên pha chế; không thay thế hóa đơn bán hàng.
        /// </summary>
        public byte[] BuildCupLabels(Order order, string storeName, string cashierName)
        {
            var buffer = new List<byte>();
            if (order.OrderDetails == null)
                return Array.Empty<byte>();

            var totalCups = 0;
            foreach (var detail in order.OrderDetails)
            {
                if (detail.Quantity > 0)
                    totalCups += detail.Quantity;
            }

            if (totalCups == 0)
                return Array.Empty<byte>();

            var cupNo = 1;
            foreach (var detail in order.OrderDetails)
            {
                if (detail.Quantity <= 0)
                    continue;

                for (var i = 0; i < detail.Quantity; i++)
                {
                    AddCupLabel(buffer, order, detail, storeName, cashierName, cupNo, totalCups);
                    cupNo++;
                }
            }

            buffer.AddRange(CMD_INIT);
            return buffer.ToArray();
        }

        // ═══════════════════════════════════════════════════════════
        // PRIVATE HELPERS
        // ═══════════════════════════════════════════════════════════

        private static void AddCupLabel(
            List<byte> buffer,
            Order order,
            OrderDetail detail,
            string storeName,
            string cashierName,
            int cupNo,
            int totalCups)
        {
            buffer.AddRange(CMD_INIT);

            buffer.AddRange(CMD_ALIGN_CENTER);
            buffer.AddRange(CMD_BOLD_ON);
            AddTextLine(buffer, StripVietnamese(storeName));
            buffer.AddRange(CMD_SIZE_WIDE);
            AddTextLine(buffer, "TEM PHA CHE");
            buffer.AddRange(CMD_SIZE_NORMAL);
            buffer.AddRange(CMD_BOLD_OFF);
            AddTextLine(buffer, DashedLine());

            buffer.AddRange(CMD_ALIGN_LEFT);
            AddTextLine(buffer, $"Don: #{order.OrderId}".PadRight(22) + $"Ly: {cupNo}/{totalCups}");
            AddTextLine(buffer, $"Gio: {order.CreatedAt:HH:mm dd/MM/yyyy}");
            AddTextLine(buffer, $"Loai: {GetOrderTypeName(order.OrderTypeId)}");
            AddTextLine(buffer, DashedLine());

            buffer.AddRange(CMD_ALIGN_CENTER);
            buffer.AddRange(CMD_SIZE_WIDE);
            buffer.AddRange(CMD_BOLD_ON);
            foreach (var line in WrapText(StripVietnamese(detail.DrinkName ?? "Mon"), 21))
            {
                AddTextLine(buffer, line);
            }
            buffer.AddRange(CMD_SIZE_NORMAL);
            buffer.AddRange(CMD_BOLD_OFF);
            AddTextLine(buffer, "");

            buffer.AddRange(CMD_ALIGN_LEFT);
            AddTextLine(buffer, $"Size    : {StripVietnamese(detail.SizeName ?? "Mac dinh")}");

            var note = StripVietnamese(detail.Note ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(note))
            {
                foreach (var line in WrapText("Tuy chon: " + note, LINE_WIDTH))
                {
                    AddTextLine(buffer, line);
                }
            }

            var toppingNames = new List<string>();
            if (detail.OrderToppings != null)
            {
                foreach (var topping in detail.OrderToppings)
                {
                    if (!string.IsNullOrWhiteSpace(topping.ToppingName))
                        toppingNames.Add(StripVietnamese(topping.ToppingName));
                }
            }

            if (toppingNames.Count > 0)
            {
                foreach (var line in WrapText("Topping : " + string.Join(", ", toppingNames), LINE_WIDTH))
                {
                    AddTextLine(buffer, line);
                }
            }

            AddTextLine(buffer, $"Thu ngan: {StripVietnamese(cashierName)}");
            AddTextLine(buffer, DashedLine());

            buffer.AddRange(CMD_ALIGN_CENTER);
            AddTextLine(buffer, "DAN TEM LEN LY");
            AddTextLine(buffer, "");
            buffer.AddRange(CMD_CUT);
        }

        private static void AddTextLine(List<byte> buffer, string text)
        {
            buffer.AddRange(TextToBytes(text));
            buffer.AddRange(CMD_LF);
        }

        /// <summary>Convert text to bytes using Codepage 437</summary>
        private static byte[] TextToBytes(string text)
        {
            return Encoding.GetEncoding(437).GetBytes(text);
        }

        /// <summary>Dashed separator line (42 chars for 80mm paper)</summary>
        private static string DashedLine()
        {
            return new string('-', LINE_WIDTH);
        }

        /// <summary>
        /// Format 3 columns: Item name (left), Qty (center), Price (right).
        /// Layout: [name........] [qty] [...price]
        /// </summary>
        private static string FormatColumns(string name, string qty, string price)
        {
            int priceWidth = 10;
            int qtyWidth = 4;
            int nameWidth = LINE_WIDTH - priceWidth - qtyWidth;

            // Truncate name if too long
            if (name.Length > nameWidth)
                name = name.Substring(0, nameWidth - 1) + ".";

            return name.PadRight(nameWidth) + qty.PadLeft(qtyWidth) + price.PadLeft(priceWidth);
        }

        /// <summary>Format label + value right-aligned across full line width</summary>
        private static string FormatRight(string label, string value)
        {
            int valueWidth = Math.Max(value.Length, 12);
            int labelWidth = LINE_WIDTH - valueWidth;
            if (label.Length > labelWidth)
                label = label.Substring(0, labelWidth);
            return label.PadRight(labelWidth) + value.PadLeft(valueWidth);
        }

        /// <summary>Format money as "xxx,xxxd" (no decimals, suffix d for VND)</summary>
        private static string FormatMoney(decimal amount)
        {
            return $"{amount:#,##0}d";
        }

        private static IEnumerable<string> WrapText(string text, int width)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                yield return "";
                yield break;
            }

            var remaining = text.Trim();
            while (remaining.Length > width)
            {
                var splitAt = remaining.LastIndexOf(' ', width);
                if (splitAt <= 0)
                    splitAt = width;

                yield return remaining.Substring(0, splitAt).TrimEnd();
                remaining = remaining.Substring(splitAt).TrimStart();
            }

            if (remaining.Length > 0)
                yield return remaining;
        }

        /// <summary>Map OrderTypeId to display name (ASCII, no diacritics)</summary>
        private static string GetOrderTypeName(int orderTypeId) => orderTypeId switch
        {
            1 => "Tai cho",
            2 => "Mang di",
            3 => "Giao hang",
            _ => "Khac"
        };

        /// <summary>
        /// Strip Vietnamese diacritics — máy in nhiệt giá rẻ chỉ hỗ trợ ASCII/CP437.
        /// Ví dụ: "Cà phê sữa đá" → "Ca phe sua da"
        /// </summary>
        private static string StripVietnamese(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            var normalized = input.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalized.Length);

            foreach (char c in normalized)
            {
                var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (category != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }

            // Handle special Vietnamese characters not covered by NFD decomposition
            var result = sb.ToString();
            result = result.Replace('đ', 'd').Replace('Đ', 'D');

            return result;
        }
    }
}
