using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.Drawing.Text;

namespace Inventory_Management_System_with_POS
{
    /// <summary>
    /// Prints receipts on 58mm thermal paper.
    /// The receipt is first drawn as a pure black-and-white picture at the printer's real resolution (203 dots per inch)
    /// and then sent 1:1, so every dot the printer burns is exactly one dot of the picture.
    ///
    /// Build the receipt with these helpers (a plain string is printed left-aligned):
    ///   Title("TOP MART")                  big bold store name, centered
    ///   Center("Quezon City")              centered text
    ///   Line()                             dotted rule
    ///   Gap()                              small empty space
    ///   LeftRight("Cashier:", "Admin")     text on the left, text on the right
    ///   Bold("Subtotal", "107.60")         same, in bold
    ///   Big("TOTAL", "107.60")             large bold left/right (the "Sub Total" look)
    ///   ItemHeader()                       Name | Qty | Price heading
    ///   Item("Red Horse", 2, 270m)         one product row (long names wrap onto the next line); price = line total
    ///
    /// Printer58.SampleLines() returns a ready-made example so you can test the look without the database:
    ///   Printer58.Print("Test", Printer58.SampleLines());
    /// </summary>
    public static class Printer58
    {
        public const int Cols = 24;                    // rough guide only: about 24 characters fit per line

        // ---- look (change these to taste) ----
        private const string FontName = "Segoe UI";    // clean sans-serif like the sample receipt (use "Georgia" for the old serif look)
        private const float BodySize = 9f;             // normal text   (bigger number = bigger text)
        private const float TitleSize = 15f;           // store name
        private const float BigSize = 12f;             // TOTAL line
        private const float LineSpacing = 1.4f;        // space between normal lines (1.0 = tight, 1.6 = airy)
        // PRINT LENGTH: your printer squeezes the receipt vertically (the printed receipt came out about half as long as designed).
        // This stretches the receipt down the paper to compensate. 1.0 = no change. Still too short/squeezed? raise it (2.2, 2.5).
        // Letters look too tall and thin? lower it (1.7, 1.5).
        private const float LengthScale = 2.0f;
        private const int FeedDots = 90;               // empty space after the last line so the paper feeds past the cutter

        private const int Dpi = 203;                   // resolution of almost every 58mm thermal printer (use 180 if yours is 180 dpi)
        private const int CanvasWidth = 336;           // dots across the receipt picture (about 42mm). Right edge still cut? try 320 or 304. Too much empty space on the right? try 352
        private const int Pad = 6;                     // dots of empty space on the left and right
        private const int RightMargin = 64;            // dots kept empty on the RIGHT (64 = about 8mm). Details still cut? raise it (80, 96). Too far from the edge? lower it
        private const int UsableWidth = CanvasWidth - Pad - RightMargin;   // text, amounts, rules and centering all stay inside this width
        private const int PaperWidth = 228;            // 58mm in 1/100 inch
        private const float LeftOffset = 6f;           // 1/100 inch from the paper's left edge

        // Darkness: every pixel darker than this (0-255) becomes a black dot. Higher = bolder and blacker.
        // 128 = normal, 190 = dark (default), 220 = very dark. Lower it if letters start to fill in.
        private const int Threshold = 215;

        // line markers (invisible characters put in front of a line by the helpers)
        private const char CenterMark = '\u0001';
        private const char RuleMark = '\u0002';
        private const char TitleMark = '\u0003';
        private const char BigMark = '\u0004';
        private const char ItemMark = '\u0005';
        private const char HeadMark = '\u0006';
        private const char GapMark = '\u0007';
        private const char BoldMark = '\u000E';

        // row kinds
        private const int KLeft = 0, KCenter = 1, KRule = 2, KLeftRight = 3, KItem = 4, KGap = 5;
        // font styles: 0 normal | 1 bold | 2 title | 3 big
        private const int SNormal = 0, SBold = 1, STitle = 2, SBig = 3;

        private class Row
        {
            public int Kind;
            public int Style;
            public string Left, Mid, Right;
            public float Height;
        }

        // ------------------------------------------------------------------ public API
        /// <summary>Receipt text as a list of lines.</summary>
        public static void Print(string title, IEnumerable<string> lines)
        {
            using (Bitmap picture = Render(lines))
                PrintPicture(title, picture);
        }

