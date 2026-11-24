#define AppVersion "0.1.0"
[Setup]
AppId={{4A6C8C59-D8CE-48D4-A612-4F3425D29C91}
AppName=N0VA Browser
AppVersion={#AppVersion}
AppPublisher=N0VALID
AppPublisherURL=https://github.com/N0VALID/N0VA
DefaultDirName={localappdata}\Programs\N0VA Browser
DefaultGroupName=N0VA Browser
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
DisableWelcomePage=no
DisableProgramGroupPage=yes
OutputDir=..\artifacts
OutputBaseFilename=N0VA-Browser-Setup-{#AppVersion}-x64
Compression=lzma2
SolidCompression=yes
SetupIconFile=..\src\app.ico
UninstallDisplayIcon={app}\N0VA Browser.exe
CloseApplications=yes
InfoBeforeFile=..\INSTALL.txt

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: checkedonce

[Files]
Source: "..\out\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\vendor\MicrosoftEdgeWebview2Setup.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall
Source: "..\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\N0VA Browser"; Filename: "{app}\N0VA Browser.exe"
Name: "{autodesktop}\N0VA Browser"; Filename: "{app}\N0VA Browser.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\N0VA Browser.exe"; Description: "Open N0VA Browser"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var ResultCode: Integer;
begin
  if CurStep = ssPostInstall then begin
    if not Exec(ExpandConstant('{app}\N0VA Browser.exe'), '--check-runtime', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
      RaiseException('Could not check the browser runtime. Reinstall N0VA Browser.');
    if ResultCode <> 0 then begin
      WizardForm.StatusLabel.Caption := 'Installing Microsoft WebView2 Runtime. This needs an internet connection...';
      if not Exec(ExpandConstant('{tmp}\MicrosoftEdgeWebview2Setup.exe'), '/silent /install', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
        RaiseException('WebView2 setup could not start. Install WebView2 from microsoft.com and run setup again.');
      if not Exec(ExpandConstant('{app}\N0VA Browser.exe'), '--check-runtime', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
        RaiseException('Could not verify WebView2 Runtime.');
      if ResultCode <> 0 then
        RaiseException('WebView2 Runtime is not available. Install it from microsoft.com and run setup again.');
    end;
  end;
end;
