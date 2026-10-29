using System;
using System.Collections.Generic;
using System.Data;
using System.Windows.Forms;

namespace Inventory_Management_System_with_POS
{
    /// <summary>
    /// Manage Reports: builds sales and inventory reports live from the database, previews / prints / exports them.
    /// The schema has no report table, so the list of generated reports (ReportStore) lasts for the running session.
    ///
    /// Control map:  comboBox1 Report type | dateTimePicker1/2 Date range | comboBox2 Category | comboBox3 Supplier |
    /// textBox10 Search history | textBox1 Report ID | dateTimePicker4 Generated date |
    /// textBox4 total generated | textBox2 last generated | textBox3 most frequent type.
    /// </summary>
    public partial class frmManageReports : Form
    {
        public frmManageReports()
        {
            InitializeComponent();
            Load += new EventHandler(frmManageReports_Load);
        }

        private void frmManageReports_Load(object sender, EventArgs e)
        {
            if (!Session.CanAccess(Feature.Reports))
            {
                Ui.Warn("Only a Manager or Admin can create reports.", "Access Denied");
                BeginInvoke(new Action(Close));
                return;
            }

            Ui.SetupGrid(dataGridView1);
            textBox1.ReadOnly = true;
            textBox2.ReadOnly = true;
            textBox3.ReadOnly = true;
            textBox4.ReadOnly = true;
            dateTimePicker4.Enabled = false;

            comboBox1.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBox1.Items.Clear();
            comboBox1.Items.AddRange(ReportService.Types);
            comboBox2.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBox3.DropDownStyle = ComboBoxStyle.DropDownList;

            dateTimePicker1.Value = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            dateTimePicker2.Value = DateTime.Today;

           // Wire(button1, delegate { ClearFilters(null, EventArgs.Empty); comboBox1.Focus(); });   // Create Report (top tab)
          //  Wire(button2, ViewSelected);                                                            // View Report
           // Wire(button3, PrintSelected);                                                           // Print / Export
            Wire(button4, ClearFilters);                                                            // CLEAR
            Wire(button5, DeleteSelected);                                                          // DELETE REPORT
            Wire(button6, ExportSelected);                                                          // EXPORT TO EXCEL
            Wire(button7, RefreshSelected);                                                         // ACTIVATE REPORT
            Wire(button8, Generate);                                                                // GENERATE REPORT

            comboBox1.SelectedIndexChanged += (s, a) => UpdateFilterAvailability();
            textBox10.TextChanged += (s, a) => LoadHistory();
            dataGridView1.SelectionChanged += (s, a) => ShowSelectedSummary();
            dataGridView1.CellDoubleClick += (s, a) => { if (a.RowIndex >= 0) ViewSelected(s, a); };

            try
            {
                LoadFilterLists();
            }
            catch (Exception ex)
            {
                Ui.Error(ex, "Could not load filters");
            }
            ClearFilters(null, EventArgs.Empty);
            LoadHistory();
        }

        private static void Wire(Button b, EventHandler h)
        {
            b.Click -= h;
            b.Click += h;
        }

        // ------------------------------------------------------------------ filters
        private void LoadFilterLists()
        {
            DataTable cat = Db.Query("SELECT category_id, category_name FROM tbl_category ORDER BY category_name");
            DataRow all = cat.NewRow(); all["category_id"] = ""; all["category_name"] = "All Categories";
            cat.Rows.InsertAt(all, 0);
            comboBox2.DisplayMember = "category_name"; comboBox2.ValueMember = "category_id"; comboBox2.DataSource = cat;

            DataTable sup = Db.Query("SELECT supplier_id, supplier_name FROM tbl_supplier ORDER BY supplier_name");
            DataRow allS = sup.NewRow(); allS["supplier_id"] = ""; allS["supplier_name"] = "All Suppliers";
            sup.Rows.InsertAt(allS, 0);
            comboBox3.DisplayMember = "supplier_name"; comboBox3.ValueMember = "supplier_id"; comboBox3.DataSource = sup;
        }

        private void ClearFilters(object sender, EventArgs e)
        {
            comboBox1.SelectedIndex = -1;
            if (comboBox2.Items.Count > 0) comboBox2.SelectedIndex = 0;
            if (comboBox3.Items.Count > 0) comboBox3.SelectedIndex = 0;
            dateTimePicker1.Value = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            dateTimePicker2.Value = DateTime.Today;
            UpdateFilterAvailability();
        }

        /// <summary>Only the filters that the chosen report uses stay enabled.</summary>
        private void UpdateFilterAvailability()
        {
            string t = comboBox1.SelectedItem as string;
            bool any = t != null;
            dateTimePicker1.Enabled = any && ReportService.UsesDates(t);
            dateTimePicker2.Enabled = any && ReportService.UsesDates(t);
            comboBox2.Enabled = any && ReportService.UsesCategory(t);
            comboBox3.Enabled = any && ReportService.UsesSupplier(t);
        }

        private string CategoryId { get { return comboBox2.SelectedValue == null ? "" : comboBox2.SelectedValue.ToString(); } }
        private string SupplierId { get { return comboBox3.SelectedValue == null ? "" : comboBox3.SelectedValue.ToString(); } }
        private string CategoryName { get { return CategoryId.Length == 0 ? "" : comboBox2.Text; } }
        private string SupplierName { get { return SupplierId.Length == 0 ? "" : comboBox3.Text; } }

