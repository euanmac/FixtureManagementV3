; Inno Setup script for the Windows installer (FixtureManagerSetup-<version>.exe).
; Built by .github/workflows/desktop_windows.yml, which passes:
;   /DAppVersion=<ApplicationDisplayVersion from the csproj>  /DSourceDir=<the dotnet publish folder>
; Installs per user (no admin prompt) into %LOCALAPPDATA%\Programs\Fixture Manager, adds a Start menu shortcut
; and an uninstaller (Settings -> Apps). Installing a newer version over an old one upgrades it in place.

#ifndef AppVersion
  #define AppVersion "1.0"
#endif
#ifndef SourceDir
  #error Pass /DSourceDir=<publish folder>
#endif

#define AppName "Fixture Manager"
#define AppExe "FixtureManagement.Desktop.exe"

[Setup]
; Identifies the app to Windows for upgrades/uninstall – never change it.
AppId={{CEEE452C-8E2F-4276-A1DB-3317E8A6B3E1}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
UninstallDisplayIcon={app}\{#AppExe}
OutputBaseFilename=FixtureManagerSetup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; Closes a running copy before upgrading.
CloseApplications=yes

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
; Clear out the previous version's files so removed DLLs don't linger.
Type: filesandordirs; Name: "{app}\*"

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
