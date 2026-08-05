using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;          // Application, CornerRadius
using Microsoft.UI.Xaml.Media;    // Brush
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs
{
    /// <summary>
    /// Shared UI helpers and registry utilities for all tweak tabs.
    ///
    /// Registry state persistence (SaveState/HasState/ClearState) lives in the
    /// logic-only partial <c>TweakHelpers.State.cs</c> so the Services layer can use
    /// it without the UI factory. The design-token brush accessors below route
    /// through <see cref="ThemeService.ManagedBrush"/> for live theme switching.
    /// </summary>
    public static partial class TweakHelpers
    {
        // ── Real-HKCU helper (works even when the tool runs as admin) ─────────

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        private static string GetRealUserSid()
        {
            var explorer = System.Diagnostics.Process
                .GetProcessesByName("explorer")
                .FirstOrDefault()
                ?? throw new InvalidOperationException("explorer.exe not found.");

            if (!OpenProcessToken(explorer.Handle, 8, out var token))
                throw new InvalidOperationException("Could not open explorer process token.");

            try
            {
                using var identity = new System.Security.Principal.WindowsIdentity(token);
                return identity.User!.Value;
            }
            finally
            {
                CloseHandle(token);
            }
        }

        public static RegistryKey CreateRealHkcuSubKey(string subKey)
        {
            var sid = GetRealUserSid();
            var hku = RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Default);
            return hku.CreateSubKey($@"{sid}\{subKey}", writable: true)!;
        }

        // ── Simple command runner (best-effort, fire-and-forget) ──────────────

        public static void RunCommand(string exe, string args)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo(exe, args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                System.Diagnostics.Process.Start(psi)?.WaitForExit();
            }
            catch { /* best-effort */ }
        }

        // ── Card brush helper ─────────────────────────────────────────────────
        // V3 flat premium: neutral opaque card surface (live-updates on theme switch).
        public static Brush CardBackground() => CardBg;

        // NOTE(migration): CardShadow() (WPF DropShadowEffect) dropped — WinUI has no
        // Effect. Card shadows are re-added as ThemeShadow with the card builders in a
        // later tab batch. See MIGRATION_LOG.

        // ── Brush helper ──────────────────────────────────────────────────────

        public static Brush BrushFrom(string color) =>
            ToolService.BrushFrom(color);

        // ── V3 design-token brushes (live-updating via ThemeService.ManagedBrush) ──
        /// <summary>Shared managed brush for a Color- or Brush-token key; live-updates on switch.</summary>
        public static Brush Token(string colorKey) => ThemeService.ManagedBrush(colorKey);

        public static Brush CardBg          => ThemeService.ManagedBrush("AkariCardBackgroundColor");
        public static Brush CardBgHover     => ThemeService.ManagedBrush("AkariCardBackgroundHoverColor");
        public static Brush Hairline        => ThemeService.ManagedBrush("AkariHairlineColor");

        // Gradient elevation stroke for CARD + frame edges (lit top → shadow bottom).
        public static Brush CardElevationBorder => ThemeService.CardElevationBorder;
        public static Brush HairlineHover   => ThemeService.ManagedBrush("AkariHairlineHoverColor");
        public static Brush TextPrimary     => ThemeService.ManagedBrush("AkariTextPrimaryColor");
        public static Brush TextSecondary   => ThemeService.ManagedBrush("AkariTextSecondaryColor");
        public static Brush TextMuted       => ThemeService.ManagedBrush("AkariTextMutedColor");
        public static Brush IconNeutral     => ThemeService.ManagedBrush("AkariIconNeutralColor");

        // ── Radius tokens ─────────────────────────────────────────────────────
        public static CornerRadius CardRadius    => Radius("AkariCardRadius",    8);
        public static CornerRadius ControlRadius => Radius("AkariControlRadius", 4);

        private static CornerRadius Radius(string key, double fallback) =>
            Application.Current?.Resources[key] is CornerRadius c ? c : new CornerRadius(fallback);

        public static Brush Accent          => ThemeService.ManagedBrush("AkariAccentColor");

        // Body-size red (the brand accent is too low-contrast at text size).
        public static Brush AccentText      => ThemeService.ManagedBrush("AkariAccentTextColor");
        public static Brush AccentTextMuted => ThemeService.ManagedBrush("AkariAccentTextMutedColor");

        // Banner / status role brushes — live-updating, darkened per theme.
        public static Brush SuccessFg     => ThemeService.ManagedBrush("AkariSuccessFgColor");
        public static Brush SuccessBg     => ThemeService.ManagedBrush("AkariSuccessBgColor");
        public static Brush SuccessBorder => ThemeService.ManagedBrush("AkariSuccessBorderColor");
        public static Brush InfoFg        => ThemeService.ManagedBrush("AkariInfoFgColor");
        public static Brush InfoBg        => ThemeService.ManagedBrush("AkariInfoBgColor");
        public static Brush InfoBorder    => ThemeService.ManagedBrush("AkariInfoBorderColor");
        public static Brush WarnFg        => ThemeService.ManagedBrush("AkariWarnFgColor");
        public static Brush WarnBg        => ThemeService.ManagedBrush("AkariWarnBgColor");
        public static Brush WarnBorder    => ThemeService.ManagedBrush("AkariWarnBorderColor");
        public static Brush DangerFg      => ThemeService.ManagedBrush("AkariDangerFgColor");
        public static Brush DangerBg      => ThemeService.ManagedBrush("AkariDangerBgColor");
        public static Brush DangerBorder  => ThemeService.ManagedBrush("AkariDangerBorderColor");

        // Card-header pill buttons (neutral + accent sets) and icon fills.
        public static Brush PillNeutralBg     => ThemeService.ManagedBrush("AkariPillNeutralBgColor");
        public static Brush PillNeutralBorder => ThemeService.ManagedBrush("AkariPillNeutralBorderColor");
        public static Brush PillNeutralFg     => ThemeService.ManagedBrush("AkariPillNeutralFgColor");
        public static Brush PillAccentBg      => ThemeService.ManagedBrush("AkariPillAccentBgColor");
        public static Brush PillAccentBorder  => ThemeService.ManagedBrush("AkariPillAccentBorderColor");
        public static Brush PillAccentFg      => ThemeService.ManagedBrush("AkariPillAccentFgColor");
        public static Brush StarGold          => ThemeService.ManagedBrush("AkariStarGoldColor");
        public static Brush WinBlueIcon       => ThemeService.ManagedBrush("AkariWinBlueIconColor");

        // Per-row badge pill colours (border + text).
        public static Brush PillPreference  => ThemeService.ManagedBrush("AkariPillPreferenceColor");
        public static Brush PillRecommended => ThemeService.ManagedBrush("AkariPillRecommendedColor");
        public static Brush PillDefault     => ThemeService.ManagedBrush("AkariPillDefaultColor");
        public static Brush PillCustom      => ThemeService.ManagedBrush("AkariPillCustomColor");
        public static Brush PillGeneric     => ThemeService.ManagedBrush("AkariPillGenericColor");
    }
}
