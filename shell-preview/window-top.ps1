# ─────────────────────────────────────────────────────────────────────────────
# Akari Tool — WINDOW TOP UI (WPF / PowerShell)
# Custom title bar (logo + AKARI TOOL wordmark) with working minimize / maximize
# / close caption buttons, plus the hamburger and "Search all settings..." bar.
# Look-only. Run:
#   powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\window-top.ps1"
# ─────────────────────────────────────────────────────────────────────────────

Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase

Add-Type -TypeDefinition @"
using System; using System.Runtime.InteropServices;
public class Dwm {
    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr h, int a, ref int v, int s);
    [DllImport("dwmapi.dll")] public static extern int DwmExtendFrameIntoClientArea(IntPtr h, ref MARGINS m);
    [StructLayout(LayoutKind.Sequential)] public struct MARGINS { public int L, R, T, B; }
}
"@

$xaml = @'
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Akari Tool" Width="960" Height="620"
        MinWidth="600" MinHeight="400"
        WindowStartupLocation="CenterScreen"
        Background="#1C1C1F"
        FontFamily="Segoe UI Variable Text, Segoe UI" FontSize="13"
        TextOptions.TextFormattingMode="Ideal" TextOptions.TextRenderingMode="ClearType">
    <WindowChrome.WindowChrome>
        <WindowChrome CaptionHeight="48" ResizeBorderThickness="6" GlassFrameThickness="-1" UseAeroCaptionButtons="False"/>
    </WindowChrome.WindowChrome>

    <Window.Resources>
        <SolidColorBrush x:Key="Bg"            Color="#1C1C1F"/>
        <SolidColorBrush x:Key="Accent"        Color="#E0142A"/>
        <SolidColorBrush x:Key="AccentText"    Color="#FF8A94"/>
        <SolidColorBrush x:Key="TextPrimary"   Color="#FFFFFF"/>
        <SolidColorBrush x:Key="TextSecondary" Color="#C6C6CA"/>
        <SolidColorBrush x:Key="TextTertiary"  Color="#8A8A90"/>
        <SolidColorBrush x:Key="SearchBg"      Color="#18FFFFFF"/>
        <SolidColorBrush x:Key="SearchBorder"  Color="#1FFFFFFF"/>
        <SolidColorBrush x:Key="NavHover"      Color="#12FFFFFF"/>
        <FontFamily x:Key="Icons">Segoe Fluent Icons, Segoe MDL2 Assets</FontFamily>

        <!-- Minimize / Maximize caption button -->
        <Style x:Key="Caption" TargetType="Button">
            <Setter Property="Width" Value="46"/><Setter Property="Height" Value="48"/>
            <Setter Property="Foreground" Value="{StaticResource TextSecondary}"/>
            <Setter Property="WindowChrome.IsHitTestVisibleInChrome" Value="True"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button">
                <Border x:Name="b" Background="Transparent"><TextBlock Text="{TemplateBinding Content}" FontFamily="{StaticResource Icons}" FontSize="10" Foreground="{TemplateBinding Foreground}" HorizontalAlignment="Center" VerticalAlignment="Center"/></Border>
                <ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="b" Property="Background" Value="#20FFFFFF"/></Trigger></ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
        </Style>
        <!-- Close caption button (red hover) -->
        <Style x:Key="CaptionClose" TargetType="Button" BasedOn="{StaticResource Caption}">
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button">
                <Border x:Name="b" Background="Transparent"><TextBlock x:Name="t" Text="{TemplateBinding Content}" FontFamily="{StaticResource Icons}" FontSize="10" Foreground="{TemplateBinding Foreground}" HorizontalAlignment="Center" VerticalAlignment="Center"/></Border>
                <ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="b" Property="Background" Value="#E0142A"/><Setter TargetName="t" Property="Foreground" Value="White"/></Trigger></ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
        </Style>
        <!-- Hamburger -->
        <Style x:Key="Hamburger" TargetType="Button">
            <Setter Property="Width" Value="40"/><Setter Property="Height" Value="36"/>
            <Setter Property="HorizontalAlignment" Value="Left"/><Setter Property="Cursor" Value="Hand"/>
            <Setter Property="WindowChrome.IsHitTestVisibleInChrome" Value="True"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button">
                <Border x:Name="b" Background="Transparent" CornerRadius="6"><TextBlock Text="&#xE700;" FontFamily="{StaticResource Icons}" FontSize="15" Foreground="{StaticResource TextSecondary}" HorizontalAlignment="Center" VerticalAlignment="Center"/></Border>
                <ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="b" Property="Background" Value="{StaticResource NavHover}"/></Trigger></ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
        </Style>
    </Window.Resources>

    <Grid Background="{StaticResource Bg}">
        <Grid.RowDefinitions><RowDefinition Height="48"/><RowDefinition Height="*"/></Grid.RowDefinitions>

        <!-- ══ TITLE BAR ══ -->
        <Grid Grid.Row="0">
            <!-- left: logo + accent dot + wordmark -->
            <StackPanel Orientation="Horizontal" VerticalAlignment="Center" Margin="14,0,0,0">
                <Image x:Name="LogoImg" Height="24" Stretch="Uniform" VerticalAlignment="Center" Margin="0,0,8,0" RenderOptions.BitmapScalingMode="HighQuality"/>
                <Ellipse Width="5" Height="5" VerticalAlignment="Center" Margin="0,0,10,0" Fill="{StaticResource Accent}"/>
                <TextBlock FontSize="10.5" FontWeight="Bold" VerticalAlignment="Center"><Run Text="AKARI " Foreground="{StaticResource TextPrimary}"/><Run Text="TOOL" Foreground="{StaticResource AccentText}"/></TextBlock>
            </StackPanel>
            <!-- right: caption buttons -->
            <StackPanel Orientation="Horizontal" HorizontalAlignment="Right" VerticalAlignment="Top">
                <Button x:Name="BtnMin"   Style="{StaticResource Caption}"      Content="&#xE921;"/>
                <Button x:Name="BtnMax"   Style="{StaticResource Caption}"      Content="&#xE922;"/>
                <Button x:Name="BtnClose" Style="{StaticResource CaptionClose}" Content="&#xE8BB;"/>
            </StackPanel>
        </Grid>

        <!-- ══ BODY: sidebar strip (hamburger + search) ══ -->
        <Grid Grid.Row="1">
            <Grid.ColumnDefinitions><ColumnDefinition Width="232"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
            <StackPanel Grid.Column="0" Margin="12,4,12,0">
                <Button x:Name="BtnHamburger" Style="{StaticResource Hamburger}" Margin="0,6,0,8"/>
                <Border Background="{StaticResource SearchBg}" BorderBrush="{StaticResource SearchBorder}" BorderThickness="1" CornerRadius="5" Height="34">
                    <Grid Margin="11,0,10,0">
                        <TextBox x:Name="SearchBox" Background="Transparent" BorderThickness="0" Foreground="{StaticResource TextPrimary}" CaretBrush="{StaticResource TextPrimary}" VerticalContentAlignment="Center" FontSize="12.5" VerticalAlignment="Center" Padding="0"/>
                        <TextBlock x:Name="SearchPlaceholder" Text="Search all settings..." FontSize="12.5" Foreground="{StaticResource TextTertiary}" VerticalAlignment="Center" IsHitTestVisible="False"/>
                        <TextBlock Text="&#xE721;" FontFamily="{StaticResource Icons}" FontSize="13" Foreground="{StaticResource TextTertiary}" HorizontalAlignment="Right" VerticalAlignment="Center"/>
                    </Grid>
                </Border>
            </StackPanel>
        </Grid>
    </Grid>
