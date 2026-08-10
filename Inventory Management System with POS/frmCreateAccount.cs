using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Inventory_Management_System_with_POS
{
    public partial class frmCreateAccount : Form {

        private readonly MySQL_Database _db;

        public frmCreateAccount()
        {
            InitializeComponent();
            _db = new MySQL_Database();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                if (txtAccName.Text == "" || cbAccountType.Text == ""
                    || txtUsername.Text == "" || txtPassword.Text == "")
                {
                    MessageBox.Show("Please fill in all required fields.",
                                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (_db.conn.State == ConnectionState.Closed)
                {
                    _db.conn.Open();
                }

                string sql = @"INSERT INTO tbl_account (account_id, account_name,
                                acc_type, gender, user_name, pass_word)
                                VALUES (@account_id, @account_name,
                                @acc_type, @gender, @user_name, @pass_word)";
                using (MySqlCommand cmd = new MySqlCommand(sql, _db.conn))
                {
                    cmd.Parameters.AddWithValue("@account_id", txtAccID.Text);
                    cmd.Parameters.AddWithValue("@account_name", txtAccName.Text);
                    cmd.Parameters.AddWithValue("@acc_type", cbAccountType.Text);
                    cmd.Parameters.AddWithValue("@gender", radioButton1.Checked ? "Male" : "Female");
                    cmd.Parameters.AddWithValue("@user_name", txtUsername.Text);
                    cmd.Parameters.AddWithValue("@pass_word", txtPassword.Text);
                    cmd.ExecuteNonQuery();
                }

                MessageBox.Show("Account created successfully.", "Success",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                btnClear.PerformClick();
            } catch (Exception ex) {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            ClearFields();
        }

        private void ClearFields()
        {
            txtAccID.Clear();
            txtAccName.Clear();
            cbAccountType.SelectedIndex = -1;
            radioButton1.Checked = false;
            Female.Checked = false;
            txtUsername.Clear();
            txtPassword.Clear();
            checkBox1.Checked = false;
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox1.Checked)
            {
                txtPassword.UseSystemPasswordChar = false;
                checkBox1.Text = "Hide Password";
            }
            else
            {
                txtPassword.UseSystemPasswordChar = true;
                checkBox1.Text = "Show Password";
            }
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            frmLoginform flog = new frmLoginform();
            flog.Show();
            this.Close();
        }

        private void frmCreateAccount_Load(object sender, EventArgs e)
        {
            txtPassword.UseSystemPasswordChar = true;

            cbAccountType.Items.Clear();
            cbAccountType.Items.AddRange(new string[] { "Admin", "Staff", "Cashier" });
        }
    }
}