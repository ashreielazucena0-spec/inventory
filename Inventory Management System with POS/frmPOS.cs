using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Inventory_Management_System_with_POS
{
    /// <summary>
    /// Point of Sale: scan or search products, build the cart, adjust quantities right in the cart table,
    /// apply approved discounts, take payment, save the sale (tbl_sale, tbl_sale_item, tbl_stock, tbl_stock_log)
    /// and print the receipt (58mm paper).
    ///
    /// Control map (matches frmPOS.Designer.cs):
    ///   textBox13 Invoice no | comboBox1 Payment type | textBox4 Barcode | textBox8 Search product |
    ///   textBox9 Sale discount | textBox10 Cash received | textBox11 Change | textBox12 Net total | label20 VAT amount
    ///   dgvProducts  = product list (left)      dataGridView1 = cart (right, Qty column is editable)
    /// Buttons: button1 Add | button3 Cancel | button4 Delete | button8 Print receipt.
    /// Discount accepts an amount (50) or a percentage (10%).
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
        private Prod _current;              // product picked from the list (single click) waiting for the Add button
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
            textBox11.ReadOnly = true;
            textBox12.ReadOnly = true;

            comboBox1.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBox1.Items.Clear();
            comboBox1.Items.AddRange(SalesService.PaymentMethods);

            Ui.SetupGrid(dataGridView1);
            foreach (DataGridViewColumn c in dataGridView1.Columns) c.SortMode = DataGridViewColumnSortMode.NotSortable;
            SetupCartGrid();
            SetupCatalogGrid();
            textBox8.TextChanged += (s, a) => FilterCatalog();

            Wire(button1, AddClicked);          // Add
            Wire(button3, CancelClicked);       // Cancel
            Wire(button4, DeleteLine);          // Delete
            Wire(button8, PayClicked);          // PRINT RECEIPT = pay + print (reprints the last receipt when the cart is empty)

            // the toolbar icons sit on top of the buttons, so make clicking an icon work too
            pictureBox2.Click += AddClicked;
            pictureBox4.Click += CancelClicked;
            pictureBox5.Click += DeleteLine;
            foreach (PictureBox pb in new[] { pictureBox2, pictureBox4, pictureBox5 }) pb.Cursor = Cursors.Hand;

            textBox4.KeyDown += (s, a) => { if (a.KeyCode == Keys.Enter) { a.SuppressKeyPress = true; Scan(); } };
            textBox8.KeyDown += (s, a) => { if (a.KeyCode == Keys.Enter) { a.SuppressKeyPress = true; SearchProduct(); } };
            textBox10.KeyDown += (s, a) => { if (a.KeyCode == Keys.Enter) { a.SuppressKeyPress = true; ProcessPayment(); } };   // pay

            textBox9.TextChanged += (s, a) => RecalcTotals();
            textBox9.Leave += (s, a) => CheckSaleDiscount();
            textBox10.TextChanged += (s, a) => UpdateChange();
            comboBox1.SelectedIndexChanged += (s, a) => PaymentChanged();

            KeyPreview = true;                  // F-keys are handled in ProcessCmdKey (works from any control)
            FixLayout();
            ShowShortcutBar();

            timer1_Tick(null, EventArgs.Empty);
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
            label20.Text = "0.00";
            comboBox1.SelectedItem = "Cash";
            textBox10.ReadOnly = false;

            try { textBox13.Text = Db.NextId("tbl_sale", "sale_id", "SALE-", 6); }
            catch { textBox13.Text = "(new)"; }
            _resetting = false;
            LoadCatalog();
            textBox4.Focus();
        }

        private void ClearEntry()
        {
            _current = null;
            textBox4.Clear();
        }

        private void CancelClicked(object sender, EventArgs e)
        {
            CancelTransaction();
        }

        /// <summary>Cancel: clears the current, unsaved transaction after confirmation.</summary>
        private void CancelTransaction()
        {
            dataGridView1.EndEdit();
            if (_cart.Count == 0)
            {
                NewSale();
                return;
            }
            if (Ui.Confirm("Cancel this transaction and clear the cart?", "Cancel Transaction"))
                NewSale();
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

        private string FindIdExact(string key)
        {
            object o = Db.Scalar("SELECT product_id FROM tbl_product WHERE product_id = @k OR barcode = @k LIMIT 1", Db.P("@k", key));
            return o == null ? null : o.ToString();
        }

        /// <summary>Current stock of a product straight from the database (used when the Qty is edited in the cart).</summary>
        private int StockOf(string productId)
        {
            object o = Db.Scalar("SELECT IFNULL(quantity,0) FROM tbl_stock WHERE product_id = @id", Db.P("@id", productId));
            return o == null || o == DBNull.Value ? 0 : Db.ToInt(o);
        }

        /// <summary>Barcode box: Enter looks the product up and adds 1 to the cart straight away (scanner friendly).</summary>
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
                AddProduct(p, 1);
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
                textBox8.Clear();
                AddProduct(p, 1);
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

            // click = pick the product (then press Add), double-click / Enter = add 1 to the cart
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
                if (addToCart) AddProduct(p, 1);
                else _current = p;
            }
            catch (Exception ex) { Ui.Error(ex); }
        }

        // ------------------------------------------------------------------ cart (right grid)
        /// <summary>Only the Qty column (Column1) can be edited; everything else in the cart is read-only.</summary>
        private void SetupCartGrid()
        {
            dataGridView1.AllowUserToAddRows = false;
            dataGridView1.AllowUserToDeleteRows = false;
            dataGridView1.MultiSelect = false;
            dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridView1.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;

            dataGridView1.ReadOnly = false;                       // SetupGrid may have locked the whole grid
            foreach (DataGridViewColumn c in dataGridView1.Columns) c.ReadOnly = true;
            Column1.ReadOnly = false;                             // Qty.
            Column1.DefaultCellStyle.BackColor = Color.LightYellow;   // hint that this column can be edited
            Column1.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            dataGridView1.CellClick += (s, a) =>
            {
                // one click on a Qty cell starts editing
                if (a.RowIndex >= 0 && a.ColumnIndex == Column1.Index)
                {
                    dataGridView1.CurrentCell = dataGridView1.Rows[a.RowIndex].Cells[a.ColumnIndex];
                    dataGridView1.BeginEdit(true);
                }
            };
            dataGridView1.EditingControlShowing += CartEditingControlShowing;
            dataGridView1.CellEndEdit += CartCellEndEdit;
        }

        private void CartEditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            TextBox tb = e.Control as TextBox;
            if (tb == null) return;
            tb.KeyPress -= QtyKeyPress;
            if (dataGridView1.CurrentCell != null && dataGridView1.CurrentCell.ColumnIndex == Column1.Index)
                tb.KeyPress += QtyKeyPress;                       // digits only in the Qty cell
        }

        private void QtyKeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true;
        }

        /// <summary>The user finished typing a new quantity: validate it, check stock, update the cart and totals.</summary>
        private void CartCellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != Column1.Index || e.RowIndex >= _cart.Count) return;

            CartLine line = _cart[e.RowIndex];
            DataGridViewCell cell = dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex];

            int qty;
            if (!Ui.TryInt(Convert.ToString(cell.Value), out qty) || qty <= 0)
            {
                Ui.Warn("Enter a quantity of 1 or more. To remove the item, select it and click Delete.");
                cell.Value = line.Qty;
                return;
            }
            if (qty == line.Qty) { cell.Value = line.Qty; return; }

            try
            {
                int stock = StockOf(line.ProductId);
                int others = _cart.Where(l => !ReferenceEquals(l, line) && l.ProductId == line.ProductId).Sum(l => l.Qty);
                if (qty + others > stock)
                {
                    Ui.Warn("Only " + stock + " of '" + line.ProductName + "' in stock" + (others > 0 ? " (" + others + " on another cart line)." : "."));
                    cell.Value = line.Qty;
                    return;
                }

                // keep the discount per unit, so a 10% line discount stays 10% after the quantity changes
                if (line.LineDiscount > 0 && line.Qty > 0)
                    line.LineDiscount = Math.Round(line.LineDiscount / line.Qty * qty, 2);
                line.Qty = qty;
                    
                UpdateCartRow(e.RowIndex);
                RecalcTotals();
            }
            catch (Exception ex)
            {
                Ui.Error(ex);
                cell.Value = line.Qty;
            }
        }

        private void UpdateCartRow(int index)
        {
            CartLine l = _cart[index];
            DataGridViewRow r = dataGridView1.Rows[index];
            r.Cells[0].Value = l.Qty;
            r.Cells[4].Value = l.LineDiscount.ToString("N2");
            r.Cells[5].Value = l.LineTotal.ToString("N2");
        }

        /// <summary>Puts a product into the cart (merges with an existing undiscounted line of the same product).</summary>
        private void AddProduct(Prod p, int qty)
        {
            dataGridView1.EndEdit();

            int inCart = _cart.Where(l => l.ProductId == p.Id).Sum(l => l.Qty);
            if (qty + inCart > p.Stock)
            {
                Ui.Warn("Only " + p.Stock + " of '" + p.Name + "' in stock" + (inCart > 0 ? " (" + inCart + " already in the cart)." : "."));
                textBox4.Focus();
                return;
            }

            CartLine line = _cart.FirstOrDefault(l => l.ProductId == p.Id && l.LineDiscount == 0);
            if (line != null) line.Qty += qty;
            else
            {
                line = new CartLine { ProductId = p.Id, ProductName = p.Name, Qty = qty, UnitPrice = p.Price, LineDiscount = 0 };
                _cart.Add(line);
            }

            RefreshGrid();
            int idx = _cart.IndexOf(line);
            if (idx >= 0 && idx < dataGridView1.Rows.Count) dataGridView1.Rows[idx].Selected = true;
            RecalcTotals();
            ClearEntry();
            textBox4.Focus();
        }

        private void AddClicked(object sender, EventArgs e)
        {
            AddCurrent();
        }

        /// <summary>Add: adds the barcode typed in the box, or the product picked in the list, to the cart (qty 1 - change it in the cart).</summary>
        private void AddCurrent()
        {
            if (textBox4.Text.Trim().Length > 0) { Scan(); return; }
            if (_current == null)
            {
                Ui.Warn("Scan a barcode, or click a product in the list first.");
                textBox4.Focus();
                return;
            }
            AddProduct(_current, 1);
        }

        /// <summary>Delete: removes the selected cart line.</summary>
        private void DeleteLine(object sender, EventArgs e)
        {
            dataGridView1.EndEdit();
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
            label20.Text = tax.ToString("N2");
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
        /// <summary>Pay: press Enter in "Cash received". Saves the sale in one database transaction and prints the receipt automatically.</summary>
        private void ProcessPayment()
        {
            dataGridView1.EndEdit();                  // commit a quantity that is still being typed

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

                // payment goes through, then the receipt prints automatically (no save button, no prompt)
                PrintReceipt(null, EventArgs.Empty);

                // show the change only when there is some to give back
                if (change > 0)
                    MessageBox.Show("Change: " + Ui.Money(change), "Payment Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

        /// <summary>PRINT RECEIPT button: with items in the cart it takes the payment, saves the sale and prints;
        /// with an empty cart it reprints the last receipt.</summary>
        private void PayClicked(object sender, EventArgs e)
        {
            dataGridView1.EndEdit();
            if (_cart.Count > 0) ProcessPayment();
            else PrintReceipt(sender, e);
        }

        /// <summary>PRINT RECEIPT: prints the last saved sale on 58mm thermal paper.</summary>
        private void PrintReceipt(object sender, EventArgs e)
        {
            if (_lastSaleId == null)
            {
                Ui.Warn("Nothing to print yet - add items to the cart, then click PRINT RECEIPT to pay and print.");
                return;
            }
            try
            {
                Printer58.Print("Receipt " + _lastSaleId, SalesService.ReceiptLines(_lastSaleId));
            }
            catch (Exception ex)
            {
                Ui.Error(ex);
            }
        }

        // ------------------------------------------------------------------ keyboard shortcuts (F1 - F12)
        //   F1  Barcode box          F5  Delete cart line      F9   Cancel transaction
        //   F2  Search product       F6  Discount box          F10  Pay + print receipt
        //   F3  Add                  F7  Payment type          F11  Reprint last receipt
        //   F4  Edit Qty (cart)      F8  Cash received         F12  Close POS
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (HandleShortcut(keyData)) return true;
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private bool HandleShortcut(Keys key)
        {
            switch (key)
            {
                case Keys.F1:
                    textBox4.Focus(); textBox4.SelectAll();
                    return true;
                case Keys.F2:
                    textBox8.Focus(); textBox8.SelectAll();
                    return true;
                case Keys.F3:
                    AddCurrent();
                    return true;
                case Keys.F4:
                    EditSelectedQty();
                    return true;
                case Keys.F5:
                    DeleteLine(null, EventArgs.Empty);
                    return true;
                case Keys.F6:
                    textBox9.Focus(); textBox9.SelectAll();
                    return true;
                case Keys.F7:
                    comboBox1.Focus(); comboBox1.DroppedDown = true;
                    return true;
                case Keys.F8:
                    if (textBox10.ReadOnly) Ui.Warn("Cash received is only used for Cash payments.");
                    else { textBox10.Focus(); textBox10.SelectAll(); }
                    return true;
                case Keys.F9:
                    CancelTransaction();
                    return true;
                case Keys.F10:
                    ProcessPayment();
                    return true;
                case Keys.F11:
                    PrintReceipt(null, EventArgs.Empty);
                    return true;
                case Keys.F12:
                    Close();
                    return true;
            }
            return false;
        }

        /// <summary>F4: starts editing the Qty of the selected cart line (the last line when none is selected).</summary>
        private void EditSelectedQty()
        {
            if (_cart.Count == 0 || dataGridView1.Rows.Count == 0) { Ui.Warn("The cart is empty."); return; }
            int idx = dataGridView1.SelectedRows.Count > 0 ? dataGridView1.SelectedRows[0].Index : dataGridView1.Rows.Count - 1;
            dataGridView1.Rows[idx].Selected = true;
            dataGridView1.CurrentCell = dataGridView1.Rows[idx].Cells[Column1.Index];
            dataGridView1.Focus();
            dataGridView1.BeginEdit(true);
        }

        /// <summary>
        /// The form opens maximized, so anything that is not anchored stays where the designer put it
        /// while the grids stretch over it. This glues the bottom row to the bottom of the window.
        /// </summary>
        private void FixLayout()
        {
            // payment row: stays at the bottom-left, keeping its spacing
            foreach (Control c in new Control[] { label16, textBox9, label11, label20, label17, textBox10, label18, textBox11, button8 })
                c.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;

            // net total: stays at the bottom-right
            foreach (Control c in new Control[] { label19, textBox12 })
                c.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

            // VAT caption next to the Discount box, the amount right under it (positions relative to the Discount box)
            label11.Left = textBox9.Right + 3;
            label11.Top = textBox9.Top - 6;
            label20.Left = label11.Left + 2;
            label20.Top = label11.Bottom + 2;

            // both grids end at the same height
            dataGridView1.Height = dgvProducts.Bottom - dataGridView1.Top;
        }

        /// <summary>Small hint line under the product list + tooltips on the toolbar buttons.</summary>
        private void ShowShortcutBar()
        {
            Label bar = new Label
            {
                AutoSize = false,
                Left = 12,
                Top = dgvProducts.Bottom + 6,          // right under the grids, never over them
                Width = ClientSize.Width - 24,
                Height = 40,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold),
                ForeColor = Color.DarkSlateGray,
                Text = "F1 Barcode   F2 Search   F3 Add   F4 Edit Qty   F5 Delete   F6 Discount\r\n" +
                       "F7 Payment Type   F8 Cash Received   F9 Cancel   F10 Pay && Print   F11 Reprint   F12 Close"
            };
            Controls.Add(bar);
            bar.BringToFront();

            ToolTip tip = new ToolTip();
            tip.SetToolTip(button1, "Add (F3)");
            tip.SetToolTip(button3, "Cancel transaction (F9)");
            tip.SetToolTip(button4, "Delete selected line (F5)");
            tip.SetToolTip(button8, "Pay and print receipt (F10) / reprint (F11)");
            tip.SetToolTip(textBox4, "Barcode (F1)");
            tip.SetToolTip(textBox8, "Search product (F2)");
            tip.SetToolTip(textBox9, "Discount (F6)");
            tip.SetToolTip(textBox10, "Cash received (F8)");
            tip.SetToolTip(comboBox1, "Payment type (F7)");
        }

        // ------------------------------------------------------------------ designer handlers
        private void timer1_Tick(object sender, EventArgs e)
        {
            label5.Text = DateTime.Now.ToLongTimeString();   // big label  = time
            label6.Text = DateTime.Now.ToLongDateString();   // small label = date
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void label5_Click(object sender, EventArgs e) { }

        private void panel1_Paint(object sender, PaintEventArgs e) { }
        private void label6_Click(object sender, EventArgs e) { }

        // kept because the designer references it; the real handler is wired in frmPOS_Load
        private void button8_Click(object sender, EventArgs e)
        {
        }

        private void label11_Click(object sender, EventArgs e)
        {

        }

        private void label20_Click(object sender, EventArgs e)
        {

        }
    }
}