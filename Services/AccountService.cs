using System.Management;

namespace AkariTool.Services
{
    /// <summary>
    /// Reads and writes the current local account's DISPLAY NAME (Win32_UserAccount.FullName).
    ///
    /// Replaces the PostInstall "Change Name" batch file, which ran:
    ///     net user Administrator /fullname:"%newName%"
    /// — except this targets the CURRENT user rather than a hardcoded Administrator.
    ///
    /// Display name only: this does NOT rename the account, does NOT move
    /// C:\Users\&lt;name&gt;, and does NOT change the login name.
    /// </summary>
    public static class AccountService
    {
        /// <summary>Current interactive user's account name, without domain prefix.</summary>
        public static string CurrentUserName()
        {
            var full = System.Security.Principal.WindowsIdentity.GetCurrent().Name; // DOMAIN\user
            int i = full.LastIndexOf('\\');
            return i >= 0 ? full[(i + 1)..] : full;
        }

        // Scoped to the local machine so a domain account with a matching name is never touched.
        private static string QueryForCurrentUser() =>
            $"SELECT * FROM Win32_UserAccount WHERE Name='{CurrentUserName().Replace("'", "''")}' AND LocalAccount=TRUE";

        /// <summary>Reads the current display name (FullName) of the current LOCAL account. Null on failure.</summary>
        public static string? GetDisplayName()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(QueryForCurrentUser());
                foreach (ManagementObject account in searcher.Get())
                {
                    using (account)
                    {
                        var name = account["FullName"] as string;
                        return string.IsNullOrWhiteSpace(name) ? null : name;
                    }
                }
                return null;
            }
            catch { return null; }
        }

        /// <summary>
        /// Sets the display name (FullName) of the CURRENT local account.
        /// Display name only — no account rename, no profile-folder move.
        /// </summary>
        public static bool SetDisplayName(string newName, Action<string> log)
        {
            if (string.IsNullOrWhiteSpace(newName))
            {
                log("Display name cannot be empty.");
                return false;
            }

            newName = newName.Trim();

            try
            {
                using var searcher = new ManagementObjectSearcher(QueryForCurrentUser());
                foreach (ManagementObject account in searcher.Get())
                {
                    using (account)
                    {
                        account["FullName"] = newName;
                        account.Put();
                        log($"Display name changed to '{newName}'. Sign out and back in to see it everywhere.");
                        return true;
                    }
                }

                log($"Could not find a local account named '{CurrentUserName()}'.");
                return false;
            }
            catch (Exception ex)
            {
                log($"ERROR changing display name: {ex.Message}");
                return false;
            }
        }
    }
}
