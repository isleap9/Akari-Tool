using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs
{
    public partial class CustomizeTab
    {
        // ── DESKTOP ▸ REGIONAL SETTINGS ──
        private void BuildDesktopRegional(StackPanel panel)
        {
            // ── Regional Settings ─────────────────────────────────────────────
            var regionSection = TweakHelpers.BuildSection(panel, "Regional Settings");

            AddRegionalDropdown(regionSection,
                id:    "region.firstDayOfWeek",
                name:  "First Day of Week",
                desc:  "Sets the day calendars and date pickers start on. Applies to new processes — sign out and back in to apply everywhere",
                value: "iFirstDayOfWeek",
                options: new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" },
                data:    new[] { "0", "1", "2", "3", "4", "5", "6" });

            AddRegionalDropdown(regionSection,
                id:    "region.measurement",
                name:  "Measurement System",
                desc:  "Sets the unit system Windows and apps use. Applies to new processes — sign out and back in to apply everywhere",
                value: "iMeasure",
                options: new[] { "Metric", "U.S. (Imperial)" },
                data:    new[] { "0", "1" });

            AddRegionalDropdown(regionSection,
                id:    "region.shortDate",
                name:  "Short Date Format",
                desc:  "Sets the short date pattern used across Windows. Applies to new processes — sign out and back in to apply everywhere",
                value: "sShortDate",
                options: new[] { "M/d/yyyy", "dd/MM/yyyy", "yyyy-MM-dd", "yyyy/MM/dd", "dd MMM yyyy" },
                data:    new[] { "M/d/yyyy", "dd/MM/yyyy", "yyyy-MM-dd", "yyyy/MM/dd", "dd MMM yyyy" });

            AddRegionalDropdown(regionSection,
                id:    "region.decimal",
                name:  "Decimal Symbol",
                desc:  "Sets the character used as the decimal separator. Applies to new processes — sign out and back in to apply everywhere",
                value: "sDecimal",
                options: new[] { ". (Period)", ", (Comma)" },
                data:    new[] { ".", "," });

            AddRegionalDropdown(regionSection,
                id:    "region.currencyDecimal",
                name:  "Currency Decimal Symbol",
                desc:  "Sets the character used as the decimal separator in currency amounts. Applies to new processes — sign out and back in to apply everywhere",
                value: "sMonDecimalSep",
                // Winhance explorer sMonDecimalSep also offers Space and Apostrophe;
                // this row is pinned to the two common options like region.decimal.
                options: new[] { ". (Period)", ", (Comma)" },
                data:    new[] { ".", "," });

            AddRegionalDropdown(regionSection,
                id:    "region.listSeparator",
                name:  "List Separator",
                desc:  "Sets the character that separates list items, e.g. in CSV exports. Applies to new processes — sign out and back in to apply everywhere",
                value: "sList",
                options: new[] { ", (Comma)", "; (Semicolon)" },
                data:    new[] { ",", ";" });
        }

        /// <summary>
        /// Adds one HKCU\Control Panel\International REG_SZ dropdown row. Pure
        /// preference — no Recommended/Windows Default badge, so neither
        /// IsRecommended nor IsDefault is set on any option. The current registry
        /// value is read on build; a value matching no option (custom locale) reads
        /// back as unknown and is never written unless the user picks an option.
        /// </summary>
        private void AddRegionalDropdown(
            StackPanel section, string id, string name, string desc,
            string value, string[] options, string[] data)
        {
            TweakHelpers.AddTweakRow(section, new TweakDefinition
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
                    Service?.Log($"[DESKTOP] {name} set to {options[idx]}.");
                },
            });
        }

        private const string InternationalKey = @"Control Panel\International";
    }
}
