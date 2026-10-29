using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Inventory_Management_System_with_POS
{
    /// <summary>Turns cell values into text for printing / CSV.</summary>
    public static class Fmt
    {
        public static string Cell(object v)
        {
            if (v == null || v == DBNull.Value) return "";
            if (v is decimal) return ((decimal)v).ToString("N2", CultureInfo.CurrentCulture);
            if (v is double) return ((double)v).ToString("N2", CultureInfo.CurrentCulture);
            if (v is DateTime)
            {
                DateTime d = (DateTime)v;
                return d.TimeOfDay == TimeSpan.Zero ? d.ToString("yyyy-MM-dd") : d.ToString("yyyy-MM-dd HH:mm");
            }
            return v.ToString();
        }
    }

    public static class Printer
    {
        /// <summary>Shows a print preview (with a Print button) for plain text lines.</summary>
        public static void PrintText(string title, IList<string> lines, float fontSize = 9f, bool landscape = false)
        {
            int index = 0;
            using (var font = new Font(FontFamily.GenericMonospace, fontSize))
            using (var doc = new PrintDocument())
            {
                doc.DocumentName = title;
                doc.DefaultPageSettings.Landscape = landscape;
                doc.DefaultPageSettings.Margins = new Margins(30, 30, 30, 30);
                doc.BeginPrint += (s, e) => index = 0;
                doc.PrintPage += (s, e) =>
                {
                    float y = e.MarginBounds.Top;
                    float lh = font.GetHeight(e.Graphics);
                    while (index < lines.Count && y + lh <= e.MarginBounds.Bottom)
                    {
                        e.Graphics.DrawString(lines[index], font, Brushes.Black, e.MarginBounds.Left, y);
                        y += lh;
                        index++;
                    }
                    e.HasMorePages = index < lines.Count;
                };
                using (var dlg = new PrintPreviewDialog())
                {
                    dlg.Document = doc;
                    dlg.Width = 900;
                    dlg.Height = 700;
                    dlg.StartPosition = FormStartPosition.CenterScreen;
                    dlg.ShowDialog();
                }
            }
        }

        /// <summary>Formats a DataTable as fixed-width text and previews it for printing.</summary>
        public static void PrintTable(string title, string subtitle, DataTable dt)
        {
            var lines = new List<string>();
            lines.Add("TOP MART");
            lines.Add(title);
            if (!string.IsNullOrEmpty(subtitle)) lines.Add(subtitle);
            lines.Add("Printed " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " by " + (Session.UserName ?? ""));
            lines.Add("");

            int cols = dt.Columns.Count;
            var widths = new int[cols];
            for (int c = 0; c < cols; c++)
            {
                widths[c] = dt.Columns[c].ColumnName.Length;
                foreach (DataRow r in dt.Rows)
                    widths[c] = Math.Max(widths[c], Fmt.Cell(r[c]).Length);
                widths[c] = Math.Min(widths[c], 28);
            }

            Func<Func<int, string>, string> line = cell =>
            {
                var sb = new StringBuilder();
                for (int c = 0; c < cols; c++)
                {
                    string s = cell(c);
                    if (s.Length > widths[c]) s = s.Substring(0, widths[c]);
                    bool right = dt.Columns[c].DataType == typeof(decimal) || dt.Columns[c].DataType == typeof(int)
                              || dt.Columns[c].DataType == typeof(long) || dt.Columns[c].DataType == typeof(double);
                    sb.Append(right ? s.PadLeft(widths[c]) : s.PadRight(widths[c]));
                    sb.Append("  ");
                }
                return sb.ToString().TrimEnd();
            };

            string header = line(c => dt.Columns[c].ColumnName);
            lines.Add(header);
            lines.Add(new string('-', header.Length));
            foreach (DataRow r in dt.Rows)
            {
                DataRow row = r;
                lines.Add(line(c => Fmt.Cell(row[c])));
            }
            lines.Add(new string('-', header.Length));
            lines.Add(dt.Rows.Count + " record(s)");

            int widest = 0;
            foreach (string l in lines) widest = Math.Max(widest, l.Length);
            PrintText(title, lines, widest > 110 ? 7f : (widest > 90 ? 8f : 9f), widest > 80);
        }
    }

    public static class Csv
    {
        /// <summary>Saves a DataTable as a .csv file that opens directly in Excel. Returns true if saved.</summary>
        public static bool Export(DataTable dt, string defaultName)
        {
            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "CSV file (opens in Excel) (*.csv)|*.csv";
                sfd.FileName = defaultName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".csv";
                if (sfd.ShowDialog() != DialogResult.OK) return false;
                try
                {
                    using (var w = new StreamWriter(sfd.FileName, false, new UTF8Encoding(true)))
                    {
                        var head = new List<string>();
                        foreach (DataColumn c in dt.Columns) head.Add(Escape(c.ColumnName));
                        w.WriteLine(string.Join(",", head));
                        foreach (DataRow r in dt.Rows)
                        {
                            var cells = new List<string>();
                            for (int i = 0; i < dt.Columns.Count; i++)
                            {
                                object v = r[i];
                                string s;
                                if (v is decimal) s = ((decimal)v).ToString("0.00", CultureInfo.InvariantCulture);
                                else if (v is DateTime) s = Fmt.Cell(v);
                                else s = Fmt.Cell(v);
                                cells.Add(Escape(s));
                            }
                            w.WriteLine(string.Join(",", cells));
                        }
                    }
                    Ui.Info("Saved:\n" + sfd.FileName, "Export");
                    return true;
                }
                catch (Exception ex)
                {
                    Ui.Error("Could not save the file.\n" + ex.Message);
                    return false;
                }
            }
        }

        private static string Escape(string s)
        {
            if (s == null) return "";
            if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0) return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }
    }

    /// <summary>Simple grid window for any table, with Print and Export buttons.</summary>
    public class ReportPreviewForm : Form
    {
        private readonly DataTable _data;
        private readonly string _title;
        private readonly string _subtitle;

        public ReportPreviewForm(string title, string subtitle, DataTable data)
        {
            _data = data;
            _title = title;
            _subtitle = subtitle;

            Text = title;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1050, 600);
            MinimumSize = new Size(700, 400);

            var top = new Panel { Dock = DockStyle.Top, Height = 62 };
            top.Controls.Add(new Label { Text = title, Left = 12, Top = 8, Width = 1000, Height = 26, Font = new Font("Segoe UI", 13f, FontStyle.Bold) });
            top.Controls.Add(new Label { Text = subtitle ?? "", Left = 12, Top = 36, Width = 1000, Height = 20 });

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 46 };
            var count = new Label { Text = data.Rows.Count + " record(s)", Dock = DockStyle.Left, Width = 300, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(12, 0, 0, 0) };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 340, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 8, 8, 0) };
            var close = new Button { Text = "Close", Width = 90, Height = 28, DialogResult = DialogResult.Cancel };
            var export = new Button { Text = "Export to Excel", Width = 120, Height = 28 };
            var print = new Button { Text = "Print", Width = 90, Height = 28 };
            print.Click += (s, e) => Printer.PrintTable(_title, _subtitle, _data);
            export.Click += (s, e) => Csv.Export(_data, _title.Replace(' ', '_'));
            buttons.Controls.Add(close);
            buttons.Controls.Add(export);
            buttons.Controls.Add(print);
            bottom.Controls.Add(count);
            bottom.Controls.Add(buttons);
            CancelButton = close;

            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells,
                BackgroundColor = SystemColors.Window
            };
            grid.DataSource = data;
            Ui.FormatGrid(grid, data);

            Controls.Add(grid);
            Controls.Add(top);
            Controls.Add(bottom);
        }

        public static void Show(string title, string subtitle, DataTable data)
        {
            using (var f = new ReportPreviewForm(title, subtitle, data)) f.ShowDialog();
        }
    }
}
