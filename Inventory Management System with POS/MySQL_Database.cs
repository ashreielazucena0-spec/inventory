using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using System.Windows.Forms;

namespace Inventory_Management_System_with_POS
{
    public class MySQL_Database
    {

        public MySqlConnection conn = new MySqlConnection();
        public MySqlCommand cmd = new MySqlCommand();
        public MySqlDataReader dr;


        public void ConnectToDatabase()
        {
            try
            {
                

                string str = "Server=localhost;Database=inventory_db;User id=root;Password=;";
                conn.ConnectionString = str;
                conn.Open();

                if (conn.State == System.Data.ConnectionState.Open)
                {
                    MessageBox.Show("Connection Established!", "Message", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    
                }
                else
                {
                    MessageBox.Show("Connection Failed!", "Message", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                
            }
        }
    }
}