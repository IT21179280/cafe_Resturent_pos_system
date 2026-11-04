; CafePOS single setup: choose Server, Client, or both.
; Build with: "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\CafePOS.iss

#define AppName "CafePOS"
#define AppVersion "1.0.0"
#define AppPublisher "CafePOS"
#define AppId "{{C07CACD4-EA7E-47FC-B50D-AE1936FAC07B}"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\CafePOS
DefaultGroupName=CafePOS
DisableProgramGroupPage=yes
OutputDir=..\artifacts\installer
OutputBaseFilename=CafePOS.Setup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
UninstallDisplayIcon={app}\Client\CafePOS.Desktop.exe
SetupLogging=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Types]
Name: "full"; Description: "Full installation (server and client)"
Name: "server"; Description: "Server only"
Name: "client"; Description: "Client only"
Name: "custom"; Description: "Custom"; Flags: iscustom

[Components]
Name: "server"; Description: "CafePOS Server (API, migrations, LAN access)"; Types: full server custom
Name: "client"; Description: "CafePOS Desktop client (cashier, kitchen, manager)"; Types: full client custom

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"; Components: client
Name: "startservice"; Description: "Install and start the CafePOS API Windows service"; GroupDescription: "Server options:"; Components: server; Flags: checkedonce

[Files]
Source: "..\artifacts\publish\server\*"; DestDir: "{app}\Server"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: server; Excludes: "appsettings.Production.json"
Source: "configure-server.ps1"; DestDir: "{app}\Scripts"; Flags: ignoreversion; Components: server
Source: "install-service.ps1"; DestDir: "{app}\Scripts"; Flags: ignoreversion; Components: server
Source: "..\artifacts\publish\client\*"; DestDir: "{app}\Client"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: client
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion isreadme
Source: "..\docs\*"; DestDir: "{app}\docs"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\CafePOS"; Filename: "{app}\Client\CafePOS.Desktop.exe"; Components: client
Name: "{autodesktop}\CafePOS"; Filename: "{app}\Client\CafePOS.Desktop.exe"; Tasks: desktopicon; Components: client

[Run]
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\Scripts\configure-server.ps1"" -ConfigPath ""{app}\Server\appsettings.Production.json"" -SettingsFile ""{tmp}\cafepos-server-config.txt"""; StatusMsg: "Writing server configuration..."; Flags: runhidden waituntilterminated; Components: server
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""CafePOS API (TCP 5080)"""; Flags: runhidden; Components: server
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""CafePOS API (TCP 5080)"" dir=in action=allow protocol=TCP localport=5080"; StatusMsg: "Opening the firewall for CafePOS..."; Flags: runhidden; Components: server
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\Scripts\install-service.ps1"" -ExePath ""{app}\Server\CafePOS.Api.exe"""; StatusMsg: "Installing the CafePOS API service..."; Flags: runhidden waituntilterminated; Components: server; Tasks: startservice
Filename: "{app}\Client\CafePOS.Desktop.exe"; Description: "Launch CafePOS"; Flags: nowait postinstall skipifsilent; Components: client

[UninstallRun]
Filename: "{sys}\sc.exe"; Parameters: "stop CafePOSApi"; Flags: runhidden waituntilterminated; RunOnceId: "CafePOSStopService"
Filename: "{sys}\sc.exe"; Parameters: "delete CafePOSApi"; Flags: runhidden waituntilterminated; RunOnceId: "CafePOSDeleteService"
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""CafePOS API (TCP 5080)"""; Flags: runhidden; RunOnceId: "CafePOSDeleteFirewall"

[Code]
var
  ConfigPage: TInputQueryWizardPage;

procedure InitializeWizard;
begin
  ConfigPage := CreateInputQueryPage(wpSelectComponents,
    'Server configuration',
    'PostgreSQL connection for the CafePOS API',
    'Enter the PostgreSQL database details for this server. ' +
    'The API will create or update its tables on first start. ' +
    'The database and role must already exist (see docs\DEPLOYMENT.md).');

  ConfigPage.Add('Database host:', False);
  ConfigPage.Add('Database port:', False);
  ConfigPage.Add('Database name:', False);
  ConfigPage.Add('Database user:', False);
  ConfigPage.Add('Database password:', True);

  ConfigPage.Values[0] := 'localhost';
  ConfigPage.Values[1] := '5432';
  ConfigPage.Values[2] := 'cafepos';
  ConfigPage.Values[3] := 'cafepos_app';
  ConfigPage.Values[4] := 'CafePos_App_2026!Dev';
end;

function ServerSelected: Boolean;
begin
  Result := WizardIsComponentSelected('server');
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := (PageID = ConfigPage.ID) and (not ServerSelected);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  Port: Integer;
begin
  Result := True;
  if CurPageID = ConfigPage.ID then
  begin
    if (Trim(ConfigPage.Values[0]) = '') or
       (Trim(ConfigPage.Values[2]) = '') or
       (Trim(ConfigPage.Values[3]) = '') or
       (ConfigPage.Values[4] = '') then
    begin
      MsgBox('Database host, name, user and password are required.', mbError, MB_OK);
      Result := False;
      Exit;
    end;

    Port := StrToIntDef(Trim(ConfigPage.Values[1]), 0);
    if (Port < 1) or (Port > 65535) then
    begin
      MsgBox('Database port must be a number between 1 and 65535.', mbError, MB_OK);
      Result := False;
    end;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Lines: TArrayOfString;
  SettingsPath: String;
begin
  Result := '';
  if not ServerSelected then Exit;

  SetArrayLength(Lines, 5);
  Lines[0] := ConfigPage.Values[0];
  Lines[1] := ConfigPage.Values[1];
  Lines[2] := ConfigPage.Values[2];
  Lines[3] := ConfigPage.Values[3];
  Lines[4] := ConfigPage.Values[4];
  SettingsPath := ExpandConstant('{tmp}\cafepos-server-config.txt');
  if not SaveStringsToFile(SettingsPath, Lines, False) then
  begin
    Result := 'Could not write the temporary server configuration file.';
    Exit;
  end;
  Log('Wrote server settings to ' + SettingsPath);
end;
