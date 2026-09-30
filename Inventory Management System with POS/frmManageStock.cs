using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace Inventory_Management_System_with_POS
{
    /// <summary>
    /// Manage Stocks (tbl_stock + tbl_stock_log): inventory list, stock in/out with reason, alert levels, movement log.
    ///
    /// Control map:  comboBox3 Select product | comboBox1 Change type | textBox4 Quantity | textBox1 Reason for change |
    /// dateTimePicker1 Adjustment date | textBox10 Search | textBox9 / textBox2 / textBox3 = units in stock / low-stock count / stock value.
    /// </summary>
    public partial class frmManageStock : Form
    {
        // Change type -> (movement, reason) written to tbl_stock_log
        private static readonly string[] ChangeTypes =
        {
            "Stock In - Purchase",
            "Stock In - Customer Return",
            "Stock In - Adjustment (add)",
            "Stock Out - Adjustment (deduct)",
            "Stock Out - Damaged",
            "Stock Out - Expired"
        };

        public frmManageStock()
        {
            InitializeComponent();
            Load += new EventHandler(frmManageStock_Load);
        }

        private void frmManageStock_Load(object sender, EventArgs e)
        {
            Ui.SetupGrid(dataGridView1);
            textBox2.ReadOnly = true;
            textBox3.ReadOnly = true;
            textBox9.ReadOnly = true;

            // Column 6 ("Recorder Level") has no separate column in the schema: the alert level is column 5.
            if (dataGridView1.Columns.Count > 5) dataGridView1.Columns[5].Visible = false;

            comboBox1.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBox1.Items.Clear();
            comboBox1.Items.AddRange(ChangeTypes);
            comboBox3.DropDownStyle = ComboBoxStyle.DropDownList;
            dateTimePicker1.MaxDate = DateTime.Today;
            dateTimePicker1.Value = DateTime.Today;

            Wire(button1, delegate { comboBox3.Focus(); });   // Adjust Stock (top tab)
            Wire(button2, ViewLogs);                          // View Logs (top tab)
            Wire(button3, SetAlertLevel);                     // Set Reorder Levels (top tab)
            Wire(button4, ClearEntry);                        // CANCEL
            Wire(button5, LoadSelectedIntoEntry);             // UPDATE SELECTED STOCK
            Wire(button6, ViewLogs);                          // VIEW ADJUSTMENT LOGS
            Wire(button8, SaveAdjustment);                    // SAVE STOCK ADJUSTMENT

            textBox10.TextChanged += (s, a) => LoadInventory();
            dataGridView1.CellClick += (s, a) => { if (a.RowIndex >= 0) SelectProductInCombo(SelectedId()); };

            try
            {
                LoadProductCombo();
                ClearEntry(null, EventArgs.Empty);
                LoadInventory();
            }
            catch (Exception ex)
            {
                Ui.Error(ex, "Could not load stock");
            }
        }

        private static void Wire(Button b, EventHandler h)
        {
            b.Click -= h;
            b.Click += h;
        }

        // ------------------------------------------------------------------ data
        private void LoadProductCombo()
        {
            DataTable dt = Db.Query(
                @"SELECT product_id, CONCAT(product_id, ' - ', product_name, IF(product_status = 'Active', '', ' (inactive)')) AS label
                  FROM tbl_product ORDER BY product_name");
            comboBox3.DataSource = null;
            comboBox3.DisplayMember = "label";
            comboBox3.ValueMember = "product_id";
            comboBox3.DataSource = dt;
            comboBox3.SelectedIndex = -1;
        }

        private string SelectedId()
        {
            return dataGridView1.SelectedRows.Count > 0 ? dataGridView1.SelectedRows[0].Cells[0].Value as string : null;
        }

        private void SelectProductInCombo(string id)
        {
            if (id != null) comboBox3.SelectedValue = id;
        }

        private void LoadInventory()
        {
            try
            {
                string keep = SelectedId();
                string q = (textBox10.Text ?? "").Trim();
                DataTable dt = Db.Query(
                    @"SELECT p.product_id, p.product_name, c.category_name, IFNULL(s.quantity,0) AS qty,
                             IFNULL(s.low_stock_threshold,10) AS thr, IFNULL(sp.supplier_name,'') AS supplier, p.product_status
                      FROM tbl_product p
                      JOIN tbl_category c ON c.category_id = p.category_id
                      LEFT JOIN tbl_stock s ON s.product_id = p.product_id
                      LEFT JOIN tbl_supplier sp ON sp.supplier_id = p.supplier_id
                      WHERE @q = '' OR p.product_id LIKE @l OR p.product_name LIKE @l OR IFNULL(p.barcode,'') LIKE @l
                            OR c.category_name LIKE @l OR IFNULL(sp.supplier_name,'') LIKE @l
                      ORDER BY p.product_name", Db.P("@q", q), Db.P("@l", "%" + q + "%"));

                // Column order in the designer: 0 Product ID, 1 Name, 2 Category, 3 Stock level, 4 Min. stock alert, 5 (hidden), 6 Supplier
                dataGridView1.Rows.Clear();
                foreach (DataRow r in dt.Rows)
                {
                    int qty = Db.ToInt(r["qty"]);
                    int thr = Db.ToInt(r["thr"]);
                    int i = dataGridView1.Rows.Add(r["product_id"].ToString(), r["product_name"].ToString(), r["category_name"].ToString(),
                        qty, thr, "", r["supplier"].ToString());
                    DataGridViewRow row = dataGridView1.Rows[i];
                    if (r["product_status"].ToString() != "Active") row.DefaultCellStyle.ForeColor = Color.Gray;
                    else if (qty <= thr) row.Cells[3].Style.ForeColor = Color.Red;
                    if (keep != null && r["product_id"].ToString() == keep) { dataGridView1.ClearSelection(); row.Selected = true; }
                }
                if (keep == null) dataGridView1.ClearSelection();

                DataTable m = Db.Query(
                    @"SELECT CAST(IFNULL(SUM(IFNULL(s.quantity,0)),0) AS SIGNED) AS units,
                             IFNULL(SUM(p.product_status = 'Active' AND IFNULL(s.quantity,0) <= IFNULL(s.low_stock_threshold,10)),0) AS low,
                             IFNULL(SUM(IFNULL(s.quantity,0) * p.cost_price),0) AS value
                      FROM tbl_product p LEFT JOIN tbl_stock s ON s.product_id = p.product_id");
                textBox9.Text = Db.ToInt(m.Rows[0]["units"]).ToString("N0");
                textBox2.Text = Db.ToInt(m.Rows[0]["low"]).ToString();
                textBox3.Text = Ui.Money(Db.ToDec(m.Rows[0]["value"]));
            }
            catch (Exception ex)
            {
                Ui.Error(ex, "Could not load stock");
            }
        }

        // ------------------------------------------------------------------ entry panel
        private void ClearEntry(object sender, EventArgs e)
        {
            comboBox3.SelectedIndex = -1;
            comboBox1.SelectedIndex = -1;
            textBox4.Clear();
            textBox1.Clear();
            dateTimePicker1.Value = DateTime.Today;
        }

        private void LoadSelectedIntoEntry(object sender, EventArgs e)
        {
            string id = SelectedId();
            if (id == null) { Ui.Warn("Select a product from the inventory list first.", "No Selection"); return; }
            SelectProductInCombo(id);
            comboBox1.Focus();
        }

        /// <summary>SAVE STOCK ADJUSTMENT: changes tbl_stock and writes a tbl_stock_log row in one transaction.</summary>
        private void SaveAdjustment(object sender, EventArgs e)
        {
            if (comboBox3.SelectedValue == null) { Ui.Warn("Select a product."); comboBox3.Focus(); return; }
            if (comboBox1.SelectedIndex < 0) { Ui.Warn("Select the change type."); comboBox1.Focus(); return; }

            int qty;
            if (!Ui.TryInt(textBox4.Text, out qty) || qty <= 0) { Ui.Warn("Enter a quantity greater than zero (whole number)."); textBox4.Focus(); return; }

            string type = comboBox1.SelectedItem.ToString();
            string movement = type.StartsWith("Stock In") ? "IN" : "OUT";
            string reason;
            switch (type)
            {
                case "Stock In - Purchase": reason = "PURCHASE"; break;
                case "Stock In - Customer Return": reason = "RETURN"; break;
                case "Stock Out - Damaged": reason = "DAMAGED"; break;
                case "Stock Out - Expired": reason = "EXPIRED"; break;
                default: reason = "ADJUSTMENT"; break;
            }

            string note = textBox1.Text.Trim();
            bool needsNote = reason == "ADJUSTMENT" || reason == "DAMAGED" || reason == "EXPIRED";
            if (needsNote && note.Length < 3) { Ui.Warn("Enter a reason for this change (at least 3 characters)."); textBox1.Focus(); return; }
            if (dateTimePicker1.Value.Date > DateTime.Today) { Ui.Warn("The adjustment date can't be in the future."); return; }

            string productId = comboBox3.SelectedValue.ToString();
            try
            {
                DataTable p = Db.Query(
                    @"SELECT p.product_name, p.cost_price, IFNULL(p.supplier_id,'') AS supplier_id, IFNULL(s.quantity,0) AS qty
                      FROM tbl_product p LEFT JOIN tbl_stock s ON s.product_id = p.product_id WHERE p.product_id = @id", Db.P("@id", productId));
                if (p.Rows.Count == 0) { Ui.Warn("That product no longer exists."); return; }
                string name = p.Rows[0]["product_name"].ToString();
                int onHand = Db.ToInt(p.Rows[0]["qty"]);
                decimal cost = Db.ToDec(p.Rows[0]["cost_price"]);

                if (movement == "OUT" && qty > onHand)
                {
                    Ui.Warn("Only " + onHand + " in stock for '" + name + "'. You can't remove " + qty + ".");
                    return;
                }

                string batch = null, supplier = null;
                DateTime? expiry = null;
                decimal? unitCost = null;
                if (reason == "PURCHASE")
                {
                    StockInInfo info = Ui.AskStockIn(name, cost, p.Rows[0]["supplier_id"].ToString());
                    if (info == null) return;
                    batch = info.BatchNo; expiry = info.Expiry; unitCost = info.UnitCost;
                    supplier = string.IsNullOrEmpty(info.SupplierId) ? null : info.SupplierId;
                }
                else if (movement == "OUT")
                {
                    unitCost = cost;
                }

                string remarks = note;
                if (reason == "DAMAGED") remarks = DamageRemarks.Build("Other", "Pending", note);

                int after = movement == "IN" ? onHand + qty : onHand - qty;
                if (!Ui.Confirm(type + "\n\n" + name + "\nQuantity: " + qty + "\nStock: " + onHand + "  ->  " + after + "\n\nSave this adjustment?"))
                    return;

                DateTime when = dateTimePicker1.Value.Date == DateTime.Today ? DateTime.Now : dateTimePicker1.Value.Date.AddHours(12);
                InventoryService.Apply(productId, movement, reason, qty, batch, expiry, unitCost, supplier, null, Session.UserId, remarks, when);

                Ui.Info("Stock updated. " + name + " now has " + after + " in stock.", "Success");
                ClearEntry(null, EventArgs.Empty);
                LoadInventory();
            }
            catch (Exception ex)
            {
                Ui.Error(ex);
            }
        }

        // ------------------------------------------------------------------ alert level / logs
        /// <summary>Set Reorder Levels: sets the minimum stock alert (low_stock_threshold) of the selected product.</summary>
        private void SetAlertLevel(object sender, EventArgs e)
        {
            string id = SelectedId();
            if (id == null) { Ui.Warn("Select a product from the inventory list first.", "No Selection"); return; }
            string name = dataGridView1.SelectedRows[0].Cells[1].Value as string;
            string current = Convert.ToString(dataGridView1.SelectedRows[0].Cells[4].Value);

            string input = Ui.Ask("Minimum Stock Alert", "Alert when '" + name + "' falls to this quantity or lower:", current);
            if (input == null) return;
            int level;
            if (!Ui.TryInt(input, out level) || level < 0) { Ui.Warn("Enter a whole number, 0 or more."); return; }

            try
            {
                Db.Exec(@"INSERT INTO tbl_stock (product_id, quantity, low_stock_threshold) VALUES (@id, 0, @t)
                          ON DUPLICATE KEY UPDATE low_stock_threshold = @t", Db.P("@id", id), Db.P("@t", level));
                LoadInventory();
            }
            catch (Exception ex)
            {
                Ui.Error(ex);
            }
        }

        /// <summary>View Logs / VIEW ADJUSTMENT LOGS: movement history of the selected product, or of everything.</summary>
        private void ViewLogs(object sender, EventArgs e)
        {
            try
            {
                string id = comboBox3.SelectedValue == null ? SelectedId() : comboBox3.SelectedValue.ToString();
                DataTable dt = InventoryService.MovementLog(id, null, null, 500);
                string title = "Stock Movement Log";
                string sub = id == null ? "All products (latest 500 entries)" : "Product " + id + " (latest 500 entries)";
                ReportPreviewForm.Show(title, sub, dt);
            }
            catch (Exception ex)
            {
                Ui.Error(ex);
            }
        }

        private void pictureBox3_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
