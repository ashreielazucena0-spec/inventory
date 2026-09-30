using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace Inventory_Management_System_with_POS
{
    /// <summary>System Accounts: list, search, create, edit, activate/deactivate, reset password, delete (tbl_account).</summary>
    public partial class frmAccounts : Form
    {
        private readonly MySQL_Database _db;

        // Grid column order (from the designer):
        // 0 #, 1 ACC. ID, 2 ACCOUNT NAME, 3 ACCT_TYPE, 4 GENDER, 5 USERNAME, 6 EMAIL, 7 PHONE NO., 8 PASSWORD, 9 TIME CREATED, 10 STATUS (added in code)
        private const int PasswordColumnIndex = 8;
        private const int StatusColumnIndex = 10;
        private const string SearchPlaceholder = "Enter account name to search. . .";

        public frmAccounts(MySQL_Database db)
        {
            InitializeComponent();
            _db = db;
            Load += new EventHandler(frmAccounts_Load);
            dataGridView1.ReadOnly = true;
        }

        private void frmAccounts_Load(object sender, EventArgs e)
        {
            if (Session.IsLoggedIn && !Session.CanAccess(Feature.Accounts))
            {
                Ui.Warn("Only an Admin can manage system accounts.", "Access Denied");
                BeginInvoke(new Action(Close));
                return;
            }

            txtsrch.Text = SearchPlaceholder;
            txtsrch.ForeColor = Color.Gray;

            dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridView1.MultiSelect = false;
            dataGridView1.AllowUserToAddRows = false;

            if (!dataGridView1.Columns.Contains("colStatus"))
            {
                var col = new DataGridViewTextBoxColumn { Name = "colStatus", HeaderText = "STATUS" };
                dataGridView1.Columns.Add(col);
            }

            // Right-click menu on a row.
            var menu = new ContextMenuStrip();
            menu.Items.Add("Edit account", null, (s, a) => EDT_button_Click(s, a));
            menu.Items.Add("Activate / Deactivate", null, (s, a) => ToggleStatus());
            menu.Items.Add("Reset password...", null, (s, a) => ResetPassword());
            menu.Items.Add("Delete account", null, (s, a) => button1_Click(s, a));
            dataGridView1.ContextMenuStrip = menu;
            dataGridView1.CellMouseDown += (s, a) =>
            {
                if (a.Button == MouseButtons.Right && a.RowIndex >= 0)
                {
                    dataGridView1.ClearSelection();
                    dataGridView1.Rows[a.RowIndex].Selected = true;
                    dataGridView1.CurrentCell = dataGridView1.Rows[a.RowIndex].Cells[1];
                }
            };
            dataGridView1.CellDoubleClick += (s, a) => { if (a.RowIndex >= 0) EDT_button_Click(s, a); };
            dataGridView1.CellFormatting -= dataGridView1_CellFormatting;
            dataGridView1.CellFormatting += dataGridView1_CellFormatting;

            RetrieveAccounts("");
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void txtsrch_Enter(object sender, EventArgs e)
        {
            if (txtsrch.Text == SearchPlaceholder)
            {
                txtsrch.Text = "";
                txtsrch.ForeColor = Color.Black;
            }
        }

        private void txtsrch_Leave(object sender, EventArgs e)
        {
            if (txtsrch.Text == "")
            {
                txtsrch.Text = SearchPlaceholder;
                txtsrch.ForeColor = Color.Gray;
                RetrieveAccounts("");
            }
        }

        private void txtsrch_TextChanged(object sender, EventArgs e)
        {
            if (txtsrch.Text != SearchPlaceholder) RetrieveAccounts(txtsrch.Text);
        }

        /// <summary>Loads accounts, filtered by the search text (id, name, type, gender, username, email, phone, status).</summary>
        private void RetrieveAccounts(string srch)
        {
            try
            {
                string keepId = SelectedId();
                DataTable dt;
                if (string.IsNullOrWhiteSpace(srch))
                {
                    dt = Db.Query(@"SELECT acc_id, acc_name, acc_type, gender, user_name, email, phone, pass_word, created_at, user_status
                                    FROM tbl_account ORDER BY acc_id ASC");
                }
                else
                {
                    dt = Db.Query(@"SELECT acc_id, acc_name, acc_type, gender, user_name, email, phone, pass_word, created_at, user_status
                                    FROM tbl_account
                                    WHERE acc_id LIKE @s OR acc_name LIKE @s OR CAST(acc_type AS CHAR) LIKE @s OR gender LIKE @s
                                       OR user_name LIKE @s OR email LIKE @s OR phone LIKE @s OR user_status LIKE @s
                                    ORDER BY acc_id ASC", Db.P("@s", "%" + srch.Trim() + "%"));
                }

                dataGridView1.Rows.Clear();
                int num = 0;
                foreach (DataRow r in dt.Rows)
                {
                    num++;
                    int idx = dataGridView1.Rows.Add(num, r["acc_id"].ToString(), r["acc_name"].ToString(), r["acc_type"].ToString(),
                        r["gender"].ToString(), r["user_name"].ToString(), r["email"].ToString(), r["phone"].ToString(),
                        r["pass_word"].ToString(), Convert.ToDateTime(r["created_at"]).ToString("yyyy-MM-dd HH:mm"),
                        r["user_status"].ToString());
                    if (!string.Equals(r["user_status"].ToString(), "Active", StringComparison.OrdinalIgnoreCase))
                        dataGridView1.Rows[idx].DefaultCellStyle.ForeColor = Color.Gray;
                    if (keepId != null && r["acc_id"].ToString() == keepId)
                    {
                        dataGridView1.ClearSelection();
                        dataGridView1.Rows[idx].Selected = true;
                    }
                }
                if (keepId == null) dataGridView1.ClearSelection();
                lblCountRec.Text = "TOTAL RECORD'S FOUND: " + num;
            }
            catch (Exception ex)
            {
                Ui.Error(ex, "Error!");
            }
        }

        private void dataGridView1_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            // Never show the stored password (hash) - mask the column.
            if (e.ColumnIndex == PasswordColumnIndex && e.Value != null)
                e.Value = "********";
        }

        private DataGridViewRow SelectedRow()
        {
            return dataGridView1.SelectedRows.Count > 0 ? dataGridView1.SelectedRows[0] : null;
        }

        private string SelectedId()
        {
            DataGridViewRow r = SelectedRow();
            return r == null ? null : r.Cells[1].Value as string;
        }

        private static bool IsLastActiveAdmin(string accId)
        {
            object cur = Db.Scalar("SELECT acc_type FROM tbl_account WHERE acc_id = @id AND user_status = 'Active'", Db.P("@id", accId));
            if (cur == null || cur.ToString() != "Admin") return false;
            return Db.ToInt(Db.Scalar("SELECT COUNT(*) FROM tbl_account WHERE acc_type = 'Admin' AND user_status = 'Active' AND acc_id <> @id",
                Db.P("@id", accId))) == 0;
        }

        /// <summary>DELETE button: removes the selected account after confirmation.</summary>
        private void button1_Click(object sender, EventArgs e)
        {
            DataGridViewRow row = SelectedRow();
            if (row == null) { Ui.Warn("Please select an account to delete.", "No Selection"); return; }

            string id = row.Cells[1].Value as string ?? "";
            string name = row.Cells[2].Value as string ?? "";

            if (id.Length == 0) { Ui.Error("Invalid account selected."); return; }
            if (id == Session.UserId) { Ui.Warn("You can't delete the account you are logged in with.", "Not Allowed"); return; }

            try
            {
                if (IsLastActiveAdmin(id)) { Ui.Warn("This is the only active Admin account and can't be deleted.", "Not Allowed"); return; }

                if (!Ui.Confirm("Delete the account '" + name + "'?\n\nSales and stock records made by this account are kept, " +
                                "but will no longer show who made them. Deactivating the account instead keeps that history.",
                                "Confirm Delete"))
                    return;

                int n = Db.Exec("DELETE FROM tbl_account WHERE acc_id = @id", Db.P("@id", id));
                if (n > 0)
                {
                    Ui.Info("Account deleted successfully.", "Success");
                    RetrieveAccounts(txtsrch.Text == SearchPlaceholder ? "" : txtsrch.Text);
                }
                else
                {
                    Ui.Warn("Account could not be found or was already deleted.");
                }
            }
            catch (Exception ex)
            {
                Ui.Error(ex, "Error!");
            }
        }

        /// <summary>EDIT button: opens the account form with the selected account loaded.</summary>
        private void EDT_button_Click(object sender, EventArgs e)
        {
            string id = SelectedId();
            if (string.IsNullOrEmpty(id)) { Ui.Warn("Please select an account to edit first.", "No Selection"); return; }

            try
            {
                DataTable dt = Db.Query(@"SELECT acc_id, acc_name, acc_type, gender, user_name, phone, email FROM tbl_account WHERE acc_id = @id",
                    Db.P("@id", id));
                if (dt.Rows.Count == 0) { Ui.Warn("That account no longer exists."); RetrieveAccounts(""); return; }
                DataRow r = dt.Rows[0];

                Account_Variables.isEditMode = true;
                using (var f = new frmCreateAccount(_db))
                {
                    f.txtAccID.Text = r["acc_id"].ToString();
                    f.txtAccName.Text = r["acc_name"].ToString();
                    f.cbAccountType.SelectedItem = r["acc_type"].ToString();
                    if (r["gender"].ToString() == "Male") f.radioButton1.Checked = true; else f.radioButton2.Checked = true;
                    f.txtUsername.Text = r["user_name"].ToString();
                    f.txtPassword.Text = "";   // the stored password is a hash; leave blank to keep it
                    f.txtphone.Text = r["phone"].ToString();
                    f.txtmail.Text = r["email"].ToString();

                    f.label1.Text = "Update Account";
                    f.btnSave.Text = "Update";
                    f.txtAccID.ReadOnly = true;
                    new ToolTip().SetToolTip(f.txtPassword, "Leave blank to keep the current password.");
                    f.ShowDialog();
                }
                Account_Variables.isEditMode = false;
                RetrieveAccounts(txtsrch.Text == SearchPlaceholder ? "" : txtsrch.Text);
            }
            catch (Exception ex)
            {
                Ui.Error(ex);
            }
        }

        /// <summary>Right-click: switches the selected account between Active and Inactive.</summary>
        private void ToggleStatus()
        {
            DataGridViewRow row = SelectedRow();
            if (row == null) { Ui.Warn("Please select an account first.", "No Selection"); return; }
            string id = row.Cells[1].Value as string;
            string name = row.Cells[2].Value as string;
            bool active = string.Equals(row.Cells[StatusColumnIndex].Value as string, "Active", StringComparison.OrdinalIgnoreCase);

            try
            {
                if (active)
                {
                    if (id == Session.UserId) { Ui.Warn("You can't deactivate the account you are logged in with.", "Not Allowed"); return; }
                    if (IsLastActiveAdmin(id)) { Ui.Warn("This is the only active Admin account and can't be deactivated.", "Not Allowed"); return; }
                    if (!Ui.Confirm("Deactivate '" + name + "'? They will no longer be able to log in.")) return;
                }
                Db.Exec("UPDATE tbl_account SET user_status = @s WHERE acc_id = @id",
                    Db.P("@s", active ? "Inactive" : "Active"), Db.P("@id", id));
                RetrieveAccounts(txtsrch.Text == SearchPlaceholder ? "" : txtsrch.Text);
            }
            catch (Exception ex)
            {
                Ui.Error(ex);
            }
        }

        /// <summary>Right-click: sets a new password for the selected account.</summary>
        private void ResetPassword()
        {
            DataGridViewRow row = SelectedRow();
            if (row == null) { Ui.Warn("Please select an account first.", "No Selection"); return; }
            string id = row.Cells[1].Value as string;
            string name = row.Cells[2].Value as string;

            string p1 = Ui.Ask("Reset Password", "New password for '" + name + "' (at least 6 characters):", "", true);
            if (p1 == null) return;
            if (p1.Length < 6) { Ui.Warn("Password must be at least 6 characters."); return; }
            string p2 = Ui.Ask("Reset Password", "Type the new password again:", "", true);
            if (p2 == null) return;
            if (p1 != p2) { Ui.Warn("The two passwords don't match."); return; }

            try
            {
                Db.Exec("UPDATE tbl_account SET pass_word = @p WHERE acc_id = @id", Db.P("@p", PasswordHasher.Hash(p1)), Db.P("@id", id));
                Ui.Info("Password updated.", "Success");
            }
            catch (Exception ex)
            {
                Ui.Error(ex);
            }
        }

        private void frmAccounts_Load_1(object sender, EventArgs e) { }
        private void dataGridView1_CellContentClick_1(object sender, DataGridViewCellEventArgs e) { }
    }
}