        // ------------------------------------------------------------------ generate
        /// <summary>GENERATE REPORT: runs the chosen report, adds it to the list and shows a preview.</summary>
        private void Generate(object sender, EventArgs e)
        {
            string type = comboBox1.SelectedItem as string;
            if (type == null) { Ui.Warn("Select a report type."); comboBox1.Focus(); return; }
            if (ReportService.UsesDates(type) && dateTimePicker1.Value.Date > dateTimePicker2.Value.Date)
            {
                Ui.Warn("The 'From' date must not be after the 'To' date.");
                return;
            }

            try
            {
                var entry = new ReportEntry
                {
                    Id = ReportStore.NextId(),
                    Type = type,
                    From = dateTimePicker1.Value.Date,
                    To = dateTimePicker2.Value.Date,
                    CategoryId = CategoryId, CategoryName = CategoryName,
                    SupplierId = SupplierId, SupplierName = SupplierName,
                    By = Session.UserName ?? "",
                    Status = "Generated"
                };
                Run(entry);
                ReportStore.Entries.Add(entry);
                LoadHistory(entry.Id);
                ViewEntry(entry);
            }
            catch (Exception ex)
            {
                Ui.Error(ex, "Could not generate the report");
            }
        }

        private static void Run(ReportEntry entry)
        {
            entry.Data = ReportService.Build(entry.Type, entry.From, entry.To, entry.CategoryId, entry.SupplierId);
            entry.GeneratedAt = DateTime.Now;
            entry.Params = ReportService.Describe(entry.Type, entry.From, entry.To, entry.CategoryName, entry.SupplierName);
        }

        // ------------------------------------------------------------------ list
        private ReportEntry Selected()
        {
            if (dataGridView1.SelectedRows.Count == 0) return null;
            string id = dataGridView1.SelectedRows[0].Cells[0].Value as string;
            return ReportStore.Entries.Find(x => x.Id == id);
        }

        private void LoadHistory(string selectId = null)
        {
            string keep = selectId ?? (Selected() == null ? null : Selected().Id);
            string q = (textBox10.Text ?? "").Trim().ToLower();
            dataGridView1.Rows.Clear();
            for (int i = ReportStore.Entries.Count - 1; i >= 0; i--)       // newest first
            {
                ReportEntry r = ReportStore.Entries[i];
                if (q.Length > 0 && !(r.Id + " " + r.Type + " " + r.Params + " " + r.Status + " " + r.By).ToLower().Contains(q)) continue;
                int idx = dataGridView1.Rows.Add(r.Id, r.GeneratedAt.ToString("yyyy-MM-dd HH:mm"), r.Type, r.Params, r.Status, r.By);
                if (keep != null && r.Id == keep) { dataGridView1.ClearSelection(); dataGridView1.Rows[idx].Selected = true; }
            }
            if (keep == null) dataGridView1.ClearSelection();
            UpdateMetrics();
            ShowSelectedSummary();
        }

        private void ShowSelectedSummary()
        {
            ReportEntry r = Selected();
            if (r == null) { textBox1.Clear(); return; }
            textBox1.Text = r.Id;
            dateTimePicker4.Value = r.GeneratedAt;
        }

        private void UpdateMetrics()
        {
            var all = ReportStore.Entries;
            textBox4.Text = all.Count.ToString();
            if (all.Count == 0) { textBox2.Text = "-"; textBox3.Text = "-"; return; }

            ReportEntry last = all[all.Count - 1];
            textBox2.Text = last.Type + " (" + last.GeneratedAt.ToString("HH:mm") + ")";

            var counts = new Dictionary<string, int>();
            foreach (ReportEntry r in all) { int n; counts.TryGetValue(r.Type, out n); counts[r.Type] = n + 1; }
            string best = null; int bestN = 0;
            foreach (var kv in counts) if (kv.Value > bestN) { best = kv.Key; bestN = kv.Value; }
            textBox3.Text = best + " (" + bestN + "x)";
        }

        // ------------------------------------------------------------------ actions on the selected report
        private ReportEntry RequireSelected()
        {
            ReportEntry r = Selected();
            if (r == null) Ui.Warn("Select a report from the list first.", "No Selection");
            return r;
        }

        private static void ViewEntry(ReportEntry r)
        {
            ReportPreviewForm.Show(r.Type + "  (" + r.Id + ")", r.Params + "   |   generated " + r.GeneratedAt.ToString("yyyy-MM-dd HH:mm") + " by " + r.By, r.Data);
        }

        private void ViewSelected(object sender, EventArgs e)
        {
            ReportEntry r = RequireSelected();
            if (r != null) ViewEntry(r);
        }

        private void PrintSelected(object sender, EventArgs e)
        {
            ReportEntry r = RequireSelected();
            if (r != null) Printer.PrintTable(r.Type, r.Params, r.Data);
        }

       
        /// <summary>ACTIVATE REPORT: re-runs the selected report with its saved filters so it shows current data.</summary>
        private void RefreshSelected(object sender, EventArgs e)
        {
            ReportEntry r = RequireSelected();
            if (r == null) return;
            try
            {
                Run(r);
                r.Status = "Refreshed";
                LoadHistory(r.Id);
                ViewEntry(r);
            }
            catch (Exception ex)
            {
                Ui.Error(ex, "Could not refresh the report");
            }
        }

        private void DeleteSelected(object sender, EventArgs e)
        {
            ReportEntry r = RequireSelected();
            if (r == null) return;
            if (!Ui.Confirm("Remove report " + r.Id + " (" + r.Type + ") from the list?")) return;
            ReportStore.Entries.Remove(r);
            LoadHistory();
        }

        private void pictureBox5_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void ExportSelected(object sender, EventArgs e)
        {
            ReportEntry r = RequireSelected();
            if (r == null) return;
            ExcelExport.Export(r.Data, r.Type,
                r.Params + "   |   generated " + r.GeneratedAt.ToString("yyyy-MM-dd HH:mm") + " by " + r.By,
                r.Type.Replace(' ', '_'));
        }

        
    }
}
