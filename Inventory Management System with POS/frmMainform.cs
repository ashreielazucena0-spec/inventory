using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace Inventory_Management_System_with_POS
{
    /// <summary>
    /// Main window: shows who is logged in, limits the menus by account type,
    /// hosts the other screens in panel2, and handles logout.
    /// </summary>
    public partial class frmMainform : Form
    {
        public readonly MySQL_Database _db;
        private readonly List<Form> _openChildForm = new List<Form>();

        public frmMainform(MySQL_Database db)
        {
            InitializeComponent();
            _db = db;
        }

        private void frmMainform_Load(object sender, EventArgs e)
        {
            statusStrip1.Visible = false;

            label4.Text = Session.UserId;
            label7.Text = Session.UserName + " (" + Session.UserType + ")";

            ApplyRoleAccess();
            NavigateTo("home");
        }

        /// <summary>Hides the menu entries the logged-in account type may not use.</summary>
        private void ApplyRoleAccess()
        {
            // Work out the flags first: reading ToolStripItem.Visible back is unreliable for items inside a closed drop-down.
            bool products = Session.CanAccess(Feature.Products);
            bool stocks = Session.CanAccess(Feature.Stocks);
            bool expired = Session.CanAccess(Feature.Expired);
            bool damage = Session.CanAccess(Feature.Damage);
            bool reports = Session.CanAccess(Feature.Reports);
            bool accounts = Session.CanAccess(Feature.Accounts);

            manageToolStripMenuItem.Available = products;
            manageStocksToolStripMenuItem.Available = stocks;
            expiredItemsToolStripMenuItem.Available = expired;
            damageToolStripMenuItem.Available = damage;
            manageReportsToolStripMenuItem.Available = reports;
            systemAccountsToolStripMenuItem.Available = accounts;
            toolStripButton3.Available = Session.CanAccess(Feature.POS);
            toolStripButton2.Available = Session.CanAccess(Feature.Dashboard);

            toolStripDropDownButton1.Available = products || stocks;
            toolStripDropDownButton2.Available = expired || damage || reports;
        }

        /// <summary>
        /// Opens a screen by name: home, dashboard, pos, products, stocks, expired, damage, reports, accounts.
        /// Other screens call this through Ui.Navigate().
        /// </summary>
        public void NavigateTo(string page)
        {
            Feature feature;
            Func<Form> make;

            switch (page)
            {
               
                case "dashboard": feature = Feature.Dashboard; make = () => new frmDashboard(); break;
                case "pos": feature = Feature.POS; make = () => new frmPOS(); break;
                case "products": feature = Feature.Products; make = () => new frmManageProduct(); break;
                case "stocks": feature = Feature.Stocks; make = () => new frmManageStock(); break;
                case "expired": feature = Feature.Expired; make = () => new frmExpiredItems(); break;
                case "damage": feature = Feature.Damage; make = () => new frmDamage(); break;
                case "reports": feature = Feature.Reports; make = () => new frmManageReports(); break;
                case "accounts": feature = Feature.Accounts; make = () => new frmAccounts(_db); break;
                default: return;
            }

            if (!Session.CanAccess(feature))
            {
                Ui.Warn("Your account type (" + Session.UserType + ") does not have access to this screen.", "Access Denied");
                return;
            }
            OpenChildForm(make());
        }

        private void OpenChildForm(Form childform)
        {
            foreach (Form form in _openChildForm) form.Close();
            _openChildForm.Clear();

            childform.TopLevel = false;
            panel2.Controls.Add(childform);
            childform.Show();
            _openChildForm.Add(childform);
        }

        /// <summary>Asks to log out; the login screen re-appears afterwards.</summary>
        public void RequestLogout()
        {
            if (Ui.Confirm("Do you want to Log out", "Logout"))
            {
                Session.LogoutRequested = true;
                Close();
            }
        }

        // ---- menu / toolbar ----
        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            foreach (Form child in _openChildForm.ToList())
            {
            child.Dispose();
            }
            panel2.Controls.Clear();
            panel2.BringToFront();




        }
        private void toolStripButton2_Click(object sender, EventArgs e) { 
            NavigateTo("dashboard"); 
        }

        private void toolStripButton3_Click(object sender, EventArgs e) { NavigateTo("pos"); }
        private void manageToolStripMenuItem_Click(object sender, EventArgs e) { NavigateTo("products"); }
        private void manageStocksToolStripMenuItem_Click(object sender, EventArgs e) { NavigateTo("stocks"); }
        private void expiredItemsToolStripMenuItem_Click(object sender, EventArgs e) { NavigateTo("expired"); }
        private void damageToolStripMenuItem_Click(object sender, EventArgs e) { NavigateTo("damage"); }
        private void manageReportsToolStripMenuItem_Click(object sender, EventArgs e) { NavigateTo("reports"); }
        private void systemAccountsToolStripMenuItem_Click(object sender, EventArgs e) { NavigateTo("accounts"); }

        private void logoutToolStripMenuItem_Click(object sender, EventArgs e) { RequestLogout(); }

        private void exitAppToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            label2.Text = DateTime.Now.ToLongTimeString();
            label5.Text = DateTime.Now.ToLongDateString();
        }

        private void label4_Click(object sender, EventArgs e) { }
        private void label5_Click(object sender, EventArgs e) { }
        private void panel1_Paint(object sender, PaintEventArgs e) { }
        private void label1_Click(object sender, EventArgs e) { }
        private void panel2_Paint(object sender, PaintEventArgs e) { }
        private void statusStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e) { statusStrip1.AllowMerge = false; }
        private void toolStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e) { }
        private void label6_Click(object sender, EventArgs e) { }
        private void panel3_Paint(object sender, PaintEventArgs e) { }

        private void toolStripDropDownButton3_Click(object sender, EventArgs e)
        {

        }
    }
}