        private static void PrintPicture(string title, Bitmap picture)
        {
            using (PrintDocument doc = new PrintDocument())
            {
                float widthUnits = picture.Width * 100f / Dpi;       // picture size in 1/100 inch
                float heightUnits = picture.Height * 100f / Dpi * LengthScale;

                doc.DocumentName = title;
                doc.PrintController = new StandardPrintController();  // no "Printing..." popup
                doc.DefaultPageSettings.PaperSize = new PaperSize("Receipt58", PaperWidth, (int)Math.Ceiling(heightUnits) + 10);
                doc.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);

                int offset = 0;                                       // dots of the picture already printed

                doc.PrintPage += (s, e) =>
                {
                    e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;   // no smoothing: keep hard black dots
                    e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;

                    // Some thermal drivers ignore the paper length we ask for and use their own (shorter) page,
                    // which chops off the bottom of a long receipt. So print only as much as one page holds
                    // and continue on the next page until the whole picture (down to CHANGE / THANK YOU) is out.
                    float pageH = e.PageSettings.PrintableArea.Height;
                    if (pageH < 100f) pageH = e.PageBounds.Height;
                    int sliceDots = Math.Max(100, (int)((pageH - 4f) * Dpi / (100f * LengthScale)));

                    int h = Math.Min(sliceDots, picture.Height - offset);
                    RectangleF dest = new RectangleF(LeftOffset, 0f, widthUnits, h * 100f / Dpi * LengthScale);
                    e.Graphics.DrawImage(picture, dest, new RectangleF(0f, offset, picture.Width, h), GraphicsUnit.Pixel);

                    offset += h;
                    e.HasMorePages = offset < picture.Height;
                };
                doc.Print();
            }
        }

        /// <summary>Use this when the receipt text comes as one string with line breaks.</summary>
        public static void Print(string title, string text)
        {
            Print(title, (text ?? "").Replace("\r", "").Split('\n'));
        }

        /// <summary>
        /// Prints a ruler page (about 11cm long) to find out where YOUR printer cuts the receipt:
        /// numbers across the top every 64 dots, thick lines on the left and right edges, "y=" marks down the page
        /// and the word END at the bottom. Call it once:  Printer58.Calibrate();
        /// </summary>
        public static void Calibrate()
        {
            int h = 900;
            using (Bitmap bmp = new Bitmap(CanvasWidth, h + FeedDots, PixelFormat.Format24bppRgb))
            {
                bmp.SetResolution(Dpi, Dpi);
                using (Graphics g = Graphics.FromImage(bmp))
                using (Font small = new Font("Arial", 7f, FontStyle.Bold, GraphicsUnit.Point))
                using (Font big = new Font("Arial", 14f, FontStyle.Bold, GraphicsUnit.Point))
                using (Pen thick = new Pen(Color.Black, 4f))
                using (Pen thin = new Pen(Color.Black, 1f))
                {
                    g.Clear(Color.White);
                    Setup(g);
                    g.DrawLine(thick, 2, 0, 2, h);                                // left edge
                    g.DrawLine(thick, CanvasWidth - 3, 0, CanvasWidth - 3, h);    // right edge
                    for (int x = 0; x <= CanvasWidth; x += 16)
                    {
                        bool major = x % 64 == 0;
                        g.DrawLine(thin, x, 0, x, major ? 30 : 14);
                        if (major && x > 0 && x < CanvasWidth - 20) g.DrawString(x.ToString(), small, Brushes.Black, x + 2, 32);
                    }
                    for (int y = 100; y < h; y += 100)
                    {
                        g.DrawLine(thin, 0, y, CanvasWidth, y);
                        g.DrawString("y=" + y, small, Brushes.Black, 8, y + 2);
                    }
                    g.DrawString("END", big, Brushes.Black, CanvasWidth / 2f - 25, h - 40);
                    g.DrawLine(thick, 0, h - 4, CanvasWidth, h - 4);
                }
                ToBlackAndWhite(bmp);
                PrintPicture("Calibration", bmp);
            }
        }

        // ---- helpers for building receipt lines ----
        public static string Title(string s) { return TitleMark + (s ?? ""); }

        public static string Center(string s) { return CenterMark + (s ?? ""); }

        public static string LeftRight(string left, string right) { return (left ?? "") + "\t" + (right ?? ""); }

        public static string Bold(string left, string right) { return BoldMark + (left ?? "") + "\t" + (right ?? ""); }

