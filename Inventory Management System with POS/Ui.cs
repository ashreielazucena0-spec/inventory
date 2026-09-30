using System;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace Inventory_Management_System_with_POS
{
    public class StockInInfo
    {
        public DateTime? Expiry;
        public decimal UnitCost;
        public string SupplierId;
        public string BatchNo;
    }

    /// <summary>Message boxes, number parsing, small pop-up dialogs and navigation helpers shared by all forms.</summary>
    public static class Ui
    {
        public const string Peso = "\u20B1";

        public static string Money(decimal v)
        {
            return Peso + v.ToString("N2", CultureInfo.CurrentCulture);
        }

        // ---------------- message boxes ----------------
        public static void Info(string message, string title = "Information")
        {
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public static void Warn(string message, string title = "Notice")
        {
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        public static void Error(string message, string title = "Error")
        {
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public static void Error(Exception ex, string title = "Error")
        {
            MessageBox.Show(Db.Friendly(ex), title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public static bool Confirm(string message, string title = "Confirm")
        {
            return MessageBox.Show(message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }

        // ---------------- parsing ----------------
        public static bool TryMoney(string text, out decimal value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = text.Replace(Peso, "").Replace("PHP", "").Trim();
            return decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value);
        }

        public static bool TryInt(string text, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            return int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out value);
        }

        /// <summary>Accepts "50" (an amount) or "10%" (a percentage of basis). Blank counts as 0.</summary>
        public static bool TryAmountOrPercent(string text, decimal basis, out decimal amount)
        {
            amount = 0;
            if (string.IsNullOrWhiteSpace(text)) return true;
            text = text.Trim();
            if (text.EndsWith("%"))
            {
                decimal pct;
                if (!TryMoney(text.Substring(0, text.Length - 1), out pct) || pct < 0 || pct > 100) return false;
                amount = Math.Round(basis * pct / 100m, 2);
                return true;
            }
            if (!TryMoney(text, out amount) || amount < 0) return false;
            amount = Math.Round(amount, 2);
            return true;
        }

        // ---------------- grid helpers ----------------
        public static void SetupGrid(DataGridView g)
        {
            g.ReadOnly = true;
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.MultiSelect = false;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        }

        /// <summary>Right-aligns numbers, formats money/dates in a grid that is bound to a DataTable.</summary>
        public static void FormatGrid(DataGridView g, DataTable dt)
        {
            foreach (DataGridViewColumn col in g.Columns)
            {
                string key = string.IsNullOrEmpty(col.DataPropertyName) ? col.Name : col.DataPropertyName;
                if (!dt.Columns.Contains(key)) continue;
                DataColumn dc = dt.Columns[key];
                Type t = dc.DataType;
                if (t == typeof(decimal) || t == typeof(double))
                {
                    col.DefaultCellStyle.Format = "N2";
                    col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                }
                else if (t == typeof(int) || t == typeof(long))
                {
                    col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                }
                else if (t == typeof(DateTime))
                {
                    bool hasTime = false;
                    foreach (DataRow r in dt.Rows)
                    {
                        if (r[dc] != DBNull.Value && Convert.ToDateTime(r[dc]).TimeOfDay != TimeSpan.Zero) { hasTime = true; break; }
                    }
                    col.DefaultCellStyle.Format = hasTime ? "yyyy-MM-dd HH:mm" : "yyyy-MM-dd";
                }
            }
        }

        // ---------------- navigation ----------------
        /// <summary>Asks the main window to open another screen (home, dashboard, pos, products, stocks, expired, damage, reports, accounts).</summary>
        public static void Navigate(Control from, string page)
        {
            Control c = from;
            while (c != null && !(c is frmMainform)) c = c.Parent;
            var main = c as frmMainform;
            if (main != null) main.NavigateTo(page);
        }

        // ---------------- dialogs ----------------
        private static Form NewDialog(string title, int w, int h)
        {
            var f = new Form();
            f.Text = title;
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.StartPosition = FormStartPosition.CenterScreen;
            f.MaximizeBox = false;
            f.MinimizeBox = false;
            f.ShowInTaskbar = false;
            f.ClientSize = new Size(w, h);
            return f;
        }

        private static Label Lbl(string text, int x, int y, int w, int h = 20)
        {
            return new Label { Text = text, Left = x, Top = y, Width = w, Height = h };
        }

        /// <summary>Single text input. Returns null if cancelled.</summary>
        public static string Ask(string title, string prompt, string def = "", bool password = false)
        {
            using (var f = NewDialog(title, 380, 130))
            {
                var lbl = Lbl(prompt, 12, 12, 356, 34);
                var tb = new TextBox { Left = 12, Top = 50, Width = 356, Text = def ?? "", UseSystemPasswordChar = password };
                var ok = new Button { Text = "OK", Left = 206, Top = 90, Width = 75, DialogResult = DialogResult.OK };
                var cancel = new Button { Text = "Cancel", Left = 293, Top = 90, Width = 75, DialogResult = DialogResult.Cancel };
                f.AcceptButton = ok;
                f.CancelButton = cancel;
                f.Controls.AddRange(new Control[] { lbl, tb, ok, cancel });
                return f.ShowDialog() == DialogResult.OK ? tb.Text : null;
            }
        }

        /// <summary>Pick one item from a list. Returns null if cancelled.</summary>
        public static string Choose(string title, string prompt, string[] options, string selected)
        {
            using (var f = NewDialog(title, 380, 130))
            {
                var lbl = Lbl(prompt, 12, 12, 356, 34);
                var cb = new ComboBox { Left = 12, Top = 50, Width = 356, DropDownStyle = ComboBoxStyle.DropDownList };
                cb.Items.AddRange(options);
                int idx = Array.IndexOf(options, selected);
                cb.SelectedIndex = idx >= 0 ? idx : 0;
                var ok = new Button { Text = "OK", Left = 206, Top = 90, Width = 75, DialogResult = DialogResult.OK };
                var cancel = new Button { Text = "Cancel", Left = 293, Top = 90, Width = 75, DialogResult = DialogResult.Cancel };
                f.AcceptButton = ok;
                f.CancelButton = cancel;
                f.Controls.AddRange(new Control[] { lbl, cb, ok, cancel });
                return f.ShowDialog() == DialogResult.OK ? cb.SelectedItem.ToString() : null;
            }
        }

        /// <summary>Search products by id, name or barcode. One match is returned directly; several open a picker. Returns product_id or null.</summary>
        public static string PickProduct(string term, bool activeOnly)
        {
            DataTable dt = Db.Query(
                @"SELECT p.product_id AS `Product ID`, p.product_name AS `Product Name`, IFNULL(p.barcode,'') AS `Barcode`,
                         c.category_name AS `Category`, p.selling_price AS `Price`, IFNULL(s.quantity,0) AS `Stock`,
                         p.product_status AS `Status`
                  FROM tbl_product p
                  JOIN tbl_category c ON c.category_id = p.category_id
                  LEFT JOIN tbl_stock s ON s.product_id = p.product_id
                  WHERE (p.product_id LIKE @t OR p.product_name LIKE @t OR p.barcode LIKE @t) "
                + (activeOnly ? "AND p.product_status = 'Active' " : "") +
                @"ORDER BY p.product_name LIMIT 200",
                Db.P("@t", "%" + (term ?? "").Trim() + "%"));

            if (dt.Rows.Count == 0)
            {
                Info("No matching product found.", "Search");
                return null;
            }
            if (dt.Rows.Count == 1) return dt.Rows[0][0].ToString();

            using (var f = NewDialog("Select Product", 720, 390))
            {
                var grid = new DataGridView
                {
                    Left = 10, Top = 10, Width = 700, Height = 330,
                    ReadOnly = true, AllowUserToAddRows = false, MultiSelect = false, RowHeadersVisible = false,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                    DataSource = dt
                };
                grid.Columns["Price"].DefaultCellStyle.Format = "N2";
                var ok = new Button { Text = "Select", Left = 550, Top = 350, Width = 75, DialogResult = DialogResult.OK };
                var cancel = new Button { Text = "Cancel", Left = 635, Top = 350, Width = 75, DialogResult = DialogResult.Cancel };
                grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) f.DialogResult = DialogResult.OK; };
                f.AcceptButton = ok;
                f.CancelButton = cancel;
                f.Controls.AddRange(new Control[] { grid, ok, cancel });
                if (f.ShowDialog() != DialogResult.OK || grid.CurrentRow == null) return null;
                return grid.CurrentRow.Cells[0].Value.ToString();
            }
        }

        /// <summary>Quick look-up used by the Home and Dashboard search boxes.</summary>
        public static void ShowProductInfo(string term)
        {
            if (string.IsNullOrWhiteSpace(term)) return;
            string id = PickProduct(term, false);
            if (id == null) return;
            DataTable dt = Db.Query(
                @"SELECT p.product_id, p.product_name, IFNULL(p.barcode,'-') AS barcode, c.category_name, p.selling_price,
                         p.product_status, IFNULL(s.quantity,0) AS qty, IFNULL(s.low_stock_threshold,10) AS thr
                  FROM tbl_product p JOIN tbl_category c ON c.category_id = p.category_id
                  LEFT JOIN tbl_stock s ON s.product_id = p.product_id WHERE p.product_id = @id", Db.P("@id", id));
            if (dt.Rows.Count == 0) return;
            DataRow r = dt.Rows[0];
            Info(r["product_name"] + "\n\nProduct ID: " + r["product_id"] + "\nBarcode: " + r["barcode"] +
                 "\nCategory: " + r["category_name"] + "\nPrice: " + Money(Db.ToDec(r["selling_price"])) +
                 "\nIn stock: " + r["qty"] + (Db.ToInt(r["qty"]) <= Db.ToInt(r["thr"]) ? "  (LOW)" : "") +
                 "\nStatus: " + r["product_status"], "Product");
        }

        /// <summary>Expiry / unit cost / supplier for stock that is being received. Returns null if cancelled.</summary>
        public static StockInInfo AskStockIn(string productName, decimal defaultCost, string defaultSupplierId)
        {
            using (var f = NewDialog("Receive Stock - " + productName, 400, 250))
            {
                string batch = InventoryService.NewBatchNo();
                var chk = new CheckBox { Text = "This stock has an expiry date", Left = 12, Top = 14, Width = 360, Checked = true };
                var dtp = new DateTimePicker { Left = 150, Top = 42, Width = 230, Format = DateTimePickerFormat.Short, Value = DateTime.Today.AddYears(1) };
                var tbCost = new TextBox { Left = 150, Top = 76, Width = 230, Text = defaultCost.ToString("0.00") };
                var cbSup = new ComboBox { Left = 150, Top = 110, Width = 230, DropDownStyle = ComboBoxStyle.DropDownList };
                var tbBatch = new TextBox { Left = 150, Top = 144, Width = 230, Text = batch, ReadOnly = true };

                DataTable sup = Db.Query("SELECT supplier_id, supplier_name FROM tbl_supplier ORDER BY supplier_name");
                DataRow none = sup.NewRow();
                none["supplier_id"] = "";
                none["supplier_name"] = "(none)";
                sup.Rows.InsertAt(none, 0);
                cbSup.DisplayMember = "supplier_name";
                cbSup.ValueMember = "supplier_id";
                cbSup.DataSource = sup;
                if (!string.IsNullOrEmpty(defaultSupplierId)) cbSup.SelectedValue = defaultSupplierId;

                chk.CheckedChanged += (s, e) => dtp.Enabled = chk.Checked;

                var ok = new Button { Text = "OK", Left = 220, Top = 200, Width = 75 };
                var cancel = new Button { Text = "Cancel", Left = 305, Top = 200, Width = 75, DialogResult = DialogResult.Cancel };
                StockInInfo result = null;
                ok.Click += (s, e) =>
                {
                    decimal cost;
                    if (!TryMoney(tbCost.Text, out cost) || cost < 0) { Warn("Enter a valid unit cost."); return; }
                    if (chk.Checked && dtp.Value.Date < DateTime.Today &&
                        !Confirm("The expiry date is already in the past. Receive this stock anyway?")) return;
                    result = new StockInInfo
                    {
                        Expiry = chk.Checked ? (DateTime?)dtp.Value.Date : null,
                        UnitCost = cost,
                        SupplierId = cbSup.SelectedValue == null ? "" : cbSup.SelectedValue.ToString(),
                        BatchNo = tbBatch.Text
                    };
                    f.DialogResult = DialogResult.OK;
                };
                f.AcceptButton = ok;
                f.CancelButton = cancel;
                f.Controls.AddRange(new Control[] {
                    chk, Lbl("Expiry date:", 12, 46, 130), dtp,
                    Lbl("Unit cost:", 12, 80, 130), tbCost,
                    Lbl("Supplier:", 12, 114, 130), cbSup,
                    Lbl("Batch no.:", 12, 148, 130), tbBatch, ok, cancel });
                return f.ShowDialog() == DialogResult.OK ? result : null;
            }
        }

        // ---------------- manager approval ----------------
        public static readonly string[] DiscountTypes = { "Senior/PWD", "Promo", "Employee", "Manager Discount", "Other" };

        /// <summary>
        /// Asks for the discount type and an approver. Managers/Admins approve their own
        /// discounts; a cashier must type a Manager or Admin username and password.
        /// </summary>
        public static bool ApproveDiscount(out string discountType, out string approverId)
        {
            return ApprovalPrompt("Discount Approval", "Choose the discount type.", true, out discountType, out approverId);
        }

        /// <summary>Manager/Admin approval for a sensitive action such as cancelling a sale. Returns the approver's acc_id or null.</summary>
        public static string ApproveManager(string message)
        {
            string t, id;
            return ApprovalPrompt("Manager Approval", message, false, out t, out id) ? id : null;
        }

        private static bool ApprovalPrompt(string title, string message, bool askType, out string type, out string approverId)
        {
            type = null;
            approverId = null;
            bool self = Session.IsManagerOrAdmin;
            int y = 12;
            var controls = new System.Collections.Generic.List<Control>();

            int height = 60 + 34 + (askType ? 34 : 0) + (self ? 0 : 68);
            using (var f = NewDialog(title, 380, height))
            {
                controls.Add(Lbl(message, 12, y, 356, 34)); y += 38;

                ComboBox cbType = null;
                if (askType)
                {
                    controls.Add(Lbl("Discount type:", 12, y + 3, 110));
                    cbType = new ComboBox { Left = 130, Top = y, Width = 238, DropDownStyle = ComboBoxStyle.DropDownList };
                    cbType.Items.AddRange(DiscountTypes);
                    cbType.SelectedIndex = 0;
                    controls.Add(cbType); y += 34;
                }

                TextBox tbUser = null, tbPass = null;
                if (self)
                {
                    controls.Add(Lbl("Approved by " + Session.UserName + " (" + Session.UserType + ")", 12, y, 356));
                    y += 24;
                }
                else
                {
                    controls.Add(Lbl("Manager username:", 12, y + 3, 115));
                    tbUser = new TextBox { Left = 130, Top = y, Width = 238 }; controls.Add(tbUser); y += 32;
                    controls.Add(Lbl("Password:", 12, y + 3, 115));
                    tbPass = new TextBox { Left = 130, Top = y, Width = 238, UseSystemPasswordChar = true }; controls.Add(tbPass); y += 34;
                }

                var ok = new Button { Text = "Approve", Left = 206, Top = y + 4, Width = 75 };
                var cancel = new Button { Text = "Cancel", Left = 293, Top = y + 4, Width = 75, DialogResult = DialogResult.Cancel };
                controls.Add(ok); controls.Add(cancel);
                f.ClientSize = new Size(380, y + 40);

                string chosenType = null, chosenApprover = null;
                ok.Click += (s, e) =>
                {
                    string id;
                    if (self) id = Session.UserId;
                    else
                    {
                        id = VerifyApprover(tbUser.Text.Trim(), tbPass.Text);
                        if (id == null) { Warn("Invalid credentials, or the account is not an active Manager/Admin.", "Approval Denied"); return; }
                    }
                    chosenApprover = id;
                    chosenType = cbType == null ? null : cbType.SelectedItem.ToString();
                    f.DialogResult = DialogResult.OK;
                };
                f.AcceptButton = ok;
                f.CancelButton = cancel;
                f.Controls.AddRange(controls.ToArray());
                if (f.ShowDialog() != DialogResult.OK) return false;
                type = chosenType;
                approverId = chosenApprover;
                return true;
            }
        }

        private static string VerifyApprover(string user, string pass)
        {
            if (user.Length == 0) return null;
            DataTable dt = Db.Query("SELECT acc_id, pass_word, acc_type, user_status FROM tbl_account WHERE user_name = @u", Db.P("@u", user));
            if (dt.Rows.Count == 0) return null;
            DataRow r = dt.Rows[0];
            if (!PasswordHasher.Verify(pass, r["pass_word"].ToString())) return null;
            string t = r["acc_type"].ToString();
            if (t != "Manager" && t != "Admin") return null;
            if (r["user_status"].ToString() != "Active") return null;
            return r["acc_id"].ToString();
        }
    }
}
