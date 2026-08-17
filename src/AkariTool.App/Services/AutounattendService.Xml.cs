using System.IO;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using AkariTool.Tabs;

namespace AkariTool.Services
{
    public partial class AutounattendService
    {
        // ═════════════════════════════════════════════════════════════════
        //  Template — ported from Winhance's autounattend-template.xml,
        //  rebranded to Akari Tool paths. Kept behaviors: Win11 hardware
        //  bypasses, local account OOBE (BypassNRO + adapters disabled),
        //  .NET 3.5 from media, script extraction via <Extensions>.
        // ═════════════════════════════════════════════════════════════════

        private static string ArchComponentSetup(string arch) => $@"    <component name=""Microsoft-Windows-Setup"" processorArchitecture=""{arch}"" publicKeyToken=""31bf3856ad364e35"" language=""neutral"" versionScope=""nonSxS"">
      <UserData>
        <ProductKey>
          <Key>00000-00000-00000-00000-00000</Key>
          <WillShowUI>Always</WillShowUI>
        </ProductKey>
        <AcceptEula>true</AcceptEula>
      </UserData>
      <RunSynchronous>
        <RunSynchronousCommand wcm:action=""add"">
          <Order>1</Order>
          <Description>Order 1-6: Skip Windows 11 Hardware Requirement Checks</Description>
          <Path>reg.exe add ""HKLM\SYSTEM\Setup\LabConfig"" /v BypassTPMCheck /t REG_DWORD /d 1 /f</Path>
        </RunSynchronousCommand>
        <RunSynchronousCommand wcm:action=""add"">
          <Order>2</Order>
          <Path>reg.exe add ""HKLM\SYSTEM\Setup\LabConfig"" /v BypassSecureBootCheck /t REG_DWORD /d 1 /f</Path>
        </RunSynchronousCommand>
        <RunSynchronousCommand wcm:action=""add"">
          <Order>3</Order>
          <Path>reg.exe add ""HKLM\SYSTEM\Setup\LabConfig"" /v BypassStorageCheck /t REG_DWORD /d 1 /f</Path>
        </RunSynchronousCommand>
        <RunSynchronousCommand wcm:action=""add"">
          <Order>4</Order>
          <Path>reg.exe add ""HKLM\SYSTEM\Setup\LabConfig"" /v BypassCPUCheck /t REG_DWORD /d 1 /f</Path>
        </RunSynchronousCommand>
        <RunSynchronousCommand wcm:action=""add"">
          <Order>5</Order>
          <Path>reg.exe add ""HKLM\SYSTEM\Setup\LabConfig"" /v BypassRAMCheck /t REG_DWORD /d 1 /f</Path>
        </RunSynchronousCommand>
        <RunSynchronousCommand wcm:action=""add"">
          <Order>6</Order>
          <Path>reg.exe add ""HKLM\SYSTEM\Setup\LabConfig"" /v BypassDiskCheck /t REG_DWORD /d 1 /f</Path>
        </RunSynchronousCommand>
      </RunSynchronous>
    </component>";

        private static string ArchComponentSpecialize(string arch) => $@"    <component name=""Microsoft-Windows-Deployment"" processorArchitecture=""{arch}"" publicKeyToken=""31bf3856ad364e35"" language=""neutral"" versionScope=""nonSxS"">
      <RunSynchronous>
        <RunSynchronousCommand wcm:action=""add"">
          <Order>1</Order>
          <Description>Loads Scripts in this XML File</Description>
          <Path>powershell.exe -NoProfile -WindowStyle Hidden -Command ""$xml = [xml]::new(); $xml.Load('C:\Windows\Panther\unattend.xml'); $sb = [scriptblock]::Create( $xml.unattend.Extensions.ExtractScript ); Invoke-Command -ScriptBlock $sb -ArgumentList $xml;""</Path>
        </RunSynchronousCommand>
        <RunSynchronousCommand wcm:action=""add"">
          <Order>2</Order>
          <Description>Registry Entry to Create a Local Account during OOBE</Description>
          <Path>reg.exe add ""HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\OOBE"" /v BypassNRO /t REG_DWORD /d 1 /f</Path>
        </RunSynchronousCommand>
        <RunSynchronousCommand wcm:action=""add"">
          <Order>3</Order>
          <Description>Disable All Network Adapters Temporarily so Windows Doesn't Update During OOBE and to Allow Local Account Creation</Description>
          <Path>powershell.exe -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -Command ""Get-NetAdapter | Disable-NetAdapter -Confirm:$false""</Path>
        </RunSynchronousCommand>
        <RunSynchronousCommand wcm:action=""add"">
          <Order>4</Order>
          <Description>Enables .NET Framework 3.5 from Windows Installation Media</Description>
          <Path>powershell.exe -NoProfile -WindowStyle Hidden -Command ""foreach($d in 'C','D','E','F','G','H','I','J','K'){{$src=Join-Path ($d+':') 'sources\sxs';if(Test-Path $src\*.cab){{dism /Online /Enable-Feature /FeatureName:NetFx3 /All /LimitAccess /Source:$src;break}}}}""</Path>
        </RunSynchronousCommand>
        <RunSynchronousCommand wcm:action=""add"">
          <Order>5</Order>
          <Description>Runs Akari Tool Script System Wide Operations like App Uninstalls, HKLM Registry entries etc.</Description>
          <Path>powershell.exe -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File ""{MainScript}""</Path>
        </RunSynchronousCommand>
      </RunSynchronous>
    </component>";