        public static string Big(string left, string right) { return BigMark + (left ?? "") + "\t" + (right ?? ""); }

        public static string Line(char c = '-') { return RuleMark.ToString(); }

        public static string Gap() { return GapMark.ToString(); }

        public static string ItemHeader() { return HeadMark + "Name\tQty\tPrice"; }

        /// <summary>One product row. Pass the LINE total as price (qty x unit price - discount).</summary>
        public static string Item(string name, int qty, decimal price)
        {
            return ItemMark + (name ?? "") + "\t" + qty + "\t" + price.ToString("N2");
        }

        /// <summary>Example receipt for testing the look: Printer58.Print("Test", Printer58.SampleLines());</summary>
        public static List<string> SampleLines()
        {
            return new List<string>
            {
                Title("TOP MART"),
                Center("123 Sample Street"),
                Center("Quezon City"),
                Center("Tel: 0912-345-6789"),
                Line(),
                LeftRight("Receipt:", "SALE-000123"),
                LeftRight("Cashier:", "Admin"),
                LeftRight("Date:", DateTime.Now.ToString("MM/dd/yyyy hh:mm tt")),
                Line(),
                ItemHeader(),
                Gap(),
                Item("Mountain Dew (290ml)", 2, 50m),
                Item("Piatos", 1, 25m),
                Item("Red Horse", 3, 405m),
                Item("Nature Spring (500ml)", 1, 10m),
                Gap(),
                Line(),
                LeftRight("Subtotal", "490.00"),
                LeftRight("Discount", "0.00"),
                LeftRight("VAT (12%)", "58.80"),
                Gap(),
                Big("TOTAL", "548.80"),
                Gap(),
                LeftRight("CASH", "600.00"),
                LeftRight("CHANGE", "51.20"),
                Line(),
                Gap(),
                Center("THANK YOU!"),
                Center("Glad to see you again!")
            };
        }

