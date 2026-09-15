function Start-Elevated {
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [string]$ArgumentList,
        [switch]$Wait
    )
    $isElevated = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]"Administrator")
    $params = @{ FilePath = $FilePath }
    if ($ArgumentList) { $params.ArgumentList = $ArgumentList }
    if ($Wait)         { $params.Wait = $true }
    if (-not $isElevated) { $params.Verb = "RunAs" }
    Start-Process @params
}