        private static string ArchComponentOobe(string arch) => $@"    <component name=""Microsoft-Windows-Shell-Setup"" processorArchitecture=""{arch}"" publicKeyToken=""31bf3856ad364e35"" language=""neutral"" versionScope=""nonSxS"">
      <OOBE>
        <HideEULAPage>true</HideEULAPage>
        <HideOEMRegistrationScreen>true</HideOEMRegistrationScreen>
        <HideOnlineAccountScreens>true</HideOnlineAccountScreens>
        <HideWirelessSetupInOOBE>true</HideWirelessSetupInOOBE>
        <NetworkLocation>Work</NetworkLocation>
        <ProtectYourPC>3</ProtectYourPC>
      </OOBE>
      <FirstLogonCommands>
        <SynchronousCommand>
          <Order>1</Order>
          <Description>Enables Network Adapters After OOBE Completes</Description>
          <CommandLine>powershell.exe -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -Command ""Get-NetAdapter | Enable-NetAdapter -Confirm:$false""</CommandLine>
        </SynchronousCommand>
      </FirstLogonCommands>
    </component>";

        private static string Template => $@"<?xml version=""1.0"" encoding=""utf-8""?>
<unattend xmlns=""urn:schemas-microsoft-com:unattend"" xmlns:wcm=""http://schemas.microsoft.com/WMIConfig/2002/State"">
  <!-- XML Generated by Akari Tool https://github.com/isleap9/Akari-Tool -->
  <settings pass=""offlineServicing""></settings>
  <settings pass=""windowsPE"">
{ArchComponentSetup("x86")}
{ArchComponentSetup("arm64")}
{ArchComponentSetup("amd64")}
  </settings>
  <settings pass=""generalize""></settings>
  <settings pass=""specialize"">
{ArchComponentSpecialize("x86")}
{ArchComponentSpecialize("arm64")}
{ArchComponentSpecialize("amd64")}
  </settings>
  <settings pass=""auditSystem""></settings>
  <settings pass=""auditUser""></settings>
  <settings pass=""oobeSystem"">
{ArchComponentOobe("x86")}
{ArchComponentOobe("arm64")}
{ArchComponentOobe("amd64")}
  </settings>
  <Extensions xmlns=""urn:akaritool:unattend"">
    <ExtractScript>
param(
    [xml] $Document
);

$scriptsDir = '{ScriptsDir}\';
foreach( $file in $Document.unattend.Extensions.File ) {{
    $path = [System.Environment]::ExpandEnvironmentVariables(
        $file.GetAttribute( 'path' )
    );
    if( $path.StartsWith( $scriptsDir ) ) {{
        mkdir -Path $scriptsDir -ErrorAction 'SilentlyContinue';
    }}
    $encoding = switch( [System.IO.Path]::GetExtension( $path ) ) {{
        {{ $_ -in '.ps1', '.xml' }} {{ [System.Text.Encoding]::UTF8; }}
        {{ $_ -in '.reg', '.vbs', '.js' }} {{ [System.Text.UnicodeEncoding]::new( $false, $true ); }}
        default {{ [System.Text.Encoding]::Default; }}
    }};
    [System.IO.File]::WriteAllBytes( $path, ( $encoding.GetPreamble() + $encoding.GetBytes( $file.InnerText.Trim() ) ) );
}}
		</ExtractScript>
    <File path=""{MainScript}""><!--SCRIPT_PLACEHOLDER--></File>
  </Extensions>
</unattend>
";
    }
}
