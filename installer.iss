; Inno Setup script for DeskCue
; Build with:  "%LocalAppData%\Programs\Inno Setup 6\ISCC.exe" installer.iss
; Produces:    installer\DeskCue-Setup-<version>.exe

#define AppName "DeskCue"
#define AppPublisher "SMIC"
#define AppExe "DeskCue.exe"
; Version comes from the published exe (stamped by the StampBuildVersion MSBuild
; target) so the installer version always matches the app. CI overrides it with
; the exact tag version via /DAppVersion=<x.y.z>. (Requires publish-sc\ to exist.)
#ifndef AppVersion
  #define AppVersion GetVersionNumbersString("publish-sc\" + AppExe)
#endif

[Setup]
; A fixed AppId ties upgrades and uninstall together across versions, and it is the one
; identifier that must NOT change with the DeskCue rename: it is what makes installs from
; before the rename upgrade in place instead of landing a second copy. The renamed exe and
; Run value are cleaned up by [InstallDelete] and the uninstall hook below.
AppId={{B7E5B2A1-4C3D-4E6F-9A2B-1D2E3F4A5B6C}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
OutputDir=installer
OutputBaseFilename=DeskCue-Setup-{#AppVersion}
SetupIconFile=app.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; Per-user install: no admin prompt (matches the app's per-user config & autostart).
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "default"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"; Flags: unchecked
Name: "autostart";   Description: "Run at Windows startup"; GroupDescription: "Startup options:"

[InstallDelete]
; Renamed to DeskCue.exe: drop the old executable and its autostart entry on upgrade so an
; upgraded install does not keep a second, orphaned copy around.
Type: files; Name: "{app}\VirtualDesktopIndicator.exe"
Type: filesandordirs; Name: "{app}\VirtualDesktopIndicator.pdb"

[Files]
Source: "publish-sc\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion
Source: "README.md";    DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}";              Filename: "{app}\{#AppExe}"
Name: "{group}\Uninstall {#AppName}";     Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";        Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; Autostart entry uses the SAME name/format as the app's in-app toggle (StartupManager),
; so the tray checkbox and this stay consistent.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "DeskCue"; \
    ValueData: """{app}\{#AppExe}"""; Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExe}"; Description: "Run {#AppName} now"; \
    Flags: nowait postinstall skipifsilent

[Code]
procedure KillRunning();
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{cmd}'), '/C taskkill /F /IM {#AppExe}', '',
       SW_HIDE, ewWaitUntilTerminated, ResultCode);
  // Also stop a pre-rename instance, whose image name was different.
  Exec(ExpandConstant('{cmd}'), '/C taskkill /F /IM VirtualDesktopIndicator.exe', '',
       SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

// Stop a running instance before overwriting files.
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  KillRunning();
  Result := '';
end;

// Stop a running instance before uninstalling.
function InitializeUninstall(): Boolean;
begin
  KillRunning();
  Result := True;
end;

// Always clean up the autostart entry on uninstall, even if it was enabled
// from inside the app rather than by the installer task.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    RegDeleteValue(HKEY_CURRENT_USER,
      'Software\Microsoft\Windows\CurrentVersion\Run', 'DeskCue');
    // Pre-rename value name, in case this install predates the rename and never ran the
    // in-app migration.
    RegDeleteValue(HKEY_CURRENT_USER,
      'Software\Microsoft\Windows\CurrentVersion\Run', 'VirtualDesktopIndicator');
  end;
end;
