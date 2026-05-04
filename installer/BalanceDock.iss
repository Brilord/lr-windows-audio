#define MyAppName "BalanceDock"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "BalanceDock"
#define MyAppExeName "BalanceDock.exe"

[Setup]
AppId={{F44A1698-1C0D-46DC-8AD7-514B38C74F99}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\BalanceDock
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=BalanceDockSetup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=..\Assets\BalanceDock.ico
CloseApplications=yes
RestartApplications=no

[Tasks]
Name: "startup"; Description: "Start BalanceDock with Windows"; GroupDescription: "Startup options:"; Flags: unchecked
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcut options:"; Flags: unchecked

[Files]
Source: "..\bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--tray"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "BalanceDock"; ValueData: """{app}\{#MyAppExeName}"" --tray"; Tasks: startup; Flags: uninsdeletevalue

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\BalanceDock"

[Code]
function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{cmd}'), '/c taskkill /IM BalanceDock.exe /F >NUL 2>NUL', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := True;
end;
