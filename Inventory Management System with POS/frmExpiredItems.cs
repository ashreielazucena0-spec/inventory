using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace Inventory_Management_System_with_POS
{
    /// <summary>
    /// Expired Items: finds batches by expiry date (from tbl_stock_log IN rows, see InventoryService.GetBatches),
    /// disposes expired units (OUT / EXPIRED) and suggests markdown prices for items close to expiry.
    ///
    /// Control map:  textBox13 Product ID | textBox1 Name | textBox4 Barcode | dateTimePicker1/2 Expiry from/to |
    /// textBox11 Selected item | textBox5 Units in batch | textBox6 Dispose quantity | textBox7 Discount % | textBox9 Dispose reason |
    /// textBox2 batches found | textBox3 units affected | textBox8 items needing action.
    /// </summary>
    public partial class frmExpiredItems : Form
    {
        private const int SoonDays = 7;

        private class BatchRef
        {
            public string ProductId, ProductName, BatchNo;
            public DateTime Expiry;
            public int Remaining;
            public decimal Cost, Price;
        }

        public frmExpiredItems()
        {
            InitializeComponent();
            Load += new EventHandler(frmExpiredItems_Load);
        }

        private void frmExpiredItems_Load(object sender, EventArgs e)
        {
            Ui.SetupGrid(dataGridView1);
            label11.Text = "Total Units Affected:";     // this label was a duplicate of "Total Expired Items Found"
            foreach (TextBox t in new[] { textBox2, textBox3, textBox8, textBox11, textBox5 }) t.ReadOnly = true;

            if (!dataGridView1.Columns.Contains("colBatch"))
                dataGridView1.Columns.Add(new DataGridViewTextBoxColumn { Name = "colBatch", HeaderText = "Batch No" });

            dateTimePicker1.Value = new DateTime(2000, 1, 1);     // show all expired stock, however old
            dateTimePicker2.Value = DateTime.Today;
            textBox9.Text = "Expired";

            Wire(button1, PrepareDisposal);       // Dispose Product (top tab)
            Wire(button2, SuggestPrice);          // SET DISCOUNTED PRICE
            Wire(button3, ProcessDiscount);       // Process Discount (top tab)
            Wire(button4, Search);                // Recalculate Expiry
            Wire(button6, Search);                // SEARCH EXPIRED ITEMS
            Wire(button8, ConfirmDisposal);       // CONFIRM DISPOSAL

            dataGridView1.SelectionChanged += (s, a) => ShowSelected();
            foreach (TextBox t in new[] { textBox13, textBox1, textBox4 })
                t.KeyDown += (s, a) => { if (a.KeyCode == Keys.Enter) { a.SuppressKeyPress = true; Search(null, EventArgs.Empty); } };

            Search(null, EventArgs.Empty);
        }

        private static void Wire(Button b, EventHandler h)
        {
            b.Click -= h;
            b.Click += h;
        }

        // ------------------------------------------------------------------ search
        private void Search(object sender, EventArgs e)
        {
            try
            {
                if (dateTimePicker1.Value.Date > dateTimePicker2.Value.Date)
                {
                    Ui.Warn("The 'From' date must not be after the 'To' date.");
                    return;
                }

                string id = textBox13.Text.Trim().ToLower();
                string name = textBox1.Text.Trim().ToLower();
                string bar = textBox4.Text.Trim().ToLower();
                string keep = SelectedBatchKey();

                DataTable batches = InventoryService.GetBatches(null);
                dataGridView1.Rows.Clear();

                int expiredCount = 0, units = 0;
                foreach (DataRow r in batches.Select("", "expiry_date ASC, product_name ASC"))
                {
                    DateTime exp = Convert.ToDateTime(r["expiry_date"]).Date;
                    if (exp < dateTimePicker1.Value.Date || exp > dateTimePicker2.Value.Date) continue;
                    if (id.Length > 0 && !r["product_id"].ToString().ToLower().Contains(id)) continue;
                    if (name.Length > 0 && !r["product_name"].ToString().ToLower().Contains(name)) continue;
                    if (bar.Length > 0 && !r["barcode"].ToString().ToLower().Contains(bar)) continue;

                    int left = Db.ToInt(r["remaining"]);
                    string status = exp < DateTime.Today ? "Expired - dispose"
                                  : exp <= DateTime.Today.AddDays(SoonDays) ? "Expiring soon" : "OK";
                    if (exp < DateTime.Today) expiredCount++;
                    units += left;

                    int i = dataGridView1.Rows.Add(r["product_id"].ToString(), r["product_name"].ToString(), r["category_name"].ToString(),
                        left, exp.ToString("yyyy-MM-dd"), status, r["batch_no"].ToString());
                    DataGridViewRow row = dataGridView1.Rows[i];
                    row.Tag = new BatchRef
                    {
                        ProductId = r["product_id"].ToString(), ProductName = r["product_name"].ToString(), BatchNo = r["batch_no"].ToString(),
                        Expiry = exp, Remaining = left, Cost = Db.ToDec(r["cost_price"]), Price = Db.ToDec(r["selling_price"])
                    };
                    if (exp < DateTime.Today) row.Cells[5].Style.ForeColor = Color.Red;
                    else if (status == "Expiring soon") row.Cells[5].Style.ForeColor = Color.DarkOrange;
                    if (keep != null && keep == r["product_id"] + "|" + r["batch_no"]) { dataGridView1.ClearSelection(); row.Selected = true; }
                }
                if (keep == null) dataGridView1.ClearSelection();

                textBox2.Text = dataGridView1.Rows.Count.ToString();
                textBox3.Text = units.ToString();
                textBox8.Text = expiredCount.ToString();
                ShowSelected();
            }
            catch (Exception ex)
            {
                Ui.Error(ex, "Could not load expiry data");
            }
        }

        private BatchRef SelectedBatch()
        {
            return dataGridView1.SelectedRows.Count > 0 ? dataGridView1.SelectedRows[0].Tag as BatchRef : null;
        }

        private string SelectedBatchKey()
        {
            BatchRef b = SelectedBatch();
            return b == null ? null : b.ProductId + "|" + b.BatchNo;
        }

        private void ShowSelected()
        {
            BatchRef b = SelectedBatch();
            if (b == null)
            {
                textBox11.Clear(); textBox5.Clear(); textBox6.Clear();
                return;
            }
            textBox11.Text = b.ProductName + (b.BatchNo.Length > 0 ? " [" + b.BatchNo + "]" : "");
            textBox5.Text = b.Remaining.ToString();
            textBox6.Text = b.Remaining.ToString();
        }

        // ------------------------------------------------------------------ disposal
        private void PrepareDisposal(object sender, EventArgs e)
        {
            if (SelectedBatch() == null) { Ui.Warn("Select an expired item from the list first.", "No Selection"); return; }
            textBox6.Focus();
            textBox6.SelectAll();
        }

        /// <summary>CONFIRM DISPOSAL: takes the expired units out of stock and logs them as OUT / EXPIRED against their batch.</summary>
        private void ConfirmDisposal(object sender, EventArgs e)
        {
            BatchRef b = SelectedBatch();
            if (b == null) { Ui.Warn("Select an expired item from the list first.", "No Selection"); return; }

            int qty;
            if (!Ui.TryInt(textBox6.Text, out qty) || qty <= 0) { Ui.Warn("Enter the quantity to dispose (whole number greater than zero)."); textBox6.Focus(); return; }
            if (qty > b.Remaining) { Ui.Warn("This batch only has " + b.Remaining + " unit(s) left."); textBox6.Focus(); return; }
            string reason = textBox9.Text.Trim();
            if (reason.Length < 3) { Ui.Warn("Enter the reason for disposal."); textBox9.Focus(); return; }

            bool expired = b.Expiry < DateTime.Today;
            if (!expired && !Ui.Confirm("This batch has not expired yet (expires " + b.Expiry.ToString("yyyy-MM-dd") + ").\nDispose of it anyway?"))
                return;
            if (!Ui.Confirm("Dispose of " + qty + " x " + b.ProductName + "?\nLoss at cost: " + Ui.Money(qty * b.Cost) + "\n\nThis removes them from stock."))
                return;

            try
            {
                InventoryService.Apply(b.ProductId, "OUT", "EXPIRED", qty, b.BatchNo.Length == 0 ? null : b.BatchNo, b.Expiry, b.Cost,
                    null, null, Session.UserId, reason, null);
                Ui.Info("Disposal recorded and stock updated.", "Success");
                Search(null, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Ui.Error(ex);
            }
        }

        // ------------------------------------------------------------------ discount suggestions
        // tbl_product has no promo/discounted-price column, so prices are only suggested here, never saved.
        // A discount is applied at the register (POS) with manager approval.
        private bool ReadPercent(out decimal pct)
        {
            if (!Ui.TryMoney(textBox7.Text, out pct) || pct <= 0 || pct >= 100)
            {
                Ui.Warn("Enter a discount percentage between 1 and 99 in 'Apply Discount (%)'.");
                textBox7.Focus();
                return false;
            }
            return true;
        }

        private void SuggestPrice(object sender, EventArgs e)
        {
            BatchRef b = SelectedBatch();
            if (b == null) { Ui.Warn("Select an item from the list first.", "No Selection"); return; }
            decimal pct;
            if (!ReadPercent(out pct)) return;
            decimal price = Math.Round(b.Price * (1 - pct / 100m), 2);
            Ui.Info(b.ProductName + "\n\nRegular price: " + Ui.Money(b.Price) + "\nDiscount: " + pct + "%\nDiscounted price: " + Ui.Money(price) +
                    (price < b.Cost ? "\n\nWarning: this is below the cost price (" + Ui.Money(b.Cost) + ")." : "") +
                    "\n\nNote: the price is a suggestion only - apply it at the POS as a discount.", "Discounted Price");
        }

        private void ProcessDiscount(object sender, EventArgs e)
        {
            decimal pct;
            if (!ReadPercent(out pct)) return;

            var t = new DataTable();
            t.Columns.Add("Product ID", typeof(string));
            t.Columns.Add("Product", typeof(string));
            t.Columns.Add("Batch", typeof(string));
            t.Columns.Add("Expiry", typeof(string));
            t.Columns.Add("Units", typeof(int));
            t.Columns.Add("Regular Price", typeof(decimal));
            t.Columns.Add("Discount %", typeof(decimal));
            t.Columns.Add("Discounted Price", typeof(decimal));
            t.Columns.Add("Cost", typeof(decimal));
            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                var b = row.Tag as BatchRef;
                if (b == null || b.Expiry < DateTime.Today) continue;      // expired units are disposed, not discounted
                t.Rows.Add(b.ProductId, b.ProductName, b.BatchNo, b.Expiry.ToString("yyyy-MM-dd"), b.Remaining, b.Price, pct,
                    Math.Round(b.Price * (1 - pct / 100m), 2), b.Cost);
            }
            if (t.Rows.Count == 0) { Ui.Info("No near-expiry items in the list. Widen the 'To' date to include items that expire soon."); return; }
            ReportPreviewForm.Show("Suggested Markdown Prices", pct + "% off items in the list that have not expired yet (suggestions only)", t);
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
