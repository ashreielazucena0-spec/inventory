using System;
using System.Windows.Forms;

namespace Inventory_Management_System_with_POS
{
    /// <summary>Home: quick-start buttons for each screen and a product look-up box.</summary>
    public partial class frmHome : Form
    {
        public frmHome()
        {
            InitializeComponent();
            Load += new EventHandler(frmHome_Load);
        }

        private void frmHome_Load(object sender, EventArgs e)
        {
            Wire(button1, delegate { Ui.Navigate(this, "expired"); });
            Wire(button10, button10_Click);
            Wire(button2, delegate { Ui.Navigate(this, "stocks"); });
            Wire(button3, delegate { Ui.Navigate(this, "damage"); });
            Wire(button4, delegate { Ui.Navigate(this, "pos"); });
            Wire(button7, delegate { Ui.Navigate(this, "stocks"); });   // VIEW INVENTORY = current inventory list
            button5.Text = "SIGN OUT";
            Wire(button5, delegate
            {
                Control c = this;
                while (c != null && !(c is frmMainform)) c = c.Parent;
                var main = c as frmMainform;
                if (main != null) main.RequestLogout();
            });

            // Only show the shortcuts this account type can use.
            button1.Visible = Session.CanAccess(Feature.Expired);
            button10.Visible = Session.CanAccess(Feature.Products);
            button2.Visible = Session.CanAccess(Feature.Stocks);
            button7.Visible = Session.CanAccess(Feature.Stocks);
            button3.Visible = Session.CanAccess(Feature.Damage);
            button4.Visible = Session.CanAccess(Feature.POS);

            // Search box: press Enter to look up a product (id, name or barcode).
            textBox1.KeyDown += (s, a) =>
            {
                if (a.KeyCode != Keys.Enter) return;
                a.SuppressKeyPress = true;
                Ui.ShowProductInfo(textBox1.Text);
            };
        }

        private static void Wire(Button b, EventHandler h)
        {
            b.Click -= h;
            b.Click += h;
        }

        private void button10_Click(object sender, EventArgs e)
        {
            Ui.Navigate(this, "products");
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            label14.Text = DateTime.Now.ToLongDateString();
            label15.Text = DateTime.Now.ToLongTimeString();
        }

        private void pictureBox2_Click(object sender, EventArgs e) { }
        private void panel2_Paint(object sender, PaintEventArgs e) { }
        private void label14_Click(object sender, EventArgs e) { }
    }
}
