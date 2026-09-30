using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace Inventory_Management_System_with_POS
{
    /// <summary>
    /// Manage Products (tbl_product, tbl_category, tbl_supplier, tbl_stock):
    /// add, edit, deactivate/activate, delete, search and summary counts.
    ///
    /// Control map:  textBox13 Product ID | textBox1 Name | comboBox1 Category | comboBox2 Supplier
    /// (this box was "Unit of Measure", which has no column in tbl_product) | textBox2 Cost | textBox3 Retail price |
    /// textBox4 Barcode | textBox5 Stock level | textBox6 Minimum stock alert | textBox10 Search |
    /// textBox9 / textBox7 / textBox8 = total / active / low-stock counts.
    /// </summary>
    public partial class frmManageProduct : Form
    {
        private string _editId;   // null = adding a new product

        public frmManageProduct()
        {
            InitializeComponent();
        }

        private class Input
        {
            public string Name, Barcode, CategoryId, SupplierId;
            public decimal Cost, Price;
            public int Stock, Threshold;
        }

        // ------------------------------------------------------------------ setup
        private void frmmanageproduct_Load(object sender, EventArgs e)
        {
            label6.Text = "Supplier:";
            textBox13.ReadOnly = true;
            textBox7.ReadOnly = true;
            textBox8.ReadOnly = true;
            textBox9.ReadOnly = true;
            comboBox1.DropDownStyle = ComboBoxStyle.DropDown;
            comboBox2.DropDownStyle = ComboBoxStyle.DropDown;
            Ui.SetupGrid(dataGridView1);

            Wire(button1, NewProduct);           // top tab: Add Product
            Wire(button2, EditSelected);         // top tab: Edit Product
            Wire(button3, ToggleStatus);         // top tab: Deactivate / Activate
            Wire(button4, NewProduct);           // CLEAR
            Wire(button5, EditSelected);         // EDIT
            Wire(button7, AddProduct);           // ADD NEW PRODUCT
            Wire(button8, UpdateProduct);        // UPDATE PRODUCT
            // button6 (DELETE) is wired by the designer to button6_Click

            textBox10.TextChanged += (s, a) => LoadProducts();
            dataGridView1.SelectionChanged += (s, a) => RefreshToggleCaption();
            dataGridView1.CellDoubleClick += (s, a) => { if (a.RowIndex >= 0) EditSelected(s, a); };

            try
            {
                LoadCombos();
                NewProduct(null, EventArgs.Empty);
                LoadProducts();
            }
            catch (Exception ex)
            {
                Ui.Error(ex, "Could not load products");
            }
        }

        private static void Wire(Button b, EventHandler h)
        {
            b.Click -= h;
            b.Click += h;
        }

        private void LoadCombos()
        {
            comboBox1.DataSource = null;
            DataTable cats = Db.Query("SELECT category_id, category_name FROM tbl_category ORDER BY category_name");
            comboBox1.DisplayMember = "category_name";
            comboBox1.ValueMember = "category_id";
            comboBox1.DataSource = cats;
            comboBox1.SelectedIndex = -1;

            comboBox2.DataSource = null;
            DataTable sup = Db.Query("SELECT supplier_id, supplier_name FROM tbl_supplier ORDER BY supplier_name");
            DataRow none = sup.NewRow();
            none["supplier_id"] = "";
            none["supplier_name"] = "(none)";
            sup.Rows.InsertAt(none, 0);
            comboBox2.DisplayMember = "supplier_name";
            comboBox2.ValueMember = "supplier_id";
            comboBox2.DataSource = sup;
            comboBox2.SelectedIndex = 0;
        }

        // ------------------------------------------------------------------ mode / clear
        private void NewProduct(object sender, EventArgs e)
        {
            _editId = null;
            textBox13.Text = "(auto)";
            textBox1.Clear();
            comboBox1.SelectedIndex = -1;
            comboBox1.Text = "";
            if (comboBox2.Items.Count > 0) comboBox2.SelectedIndex = 0;
            textBox2.Text = "0.00";
            textBox3.Text = "0.00";
            textBox4.Clear();
            textBox5.Text = "0";
            textBox5.ReadOnly = false;      // opening stock can be entered when adding
            textBox6.Text = "10";
            button7.Enabled = true;
            button8.Enabled = false;
            textBox1.Focus();
        }

        private void EnterEditMode(string id)
        {
            DataTable dt = Db.Query(
                @"SELECT p.product_id, p.product_name, IFNULL(p.barcode,'') AS barcode, p.category_id, IFNULL(p.supplier_id,'') AS supplier_id,
                         p.cost_price, p.selling_price, IFNULL(s.quantity,0) AS qty, IFNULL(s.low_stock_threshold,10) AS thr
                  FROM tbl_product p LEFT JOIN tbl_stock s ON s.product_id = p.product_id WHERE p.product_id = @id", Db.P("@id", id));
            if (dt.Rows.Count == 0) { Ui.Warn("That product no longer exists."); LoadProducts(); return; }
            DataRow r = dt.Rows[0];

            _editId = id;
            textBox13.Text = id;
            textBox1.Text = r["product_name"].ToString();
            comboBox1.SelectedValue = r["category_id"].ToString();
            comboBox2.SelectedValue = r["supplier_id"].ToString();
            textBox2.Text = Db.ToDec(r["cost_price"]).ToString("0.00");
            textBox3.Text = Db.ToDec(r["selling_price"]).ToString("0.00");
            textBox4.Text = r["barcode"].ToString();
            textBox5.Text = r["qty"].ToString();
            textBox5.ReadOnly = true;       // stock changes go through Manage Stocks so they are logged
            textBox6.Text = r["thr"].ToString();
            button7.Enabled = false;
            button8.Enabled = true;
            textBox1.Focus();
        }

        private void EditSelected(object sender, EventArgs e)
        {
            string id = SelectedId();
            if (id == null) { Ui.Warn("Select a product from the list first.", "No Selection"); return; }
            try { EnterEditMode(id); }
            catch (Exception ex) { Ui.Error(ex); }
        }

        // ------------------------------------------------------------------ grid
        private string SelectedId()
        {
            return dataGridView1.SelectedRows.Count > 0 ? dataGridView1.SelectedRows[0].Cells[0].Value as string : null;
        }

        private void RefreshToggleCaption()
        {
            if (dataGridView1.SelectedRows.Count == 0) { button3.Text = "Deactivate Product"; return; }
            string status = dataGridView1.SelectedRows[0].Cells[5].Value as string;
            button3.Text = status == "Inactive" ? "Activate Product" : "Deactivate Product";
        }

        private void LoadProducts()
        {
            try
            {
                string keep = SelectedId();
                string q = (textBox10.Text ?? "").Trim();
                DataTable dt = Db.Query(
                    @"SELECT p.product_id, p.product_name, c.category_name, p.selling_price, IFNULL(s.quantity,0) AS qty,
                             IFNULL(s.low_stock_threshold,10) AS thr, p.product_status, IFNULL(sp.supplier_name,'') AS supplier
                      FROM tbl_product p
                      JOIN tbl_category c ON c.category_id = p.category_id
                      LEFT JOIN tbl_stock s ON s.product_id = p.product_id
                      LEFT JOIN tbl_supplier sp ON sp.supplier_id = p.supplier_id
                      WHERE @q = '' OR p.product_id LIKE @l OR p.product_name LIKE @l OR IFNULL(p.barcode,'') LIKE @l
                            OR c.category_name LIKE @l OR IFNULL(sp.supplier_name,'') LIKE @l
                      ORDER BY p.product_id", Db.P("@q", q), Db.P("@l", "%" + q + "%"));

                dataGridView1.Rows.Clear();
                foreach (DataRow r in dt.Rows)
                {
                    int qty = Db.ToInt(r["qty"]);
                    bool active = r["product_status"].ToString() == "Active";
                    int i = dataGridView1.Rows.Add(r["product_id"].ToString(), r["product_name"].ToString(), r["category_name"].ToString(),
                        Db.ToDec(r["selling_price"]).ToString("N2"), qty, r["product_status"].ToString(), r["supplier"].ToString());
                    DataGridViewRow row = dataGridView1.Rows[i];
                    if (!active) row.DefaultCellStyle.ForeColor = Color.Gray;
                    else if (qty <= Db.ToInt(r["thr"])) row.Cells[4].Style.ForeColor = Color.Red;
                    if (keep != null && r["product_id"].ToString() == keep) { dataGridView1.ClearSelection(); row.Selected = true; }
                }
                if (keep == null) dataGridView1.ClearSelection();

                DataTable t = Db.Query(
                    @"SELECT COUNT(*) AS total,
                             IFNULL(SUM(p.product_status = 'Active'),0) AS active,
                             IFNULL(SUM(p.product_status = 'Active' AND IFNULL(s.quantity,0) <= IFNULL(s.low_stock_threshold,10)),0) AS low
                      FROM tbl_product p LEFT JOIN tbl_stock s ON s.product_id = p.product_id");
                textBox9.Text = Db.ToInt(t.Rows[0]["total"]).ToString();
                textBox7.Text = Db.ToInt(t.Rows[0]["active"]).ToString();
                textBox8.Text = Db.ToInt(t.Rows[0]["low"]).ToString();
                RefreshToggleCaption();
            }
            catch (Exception ex)
            {
                Ui.Error(ex, "Could not load products");
            }
        }

        // ------------------------------------------------------------------ validation
        private bool ReadForm(out Input v)
        {
            v = null;
            string name = textBox1.Text.Trim();
            if (name.Length == 0) { Ui.Warn("Enter the product name."); textBox1.Focus(); return false; }
            if (name.Length > 150) { Ui.Warn("Product name is too long (max 150 characters)."); return false; }
            if (comboBox1.Text.Trim().Length == 0) { Ui.Warn("Choose or type a category."); comboBox1.Focus(); return false; }

            decimal cost, price;
            if (!Ui.TryMoney(textBox2.Text, out cost) || cost < 0) { Ui.Warn("Enter a valid cost price."); textBox2.Focus(); return false; }
            if (!Ui.TryMoney(textBox3.Text, out price) || price < 0) { Ui.Warn("Enter a valid retail price."); textBox3.Focus(); return false; }
            if (price < cost && !Ui.Confirm("The retail price (" + Ui.Money(price) + ") is lower than the cost price (" + Ui.Money(cost) + ").\nSave anyway?"))
                return false;

            string barcode = textBox4.Text.Trim();
            if (barcode.Length > 50) { Ui.Warn("Barcode is too long (max 50 characters)."); return false; }

            int stock = 0, thr;
            if (_editId == null && !Ui.TryInt(textBox5.Text, out stock) ) { Ui.Warn("Enter the opening stock as a whole number (0 if none)."); textBox5.Focus(); return false; }
            if (stock < 0) { Ui.Warn("Stock can't be negative."); textBox5.Focus(); return false; }
            if (!Ui.TryInt(textBox6.Text, out thr) || thr < 0) { Ui.Warn("Enter the minimum stock alert as a whole number."); textBox6.Focus(); return false; }

            v = new Input { Name = name, Barcode = barcode, Cost = cost, Price = price, Stock = stock, Threshold = thr };
            return true;
        }

        /// <summary>Uses the chosen list item, or creates a new category/supplier when a new name was typed. Returns "" for none, null if cancelled.</summary>
        private string ResolveOrCreate(ComboBox cb, string table, string idCol, string nameCol, string prefix, int pad, string what)
        {
            string text = cb.Text.Trim();
            if (text.Length == 0 || text == "(none)") return "";

            var drv = cb.SelectedItem as DataRowView;
            if (drv != null && string.Equals(drv[nameCol].ToString(), text, StringComparison.OrdinalIgnoreCase))
                return drv[idCol].ToString();

            object existing = Db.Scalar("SELECT " + idCol + " FROM " + table + " WHERE " + nameCol + " = @n", Db.P("@n", text));
            if (existing != null) return existing.ToString();

            if (!Ui.Confirm("'" + text + "' is not in the list. Add it as a new " + what + "?")) return null;
            string id = Db.NextId(table, idCol, prefix, pad);
            Db.Exec("INSERT INTO " + table + " (" + idCol + ", " + nameCol + ") VALUES (@i, @n)", Db.P("@i", id), Db.P("@n", text));
            return id;
        }

        private bool ResolveLookups(Input v)
        {
            string cat = ResolveOrCreate(comboBox1, "tbl_category", "category_id", "category_name", "CAT-", 4, "category");
            if (cat == null || cat.Length == 0) return false;
            string sup = ResolveOrCreate(comboBox2, "tbl_supplier", "supplier_id", "supplier_name", "SUP-", 4, "supplier");
            if (sup == null) return false;
            v.CategoryId = cat;
            v.SupplierId = sup;
            return true;
        }

        private bool BarcodeTaken(string barcode, string exceptId)
        {
            if (barcode.Length == 0) return false;
            return Db.ToInt(Db.Scalar("SELECT COUNT(*) FROM tbl_product WHERE barcode = @b AND product_id <> @id",
                Db.P("@b", barcode), Db.P("@id", exceptId ?? ""))) > 0;
        }

        // ------------------------------------------------------------------ add
        /// <summary>ADD NEW PRODUCT: creates the product, its stock row, and (if opening stock &gt; 0) a stock-in log entry.</summary>
        private void AddProduct(object sender, EventArgs e)
        {
            Input v;
            if (!ReadForm(out v)) return;

            try
            {
                if (BarcodeTaken(v.Barcode, null)) { Ui.Warn("Another product already uses that barcode."); textBox4.Focus(); return; }
                if (!ResolveLookups(v)) return;

                StockInInfo batch = null;
                if (v.Stock > 0)
                {
                    batch = Ui.AskStockIn(v.Name, v.Cost, v.SupplierId);
                    if (batch == null) return;
                }

                string newId;
                using (var c = Db.Open())
                using (var tx = c.BeginTransaction())
                {
                    try
                    {
                        newId = Db.NextId("tbl_product", "product_id", "PRD-", 4, c, tx);
                        Db.Exec(c, tx,
                            @"INSERT INTO tbl_product (product_id, product_name, barcode, category_id, supplier_id, cost_price, selling_price, product_status)
                              VALUES (@id, @n, @b, @c, @s, @cost, @price, 'Active')",
                            Db.P("@id", newId), Db.P("@n", v.Name), Db.PN("@b", v.Barcode), Db.P("@c", v.CategoryId),
                            Db.PN("@s", v.SupplierId), Db.P("@cost", v.Cost), Db.P("@price", v.Price));
                        Db.Exec(c, tx, "INSERT INTO tbl_stock (product_id, quantity, low_stock_threshold) VALUES (@id, 0, @t)",
                            Db.P("@id", newId), Db.P("@t", v.Threshold));
                        if (batch != null)
                        {
                            InventoryService.ApplyMovement(c, tx, newId, "IN", "PURCHASE", v.Stock, batch.BatchNo, batch.Expiry,
                                batch.UnitCost, string.IsNullOrEmpty(batch.SupplierId) ? v.SupplierId : batch.SupplierId,
                                null, Session.UserId, "Opening stock on product creation", null);
                        }
                        tx.Commit();
                    }
                    catch
                    {
                        try { tx.Rollback(); } catch { }
                        throw;
                    }
                }

                Ui.Info("Product " + newId + " added.", "Success");
                LoadCombos();
                NewProduct(null, EventArgs.Empty);
                LoadProducts();
            }
            catch (Exception ex)
            {
                Ui.Error(ex);
            }
        }

        // ------------------------------------------------------------------ update
        /// <summary>UPDATE PRODUCT: saves name, category, supplier, prices, barcode and the minimum stock alert.</summary>
        private void UpdateProduct(object sender, EventArgs e)
        {
            if (_editId == null) { Ui.Warn("Select a product and press EDIT first."); return; }
            Input v;
            if (!ReadForm(out v)) return;

            try
            {
                if (BarcodeTaken(v.Barcode, _editId)) { Ui.Warn("Another product already uses that barcode."); textBox4.Focus(); return; }
                if (!ResolveLookups(v)) return;

                using (var c = Db.Open())
                using (var tx = c.BeginTransaction())
                {
                    try
                    {
                        Db.Exec(c, tx,
                            @"UPDATE tbl_product SET product_name=@n, barcode=@b, category_id=@c, supplier_id=@s, cost_price=@cost, selling_price=@price
                              WHERE product_id=@id",
                            Db.P("@n", v.Name), Db.PN("@b", v.Barcode), Db.P("@c", v.CategoryId), Db.PN("@s", v.SupplierId),
                            Db.P("@cost", v.Cost), Db.P("@price", v.Price), Db.P("@id", _editId));
                        Db.Exec(c, tx,
                            @"INSERT INTO tbl_stock (product_id, quantity, low_stock_threshold) VALUES (@id, 0, @t)
                              ON DUPLICATE KEY UPDATE low_stock_threshold = @t",
                            Db.P("@id", _editId), Db.P("@t", v.Threshold));
                        tx.Commit();
                    }
                    catch
                    {
                        try { tx.Rollback(); } catch { }
                        throw;
                    }
                }
                Ui.Info("Product updated.", "Success");
                LoadCombos();
                NewProduct(null, EventArgs.Empty);
                LoadProducts();
            }
            catch (Exception ex)
            {
                Ui.Error(ex);
            }
        }

        // ------------------------------------------------------------------ activate / deactivate
        private void ToggleStatus(object sender, EventArgs e)
        {
            string id = SelectedId();
            if (id == null) { Ui.Warn("Select a product from the list first.", "No Selection"); return; }
            string name = dataGridView1.SelectedRows[0].Cells[1].Value as string;
            bool active = (dataGridView1.SelectedRows[0].Cells[5].Value as string) == "Active";

            if (!Ui.Confirm(active
                    ? "Deactivate '" + name + "'? It can no longer be sold, but its history is kept."
                    : "Activate '" + name + "' so it can be sold again?"))
                return;
            try
            {
                Db.Exec("UPDATE tbl_product SET product_status = @s WHERE product_id = @id",
                    Db.P("@s", active ? "Inactive" : "Active"), Db.P("@id", id));
                LoadProducts();
            }
            catch (Exception ex)
            {
                Ui.Error(ex);
            }
        }

        // ------------------------------------------------------------------ delete (wired by the designer)
        /// <summary>
        /// DELETE: only for products with no sales and no stock movements other than the opening stock-in
        /// (for example one added by mistake). Anything else must be deactivated to keep the history.
        /// </summary>
        private void button6_Click(object sender, EventArgs e)
        {
            string id = SelectedId();
            if (id == null) { Ui.Warn("Select a product from the list first.", "No Selection"); return; }
            string name = dataGridView1.SelectedRows[0].Cells[1].Value as string;

            try
            {
                if (Db.ToInt(Db.Scalar("SELECT COUNT(*) FROM tbl_sale_item WHERE product_id = @id", Db.P("@id", id))) > 0)
                {
                    Ui.Warn("'" + name + "' has sales history and can't be deleted.\nUse Deactivate Product instead.", "Can't Delete");
                    return;
                }
                if (Db.ToInt(Db.Scalar(@"SELECT COUNT(*) FROM tbl_stock_log WHERE product_id = @id
                                         AND NOT (movement_type = 'IN' AND reason = 'PURCHASE')", Db.P("@id", id))) > 0)
                {
                    Ui.Warn("'" + name + "' has stock movements (sales, damage, adjustments...) and can't be deleted.\nUse Deactivate Product instead.", "Can't Delete");
                    return;
                }
                if (!Ui.Confirm("Permanently delete '" + name + "' (" + id + ")?\nThis can't be undone.", "Confirm Delete")) return;

                using (var c = Db.Open())
                using (var tx = c.BeginTransaction())
                {
                    try
                    {
                        Db.Exec(c, tx, "DELETE FROM tbl_stock_log WHERE product_id = @id", Db.P("@id", id));
                        Db.Exec(c, tx, "DELETE FROM tbl_product WHERE product_id = @id", Db.P("@id", id));   // tbl_stock row cascades
                        tx.Commit();
                    }
                    catch
                    {
                        try { tx.Rollback(); } catch { }
                        throw;
                    }
                }
                Ui.Info("Product deleted.", "Success");
                NewProduct(null, EventArgs.Empty);
                LoadProducts();
            }
            catch (Exception ex)
            {
                Ui.Error(ex);
            }
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void panel1_Paint(object sender, PaintEventArgs e) { }
    }
}
