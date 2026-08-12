; Akari Tool — Inno Setup script
; Compile with:  ISCC.exe /DMyAppVersion=2.0.0 installer\AkariTool.iss
; (build-installer.ps1 does this for you)

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif

#define MyAppName      "Akari Tool"
#define MyAppExeName   "AkariTool.exe"
#define MyAppPublisher "isleap"
#define MyAppURL       "https://github.com/isleap9/Akari-Tool"
; Unpackaged WinUI 3, fully self-contained publish output produced by
; build-installer.ps1 (VS MSBuild /t:Publish, x64 Release). This is the whole
; runtime payload — the .NET 10 runtime AND the Windows App SDK runtime — not just
; AkariTool.exe. Path uses the WinUI TFM (net10.0-windows10.0.26100.0) and the
; x64 platform sub-dir, unlike the old WPF output path.
#define PublishDir     "..\bin\x64\Release\net10.0-windows10.0.26100.0\win-x64\publish"

; Fail the compile early with a clear message if the payload was not published,
; rather than emitting an installer that is missing the runtime.
#if !FileExists(AddBackslash(SourcePath) + PublishDir + "\" + MyAppExeName)
  #error Publish output not found. Run build-installer.ps1 (it publishes, then compiles this).
#endif
; The app's own PRI is required for WinUI resource resolution; build-installer.ps1
; copies it into PublishDir because the Publish target drops it. Guard against a
; payload assembled without it.
#if !FileExists(AddBackslash(SourcePath) + PublishDir + "\AkariTool.pri")
  #error AkariTool.pri missing from publish payload - run build-installer.ps1 (do not call ISCC directly).
#endif

[Setup]
; NEVER change this AppId — it's how Inno recognises an existing install
; and performs an in-place upgrade instead of a second install.
AppId={{2A713A91-B6C4-4F0A-95B4-0432CA628A66}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\installer-output
OutputBaseFilename=AkariTool-Setup-v{#MyAppVersion}
SetupIconFile=..\Assets\AkariLogo.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; App requires admin anyway (app.manifest), so the installer does too.
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Gracefully close a running Akari Tool during silent self-updates.
CloseApplications=yes
RestartApplications=no
MinVersion=10.0.19041

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; Normal (interactive) install: offer to launch at the end.
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
; Silent self-update (app passes /RELAUNCH=1): relaunch automatically.
Filename: "{app}\{#MyAppExeName}"; Parameters: ""; Flags: nowait; Check: ShouldRelaunch

[Code]
function ShouldRelaunch: Boolean;
begin
  Result := ExpandConstant('{param:RELAUNCH|0}') = '1';
end;
