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

# ── Build a searchable index of every card across all tabs (for global search) ─
function Get-ElementText([System.Windows.DependencyObject]$el) {
    $sb = New-Object System.Text.StringBuilder
    $stack = New-Object System.Collections.Stack
    $stack.Push($el)
    while ($stack.Count -gt 0) {
        $n = $stack.Pop()
        if ($n -is [System.Windows.Controls.TextBlock]) { [void]$sb.Append($n.Text); [void]$sb.Append(" ") }
        foreach ($c in [System.Windows.LogicalTreeHelper]::GetChildren($n)) {
            if ($c -is [System.Windows.DependencyObject]) { $stack.Push($c) }
        }
    }
    $sb.ToString()
}
$sync.CardIndex = New-Object System.Collections.ArrayList
foreach ($p in $panels) {
    $pl = $sync[$p]
    if ($pl -and $pl.Content -and $pl.Content.Children) {
        foreach ($child in $pl.Content.Children) {
            if ($child -is [System.Windows.Controls.Border]) {
                [void]$sync.CardIndex.Add([pscustomobject]@{ Panel = $p; Card = $child; Text = (Get-ElementText $child).ToLowerInvariant() })
            }
        }
    }
}

# ── Hamburger: toggle compact / expanded sidebar ─────────────────────────────
$sync.SidebarExpanded = $true
$navNames = @("NavCheck","NavRefresh","NavSetup","NavInstallers","NavGraphics","NavWindows","NavHardware","NavAdvanced")
if ($sync.NavHamburger) {
    $sync.NavHamburger.Add_Click({
        $sync.SidebarExpanded = -not $sync.SidebarExpanded
        $expanded = $sync.SidebarExpanded
        $col = $sync.window.FindName("SidebarCol")
        if ($col) { $col.Width = if ($expanded) { New-Object System.Windows.GridLength 210 } else { New-Object System.Windows.GridLength 56 } }
        $vis = if ($expanded) { [System.Windows.Visibility]::Visible } else { [System.Windows.Visibility]::Collapsed }
        if ($sync.SidebarSearch) { $sync.SidebarSearch.Visibility = $vis }
        if ($sync.SidebarFooter) { $sync.SidebarFooter.Visibility = $vis }
        foreach ($n in $navNames) {
            $rb = $sync[$n]
            if ($rb -and $rb.Template) {
                $t = $rb.Template.FindName("NavText", $rb)
                $c = $rb.Template.FindName("NavContent", $rb)
                if ($t) { $t.Visibility = $vis }
                if ($c) {
                    if ($expanded) {
                        $c.HorizontalAlignment = [System.Windows.HorizontalAlignment]::Left
                        $c.Margin = New-Object System.Windows.Thickness(10,0,0,0)
                    } else {
                        $c.HorizontalAlignment = [System.Windows.HorizontalAlignment]::Center
                        $c.Margin = New-Object System.Windows.Thickness(0)
                    }
                }
            }
        }
    }.GetNewClosure())
}

# ── Search: GLOBAL filter across every tab; jumps to the first tab with hits ──
if ($sync.SearchBox -and $sync.SearchPlaceholder) {
    $sync.SearchBox.Add_TextChanged({
        $q = $sync.SearchBox.Text
        $sync.SearchPlaceholder.Visibility =
            if ([string]::IsNullOrEmpty($q)) { [System.Windows.Visibility]::Visible } else { [System.Windows.Visibility]::Collapsed }
        $ql = $q.ToLowerInvariant()
        $matchPanels = New-Object System.Collections.Generic.HashSet[string]
        foreach ($item in $sync.CardIndex) {
            $match = ($ql -eq "") -or $item.Text.Contains($ql)
            $item.Card.Visibility = if ($match) { [System.Windows.Visibility]::Visible } else { [System.Windows.Visibility]::Collapsed }
            if ($match -and $ql -ne "") { [void]$matchPanels.Add($item.Panel) }
        }
        if ($ql -ne "") {
            $cur = $panels | Where-Object { $sync[$_].Visibility -eq [System.Windows.Visibility]::Visible } | Select-Object -First 1
            if (-not $matchPanels.Contains($cur)) {
                $target = $panels | Where-Object { $matchPanels.Contains($_) } | Select-Object -First 1
                if ($target) {
                    $navKey = ($navMap.GetEnumerator() | Where-Object { $_.Value -eq $target } | Select-Object -First 1).Key
                    if ($navKey -and $sync[$navKey]) { $sync[$navKey].IsChecked = $true }
                }
            }
        }
    }.GetNewClosure())
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
