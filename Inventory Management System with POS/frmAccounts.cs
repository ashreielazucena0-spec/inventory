using MySql.Data.MySqlClient;
using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using System.Xml.Linq;

namespace Inventory_Management_System_with_POS
{
    public partial class frmAccounts : Form
    {
        private readonly MySQL_Database _db;
        private string selectedAccountId = "";

        public frmAccounts(MySQL_Database db)
        {
            InitializeComponent();
            _db = db;
            this.Load += new EventHandler(frmAccounts_Load);
        }

        private void frmAccounts_Load(object sender, EventArgs e)
        {
            txtsrch.Text = "Enter account name to search. . .";
            txtsrch.ForeColor = Color.Gray;

            dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridView1.MultiSelect = false;

            RetrieveAccounts("");
        }

        private void label4_Click(object sender, EventArgs e)
        {
        }

        private void label3_Click(object sender, EventArgs e)
        {
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void pictureBox_HouseHover(object sender, EventArgs e)
        {
            pictureBox1.Cursor = Cursors.Hand;
            pictureBox1.BackColor = Color.Red;
        }

        private void pictureBox1_HouseLeave(object sender, EventArgs e)
        {
            pictureBox1.Cursor = Cursors.Default;
            pictureBox1.BackColor = Color.Transparent;
        }

        private void txtsrch_Enter(object sender, EventArgs e)
        {
            if (txtsrch.Text == "Enter account name to search. . .")
            {
                txtsrch.Text = "";
                txtsrch.ForeColor = Color.Black;
            }
        }

        private void txtsrch_Leave(object sender, EventArgs e)
        {
            if (txtsrch.Text == "")
            {
                txtsrch.Text = "Enter account name to search. . .";
                txtsrch.ForeColor = Color.Gray;

                RetrieveAccounts("");
            }
        }

        private void RetrieveAccounts(string srch)
        {
            try
            {
                if (_db.conn.State == ConnectionState.Open)
                {
                    _db.conn.Close();
                }

                _db.conn.Open();

                string sql;

                using (MySqlCommand cmd = new MySqlCommand())
                {
                    cmd.Connection = _db.conn;

                    if (string.IsNullOrWhiteSpace(srch))
                    {
                        sql = @"SELECT account_id,
                                       account_name,
                                       acct_type,
                                       gender,
                                       user_name,
                                       pass_word,
                                       created_at
                                FROM tbl_account
                                ORDER BY account_id ASC";

                        cmd.CommandText = sql;
                    }
                    else
                    {
                        sql = @"SELECT account_id,
                                       account_name,
                                       acct_type,
                                       gender,
                                       user_name,
                                       pass_word,
                                       created_at
                                FROM tbl_account
                                WHERE CAST(account_id AS CHAR) LIKE @search
                                   OR account_name LIKE @search
                                   OR acct_type LIKE @search
                                   OR gender LIKE @search
                                   OR user_name LIKE @search
                                   OR pass_word LIKE @search
                                ORDER BY account_id ASC";

                        cmd.CommandText = sql;
                        cmd.Parameters.AddWithValue(
                            "@search",
                            "%" + srch + "%"
                        );
                    }

                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        int num = 0;

                        dataGridView1.Rows.Clear();

                        while (reader.Read())
                        {
                            num++;

                            dataGridView1.Rows.Add(
                                num,
                                reader["account_id"].ToString(),
                                reader["account_name"].ToString(),
                                reader["acct_type"].ToString(),
                                reader["gender"].ToString(),
                                reader["user_name"].ToString(),
                                reader["pass_word"].ToString(),
                                reader["created_at"].ToString()
                            );
                        }

                        dataGridView1.ClearSelection();

                        lblCountRec.Text =
                            "TOTAL RECORD'S FOUND: " + num;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Error!",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            finally
            {
                if (_db.conn.State == ConnectionState.Open)
                {
                    _db.conn.Close();
                }
            }
        }

        private void dataGridView1_CellClick(
            object sender,
            DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
                return;

            dataGridView1.ClearSelection();
            dataGridView1.Rows[e.RowIndex].Selected = true;

            DataGridViewRow row = dataGridView1.Rows[e.RowIndex];

            selectedAccountId =
                row.Cells[1].Value?.ToString() ?? "";
        }

        private void dataGridView1_CellContentClick(
            object sender,
            DataGridViewCellEventArgs e)
        {
        }

        private void dataGridView1_CellFormatting(
            object sender,
            DataGridViewCellFormattingEventArgs e)
        {
            if (e.ColumnIndex == 6 && e.Value != null)
            {
                e.Value = new string(
                    '@',
                    e.Value.ToString().Length
                );
            }
        }

        private void txtsrch_TextChanged(object sender, EventArgs e)
        {
            if (txtsrch.Text !=
                "Enter account name to search. . .")
            {
                RetrieveAccounts(txtsrch.Text);
            }
        }

        private void panel3_Paint(object sender, PaintEventArgs e)
        {
        }

        private void button1_Click(object sender, EventArgs e)
        {
            DataGridViewRow selectedRow =
                dataGridView1.CurrentRow;

            if (selectedRow == null ||
                dataGridView1.Rows.Count == 0)
            {
                MessageBox.Show(
                    "Please select an account to delete.",
                    "No Selection",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );

                return;
            }

            string accountId =
                selectedRow.Cells[1].Value?.ToString() ?? "";

            string accountName =
                selectedRow.Cells[2].Value?.ToString() ?? "";

            if (string.IsNullOrEmpty(accountId))
            {
                MessageBox.Show(
                    "Invalid account selected.",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );

                return;
            }

            DialogResult confirm = MessageBox.Show(
                $"Are you sure you want to delete the account '{accountName}'?",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (confirm != DialogResult.Yes)
                return;

            try
            {
                if (_db.conn.State != ConnectionState.Open)
                {
                    _db.conn.Open();
                }

                string sql =
                    "DELETE FROM tbl_account WHERE account_id = @account_id";

                using (MySqlCommand cmd =
                    new MySqlCommand(sql, _db.conn))
                {
                    cmd.Parameters.AddWithValue(
                        "@account_id",
                        accountId
                    );

                    int rowsAffected =
                        cmd.ExecuteNonQuery();

                    if (rowsAffected > 0)
                    {
                        MessageBox.Show(
                            "Account deleted successfully.",
                            "Success",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information
                        );

                        RetrieveAccounts("");

                        txtsrch.Text =
                            "Enter account name to search. . .";

                        txtsrch.ForeColor = Color.Gray;
                    }
                    else
                    {
                        MessageBox.Show(
                            "Account could not be found or was already deleted.",
                            "Notice",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Error!",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            finally
            {
                if (_db.conn.State == ConnectionState.Open)
                {
                    _db.conn.Close();
                }
            }
        }

        private void EDT_button_Click(object sender, EventArgs e)
        {
            try
            {
                if (dataGridView1.CurrentRow == null)
                {
                    MessageBox.Show(
                        "Please select an account to edit first.",
                        "No Selection",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );

                    return;
                }

                string slctd_AccID = dataGridView1.CurrentRow.Cells[1].Value?.ToString() ?? "";

                if (string.IsNullOrEmpty(slctd_AccID))
                {
                    MessageBox.Show(
                        "Invalid account selected.",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                    return;
                }

                selectedAccountId = slctd_AccID;

                string accID = "", accName = "", accType = "", gender = "", uname = "", pword = "";

                if (_db.conn.State != ConnectionState.Open) { _db.conn.Open(); }

                string sql = @"SELECT * FROM tbl_account WHERE account_id = @id";
                using (MySqlCommand cmd = new MySqlCommand(sql, _db.conn))
                {
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@id", slctd_AccID);

                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            accID = dr[0].ToString();
                            accName = dr[1].ToString();
                            accType = dr[2].ToString();
                            gender = dr[3].ToString();
                            uname = dr[4].ToString();
                            pword = dr[5].ToString();
                        }
                    }
                }

                Account_Variables.isEditMode = true;

                using (frmCreateAccount fCreateAcc = new frmCreateAccount(_db))
                {
                    fCreateAcc.txtAccID.Text = accID;
                    fCreateAcc.txtAccName.Text = accName;
                    fCreateAcc.cbAccountType.SelectedItem = accType;

                    if (gender == "Male")
                    {
                        fCreateAcc.radioButton1.Checked = true;
                    }
                    else
                    {
                        fCreateAcc.radioButton2.Checked = true;
                    }

                    fCreateAcc.txtUsername.Text = uname;
                    fCreateAcc.txtPassword.Text = pword;

                    fCreateAcc.label1.Text = "Update Account";
                    fCreateAcc.btnSave.Text = "Update";
                    fCreateAcc.txtAccID.ReadOnly = true;

                    fCreateAcc.ShowDialog();
                }

                // Refresh the grid after the dialog closes, in case an update happened
                RetrieveAccounts("");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "An error occurred: " + ex.Message,
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }
    }
}