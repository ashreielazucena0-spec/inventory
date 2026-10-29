using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Inventory_Management_System_with_POS
{
    public class CartLine
    {
        public string ProductId;
        public string ProductName;
        public int Qty;
        public decimal UnitPrice;
        public decimal LineDiscount;

        public decimal LineTotal { get { return Qty * UnitPrice - LineDiscount; } }
    }

    public static class SalesService
    {
        /// <summary>Tax rate applied to (subtotal - discount). 0 = no tax, as in the existing sales.</summary>
        public static decimal TaxRate = 0m;

        public static readonly string[] PaymentMethods = { "Cash", "GCash", "Maya", "Card" };

        /// <summary>
        /// Saves a sale in one transaction: tbl_sale, tbl_sale_item, tbl_stock and
        /// tbl_stock_log (OUT / SALE). Nothing is saved if any step fails, e.g. when
        /// another till just sold the last unit.
        /// </summary>
        public static string CompleteSale(IList<CartLine> lines, decimal orderDiscount, string discountType,
            string approverId, string paymentMethod, decimal amountPaid, string cashierId, out decimal changeDue)
        {
            if (lines == null || lines.Count == 0) throw new InvalidOperationException("The cart is empty.");

            decimal subtotal = lines.Sum(l => l.LineTotal);
            if (orderDiscount < 0 || orderDiscount > subtotal) throw new InvalidOperationException("Invalid discount amount.");
            decimal tax = Math.Round((subtotal - orderDiscount) * TaxRate, 2);
            decimal total = subtotal - orderDiscount + tax;
            if (amountPaid < total) throw new InvalidOperationException("Amount paid is less than the total.");
            changeDue = amountPaid - total;

            bool hasDiscount = orderDiscount > 0 || lines.Any(l => l.LineDiscount > 0);
            if (hasDiscount && string.IsNullOrEmpty(approverId))
                throw new InvalidOperationException("A discount needs manager approval.");

            using (var c = Db.Open())
            using (var tx = c.BeginTransaction())
            {
                try
                {
                    string saleId = Db.NextId("tbl_sale", "sale_id", "SALE-", 6, c, tx);

                    Db.Exec(c, tx,
                        @"INSERT INTO tbl_sale
                            (sale_id, cashier_id, subtotal, discount_amount, discount_type, discount_approved_by,
                             tax_amount, total_amount, amount_paid, change_due, payment_method, sale_status)
                          VALUES (@id, @cash, @sub, @disc, @dtype, @appr, @tax, @tot, @paid, @chg, @pay, 'Completed')",
                        Db.P("@id", saleId), Db.PN("@cash", cashierId), Db.P("@sub", subtotal),
                        Db.P("@disc", orderDiscount),
                        Db.PN("@dtype", hasDiscount ? discountType : null),
                        Db.PN("@appr", hasDiscount ? approverId : null),
                        Db.P("@tax", tax), Db.P("@tot", total), Db.P("@paid", amountPaid), Db.P("@chg", changeDue),
                        Db.P("@pay", paymentMethod));

                    foreach (CartLine l in lines)
                    {
                        object status = Db.Scalar(c, tx, "SELECT product_status FROM tbl_product WHERE product_id = @p FOR UPDATE",
                            Db.P("@p", l.ProductId));
                        if (status == null) throw new InvalidOperationException("Product " + l.ProductId + " no longer exists.");
                        if (status.ToString() != "Active")
                            throw new InvalidOperationException(l.ProductName + " is inactive and can't be sold.");

                        Db.Exec(c, tx,
                            @"INSERT INTO tbl_sale_item (sale_id, product_id, quantity, unit_price, line_discount, line_total)
                              VALUES (@s, @p, @q, @u, @d, @t)",
                            Db.P("@s", saleId), Db.P("@p", l.ProductId), Db.P("@q", l.Qty),
                            Db.P("@u", l.UnitPrice), Db.P("@d", l.LineDiscount), Db.P("@t", l.LineTotal));

                        try
                        {
                            InventoryService.ApplyMovement(c, tx, l.ProductId, "OUT", "SALE", l.Qty,
                                null, null, null, null, saleId, cashierId, "POS sale", null);
                        }
                        catch (InvalidOperationException ex)
                        {
                            throw new InvalidOperationException(l.ProductName + ": " + ex.Message);
                        }
                    }

                    tx.Commit();
                    return saleId;
                }
                catch
                {
                    try { tx.Rollback(); } catch { }
                    throw;
                }
            }
        }

        /// <summary>
        /// Cancels a completed sale: sale_status becomes 'Cancelled' and the items go
        /// back into stock (IN / RETURN, referencing the sale).
        /// </summary>
        public static void VoidSale(string saleId, string approverId, string performedBy, string reason)
        {
            using (var c = Db.Open())
            using (var tx = c.BeginTransaction())
            {
                try
                {
                    object status = Db.Scalar(c, tx, "SELECT sale_status FROM tbl_sale WHERE sale_id = @id FOR UPDATE", Db.P("@id", saleId));
                    if (status == null) throw new InvalidOperationException("Invoice " + saleId + " was not found.");
                    if (status.ToString() != "Completed")
                        throw new InvalidOperationException("Invoice " + saleId + " is already " + status + ".");

                    Db.Exec(c, tx, "UPDATE tbl_sale SET sale_status = 'Cancelled' WHERE sale_id = @id", Db.P("@id", saleId));

                    DataTable items = Db.Query(c, tx, "SELECT product_id, quantity FROM tbl_sale_item WHERE sale_id = @id", Db.P("@id", saleId));
                    string note = "Sale cancelled: " + reason + (string.IsNullOrEmpty(approverId) ? "" : " (approved by " + approverId + ")");
                    foreach (DataRow r in items.Rows)
                    {
                        InventoryService.ApplyMovement(c, tx, r["product_id"].ToString(), "IN", "RETURN", Db.ToInt(r["quantity"]),
                            null, null, null, null, saleId, performedBy, note, null);
                    }
                    tx.Commit();
                }
                catch
                {
                    try { tx.Rollback(); } catch { }
                    throw;
                }
            }
        }

        /// <summary>Plain-text receipt (40 characters wide), built from what is saved in the database.</summary>
        public static List<string> ReceiptLines(string saleId)
        {
            const int W = 40;
            DataTable s = Db.Query(
                @"SELECT s.*, IFNULL(a.acc_name,'') AS cashier_name, IFNULL(ap.acc_name,'') AS approver_name
                  FROM tbl_sale s
                  LEFT JOIN tbl_account a ON a.acc_id = s.cashier_id
                  LEFT JOIN tbl_account ap ON ap.acc_id = s.discount_approved_by
                  WHERE s.sale_id = @id", Db.P("@id", saleId));
            if (s.Rows.Count == 0) throw new InvalidOperationException("Invoice " + saleId + " was not found.");
            DataRow h = s.Rows[0];

            DataTable items = Db.Query(
                @"SELECT p.product_name, si.quantity, si.unit_price, si.line_discount, si.line_total
                  FROM tbl_sale_item si JOIN tbl_product p ON p.product_id = si.product_id
                  WHERE si.sale_id = @id ORDER BY si.sale_item_id", Db.P("@id", saleId));

            var L = new List<string>();
            L.Add(Center("TOP MART", W));
            L.Add(Center("Official Receipt", W));
            L.Add(new string('-', W));
            L.Add("Invoice : " + saleId);
            L.Add("Date    : " + Convert.ToDateTime(h["created_at"]).ToString("yyyy-MM-dd HH:mm"));
            L.Add("Cashier : " + h["cashier_name"]);
            if (h["sale_status"].ToString() != "Completed") L.Add("Status  : " + h["sale_status"].ToString().ToUpper());
            L.Add(new string('-', W));
            L.Add("Item".PadRight(22) + "Qty".PadLeft(4) + "Amount".PadLeft(14));
            foreach (DataRow r in items.Rows)
            {
                string name = r["product_name"].ToString();
                if (name.Length > 22) name = name.Substring(0, 22);
                int q = Db.ToInt(r["quantity"]);
                L.Add(name.PadRight(22) + q.ToString().PadLeft(4) + Db.ToDec(r["line_total"]).ToString("N2").PadLeft(14));
                if (q > 1 || Db.ToDec(r["line_discount"]) > 0)
                {
                    string detail = "  " + q + " x " + Db.ToDec(r["unit_price"]).ToString("N2");
                    if (Db.ToDec(r["line_discount"]) > 0) detail += "  (less " + Db.ToDec(r["line_discount"]).ToString("N2") + ")";
                    L.Add(detail);
                }
            }
            L.Add(new string('-', W));
            L.Add(Row("Subtotal", Db.ToDec(h["subtotal"]), W));
            if (Db.ToDec(h["discount_amount"]) > 0)
            {
                string dt = h["discount_type"] == DBNull.Value ? "Discount" : "Discount (" + h["discount_type"] + ")";
                L.Add(Row(dt, -Db.ToDec(h["discount_amount"]), W));
            }
            if (Db.ToDec(h["tax_amount"]) > 0) L.Add(Row("Tax", Db.ToDec(h["tax_amount"]), W));
            L.Add(Row("TOTAL", Db.ToDec(h["total_amount"]), W));
            L.Add(Row(h["payment_method"] + " paid", Db.ToDec(h["amount_paid"]), W));
            L.Add(Row("Change", Db.ToDec(h["change_due"]), W));
            if (h["approver_name"].ToString().Length > 0) L.Add("Discount approved by: " + h["approver_name"]);
            L.Add(new string('-', W));
            L.Add(Center("THANK YOU! PLEASE COME AGAIN", W));
            return L;
        }

        private static string Center(string s, int w)
        {
            if (s.Length >= w) return s;
            return new string(' ', (w - s.Length) / 2) + s;
        }

        private static string Row(string label, decimal amount, int w)
        {
            string a = amount.ToString("N2");
            return label.PadRight(w - a.Length) + a;
        }
    }
}
