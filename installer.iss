; =====================================================================
; ScripOx CRM - Inno Setup Script
; Author: Ox Tech (https://oxtech.uk)
; Contact: +201008616682
; =====================================================================

#define MyAppName "ScripOx"
#ifndef MyAppVersion
#define MyAppVersion "1.1.2"
#endif
#define MyAppPublisher "Ox Tech"
#define MyAppURL "https://oxtech.uk"
#define MyAppSupportPhone "+201008616682"
#define MyAppExeName "ScripOx.exe"

[Setup]
AppId={{8B7B2E5C-D96B-44D1-A12E-2A42137E7791}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
AppSupportPhone={#MyAppSupportPhone}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription="ScripOx CRM - Lead & Market Intelligence Platform by Ox Tech"
VersionInfoVersion={#MyAppVersion}
VersionInfoTextVersion={#MyAppVersion}
VersionInfoCopyright="© 2025-2026 Ox Tech (oxtech.uk)"
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=no
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline
OutputDir=dist\installer
OutputBaseFilename=ScripOx-Setup-v{#MyAppVersion}
SetupIconFile=ScripOx.Desktop\Resources\Icons\app.ico
UninstallDisplayName={#MyAppName} - {#MyAppPublisher}
UninstallDisplayIcon={app}\{#MyAppExeName}
CreateUninstallRegKey=yes
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
CloseApplicationsFilter=ScripOx.exe

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Main application binaries and dependencies
Source: "dist\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; Optional seed database if available
Source: "scripox.db"; DestDir: "{app}"; Flags: onlyifdoesntexist uninsneveruninstall; Check: SeedDbExists

[Icons]
; Start Menu Group (Folder) Shortcuts
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{group}\Ox Tech Website"; Filename: "{#MyAppURL}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -Command ""Invoke-WebRequest -Uri 'https://go.microsoft.com/fwlink/p/?LinkId=2124703' -OutFile '$env:TEMP\MicrosoftEdgeWebview2Setup.exe'; Start-Process '$env:TEMP\MicrosoftEdgeWebview2Setup.exe' -ArgumentList '/silent /install' -Wait"""; StatusMsg: "Installing Microsoft Edge WebView2 Runtime..."; Flags: runhidden; Check: WebView2NotInstalled
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
function WebView2NotInstalled: Boolean;
var
  Version: String;
begin
  Result := not (
    RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-F55F-4EBE-9E02-9F642301A334}', 'pv', Version) or
    RegQueryStringValue(HKLM, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-F55F-4EBE-9E02-9F642301A334}', 'pv', Version) or
    RegQueryStringValue(HKCU, 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) or
    RegQueryStringValue(HKCU, 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-F55F-4EBE-9E02-9F642301A334}', 'pv', Version)
  );
end;

function SeedDbExists: Boolean;
begin
  Result := FileExists(ExpandConstant('{src}\scripox.db'));
end;