        // ------------------------------------------------------------------ drawing the receipt picture
        private static Bitmap Render(IEnumerable<string> lines)
        {
            using (Font fNormal = new Font(FontName, BodySize, FontStyle.Regular))
            using (Font fBold = new Font(FontName, BodySize, FontStyle.Bold))
            using (Font fTitle = new Font(FontName, TitleSize, FontStyle.Bold))
            using (Font fBig = new Font(FontName, BigSize, FontStyle.Bold))
            {
                Font[] fonts = { fNormal, fBold, fTitle, fBig };

                List<Row> rows;
                using (Bitmap probe = new Bitmap(1, 1))
                using (Graphics g = Graphics.FromImage(probe))
                {
                    probe.SetResolution(Dpi, Dpi);
                    Setup(g);
                    rows = Layout(g, fonts, lines);
                }

                float total = 0f;
                foreach (Row r in rows) total += r.Height;

                int height = (int)Math.Ceiling(total) + 2 * Pad + FeedDots;
                Bitmap bmp = new Bitmap(CanvasWidth, height, PixelFormat.Format24bppRgb);
                bmp.SetResolution(Dpi, Dpi);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.White);
                    Setup(g);
                    float y = Pad;
                    foreach (Row r in rows)
                    {
                        DrawRow(g, fonts, r, y);
                        y += r.Height;
                    }
                }
                ToBlackAndWhite(bmp);
                return bmp;
            }
        }

        private static Graphics Setup(Graphics g)
        {
            g.PageUnit = GraphicsUnit.Pixel;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;   // smooth edges first, then turned into solid dots below
            g.SmoothingMode = SmoothingMode.HighQuality;
            return g;
        }

        /// <summary>Turns every pixel into pure black or pure white. A high threshold also thickens thin strokes.</summary>
        private static void ToBlackAndWhite(Bitmap bmp)
        {
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
            try
            {
                byte[] row = new byte[Math.Abs(data.Stride)];
                for (int y = 0; y < bmp.Height; y++)
                {
                    IntPtr ptr = new IntPtr(data.Scan0.ToInt64() + (long)y * data.Stride);
                    System.Runtime.InteropServices.Marshal.Copy(ptr, row, 0, row.Length);
                    for (int x = 0; x < bmp.Width; x++)
                    {
                        int i = x * 3;
                        int lum = (row[i] + row[i + 1] + row[i + 2]) / 3;
                        byte v = (byte)(lum < Threshold ? 0 : 255);
                        row[i] = v; row[i + 1] = v; row[i + 2] = v;
                    }
                    System.Runtime.InteropServices.Marshal.Copy(row, 0, ptr, row.Length);
                }
            }
            finally { bmp.UnlockBits(data); }
        }

        // ------------------------------------------------------------------ layout
        private static float Width(Graphics g, Font f, string s)
        {
            if (string.IsNullOrEmpty(s)) return 0f;
            s = s.Replace('\t', ' ');
            using (StringFormat sf = new StringFormat(StringFormat.GenericTypographic))
            {
                sf.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
                return g.MeasureString(s, f, 10000, sf).Width;
            }
        }

        private static float LineH(Graphics g, Font f, int style)
        {
            return f.GetHeight(g) * (style >= STitle ? 1.2f : LineSpacing);
        }

        /// <summary>Column positions for the Name | Qty | Price rows (same for the heading and every product).</summary>
        private static void ItemCols(Graphics g, Font f, out float nameW, out float qtyCenter)
        {
            float priceW = Width(g, f, "0,000.00");
            float qtyW = Width(g, f, "00");
            const float gap = 8f;
            nameW = UsableWidth - priceW - qtyW - 2f * gap;
            qtyCenter = Pad + UsableWidth - priceW - gap - qtyW / 2f;
        }

        private static List<Row> Layout(Graphics g, Font[] fonts, IEnumerable<string> lines)
        {
            List<Row> rows = new List<Row>();
            bool firstLine = true;                    // the first centered line (store name) is printed as the big title
            foreach (string raw0 in lines)
            {
                string raw = (raw0 ?? "").TrimEnd();
                char m = raw.Length > 0 ? raw[0] : '\0';

                if (m == RuleMark)
                {
                    rows.Add(new Row { Kind = KRule, Height = LineH(g, fonts[SNormal], SNormal) * 0.9f });
                }
                else if (m == GapMark)
                {
                    rows.Add(new Row { Kind = KGap, Height = LineH(g, fonts[SNormal], SNormal) * 0.6f });
                }
                else if (m == CenterMark || m == TitleMark)
                {
                    int style = (m == TitleMark || (m == CenterMark && firstLine)) ? STitle : SNormal;
                    foreach (string part in WrapText(g, fonts[style], raw.Substring(1), UsableWidth))
                        rows.Add(new Row { Kind = KCenter, Style = style, Left = part, Height = LineH(g, fonts[style], style) });
                }
                else if (m == BigMark || m == BoldMark)
                {
                    AddTabbed(rows, g, fonts, m == BigMark ? SBig : SBold, raw.Substring(1));
                }
                else if (m == ItemMark || m == HeadMark)
                {
                    AddItem(rows, g, fonts, m == HeadMark ? SBold : SNormal, raw.Substring(1));
                }
                else if (raw.IndexOf('\t') >= 0)
                {
                    AddTabbed(rows, g, fonts, SNormal, raw);
                }
                else
                {
                    foreach (string part in WrapText(g, fonts[SNormal], raw, UsableWidth))
                        rows.Add(new Row { Kind = KLeft, Style = SNormal, Left = part, Height = LineH(g, fonts[SNormal], SNormal) });
                }

                if (raw.Length > 0 && m != GapMark) firstLine = false;
            }
            return rows;
        }

        /// <summary>
        /// A line with tabs: "left \t right" is left/right text; "name \t qty \t amount" is a Name | Qty | Price row
        /// (when the middle part is empty it falls back to left/right). The amount is always the LAST part.
        /// Also recognises the old-style "Item" heading (bold) and the TOTAL line (big).
        /// </summary>
        private static void AddTabbed(List<Row> rows, Graphics g, Font[] fonts, int style, string text)
        {
            string[] p = text.Split('\t');
            string left = p[0];
            string right = p[p.Length - 1];
            string mid = p.Length >= 3 ? p[1] : "";
            string key = left.Trim().ToUpperInvariant();

            if (style == SNormal && (key == "TOTAL" || key == "NET TOTAL" || key == "GRAND TOTAL")) style = SBig;

            if (mid.Trim().Length > 0)
            {
                if (style == SNormal && key == "ITEM") style = SBold;
                AddItem(rows, g, fonts, style, left + "\t" + mid + "\t" + right);
            }
            else
            {
                AddLeftRight(rows, g, fonts, style, left + "\t" + right);
            }
        }

        private static void AddLeftRight(List<Row> rows, Graphics g, Font[] fonts, int style, string text)
        {
            Font f = fonts[style];
            int tab = text.IndexOf('\t');
            string left = tab >= 0 ? text.Substring(0, tab) : text;
            string right = tab >= 0 ? text.Substring(tab + 1).Replace('\t', ' ') : "";
            float avail = UsableWidth - Width(g, f, right) - 12f;
            while (left.Length > 0 && Width(g, f, left) > avail) left = left.Substring(0, left.Length - 1);
            rows.Add(new Row { Kind = KLeftRight, Style = style, Left = left, Right = right, Height = LineH(g, f, style) });
        }

        /// <summary>Name | Qty | Price. A long name wraps onto extra lines; qty and price sit on the first one.</summary>
        private static void AddItem(List<Row> rows, Graphics g, Font[] fonts, int style, string text)
        {
            string[] p = text.Split('\t');
            string name = p.Length > 0 ? p[0] : "";
            string qty = p.Length > 1 ? p[1] : "";
            string price = p.Length > 2 ? p[2] : "";

            float nameW, qtyCenter;
            ItemCols(g, fonts[SNormal], out nameW, out qtyCenter);

            bool first = true;
            foreach (string part in WrapText(g, fonts[style], name, nameW))
            {
                rows.Add(new Row
                {
                    Kind = KItem,
                    Style = style,
                    Left = part,
                    Mid = first ? qty : "",
                    Right = first ? price : "",
                    Height = LineH(g, fonts[style], style)
                });
                first = false;
            }
        }

        /// <summary>Wraps text on spaces so no line is wider than maxW (very long words are broken).</summary>
        private static IEnumerable<string> WrapText(Graphics g, Font f, string text, float maxW)
        {
            if (Width(g, f, text) <= maxW) { yield return text; yield break; }

            string line = "";
            foreach (string w in text.Split(' '))
            {
                string word = w;
                string test = line.Length == 0 ? word : line + " " + word;
                if (Width(g, f, test) <= maxW) { line = test; continue; }

                if (line.Length > 0) { yield return line; line = ""; }
                while (word.Length > 1 && Width(g, f, word) > maxW)
                {
                    int n = word.Length - 1;
                    while (n > 1 && Width(g, f, word.Substring(0, n)) > maxW) n--;
                    yield return word.Substring(0, n);
                    word = word.Substring(n);
                }
                line = word;
            }
            if (line.Length > 0) yield return line;
        }

        // ------------------------------------------------------------------ drawing rows
        private static void DrawRow(Graphics g, Font[] fonts, Row r, float y)
        {
            if (r.Kind == KGap) return;

            if (r.Kind == KRule)
            {
                float ym = y + r.Height / 2f;
                using (Pen p = new Pen(Color.Black, 2f))
                {
                    p.DashStyle = DashStyle.Dot;                 // dotted rule like the sample receipt
                    g.DrawLine(p, Pad, ym, Pad + UsableWidth, ym);
                }
                return;
            }

            Font f = fonts[r.Style];

            if (r.Kind == KCenter)
            {
                float w = Width(g, f, r.Left);
                DrawText(g, f, r.Left, Pad + Math.Max(0f, (UsableWidth - w) / 2f), y);
                return;
            }

            DrawText(g, f, r.Left, Pad, y);

            if (r.Kind == KLeftRight)
            {
                float rw = Width(g, f, r.Right);
                DrawText(g, f, r.Right, Pad + UsableWidth - rw, y);
            }
            else if (r.Kind == KItem)
            {
                float nameW, qtyCenter;
                ItemCols(g, fonts[SNormal], out nameW, out qtyCenter);
                if (!string.IsNullOrEmpty(r.Mid))
                    DrawText(g, f, r.Mid, qtyCenter - Width(g, f, r.Mid) / 2f, y);
                if (!string.IsNullOrEmpty(r.Right))
                    DrawText(g, f, r.Right, Pad + UsableWidth - Width(g, f, r.Right), y);
            }
        }

        private static void DrawText(Graphics g, Font f, string text, float x, float y)
        {
            if (string.IsNullOrEmpty(text)) return;
            text = text.Replace('\t', ' ');
            using (StringFormat sf = new StringFormat(StringFormat.GenericTypographic))
            {
                sf.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
                g.DrawString(text, f, Brushes.Black, x, y, sf);
            }
        }
    }
}