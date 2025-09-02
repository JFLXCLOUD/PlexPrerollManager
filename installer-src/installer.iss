; PlexPrerollManager Installer Script
; This script creates a professional Windows installer

#define MyAppName "PlexPrerollManager"
#define MyAppVersion "2.1.0"
#define MyAppPublisher "JFLXCLOUD"
#define MyAppURL "https://github.com/JFLXCLOUD/PlexPrerollManager"
#define MyAppExeName "PlexPrerollManager.exe"

; Build type detection
#ifdef FrameworkDependent
  #define BuildType "Framework-Dependent"
  #define PublishDir "publish-framework"
#else
  #define BuildType "Self-Contained"
  #define PublishDir "..\publish"
#endif

[Setup]
; NOTE: The value of AppId uniquely identifies this application.
; Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{9EFFC99E-FEA5-416C-A24E-85A186EDD645}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
;AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={pf}\{#MyAppName}
DisableProgramGroupPage=yes
//LicenseFile=LICENSE
OutputDir=..\installer
OutputBaseFilename=PlexPrerollManager-Setup-{#MyAppVersion}-{#BuildType}
SetupIconFile=icon.ico
Compression=lzma
SolidCompression=yes
PrivilegesRequired=admin
; Upgrade handling
AppVerName={#MyAppName} {#MyAppVersion}
VersionInfoVersion={#MyAppVersion}
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "service"; Description: "Install as Windows service (recommended)"; GroupDescription: "Service Installation"

[Files]
; Application executable and dependencies
; NOTE: Run build-installer.bat or build-installer-framework.bat first to create the publish directory
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; Web interface files
Source: "..\dashboard.html"; DestDir: "{app}\web"; Flags: ignoreversion
Source: "..\scheduling-dashboard.html"; DestDir: "{app}\web"; Flags: ignoreversion

; Configuration files - preserve existing if present
Source: "..\appsettings.json"; DestDir: "{app}"; Flags: ignoreversion; Check: not FileExists(ExpandConstant('{app}\appsettings.json'))

; Default configuration template (always install as backup)
Source: "..\appsettings.json"; DestDir: "{app}\config"; DestName: "appsettings.default.json"; Flags: ignoreversion

; Create data directories
Source: "..\appsettings.json"; DestDir: "{app}\data"; DestName: ".gitkeep"; Flags: ignoreversion

[Icons]
Name: "{commonprograms}\{#MyAppName}"; Filename: "http://localhost:8089"; IconFilename: "{app}\{#MyAppExeName}"; IconIndex: 0
Name: "{commondesktop}\{#MyAppName}"; Filename: "http://localhost:8089"; Tasks: desktopicon; IconFilename: "{app}\{#MyAppExeName}"; IconIndex: 0

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent runascurrentuser

[Code]
var
  ServicePage: TInputOptionWizardPage;

function GetInstallDate(Param: String): String;
begin
  Result := GetDateTimeString('yyyy-mm-dd hh:nn:ss', #0, #0);
end;

function IsDotNetInstalled(): Boolean;
var
  ResultCode: Integer;
begin
  // Check if .NET 9.0 or later is installed
  Result := Exec(ExpandConstant('{sys}\cmd.exe'), '/c dotnet --version 2>nul | findstr "^9\." >nul', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  if ResultCode = 0 then
    Result := True
  else
    Result := False;
end;

procedure InitializeWizard;
begin
  ServicePage := CreateInputOptionPage(wpSelectTasks,
    'Service Installation', 'How would you like to run PlexPrerollManager?',
    'Choose whether to install as a Windows service or run manually.',
    True, False);

  ServicePage.Add('Install as Windows service (recommended)');
  ServicePage.Add('Run manually (advanced users)');

  ServicePage.Values[0] := True;
end;

function IsUpgrade(): Boolean;
var
  sPrevPath: String;
begin
  sPrevPath := WizardForm.PrevAppDir;
  Result := (sPrevPath <> '');
end;

function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
  UninstallString: String;
  ExistingVersion: String;
  MessageText: String;
begin
  // Check if application is already installed (multiple detection methods)
  if RegQueryStringValue(HKLM, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#MyAppName}_is1', 'UninstallString', UninstallString) or
     RegQueryStringValue(HKLM, 'Software\{#MyAppName}', 'InstallPath', UninstallString) then
  begin
    // Get existing version if available
    RegQueryStringValue(HKLM, 'Software\{#MyAppName}', 'Version', ExistingVersion);

    // Build message with version info
    MessageText := '{#MyAppName} is already installed on this system.';
    if ExistingVersion <> '' then
      MessageText := MessageText + #13#10 + 'Current version: ' + ExistingVersion;
    MessageText := MessageText + #13#10 + 'New version: {#MyAppVersion}' + #13#10 + #13#10 +
                   'Would you like to upgrade to the new version?' + #13#10 +
                   'Your configuration and data will be preserved.' + #13#10 + #13#10 +
                   'Click Yes to upgrade (recommended),' + #13#10 +
                   'Click No to install alongside existing version.';

    if MsgBox(MessageText, mbConfirmation, MB_YESNO) = IDYES then
    begin
      // Stop existing service before upgrade
      Exec(ExpandConstant('{sys}\sc.exe'), 'stop PlexPrerollManager', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

      // Note: We don't automatically uninstall to preserve user data
      // The new installation will overwrite files but preserve data directories
      MsgBox('Existing service stopped. Proceeding with upgrade installation.' + #13#10 +
             'Your configuration and data will be preserved.', mbInformation, MB_OK);
    end;
  end;

  Result := True;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;

  if CurPageID = wpReady then
  begin
    // Check .NET installation before proceeding
    if not IsDotNetInstalled() then
    begin
      if MsgBox('Warning: .NET 9.0 or later was not detected on this system.' + #13#10 +
                'PlexPrerollManager requires .NET 9.0 to run.' + #13#10 + #13#10 +
                'Would you like to continue with the installation anyway?' + #13#10 +
                '(You will need to install .NET 9.0 separately)',
                mbConfirmation, MB_YESNO) = IDNO then
      begin
        Result := False;
        Exit;
      end;
    end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  UpgradeMessage: String;
  ConfigPreserved: String;
begin
  if CurStep = ssPostInstall then
  begin
    // Determine if this is an upgrade
    if IsUpgrade() then
      UpgradeMessage := ' upgraded'
    else
      UpgradeMessage := ' installed';

    // Check if configuration was preserved
    if FileExists(ExpandConstant('{app}\appsettings.json')) then
      ConfigPreserved := #13#10 + 'Your existing configuration has been preserved.' + #13#10 +
                        'A default configuration template is available as appsettings.default.json.'
    else
      ConfigPreserved := '';

    if ServicePage.Values[0] then
    begin
      // Install as service
      Exec(ExpandConstant('{sys}\sc.exe'), 'create PlexPrerollManager binPath= "' + ExpandConstant('{app}\{#MyAppExeName}') + '" start= auto', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
      Exec(ExpandConstant('{sys}\sc.exe'), 'start PlexPrerollManager', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
      MsgBox('PlexPrerollManager has been' + UpgradeMessage + ' as a Windows service and started automatically.' + #13#10 +
             'You can access it at: http://localhost:8089' + ConfigPreserved, mbInformation, MB_OK);
    end else begin
      MsgBox('PlexPrerollManager has been' + UpgradeMessage + '.' + #13#10 +
             'You can start it manually by running the executable or access it at: http://localhost:8089' + ConfigPreserved, mbInformation, MB_OK);
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // Stop and remove service if it exists
    Exec(ExpandConstant('{sys}\sc.exe'), 'stop PlexPrerollManager', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec(ExpandConstant('{sys}\sc.exe'), 'delete PlexPrerollManager', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;

[Registry]
; Store installation information for upgrade detection
Root: HKLM; Subkey: "Software\{#MyAppName}"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\{#MyAppName}"; ValueType: string; ValueName: "Version"; ValueData: "{#MyAppVersion}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\{#MyAppName}"; ValueType: string; ValueName: "InstallDate"; ValueData: "{code:GetInstallDate}"; Flags: uninsdeletekey

[UninstallDelete]
Type: filesandordirs; Name: "{app}\*"