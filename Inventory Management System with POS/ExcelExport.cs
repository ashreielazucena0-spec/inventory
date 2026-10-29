using System;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Inventory_Management_System_with_POS
{
    /// <summary>
    /// Exports a DataTable to a formatted .xlsx file (title, styled header, borders, zebra rows,
    /// frozen header, filter buttons, auto column widths, landscape print setup).
    /// Needs no Excel installation and no NuGet package.
    /// Requires a reference to System.IO.Compression (.NET Framework 4.5+).
    /// </summary>
    public static class ExcelExport
    {
        private const int HeaderRow = 4;
        private const int StTitle = 1, StSub = 2, StHead = 3;
        private static readonly int[] KindStyle = { 4, 6, 8, 10, 12 };   // text, integer, decimal, date, date+time

        // ------------------------------------------------------------------ public API
        public static DataTable FromGrid(DataGridView g)
        {
            var cols = g.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).OrderBy(c => c.DisplayIndex).ToList();
            var dt = new DataTable();
            var types = new Type[cols.Count];

            for (int i = 0; i < cols.Count; i++)
            {
                Type t = null;
                bool mixed = false;
                foreach (DataGridViewRow r in g.Rows)
                {
                    if (r.IsNewRow) continue;
                    object v = r.Cells[cols[i].Index].Value;
                    if (v == null || v == DBNull.Value) continue;
                    Type vt = v.GetType();
                    if (t == null) t = vt;
                    else if (t != vt) { mixed = true; break; }
                }
                bool numericOrDate = t == typeof(int) || t == typeof(long) || t == typeof(decimal) || t == typeof(double) || t == typeof(DateTime);
                types[i] = (numericOrDate && !mixed) ? t : typeof(string);

                string name = string.IsNullOrWhiteSpace(cols[i].HeaderText) ? "Column" + (i + 1) : cols[i].HeaderText;
                string unique = name;
                int n = 2;
                while (dt.Columns.Contains(unique)) unique = name + " (" + (n++) + ")";
                dt.Columns.Add(unique, types[i]);
            }

            foreach (DataGridViewRow r in g.Rows)
            {
                if (r.IsNewRow) continue;
                var vals = new object[cols.Count];
                for (int i = 0; i < cols.Count; i++)
                {
                    object v = r.Cells[cols[i].Index].Value;
                    if (v == null || v == DBNull.Value) vals[i] = DBNull.Value;
                    else vals[i] = types[i] == typeof(string) ? (object)Convert.ToString(v, CultureInfo.CurrentCulture) : v;
                }
                dt.Rows.Add(vals);
            }
            return dt;
        }

        public static void Export(DataTable dt, string title, string subtitle, string fileName)
        {
            if (dt == null || dt.Columns.Count == 0 || dt.Rows.Count == 0)
            {
                Ui.Warn("There is no data to export.");
                return;
            }

            string path;
            using (var dlg = new SaveFileDialog())
            {
                dlg.Filter = "Excel Workbook (*.xlsx)|*.xlsx";
                dlg.FileName = SafeFileName(fileName) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".xlsx";
                if (dlg.ShowDialog() != DialogResult.OK) return;
                path = dlg.FileName;
            }

            try
            {
                Write(path, dt, title ?? "Report", subtitle ?? "");
            }
            catch (IOException)
            {
                Ui.Error("Could not save the file. If it is open in Excel, close it and try again.");
                return;
            }
            catch (Exception ex)
            {
                Ui.Error(ex);
                return;
            }

            if (Ui.Confirm("Excel file saved:\n" + path + "\n\nOpen it now?"))
            {
                try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
                catch { }
            }
        }

        // ------------------------------------------------------------------ workbook
        private static void Write(string path, DataTable dt, string title, string subtitle)
        {
            string sheetName = SheetName(title);
            string lastCol = ColName(dt.Columns.Count - 1);
            int lastRow = HeaderRow + dt.Rows.Count;

            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                AddEntry(zip, "[Content_Types].xml",
                    "<?xml version='1.0' encoding='UTF-8' standalone='yes'?>" +
                    "<Types xmlns='http://schemas.openxmlformats.org/package/2006/content-types'>" +
                    "<Default Extension='rels' ContentType='application/vnd.openxmlformats-package.relationships+xml'/>" +
                    "<Default Extension='xml' ContentType='application/xml'/>" +
                    "<Override PartName='/xl/workbook.xml' ContentType='application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml'/>" +
                    "<Override PartName='/xl/worksheets/sheet1.xml' ContentType='application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml'/>" +
                    "<Override PartName='/xl/styles.xml' ContentType='application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml'/>" +
                    "</Types>");

                AddEntry(zip, "_rels/.rels",
                    "<?xml version='1.0' encoding='UTF-8' standalone='yes'?>" +
                    "<Relationships xmlns='http://schemas.openxmlformats.org/package/2006/relationships'>" +
                    "<Relationship Id='rId1' Type='http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument' Target='xl/workbook.xml'/>" +
                    "</Relationships>");

                string quoted = "'" + Esc(sheetName) + "'";
                AddEntry(zip, "xl/workbook.xml",
                    "<?xml version='1.0' encoding='UTF-8' standalone='yes'?>" +
                    "<workbook xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main' xmlns:r='http://schemas.openxmlformats.org/officeDocument/2006/relationships'>" +
                    "<sheets><sheet name='" + Esc(sheetName) + "' sheetId='1' r:id='rId1'/></sheets>" +
                    "<definedNames>" +
                    "<definedName name='_xlnm._FilterDatabase' localSheetId='0' hidden='1'>" + quoted + "!$A$" + HeaderRow + ":$" + lastCol + "$" + lastRow + "</definedName>" +
                    "<definedName name='_xlnm.Print_Titles' localSheetId='0'>" + quoted + "!$" + HeaderRow + ":$" + HeaderRow + "</definedName>" +
                    "</definedNames></workbook>");

                AddEntry(zip, "xl/_rels/workbook.xml.rels",
                    "<?xml version='1.0' encoding='UTF-8' standalone='yes'?>" +
                    "<Relationships xmlns='http://schemas.openxmlformats.org/package/2006/relationships'>" +
                    "<Relationship Id='rId1' Type='http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet' Target='worksheets/sheet1.xml'/>" +
                    "<Relationship Id='rId2' Type='http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles' Target='styles.xml'/>" +
                    "</Relationships>");

                AddEntry(zip, "xl/styles.xml", BuildStyles());
                AddEntry(zip, "xl/worksheets/sheet1.xml", BuildSheet(dt, title, subtitle, lastCol, lastRow));
            }
        }

        private static string BuildSheet(DataTable dt, string title, string subtitle, string lastCol, int lastRow)
        {
            int cols = dt.Columns.Count;
            var kinds = new int[cols];
            var widths = new double[cols];

            for (int c = 0; c < cols; c++)
            {
                kinds[c] = KindOf(dt, c);
                widths[c] = dt.Columns[c].ColumnName.Length + 5;       // room for the filter button
                foreach (DataRow r in dt.Rows)
                    widths[c] = Math.Max(widths[c], DisplayLength(r[c], kinds[c]) + 2);
                widths[c] = Math.Max(10, Math.Min(60, widths[c]));
            }

            var sb = new StringBuilder();
            sb.Append("<?xml version='1.0' encoding='UTF-8' standalone='yes'?>");
            sb.Append("<worksheet xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main'>");
            sb.Append("<sheetPr><pageSetUpPr fitToPage='1'/></sheetPr>");
            sb.Append("<dimension ref='A1:" + lastCol + (lastRow + 2) + "'/>");
            sb.Append("<sheetViews><sheetView showGridLines='0' workbookViewId='0'>");
            sb.Append("<pane ySplit='" + HeaderRow + "' topLeftCell='A" + (HeaderRow + 1) + "' activePane='bottomLeft' state='frozen'/>");
            sb.Append("<selection pane='bottomLeft' activeCell='A" + (HeaderRow + 1) + "' sqref='A" + (HeaderRow + 1) + "'/>");
            sb.Append("</sheetView></sheetViews>");
            sb.Append("<sheetFormatPr defaultRowHeight='15'/>");

            sb.Append("<cols>");
            for (int c = 0; c < cols; c++)
                sb.Append("<col min='" + (c + 1) + "' max='" + (c + 1) + "' width='" + widths[c].ToString("0.##", CultureInfo.InvariantCulture) + "' customWidth='1'/>");
            sb.Append("</cols>");

            sb.Append("<sheetData>");

            // title + subtitle
            sb.Append("<row r='1' ht='30' customHeight='1'>" + StrCell("A1", StTitle, title) + "</row>");
            if (subtitle.Length > 0)
                sb.Append("<row r='2' ht='18' customHeight='1'>" + StrCell("A2", StSub, subtitle) + "</row>");

            // header
            sb.Append("<row r='" + HeaderRow + "' ht='26' customHeight='1'>");
            for (int c = 0; c < cols; c++)
                sb.Append(StrCell(ColName(c) + HeaderRow, StHead, dt.Columns[c].ColumnName));
            sb.Append("</row>");

            // data
            for (int i = 0; i < dt.Rows.Count; i++)
            {
                int rowNo = HeaderRow + 1 + i;
                int alt = i % 2;
                sb.Append("<row r='" + rowNo + "'>");
                for (int c = 0; c < cols; c++)
                {
                    string cell = ColName(c) + rowNo;
                    int style = KindStyle[kinds[c]] + alt;
                    object v = dt.Rows[i][c];

                    if (v == null || v == DBNull.Value)
                    {
                        sb.Append("<c r='" + cell + "' s='" + style + "'/>");
                    }
                    else if (kinds[c] == 1 || kinds[c] == 2)
                    {
                        sb.Append("<c r='" + cell + "' s='" + style + "'><v>" + Convert.ToString(v, CultureInfo.InvariantCulture) + "</v></c>");
                    }
                    else if (kinds[c] == 3 || kinds[c] == 4)
                    {
                        sb.Append("<c r='" + cell + "' s='" + style + "'><v>" + ((DateTime)v).ToOADate().ToString("R", CultureInfo.InvariantCulture) + "</v></c>");
                    }
                    else
                    {
                        sb.Append(StrCell(cell, style, Convert.ToString(v, CultureInfo.CurrentCulture)));
                    }
                }
                sb.Append("</row>");
            }

            // footer
            string footer = "Total records: " + dt.Rows.Count + "   |   Exported " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") +
                            (string.IsNullOrEmpty(Session.UserName) ? "" : " by " + Session.UserName);
            sb.Append("<row r='" + (lastRow + 2) + "'>" + StrCell("A" + (lastRow + 2), StSub, footer) + "</row>");
            sb.Append("</sheetData>");

            sb.Append("<autoFilter ref='A" + HeaderRow + ":" + lastCol + lastRow + "'/>");
            if (cols > 1)
            {
                sb.Append("<mergeCells count='" + (subtitle.Length > 0 ? 2 : 1) + "'>");
                sb.Append("<mergeCell ref='A1:" + lastCol + "1'/>");
                if (subtitle.Length > 0) sb.Append("<mergeCell ref='A2:" + lastCol + "2'/>");
                sb.Append("</mergeCells>");
            }
            sb.Append("<pageMargins left='0.4' right='0.4' top='0.6' bottom='0.6' header='0.3' footer='0.3'/>");
            sb.Append("<pageSetup orientation='landscape' fitToWidth='1' fitToHeight='0'/>");
            sb.Append("</worksheet>");
            return sb.ToString();
        }

        // ------------------------------------------------------------------ styles
        // cellXfs ids: 0 default | 1 title | 2 subtitle/footer | 3 header |
        // 4/5 text | 6/7 integer | 8/9 decimal | 10/11 date | 12/13 date+time   (odd of each pair = zebra row)
        private static string BuildStyles()
        {
            const string left = "<alignment vertical='center' wrapText='1'/>";
            const string num = "<alignment vertical='center'/>";
            const string mid = "<alignment horizontal='center' vertical='center'/>";
            const string head = "<alignment horizontal='center' vertical='center' wrapText='1'/>";

            var sb = new StringBuilder();
            sb.Append("<?xml version='1.0' encoding='UTF-8' standalone='yes'?>");
            sb.Append("<styleSheet xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main'>");
            sb.Append("<numFmts count='2'>" +
                      "<numFmt numFmtId='164' formatCode='yyyy\\-mm\\-dd'/>" +
                      "<numFmt numFmtId='165' formatCode='yyyy\\-mm\\-dd\\ hh:mm'/></numFmts>");
            sb.Append("<fonts count='4'>" +
                      "<font><sz val='11'/><name val='Calibri'/></font>" +
                      "<font><b/><sz val='16'/><color rgb='FF1F3864'/><name val='Calibri'/></font>" +
                      "<font><i/><sz val='10'/><color rgb='FF595959'/><name val='Calibri'/></font>" +
                      "<font><b/><sz val='11'/><color rgb='FFFFFFFF'/><name val='Calibri'/></font></fonts>");
            sb.Append("<fills count='4'>" +
                      "<fill><patternFill patternType='none'/></fill>" +
                      "<fill><patternFill patternType='gray125'/></fill>" +
                      "<fill><patternFill patternType='solid'><fgColor rgb='FF1F4E78'/><bgColor indexed='64'/></patternFill></fill>" +
                      "<fill><patternFill patternType='solid'><fgColor rgb='FFEAF1F8'/><bgColor indexed='64'/></patternFill></fill></fills>");
            sb.Append("<borders count='2'>" +
                      "<border><left/><right/><top/><bottom/><diagonal/></border>" +
                      "<border><left style='thin'><color rgb='FFBFBFBF'/></left><right style='thin'><color rgb='FFBFBFBF'/></right>" +
                      "<top style='thin'><color rgb='FFBFBFBF'/></top><bottom style='thin'><color rgb='FFBFBFBF'/></bottom><diagonal/></border></borders>");
            sb.Append("<cellStyleXfs count='1'><xf numFmtId='0' fontId='0' fillId='0' borderId='0'/></cellStyleXfs>");
            sb.Append("<cellXfs count='14'>");
            sb.Append(Xf(0, 0, 0, 0, ""));
            sb.Append(Xf(0, 1, 0, 0, num));
            sb.Append(Xf(0, 2, 0, 0, num));
            sb.Append(Xf(0, 3, 2, 1, head));
            sb.Append(Xf(0, 0, 0, 1, left));  sb.Append(Xf(0, 0, 3, 1, left));
            sb.Append(Xf(3, 0, 0, 1, num));   sb.Append(Xf(3, 0, 3, 1, num));
            sb.Append(Xf(4, 0, 0, 1, num));   sb.Append(Xf(4, 0, 3, 1, num));
            sb.Append(Xf(164, 0, 0, 1, mid)); sb.Append(Xf(164, 0, 3, 1, mid));
            sb.Append(Xf(165, 0, 0, 1, mid)); sb.Append(Xf(165, 0, 3, 1, mid));
            sb.Append("</cellXfs>");
            sb.Append("<cellStyles count='1'><cellStyle name='Normal' xfId='0' builtinId='0'/></cellStyles>");
            sb.Append("</styleSheet>");
            return sb.ToString();
        }

        private static string Xf(int numFmt, int font, int fill, int border, string align)
        {
            return "<xf numFmtId='" + numFmt + "' fontId='" + font + "' fillId='" + fill + "' borderId='" + border +
                   "' xfId='0' applyNumberFormat='1' applyFont='1' applyFill='1' applyBorder='1' applyAlignment='1'>" + align + "</xf>";
        }

        // ------------------------------------------------------------------ helpers
        /// <summary>0 text, 1 integer, 2 decimal, 3 date, 4 date + time.</summary>
        private static int KindOf(DataTable dt, int c)
        {
            Type t = dt.Columns[c].DataType;
            if (t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte) ||
                t == typeof(uint) || t == typeof(ulong) || t == typeof(ushort)) return 1;
            if (t == typeof(decimal) || t == typeof(double) || t == typeof(float)) return 2;
            if (t == typeof(DateTime))
            {
                foreach (DataRow r in dt.Rows)
                    if (r[c] != DBNull.Value && ((DateTime)r[c]).TimeOfDay != TimeSpan.Zero) return 4;
                return 3;
            }
            return 0;
        }

        private static int DisplayLength(object v, int kind)
        {
            if (v == null || v == DBNull.Value) return 0;
            switch (kind)
            {
                case 1: return Convert.ToDecimal(v, CultureInfo.InvariantCulture).ToString("N0", CultureInfo.CurrentCulture).Length;
                case 2: return Convert.ToDecimal(v, CultureInfo.InvariantCulture).ToString("N2", CultureInfo.CurrentCulture).Length;
                case 3: return 10;
                case 4: return 16;
            }
            int max = 0;
            foreach (string line in Convert.ToString(v, CultureInfo.CurrentCulture).Split('\n'))
                max = Math.Max(max, line.Length);
            return max;
        }

        private static string StrCell(string cellRef, int style, string text)
        {
            return "<c r='" + cellRef + "' s='" + style + "' t='inlineStr'><is><t xml:space='preserve'>" + Esc(text) + "</t></is></c>";
        }

        private static string ColName(int index)
        {
            string s = "";
            for (int n = index + 1; n > 0; n = (n - 1) / 26)
                s = (char)('A' + (n - 1) % 26) + s;
            return s;
        }

        private static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length + 8);
            foreach (char ch in s)
            {
                if (ch < 0x20 && ch != '\t' && ch != '\n' && ch != '\r') continue;   // not allowed in XML
                if (ch == 0xFFFE || ch == 0xFFFF) continue;
                switch (ch)
                {
                    case '&': sb.Append("&amp;"); break;
                    case '<': sb.Append("&lt;"); break;
                    case '>': sb.Append("&gt;"); break;
                    case '"': sb.Append("&quot;"); break;
                    case '\'': sb.Append("&apos;"); break;
                    default: sb.Append(ch); break;
                }
            }
            return sb.ToString();
        }

        private static string SheetName(string title)
        {
            string n = new string((title ?? "").Where(ch => "[]:*?/\\'".IndexOf(ch) < 0).ToArray()).Trim();
            if (n.Length == 0) n = "Report";
            return n.Length > 31 ? n.Substring(0, 31) : n;
        }

        private static string SafeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) name = "Export";
            foreach (char bad in Path.GetInvalidFileNameChars()) name = name.Replace(bad, '_');
            return name.Replace(' ', '_');
        }

        private static void AddEntry(ZipArchive zip, string name, string content)
        {
            ZipArchiveEntry e = zip.CreateEntry(name);
            using (var w = new StreamWriter(e.Open(), new UTF8Encoding(false)))
                w.Write(content);
        }
    }
}