</Window>
'@

$window = [Windows.Markup.XamlReader]::Parse($xaml)

# logo (real AkariLogo.png from ..\assets)
$logoPath = Join-Path $PSScriptRoot '..\assets\AkariLogo.png'
if (Test-Path $logoPath) {
    $bi = New-Object System.Windows.Media.Imaging.BitmapImage
    $bi.BeginInit(); $bi.CacheOption = 'OnLoad'; $bi.UriSource = [Uri](Resolve-Path $logoPath).Path; $bi.EndInit(); $bi.Freeze()
    $window.FindName('LogoImg').Source = $bi
}

# caption buttons
$window.FindName('BtnClose').Add_Click({ $window.Close() })
$window.FindName('BtnMin').Add_Click({ $window.WindowState = 'Minimized' })
$max = $window.FindName('BtnMax')
$max.Add_Click({
    if ($window.WindowState -eq 'Maximized') { $window.WindowState = 'Normal'; $max.Content = [char]0xE922 }
    else { $window.WindowState = 'Maximized'; $max.Content = [char]0xE923 }
})

# search placeholder show/hide
$sb  = $window.FindName('SearchBox')
$ph  = $window.FindName('SearchPlaceholder')
$sb.Add_TextChanged({ $ph.Visibility = if ([string]::IsNullOrEmpty($sb.Text)) { 'Visible' } else { 'Collapsed' } })

# dark title bar + Mica
$window.Add_Loaded({
    try {
        $h = (New-Object System.Windows.Interop.WindowInteropHelper($window)).Handle
        $dark = 1; [Dwm]::DwmSetWindowAttribute($h, 20, [ref]$dark, 4) | Out-Null
        $m = New-Object Dwm+MARGINS; $m.L=-1;$m.R=-1;$m.T=-1;$m.B=-1; [Dwm]::DwmExtendFrameIntoClientArea($h, [ref]$m) | Out-Null
        $mica = 2; [Dwm]::DwmSetWindowAttribute($h, 38, [ref]$mica, 4) | Out-Null
        $window.Background = [System.Windows.Media.Brushes]::Transparent
    } catch { }
})

$window.ShowDialog() | Out-Null
