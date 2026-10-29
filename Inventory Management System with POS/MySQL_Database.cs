using System;
using MySql.Data.MySqlClient;
using System.Windows.Forms;

namespace Inventory_Management_System_with_POS
{
    public class MySQL_Database
    {
        public const string ConnectionString = "Server=localhost;Database=inventory_db;User id=root;Password=123456789;";

        public MySqlConnection conn = new MySqlConnection();
        public MySqlCommand cmd = new MySqlCommand();
        public MySqlDataReader dr;

        public void ConnectToDatabase()
        {
            try
            {
                if (conn.State == System.Data.ConnectionState.Open)
                    conn.Close();

                conn.ConnectionString = ConnectionString;
                conn.Open();
            }
            catch (MySqlException ex)
            {
                MessageBox.Show(
                    Db.Friendly(ex),
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        public void DisconnectFromDatabase()
        {
            try
            {
                if (conn.State == System.Data.ConnectionState.Open)
                    conn.Close();
            }
            catch
            {
            }
        }

        public bool IsConnected()
        {
            try
            {
                if (conn.State != System.Data.ConnectionState.Open)
                {
                    conn.ConnectionString = ConnectionString;
                    conn.Open();
                }

                return conn.State == System.Data.ConnectionState.Open;
            }
            catch
            {
                return false;
            }
        }

        public MySqlConnection GetConnection()
        {
            if (conn.State != System.Data.ConnectionState.Open)
            {
                conn.ConnectionString = ConnectionString;
                conn.Open();
            }

            return conn;
        }
    }
}