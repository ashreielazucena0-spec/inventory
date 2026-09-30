using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Inventory_Management_System_with_POS
{
    /// <summary>
    /// Point of Sale: scan or search products, build the cart, apply approved discounts, take payment,
    /// save the sale (tbl_sale, tbl_sale_item, tbl_stock, tbl_stock_log) and print the receipt.
    ///
    /// Control map:  textBox13 Invoice no | comboBox1 Payment type | checkBox1 Cancel | textBox4 Barcode | textBox8 Search product |
    /// textBox3 Item code | textBox5 Product name | textBox1 Quantity | textBox2 Price | textBox7 Line discount | textBox6 Line total |
    /// textBox9 Sale discount | textBox10 Cash received | textBox11 Change | textBox12 Net total.
    /// Buttons: button1 Add | button2 Save | button3 Cancel | button4 Delete | button8 Print receipt.
    /// Discounts accept an amount (50) or a percentage (10%).
    /// </summary>
    public partial class frmPOS : Form
    {
        private class Prod
        {
            public string Id, Name;
            public decimal Price;
            public int Stock;
        }

        private readonly List<CartLine> _cart = new List<CartLine>();
        private Prod _current;
        private string _lastSaleId;
        private string _discountType;
        private string _approverId;
        private bool _resetting;

        public frmPOS()
        {
            InitializeComponent();
            timer1.Interval = 1000;
            timer1.Start();
        }

        private void frmPOS_Load(object sender, EventArgs e)
        {
            textBox13.ReadOnly = true;
            textBox5.ReadOnly = true;
            textBox2.ReadOnly = true;
            textBox6.ReadOnly = true;
            textBox11.ReadOnly = true;
            textBox12.ReadOnly = true;

            comboBox1.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBox1.Items.Clear();
            comboBox1.Items.AddRange(SalesService.PaymentMethods);

            Ui.SetupGrid(dataGridView1);
            foreach (DataGridViewColumn c in dataGridView1.Columns) c.SortMode = DataGridViewColumnSortMode.NotSortable;
            SetupCatalogGrid();
            textBox8.TextChanged += (s, a) => FilterCatalog();

            Wire(button1, AddClicked);          // Add
            Wire(button2, SaveSale);            // Save
            Wire(button3, CancelClicked);       // Cancel
            Wire(button4, DeleteLine);          // Delete
            Wire(button8, PrintReceipt);        // PRINT RECEIPT

            // the toolbar icons sit on top of the buttons, so make clicking an icon work too
            pictureBox2.Click += AddClicked;
            pictureBox3.Click += SaveSale;
            pictureBox4.Click += CancelClicked;
            pictureBox5.Click += DeleteLine;
            foreach (PictureBox pb in new[] { pictureBox2, pictureBox3, pictureBox4, pictureBox5 }) pb.Cursor = Cursors.Hand;

            textBox4.KeyDown += (s, a) => { if (a.KeyCode == Keys.Enter) { a.SuppressKeyPress = true; Scan(); } };
            textBox3.KeyDown += (s, a) => { if (a.KeyCode == Keys.Enter) { a.SuppressKeyPress = true; LookupById(); } };
            textBox8.KeyDown += (s, a) => { if (a.KeyCode == Keys.Enter) { a.SuppressKeyPress = true; SearchProduct(); } };
            textBox1.KeyDown += (s, a) => { if (a.KeyCode == Keys.Enter) { a.SuppressKeyPress = true; AddCurrent(); } };

            textBox1.TextChanged += (s, a) => RecalcLine();
            // textBox7 (line discount) is wired to textBox7_TextChanged by the designer
            textBox9.TextChanged += (s, a) => RecalcTotals();
            textBox9.Leave += (s, a) => CheckSaleDiscount();
            textBox10.TextChanged += (s, a) => UpdateChange();
            comboBox1.SelectedIndexChanged += (s, a) => PaymentChanged();
            checkBox1.CheckedChanged += (s, a) => { if (!_resetting && checkBox1.Checked) CancelTransaction(); };

            NewSale();
        }

        private static void Wire(Button b, EventHandler h)
        {
            b.Click -= h;
            b.Click += h;
        }

        // ------------------------------------------------------------------ new sale / cancel
        private void NewSale()
        {
            _resetting = true;
            _cart.Clear();
            _approverId = null;
            _discountType = null;
            dataGridView1.Rows.Clear();
            ClearEntry();
            textBox9.Text = "0";
            textBox10.Text = "";
            textBox11.Text = "0.00";
            textBox12.Text = "0.00";
            comboBox1.SelectedItem = "Cash";
            textBox10.ReadOnly = false;
            checkBox1.Checked = false;
            try { textBox13.Text = Db.NextId("tbl_sale", "sale_id", "SALE-", 6); }
            catch { textBox13.Text = "(new)"; }
            _resetting = false;
            LoadCatalog();
            textBox4.Focus();
        }

        private void ClearEntry()
        {
            _current = null;
            textBox3.Clear();
            textBox4.Clear();
            textBox5.Clear();
            textBox2.Clear();
            textBox1.Text = "1";
            textBox7.Text = "0";
            textBox6.Clear();
        }

        private void CancelClicked(object sender, EventArgs e)
        {
            CancelTransaction();
        }

        /// <summary>Cancel: clears the current, unsaved transaction after confirmation.</summary>
        private void CancelTransaction()
        {
            if (_cart.Count == 0)
            {
                NewSale();
                return;
            }
            if (Ui.Confirm("Cancel this transaction and clear the cart?", "Cancel Transaction"))
                NewSale();
            else
            {
                _resetting = true;
                checkBox1.Checked = false;
                _resetting = false;
            }
        }

        // ------------------------------------------------------------------ finding products
        private Prod LoadProd(string id)
        {
            DataTable dt = Db.Query(
                @"SELECT p.product_id, p.product_name, p.selling_price, p.product_status, IFNULL(s.quantity,0) AS qty
                  FROM tbl_product p LEFT JOIN tbl_stock s ON s.product_id = p.product_id WHERE p.product_id = @id", Db.P("@id", id));
            if (dt.Rows.Count == 0) return null;
            DataRow r = dt.Rows[0];
            if (r["product_status"].ToString() != "Active")
            {
                Ui.Warn(r["product_name"] + " is inactive and can't be sold.");
                return null;
            }
            if (Db.ToInt(r["qty"]) <= 0)
            {
                Ui.Warn(r["product_name"] + " is out of stock.");
                return null;
            }
            return new Prod { Id = id, Name = r["product_name"].ToString(), Price = Db.ToDec(r["selling_price"]), Stock = Db.ToInt(r["qty"]) };
        }

        private void ShowCurrent()
        {
            textBox3.Text = _current.Id;
            textBox5.Text = _current.Name;
            textBox2.Text = _current.Price.ToString("N2");
            RecalcLine();
        }

        private string FindIdExact(string key)
        {
            object o = Db.Scalar("SELECT product_id FROM tbl_product WHERE product_id = @k OR barcode = @k LIMIT 1", Db.P("@k", key));
            return o == null ? null : o.ToString();
        }

        /// <summary>Barcode box: Enter looks the product up and adds it to the cart straight away (scanner friendly).</summary>
        private void Scan()
        {
            string key = textBox4.Text.Trim();
            if (key.Length == 0) return;
            try
            {
                string id = FindIdExact(key);
                if (id == null) { Ui.Warn("No product found with barcode / code '" + key + "'."); textBox4.SelectAll(); return; }
                Prod p = LoadProd(id);
                if (p == null) { textBox4.SelectAll(); return; }
                _current = p;
                ShowCurrent();
                AddCurrent();
            }
            catch (Exception ex) { Ui.Error(ex); }
        }

        private void LookupById()
        {
            string key = textBox3.Text.Trim();
            if (key.Length == 0) return;
            try
            {
                string id = FindIdExact(key);
                if (id == null) { Ui.Warn("No product found with code '" + key + "'."); return; }
                Prod p = LoadProd(id);
                if (p == null) return;
                _current = p;
                ShowCurrent();
                textBox1.Focus();
                textBox1.SelectAll();
            }
            catch (Exception ex) { Ui.Error(ex); }
        }

        private void SearchProduct()
        {
            string term = textBox8.Text.Trim();
            if (term.Length == 0) return;
            try
            {
                string id = Ui.PickProduct(term, true);
                if (id == null) return;
                Prod p = LoadProd(id);
                if (p == null) return;
                _current = p;
                ShowCurrent();
                textBox8.Clear();
                textBox1.Focus();
                textBox1.SelectAll();
            }
            catch (Exception ex) { Ui.Error(ex); }
        }

        // ------------------------------------------------------------------ product list (left grid)
        private DataView _catalog;

        private static string LikeEscape(string s)
        {
            return s.Replace("'", "''").Replace("[", "[[]").Replace("%", "[%]").Replace("*", "[*]");
        }

        private void SetupCatalogGrid()
        {
            dgvProducts.ReadOnly = true;
            dgvProducts.AllowUserToAddRows = false;
            dgvProducts.AllowUserToDeleteRows = false;
            dgvProducts.MultiSelect = false;
            dgvProducts.RowHeadersVisible = false;
            dgvProducts.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvProducts.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            // click = load the product into the entry boxes, double-click / Enter = add it to the cart
            dgvProducts.CellClick += (s, a) => { if (a.RowIndex >= 0) PickFromCatalog(a.RowIndex, false); };
            dgvProducts.CellDoubleClick += (s, a) => { if (a.RowIndex >= 0) PickFromCatalog(a.RowIndex, true); };
            dgvProducts.KeyDown += (s, a) =>
            {
                if (a.KeyCode == Keys.Enter && dgvProducts.CurrentRow != null)
                {
                    a.SuppressKeyPress = true;
                    PickFromCatalog(dgvProducts.CurrentRow.Index, true);
                }
            };

            // out-of-stock rows are greyed out
            dgvProducts.CellFormatting += (s, a) =>
            {
                if (a.RowIndex < 0 || !dgvProducts.Columns.Contains("Stock")) return;
                object q = dgvProducts.Rows[a.RowIndex].Cells["Stock"].Value;
                if (q != null && q != DBNull.Value && Convert.ToInt32(q) <= 0)
                    a.CellStyle.ForeColor = Color.Gray;
            };
        }

        /// <summary>Loads every active product into the left grid (runs when the POS opens and after each sale).</summary>
        private void LoadCatalog()
        {
            try
            {
                DataTable dt = Db.Query(
                    @"SELECT p.product_id AS `Code`, p.product_name AS `Product`,
                             p.selling_price AS `Price`, IFNULL(s.quantity,0) AS `Stock`
                      FROM tbl_product p
                      LEFT JOIN tbl_stock s ON s.product_id = p.product_id
                      WHERE p.product_status = 'Active'
                      ORDER BY p.product_name");

                _catalog = dt.DefaultView;
                dgvProducts.DataSource = _catalog;

                dgvProducts.Columns["Code"].FillWeight = 22;
                dgvProducts.Columns["Product"].FillWeight = 46;
                dgvProducts.Columns["Price"].FillWeight = 16;
                dgvProducts.Columns["Stock"].FillWeight = 16;
                dgvProducts.Columns["Price"].DefaultCellStyle.Format = "N2";
                dgvProducts.Columns["Price"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                dgvProducts.Columns["Stock"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                FilterCatalog();
                dgvProducts.ClearSelection();
            }
            catch (Exception ex) { Ui.Error(ex); }
        }

        /// <summary>Typing in the Search Product box narrows the list as you type.</summary>
        private void FilterCatalog()
        {
            if (_catalog == null) return;
            string t = LikeEscape(textBox8.Text.Trim());
            _catalog.RowFilter = t.Length == 0 ? ""
                : "[Code] LIKE '%" + t + "%' OR [Product] LIKE '%" + t + "%'";
        }

        private void PickFromCatalog(int rowIndex, bool addToCart)
        {
            try
            {
                DataGridViewRow row = dgvProducts.Rows[rowIndex];
                string id = row.Cells["Code"].Value.ToString();
                if (!addToCart && Db.ToInt(row.Cells["Stock"].Value) <= 0) return; // plain click on an out-of-stock row: stay quiet

                Prod p = LoadProd(id);            // warns if inactive / out of stock
                if (p == null) return;
                _current = p;
                ShowCurrent();
                if (addToCart) AddCurrent();
            }
            catch (Exception ex) { Ui.Error(ex); }
        }

        // ------------------------------------------------------------------ line entry
        private void RecalcLine()
        {
            if (_current == null) { textBox6.Clear(); return; }
            int qty;
            if (!Ui.TryInt(textBox1.Text, out qty) || qty <= 0) { textBox6.Clear(); return; }
            decimal basis = qty * _current.Price, disc;
            if (!Ui.TryAmountOrPercent(textBox7.Text, basis, out disc) || disc > basis) { textBox6.Clear(); return; }
            textBox6.Text = (basis - disc).ToString("N2");
        }

        private void textBox7_TextChanged(object sender, EventArgs e)
        {
            RecalcLine();
        }

        private void AddClicked(object sender, EventArgs e)
        {
            AddCurrent();
        }

        /// <summary>Add: puts the loaded product (quantity, line discount) into the cart.</summary>
        private void AddCurrent()
        {
            if (_current == null) { Ui.Warn("Scan a barcode or search for a product first."); textBox4.Focus(); return; }

            int qty;
            if (!Ui.TryInt(textBox1.Text, out qty) || qty <= 0) { Ui.Warn("Enter a quantity greater than zero."); textBox1.Focus(); return; }

            decimal basis = qty * _current.Price, lineDisc;
            if (!Ui.TryAmountOrPercent(textBox7.Text, basis, out lineDisc) || lineDisc > basis)
            {
                Ui.Warn("Enter a valid discount: an amount up to " + basis.ToString("N2") + " or a percentage like 10%.");
                textBox7.Focus();
                return;
            }

            int inCart = _cart.Where(l => l.ProductId == _current.Id).Sum(l => l.Qty);
            if (qty + inCart > _current.Stock)
            {
                Ui.Warn("Only " + _current.Stock + " of '" + _current.Name + "' in stock" + (inCart > 0 ? " (" + inCart + " already in the cart)." : "."));
                textBox1.Focus();
                return;
            }

            if (lineDisc > 0 && !EnsureApproval()) return;

            CartLine same = lineDisc == 0 ? _cart.FirstOrDefault(l => l.ProductId == _current.Id && l.LineDiscount == 0) : null;
            if (same != null) same.Qty += qty;
            else _cart.Add(new CartLine { ProductId = _current.Id, ProductName = _current.Name, Qty = qty, UnitPrice = _current.Price, LineDiscount = lineDisc });

            RefreshGrid();
            RecalcTotals();
            ClearEntry();
            textBox4.Focus();
        }

        /// <summary>Delete: removes the selected cart line.</summary>
        private void DeleteLine(object sender, EventArgs e)
        {
            if (dataGridView1.SelectedRows.Count == 0) { Ui.Warn("Select the cart line to delete.", "No Selection"); return; }
            int idx = dataGridView1.SelectedRows[0].Index;
            if (idx < 0 || idx >= _cart.Count) return;
            _cart.RemoveAt(idx);
            RefreshGrid();
            RecalcTotals();
        }

        private void RefreshGrid()
        {
            dataGridView1.Rows.Clear();
            foreach (CartLine l in _cart)
                dataGridView1.Rows.Add(l.Qty, l.ProductId, l.ProductName, l.UnitPrice.ToString("N2"), l.LineDiscount.ToString("N2"), l.LineTotal.ToString("N2"));
            dataGridView1.ClearSelection();
        }

        // ------------------------------------------------------------------ discount approval
        private bool AnyDiscount()
        {
            decimal sub = _cart.Sum(l => l.LineTotal), d;
            Ui.TryAmountOrPercent(textBox9.Text, sub, out d);
            return d > 0 || _cart.Any(l => l.LineDiscount > 0);
        }

        /// <summary>One approval covers every discount on this sale.</summary>
        private bool EnsureApproval()
        {
            if (_approverId != null) return true;
            string type, approver;
            if (!Ui.ApproveDiscount(out type, out approver)) return false;
            _discountType = type;
            _approverId = approver;
            return true;
        }

        private void CheckSaleDiscount()
        {
            decimal sub = _cart.Sum(l => l.LineTotal), d;
            if (!Ui.TryAmountOrPercent(textBox9.Text, sub, out d) || d > sub)
            {
                Ui.Warn("Enter a valid discount: an amount up to " + sub.ToString("N2") + " or a percentage like 10%.");
                textBox9.Text = "0";
                return;
            }
            if (d > 0 && !EnsureApproval()) textBox9.Text = "0";
        }

        // ------------------------------------------------------------------ totals / payment
        private void Totals(out decimal subtotal, out decimal discount, out decimal tax, out decimal net)
        {
            subtotal = _cart.Sum(l => l.LineTotal);
            if (!Ui.TryAmountOrPercent(textBox9.Text, subtotal, out discount) || discount > subtotal) discount = 0;
            tax = Math.Round((subtotal - discount) * SalesService.TaxRate, 2);
            net = subtotal - discount + tax;
        }

        private void RecalcTotals()
        {
            decimal sub, disc, tax, net;
            Totals(out sub, out disc, out tax, out net);
            if (!AnyDiscount()) { _approverId = null; _discountType = null; }
            textBox12.Text = net.ToString("N2");
            if (comboBox1.Text != "Cash") textBox10.Text = net.ToString("N2");
            UpdateChange();
        }

        private void PaymentChanged()
        {
            bool cash = comboBox1.Text == "Cash";
            textBox10.ReadOnly = !cash;
            RecalcTotals();
            if (cash) textBox10.Clear();
        }

        private void UpdateChange()
        {
            decimal sub, disc, tax, net, paid;
            Totals(out sub, out disc, out tax, out net);
            if (!Ui.TryMoney(textBox10.Text, out paid)) { textBox11.Text = "0.00"; return; }
            decimal change = paid - net;
            textBox11.Text = (change > 0 ? change : 0m).ToString("N2");
        }

        // ------------------------------------------------------------------ save / print
        /// <summary>Save: completes the sale in one database transaction and offers to print the receipt.</summary>
        private void SaveSale(object sender, EventArgs e)
        {
            if (_cart.Count == 0) { Ui.Warn("The cart is empty."); textBox4.Focus(); return; }
            if (!Session.IsLoggedIn) { Ui.Error("No cashier is logged in."); return; }

            decimal sub, disc, tax, net;
            Totals(out sub, out disc, out tax, out net);
            string method = comboBox1.Text;
            if (method.Length == 0) { Ui.Warn("Choose the payment type."); comboBox1.Focus(); return; }

            decimal paid;
            if (method == "Cash")
            {
                if (!Ui.TryMoney(textBox10.Text, out paid) || paid < net)
                {
                    Ui.Warn("Cash received must be at least the net total (" + Ui.Money(net) + ").");
                    textBox10.Focus();
                    return;
                }
            }
            else paid = net;

            if (AnyDiscount() && !EnsureApproval()) return;

            try
            {
                decimal change;
                string saleId = SalesService.CompleteSale(_cart, disc, _discountType, _approverId, method, paid, Session.UserId, out change);
                _lastSaleId = saleId;

                bool print = Ui.Confirm("Sale " + saleId + " saved.\nTotal: " + Ui.Money(net) + "\nChange: " + Ui.Money(change) + "\n\nPrint the receipt?", "Sale Complete");
                if (print) PrintReceipt(null, EventArgs.Empty);
                NewSale();
            }
            catch (InvalidOperationException ex)
            {
                Ui.Warn(ex.Message, "Sale Not Saved");          // e.g. not enough stock; nothing was saved
            }
            catch (Exception ex)
            {
                Ui.Error(ex, "Sale Not Saved");
            }
        }

        /// <summary>PRINT RECEIPT: prints the last saved sale.</summary>
        private void PrintReceipt(object sender, EventArgs e)
        {
            if (_lastSaleId == null)
            {
                Ui.Warn("Save a sale first - the receipt prints from the saved sale.");
                return;
            }
            try
            {
                Printer.PrintText("Receipt " + _lastSaleId, SalesService.ReceiptLines(_lastSaleId), 9f, false);
            }
            catch (Exception ex)
            {
                Ui.Error(ex);
            }
        }

        // ------------------------------------------------------------------ designer handlers
        private void timer1_Tick(object sender, EventArgs e)
        {
            label5.Text = DateTime.Now.ToLongDateString();
            label6.Text = DateTime.Now.ToLongTimeString();
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void label5_Click(object sender, EventArgs e) { }

        private void panel1_Paint(object sender, PaintEventArgs e) { }
        private void label6_Click(object sender, EventArgs e) { }
    }
}