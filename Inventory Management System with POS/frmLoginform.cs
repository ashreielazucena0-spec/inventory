using MySql.Data.MySqlClient;
using System;
using System.Data;
using System.Windows.Forms;

namespace Inventory_Management_System_with_POS
{
    public partial class frmLoginform : Form
    {
        public readonly MySQL_Database _db;

        public frmLoginform()
        {
            InitializeComponent();
            _db = new MySQL_Database();
        }

        private void frmLoginform_Load(object sender, EventArgs e)
        {
            // Test the connection once at start-up so a wrong server/password is reported immediately.
            _db.ConnectToDatabase();
            if (_db.conn.State == ConnectionState.Open) _db.conn.Close();
            txtPassword.UseSystemPasswordChar = true;
        }

        /// <summary>LOGIN: checks tbl_account (hashed password, Active status) and starts the session.</summary>
        private void button1_Click(object sender, EventArgs e)
        {
            string user = txtUsername.Text.Trim();
            string pass = txtPassword.Text;

            if (user.Length == 0 || pass.Length == 0)
            {
                MessageBox.Show("Please enter both username and password.", "Input Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                DataTable dt = Db.Query(
                    "SELECT acc_id, acc_name, acc_type, pass_word, user_status FROM tbl_account WHERE user_name = @u",
                    Db.P("@u", user));

                // Same message for "no such user" and "wrong password" so usernames can't be guessed.
                if (dt.Rows.Count == 0 || !PasswordHasher.Verify(pass, dt.Rows[0]["pass_word"].ToString()))
                {
                    MessageBox.Show("Invalid username or password.", "Login Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                DataRow row = dt.Rows[0];

                if (!string.Equals(row["user_status"].ToString(), "Active", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("This account is inactive. Please contact an administrator.", "Login Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Old accounts saved with a plain-text password are upgraded to a hash on first successful login.
                if (PasswordHasher.NeedsUpgrade(row["pass_word"].ToString()))
                {
                    Db.Exec("UPDATE tbl_account SET pass_word = @p WHERE acc_id = @id",
                        Db.P("@p", PasswordHasher.Hash(pass)), Db.P("@id", row["acc_id"].ToString()));
                }

                Session.Start(row["acc_id"].ToString(), row["acc_name"].ToString(), row["acc_type"].ToString());
                MessageBox.Show("Login successful!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                Hide();
                using (var frmMain = new frmMainform(_db))
                {
                    frmMain.ShowDialog();
                }

                if (Session.LogoutRequested)
                {
                    // Logout: come back to the login screen instead of closing the application.
                    Session.LogoutRequested = false;
                    Session.Clear();
                    txtUsername.Clear();
                    txtPassword.Clear();
                    Show();
                    txtUsername.Focus();
                }
                else
                {
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(Db.Friendly(ex), "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>"Create account" link - self sign-up (Cashier only, see frmCreateAccount.PublicSignup).</summary>
        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Hide();
            using (var frm = new frmCreateAccount(_db))
            {
                frm.PublicSignup = true;
                frm.ShowDialog();
            }
            Show();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            txtUsername.Clear();
            txtPassword.Clear();
            txtUsername.Focus();
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            txtPassword.UseSystemPasswordChar = !checkBox1.Checked;
            checkBox1.Text = checkBox1.Checked ? "Hide Password" : "Show Password";
        }

        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Do you want to exit?", "Exit", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                Application.Exit();
            }
        }

        private void panel1_Paint(object sender, PaintEventArgs e) { }
        private void txtPassword_TextChanged(object sender, EventArgs e) { }
        private void label4_Click(object sender, EventArgs e) { }
        private void txtUsername_TextChanged(object sender, EventArgs e) { }
    }
}
