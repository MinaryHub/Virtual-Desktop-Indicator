; Inno Setup script for Virtual Desktop Indicator
; Build with:  "%LocalAppData%\Programs\Inno Setup 6\ISCC.exe" installer.iss
; Produces:    installer\VirtualDesktopIndicator-Setup-<version>.exe

#define AppName "Virtual Desktop Indicator"
#define AppVersion "1.0.0"
#define AppPublisher "SMIC"
#define AppExe "VirtualDesktopIndicator.exe"

[Setup]
; A fixed AppId ties upgrades and uninstall together across versions.
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
OutputBaseFilename=VirtualDesktopIndicator-Setup-{#AppVersion}
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
Name: "desktopicon"; Description: "바탕 화면에 바로 가기 만들기"; GroupDescription: "추가 아이콘:"; Flags: unchecked
Name: "autostart";   Description: "Windows 시작 시 자동 실행"; GroupDescription: "시작 옵션:"

[Files]
Source: "publish-sc\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion
Source: "README.md";    DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}";              Filename: "{app}\{#AppExe}"
Name: "{group}\{#AppName} 제거";          Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";        Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; Autostart entry uses the SAME name/format as the app's in-app toggle (StartupManager),
; so the tray checkbox and this stay consistent.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "VirtualDesktopIndicator"; \
    ValueData: """{app}\{#AppExe}"""; Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExe}"; Description: "{#AppName} 지금 실행"; \
    Flags: nowait postinstall skipifsilent

[Code]
procedure KillRunning();
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{cmd}'), '/C taskkill /F /IM {#AppExe}', '',
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
    RegDeleteValue(HKEY_CURRENT_USER,
      'Software\Microsoft\Windows\CurrentVersion\Run', 'VirtualDesktopIndicator');
end;
