# ─────────────────────────────────────────────────────────────────────────────
# Akari Tool — SHELL PREVIEW (WPF / PowerShell)
#
# A standalone, look-only recreation of the Akari Tool WinUI 3 shell:
# title bar (vector logo + AKARI TOOL wordmark + accent dot), grouped sidebar
# with badges, Home dashboard (live system-info card, global search, category
# cards) and the footer. NOTHING here is wired to real tweak logic — it is the
# visual shell only. Palette/logo taken from the original App.xaml (crimson
# #E0142A). Run it: right-click → Run with PowerShell, or:
#   powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\shell-preview.ps1"
# ─────────────────────────────────────────────────────────────────────────────

Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Windows.Forms

# DWM P/Invoke — dark title bar + Mica backdrop on Windows 11 (best-effort).
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class Dwm {
    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr h, int a, ref int v, int s);
    [DllImport("dwmapi.dll")] public static extern int DwmExtendFrameIntoClientArea(IntPtr h, ref MARGINS m);
    [StructLayout(LayoutKind.Sequential)] public struct MARGINS { public int L, R, T, B; }
}
"@

$xaml = @'
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Akari Tool" Width="1380" Height="900"
        MinWidth="1040" MinHeight="640"
        WindowStartupLocation="CenterScreen"
        Background="#1C1C1F"
        FontFamily="Segoe UI Variable Text, Segoe UI" FontSize="13"
        TextOptions.TextFormattingMode="Ideal" TextOptions.TextRenderingMode="ClearType">
    <WindowChrome.WindowChrome>
        <WindowChrome CaptionHeight="44" ResizeBorderThickness="6" GlassFrameThickness="-1" UseAeroCaptionButtons="False"/>
    </WindowChrome.WindowChrome>

    <Window.Resources>
        <SolidColorBrush x:Key="Bg"            Color="#1C1C1F"/>
        <SolidColorBrush x:Key="CardBg"        Color="#2A2A2E"/>
        <SolidColorBrush x:Key="CardBorder"    Color="#1FFFFFFF"/>
        <SolidColorBrush x:Key="NavHover"      Color="#12FFFFFF"/>
        <SolidColorBrush x:Key="NavSelected"   Color="#1EFFFFFF"/>
        <SolidColorBrush x:Key="Accent"        Color="#E0142A"/>
        <SolidColorBrush x:Key="AccentText"    Color="#FF8A94"/>
        <SolidColorBrush x:Key="TextPrimary"   Color="#FFFFFF"/>
        <SolidColorBrush x:Key="TextSecondary" Color="#C6C6CA"/>
        <SolidColorBrush x:Key="TextTertiary"  Color="#84848A"/>
        <SolidColorBrush x:Key="SearchBg"      Color="#18FFFFFF"/>
        <SolidColorBrush x:Key="Good"          Color="#3FB27F"/>
        <FontFamily x:Key="Icons">Segoe Fluent Icons, Segoe MDL2 Assets</FontFamily>

        <Style x:Key="NavHeader" TargetType="TextBlock">
            <Setter Property="FontSize" Value="10.5"/><Setter Property="FontWeight" Value="SemiBold"/>
            <Setter Property="Foreground" Value="{StaticResource TextTertiary}"/><Setter Property="Margin" Value="14,14,0,4"/>
        </Style>
        <!-- Sidebar nav row (hover highlight) -->
        <Style x:Key="NavRow" TargetType="Border">
            <Setter Property="Height" Value="36"/><Setter Property="CornerRadius" Value="6"/>
            <Setter Property="Background" Value="Transparent"/><Setter Property="Cursor" Value="Hand"/>
            <Style.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter Property="Background" Value="{StaticResource NavHover}"/></Trigger></Style.Triggers>
        </Style>
        <!-- Home card (hover highlight) -->
        <Style x:Key="Card" TargetType="Border">
            <Setter Property="Background" Value="{StaticResource CardBg}"/><Setter Property="BorderBrush" Value="{StaticResource CardBorder}"/>
            <Setter Property="BorderThickness" Value="1"/><Setter Property="CornerRadius" Value="10"/>
            <Setter Property="Padding" Value="16,14,14,14"/><Setter Property="Cursor" Value="Hand"/>
            <Style.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter Property="Background" Value="#33FFFFFF"/></Trigger></Style.Triggers>
        </Style>
        <!-- Caption button -->
        <Style x:Key="Caption" TargetType="Button">
            <Setter Property="Background" Value="Transparent"/><Setter Property="BorderThickness" Value="0"/>
            <Setter Property="Foreground" Value="{StaticResource TextSecondary}"/><Setter Property="Width" Value="46"/>
            <Setter Property="WindowChrome.IsHitTestVisibleInChrome" Value="True"/>
            <Setter Property="FontFamily" Value="{StaticResource Icons}"/><Setter Property="FontSize" Value="10"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button">
                <Border x:Name="b" Background="{TemplateBinding Background}"><TextBlock Text="{TemplateBinding Content}" FontFamily="{StaticResource Icons}" FontSize="10" Foreground="{TemplateBinding Foreground}" HorizontalAlignment="Center" VerticalAlignment="Center"/></Border>
                <ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="b" Property="Background" Value="#22FFFFFF"/></Trigger></ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
        </Style>
    </Window.Resources>

    <Grid Background="{StaticResource Bg}">
        <Grid.RowDefinitions>
            <RowDefinition Height="44"/><RowDefinition Height="*"/><RowDefinition Height="30"/>
        </Grid.RowDefinitions>

        <!-- ══ TITLE BAR ══ -->
        <Grid Grid.Row="0">
            <StackPanel Orientation="Horizontal" VerticalAlignment="Center" Margin="16,0,0,0">
                <Viewbox Height="16" Stretch="Uniform" VerticalAlignment="Center" Margin="0,0,8,0">
                    <Canvas Width="634" Height="639">
                        <Path Fill="{StaticResource TextPrimary}" Data="M 402.933333333,90.2666666666 c -8.13333333333,16.8 -29.3333333333,59.7333333333 -47.0666666667,95.7333333333 c -35.3333333333,71.8666666666 -61.3333333333,124.533333333 -81.2,165.333333333 c -7.2,14.6666666667 -20.9333333333,42.8 -30.6666666667,62.6666666667 c -28,57.0666666667 -48.6666666667,99.3333333333 -62,126.666666667 c -6.8,13.8666666667 -29.8666666667,61.0666666667 -51.3333333333,104.666666667 c -21.4666666667,43.6 -46.4,94.5333333333 -55.4666666667,113.066666667 l -16.4,33.6 l 57.6,0 l 57.6,0 l 32.6666666667,-67.0666666666 c 17.8666666667,-36.8 74,-149.866666667 124.666666667,-251.2 l 92,-184.4 l 41.7333333333,83.0666666666 c 77.4666666666,154.266666667 83.0666666666,165.2 84.9333333333,164.4 c 0.933333333333,-0.266666666667 7.6,-5.06666666667 14.9333333333,-10.4 c 7.2,-5.46666666667 24.6666666667,-18.5333333333 38.6666666667,-29.0666666667 l 25.7333333333,-19.0666666667 l -64.1333333333,-127.466666667 c -108.533333333,-215.466666667 -145.6,-288.8 -146.533333333,-289.866666667 c -0.4,-0.533333333333 -7.46666666666,12.6666666667 -15.7333333333,29.3333333333 z"/>
                        <Path Fill="{StaticResource Accent}" Data="M 630.666666667,540.266666667 c -43.8666666667,33.7333333333 -118.933333333,92.1333333333 -146.533333333,113.733333333 c -15.8666666667,12.5333333333 -46.5333333333,36.5333333333 -68.1333333333,53.4666666667 c -79.2,61.8666666667 -102.533333333,80.1333333333 -105.866666667,83.3333333333 c -0.8,0.666666666667 23.8666666667,1.2 58.6666666667,1.2 l 60,0 l 18.5333333333,-14.2666666667 c 10.2666666667,-7.73333333333 44.1333333333,-33.3333333333 75.3333333333,-56.9333333333 c 31.2,-23.4666666667 62.9333333333,-47.4666666667 70.6666666666,-53.4666666667 c 7.73333333333,-5.86666666667 14.6666666667,-11.2 15.4666666667,-11.7333333333 c 1.46666666667,-0.8 14.8,24.1333333333 60.8,113.333333333 l 11.8666666667,23.0666666667 l 52.6666666667,0 c 29.0666666667,0 52.5333333333,-0.533333333333 52.2666666667,-1.06666666667 c -2.4,-5.86666666667 -131.733333333,-261.6 -133.6,-264.4 c -1.33333333333,-1.86666666667 -4.26666666667,-0.133333333333 -22.1333333333,13.7333333333 z"/>
                    </Canvas>
                </Viewbox>
                <Ellipse Width="5" Height="5" VerticalAlignment="Center" Margin="0,0,9,0" Fill="{StaticResource Accent}"/>
                <TextBlock FontSize="11" FontWeight="Bold" VerticalAlignment="Center"><Run Text="AKARI " Foreground="{StaticResource TextPrimary}"/><Run Text="TOOL" Foreground="{StaticResource AccentText}"/></TextBlock>
            </StackPanel>
            <StackPanel Orientation="Horizontal" HorizontalAlignment="Right" VerticalAlignment="Stretch">
                <Button x:Name="BtnMin"   Style="{StaticResource Caption}" Content="&#xE921;"/>
                <Button x:Name="BtnMax"   Style="{StaticResource Caption}" Content="&#xE922;"/>
                <Button x:Name="BtnClose" Style="{StaticResource Caption}" Content="&#xE8BB;"/>
            </StackPanel>
        </Grid>

        <!-- ══ BODY ══ -->
        <Grid Grid.Row="1">
            <Grid.ColumnDefinitions><ColumnDefinition Width="232"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>

            <!-- SIDEBAR -->
            <ScrollViewer Grid.Column="0" VerticalScrollBarVisibility="Hidden">
                <StackPanel Margin="12,4,12,10">
                    <TextBlock Text="&#xE700;" FontFamily="{StaticResource Icons}" FontSize="15" Foreground="{StaticResource TextSecondary}" Margin="6,6,0,8"/>
                    <Border Background="{StaticResource SearchBg}" CornerRadius="5" Height="34" Margin="0,0,0,10">
                        <Grid Margin="10,0,8,0">
                            <TextBlock Text="Search all settings..." FontSize="12.5" Foreground="{StaticResource TextTertiary}" VerticalAlignment="Center"/>
                            <TextBlock Text="&#xE721;" FontFamily="{StaticResource Icons}" FontSize="13" Foreground="{StaticResource TextTertiary}" HorizontalAlignment="Right" VerticalAlignment="Center"/>
                        </Grid>
                    </Border>

                    <Border Background="{StaticResource NavSelected}" CornerRadius="6" Height="38">
                        <Grid>
                            <Border Width="3" Height="18" CornerRadius="2" HorizontalAlignment="Left" VerticalAlignment="Center" Background="{StaticResource Accent}"/>
                            <StackPanel Orientation="Horizontal" VerticalAlignment="Center" Margin="14,0,0,0">
                                <TextBlock Text="&#xE80F;" FontFamily="{StaticResource Icons}" FontSize="15" Foreground="{StaticResource TextPrimary}" Width="26" VerticalAlignment="Center"/>
                                <TextBlock Text="Home" FontSize="13" Foreground="{StaticResource TextPrimary}" VerticalAlignment="Center"/>
                            </StackPanel>
                        </Grid>
                    </Border>

                    <TextBlock Text="SOFTWARE" Style="{StaticResource NavHeader}"/>
                    <Border Style="{StaticResource NavRow}"><StackPanel Orientation="Horizontal" VerticalAlignment="Center" Margin="14,0,0,0"><TextBlock Text="&#xE8FD;" FontFamily="{StaticResource Icons}" FontSize="15" Foreground="{StaticResource TextSecondary}" Width="26"/><TextBlock Text="Windows Apps" FontSize="13" Foreground="{StaticResource TextSecondary}" VerticalAlignment="Center"/></StackPanel></Border>
                    <Border Style="{StaticResource NavRow}"><StackPanel Orientation="Horizontal" VerticalAlignment="Center" Margin="14,0,0,0"><TextBlock Text="&#xE896;" FontFamily="{StaticResource Icons}" FontSize="15" Foreground="{StaticResource TextSecondary}" Width="26"/><TextBlock Text="External Apps" FontSize="13" Foreground="{StaticResource TextSecondary}" VerticalAlignment="Center"/></StackPanel></Border>
                    <Border Style="{StaticResource NavRow}"><StackPanel Orientation="Horizontal" VerticalAlignment="Center" Margin="14,0,0,0"><TextBlock Text="&#xE74D;" FontFamily="{StaticResource Icons}" FontSize="15" Foreground="{StaticResource TextSecondary}" Width="26"/><TextBlock Text="Debloat" FontSize="13" Foreground="{StaticResource TextSecondary}" VerticalAlignment="Center"/></StackPanel></Border>

                    <TextBlock Text="OPTIMIZE" Style="{StaticResource NavHeader}"/>
                    <Border Style="{StaticResource NavRow}"><StackPanel Orientation="Horizontal" VerticalAlignment="Center" Margin="14,0,0,0"><TextBlock Text="&#xE735;" FontFamily="{StaticResource Icons}" FontSize="15" Foreground="{StaticResource Accent}" Width="26"/><TextBlock Text="AkariOS" FontSize="13" Foreground="{StaticResource TextSecondary}" VerticalAlignment="Center"/></StackPanel></Border>
                    <Border Style="{StaticResource NavRow}"><Grid><StackPanel Orientation="Horizontal" VerticalAlignment="Center" Margin="14,0,0,0"><TextBlock Text="&#xE7FC;" FontFamily="{StaticResource Icons}" FontSize="15" Foreground="{StaticResource TextSecondary}" Width="26"/><TextBlock Text="Gaming &amp; Performance" FontSize="13" Foreground="{StaticResource TextSecondary}" VerticalAlignment="Center"/></StackPanel><Border Background="{StaticResource Accent}" CornerRadius="9" MinWidth="18" Height="18" HorizontalAlignment="Right" VerticalAlignment="Center" Margin="0,0,4,0" Padding="5,0"><TextBlock Text="12" FontSize="11" FontWeight="SemiBold" Foreground="White" HorizontalAlignment="Center" VerticalAlignment="Center"/></Border></Grid></Border>
                    <Border Style="{StaticResource NavRow}"><Grid><StackPanel Orientation="Horizontal" VerticalAlignment="Center" Margin="14,0,0,0"><TextBlock Text="&#xE72E;" FontFamily="{StaticResource Icons}" FontSize="15" Foreground="{StaticResource TextSecondary}" Width="26"/><TextBlock Text="Privacy &amp; Security" FontSize="13" Foreground="{StaticResource TextSecondary}" VerticalAlignment="Center"/></StackPanel><Border Background="{StaticResource Accent}" CornerRadius="9" MinWidth="18" Height="18" HorizontalAlignment="Right" VerticalAlignment="Center" Margin="0,0,4,0" Padding="5,0"><TextBlock Text="12" FontSize="11" FontWeight="SemiBold" Foreground="White" HorizontalAlignment="Center" VerticalAlignment="Center"/></Border></Grid></Border>
                    <Border Style="{StaticResource NavRow}"><Grid><StackPanel Orientation="Horizontal" VerticalAlignment="Center" Margin="14,0,0,0"><TextBlock Text="&#xE895;" FontFamily="{StaticResource Icons}" FontSize="15" Foreground="{StaticResource TextSecondary}" Width="26"/><TextBlock Text="Windows Updates" FontSize="13" Foreground="{StaticResource TextSecondary}" VerticalAlignment="Center"/></StackPanel><Border Background="{StaticResource Accent}" CornerRadius="9" MinWidth="18" Height="18" HorizontalAlignment="Right" VerticalAlignment="Center" Margin="0,0,4,0" Padding="5,0"><TextBlock Text="2" FontSize="11" FontWeight="SemiBold" Foreground="White" HorizontalAlignment="Center" VerticalAlignment="Center"/></Border></Grid></Border>
                    <Border Style="{StaticResource NavRow}"><Grid><StackPanel Orientation="Horizontal" VerticalAlignment="Center" Margin="14,0,0,0"><TextBlock Text="&#xE7E7;" FontFamily="{StaticResource Icons}" FontSize="15" Foreground="{StaticResource TextSecondary}" Width="26"/><TextBlock Text="Notifications" FontSize="13" Foreground="{StaticResource TextSecondary}" VerticalAlignment="Center"/></StackPanel><Border Background="{StaticResource Accent}" CornerRadius="9" MinWidth="18" Height="18" HorizontalAlignment="Right" VerticalAlignment="Center" Margin="0,0,4,0" Padding="5,0"><TextBlock Text="3" FontSize="11" FontWeight="SemiBold" Foreground="White" HorizontalAlignment="Center" VerticalAlignment="Center"/></Border></Grid></Border>
                    <Border Style="{StaticResource NavRow}"><StackPanel Orientation="Horizontal" VerticalAlignment="Center" Margin="14,0,0,0"><TextBlock Text="&#xE767;" FontFamily="{StaticResource Icons}" FontSize="15" Foreground="{StaticResource TextSecondary}" Width="26"/><TextBlock Text="Sound" FontSize="13" Foreground="{StaticResource TextSecondary}" VerticalAlignment="Center"/></StackPanel></Border>
                    <Border Style="{StaticResource NavRow}"><Grid><StackPanel Orientation="Horizontal" VerticalAlignment="Center" Margin="14,0,0,0"><TextBlock Text="&#xE945;" FontFamily="{StaticResource Icons}" FontSize="15" Foreground="{StaticResource TextSecondary}" Width="26"/><TextBlock Text="Power" FontSize="13" Foreground="{StaticResource TextSecondary}" VerticalAlignment="Center"/></StackPanel><Border Background="{StaticResource Accent}" CornerRadius="9" MinWidth="18" Height="18" HorizontalAlignment="Right" VerticalAlignment="Center" Margin="0,0,4,0" Padding="5,0"><TextBlock Text="3" FontSize="11" FontWeight="SemiBold" Foreground="White" HorizontalAlignment="Center" VerticalAlignment="Center"/></Border></Grid></Border>
                    <Border Style="{StaticResource NavRow}"><StackPanel Orientation="Horizontal" VerticalAlignment="Center" Margin="14,0,0,0"><TextBlock Text="&#xE790;" FontFamily="{StaticResource Icons}" FontSize="15" Foreground="{StaticResource TextSecondary}" Width="26"/><TextBlock Text="Customize" FontSize="13" Foreground="{StaticResource TextSecondary}" VerticalAlignment="Center"/></StackPanel></Border>
                    <Border Style="{StaticResource NavRow}"><StackPanel Orientation="Horizontal" VerticalAlignment="Center" Margin="14,0,0,0"><TextBlock Text="&#xE90F;" FontFamily="{StaticResource Icons}" FontSize="15" Foreground="{StaticResource TextSecondary}" Width="26"/><TextBlock Text="Tools" FontSize="13" Foreground="{StaticResource TextSecondary}" VerticalAlignment="Center"/></StackPanel></Border>

                    <TextBlock Text="ADVANCED" Style="{StaticResource NavHeader}"/>
                    <Border Style="{StaticResource NavRow}"><StackPanel Orientation="Horizontal" VerticalAlignment="Center" Margin="14,0,0,0"><TextBlock Text="&#xEC7A;" FontFamily="{StaticResource Icons}" FontSize="15" Foreground="{StaticResource TextSecondary}" Width="26"/><TextBlock Text="Advanced Tools" FontSize="13" Foreground="{StaticResource TextSecondary}" VerticalAlignment="Center"/></StackPanel></Border>
                    <Border Style="{StaticResource NavRow}"><StackPanel Orientation="Horizontal" VerticalAlignment="Center" Margin="14,0,0,0"><TextBlock Text="&#xE777;" FontFamily="{StaticResource Icons}" FontSize="15" Foreground="{StaticResource TextSecondary}" Width="26"/><TextBlock Text="Backup &amp; Restore" FontSize="13" Foreground="{StaticResource TextSecondary}" VerticalAlignment="Center"/></StackPanel></Border>
                    <Border Style="{StaticResource NavRow}"><StackPanel Orientation="Horizontal" VerticalAlignment="Center" Margin="14,0,0,0"><TextBlock Text="&#xE73E;" FontFamily="{StaticResource Icons}" FontSize="15" Foreground="{StaticResource TextSecondary}" Width="26"/><TextBlock Text="Verify" FontSize="13" Foreground="{StaticResource TextSecondary}" VerticalAlignment="Center"/></StackPanel></Border>
                    <Border Style="{StaticResource NavRow}" Margin="0,6,0,0"><StackPanel Orientation="Horizontal" VerticalAlignment="Center" Margin="14,0,0,0"><TextBlock Text="&#xE713;" FontFamily="{StaticResource Icons}" FontSize="15" Foreground="{StaticResource TextSecondary}" Width="26"/><TextBlock Text="Settings" FontSize="13" Foreground="{StaticResource TextSecondary}" VerticalAlignment="Center"/></StackPanel></Border>
                </StackPanel>
            </ScrollViewer>

            <!-- CONTENT (Home) -->
            <ScrollViewer Grid.Column="1" VerticalScrollBarVisibility="Auto">
                <StackPanel Margin="36,26,36,20" MaxWidth="980" HorizontalAlignment="Left">
                    <TextBlock Text="Akari Tool" FontSize="30" FontWeight="Bold" Foreground="{StaticResource TextPrimary}"/>
                    <TextBlock Text="Your control center for Windows — optimization, software &amp; utilities" FontSize="13" Foreground="{StaticResource TextTertiary}" Margin="0,3,0,18"/>

                    <Border Background="{StaticResource CardBg}" BorderBrush="{StaticResource CardBorder}" BorderThickness="1" CornerRadius="10" Padding="22,18,22,18" Margin="0,0,0,14">
                        <StackPanel>
                            <TextBlock x:Name="TxtEdition" Text="Windows" FontSize="21" FontWeight="Bold" Foreground="{StaticResource TextPrimary}"/>
                            <TextBlock x:Name="TxtVersion" Text="Version" FontSize="12.5" Foreground="{StaticResource TextSecondary}" Margin="0,3,0,0"/>
                            <Border Height="1" Background="{StaticResource CardBorder}" Margin="0,16,0,16"/>
                            <Grid>
                                <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="*"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
                                <StackPanel Grid.Column="0"><TextBlock Text="PROCESSOR" FontSize="10" FontWeight="Medium" Foreground="{StaticResource TextTertiary}"/><TextBlock x:Name="TxtCpu" Text="—" FontSize="13" Foreground="{StaticResource TextPrimary}" Margin="0,4,0,0" TextWrapping="Wrap"/></StackPanel>
                                <StackPanel Grid.Column="1"><TextBlock Text="GRAPHICS" FontSize="10" FontWeight="Medium" Foreground="{StaticResource TextTertiary}"/><TextBlock x:Name="TxtGpu" Text="—" FontSize="13" Foreground="{StaticResource TextPrimary}" Margin="0,4,0,0" TextWrapping="Wrap"/></StackPanel>
                                <StackPanel Grid.Column="2"><TextBlock Text="MEMORY" FontSize="10" FontWeight="Medium" Foreground="{StaticResource TextTertiary}"/><TextBlock x:Name="TxtMem" Text="—" FontSize="13" Foreground="{StaticResource TextPrimary}" Margin="0,4,0,0" TextWrapping="Wrap"/></StackPanel>
                            </Grid>
                        </StackPanel>
                    </Border>

                    <Border Background="{StaticResource CardBg}" BorderBrush="{StaticResource CardBorder}" BorderThickness="1" CornerRadius="8" Height="44" Margin="0,0,0,20">
                        <Grid Margin="16,0,14,0">
                            <TextBlock Text="Search all tweaks across every tab..." FontSize="13.5" Foreground="{StaticResource TextTertiary}" VerticalAlignment="Center"/>
                            <TextBlock Text="&#xE721;" FontFamily="{StaticResource Icons}" FontSize="15" Foreground="{StaticResource TextTertiary}" HorizontalAlignment="Right" VerticalAlignment="Center"/>
                        </Grid>
                    </Border>

                    <TextBlock Text="SOFTWARE" FontSize="11" FontWeight="SemiBold" Foreground="{StaticResource TextTertiary}" Margin="2,0,0,10"/>
                    <UniformGrid Columns="3" Margin="0,0,0,18">
                        <Border Style="{StaticResource Card}" Margin="0,0,12,0"><Grid><Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions><Border Grid.Column="0" Width="36" Height="36" CornerRadius="8" Background="#22FFFFFF" VerticalAlignment="Top" Margin="0,0,12,0"><TextBlock Text="&#xE8FD;" FontFamily="{StaticResource Icons}" FontSize="17" Foreground="{StaticResource TextPrimary}" HorizontalAlignment="Center" VerticalAlignment="Center"/></Border><StackPanel Grid.Column="1" VerticalAlignment="Center"><TextBlock Text="Windows Apps" FontSize="14" FontWeight="SemiBold" Foreground="{StaticResource TextPrimary}"/><TextBlock Text="Manage built-in Windows apps &amp; features" FontSize="11.5" Foreground="{StaticResource TextTertiary}" TextWrapping="Wrap" Margin="0,2,0,0"/></StackPanel><TextBlock Grid.Column="2" Text="&#xE76C;" FontFamily="{StaticResource Icons}" FontSize="12" Foreground="{StaticResource TextTertiary}" VerticalAlignment="Center"/></Grid></Border>
                        <Border Style="{StaticResource Card}" Margin="0,0,12,0"><Grid><Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions><Border Grid.Column="0" Width="36" Height="36" CornerRadius="8" Background="#22FFFFFF" VerticalAlignment="Top" Margin="0,0,12,0"><TextBlock Text="&#xE896;" FontFamily="{StaticResource Icons}" FontSize="17" Foreground="{StaticResource TextPrimary}" HorizontalAlignment="Center" VerticalAlignment="Center"/></Border><StackPanel Grid.Column="1" VerticalAlignment="Center"><TextBlock Text="External Apps" FontSize="14" FontWeight="SemiBold" Foreground="{StaticResource TextPrimary}"/><TextBlock Text="Install apps via WinGet" FontSize="11.5" Foreground="{StaticResource TextTertiary}" TextWrapping="Wrap" Margin="0,2,0,0"/></StackPanel><TextBlock Grid.Column="2" Text="&#xE76C;" FontFamily="{StaticResource Icons}" FontSize="12" Foreground="{StaticResource TextTertiary}" VerticalAlignment="Center"/></Grid></Border>
                        <Border Style="{StaticResource Card}"><Grid><Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions><Border Grid.Column="0" Width="36" Height="36" CornerRadius="8" Background="#22FFFFFF" VerticalAlignment="Top" Margin="0,0,12,0"><TextBlock Text="&#xE74D;" FontFamily="{StaticResource Icons}" FontSize="17" Foreground="{StaticResource TextPrimary}" HorizontalAlignment="Center" VerticalAlignment="Center"/></Border><StackPanel Grid.Column="1" VerticalAlignment="Center"><TextBlock Text="Debloat" FontSize="14" FontWeight="SemiBold" Foreground="{StaticResource TextPrimary}"/><TextBlock Text="Remove bloatware, Edge &amp; OneDrive" FontSize="11.5" Foreground="{StaticResource TextTertiary}" TextWrapping="Wrap" Margin="0,2,0,0"/></StackPanel><TextBlock Grid.Column="2" Text="&#xE76C;" FontFamily="{StaticResource Icons}" FontSize="12" Foreground="{StaticResource TextTertiary}" VerticalAlignment="Center"/></Grid></Border>
                    </UniformGrid>

                    <TextBlock Text="OPTIMIZE" FontSize="11" FontWeight="SemiBold" Foreground="{StaticResource TextTertiary}" Margin="2,0,0,10"/>
                    <UniformGrid Columns="3" Rows="2" Margin="0,0,0,18">
                        <Border Style="{StaticResource Card}" Margin="0,0,12,12"><Grid><Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions><Border Grid.Column="0" Width="36" Height="36" CornerRadius="8" Background="#22FFFFFF" VerticalAlignment="Top" Margin="0,0,12,0"><TextBlock Text="&#xE735;" FontFamily="{StaticResource Icons}" FontSize="17" Foreground="{StaticResource Accent}" HorizontalAlignment="Center" VerticalAlignment="Center"/></Border><StackPanel Grid.Column="1" VerticalAlignment="Center"><TextBlock Text="AkariOS" FontSize="14" FontWeight="SemiBold" Foreground="{StaticResource TextPrimary}"/><TextBlock Text="AkariOS presets &amp; environment tools" FontSize="11.5" Foreground="{StaticResource TextTertiary}" TextWrapping="Wrap" Margin="0,2,0,0"/></StackPanel><TextBlock Grid.Column="2" Text="&#xE76C;" FontFamily="{StaticResource Icons}" FontSize="12" Foreground="{StaticResource TextTertiary}" VerticalAlignment="Center"/></Grid></Border>
                        <Border Style="{StaticResource Card}" Margin="0,0,12,12"><Grid><Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions><Border Grid.Column="0" Width="36" Height="36" CornerRadius="8" Background="#22FFFFFF" VerticalAlignment="Top" Margin="0,0,12,0"><TextBlock Text="&#xE7FC;" FontFamily="{StaticResource Icons}" FontSize="17" Foreground="{StaticResource TextPrimary}" HorizontalAlignment="Center" VerticalAlignment="Center"/></Border><StackPanel Grid.Column="1" VerticalAlignment="Center"><TextBlock Text="Gaming" FontSize="14" FontWeight="SemiBold" Foreground="{StaticResource TextPrimary}"/><TextBlock Text="Latency, GPU &amp; gaming performance tweaks" FontSize="11.5" Foreground="{StaticResource TextTertiary}" TextWrapping="Wrap" Margin="0,2,0,0"/></StackPanel><TextBlock Grid.Column="2" Text="&#xE76C;" FontFamily="{StaticResource Icons}" FontSize="12" Foreground="{StaticResource TextTertiary}" VerticalAlignment="Center"/></Grid></Border>
                        <Border Style="{StaticResource Card}" Margin="0,0,0,12"><Grid><Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions><Border Grid.Column="0" Width="36" Height="36" CornerRadius="8" Background="#22FFFFFF" VerticalAlignment="Top" Margin="0,0,12,0"><TextBlock Text="&#xE72E;" FontFamily="{StaticResource Icons}" FontSize="17" Foreground="{StaticResource TextPrimary}" HorizontalAlignment="Center" VerticalAlignment="Center"/></Border><StackPanel Grid.Column="1" VerticalAlignment="Center"><TextBlock Text="Privacy" FontSize="14" FontWeight="SemiBold" Foreground="{StaticResource TextPrimary}"/><TextBlock Text="Telemetry, tracking &amp; privacy hardening" FontSize="11.5" Foreground="{StaticResource TextTertiary}" TextWrapping="Wrap" Margin="0,2,0,0"/></StackPanel><TextBlock Grid.Column="2" Text="&#xE76C;" FontFamily="{StaticResource Icons}" FontSize="12" Foreground="{StaticResource TextTertiary}" VerticalAlignment="Center"/></Grid></Border>
                        <Border Style="{StaticResource Card}" Margin="0,0,12,0"><Grid><Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions><Border Grid.Column="0" Width="36" Height="36" CornerRadius="8" Background="#22FFFFFF" VerticalAlignment="Top" Margin="0,0,12,0"><TextBlock Text="&#xE895;" FontFamily="{StaticResource Icons}" FontSize="17" Foreground="{StaticResource TextPrimary}" HorizontalAlignment="Center" VerticalAlignment="Center"/></Border><StackPanel Grid.Column="1" VerticalAlignment="Center"><TextBlock Text="Update" FontSize="14" FontWeight="SemiBold" Foreground="{StaticResource TextPrimary}"/><TextBlock Text="Windows Update behavior &amp; policies" FontSize="11.5" Foreground="{StaticResource TextTertiary}" TextWrapping="Wrap" Margin="0,2,0,0"/></StackPanel><TextBlock Grid.Column="2" Text="&#xE76C;" FontFamily="{StaticResource Icons}" FontSize="12" Foreground="{StaticResource TextTertiary}" VerticalAlignment="Center"/></Grid></Border>
                        <Border Style="{StaticResource Card}" Margin="0,0,12,0"><Grid><Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions><Border Grid.Column="0" Width="36" Height="36" CornerRadius="8" Background="#22FFFFFF" VerticalAlignment="Top" Margin="0,0,12,0"><TextBlock Text="&#xE7E7;" FontFamily="{StaticResource Icons}" FontSize="17" Foreground="{StaticResource TextPrimary}" HorizontalAlignment="Center" VerticalAlignment="Center"/></Border><StackPanel Grid.Column="1" VerticalAlignment="Center"><TextBlock Text="Notifications" FontSize="14" FontWeight="SemiBold" Foreground="{StaticResource TextPrimary}"/><TextBlock Text="Notification, tips &amp; suggestion controls" FontSize="11.5" Foreground="{StaticResource TextTertiary}" TextWrapping="Wrap" Margin="0,2,0,0"/></StackPanel><TextBlock Grid.Column="2" Text="&#xE76C;" FontFamily="{StaticResource Icons}" FontSize="12" Foreground="{StaticResource TextTertiary}" VerticalAlignment="Center"/></Grid></Border>
                        <Border Style="{StaticResource Card}"><Grid><Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions><Border Grid.Column="0" Width="36" Height="36" CornerRadius="8" Background="#22FFFFFF" VerticalAlignment="Top" Margin="0,0,12,0"><TextBlock Text="&#xE945;" FontFamily="{StaticResource Icons}" FontSize="17" Foreground="{StaticResource TextPrimary}" HorizontalAlignment="Center" VerticalAlignment="Center"/></Border><StackPanel Grid.Column="1" VerticalAlignment="Center"><TextBlock Text="Power" FontSize="14" FontWeight="SemiBold" Foreground="{StaticResource TextPrimary}"/><TextBlock Text="Power plans &amp; performance settings" FontSize="11.5" Foreground="{StaticResource TextTertiary}" TextWrapping="Wrap" Margin="0,2,0,0"/></StackPanel><TextBlock Grid.Column="2" Text="&#xE76C;" FontFamily="{StaticResource Icons}" FontSize="12" Foreground="{StaticResource TextTertiary}" VerticalAlignment="Center"/></Grid></Border>
                    </UniformGrid>

                    <TextBlock Text="ADVANCED" FontSize="11" FontWeight="SemiBold" Foreground="{StaticResource TextTertiary}" Margin="2,0,0,10"/>
                    <UniformGrid Columns="3" Margin="0,0,0,12">
                        <Border Style="{StaticResource Card}" Margin="0,0,12,0"><Grid><Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions><Border Grid.Column="0" Width="36" Height="36" CornerRadius="8" Background="#22FFFFFF" VerticalAlignment="Top" Margin="0,0,12,0"><TextBlock Text="&#xEC7A;" FontFamily="{StaticResource Icons}" FontSize="17" Foreground="{StaticResource TextPrimary}" HorizontalAlignment="Center" VerticalAlignment="Center"/></Border><StackPanel Grid.Column="1" VerticalAlignment="Center"><TextBlock Text="Advanced Tools" FontSize="14" FontWeight="SemiBold" Foreground="{StaticResource TextPrimary}"/><TextBlock Text="ISO wizard &amp; unattended setup builder" FontSize="11.5" Foreground="{StaticResource TextTertiary}" TextWrapping="Wrap" Margin="0,2,0,0"/></StackPanel><TextBlock Grid.Column="2" Text="&#xE76C;" FontFamily="{StaticResource Icons}" FontSize="12" Foreground="{StaticResource TextTertiary}" VerticalAlignment="Center"/></Grid></Border>
                        <Border Style="{StaticResource Card}" Margin="0,0,12,0"><Grid><Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions><Border Grid.Column="0" Width="36" Height="36" CornerRadius="8" Background="#22FFFFFF" VerticalAlignment="Top" Margin="0,0,12,0"><TextBlock Text="&#xE777;" FontFamily="{StaticResource Icons}" FontSize="17" Foreground="{StaticResource TextPrimary}" HorizontalAlignment="Center" VerticalAlignment="Center"/></Border><StackPanel Grid.Column="1" VerticalAlignment="Center"><TextBlock Text="Backup &amp; Restore" FontSize="14" FontWeight="SemiBold" Foreground="{StaticResource TextPrimary}"/><TextBlock Text="Export &amp; import your tweak configuration" FontSize="11.5" Foreground="{StaticResource TextTertiary}" TextWrapping="Wrap" Margin="0,2,0,0"/></StackPanel><TextBlock Grid.Column="2" Text="&#xE76C;" FontFamily="{StaticResource Icons}" FontSize="12" Foreground="{StaticResource TextTertiary}" VerticalAlignment="Center"/></Grid></Border>
                        <Border Style="{StaticResource Card}"><Grid><Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions><Border Grid.Column="0" Width="36" Height="36" CornerRadius="8" Background="#22FFFFFF" VerticalAlignment="Top" Margin="0,0,12,0"><TextBlock Text="&#xE7BA;" FontFamily="{StaticResource Icons}" FontSize="17" Foreground="{StaticResource Accent}" HorizontalAlignment="Center" VerticalAlignment="Center"/></Border><StackPanel Grid.Column="1" VerticalAlignment="Center"><TextBlock Text="Verify System" FontSize="14" FontWeight="SemiBold" Foreground="{StaticResource TextPrimary}"/><TextBlock Text="Track tweaks Windows reverted" FontSize="11.5" Foreground="{StaticResource TextTertiary}" TextWrapping="Wrap" Margin="0,2,0,0"/></StackPanel><TextBlock Grid.Column="2" Text="&#xE76C;" FontFamily="{StaticResource Icons}" FontSize="12" Foreground="{StaticResource TextTertiary}" VerticalAlignment="Center"/></Grid></Border>
                    </UniformGrid>
                </StackPanel>
            </ScrollViewer>
        </Grid>

        <!-- ══ STATUS BAR ══ -->
        <Border Grid.Row="2" Padding="16,0,16,0">
            <Grid VerticalAlignment="Center">
                <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
                    <Ellipse Width="6" Height="6" Margin="0,0,7,0" Fill="{StaticResource Good}" VerticalAlignment="Center"/>
                    <TextBlock Text="Ready" FontSize="11.5" Foreground="{StaticResource TextSecondary}" VerticalAlignment="Center"/>
                </StackPanel>
                <StackPanel Orientation="Horizontal" HorizontalAlignment="Right" VerticalAlignment="Center">
                    <TextBlock Text="&#xE706;" FontFamily="{StaticResource Icons}" FontSize="13" Foreground="{StaticResource TextSecondary}" VerticalAlignment="Center" Margin="0,0,16,0"/>
                    <TextBlock Text="&#xE70E;" FontFamily="{StaticResource Icons}" FontSize="11" Foreground="{StaticResource TextSecondary}" VerticalAlignment="Center" Margin="0,0,5,0"/>
                    <TextBlock Text="LOG" FontSize="10" Foreground="{StaticResource TextSecondary}" VerticalAlignment="Center" Margin="0,0,16,0"/>
                    <TextBlock Text="WPF · PowerShell · build 1.0" FontSize="10.5" Foreground="{StaticResource TextTertiary}" VerticalAlignment="Center"/>
                </StackPanel>
            </Grid>
        </Border>
    </Grid>
