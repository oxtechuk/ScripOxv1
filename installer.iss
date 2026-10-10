; =====================================================================
; ScripOx CRM - Inno Setup Script
; Author: Ox Tech (https://oxtech.uk)
; Contact: +201008616682
; =====================================================================

#define MyAppName "ScripOx CRM"
#ifndef MyAppVersion
#define MyAppVersion "1.0.7"
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
DefaultDirName={autopf}\{#MyAppPublisher}\{#MyAppName}
DefaultGroupName={#MyAppPublisher}\{#MyAppName}
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
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
function SeedDbExists: Boolean;
begin
  Result := FileExists(ExpandConstant('{src}\scripox.db'));
end;
