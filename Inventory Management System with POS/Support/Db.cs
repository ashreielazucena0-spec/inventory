using MySql.Data.MySqlClient;
using System;
using System.Data;

namespace Inventory_Management_System_with_POS
{
    /// <summary>
    /// Small data-access helper. Every call uses its own connection (or one you
    /// pass in for transactions), so forms no longer fight over one shared
    /// open connection.
    /// </summary>
    public static class Db
    {
        public static MySqlConnection Open()
        {
            var c = new MySqlConnection(MySQL_Database.ConnectionString);
            c.Open();

            // make the connection use the same collation as the tables (fixes "Illegal mix of collations")
            using (var cmd = new MySqlCommand("SET NAMES utf8mb4 COLLATE utf8mb4_general_ci", c))
                cmd.ExecuteNonQuery();

            return c;
        }

        public static MySqlParameter P(string name, object value)
        {
            return new MySqlParameter(name, value ?? DBNull.Value);
        }

        /// <summary>Parameter that turns blank text into NULL.</summary>
        public static MySqlParameter PN(string name, string value)
        {
            return new MySqlParameter(name, string.IsNullOrWhiteSpace(value) ? (object)DBNull.Value : value.Trim());
        }

        private static MySqlCommand Cmd(MySqlConnection c, MySqlTransaction tx, string sql, MySqlParameter[] ps)
        {
            var cmd = new MySqlCommand(sql, c, tx);
            if (ps != null)
                foreach (var p in ps) cmd.Parameters.Add(p);
            return cmd;
        }

        // ---- own connection ----
        public static DataTable Query(string sql, params MySqlParameter[] ps)
        {
            using (var c = Open()) return Query(c, null, sql, ps);
        }

        public static object Scalar(string sql, params MySqlParameter[] ps)
        {
            using (var c = Open()) return Scalar(c, null, sql, ps);
        }

        public static int Exec(string sql, params MySqlParameter[] ps)
        {
            using (var c = Open()) return Exec(c, null, sql, ps);
        }

        // ---- caller-supplied connection / transaction ----
        public static DataTable Query(MySqlConnection c, MySqlTransaction tx, string sql, params MySqlParameter[] ps)
        {
            var dt = new DataTable();
            using (var cmd = Cmd(c, tx, sql, ps))
            using (var da = new MySqlDataAdapter(cmd))
                da.Fill(dt);
            return dt;
        }

        public static object Scalar(MySqlConnection c, MySqlTransaction tx, string sql, params MySqlParameter[] ps)
        {
            using (var cmd = Cmd(c, tx, sql, ps)) return cmd.ExecuteScalar();
        }

        public static int Exec(MySqlConnection c, MySqlTransaction tx, string sql, params MySqlParameter[] ps)
        {
            using (var cmd = Cmd(c, tx, sql, ps)) return cmd.ExecuteNonQuery();
        }

        public static int ToInt(object o)
        {
            return (o == null || o == DBNull.Value) ? 0 : Convert.ToInt32(o);
        }

        public static decimal ToDec(object o)
        {
            return (o == null || o == DBNull.Value) ? 0m : Convert.ToDecimal(o);
        }

        /// <summary>
        /// Next running id such as PRD-0007 / SALE-000012 / ACC-004 for a text primary key.
        /// Table/column names are internal constants, never user input.
        /// </summary>
        public static string NextId(string table, string column, string prefix, int pad,
                                    MySqlConnection c, MySqlTransaction tx)
        {
            string sql = "SELECT COALESCE(MAX(CAST(SUBSTRING(" + column + ", @len) AS UNSIGNED)), 0) + 1 FROM " + table +
                         " WHERE " + column + " LIKE @like AND SUBSTRING(" + column + ", @len) REGEXP '^[0-9]+$'" +
                         (tx != null ? " FOR UPDATE" : "");
            object o = Scalar(c, tx, sql, P("@len", prefix.Length + 1), P("@like", prefix + "%"));
            long n = Convert.ToInt64(o);
            return prefix + n.ToString().PadLeft(pad, '0');
        }

        public static string NextId(string table, string column, string prefix, int pad)
        {
            using (var c = Open()) return NextId(table, column, prefix, pad, c, null);
        }

        /// <summary>Turns common MySQL errors into plain language.</summary>
        public static string Friendly(Exception ex)
        {
            var me = ex as MySqlException;
            if (me != null)
            {
                switch (me.Number)
                {
                    case 1062: return "That value already exists (duplicate entry).";
                    case 1451: return "This record is used by other records and cannot be removed.";
                    case 1452: return "A related record was not found (invalid reference).";
                    case 1045: return "Database login failed. Check the user name and password in MySQL_Database.cs.";
                    case 1049: return "Database 'inventory_db' was not found. Import inventory_db.sql first.";
                }
            }
            return ex.Message;
        }
    }
}