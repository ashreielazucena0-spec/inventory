using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace Inventory_Management_System_with_POS
{
    /// <summary>
    /// Damaged Items: log damaged stock (tbl_stock_log OUT / DAMAGED + tbl_stock), track disposition, history and report.
    /// Damage type and disposition are kept in the log's remarks text (see DamageRemarks).
    ///
    /// Control map:  textBox13 Product ID | textBox1 Product name | textBox4 Barcode | textBox7 Stock on hand |
    /// textBox2 Damage qty | textBox3 Damage type | textBox6 Disposition | textBox5 Detailed reason |
    /// textBox8 Search logs | textBox9 log count | textBox10 pending dispositions | textBox11 total estimated loss.
    /// </summary>
    public partial class frmDamage : Form
    {
        private static readonly string[] DamageTypes = { "Broken", "Spoiled", "Leaking", "Crushed", "Defective", "Other" };
        private static readonly string[] Dispositions = { "Pending", "Disposed", "Returned to Supplier" };

        private class LogRef
        {
            public long LogId;
            public string ProductId;
            public string Type, Disposition, Details;
            public int Qty;
        }

        private string _productId;        // product currently loaded in the entry panel
        private decimal _cost;
        private LogRef _editing;          // damage log loaded from the list (null = logging new damage)

        public frmDamage()
        {
            InitializeComponent();
            Load += new EventHandler(frmDamage_Load);
        }

        private void frmDamage_Load(object sender, EventArgs e)
        {
            Ui.SetupGrid(dataGridView1);
            label12.Text = "Total Damage Logs:";       // this label was a duplicate of "Search Damage Logs"
            textBox1.ReadOnly = false;
            textBox7.ReadOnly = true;
            textBox9.ReadOnly = true;
            textBox10.ReadOnly = true;
            textBox11.ReadOnly = true;

            var typeList = new AutoCompleteStringCollection(); typeList.AddRange(DamageTypes);
            textBox3.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            textBox3.AutoCompleteSource = AutoCompleteSource.CustomSource;
            textBox3.AutoCompleteCustomSource = typeList;
            var dispList = new AutoCompleteStringCollection(); dispList.AddRange(Dispositions);
            textBox6.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            textBox6.AutoCompleteSource = AutoCompleteSource.CustomSource;
            textBox6.AutoCompleteCustomSource = dispList;

            Wire(button1, delegate { ClearEntry(null, EventArgs.Empty); textBox13.Focus(); });   // Log Damage (top tab)
            Wire(button2, SetDisposition);                                                       // Disposal/Return
            Wire(button3, ViewHistory);                                                          // View History
            Wire(button4, ClearEntry);                                                           // CLEAR
            Wire(button5, delegate { LoadLogs(); });                                             // SEARCH
            Wire(button6, UpdateSelectedLog);                                                    // UPDATE SELECTED LOG
            Wire(button7, DamageReport);                                                         // DAMAGE REPORT
            Wire(button8, SaveReport);                                                           // SAVE REPORT

            textBox8.TextChanged += (s, a) => LoadLogs();
            textBox13.KeyDown += (s, a) => Lookup(a, textBox13.Text, true);
            textBox4.KeyDown += (s, a) => Lookup(a, textBox4.Text, true);
            textBox1.KeyDown += (s, a) => Lookup(a, textBox1.Text, false);
            dataGridView1.CellClick += (s, a) => { if (a.RowIndex >= 0) LoadLogIntoEntry(); };

            ClearEntry(null, EventArgs.Empty);
            LoadLogs();
        }

        private static void Wire(Button b, EventHandler h)
        {
            b.Click -= h;
            b.Click += h;
        }

        // ------------------------------------------------------------------ product look-up
        private void Lookup(KeyEventArgs a, string text, bool exact)
        {
            if (a.KeyCode != Keys.Enter) return;
            a.SuppressKeyPress = true;
            text = (text ?? "").Trim();
            if (text.Length == 0) return;

            try
            {
                string id = null;
                if (exact)
                {
                    object o = Db.Scalar("SELECT product_id FROM tbl_product WHERE product_id = @k OR barcode = @k LIMIT 1", Db.P("@k", text));
                    if (o == null) { Ui.Warn("No product found with that ID or barcode."); return; }
                    id = o.ToString();
                }
                else
                {
                    id = Ui.PickProduct(text, false);
                    if (id == null) return;
                }
                LoadProduct(id);
                textBox2.Focus();
            }
            catch (Exception ex)
            {
                Ui.Error(ex);
            }
        }

        private void LoadProduct(string id)
        {
            DataTable dt = Db.Query(
                @"SELECT p.product_id, p.product_name, IFNULL(p.barcode,'') AS barcode, p.cost_price, IFNULL(s.quantity,0) AS qty
                  FROM tbl_product p LEFT JOIN tbl_stock s ON s.product_id = p.product_id WHERE p.product_id = @id", Db.P("@id", id));
            if (dt.Rows.Count == 0) return;
            DataRow r = dt.Rows[0];
            _editing = null;
            _productId = id;
            _cost = Db.ToDec(r["cost_price"]);
            textBox13.Text = id;
            textBox1.Text = r["product_name"].ToString();
            textBox4.Text = r["barcode"].ToString();
            textBox7.Text = r["qty"].ToString();
            textBox2.ReadOnly = false;
            if (textBox6.Text.Trim().Length == 0) textBox6.Text = "Pending";
        }

        private void ClearEntry(object sender, EventArgs e)
        {
            _productId = null;
            _editing = null;
            _cost = 0;
            textBox13.Clear();
            textBox1.Clear();
            textBox4.Clear();
            textBox7.Clear();
            textBox2.Clear();
            textBox2.ReadOnly = false;
            textBox3.Clear();
            textBox6.Text = "Pending";
            textBox5.Clear();
            dataGridView1.ClearSelection();
        }

        // ------------------------------------------------------------------ list
        private void LoadLogs()
        {
            try
            {
                long keep = _editing == null ? -1 : _editing.LogId;
                string q = (textBox8.Text ?? "").Trim();
                DataTable dt = Db.Query(
                    @"SELECT l.log_id, l.product_id, p.product_name, c.category_name, l.quantity, IFNULL(l.remarks,'') AS remarks,
                             l.created_at, COALESCE(l.unit_cost, p.cost_price) AS cost
                      FROM tbl_stock_log l
                      JOIN tbl_product p ON p.product_id = l.product_id
                      JOIN tbl_category c ON c.category_id = p.category_id
                      WHERE l.movement_type = 'OUT' AND l.reason = 'DAMAGED'
                        AND (@q = '' OR l.product_id LIKE @l OR p.product_name LIKE @l OR IFNULL(p.barcode,'') LIKE @l
                             OR c.category_name LIKE @l OR IFNULL(l.remarks,'') LIKE @l)
                      ORDER BY l.created_at DESC, l.log_id DESC", Db.P("@q", q), Db.P("@l", "%" + q + "%"));

                dataGridView1.Rows.Clear();
                int pending = 0;
                decimal loss = 0;
                foreach (DataRow r in dt.Rows)
                {
                    string type, disp, details;
                    DamageRemarks.Parse(r["remarks"].ToString(), out type, out disp, out details);
                    int qty = Db.ToInt(r["quantity"]);
                    loss += qty * Db.ToDec(r["cost"]);
                    if (disp == "Pending") pending++;

                    int i = dataGridView1.Rows.Add(r["log_id"].ToString(), r["product_name"].ToString(), r["category_name"].ToString(),
                        qty, type, Convert.ToDateTime(r["created_at"]).ToString("yyyy-MM-dd HH:mm"), disp);
                    DataGridViewRow row = dataGridView1.Rows[i];
                    row.Tag = new LogRef { LogId = Convert.ToInt64(r["log_id"]), ProductId = r["product_id"].ToString(), Type = type, Disposition = disp, Details = details, Qty = qty };
                    if (disp == "Pending") row.Cells[6].Style.ForeColor = Color.DarkOrange;
                    if (row.Tag != null && ((LogRef)row.Tag).LogId == keep) { dataGridView1.ClearSelection(); row.Selected = true; }
                }
                if (keep < 0) dataGridView1.ClearSelection();

                textBox9.Text = dt.Rows.Count.ToString();
                textBox10.Text = pending.ToString();
                textBox11.Text = Ui.Money(loss);
            }
            catch (Exception ex)
            {
                Ui.Error(ex, "Could not load damage logs");
            }
        }

        private LogRef SelectedLog()
        {
            return dataGridView1.SelectedRows.Count > 0 ? dataGridView1.SelectedRows[0].Tag as LogRef : null;
        }

        /// <summary>Clicking a log fills the entry panel so its type / disposition / details can be changed.</summary>
        private void LoadLogIntoEntry()
        {
            LogRef log = SelectedLog();
            if (log == null) return;
            DataTable dt = Db.Query(
                @"SELECT p.product_name, IFNULL(p.barcode,'') AS barcode, IFNULL(s.quantity,0) AS qty
                  FROM tbl_product p LEFT JOIN tbl_stock s ON s.product_id = p.product_id WHERE p.product_id = @id", Db.P("@id", log.ProductId));
            if (dt.Rows.Count == 0) return;
            _editing = log;
            _productId = log.ProductId;
            textBox13.Text = log.ProductId;
            textBox1.Text = dt.Rows[0]["product_name"].ToString();
            textBox4.Text = dt.Rows[0]["barcode"].ToString();
            textBox7.Text = dt.Rows[0]["qty"].ToString();
            textBox2.Text = log.Qty.ToString();
            textBox2.ReadOnly = true;                 // stock already reduced; quantity of a saved log can't change
            textBox3.Text = log.Type;
            textBox6.Text = log.Disposition;
            textBox5.Text = log.Details;
        }

        // ------------------------------------------------------------------ save / update
        private bool ReadTypeDisposition(out string type, out string disposition)
        {
            type = textBox3.Text.Trim();
            disposition = textBox6.Text.Trim();
            if (type.Length == 0) type = "Damaged";
            if (disposition.Length == 0) disposition = "Pending";
            foreach (string d in Dispositions)
                if (string.Equals(d, disposition, StringComparison.OrdinalIgnoreCase)) { disposition = d; return true; }
            Ui.Warn("Disposition must be one of: " + string.Join(", ", Dispositions) + ".");
            textBox6.Focus();
            return false;
        }

        /// <summary>SAVE REPORT: records the damage and takes the quantity out of stock.</summary>
        private void SaveReport(object sender, EventArgs e)
        {
            if (_editing != null) { Ui.Warn("This damage log is already saved. Use UPDATE SELECTED LOG to change it, or CLEAR to report new damage."); return; }
            if (_productId == null) { Ui.Warn("Enter a Product ID or barcode and press Enter to load the product first."); textBox13.Focus(); return; }

            int qty;
            if (!Ui.TryInt(textBox2.Text, out qty) || qty <= 0) { Ui.Warn("Enter the damaged quantity (whole number greater than zero)."); textBox2.Focus(); return; }
            string type, disp;
            if (!ReadTypeDisposition(out type, out disp)) return;
            string details = textBox5.Text.Trim();
            if (details.Length < 3) { Ui.Warn("Enter the detailed reason (at least 3 characters)."); textBox5.Focus(); return; }

            try
            {
                int onHand = Db.ToInt(Db.Scalar("SELECT IFNULL((SELECT quantity FROM tbl_stock WHERE product_id = @id),0)", Db.P("@id", _productId)));
                if (qty > onHand) { Ui.Warn("Only " + onHand + " in stock. The damaged quantity can't be more than that."); textBox2.Focus(); return; }

                if (!Ui.Confirm("Report " + qty + " x " + textBox1.Text + " as damaged?\nEstimated loss: " + Ui.Money(qty * _cost) + "\nStock: " + onHand + "  ->  " + (onHand - qty)))
                    return;

                InventoryService.Apply(_productId, "OUT", "DAMAGED", qty, null, null, _cost, null, null, Session.UserId,
                    DamageRemarks.Build(type, disp, details), null);

                Ui.Info("Damage recorded and stock updated.", "Success");
                ClearEntry(null, EventArgs.Empty);
                LoadLogs();
            }
            catch (Exception ex)
            {
                Ui.Error(ex);
            }
        }

        /// <summary>UPDATE SELECTED LOG: changes the damage type, disposition and details of a saved log.</summary>
        private void UpdateSelectedLog(object sender, EventArgs e)
        {
            if (_editing == null) { Ui.Warn("Click a damage log in the list first."); return; }
            string type, disp;
            if (!ReadTypeDisposition(out type, out disp)) return;
            string details = textBox5.Text.Trim();
            if (details.Length < 3) { Ui.Warn("Enter the detailed reason (at least 3 characters)."); textBox5.Focus(); return; }

            SaveRemarks(_editing.LogId, DamageRemarks.Build(type, disp, details));
            ClearEntry(null, EventArgs.Empty);
        }

        /// <summary>Disposal/Return: marks the selected log as Disposed or Returned to Supplier.</summary>
        private void SetDisposition(object sender, EventArgs e)
        {
            LogRef log = SelectedLog();
            if (log == null) { Ui.Warn("Select a damage log from the list first.", "No Selection"); return; }
            string choice = Ui.Choose("Disposition", "What happened to these damaged items?", Dispositions, log.Disposition);
            if (choice == null) return;
            SaveRemarks(log.LogId, DamageRemarks.Build(log.Type, choice, log.Details));
            ClearEntry(null, EventArgs.Empty);
        }

        private void SaveRemarks(long logId, string remarks)
        {
            try
            {
                Db.Exec("UPDATE tbl_stock_log SET remarks = @r WHERE log_id = @id AND reason = 'DAMAGED'",
                    Db.P("@r", remarks), Db.P("@id", logId));
                LoadLogs();
            }
            catch (Exception ex)
            {
                Ui.Error(ex);
            }
        }

        // ------------------------------------------------------------------ history / report
        private void ViewHistory(object sender, EventArgs e)
        {
            try
            {
                DataTable dt = ReportService.Build(ReportService.Damaged, new DateTime(2000, 1, 1), DateTime.Today, null, null);
                ReportPreviewForm.Show("Damage History", "All damage logs", dt);
            }
            catch (Exception ex) { Ui.Error(ex); }
        }

        private void DamageReport(object sender, EventArgs e)
        {
            try
            {
                DataTable dt = ReportService.Build(ReportService.Damaged, new DateTime(2000, 1, 1), DateTime.Today, null, null);
                if (dt.Rows.Count == 0) { Ui.Info("There are no damage logs to report."); return; }
                Printer.PrintTable("Damage Report", "All damage logs to " + DateTime.Today.ToString("yyyy-MM-dd"), dt);
            }
            catch (Exception ex) { Ui.Error(ex); }
        }

        private void pictureBox5_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void label1_Click(object sender, EventArgs e) { }
    }
}
