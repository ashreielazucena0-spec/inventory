using System;

namespace Inventory_Management_System_with_POS
{
    /// <summary>Screens that can be restricted by account type.</summary>
    public enum Feature
    {
        Home, Dashboard, POS, Products, Stocks, Expired, Damage, Reports, Accounts
    }

    /// <summary>
    /// Holds the account that is currently logged in (tbl_account) and decides
    /// which screens that account type may open.
    /// </summary>
    public static class Session
    {
        public static string UserId { get; private set; }
        public static string UserName { get; private set; }
        public static string UserType { get; private set; }

        /// <summary>Set by Logout so the login form re-appears instead of closing the app.</summary>
        public static bool LogoutRequested { get; set; }

        public static bool IsLoggedIn { get { return !string.IsNullOrEmpty(UserId); } }
        public static bool IsAdmin { get { return UserType == "Admin"; } }
        public static bool IsManagerOrAdmin { get { return UserType == "Admin" || UserType == "Manager"; } }

        public static void Start(string id, string name, string type)
        {
            UserId = id;
            UserName = name;
            UserType = type;
        }

        public static void Clear()
        {
            UserId = null;
            UserName = null;
            UserType = null;
        }

        /// <summary>
        /// Admin: everything. Manager: everything except System Accounts.
        /// Cashier: Home, Dashboard, POS. Inventory Staff: Home, Dashboard,
        /// Products, Stocks, Expired Items, Damage.
        /// </summary>
        public static bool CanAccess(Feature f)
        {
            switch (UserType)
            {
                case "Admin":
                    return true;
                case "Manager":
                    return f != Feature.Accounts;
                case "Cashier":
                    return f == Feature.Home || f == Feature.Dashboard || f == Feature.POS;
                case "Inventory Staff":
                    return f == Feature.Home || f == Feature.Dashboard || f == Feature.Products
                        || f == Feature.Stocks || f == Feature.Expired || f == Feature.Damage;
                default:
                    return false;
            }
        }
    }
}
