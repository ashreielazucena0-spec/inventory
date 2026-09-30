using System;
using System.Collections.Generic;
using System.Data;

namespace Inventory_Management_System_with_POS
{
    public class ReportEntry
    {
        public string Id;
        public DateTime GeneratedAt;
        public string Type;
        public string Params;
        public string Status;
        public string By;
        public DataTable Data;
        // saved filters so the report can be re-run
        public DateTime From, To;
        public string CategoryId, CategoryName, SupplierId, SupplierName;
    }

    /// <summary>
    /// The schema has no report table, so generated reports are kept for the
    /// running session only (the data itself always comes live from the database).
    /// </summary>
    public static class ReportStore
    {
        public static readonly List<ReportEntry> Entries = new List<ReportEntry>();
        private static int _seq;

        public static string NextId()
        {
            _seq++;
            return "RPT-" + DateTime.Now.ToString("yyyyMMdd") + "-" + _seq.ToString("000");
        }
    }

    public static class ReportService
    {
        public const string SalesTransactions = "Sales Transactions";
        public const string DailySales = "Daily Sales Summary";
        public const string SalesByProduct = "Sales by Product";
        public const string SalesByCategory = "Sales by Category";
        public const string SalesByCashier = "Sales by Cashier";
        public const string SalesByPayment = "Sales by Payment Method";
        public const string DiscountsVoids = "Discounts and Voids";
        public const string CurrentStock = "Current Stock";
        public const string LowStock = "Low Stock";
        public const string Valuation = "Inventory Valuation";
        public const string MovementLog = "Stock Movement Log";
        public const string Damaged = "Damaged Items";
        public const string Expired = "Expired Items";
        public const string ProductList = "Product List";

        public static readonly string[] Types =
        {
            SalesTransactions, DailySales, SalesByProduct, SalesByCategory, SalesByCashier, SalesByPayment, DiscountsVoids,
            CurrentStock, LowStock, Valuation, MovementLog, Damaged, Expired, ProductList
        };

        public static bool UsesDates(string t)
        {
            return t == SalesTransactions || t == DailySales || t == SalesByProduct || t == SalesByCategory
                || t == SalesByCashier || t == SalesByPayment || t == DiscountsVoids
                || t == MovementLog || t == Damaged || t == Expired;
        }

        public static bool UsesCategory(string t)
        {
            return t == SalesByProduct || t == SalesByCategory || t == CurrentStock || t == LowStock || t == Valuation
                || t == MovementLog || t == Damaged || t == Expired || t == ProductList;
        }

        public static bool UsesSupplier(string t)
        {
            return t == SalesByProduct || t == CurrentStock || t == LowStock || t == Valuation
                || t == MovementLog || t == Damaged || t == Expired || t == ProductList;
        }

        public static string Describe(string type, DateTime from, DateTime to, string categoryName, string supplierName)
        {
            var parts = new List<string>();
            if (UsesDates(type))
                parts.Add((type == Expired ? "Expiry " : "") + from.ToString("yyyy-MM-dd") + " to " + to.ToString("yyyy-MM-dd"));
            if (UsesCategory(type)) parts.Add("Category: " + (string.IsNullOrEmpty(categoryName) ? "All" : categoryName));
            if (UsesSupplier(type)) parts.Add("Supplier: " + (string.IsNullOrEmpty(supplierName) ? "All" : supplierName));
            return parts.Count == 0 ? "Current data" : string.Join(" | ", parts);
        }