</Window>
'@

$window = [Windows.Markup.XamlReader]::Parse($xaml)

# ── Caption buttons ──────────────────────────────────────────────────────────
$window.FindName('BtnClose').Add_Click({ $window.Close() })
$window.FindName('BtnMin').Add_Click({ $window.WindowState = 'Minimized' })
$window.FindName('BtnMax').Add_Click({
    $window.WindowState = if ($window.WindowState -eq 'Maximized') { 'Normal' } else { 'Maximized' }
})

# ── Dark title bar + Mica (Win11, best-effort) ───────────────────────────────
$window.Add_Loaded({
    try {
        $h = (New-Object System.Windows.Interop.WindowInteropHelper($window)).Handle
        $dark = 1; [Dwm]::DwmSetWindowAttribute($h, 20, [ref]$dark, 4) | Out-Null   # dark title bar
        $m = New-Object Dwm+MARGINS; $m.L = -1; $m.R = -1; $m.T = -1; $m.B = -1
        [Dwm]::DwmExtendFrameIntoClientArea($h, [ref]$m) | Out-Null
        $mica = 2; [Dwm]::DwmSetWindowAttribute($h, 38, [ref]$mica, 4) | Out-Null   # Mica backdrop
        $window.Background = [System.Windows.Media.Brushes]::Transparent
    } catch { }
})

