function Invoke-UltimateScript {
    <#
    .SYNOPSIS
        Runs the CURRENT upstream FR33THY "Ultimate" script for a repo-relative
        path in an elevated PowerShell console (menu-driven, always latest).
    .DESCRIPTION
        Heavy / frequently-updated tweaks are not re-implemented inline in Akari.
        Instead they delegate to the live upstream script so they never need
        re-porting when Ultimate changes. To retarget in the future, change only
        the -Path passed by the button handler.
    .PARAMETER Path
        Repo-relative path, e.g. "8 Advanced/1 Defender.ps1". Spaces and symbols
        are URL-encoded per segment automatically.
    .PARAMETER Confirm
        Optional prompt text. When set, a Yes/No warning dialog is shown first and
        the script only runs on Yes.
    .PARAMETER Status
        Status-bar text shown while launching.
    #>
    param(
        [Parameter(Mandatory)][string]$Path,
        [string]$Confirm,
        [string]$Status = "Launching Ultimate script..."
    )

    if ($Confirm) {
        $r = [System.Windows.MessageBox]::Show($Confirm, "Akari Tool", "YesNo", "Warning")
        if ($r -ne "Yes") { return }
    }

    # URL-encode each path segment but keep the separators
    $enc = ($Path -split '/' | ForEach-Object { [uri]::EscapeDataString($_) }) -join '/'
    $url = "https://raw.githubusercontent.com/FR33THYFR33THY/Ultimate/main/$enc"

    Set-Status $Status "#AAAAAA"
    Start-Elevated -FilePath "powershell.exe" -ArgumentList "-NoProfile -ExecutionPolicy Bypass -Command `"& { iwr '$url' -UseBasicParsing | iex }`""
    Set-Status "Ultimate script launched (menu-driven console)." "#66BB6A"
}
