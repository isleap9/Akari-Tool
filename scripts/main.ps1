# ── Parse XAML ───────────────────────────────────────────────────────────────
try {
    $sync.window = [Windows.Markup.XamlReader]::Parse($inputXML)
} catch {
    [System.Windows.MessageBox]::Show("XAML Error: $_", "Akari Tool")
    exit
}

# Store every named control in $sync
([xml]$inputXML).SelectNodes("//*[@Name]") | ForEach-Object {
    $sync[$_.Name] = $sync.window.FindName($_.Name)
}

# ── Brand logo (decode embedded base64 → title bar image + window/taskbar icon) ─
function ConvertFrom-Base64Image([string]$b64) {
    $bytes  = [Convert]::FromBase64String($b64)
    $stream = New-Object System.IO.MemoryStream(,$bytes)
    $img    = New-Object System.Windows.Media.Imaging.BitmapImage
    $img.BeginInit()
    $img.CacheOption  = [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad
    $img.StreamSource = $stream
    $img.EndInit()
    $img.Freeze()
    return $img
}
if ($sync.assets -and $sync.assets.logo) {
    try {
        $logoImg = ConvertFrom-Base64Image $sync.assets.logo
        if ($sync.TitleLogo) { $sync.TitleLogo.Source = $logoImg }
        $sync.window.Icon = $logoImg
    } catch {}
}

# ── Mica + dark title bar on window load ─────────────────────────────────────
$sync.window.Add_Loaded({
    $helper = New-Object System.Windows.Interop.WindowInteropHelper($sync.window)
    $hwnd   = $helper.Handle

    try {
        # Dark title bar (DWMWA_USE_IMMERSIVE_DARK_MODE = 20)
        $dark = 1
        [DwmApi]::DwmSetWindowAttribute($hwnd, 20, [ref]$dark, 4) | Out-Null

        # Extend frame so Mica reaches the title bar
        $m = New-Object DwmApi+MARGINS
        $m.Left = -1; $m.Right = -1; $m.Top = -1; $m.Bottom = -1
        [DwmApi]::DwmExtendFrameIntoClientArea($hwnd, [ref]$m) | Out-Null

        # Mica Alt (DWMWA_SYSTEMBACKDROP_TYPE = 38, value 4 = MicaAlt / 2 = Mica)
        $mica = 2
        [DwmApi]::DwmSetWindowAttribute($hwnd, 38, [ref]$mica, 4) | Out-Null

        # Make the WPF background transparent so Mica shows through
        $sync.window.Background = [System.Windows.Media.Brushes]::Transparent
    } catch {
        # Mica unavailable (Win10 / older build) — keep fallback dark background from XAML
    }
})

# ── Navigation switching ──────────────────────────────────────────────────────
$panels = @(
    "PanelCheck", "PanelRefresh", "PanelSetup", "PanelInstallers",
    "PanelGraphics", "PanelWindows", "PanelHardware", "PanelAdvanced"
)

$navMap = @{
    NavCheck     = "PanelCheck"
    NavRefresh   = "PanelRefresh"
    NavSetup     = "PanelSetup"
    NavInstallers= "PanelInstallers"
    NavGraphics  = "PanelGraphics"
    NavWindows   = "PanelWindows"
    NavHardware  = "PanelHardware"
    NavAdvanced  = "PanelAdvanced"
}

foreach ($navName in $navMap.Keys) {
    $target = $navMap[$navName]
    $sync[$navName].Add_Checked({
        param($s, $e)
        $t = $navMap[$s.Name]
        $panels | ForEach-Object {
            $sync[$_].Visibility = [System.Windows.Visibility]::Collapsed
        }
        $sync[$t].Visibility = [System.Windows.Visibility]::Visible
    }.GetNewClosure())
}

# ── Wire all buttons to their Invoke-* functions ──────────────────────────────
$sync.Keys | Where-Object { $_ -like "Btn*" } | ForEach-Object {
    $btnName = $_
    if ($sync[$btnName] -and $sync[$btnName].GetType().Name -eq "Button") {
        $sync[$btnName].Add_Click({
            $fn = "Invoke-$($btnName)"
            if (Get-Command $fn -ErrorAction SilentlyContinue) {
                & $fn
            }
        }.GetNewClosure())
    }
}

# ── Status bar helper (call from runspaces via Dispatcher) ───────────────────
function Set-Status {
    param([string]$Text, [string]$Color = "White")
    $sync.window.Dispatcher.Invoke([action]{
        $sync.StatusText.Text       = $Text
        $sync.StatusText.Foreground = $Color
    }, "Normal")
}

# ── Show window ───────────────────────────────────────────────────────────────
$sync.window.ShowDialog() | Out-Null