        public static DataTable Build(string type, DateTime from, DateTime to, string categoryId, string supplierId)
        {
            var f = Db.P("@f", from.Date);
            var t = Db.P("@t", to.Date.AddDays(1));
            Func<object> cat = () => string.IsNullOrEmpty(categoryId) ? null : categoryId;
            Func<object> sup = () => string.IsNullOrEmpty(supplierId) ? null : supplierId;
            DataTable dt;

            switch (type)
            {
                case SalesTransactions:
                    dt = Db.Query(
                        @"SELECT s.sale_id AS `Invoice No`, s.created_at AS `Date`, IFNULL(a.acc_name,'(removed account)') AS `Cashier`,
                                 s.payment_method AS `Payment`, s.subtotal AS `Subtotal`, s.discount_amount AS `Discount`,
                                 s.tax_amount AS `Tax`, s.total_amount AS `Total`, s.amount_paid AS `Amount Paid`,
                                 s.change_due AS `Change`, s.sale_status AS `Status`
                          FROM tbl_sale s LEFT JOIN tbl_account a ON a.acc_id = s.cashier_id
                          WHERE s.created_at >= @f AND s.created_at < @t
                          ORDER BY s.created_at", f, t);
                    // totals count completed sales only
                    if (dt.Rows.Count > 0)
                    {
                        decimal sub = 0, disc = 0, tax = 0, tot = 0;
                        foreach (DataRow r in dt.Rows)
                        {
                            if (r["Status"].ToString() != "Completed") continue;
                            sub += Db.ToDec(r["Subtotal"]); disc += Db.ToDec(r["Discount"]);
                            tax += Db.ToDec(r["Tax"]); tot += Db.ToDec(r["Total"]);
                        }
                        DataRow row = dt.NewRow();
                        row["Invoice No"] = "TOTAL (completed)";
                        row["Subtotal"] = sub; row["Discount"] = disc; row["Tax"] = tax; row["Total"] = tot;
                        dt.Rows.Add(row);
                    }
                    return dt;

                case DailySales:
                    dt = Db.Query(
                        @"SELECT DATE_FORMAT(s.created_at, '%Y-%m-%d') AS `Date`, COUNT(*) AS `Transactions`,
                                 SUM(s.subtotal) AS `Subtotal`, SUM(s.discount_amount) AS `Discounts`,
                                 SUM(s.tax_amount) AS `Tax`, SUM(s.total_amount) AS `Net Sales`
                          FROM tbl_sale s
                          WHERE s.sale_status = 'Completed' AND s.created_at >= @f AND s.created_at < @t
                          GROUP BY DATE_FORMAT(s.created_at, '%Y-%m-%d')
                          ORDER BY DATE_FORMAT(s.created_at, '%Y-%m-%d')", f, t);
                    AppendTotals(dt, "Date", "Transactions", "Subtotal", "Discounts", "Tax", "Net Sales");
                    return dt;

                case SalesByProduct:
                    dt = Db.Query(
                        @"SELECT p.product_id AS `Product ID`, p.product_name AS `Product`, c.category_name AS `Category`,
                                 CAST(SUM(si.quantity) AS SIGNED) AS `Qty Sold`, SUM(si.line_total) AS `Sales`,
                                 SUM(si.line_total - si.quantity * p.cost_price) AS `Est Profit`
                          FROM tbl_sale_item si
                          JOIN tbl_sale s ON s.sale_id = si.sale_id
                          JOIN tbl_product p ON p.product_id = si.product_id
                          JOIN tbl_category c ON c.category_id = p.category_id
                          WHERE s.sale_status = 'Completed' AND s.created_at >= @f AND s.created_at < @t
                            AND (@cat IS NULL OR p.category_id = @cat) AND (@sup IS NULL OR p.supplier_id = @sup)
                          GROUP BY p.product_id, p.product_name, c.category_name
                          ORDER BY SUM(si.line_total) DESC", f, t, Db.P("@cat", cat()), Db.P("@sup", sup()));
                    AppendTotals(dt, "Product", "Qty Sold", "Sales", "Est Profit");
                    return dt;

                case SalesByCategory:
                    dt = Db.Query(
                        @"SELECT c.category_name AS `Category`, CAST(SUM(si.quantity) AS SIGNED) AS `Qty Sold`,
                                 SUM(si.line_total) AS `Sales`
                          FROM tbl_sale_item si
                          JOIN tbl_sale s ON s.sale_id = si.sale_id
                          JOIN tbl_product p ON p.product_id = si.product_id
                          JOIN tbl_category c ON c.category_id = p.category_id
                          WHERE s.sale_status = 'Completed' AND s.created_at >= @f AND s.created_at < @t
                            AND (@cat IS NULL OR p.category_id = @cat)
                          GROUP BY c.category_id, c.category_name
                          ORDER BY SUM(si.line_total) DESC", f, t, Db.P("@cat", cat()));
                    AppendTotals(dt, "Category", "Qty Sold", "Sales");
                    return dt;

                case SalesByCashier:
                    dt = Db.Query(
                        @"SELECT IFNULL(a.acc_name,'(removed account)') AS `Cashier`, COUNT(*) AS `Transactions`,
                                 SUM(s.discount_amount) AS `Discounts`, SUM(s.total_amount) AS `Net Sales`
                          FROM tbl_sale s LEFT JOIN tbl_account a ON a.acc_id = s.cashier_id
                          WHERE s.sale_status = 'Completed' AND s.created_at >= @f AND s.created_at < @t
                          GROUP BY s.cashier_id, a.acc_name
                          ORDER BY SUM(s.total_amount) DESC", f, t);
                    AppendTotals(dt, "Cashier", "Transactions", "Discounts", "Net Sales");
                    return dt;

                case SalesByPayment:
                    dt = Db.Query(
                        @"SELECT s.payment_method AS `Payment Method`, COUNT(*) AS `Transactions`, SUM(s.total_amount) AS `Net Sales`
                          FROM tbl_sale s
                          WHERE s.sale_status = 'Completed' AND s.created_at >= @f AND s.created_at < @t
                          GROUP BY s.payment_method
                          ORDER BY SUM(s.total_amount) DESC", f, t);
                    AppendTotals(dt, "Payment Method", "Transactions", "Net Sales");
                    return dt;

                case DiscountsVoids:
                    return Db.Query(
                        @"SELECT s.sale_id AS `Invoice No`, s.created_at AS `Date`, IFNULL(a.acc_name,'') AS `Cashier`,
                                 IFNULL(s.discount_type,'') AS `Discount Type`, s.discount_amount AS `Order Discount`,
                                 IFNULL((SELECT SUM(i.line_discount) FROM tbl_sale_item i WHERE i.sale_id = s.sale_id),0) AS `Line Discounts`,
                                 IFNULL(ap.acc_name,'') AS `Approved By`, s.total_amount AS `Total`, s.sale_status AS `Status`
                          FROM tbl_sale s
                          LEFT JOIN tbl_account a ON a.acc_id = s.cashier_id
                          LEFT JOIN tbl_account ap ON ap.acc_id = s.discount_approved_by
                          WHERE s.created_at >= @f AND s.created_at < @t
                            AND (s.discount_amount > 0 OR s.sale_status <> 'Completed'
                                 OR EXISTS (SELECT 1 FROM tbl_sale_item i WHERE i.sale_id = s.sale_id AND i.line_discount > 0))
                          ORDER BY s.created_at", f, t);

                case CurrentStock:
                case LowStock:
                    dt = Db.Query(
                        @"SELECT p.product_id AS `Product ID`, p.product_name AS `Product`, IFNULL(p.barcode,'') AS `Barcode`,
                                 c.category_name AS `Category`, IFNULL(sp.supplier_name,'') AS `Supplier`,
                                 CAST(IFNULL(s.quantity,0) AS SIGNED) AS `In Stock`, CAST(IFNULL(s.low_stock_threshold,10) AS SIGNED) AS `Alert Level`,
                                 p.cost_price AS `Cost`, p.selling_price AS `Price`, IFNULL(s.quantity,0) * p.cost_price AS `Stock Value`,
                                 p.product_status AS `Status`
                          FROM tbl_product p
                          JOIN tbl_category c ON c.category_id = p.category_id
                          LEFT JOIN tbl_stock s ON s.product_id = p.product_id
                          LEFT JOIN tbl_supplier sp ON sp.supplier_id = p.supplier_id
                          WHERE (@cat IS NULL OR p.category_id = @cat) AND (@sup IS NULL OR p.supplier_id = @sup) "
                        + (type == LowStock ? "AND p.product_status = 'Active' AND IFNULL(s.quantity,0) <= IFNULL(s.low_stock_threshold,10) " : "") +
                        @"ORDER BY p.product_name", Db.P("@cat", cat()), Db.P("@sup", sup()));
                    AppendTotals(dt, "Product", "In Stock", "Stock Value");
                    return dt;

                case Valuation:
                    dt = Db.Query(
                        @"SELECT c.category_name AS `Category`, COUNT(p.product_id) AS `Products`,
                                 CAST(IFNULL(SUM(IFNULL(s.quantity,0)),0) AS SIGNED) AS `Units`,
                                 IFNULL(SUM(IFNULL(s.quantity,0) * p.cost_price),0) AS `Cost Value`,
                                 IFNULL(SUM(IFNULL(s.quantity,0) * p.selling_price),0) AS `Retail Value`
                          FROM tbl_category c
                          LEFT JOIN tbl_product p ON p.category_id = c.category_id AND (@sup IS NULL OR p.supplier_id = @sup)
                          LEFT JOIN tbl_stock s ON s.product_id = p.product_id
                          WHERE (@cat IS NULL OR c.category_id = @cat)
                          GROUP BY c.category_id, c.category_name
                          ORDER BY c.category_name", Db.P("@cat", cat()), Db.P("@sup", sup()));
                    AppendTotals(dt, "Category", "Products", "Units", "Cost Value", "Retail Value");
                    return dt;

                case MovementLog:
                    return Db.Query(
                        @"SELECT l.log_id AS `Log ID`, l.created_at AS `Date`, l.product_id AS `Product ID`, p.product_name AS `Product`,
                                 c.category_name AS `Category`, l.movement_type AS `Type`, l.reason AS `Reason`, l.quantity AS `Qty`,
                                 IFNULL(l.batch_no,'') AS `Batch`, l.expiry_date AS `Expiry`, l.unit_cost AS `Unit Cost`,
                                 IFNULL(l.reference_id,'') AS `Reference`, IFNULL(a.acc_name,'') AS `Performed By`, IFNULL(l.remarks,'') AS `Remarks`
                          FROM tbl_stock_log l
                          JOIN tbl_product p ON p.product_id = l.product_id
                          JOIN tbl_category c ON c.category_id = p.category_id
                          LEFT JOIN tbl_account a ON a.acc_id = l.performed_by
                          WHERE l.created_at >= @f AND l.created_at < @t
                            AND (@cat IS NULL OR p.category_id = @cat) AND (@sup IS NULL OR p.supplier_id = @sup)
                          ORDER BY l.created_at DESC, l.log_id DESC", f, t, Db.P("@cat", cat()), Db.P("@sup", sup()));

                case Damaged:
                    dt = Db.Query(
                        @"SELECT l.created_at AS `Date`, l.product_id AS `Product ID`, p.product_name AS `Product`,
                                 c.category_name AS `Category`, CAST(l.quantity AS SIGNED) AS `Qty`,
                                 COALESCE(l.unit_cost, p.cost_price) AS `Unit Cost`,
                                 l.quantity * COALESCE(l.unit_cost, p.cost_price) AS `Est Loss`,
                                 IFNULL(l.remarks,'') AS `Remarks`, IFNULL(a.acc_name,'') AS `Logged By`
                          FROM tbl_stock_log l
                          JOIN tbl_product p ON p.product_id = l.product_id
                          JOIN tbl_category c ON c.category_id = p.category_id
                          LEFT JOIN tbl_account a ON a.acc_id = l.performed_by
                          WHERE l.movement_type = 'OUT' AND l.reason = 'DAMAGED'
                            AND l.created_at >= @f AND l.created_at < @t
                            AND (@cat IS NULL OR p.category_id = @cat) AND (@sup IS NULL OR p.supplier_id = @sup)
                          ORDER BY l.created_at DESC, l.log_id DESC", f, t, Db.P("@cat", cat()), Db.P("@sup", sup()));
                    AppendTotals(dt, "Product", "Qty", "Est Loss");
                    return dt;

                case Expired:
                    return BuildExpired(from.Date, to.Date, categoryId, supplierId);

                case ProductList:
                    return Db.Query(
                        @"SELECT p.product_id AS `Product ID`, p.product_name AS `Product`, IFNULL(p.barcode,'') AS `Barcode`,
                                 c.category_name AS `Category`, IFNULL(sp.supplier_name,'') AS `Supplier`,
                                 p.cost_price AS `Cost`, p.selling_price AS `Price`, p.product_status AS `Status`, p.created_at AS `Created`
                          FROM tbl_product p
                          JOIN tbl_category c ON c.category_id = p.category_id
                          LEFT JOIN tbl_supplier sp ON sp.supplier_id = p.supplier_id
                          WHERE (@cat IS NULL OR p.category_id = @cat) AND (@sup IS NULL OR p.supplier_id = @sup)
                          ORDER BY p.product_id", Db.P("@cat", cat()), Db.P("@sup", sup()));
            }
            throw new ArgumentException("Unknown report type: " + type);
        }

        private static DataTable BuildExpired(DateTime from, DateTime to, string categoryId, string supplierId)
        {
            DataTable batches = InventoryService.GetBatches(null);
            var result = new DataTable();
            result.Columns.Add("Product ID", typeof(string));
            result.Columns.Add("Product", typeof(string));
            result.Columns.Add("Category", typeof(string));
            result.Columns.Add("Batch", typeof(string));
            result.Columns.Add("Expiry Date", typeof(DateTime));
            result.Columns.Add("Units Left", typeof(int));
            result.Columns.Add("Unit Cost", typeof(decimal));
            result.Columns.Add("Loss at Cost", typeof(decimal));
            result.Columns.Add("Status", typeof(string));

            foreach (DataRow r in batches.Select("", "expiry_date ASC"))
            {
                DateTime exp = Convert.ToDateTime(r["expiry_date"]).Date;
                if (exp < from || exp > to) continue;
                if (!string.IsNullOrEmpty(categoryId) && r["category_id"].ToString() != categoryId) continue;
                if (!string.IsNullOrEmpty(supplierId) && r["supplier_id"].ToString() != supplierId) continue;
                int left = Db.ToInt(r["remaining"]);
                decimal cost = Db.ToDec(r["cost_price"]);
                result.Rows.Add(r["product_id"], r["product_name"], r["category_name"], r["batch_no"], exp, left, cost, left * cost,
                    exp < DateTime.Today ? "Expired" : "Not yet expired");
            }
            AppendTotals(result, "Product", "Units Left", "Loss at Cost");
            return result;
        }

        /// <summary>Adds a TOTAL row that sums the given numeric columns (label goes in a text column).</summary>
        private static void AppendTotals(DataTable dt, string labelColumn, params string[] sumColumns)
        {
            if (dt.Rows.Count == 0) return;
            DataRow tot = dt.NewRow();
            tot[labelColumn] = "TOTAL";
            foreach (string name in sumColumns)
            {
                DataColumn col = dt.Columns[name];
                if (col.DataType == typeof(decimal))
                {
                    decimal s = 0;
                    foreach (DataRow r in dt.Rows) s += Db.ToDec(r[name]);
                    tot[name] = s;
                }
                else
                {
                    long s = 0;
                    foreach (DataRow r in dt.Rows) s += r[name] == DBNull.Value ? 0 : Convert.ToInt64(r[name]);
                    tot[name] = Convert.ChangeType(s, col.DataType);
                }
            }
            dt.Rows.Add(tot);
        }
    }
}
