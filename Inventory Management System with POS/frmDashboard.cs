using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace Inventory_Management_System_with_POS
{
    /// <summary>
    /// Dashboard: key numbers, low-stock list, today's sales, quick actions and navigation.
    ///
    /// Control map:  textBox4 active products | textBox1 stock value | textBox2 reports generated |
    /// label5 today's sales | dataGridView1 low-stock list | dataGridView2 today's sold items.
    /// </summary>
    public partial class frmDashboard : Form
    {
        private int _ticks;

        private static readonly string[] ReportChoices = { "Manage Reports", "Today's Sales Report", "Low Stock Report", "Current Stock Report" };
        private static readonly string[] ManagementChoices = { "Manage Products", "Manage Stocks", "Expired Items", "Damaged Items", "System Accounts" };

        public frmDashboard()
        {
            InitializeComponent();
            timer1.Interval = 1000;
            timer1.Start();
            Load += new EventHandler(frmDashboard_Load);
        }

        private void frmDashboard_Load(object sender, EventArgs e)
        {
            Ui.SetupGrid(dataGridView1);
            Ui.SetupGrid(dataGridView2);
            textBox1.ReadOnly = true;
            textBox2.ReadOnly = true;
            textBox4.ReadOnly = true;

            if (!dataGridView2.Columns.Contains("colInvoice"))
                dataGridView2.Columns.Add(new DataGridViewTextBoxColumn { Name = "colInvoice", HeaderText = "Invoice" });

            comboBox1.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBox1.Items.Clear(); comboBox1.Items.AddRange(ReportChoices);
            comboBox1.SelectedIndex = -1; comboBox1.Text = "Reports";
            comboBox2.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBox2.Items.Clear(); comboBox2.Items.AddRange(ManagementChoices);
            comboBox2.SelectedIndex = -1; comboBox2.Text = "Management";
            comboBox1.SelectedIndexChanged += ReportsChosen;
            comboBox2.SelectedIndexChanged += ManagementChosen;

            Wire(button1, delegate { Ui.Navigate(this, "pos"); });            // Add New Transaction
            Wire(button3, CancelTransaction);                                  // Cancel Transaction
            Wire(button5, delegate { Ui.Navigate(this, "stocks"); });         // Adjust Stock
            Wire(button6, ViewLog);                                            // View Log
            Wire(button7, delegate { Ui.Navigate(this, "expired"); });        // Dispose Product
            Wire(button9, delegate { Ui.Navigate(this, "damage"); });         // Log Damage
            Wire(button2, delegate { Ui.Navigate(this, "reports"); });        // Create Report
            Wire(button12, delegate { QuickReport(ReportService.SalesTransactions, true); });   // Generate today's report
            Wire(button8, RecentActivity);                                     // View recent activity log
            Wire(button10, button10_Click);                                    // Home
            Wire(button11, SearchProduct);                                     // Search

            // "Delete Item" has nothing to delete on this screen: saved sales are cancelled, never edited line by line.
            button4.Enabled = false;
            new ToolTip().SetToolTip(button4, "Saved sales can't be edited. Use Cancel Transaction to cancel a whole sale.");

            // Only offer what this account type can open.
            button1.Visible = Session.CanAccess(Feature.POS);
            button5.Visible = Session.CanAccess(Feature.Stocks);
            button6.Visible = Session.CanAccess(Feature.Stocks);
            button7.Visible = Session.CanAccess(Feature.Expired);
            button9.Visible = Session.CanAccess(Feature.Damage);
            button2.Visible = Session.CanAccess(Feature.Reports);
            comboBox1.Visible = Session.CanAccess(Feature.Reports);
            comboBox2.Visible = Session.CanAccess(Feature.Products) || Session.CanAccess(Feature.Stocks);

            RefreshData();
        }

        private static void Wire(Button b, EventHandler h)
        {
            b.Click -= h;
            b.Click += h;
        }

        // ------------------------------------------------------------------ data
        private void RefreshData()
        {
            try
            {
                textBox4.Text = Db.ToInt(Db.Scalar("SELECT COUNT(*) FROM tbl_product WHERE product_status = 'Active'")).ToString();
                textBox1.Text = Ui.Money(Db.ToDec(Db.Scalar(
                    "SELECT IFNULL(SUM(IFNULL(s.quantity,0) * p.cost_price),0) FROM tbl_product p LEFT JOIN tbl_stock s ON s.product_id = p.product_id")));
                textBox2.Text = ReportStore.Entries.Count.ToString();

                DateTime today = DateTime.Today;
                DataTable tot = Db.Query(
                    @"SELECT IFNULL(SUM(total_amount),0) AS total, COUNT(*) AS n FROM tbl_sale
                      WHERE sale_status = 'Completed' AND created_at >= @d AND created_at < @d2",
                    Db.P("@d", today), Db.P("@d2", today.AddDays(1)));
                label5.Text = "TODAY'S SALES: " + Ui.Money(Db.ToDec(tot.Rows[0]["total"])) + "  (" + Db.ToInt(tot.Rows[0]["n"]) + " sale/s)";

                // today's sold items
                string keepSale = SelectedSaleId();
                DataTable items = Db.Query(
                    @"SELECT s.sale_id, p.product_name, si.quantity, si.line_total
                      FROM tbl_sale_item si
                      JOIN tbl_sale s ON s.sale_id = si.sale_id
                      JOIN tbl_product p ON p.product_id = si.product_id
                      WHERE s.sale_status = 'Completed' AND s.created_at >= @d AND s.created_at < @d2
                      ORDER BY s.created_at DESC, si.sale_item_id",
                    Db.P("@d", today), Db.P("@d2", today.AddDays(1)));
                dataGridView2.Rows.Clear();
                foreach (DataRow r in items.Rows)
                {
                    int i = dataGridView2.Rows.Add(r["product_name"].ToString(), Db.ToInt(r["quantity"]),
                        Db.ToDec(r["line_total"]).ToString("N2"), r["sale_id"].ToString());
                    dataGridView2.Rows[i].Tag = r["sale_id"].ToString();
                    if (keepSale != null && keepSale == r["sale_id"].ToString()) dataGridView2.Rows[i].Selected = true;
                }
                if (keepSale == null) dataGridView2.ClearSelection();

                // low stock
                DataTable low = Db.Query(
                    @"SELECT p.product_name, p.product_id, IFNULL(s.quantity,0) AS qty
                      FROM tbl_product p LEFT JOIN tbl_stock s ON s.product_id = p.product_id
                      WHERE p.product_status = 'Active' AND IFNULL(s.quantity,0) <= IFNULL(s.low_stock_threshold,10)
                      ORDER BY IFNULL(s.quantity,0), p.product_name");
                dataGridView1.Rows.Clear();
                foreach (DataRow r in low.Rows)
                {
                    int i = dataGridView1.Rows.Add(r["product_name"].ToString(), r["product_id"].ToString(), Db.ToInt(r["qty"]));
                    if (Db.ToInt(r["qty"]) == 0) dataGridView1.Rows[i].DefaultCellStyle.ForeColor = Color.Red;
                }
                dataGridView1.ClearSelection();
            }
            catch (Exception ex)
            {
                timer1.Stop();     // don't keep popping the same error every 30 seconds
                Ui.Error(ex, "Could not refresh the dashboard");
            }
        }

        private string SelectedSaleId()
        {
            return dataGridView2.SelectedRows.Count > 0 ? dataGridView2.SelectedRows[0].Tag as string : null;
        }

        // ------------------------------------------------------------------ actions
        /// <summary>Cancel Transaction: cancels a completed sale (selected line's invoice, or one you type) and returns its stock.</summary>
        private void CancelTransaction(object sender, EventArgs e)
        {
            try
            {
                string saleId = SelectedSaleId();
                if (saleId == null)
                {
                    string typed = Ui.Ask("Cancel Transaction", "Invoice number to cancel (for example SALE-000001).\nTip: select a sold item in today's list first.", "");
                    if (typed == null) return;
                    saleId = typed.Trim().ToUpper();
                }
                if (saleId.Length == 0) return;

                DataTable s = Db.Query("SELECT sale_status, total_amount, created_at FROM tbl_sale WHERE sale_id = @id", Db.P("@id", saleId));
                if (s.Rows.Count == 0) { Ui.Warn("Invoice " + saleId + " was not found."); return; }
                if (s.Rows[0]["sale_status"].ToString() != "Completed")
                {
                    Ui.Warn("Invoice " + saleId + " is already " + s.Rows[0]["sale_status"] + ".");
                    return;
                }

                string approver = Ui.ApproveManager("Cancelling " + saleId + " (" + Ui.Money(Db.ToDec(s.Rows[0]["total_amount"])) + ") needs Manager or Admin approval.");
                if (approver == null) return;

                string reason = Ui.Ask("Cancel Transaction", "Reason for cancelling " + saleId + ":", "");
                if (reason == null) return;
                reason = reason.Trim();
                if (reason.Length < 3) { Ui.Warn("Please enter a reason (at least 3 characters)."); return; }

                if (!Ui.Confirm("Cancel " + saleId + " and return its items to stock?")) return;

                SalesService.VoidSale(saleId, approver, Session.UserId, reason);
                Ui.Info(saleId + " was cancelled and its items were returned to stock.", "Sale Cancelled");
                RefreshData();
            }
            catch (Exception ex)
            {
                Ui.Error(ex);
            }
        }

        /// <summary>View Log: stock movements of the selected low-stock item, or of all items.</summary>
        private void ViewLog(object sender, EventArgs e)
        {
            try
            {
                string id = dataGridView1.SelectedRows.Count > 0 ? dataGridView1.SelectedRows[0].Cells[1].Value as string : null;
                DataTable dt = InventoryService.MovementLog(id, null, null, 300);
                ReportPreviewForm.Show("Stock Movement Log", id == null ? "All products (latest 300 entries)" : "Product " + id + " (latest 300 entries)", dt);
            }
            catch (Exception ex) { Ui.Error(ex); }
        }

        /// <summary>View recent activity log: the latest stock movements (sales, purchases, damage, disposals...).</summary>
        private void RecentActivity(object sender, EventArgs e)
        {
            try
            {
                DataTable dt = InventoryService.MovementLog(null, null, null, 100);
                ReportPreviewForm.Show("Recent Activity", "Latest 100 stock movements", dt);
            }
            catch (Exception ex) { Ui.Error(ex); }
        }

        private void SearchProduct(object sender, EventArgs e)
        {
            string term = Ui.Ask("Search Product", "Product ID, name or barcode:", "");
            if (term != null) Ui.ShowProductInfo(term);
        }

        /// <summary>Builds a report, lists it under Manage Reports and shows the preview.</summary>
        private void QuickReport(string type, bool today)
        {
            try
            {
                DateTime from = DateTime.Today, to = DateTime.Today;
                var entry = new ReportEntry
                {
                    Id = ReportStore.NextId(), Type = type, From = from, To = to,
                    CategoryId = "", CategoryName = "", SupplierId = "", SupplierName = "",
                    By = Session.UserName ?? "", Status = "Generated"
                };
                entry.Data = ReportService.Build(type, from, to, "", "");
                entry.GeneratedAt = DateTime.Now;
                entry.Params = ReportService.Describe(type, from, to, "", "");
                ReportStore.Entries.Add(entry);
                textBox2.Text = ReportStore.Entries.Count.ToString();
                ReportPreviewForm.Show(type + "  (" + entry.Id + ")", entry.Params + (today ? "  (today)" : ""), entry.Data);
            }
            catch (Exception ex) { Ui.Error(ex); }
        }

        private void ReportsChosen(object sender, EventArgs e)
        {
            string c = comboBox1.SelectedItem as string;
            if (c == null) return;
            comboBox1.SelectedIndexChanged -= ReportsChosen;
            comboBox1.SelectedIndex = -1; comboBox1.Text = "Reports";
            comboBox1.SelectedIndexChanged += ReportsChosen;

            if (c == "Manage Reports") Ui.Navigate(this, "reports");
            else if (c == "Today's Sales Report") QuickReport(ReportService.SalesTransactions, true);
            else if (c == "Low Stock Report") QuickReport(ReportService.LowStock, false);
            else if (c == "Current Stock Report") QuickReport(ReportService.CurrentStock, false);
        }

        private void ManagementChosen(object sender, EventArgs e)
        {
            string c = comboBox2.SelectedItem as string;
            if (c == null) return;
            comboBox2.SelectedIndexChanged -= ManagementChosen;
            comboBox2.SelectedIndex = -1; comboBox2.Text = "Management";
            comboBox2.SelectedIndexChanged += ManagementChosen;

            if (c == "Manage Products") Ui.Navigate(this, "products");
            else if (c == "Manage Stocks") Ui.Navigate(this, "stocks");
            else if (c == "Expired Items") Ui.Navigate(this, "expired");
            else if (c == "Damaged Items") Ui.Navigate(this, "damage");
            else if (c == "System Accounts") Ui.Navigate(this, "accounts");
        }

        // ------------------------------------------------------------------ designer handlers
        private void button10_Click(object sender, EventArgs e)
        {
            Ui.Navigate(this, "home");
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            label6.Text = DateTime.Now.ToLongDateString();
            label8.Text = DateTime.Now.ToLongTimeString();
            _ticks++;
            if (_ticks % 30 == 0) RefreshData();       // numbers refresh every 30 seconds
        }

        private void label6_Click(object sender, EventArgs e)
        {
            label6.Text = DateTime.Now.ToLongDateString();
            label8.Text = DateTime.Now.ToLongTimeString();
        }

        private void pictureBox1_Click(object sender, EventArgs e) { Close(); }
        private void pictureBox2_Click(object sender, EventArgs e) { }
        private void pictureBox3_Click(object sender, EventArgs e) { }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void label5_Click(object sender, EventArgs e)
        {

        }

        private void frmDashboard_Load_1(object sender, EventArgs e)
        {

        }
    }
}
