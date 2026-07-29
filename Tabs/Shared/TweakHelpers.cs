using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs
{
    /// <summary>
    /// Shared UI helpers and registry state utilities for all tweak tabs.
    /// Replaces the partial-class helpers that used to live in TweakUIHelpers.cs
    /// and AkariOSTweaks.cs. No AkariOS dependency.
    /// </summary>
    public static partial class TweakHelpers
    {
        // ── Registry state key ────────────────────────────────────────────────
        // Stores a flag (1) for each tweak that has been applied so toggles
        // survive app restarts.

        private static RegistryKey? _stateKey;
        private static RegistryKey StateKey => _stateKey ??=
            Registry.CurrentUser.CreateSubKey(
                @"Software\AkariTool",
                RegistryKeyPermissionCheck.ReadWriteSubTree)
            ?? throw new InvalidOperationException("Failed to open AkariTool registry key.");

        public static void SaveState(string key) => StateKey.SetValue(key, 1);
        public static void ClearState(string key) => StateKey.DeleteValue(key, throwOnMissingValue: false);
        public static bool HasState(string key) => StateKey.GetValue(key) != null;

        // ── Real-HKCU helper (works even when the tool runs as admin) ─────────

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

        private static string GetRealUserSid()
        {
            var explorer = System.Diagnostics.Process
                .GetProcessesByName("explorer")
                .FirstOrDefault()
                ?? throw new InvalidOperationException("explorer.exe not found.");

            if (!OpenProcessToken(explorer.Handle, 8, out var token))
                throw new InvalidOperationException("Could not open explorer process token.");

            using var identity = new System.Security.Principal.WindowsIdentity(token);
            return identity.User!.Value;
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

        // ── Card brush / shadow helpers ───────────────────────────────────────

        // V3 flat premium: neutral opaque card surface. Returns the shared managed
        // brush so cards live-update on theme switch (was a per-call LinearGradientBrush).
        public static Brush CardBackground() => CardBg;

        // V3: cards carry a subtle NEUTRAL shadow only — no crimson tint, no glow.
        public static DropShadowEffect CardShadow() => new DropShadowEffect
        {
            Color = Color.FromRgb(0x00, 0x00, 0x00),
            BlurRadius = 14,
            ShadowDepth = 4,
            Direction = 270,
            Opacity = 0.28
        };


        // ── Brush helper ──────────────────────────────────────────────────────

        public static Brush BrushFrom(string color) =>
            ToolService.BrushFrom(color);

        // ── V3 design-token brushes ───────────────────────────────────────────
        // Each returns a persistent managed brush that live-updates on theme switch
        // (ThemeService mutates the shared instance's Colour in place). Keyed on the
        // Color-token so the switch resolves the right per-theme value.
        /// <summary>Shared managed brush for a Color- or Brush-token key; live-updates on switch.</summary>
        public static Brush Token(string colorKey) => ThemeService.ManagedBrush(colorKey);

        public static Brush CardBg          => ThemeService.ManagedBrush("AkariCardBackgroundColor");
        public static Brush CardBgHover     => ThemeService.ManagedBrush("AkariCardBackgroundHoverColor");
        public static Brush Hairline        => ThemeService.ManagedBrush("AkariHairlineColor");

        // Gradient elevation stroke for CARD + frame edges (lit top → shadow bottom).
        // Not for dividers or separators — those keep the flat Hairline.
        public static Brush CardElevationBorder => ThemeService.CardElevationBorder;
        public static Brush HairlineHover   => ThemeService.ManagedBrush("AkariHairlineHoverColor");
        public static Brush TextPrimary     => ThemeService.ManagedBrush("AkariTextPrimaryColor");
        public static Brush TextSecondary   => ThemeService.ManagedBrush("AkariTextSecondaryColor");
        public static Brush TextMuted       => ThemeService.ManagedBrush("AkariTextMutedColor");
        public static Brush IconNeutral     => ThemeService.ManagedBrush("AkariIconNeutralColor");
        // ── Radius tokens ─────────────────────────────────────────────────────
        // Mirrors AkariCardRadius / AkariControlRadius in AkariFluentTheme.xaml for
        // C#-built UI. Resolved from the dictionary so the two can never drift.
        public static CornerRadius CardRadius    => Radius("AkariCardRadius",    8);
        public static CornerRadius ControlRadius => Radius("AkariControlRadius", 4);

        private static CornerRadius Radius(string key, double fallback) =>
            Application.Current?.Resources[key] is CornerRadius c ? c : new CornerRadius(fallback);

        public static Brush Accent          => ThemeService.ManagedBrush("AkariAccentColor");

        // Body-size red. The brand accent only reaches ~3.3:1 on the dark canvas,
        // so anything at normal text size uses these instead.
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
