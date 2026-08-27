using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Win32;
using AkariTool.Services;
using AkariTool.Tabs;
using AkariTool.ViewModels.AkariOS;
using AkariTool.Core.Models.ShaderCache;
using WinUI.Framework.IoC;
using WinUI.Framework.Services;
using AkariTool.Core.Competitive;

namespace AkariTool.Views;

public sealed partial class AkariOSPage
{
    // ══════════════════════════════════════════════════════════════════════
    //  GAMING TWEAKS (toggles)  (net8 AkariOSTab.GamingTweaksCard.cs, ported near
    //  line-for-line)
    //
    //  Three toggle rows: Disable Preemption (NVIDIA), Disable HDCP, Network
    //  Optimization. Preemption/HDCP are native registry writes with read-back
    //  restore-on-build (SystemStateReader); Network Optimization extracts an embedded
    //  .bat that REBOOTS the machine, gated by the reboot confirm dialog. The restore
    //  reads set the toggle via a SUPPRESSING setter (BuildToggle), so page load never
    //  fires a write. Second AkariDialogs→TweakDialogs swap (arg order reversed).
    // ══════════════════════════════════════════════════════════════════════

    private readonly Dictionary<string, Action<bool>> _gamingSetters = new();

    private void BuildGamingToggleContent(StackPanel panel)
    {
        void AddRow(string name, string desc, Action<bool> apply, string? disabledNote = null)
        {
            var sep = panel.Children.Count > 1;   // > 1: card title header is child 0
            if (sep) panel.Children.Add(new Border { Background = Res("DividerStrokeColorDefaultBrush"), Height = 1, Margin = new Thickness(-20, 0, -20, 0) });

            var row = new Grid { Margin = new Thickness(0, 12, 0, 12) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var info = new StackPanel();
            Grid.SetColumn(info, 0);
            info.Children.Add(new TextBlock { Text = name, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = Res("TextFillColorPrimaryBrush") });
            info.Children.Add(new TextBlock { Text = desc, FontSize = 12, Foreground = Res("TextFillColorSecondaryBrush"), Margin = new Thickness(0, 2, 0, 0), TextWrapping = TextWrapping.Wrap });

            // Unavailable rows read like the AkariOS-only banner: a short reason
            // in the info tint, with the control disabled rather than hidden.
            if (disabledNote is not null)
                info.Children.Add(new TextBlock
                {
                    Text = disabledNote,
                    FontSize = 11.5,
                    Foreground = Hex("#4CC2FF"),
                    Margin = new Thickness(0, 4, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                });

            var (toggleCtrl, setter) = BuildToggle(apply);
            Grid.SetColumn(toggleCtrl, 1);
            _gamingSetters[name] = setter;

            if (disabledNote is not null)
            {
                // WinUI: IsEnabled lives on Control, not FrameworkElement.
                if (toggleCtrl is Control c) c.IsEnabled = false;
                info.Opacity = 0.6;
            }

            row.Children.Add(info);
            row.Children.Add(toggleCtrl);
            panel.Children.Add(row);
        }

        AddRow("Disable Preemption (NVIDIA)", "Disable GPU preemption for lower latency", SetPreemption);
        AddRow("Disable HDCP", "Disable HDCP for lower GPU overhead", SetHdcp);
        // The network batches ship inside the app as embedded resources, so this
        // row is always available — no C:\PostInstall payload check.
        AddRow("Network Optimization", "Apply AkariOS network driver tweaks", SetNetworkOptimization);

        // ── Restore from the REGISTRY, not from markers ───────────────────
        // Preemption and HDCP are native, so their real values are readable and
        // cannot go stale. A marker only records that the app once ran something;
        // a read records what the system actually says.
        void RestoreFromRead(string name, bool? state)
        {
            if (state == true && _gamingSetters.TryGetValue(name, out var s)) s(true);
        }

        RestoreFromRead("Disable Preemption (NVIDIA)", SystemStateReader.ReadPreemption());
        RestoreFromRead("Disable HDCP",                SystemStateReader.ReadHdcp());

        // Network Optimization still shells out to a batch, whose effects span NIC
        // driver properties and netsh global state — not meaningfully readable until
        // it is ported to native code. It keeps its marker until that port lands.
        if (TweakHelpers.HasState("NetworkOptimization") &&
            _gamingSetters.TryGetValue("Network Optimization", out var net))
            net(true);
    }

    // net8 TweakHelpers.BuildToggle — a ToggleSwitch whose setter sets IsOn WITHOUT
    // re-firing onToggle (the `suppress` flag). Load-bearing: restore-on-build uses
    // the setter, so reading current state never triggers a write. Ported verbatim
    // (build #3 has no TweakHelpers.BuildToggle — the old factory was replaced).
    private static (FrameworkElement Control, Action<bool> Setter) BuildToggle(Action<bool>? onToggle = null)
    {
        var toggle = new ToggleSwitch
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
            OnContent = null,
            OffContent = null,
            MinWidth = 0,
        };

        bool suppress = false;
        toggle.Toggled += (_, _) =>
        {
            if (suppress) return;
            onToggle?.Invoke(toggle.IsOn);
        };

        Action<bool> setter = state =>
        {
            if (toggle.IsOn == state) return;
            suppress = true;
            toggle.IsOn = state;
            suppress = false;
        };

        return (toggle, setter);
    }

    private void SetPreemption(bool disable)
    {
        try
        {
            if (disable)
            {
                if (TweakHelpers.HasState("DisablePreemption")) return;
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\GraphicsDrivers\Scheduler", "EnablePreemption", 0, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\nvlddmkm", "DisablePreemption", 1, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\nvlddmkm", "DisableCudaContextPreemption", 1, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\nvlddmkm", "EnableCEPreemption", 0, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\nvlddmkm", "DisablePreemptionOnS3S4", 1, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\nvlddmkm", "ComputePreemption", 0, RegistryValueKind.DWord);
                TweakHelpers.SaveState("DisablePreemption");
            }
            else
            {
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\GraphicsDrivers\Scheduler", "EnablePreemption", 1, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\nvlddmkm", "DisablePreemption", 0, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\nvlddmkm", "DisableCudaContextPreemption", 0, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\nvlddmkm", "EnableCEPreemption", 1, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\nvlddmkm", "DisablePreemptionOnS3S4", 0, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\nvlddmkm", "ComputePreemption", 1, RegistryValueKind.DWord);
                TweakHelpers.ClearState("DisablePreemption");
            }
            Service?.Log($"NVIDIA Preemption {(disable ? "disabled" : "enabled")}. Restart to apply.");
        }
        catch (Exception ex) { Service?.Log($"ERROR SetPreemption: {ex.Message}"); }
    }

    /// <summary>
    /// HDCP on/off, fully native — no C:\PostInstall dependency. Both directions
    /// write RMHdcpKeyglobZero across every enumerated adapter, so revert is a true
    /// inverse of apply. State is read back from the registry, not from a marker.
    /// </summary>
    private void SetHdcp(bool disable)
    {
        try
        {
            int written = GpuTweaks.SetHdcpDisabled(disable, m => Service?.Log(m));

            if (written <= 0)
            {
                Service?.Log($"HDCP {(disable ? "disable" : "enable")} failed — no adapters were written.");
                if (_gamingSetters.TryGetValue("Disable HDCP", out var set)) set(!disable);
                return;
            }

            Service?.Log($"HDCP {(disable ? "disabled" : "enabled")} on {written} adapter(s). Restart to apply.");
        }
        catch (Exception ex)
        {
            Service?.Log($"ERROR SetHdcp: {ex.Message}");
            if (_gamingSetters.TryGetValue("Disable HDCP", out var set)) set(!disable);
        }
    }

    /// <summary>
    /// Reboot warning for the network batches. net8 went through AkariDialogs; build #3
    /// uses TweakDialogs.ConfirmContentAsync (arg order reversed to title, content,
    /// primaryText). Wording and button labels unchanged.
    /// </summary>
    private async Task<bool> ConfirmNetworkRebootAsync(bool enable)
    {
        string what = enable
            ? "Applying AkariOS network tweaks"
            : "Reverting AkariOS network tweaks";

        return await _dialogs.ConfirmContentAsync(
            "This will restart your PC",
            new TextBlock
            {
                Text = $"{what} runs a script that restarts your computer immediately " +
                       "when it finishes. Save your work before continuing.\n\nContinue?",
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 440,
            },
            primaryText: enable ? "Restart and apply" : "Restart and revert");
    }

    // The batches ship inside the assembly (csproj embeds Scripts\Network\*.bat),
    // so the feature no longer depends on the C:\PostInstall payload being present.
    private const string NetworkApplyBat  = "network-apply.bat";
    private const string NetworkRevertBat = "network-revert.bat";

    /// <summary>
    /// Writes an embedded batch to a unique temp path and returns it. Resource
    /// lookup matches ToolService's .ps1 pattern — by name suffix, so it survives
    /// a root-namespace change. The caller deletes the file; the batch reboots the
    /// machine, so that cleanup is best-effort.
    /// </summary>
    private static async Task<string> ExtractNetworkBatAsync(string fileName)
    {
        var asm = typeof(AkariOSPage).Assembly;
        string suffix = $".Scripts.Network.{fileName}";

        string name = asm.GetManifestResourceNames()
                         .FirstOrDefault(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                      ?? throw new FileNotFoundException($"Embedded resource not found: {suffix}");

        string path = Path.Combine(Path.GetTempPath(), $"AkariTool-{Guid.NewGuid():N}-{fileName}");

        await using (var rs = asm.GetManifestResourceStream(name)!)
        await using (var fs = File.Create(path))
            await rs.CopyToAsync(fs);

        return path;
    }

    private void SetNetworkOptimization(bool enable) => _ = SetNetworkOptimizationAsync(enable);

    /// <summary>
    /// Applies or reverts the embedded network batch. The batch is extracted to temp
    /// and its exit code checked before any state is saved. Both batches end in
    /// `shutdown -r -t 01`, so the app warns (ConfirmNetworkRebootAsync) before running.
    /// </summary>
    private async Task SetNetworkOptimizationAsync(bool enable)
    {
        bool previous = !enable;   // the position to fall back to on any failure

        void Revert()
        {
            // BuildToggle's setter never re-fires the callback, so this cannot recurse.
            if (_gamingSetters.TryGetValue("Network Optimization", out var set)) set(previous);
        }

        string? bat = null;

        try
        {
            if (enable && TweakHelpers.HasState("NetworkOptimization")) return;

            // Both network batches end in `pause` + `shutdown -r -t 01`; with a redirected,
            // window-less runner `pause` hits EOF and the machine reboots ~1s later. So the
            // app must warn before running either.
            if (!await ConfirmNetworkRebootAsync(enable))
            {
                Service?.Log("Network Optimization cancelled — nothing was changed.");
                Revert();
                return;
            }

            bat = await ExtractNetworkBatAsync(enable ? NetworkApplyBat : NetworkRevertBat);

            // ToolService.RunProcess — UseShellExecute=false, stdout/stderr into the log,
            // awaited exit code.
            int exit = await Service!.RunProcess("cmd.exe", $"/c \"{bat}\"",
                timeoutMilliseconds: 120_000);

            if (exit != 0)
            {
                Service.Log($"Network Optimization {(enable ? "apply" : "revert")} failed (exit {exit}). Nothing was saved.");
                Revert();
                return;
            }

            // The tweak script now exits cleanly after applying (pause/shutdown removed — the
            // unattended `pause` hung RunProcess to its 120s timeout, so the reboot never ran
            // and the exit was always -1). Exit 0 now means the work actually completed → persist.
            if (enable) TweakHelpers.SaveState("NetworkOptimization");
            else        TweakHelpers.ClearState("NetworkOptimization");

            bool persisted = TweakHelpers.HasState("NetworkOptimization") == enable;
            if (!persisted)
                Service.Log("WARNING: Network Optimization state did not persist before the restart.");

            Service.Log($"Network Optimization {(enable ? "applied" : "reverted")}. Restarting…");

            // Reboot is now app-owned (was buried in the .bat's shutdown line, which never
            // reached it). /t 5 gives the marker write + temp-bat cleanup a margin before the
            // machine goes down.
            RunShellProcess("shutdown", "/r /t 5");
        }
        catch (Exception ex)
        {
            Service?.Log($"ERROR SetNetworkOptimization: {ex.Message}");
            Revert();
        }
        finally
        {
            // Temp bat is just the tweak script now (no reboot); safe to delete before the
            // app-scheduled reboot fires.
            try { if (bat is not null && File.Exists(bat)) File.Delete(bat); }
            catch (Exception ex) { Service?.Log($"Could not delete temporary batch: {ex.Message}"); }
        }
    }
}
