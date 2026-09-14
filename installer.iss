#define MyAppName "RyzerDNS Client"
#define MyAppShortName "RyzerDNS"
#define MyAppVersion "1.0.1"
#define MyAppPublisher "RyzerDNS"
#define MyAppURL "https://ryzerdns.ir"
#define MyAppExeName "RyzerDNS.exe"
#define MyAppCopyright "© 2026 RyzerDNS"
#define MyAppDescription "RyzerDNS Network Optimizer"

[Setup]
; Unique application identifier
AppId={{D37E8614-2C62-4C15-99DF-E31BC8962058}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
AppCopyright={#MyAppCopyright}
AppComments={#MyAppDescription}

; Official Windows Executable Properties (Details tab in Windows Explorer)
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppDescription}
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}
VersionInfoProductTextVersion={#MyAppVersion}
VersionInfoVersion={#MyAppVersion}
VersionInfoTextVersion={#MyAppVersion}
VersionInfoCopyright={#MyAppCopyright}

; Modern User-level directory: %LocalAppData%\Programs\RyzerDNS
; Matches modern apps (Discord, Telegram Desktop) to permit smooth in-app silent auto-updates
DefaultDirName={localappdata}\Programs\{#MyAppShortName}
DefaultGroupName={#MyAppShortName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

; Output installer file
OutputDir=Output
OutputBaseFilename=RyzerDNS-Setup
SetupIconFile=Assets\app.ico
UninstallDisplayIcon={app}\Assets\app.ico

; Modern Compression
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern

; Architecture
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Pack all self-contained published files and directories
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Start Menu Shortcut
Name: "{group}\{#MyAppShortName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\app.ico"
; Desktop Shortcut
Name: "{autodesktop}\{#MyAppShortName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\app.ico"; Tasks: desktopicon
; Uninstaller in Start Menu
Name: "{group}\{cm:UninstallProgram,{#MyAppShortName}}"; Filename: "{uninstallexe}"

[Run]
; Run application upon install completion (shellexec handles Administrator elevation from app.manifest cleanly without error 740)
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent shellexec

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
