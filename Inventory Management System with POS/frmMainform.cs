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
    public partial class frmMainform : Form
    {
        public readonly MySQL_Database _db;
        public frmMainform(MySQL_Database db)
        {
            InitializeComponent();
            _db = db;
        }

        private void label4_Click(object sender, EventArgs e)
        {

        }

        private void label5_Click(object sender, EventArgs e)
        {

        }

        private void panel1_Paint(object sender, PaintEventArgs e)
        {

        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            label2.Text = DateTime.Now.ToLongTimeString();
            label5.Text = DateTime.Now.ToLongDateString();
        }

        private void frmMainform_Load(object sender, EventArgs e)
        {
            //_db.ConnectToDatabase();
        }

        private void manageToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }
    }
}