# ── Live system info (the only "real" part — pure read-only WMI/CIM) ──────────
try {
    $os  = Get-CimInstance Win32_OperatingSystem
    $cpu = (Get-CimInstance Win32_Processor | Select-Object -First 1).Name
    $gpu = (Get-CimInstance Win32_VideoController | Where-Object { $_.AdapterRAM -gt 0 -or $_.Name -notmatch 'Basic|Remote' } | Select-Object -First 1).Name
    $ramBytes = (Get-CimInstance Win32_PhysicalMemory | Measure-Object Capacity -Sum).Sum
    $ramGB    = [math]::Round($ramBytes / 1GB)
    $ramSpeed = (Get-CimInstance Win32_PhysicalMemory | Select-Object -First 1 -ExpandProperty Speed)

    $window.FindName('TxtEdition').Text = ($os.Caption -replace '^Microsoft ', '')
    $window.FindName('TxtVersion').Text = "Version $($os.Version) • Build $($os.BuildNumber)"
    $window.FindName('TxtCpu').Text     = ($cpu -replace '\s+', ' ').Trim()
    $window.FindName('TxtGpu').Text     = $gpu
    $window.FindName('TxtMem').Text     = if ($ramSpeed) { "$ramGB GB @ $ramSpeed MHz" } else { "$ramGB GB" }
} catch { }

$window.ShowDialog() | Out-Null
