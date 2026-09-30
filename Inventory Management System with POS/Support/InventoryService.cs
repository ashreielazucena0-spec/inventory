using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text.RegularExpressions;

namespace Inventory_Management_System_with_POS
{
    /// <summary>
    /// The one place that changes stock. tbl_stock holds the current quantity;
    /// every change also writes a tbl_stock_log row, in the same transaction.
    /// </summary>
    public static class InventoryService
    {
        /// <summary>
        /// Adds (IN) or removes (OUT) stock inside a transaction you control.
        /// Throws if the product would go below zero.
        /// </summary>
        public static void ApplyMovement(MySqlConnection c, MySqlTransaction tx,
            string productId, string movementType, string reason, int qty,
            string batchNo, DateTime? expiry, decimal? unitCost, string supplierId,
            string referenceId, string performedBy, string remarks, DateTime? createdAt)
        {
            if (qty <= 0) throw new ArgumentException("Quantity must be greater than zero.");
            if (movementType != "IN" && movementType != "OUT") throw new ArgumentException("Movement type must be IN or OUT.");

            // Make sure a stock row exists, then lock it for this transaction.
            Db.Exec(c, tx, "INSERT IGNORE INTO tbl_stock (product_id, quantity) VALUES (@p, 0)", Db.P("@p", productId));
            int current = Db.ToInt(Db.Scalar(c, tx, "SELECT quantity FROM tbl_stock WHERE product_id = @p FOR UPDATE", Db.P("@p", productId)));

            int delta = movementType == "IN" ? qty : -qty;
            if (current + delta < 0)
                throw new InvalidOperationException("Not enough stock. On hand: " + current + ", requested: " + qty + ".");

            Db.Exec(c, tx, "UPDATE tbl_stock SET quantity = quantity + @d WHERE product_id = @p",
                Db.P("@d", delta), Db.P("@p", productId));

            if (remarks != null && remarks.Length > 255) remarks = remarks.Substring(0, 255);

            Db.Exec(c, tx,
                @"INSERT INTO tbl_stock_log
                    (product_id, movement_type, reason, quantity, batch_no, expiry_date, unit_cost,
                     supplier_id, reference_id, performed_by, remarks, created_at)
                  VALUES (@p, @mt, @rs, @q, @b, @x, @uc, @sp, @ref, @by, @rm, @at)",
                Db.P("@p", productId), Db.P("@mt", movementType), Db.P("@rs", reason), Db.P("@q", qty),
                Db.PN("@b", batchNo),
                Db.P("@x", expiry.HasValue ? (object)expiry.Value.Date : null),
                Db.P("@uc", unitCost.HasValue ? (object)unitCost.Value : null),
                Db.PN("@sp", supplierId), Db.PN("@ref", referenceId), Db.PN("@by", performedBy), Db.PN("@rm", remarks),
                Db.P("@at", createdAt ?? DateTime.Now));
        }

        /// <summary>Same as ApplyMovement but runs in its own transaction.</summary>
        public static void Apply(string productId, string movementType, string reason, int qty,
            string batchNo, DateTime? expiry, decimal? unitCost, string supplierId,
            string referenceId, string performedBy, string remarks, DateTime? createdAt)
        {
            using (var c = Db.Open())
            using (var tx = c.BeginTransaction())
            {
                try
                {
                    ApplyMovement(c, tx, productId, movementType, reason, qty, batchNo, expiry, unitCost,
                        supplierId, referenceId, performedBy, remarks, createdAt);
                    tx.Commit();
                }
                catch
                {
                    try { tx.Rollback(); } catch { }
                    throw;
                }
            }
        }

        public static string NewBatchNo()
        {
            return "BATCH-" + DateTime.Now.ToString("yyyyMMddHHmmss");
        }

        // ------------------------------------------------------------------
        // Batches / expiry
        // ------------------------------------------------------------------
        private class Batch
        {
            public string ProductId;
            public string BatchNo;
            public DateTime? Expiry;
            public DateTime Received;
            public int Remaining;
        }

