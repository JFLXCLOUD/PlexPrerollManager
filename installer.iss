; PlexPrerollManager Installer Script
; This script creates a professional Windows installer

#define MyAppName "PlexPrerollManager"
#define MyAppVersion "2.0.0"
#define MyAppPublisher "JFLXCLOUD"
#define MyAppURL "https://github.com/JFLXCLOUD/PlexPrerollManager"
#define MyAppExeName "PlexPrerollManager.exe"

; Build type detection
#ifdef FrameworkDependent
  #define BuildType "Framework-Dependent"
  #define PublishDir "publish-framework"
#else
  #define BuildType "Self-Contained"
  #define PublishDir "publish"
#endif

[Setup]
; NOTE: The value of AppId uniquely identifies this application.
; Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{12345678-1234-1234-1234-123456789ABC}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
;AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={pf}\{#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=LICENSE
OutputDir=installer
OutputBaseFilename=PlexPrerollManager-Setup-{#MyAppVersion}-{#BuildType}
SetupIconFile=icon.ico
Compression=lzma
SolidCompression=yes
PrivilegesRequired=admin

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "service"; Description: "Install as Windows service (recommended)"; GroupDescription: "Service Installation"

[Files]
; Application executable and dependencies
; NOTE: Run build-installer.bat or build-installer-gui.bat first to create the publish directory
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; Web interface files
Source: "dashboard.html"; DestDir: "{app}"; Flags: ignoreversion
Source: "scheduling-dashboard.html"; DestDir: "{app}"; Flags: ignoreversion

; Configuration files
Source: "appsettings.json"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{commonprograms}\{#MyAppName}"; Filename: "http://localhost:8089"; IconFilename: "{app}\{#MyAppExeName}"; IconIndex: 0
Name: "{commondesktop}\{#MyAppName}"; Filename: "http://localhost:8089"; Tasks: desktopicon; IconFilename: "{app}\{#MyAppExeName}"; IconIndex: 0

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent runascurrentuser

[Code]
var
  ServicePage: TInputOptionWizardPage;

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
begin
  if CurStep = ssPostInstall then
  begin
    if ServicePage.Values[0] then
    begin
      // Install as service
      Exec(ExpandConstant('{sys}\sc.exe'), 'create PlexPrerollManager binPath= "' + ExpandConstant('{app}\{#MyAppExeName}') + '" start= auto', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
      Exec(ExpandConstant('{sys}\sc.exe'), 'start PlexPrerollManager', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
      MsgBox('PlexPrerollManager has been installed as a Windows service and started automatically.', mbInformation, MB_OK);
    end else begin
      MsgBox('PlexPrerollManager has been installed. You can start it manually by running the executable.', mbInformation, MB_OK);
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

[UninstallDelete]
Type: filesandordirs; Name: "{app}\*"