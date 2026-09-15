# ── Scheduling tweaks (ported from AkariOS Companion) ──────────────────────────

# SvcHost Split Threshold — higher values group services into fewer svchost
# processes (less per-process overhead). Value written in KB.
function Invoke-BtnSvcHostApply {
    $vals = @(380000, 4194304, 8388608, 16777216, 33554432, 67108864, 134217728, 268435456)
    $i = [int]$sync.CboSvcHost.SelectedIndex
    if ($i -lt 0 -or $i -ge $vals.Count) { $i = 0 }
    try {
        New-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control" `
            -Name "SvcHostSplitThresholdInKB" -Value $vals[$i] -PropertyType DWord -Force -ErrorAction Stop | Out-Null
        Set-Status "SvcHost split threshold set to $($vals[$i]) KB. Restart to apply." "#66BB6A"
    } catch {
        Set-Status "Failed to set SvcHost threshold: $($_.Exception.Message)" "#EF5350"
    }
}

# Win32 Priority Separation — foreground/background CPU quantum. Value is decimal
# of the hex shown in the dropdown (26=38, 2A=42, 28=40, 16=22, 06=6).
# 26 (Hex) is Fr33thy Ultimate's recommendation and the default selection.
function Invoke-BtnPrioritySepApply {
    $vals = @(38, 42, 40, 22, 6)
    $i = [int]$sync.CboPrioritySep.SelectedIndex
    if ($i -lt 0 -or $i -ge $vals.Count) { $i = 0 }
    try {
        New-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\PriorityControl" `
            -Name "Win32PrioritySeparation" -Value $vals[$i] -PropertyType DWord -Force -ErrorAction Stop | Out-Null
        Set-Status ("Win32 Priority Separation set to 0x{0:X} ({0}). Restart to apply." -f $vals[$i]) "#66BB6A"
    } catch {
        Set-Status "Failed to set Win32 Priority Separation: $($_.Exception.Message)" "#EF5350"
    }
}