        /// <summary>
        /// tbl_stock only stores a total per product, so units left in each batch are
        /// worked out from tbl_stock_log:
        ///   1. every IN row is a batch;
        ///   2. OUT rows that name a batch (e.g. expired disposal) are taken from that batch;
        ///   3. all other OUT rows (sales, damage) are taken oldest-received-first (FIFO);
        ///   4. the result is capped at the quantity really on hand in tbl_stock.
        /// Returns only batches that still have units and have an expiry date.
        /// </summary>
        public static DataTable GetBatches(string productId)
        {
            DataTable logs = Db.Query(
                @"SELECT product_id, movement_type, quantity, batch_no, expiry_date, created_at
                  FROM tbl_stock_log
                  WHERE (@p IS NULL OR product_id = @p)
                  ORDER BY product_id, created_at, log_id",
                Db.P("@p", productId));

            DataTable stock = Db.Query("SELECT product_id, quantity FROM tbl_stock WHERE (@p IS NULL OR product_id = @p)",
                Db.P("@p", productId));
            var onHand = new Dictionary<string, int>();
            foreach (DataRow r in stock.Rows) onHand[r["product_id"].ToString()] = Db.ToInt(r["quantity"]);

            var batchesByProduct = new Dictionary<string, List<Batch>>();
            var unassignedOut = new Dictionary<string, int>();
            var namedOut = new List<KeyValuePair<string, KeyValuePair<string, int>>>(); // product -> (batch, qty)

            foreach (DataRow r in logs.Rows)
            {
                string pid = r["product_id"].ToString();
                int q = Db.ToInt(r["quantity"]);
                bool isIn = r["movement_type"].ToString() == "IN";
                string bno = r["batch_no"] == DBNull.Value ? null : r["batch_no"].ToString();

                if (isIn)
                {
                    List<Batch> list;
                    if (!batchesByProduct.TryGetValue(pid, out list)) { list = new List<Batch>(); batchesByProduct[pid] = list; }
                    list.Add(new Batch
                    {
                        ProductId = pid,
                        BatchNo = bno,
                        Expiry = r["expiry_date"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["expiry_date"]),
                        Received = Convert.ToDateTime(r["created_at"]),
                        Remaining = q
                    });
                }
                else if (!string.IsNullOrEmpty(bno))
                {
                    namedOut.Add(new KeyValuePair<string, KeyValuePair<string, int>>(pid, new KeyValuePair<string, int>(bno, q)));
                }
                else
                {
                    int cur;
                    unassignedOut.TryGetValue(pid, out cur);
                    unassignedOut[pid] = cur + q;
                }
            }

            // 2. OUT rows that name a batch
            foreach (var n in namedOut)
            {
                List<Batch> list;
                if (!batchesByProduct.TryGetValue(n.Key, out list)) continue;
                int left = n.Value.Value;
                foreach (Batch b in list)
                {
                    if (left <= 0) break;
                    if (b.BatchNo != n.Value.Key) continue;
                    int take = Math.Min(left, b.Remaining);
                    b.Remaining -= take;
                    left -= take;
                }
                if (left > 0)
                {
                    int cur;
                    unassignedOut.TryGetValue(n.Key, out cur);
                    unassignedOut[n.Key] = cur + left;
                }
            }

            // 3. remaining OUT rows: oldest first
            foreach (var kv in unassignedOut)
            {
                List<Batch> list;
                if (!batchesByProduct.TryGetValue(kv.Key, out list)) continue;
                int left = kv.Value;
                foreach (Batch b in list)
                {
                    if (left <= 0) break;
                    int take = Math.Min(left, b.Remaining);
                    b.Remaining -= take;
                    left -= take;
                }
            }

            // 4. never report more than what tbl_stock says is on hand (newest batches are kept)
            foreach (var kv in batchesByProduct)
            {
                int have;
                onHand.TryGetValue(kv.Key, out have);
                int total = 0;
                foreach (Batch b in kv.Value) total += b.Remaining;
                int excess = total - have;
                foreach (Batch b in kv.Value)
                {
                    if (excess <= 0) break;
                    int cut = Math.Min(excess, b.Remaining);
                    b.Remaining -= cut;
                    excess -= cut;
                }
            }

            DataTable info = Db.Query(
                @"SELECT p.product_id, p.product_name, IFNULL(p.barcode,'') AS barcode, p.category_id, c.category_name,
                         p.supplier_id, p.cost_price, p.selling_price
                  FROM tbl_product p JOIN tbl_category c ON c.category_id = p.category_id
                  WHERE (@p IS NULL OR p.product_id = @p)",
                Db.P("@p", productId));

            var result = new DataTable();
            result.Columns.Add("product_id", typeof(string));
            result.Columns.Add("product_name", typeof(string));
            result.Columns.Add("barcode", typeof(string));
            result.Columns.Add("category_id", typeof(string));
            result.Columns.Add("category_name", typeof(string));
            result.Columns.Add("supplier_id", typeof(string));
            result.Columns.Add("batch_no", typeof(string));
            result.Columns.Add("expiry_date", typeof(DateTime));
            result.Columns.Add("remaining", typeof(int));
            result.Columns.Add("cost_price", typeof(decimal));
            result.Columns.Add("selling_price", typeof(decimal));

            foreach (DataRow p in info.Rows)
            {
                List<Batch> list;
                if (!batchesByProduct.TryGetValue(p["product_id"].ToString(), out list)) continue;
                foreach (Batch b in list)
                {
                    if (b.Remaining <= 0 || !b.Expiry.HasValue) continue;
                    result.Rows.Add(p["product_id"], p["product_name"], p["barcode"], p["category_id"], p["category_name"],
                        p["supplier_id"] == DBNull.Value ? "" : p["supplier_id"].ToString(),
                        b.BatchNo ?? "", b.Expiry.Value, b.Remaining, p["cost_price"], p["selling_price"]);
                }
            }
            return result;
        }

