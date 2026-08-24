using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Inventory_Management_System_with_POS
{
    public partial class frmAccounts : Form
    {
        private readonly MySQL_Database _db;
        public frmAccounts(MySQL_Database db)
        {
            InitializeComponent();
            _db = db;
        }

        private void label4_Click(object sender, EventArgs e)
        {

        }

        private void label3_Click(object sender, EventArgs e)
        {

        }

        private void pictureBox1_Click(object sender, EventArgs e) {
            this.Close();
        }


        private void pictureBox_HouseHover(object sender, EventArgs e) {
            pictureBox1.Cursor = Cursors.Hand;
            pictureBox1.BackColor = Color.Red;
        }


        private void pictureBox1_HouseLeave(object sender, EventArgs e) {
            pictureBox1.Cursor = Cursors.Default;
            pictureBox1.BackColor = Color.Transparent;
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }


        private void txtsrch_Enter(object sender, EventArgs e)
        {
            if (txtsrch.Text == "Enter account name to search. . .")
            {
                txtsrch.Text = "";
                txtsrch.ForeColor = Color.Black;
            }
        }


        private void txtsrch_Leave(object sender, EventArgs e) {
            if (txtsrch.Text == "")
            {
                txtsrch.Text = "Enter account name to search. . .";
                txtsrch.ForeColor = Color.Gray;
                RetrieveAccounts("");
            }
        }

        private void RetrieveAccounts(string srch) {
            try {
                _db.conn.Close();
                if (_db.conn.State == ConnectionState.Closed) {
                    _db.conn.Open();
                }

                string sql = @"SELECT * FROM tbl_account WHERE user_name
                               LIKE @search OR account_name LIKE @search";
                using (var cmd = new MySqlCommand(sql, _db.conn)) {
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@search", srch + "%");
                    using (var reader = cmd.ExecuteReader(CommandBehavior.CloseConnection)) {
                        int num = 0;
                        dataGridView1.Rows.Clear();

                        while (reader.Read()) {
                            num += 1;
                            dataGridView1.Rows.Add(num, reader["account_id"].ToString(),
                                                        reader["account_name"].ToString(),
                                                        reader["acc_type"].ToString(),
                                                        reader["gender"].ToString(),
                                                        reader["user_name"].ToString(),
                                                        reader["pass_word"].ToString(),
                                                        reader["created_at"].ToString());
                        }
                        dataGridView1.ClearSelection();
                        lblCountRec.Text = "TOTAL RECORD'S FOUND: " + num;
                    }
                }
            } catch (Exception ex) {
                MessageBox.Show(ex.Message, "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            } finally {
                if (_db.conn.State == ConnectionState.Open) {
                    _db.conn.Close();
                }
            }
        }
        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e) {

        }


        private void dataGridView1_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e) {
            if (e.ColumnIndex == 6 && e.Value != null)
            {
                e.Value = new string('@', e.Value.ToString().Length);
            }
        }


        private void txtsrch_TextChanged(object sender, EventArgs e) {
            RetrieveAccounts(txtsrch.Text);
        }

        private void panel3_Paint(object sender, PaintEventArgs e)
        {

        }



        private void button1_Click(object sender, EventArgs e)
        {
            DataGridViewRow selectedRow = dataGridView1.CurrentRow;

            if (selectedRow == null || dataGridView1.Rows.Count == 0)
            {
                MessageBox.Show("Please select an account to delete.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string accountId = selectedRow.Cells[1].Value.ToString();
            string accountName = selectedRow.Cells[2].Value.ToString();

            DialogResult confirm = MessageBox.Show(
                $"Are you sure you want to delete the account '{accountName}'?",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes)
                return;

            try
            {
                if (_db.conn.State != ConnectionState.Open)
                {
                    _db.conn.Open();
                }

                string sql = "DELETE FROM tbl_account WHERE account_id = @account_id";
                using (var cmd = new MySqlCommand(sql, _db.conn))
                {
                    cmd.Parameters.AddWithValue("@account_id", accountId);
                    int rowsAffected = cmd.ExecuteNonQuery();

                    if (rowsAffected > 0)
                    {
                        MessageBox.Show("Account deleted successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        RetrieveAccounts("");
                    }
                    else
                    {
                        MessageBox.Show("Account could not be found or was already deleted.", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (_db.conn.State == ConnectionState.Open)
                {
                    _db.conn.Close();
                }
            }
        }
    }
        }
    
