using System.Linq;
using Microsoft.Win32;

namespace AkariTool.Tabs
{
    // MVVM PORT: extracted verbatim from net8 CustomizeTab.Desktop.Regional.cs.
    // Section "Regional Settings" — 6 HKCU\Control Panel\International REG_SZ dropdowns
    // (Ids "region.*", built by the MakeRegionalDropdown generator) plus the relocated
    // "os-set-utc" tweak. The net8 AddRegionalDropdown(void, called AddTweakRow) became
    // MakeRegionalDropdown returning a TweakDefinition; logic/Ids/registry are unchanged.
    public static partial class CustomizeTweaks
    {
        private const string InternationalKey = @"Control Panel\International";

        public static TweakDefinition[] RegionalSettings(Action<string> Log)
        {
            return new[]
            {
                MakeRegionalDropdown(Log,
                    id:    "region.firstDayOfWeek",
                    name:  "First Day of Week",
                    desc:  "Sets the day calendars and date pickers start on. Applies to new processes — sign out and back in to apply everywhere",
                    value: "iFirstDayOfWeek",
                    options: new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" },
                    data:    new[] { "0", "1", "2", "3", "4", "5", "6" }),

                MakeRegionalDropdown(Log,
                    id:    "region.measurement",
                    name:  "Measurement System",
                    desc:  "Sets the unit system Windows and apps use. Applies to new processes — sign out and back in to apply everywhere",
                    value: "iMeasure",
                    options: new[] { "Metric", "U.S. (Imperial)" },
                    data:    new[] { "0", "1" }),

                MakeRegionalDropdown(Log,
                    id:    "region.shortDate",
                    name:  "Short Date Format",
                    desc:  "Sets the short date pattern used across Windows. Applies to new processes — sign out and back in to apply everywhere",
                    value: "sShortDate",
                    options: new[] { "M/d/yyyy", "dd/MM/yyyy", "yyyy-MM-dd", "yyyy/MM/dd", "dd MMM yyyy" },
                    data:    new[] { "M/d/yyyy", "dd/MM/yyyy", "yyyy-MM-dd", "yyyy/MM/dd", "dd MMM yyyy" }),

                MakeRegionalDropdown(Log,
                    id:    "region.decimal",
                    name:  "Decimal Symbol",
                    desc:  "Sets the character used as the decimal separator. Applies to new processes — sign out and back in to apply everywhere",
                    value: "sDecimal",
                    options: new[] { ". (Period)", ", (Comma)" },
                    data:    new[] { ".", "," }),

                MakeRegionalDropdown(Log,
                    id:    "region.currencyDecimal",
                    name:  "Currency Decimal Symbol",
                    desc:  "Sets the character used as the decimal separator in currency amounts. Applies to new processes — sign out and back in to apply everywhere",
                    value: "sMonDecimalSep",
                    // Winhance explorer sMonDecimalSep also offers Space and Apostrophe;
                    // this row is pinned to the two common options like region.decimal.
                    options: new[] { ". (Period)", ", (Comma)" },
                    data:    new[] { ".", "," }),

                MakeRegionalDropdown(Log,
                    id:    "region.listSeparator",
                    name:  "List Separator",
                    desc:  "Sets the character that separates list items, e.g. in CSV exports. Applies to new processes — sign out and back in to apply everywhere",
                    value: "sList",
                    options: new[] { ", (Comma)", "; (Semicolon)" },
                    data:    new[] { ",", ";" }),

                // RELOCATED (WinUI migration): "os-set-utc" moved here verbatim from the
                // dead Tabs/OSTweaks/Catalog/OSTweakCatalog.Performance.cs so the tweak
                // stays reachable once OSTweaksTab is removed. Id, Name, Description,
                // registry path/value and semantics are unchanged. It sits with Regional
                // Settings because it governs how the hardware clock is interpreted.
                new TweakDefinition
                {
                    Id = "os-set-utc", Name = "Set Clock to UTC",
                    Description = "Stores the hardware clock as UTC — fixes time sync conflict when dual-booting with Linux",
                    Group = "Regional Settings",
                    IsPreference = true,
                    ReadState = () =>
                    {
                        var v = ReadDwordLm(@"SYSTEM\CurrentControlSet\Control\TimeZoneInformation", "RealTimeIsUniversal");
                        return v.HasValue ? v == 1 : false;
                    },
                    Apply = enable =>
                    {
                        if (enable)
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\TimeZoneInformation", "RealTimeIsUniversal", 1, RegistryValueKind.DWord);
                        else
                            Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\TimeZoneInformation", true)
                                ?.DeleteValue("RealTimeIsUniversal", throwOnMissingValue: false);
                        Log($"UTC clock {(enable ? "enabled" : "disabled")}. Restart to apply.");
                    }
                },
            };
        }

        /// <summary>HKLM DWORD read (mirrors the helper the OSTweaks catalog used).</summary>
        private static int? ReadDwordLm(string subKey, string valueName)
        {
            try
            {
                using var k = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default).OpenSubKey(subKey);
                return k?.GetValue(valueName) is int i ? i : null;
            }
            catch { return null; }
        }

        /// <summary>
        /// Builds one HKCU\Control Panel\International REG_SZ dropdown row. Pure
        /// preference — no Recommended/Windows Default badge, so neither IsRecommended
        /// nor IsDefault is set on any option. A value matching no option (custom locale)
        /// reads back as -1 (unselected) and is never written unless the user picks one.
        /// </summary>
        private static TweakDefinition MakeRegionalDropdown(
            Action<string> Log, string id, string name, string desc,
            string value, string[] options, string[] data) => new TweakDefinition
        {
            Id           = id,
            Name         = name,
            Description  = desc,
            Group        = "Regional Settings",
            InputKind    = TweakInputKind.Dropdown,
            IsPreference = true,
            Options      = options.Select((label, i) => new TweakDropdownOption(label, data[i])).ToArray(),
            ReadCurrentIndex = () =>
            {
                // -1 = "matches no option" (value absent, or a custom locale such
                // as an Italian sShortDate we don't list). The row renders
                // unselected; nothing is written until the user picks.
                var cur = SystemStateReader.ReadInternationalValue(value);
                if (cur is null) return -1;
                var idx = Array.FindIndex(data, d => string.Equals(d, cur, StringComparison.Ordinal));
                return idx >= 0 ? idx : -1;
            },
            ApplyIndex = idx =>
            {
                Registry.SetValue($@"HKEY_CURRENT_USER\{InternationalKey}", value, data[idx], RegistryValueKind.String);
                Log($"[DESKTOP] {name} set to {options[idx]}.");
            },
        };
    }
}
