; Build with Inno Setup 6.7.3. Payload is prepared by build-release.ps1.
#ifndef AppVersion
  #error AppVersion is required; use build-release.ps1
#endif
#ifndef PayloadDir
  #error PayloadDir is required
#endif
#ifndef OutputDir
  #define OutputDir "..\release"
#endif
#ifdef TestInstall
  #define ProductId "CatEyeScreenRecorder.InstallTest"
  #define ProductName "CatEye Install Test"
  #define OutputName "CatEye-InstallTest"
#else
  #define ProductId "{{CE65BE1E-1322-48D7-961B-8F0348E67E9B}"
  #define ProductName "CatEye Screen Recorder"
  #define OutputName "CatEyeScreenRecorder-Setup-v" + AppVersion + "-Windows-x64"
#endif

[Setup]
AppId={#ProductId}
AppName={#ProductName}
AppVersion={#AppVersion}
AppVerName={#ProductName} {#AppVersion}
AppPublisher=CatEye Screen Recorder
AppPublisherURL=https://github.com/greatworks/CatEyeScreenRecorder
AppSupportURL=https://github.com/greatworks/CatEyeScreenRecorder/issues
AppUpdatesURL=https://github.com/greatworks/CatEyeScreenRecorder/releases
DefaultDirName={localappdata}\Programs\{#ProductName}
DefaultGroupName={#ProductName}
UninstallDisplayName=猫眼录屏 CatEye Screen Recorder {#AppVersion}
UninstallDisplayIcon={app}\CatEyeScreenRecorder.exe
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=6.1sp1
AppMutex=CatEyeScreenRecorder.Running
SetupMutex=CatEyeScreenRecorder.Setup
CloseApplications=no
RestartApplications=no
DisableProgramGroupPage=yes
DisableDirPage=no
UsePreviousAppDir=yes
WizardStyle=modern
SetupIconFile=..\CatEye.ico
OutputDir={#OutputDir}
OutputBaseFilename={#OutputName}
Compression=lzma2
SolidCompression=yes
LZMANumBlockThreads=2
Uninstallable=yes
VersionInfoVersion={#AppVersion}.0

[Languages]
Name: "chinesesimp"; MessagesFile: "Languages\ChineseSimplified.isl"
Name: "chinesetrad"; MessagesFile: "Languages\ChineseTraditional.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "portuguese"; MessagesFile: "compiler:Languages\Portuguese.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "update.config"
; Keep an existing update repository configuration on upgrade.
Source: "{#PayloadDir}\update.config"; DestDir: "{app}"; Flags: onlyifdoesntexist

[Icons]
Name: "{group}\{#ProductName}"; Filename: "{app}\CatEyeScreenRecorder.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\{#ProductName}"; Filename: "{app}\CatEyeScreenRecorder.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\CatEyeScreenRecorder.exe"; Description: "{cm:LaunchProgram,{#ProductName}}"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup(): Boolean;
var
  Release: Cardinal;
  MessageText: String;
begin
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) and (Release >= 379893);
  if not Result then
  begin
    if (ActiveLanguage = 'chinesesimp') or (ActiveLanguage = 'chinesetrad') then
      MessageText := '猫眼录屏需要 .NET Framework 4.5.2 或更高版本。请先通过 Microsoft 官方渠道安装此运行环境，再重新运行安装包。'
    else
      MessageText := 'CatEye requires .NET Framework 4.5.2 or later. Install it from Microsoft, then run this installer again.';
    MsgBox(MessageText, mbError, MB_OK);
  end;
end;

// No UninstallDelete entry: recordings and user preferences are deliberately retained.
