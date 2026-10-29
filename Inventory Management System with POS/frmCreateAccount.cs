using System;
using System.Data;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Inventory_Management_System_with_POS
{
    public partial class frmCreateAccount : Form
    {
        // Keep false so the login-screen sign-up can't create Admin/Manager accounts.
        private const bool AllowPublicPrivilegedSignup = false;

        private readonly MySQL_Database _db;

        // True only when opened from the login screen.
        public bool PublicSignup { get; set; }

        private bool RestrictRoles =>
            PublicSignup && !AllowPublicPrivilegedSignup && !Account_Variables.isEditMode;

        public frmCreateAccount(MySQL_Database db)
        {
            InitializeComponent();
            _db = db;
        }

        private void frmCreateAccount_Load(object sender, EventArgs e)
        {
            txtPassword.UseSystemPasswordChar = true;

            string current = cbAccountType.Text;

            cbAccountType.DropDownStyle = ComboBoxStyle.DropDownList;
            cbAccountType.Items.Clear();
            cbAccountType.Items.AddRange(new object[] { "Admin", "Cashier", "Inventory Staff", "Manager" });

            if (!string.IsNullOrEmpty(current) && cbAccountType.Items.Contains(current))
                cbAccountType.SelectedItem = current;

            if (RestrictRoles)
            {
                cbAccountType.Items.Remove("Admin");
                cbAccountType.Items.Remove("Manager");
                cbAccountType.SelectedItem = "Cashier";
            }
            cbAccountType.Enabled = true;

            if (!Account_Variables.isEditMode) ShowNextId();
        }

        private void ShowNextId()
        {
            try { txtAccID.Text = Db.NextId("tbl_account", "acc_id", "ACC-", 3); }
            catch { }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            bool edit = Account_Variables.isEditMode;
            string id = txtAccID.Text.Trim();
            string name = txtAccName.Text.Trim();
            string user = txtUsername.Text.Trim();
            string pass = txtPassword.Text;
            string type = cbAccountType.Text;
            string phone = txtphone.Text.Trim();
            string mail = txtmail.Text.Trim();

            if (id.Length == 0 || name.Length == 0 || type.Length == 0 || user.Length == 0 || (!edit && pass.Length == 0))
            {
                Ui.Error("Please fill in all required fields.");
                return;
            }
            if (id.Length > 10) { Ui.Error("Account ID can be at most 10 characters."); return; }
            if (!radioButton1.Checked && !radioButton2.Checked) { Ui.Error("Please select a gender."); return; }
            if (user.Length < 3 || user.Length > 50 || user.Contains(" ")) { Ui.Error("Username must be 3-50 characters with no spaces."); return; }
            if (pass.Length > 0 && pass.Length < 6) { Ui.Error("Password must be at least 6 characters."); return; }
            if (phone.Length > 0 && !Regex.IsMatch(phone, @"^[0-9+\-\s()]{7,20}$")) { Ui.Error("Phone number looks invalid."); return; }
            if (mail.Length > 0 && !IsEmail(mail)) { Ui.Error("Email address looks invalid."); return; }

            if (RestrictRoles && (type == "Admin" || type == "Manager"))
            {
                Ui.Error("Public sign-up can't create Admin or Manager accounts.");
                return;
            }

            try
            {
                if (Db.ToInt(Db.Scalar("SELECT COUNT(*) FROM tbl_account WHERE user_name = @u AND acc_id <> @id",
                        Db.P("@u", user), Db.P("@id", id))) > 0)
                {
                    Ui.Error("That username is already taken.");
                    return;
                }
                if (!edit && Db.ToInt(Db.Scalar("SELECT COUNT(*) FROM tbl_account WHERE acc_id = @id", Db.P("@id", id))) > 0)
                {
                    Ui.Error("That Account ID already exists.");
                    return;
                }

                string gender = radioButton1.Checked ? "Male" : "Female";

                if (edit)
                {
                    if (type != "Admin" && IsLastActiveAdmin(id))
                    {
                        Ui.Error("This is the only active Admin account, so its type can't be changed.");
                        return;
                    }

                    if (pass.Length > 0)
                    {
                        Db.Exec(@"UPDATE tbl_account SET acc_name=@n, acc_type=@t, gender=@g, user_name=@u, pass_word=@p, phone=@ph, email=@em
                                  WHERE acc_id=@id",
                            Db.P("@n", name), Db.P("@t", type), Db.P("@g", gender), Db.P("@u", user),
                            Db.P("@p", PasswordHasher.Hash(pass)), Db.PN("@ph", phone), Db.PN("@em", mail), Db.P("@id", id));
                    }
                    else
                    {
                        Db.Exec(@"UPDATE tbl_account SET acc_name=@n, acc_type=@t, gender=@g, user_name=@u, phone=@ph, email=@em
                                  WHERE acc_id=@id",
                            Db.P("@n", name), Db.P("@t", type), Db.P("@g", gender), Db.P("@u", user),
                            Db.PN("@ph", phone), Db.PN("@em", mail), Db.P("@id", id));
                    }

                    Ui.Info("Account updated successfully.", "Success");
                    Account_Variables.isEditMode = false;
                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    Db.Exec(@"INSERT INTO tbl_account (acc_id, acc_name, user_name, pass_word, gender, acc_type, phone, email)
                              VALUES (@id, @n, @u, @p, @g, @t, @ph, @em)",
                        Db.P("@id", id), Db.P("@n", name), Db.P("@u", user), Db.P("@p", PasswordHasher.Hash(pass)),
                        Db.P("@g", gender), Db.P("@t", type), Db.PN("@ph", phone), Db.PN("@em", mail));

                    Ui.Info("Account created successfully.", "Success");
                    btnClear.PerformClick();
                }
            }
            catch (Exception ex)
            {
                Ui.Error(ex);
            }
        }

        private static bool IsLastActiveAdmin(string accId)
        {
            object cur = Db.Scalar("SELECT acc_type FROM tbl_account WHERE acc_id = @id AND user_status = 'Active'", Db.P("@id", accId));
            if (cur == null || cur.ToString() != "Admin") return false;
            return Db.ToInt(Db.Scalar("SELECT COUNT(*) FROM tbl_account WHERE acc_type = 'Admin' AND user_status = 'Active' AND acc_id <> @id",
                Db.P("@id", accId))) == 0;
        }

        private static bool IsEmail(string s)
        {
            try { return new MailAddress(s).Address == s; }
            catch { return false; }
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            ClearFields();
        }

        private void ClearFields()
        {
            bool restrict = RestrictRoles;

            txtAccID.Clear();
            txtAccID.ReadOnly = false;
            txtAccName.Clear();
            cbAccountType.SelectedIndex = -1;
            if (restrict) cbAccountType.SelectedItem = "Cashier";
            radioButton1.Checked = false;
            radioButton2.Checked = false;
            txtUsername.Clear();
            txtPassword.Clear();
            txtphone.Clear();
            txtmail.Clear();
            checkBox1.Checked = false;
            label1.Text = "Create Account";
            btnSave.Text = "Save";
            Account_Variables.isEditMode = false;
            ShowNextId();
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            txtPassword.UseSystemPasswordChar = !checkBox1.Checked;
            checkBox1.Text = checkBox1.Checked ? "Hide Password" : "Show Password";
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Close();
        }

        private void frmCreateAccount_FormClosed(object sender, FormClosedEventArgs e)
        {
            Account_Variables.isEditMode = false;
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void cbAccountType_SelectedIndexChanged(object sender, EventArgs e) { }
    }
}