        // ------------------------------------------------------------------
        // Movement log
        // ------------------------------------------------------------------
        public static DataTable MovementLog(string productId, DateTime? from, DateTime? to, int limit)
        {
            string sql =
                @"SELECT l.log_id AS `Log ID`, l.created_at AS `Date`, l.product_id AS `Product ID`, p.product_name AS `Product`,
                         l.movement_type AS `Type`, l.reason AS `Reason`, l.quantity AS `Qty`,
                         IFNULL(l.batch_no,'') AS `Batch`, l.expiry_date AS `Expiry`, l.unit_cost AS `Unit Cost`,
                         IFNULL(l.reference_id,'') AS `Reference`, IFNULL(sp.supplier_name,'') AS `Supplier`,
                         IFNULL(a.acc_name,'') AS `Performed By`, IFNULL(l.remarks,'') AS `Remarks`
                  FROM tbl_stock_log l
                  JOIN tbl_product p ON p.product_id = l.product_id
                  LEFT JOIN tbl_supplier sp ON sp.supplier_id = l.supplier_id
                  LEFT JOIN tbl_account a ON a.acc_id = l.performed_by
                  WHERE (@pid IS NULL OR l.product_id = @pid)
                    AND (@f IS NULL OR l.created_at >= @f)
                    AND (@t IS NULL OR l.created_at < @t)
                  ORDER BY l.created_at DESC, l.log_id DESC
                  LIMIT " + Math.Max(1, limit);
            return Db.Query(sql,
                Db.P("@pid", productId),
                Db.P("@f", from.HasValue ? (object)from.Value.Date : null),
                Db.P("@t", to.HasValue ? (object)to.Value.Date.AddDays(1) : null));
        }
    }

    /// <summary>
    /// tbl_stock_log has no columns for damage type or disposition, so the
    /// Damage screen keeps them inside the remarks text in a fixed layout:
    ///   Type: Broken; Disposition: Pending; Details: cap cracked
    /// Older free-text remarks are read as type "Damaged", disposition "Pending".
    /// </summary>
    public static class DamageRemarks
    {
        private static readonly Regex Layout =
            new Regex(@"^Type:\s*(.*?);\s*Disposition:\s*(.*?);\s*Details:\s*(.*)$", RegexOptions.Singleline | RegexOptions.Compiled);

        private static string Clean(string s)
        {
            return (s ?? "").Replace(";", ",").Trim();
        }

        public static string Build(string type, string disposition, string details)
        {
            string head = "Type: " + Clean(type) + "; Disposition: " + Clean(disposition) + "; Details: ";
            string d = (details ?? "").Trim();
            int room = 255 - head.Length;
            if (d.Length > room) d = d.Substring(0, Math.Max(0, room));
            return head + d;
        }

        public static void Parse(string remarks, out string type, out string disposition, out string details)
        {
            type = "Damaged";
            disposition = "Pending";
            details = remarks ?? "";
            if (string.IsNullOrEmpty(remarks)) return;
            Match m = Layout.Match(remarks);
            if (!m.Success) return;
            type = m.Groups[1].Value.Trim().Length == 0 ? "Damaged" : m.Groups[1].Value.Trim();
            disposition = m.Groups[2].Value.Trim().Length == 0 ? "Pending" : m.Groups[2].Value.Trim();
            details = m.Groups[3].Value.Trim();
        }
    }
}
