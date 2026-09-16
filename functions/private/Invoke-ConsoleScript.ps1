function Invoke-ConsoleScript {
    <#
    .SYNOPSIS
        Runs an EMBEDDED menu-driven script in an elevated PowerShell console.
        Fully self-contained — nothing on disk to carry, nothing downloaded.
    .DESCRIPTION
        Some tweaks are interactive menu-driven consoles that aren't worth
        re-implementing as native WPF flows (FR33THY's Ultimate menus, and your
        own scripts). Their .ps1 files live in assets/text/ and get baked into
        akari.ps1 as base64 at compile time. This helper decodes the chosen one
        to a temp file and launches it elevated.

        To add or update one: drop a file at assets/text/<name>.ps1, recompile,
        and call `Invoke-ConsoleScript -Asset "<name>"` from a button handler.
    .PARAMETER Asset
        Embedded asset key (the assets/text file's base name), e.g. "smtht".
    .PARAMETER Confirm
        Optional prompt text. When set, a Yes/No warning dialog is shown first and
        the script only runs on Yes.
    .PARAMETER Status
        Status-bar text shown while launching.
    #>
    param(
        [Parameter(Mandatory)][string]$Asset,
        [string]$Confirm,
        [string]$Status = "Launching script..."
    )

    if ($Confirm) {
        $r = [System.Windows.MessageBox]::Show($Confirm, "Akari Tool", "YesNo", "Warning")
        if ($r -ne "Yes") { return }
    }

    if (-not ($sync.assets -and $sync.assets.$Asset)) {
        Set-Status "Embedded script not found: $Asset" "#EF5350"
        [System.Windows.MessageBox]::Show(
            "Embedded script '$Asset' is missing. Recompile akari.ps1 after adding assets\text\$Asset.ps1.",
            "Akari Tool", "OK", "Error") | Out-Null
        return
    }

    # Decode the embedded script to a temp file, then launch it elevated
    $dest = Join-Path $env:SystemRoot "Temp\akari_$Asset.ps1"
    $text = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($sync.assets.$Asset))
    [IO.File]::WriteAllText($dest, $text.TrimStart([char]0xFEFF), (New-Object Text.UTF8Encoding($false)))

    Set-Status $Status "#AAAAAA"
    Start-Elevated -FilePath "powershell.exe" -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$dest`""
    Set-Status "Script launched (menu-driven console)." "#66BB6A"
}
