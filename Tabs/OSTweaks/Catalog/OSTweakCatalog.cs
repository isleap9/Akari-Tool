using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs.OSTweaks
{
    public static partial class OSTweakCatalog
    {
        private static int? ReadDword(RegistryHive hive, string subKey, string valueName)
        {
            try
            {
                using var k = RegistryKey.OpenBaseKey(hive, RegistryView.Default).OpenSubKey(subKey);
                return k?.GetValue(valueName) is int i ? i : (int?)null;
            }
            catch { return null; }
        }

        private static string? ReadString(RegistryHive hive, string subKey, string valueName)
        {
            try
            {
                using var k = RegistryKey.OpenBaseKey(hive, RegistryView.Default).OpenSubKey(subKey);
                return k?.GetValue(valueName) as string;
            }
            catch { return null; }
        }

    }
}
