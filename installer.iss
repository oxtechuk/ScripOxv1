; =====================================================================
; ScripOx CRM - Inno Setup Script
; Author: Ox Tech (https://oxtech.uk)
; =====================================================================

#define MyAppName "ScripOx CRM"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Ox Tech"
#define MyAppURL "https://oxtech.uk"
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
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline
OutputDir=dist\installer
OutputBaseFilename=ScripOx-Setup-v{#MyAppVersion}
SetupIconFile=ScripOx.Desktop\Resources\Icons\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
CloseApplicationsFilter=ScripOx.exe

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Main application binaries and dependencies
Source: "dist\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; Optional seed database if available
Source: "scripox.db"; DestDir: "{app}"; Flags: onlyifdoesntexist uninsneveruninstall; Check: SeedDbExists

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
function SeedDbExists: Boolean;
begin
  Result := FileExists(ExpandConstant('{src}\scripox.db'));
end;
