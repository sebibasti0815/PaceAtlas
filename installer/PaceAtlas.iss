#ifndef AppVersion
  #error AppVersion must be passed by build-installer.ps1
#endif
#ifndef PublishDir
  #error PublishDir must be passed by build-installer.ps1
#endif

[Setup]
AppId={{8E5B6994-0C22-4A55-9CA3-5E915A3D9601}
AppName=Pace Atlas
AppVersion={#AppVersion}
AppPublisher=Pace Atlas
DefaultDirName={autopf}\Pace Atlas
DefaultGroupName=Pace Atlas
UninstallDisplayIcon={app}\PaceAtlas.WinUI.exe
OutputDir=..\artifacts\installer
OutputBaseFilename=PaceAtlas-Setup-{#AppVersion}-win-x64
SetupIconFile=..\PaceAtlas.WinUI\app.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
MinVersion=10.0.19041
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"

[Tasks]
Name: "desktopicon"; Description: "Desktopverknüpfung erstellen"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"
Source: "prerequisites\windowsdesktop-runtime-10-x64.exe"; Flags: dontcopy
Source: "prerequisites\WindowsAppRuntimeInstall-x64.exe"; Flags: dontcopy

[InstallDelete]
Type: files; Name: "{app}\PaceAtlas.WinUIPrototype.exe"

[Icons]
Name: "{autoprograms}\Pace Atlas"; Filename: "{app}\PaceAtlas.WinUI.exe"
Name: "{autodesktop}\Pace Atlas"; Filename: "{app}\PaceAtlas.WinUI.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\PaceAtlas.WinUI.exe"; Description: "Pace Atlas starten"; Flags: nowait postinstall skipifsilent; Check: CanLaunch

[Code]
var
  DependenciesNeedRestart: Boolean;

function CanLaunch: Boolean;
begin
  Result := not DependenciesNeedRestart;
end;

function InstallPrerequisite(const FileName, Arguments, Description: String;
  var NeedsRestart: Boolean): String;
var
  ExitCode: Integer;
begin
  Result := '';
  ExtractTemporaryFile(FileName);
  if not Exec(ExpandConstant('{tmp}\' + FileName), Arguments, '', SW_HIDE,
    ewWaitUntilTerminated, ExitCode) then
  begin
    Result := Description + ' konnte nicht gestartet werden: ' + SysErrorMessage(ExitCode);
    Exit;
  end;
  if ExitCode = 3010 then
  begin
    NeedsRestart := True;
    DependenciesNeedRestart := True;
  end
  else if ExitCode <> 0 then
    Result := Description + ' konnte nicht installiert werden (Code ' + IntToStr(ExitCode) + ').';
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := InstallPrerequisite('windowsdesktop-runtime-10-x64.exe',
    '/install /quiet /norestart', '.NET Desktop Runtime 10 (x64)', NeedsRestart);
  if Result <> '' then Exit;
  Result := InstallPrerequisite('WindowsAppRuntimeInstall-x64.exe',
    '--quiet', 'Windows App SDK Runtime 1.8 (x64)', NeedsRestart);
end;
